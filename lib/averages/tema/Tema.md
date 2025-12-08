# TEMA: Triple Exponential Moving Average

## Overview and Purpose

The Triple Exponential Moving Average (TEMA) is a technical indicator developed by Patrick Mulloy in 1994, introduced alongside DEMA. It takes the concept of lag reduction even further than DEMA by using a triple smoothing technique. TEMA is designed to be even more responsive to price changes than DEMA or traditional moving averages, effectively eliminating the lag associated with trend-following indicators.

TEMA is constructed using a combination of single, double, and triple Exponential Moving Averages (EMAs). This unique composition allows it to track price action very closely, making it a favorite among short-term traders and scalpers who require immediate signals.

## Core Concepts

* **Maximum Lag Reduction:** TEMA offers superior lag reduction compared to SMA, EMA, and even DEMA.
* **Triple Smoothing:** It utilizes three layers of EMA calculations to derive its value.
* **Composite Formula:** The formula cleverly combines $EMA_1$, $EMA_2$, and $EMA_3$ to subtract lag.
* **Trend Following:** Despite its speed, it remains a trend-following indicator, useful for identifying direction and reversals.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Length | 20 | Controls responsiveness/smoothness | Shorter for scalping, longer for trend filtering |
| Source | Close | Data point used for calculation | Change to HL2 or HLC3 for typical price representation |
| Alpha | 3/(length+1) | Determines weighting decay | Direct alpha manipulation allows for precise tuning |

## Calculation and Mathematical Foundation

**Simplified explanation:**
TEMA uses a single EMA, a double EMA (EMA of EMA), and a triple EMA (EMA of EMA of EMA). It combines these three components to cancel out the lag inherent in the smoothing process.

**Technical formula:**
$$TEMA = 3 \times EMA_1 - 3 \times EMA_2 + EMA_3$$

Where:

* $EMA_1 = EMA(Price)$
* $EMA_2 = EMA(EMA_1)$
* $EMA_3 = EMA(EMA_2)$

The formula is derived from the error correction principle, similar to DEMA but extended to a third degree.
The lag error is estimated and subtracted from the original EMA, resulting in a highly responsive curve that often leads price turns.

> 🔍 **Technical Note:** The implementation leverages the optimized `Ema` class, which uses **Hunter's bias compensation**. This ensures that all three underlying EMAs are initialized correctly from the very first data point, providing accurate TEMA values immediately without a long warmup period.

## C# Implementation

The library provides a high-performance implementation of TEMA that supports both standard period-based initialization and direct alpha specification.

### Usage Examples

```csharp
using QuanTAlib;

// Initialize with period 14
var tema = new Tema(14);

// Or initialize with specific alpha
var temaAlpha = new Tema(0.15);

// Streaming update
TValue result = tema.Update(new TValue(time, price));
Console.WriteLine($"Current TEMA: {result.Value}");

// Batch calculation (TSeries API)
TSeries source = ...;
TSeries results = Tema.Calculate(source, 14);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Tema.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Zero-Allocation Span API

For performance-critical scenarios, the static `Calculate` method uses `ArrayPool` internally to manage the intermediate buffers for the underlying EMAs, ensuring zero heap allocations for the user (beyond the input/output arrays).

```csharp
// Allocate buffers once
double[] source = new double[200000];
double[] temaOutput = new double[200000];

// Zero heap allocation during calculation
Tema.Calculate(source.AsSpan(), temaOutput.AsSpan(), period: 50);
```

### Eventing and Reactive Support

This indicator implements the `ITValuePublisher` interface, enabling event-driven and reactive workflows.

* **Subscription:** Can be constructed with an `ITValuePublisher` (e.g., `TSeries`) to automatically update when the source emits a new value.
* **Publication:** Emits a `Pub` event with the new `TValue` whenever it is updated.

```csharp
using QuanTAlib;

// 1. Setup a source (publisher)
var source = new TSeries();

// 2. Create indicator subscribed to source
// It waits for events from 'source'
var tema = new Tema(source, period: 14);

// 3. Optional: Subscribe to indicator's output
tema.Pub += (item) => Console.WriteLine($"TEMA Updated: {item.Value}");

// 4. Ingest data into source
// This triggers the chain: source -> tema -> Console.WriteLine
source.Add(new TValue(DateTime.Now, 100));
source.Add(new TValue(DateTime.Now, 105));
```

This pattern allows building complex, reactive processing pipelines without manual update loops.

### Handling Invalid Values

`Tema` delegates value handling to the underlying `Ema` instances, which use **last-value substitution** for `NaN` or `Infinity`. This ensures continuity and stability in the output series.

## Interpretation Details

* **Trend Direction:** Price above TEMA indicates an uptrend; price below indicates a downtrend.
* **Signal Line:** TEMA is often used as a signal line for other indicators due to its speed.
* **Crossovers:** TEMA crossovers with price or other averages provide very early entry/exit signals.
* **Volatility:** Due to its speed, TEMA can be volatile in choppy markets.

## Limitations and Considerations

* **Overshoot:** Like DEMA, TEMA can overshoot price action during sudden, sharp reversals.
* **Noise:** Its extreme responsiveness makes it susceptible to market noise and false signals in sideways markets.
* **Complexity:** The triple calculation is computationally more expensive than SMA or EMA, though negligible on modern hardware.

## References

1. Mulloy, P.G. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1).
2. Achelis, S.B. (2000). *Technical Analysis from A to Z*. McGraw-Hill.
