# AC: Acceleration Oscillator

> "Knowing speed is useful. Knowing whether you're speeding up or slowing down is what keeps you alive."

## Introduction

The Acceleration Oscillator (AC) is Bill Williams' second-derivative momentum indicator. Where the Awesome Oscillator (AO) measures the speed of market momentum, AC measures whether that momentum is accelerating or decelerating. AC is computed as AO minus a 5-period SMA of AO. Zero crossings and color changes signal shifts in market driving force before price reverses.

## Historical Context

Bill Williams introduced AC alongside AO in his "Trading Chaos" methodology. While AO already strips trend by subtracting a slow SMA from a fast SMA (both applied to the bar midpoint), traders found they needed earlier warning of momentum shifts. AC provides exactly that: the rate of change of AO itself. When AC crosses zero from below, the market's driving force is accelerating upward, often preceding AO's own zero crossing by several bars.

## Calculation

The AC indicator is calculated in two stages:

### Stage 1: Awesome Oscillator

$$\text{Median Price} = \frac{\text{High} + \text{Low}}{2}$$

$$\text{AO} = \text{SMA}(\text{Median Price}, \text{fast}) - \text{SMA}(\text{Median Price}, \text{slow})$$

### Stage 2: Acceleration

$$\text{AC} = \text{AO} - \text{SMA}(\text{AO}, \text{acPeriod})$$

Default parameters: fast = 5, slow = 34, acPeriod = 5.

## Interpretation

- **AC > 0 and rising (green):** Bullish acceleration. Momentum is strengthening.
- **AC > 0 and falling (red):** Bullish deceleration. Momentum still positive but weakening.
- **AC < 0 and falling (green to red):** Bearish acceleration. Momentum is weakening further.
- **AC < 0 and rising (red to green):** Bearish deceleration. Downward momentum is weakening.
- **Zero crossings:** Often precede AO zero crossings, providing earlier entry/exit signals.

### Bill Williams' Trading Rules

1. **Buy signal:** AC is green (rising) for two consecutive bars above zero, or three consecutive green bars below zero.
2. **Sell signal:** AC is red (falling) for two consecutive bars below zero, or three consecutive red bars above zero.

## Parameters

| Parameter | Default | Range | Description |
| :-------- | :------ | :---- | :---------- |
| fastPeriod | 5 | > 0 | Fast SMA period for AO calculation |
| slowPeriod | 34 | > fast | Slow SMA period for AO calculation |
| acPeriod | 5 | > 0 | SMA period applied to AO values |

## API

### Streaming

```csharp
var ac = new Ac(fastPeriod: 5, slowPeriod: 34, acPeriod: 5);
TValue result = ac.Update(bar, isNew: true);
```

### Batch (TBarSeries)

```csharp
TSeries results = Ac.Batch(barSeries);
```

### Batch (Span)

```csharp
Ac.Batch(highSpan, lowSpan, outputSpan, fastPeriod: 5, slowPeriod: 34, acPeriod: 5);
```

### Calculate

```csharp
var (results, indicator) = Ac.Calculate(barSeries, fastPeriod: 5, slowPeriod: 34, acPeriod: 5);
```

## Usage

```csharp
// Streaming
var ac = new Ac();
foreach (var bar in bars)
{
    var result = ac.Update(bar);
    if (ac.IsHot && result.Value > 0)
    {
        // Bullish momentum accelerating
    }
}

// Event-driven chaining
ac.Pub += (sender, e) => Console.WriteLine($"AC: {e.Value.Value:F4}");
```

## Performance

| Operation | Complexity | Allocations |
| :-------- | :--------- | :---------- |
| Update (streaming) | O(1) | Zero |
| Batch (Span) | O(n) | ArrayPool |
| Warmup period | slow + ac - 1 | — |

AC uses three internal SMA instances. Each SMA uses a RingBuffer for O(1) sliding window computation. The Batch path uses SIMD-accelerated subtraction via `SimdExtensions.Subtract`.

## Validation

AC is validated via self-consistency (AC = AO - SMA(AO, acPeriod)) and batch/streaming equivalence. No external library implements AC with identical SMA methodology for cross-library validation.

| Test | Status |
| :--- | :----- |
| AC = AO - SMA(AO) identity | Pass |
| Batch/streaming match | Pass |
| Span/TBarSeries match | Pass |
| Determinism | Pass |
| Constant input convergence | Pass (→ 0) |
| Large dataset stability | Pass (5000 bars) |

## Sources

- Williams, Bill. "Trading Chaos." Wiley, 1995.
- Williams, Bill. "New Trading Dimensions." Wiley, 1998.
- [Investopedia: Accelerator Oscillator](https://www.investopedia.com/terms/a/accelerationdeceleration-indicator.asp)
- [TradingView: AC](https://www.tradingview.com/support/solutions/43000501837-accelerator-oscillator-ac/)
