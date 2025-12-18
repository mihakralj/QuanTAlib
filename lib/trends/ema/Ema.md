# EMA: Exponential Moving Average

## What It Does

The Exponential Moving Average (EMA) is one of the most widely used indicators in technical analysis. Unlike the Simple Moving Average (SMA), which treats all data points equally, the EMA assigns exponentially decreasing weights to historical data. This means the most recent price has the biggest impact, and the influence of older prices fades away quickly but never completely disappears. The result is an indicator that tracks the price more closely and reacts faster to trend changes.

## Historical Context

The concept of exponential smoothing originated in signal processing and statistics (specifically control theory) in the 1950s (Robert G. Brown). It was adopted by financial analysts in the 1960s and 70s as computers made iterative calculations feasible. It became a cornerstone of modern technical analysis because it solved the "drop-off effect" of the SMA, where a large price exiting the window would cause the average to jump artificially.

## How It Works

### The Core Idea

Imagine a bucket of water. Every day, you take out 10% of the water and replace it with 10% of new water (the current price). The bucket always contains a mix of the new water and the old water. The water from yesterday is still there (90%), the water from two days ago is there (81%), and so on. This is exactly how an EMA works.

### Mathematical Foundation

The standard formula for EMA is recursive. While often presented as a weighted sum, the computationally optimized form used in high-performance libraries is:

$$EMA_t = EMA_{t-1} + \alpha \cdot (P_t - EMA_{t-1})$$

This form highlights that the EMA simply adjusts the previous value by a fraction of the "error" (the difference between the current price and the previous average).

Where:

- $\alpha$ (alpha) is the smoothing factor, calculated as $\frac{2}{n+1}$.
- $n$ is the period.
- $P_t$ is the current price.

For a 9-period EMA, $\alpha = \frac{2}{10} = 0.2$. This means we correct the previous average by 20% of the distance to the new price.

### The "Infinite" Memory Myth (IIR Filter)

EMA is an Infinite Impulse Response (IIR) filter, meaning theoretically, every past data point contributes something. However, this contribution decays exponentially.

A common misconception is that a 14-period EMA represents the last 14 bars. In reality:

- **1 Period ($N$ bars):** Captures only **~86.5%** of the total weight.
- **1.5 Periods ($\approx 1.5N$ bars):** Needed to reach **95%** confidence (convergence).
- **3 Periods ($3N$ bars):** Needed to reach **99.7%** confidence (mathematical insignificance of older data).

This "long tail" is why EMAs are smoother than SMAs but can sometimes seem to "drag" old volatility forward longer than expected.

### Implementation Details

Our implementation includes a critical improvement over the standard textbook formula: **Zero-Lag Initialization**.

Standard EMAs usually start at 0 or the first price, requiring a long "warmup" period to converge to the correct value. We use a compensator factor that mathematically corrects the early bias, making the EMA statistically valid from the very first bar.

- **Complexity:** O(1) per update.
- **State:** Minimal (Current EMA value + Compensator state).
- **Precision:** Uses double-precision floating point to prevent error accumulation over long datasets.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Shorter (9-12) = Momentum/Scalping; Longer (50-200) = Trend/Support |

**Configuration note:** The 200-day EMA is a standard institutional benchmark for long-term trend direction.

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Single multiplication and addition |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Minimal state (approx 32 bytes) |

## Interpretation

### Trading Signals

#### Trend Identification

- **Bullish:** Price > EMA(50) > EMA(200).
- **Bearish:** Price < EMA(50) < EMA(200).

#### Crossovers

- **Golden Cross:** EMA(50) crosses above EMA(200). A major long-term buy signal.
- **Death Cross:** EMA(50) crosses below EMA(200). A major long-term sell signal.

#### Dynamic Support/Resistance

- In strong trends, price often bounces off the EMA(20) or EMA(50). Traders place limit orders at these levels.

### When It Works Best

- **Trending Markets:** EMA is the king of trend-following indicators. It keeps you in the trade while the trend persists and gets you out relatively quickly when it reverses.

### When It Struggles

- **Sideways Markets:** In a range, the EMA flattens out and price crosses it repeatedly, generating constant false signals (whipsaws).

## Comparison: EMA vs SMA vs WMA

| Aspect | EMA | SMA | WMA |
|--------|-----|-----|-----|
| **Weighting** | Exponential | Equal | Linear |
| **Lag** | Low | High | Moderate |
| **Responsiveness** | High | Low | Moderate |
| **Memory** | Infinite (theoretical) | Finite (window) | Finite (window) |
| **Calculation** | Recursive | Summation | Weighted Sum |

**Summary:** Use EMA for most trading strategies unless you specifically need the stability of an SMA or the specific timing of a WMA.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Compensated Initialization

- **Alternative:** Seed with SMA of first N bars (common in other libraries).
- **Trade-off:** Slightly more complex math (`1/(1-decay)` scaling).
- **Rationale:** The "SMA seed" method is mathematically incorrect for an EMA, creating a permanent offset error that only slowly fades. Our implementation uses a **diminishing compensator**:
  - We track the sum of weights: $S_t = 1 - (1-\alpha)^t$.
  - We scale the partial EMA by $1/S_t$.
  - As $t \to \infty$, $S_t \to 1$, and the compensator naturally disappears.
  - **Result:** The EMA is statistically valid from the very first bar ($EMA_1 = Price_1$), without the arbitrary lag or distortion introduced by an SMA warmup.

### Choice: Alpha-based Constructor

- **Alternative:** Only Period-based constructor.
- **Trade-off:** Exposes internal math parameter.
- **Rationale:** Advanced users (quants) often prefer to tune $\alpha$ directly (e.g., 0.05) rather than converting to periods.

## References

- Brown, Robert G. "Statistical Forecasting for Inventory Control." McGraw-Hill, 1959.
- Appel, Gerald. "Technical Analysis: Power Tools for Active Investors." FT Press, 2005.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var ema = new Ema(period: 14);

// Process each new bar
TValue result = ema.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"EMA: {result.Value:F2}");

// Check if buffer is full
if (ema.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries emaValues = Ema.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Ema.Batch(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var ema = new Ema(14);

// New bar
ema.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
ema.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```
