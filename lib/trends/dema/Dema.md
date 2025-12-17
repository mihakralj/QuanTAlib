# DEMA: Double Exponential Moving Average

## Overview and Purpose

The Double Exponential Moving Average (DEMA) is a technical indicator developed by Patrick Mulloy in 1994 to reduce the lag associated with traditional moving averages. Despite its name, DEMA is not simply a double smoothing of the price (like a double EMA would be). Instead, it uses a combination of a single EMA and a double EMA to subtract the lag inherent in the original EMA.

DEMA responds more quickly to price changes than a standard EMA or SMA, making it popular among traders who need faster signals for trend reversals or breakouts. It effectively filters out noise while maintaining high responsiveness, offering a "best of both worlds" solution between smoothing and lag reduction.

## Core Concepts

* **Lag Reduction:** DEMA's primary goal is to minimize the delay between price action and the indicator's response.
* **Composite Calculation:** It combines a single EMA and a double EMA (EMA of EMA) to achieve its unique characteristics.
* **High Responsiveness:** Reacts faster to market moves than traditional averages, potentially offering earlier entry and exit signals.
* **Trend Identification:** Like other moving averages, it helps identify the direction of the trend and potential support/resistance levels.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Length | 20 | Controls responsiveness/smoothness | Shorter for scalping/day trading, longer for swing/position trading |
| Source | Close | Data point used for calculation | Change to HL2 or HLC3 for more balanced price representation |
| Alpha | 2/(length+1) | Determines weighting decay | Direct alpha manipulation allows for precise tuning beyond standard length settings |

## Calculation and Mathematical Foundation

**Simplified explanation:**
DEMA takes a standard EMA, calculates a second EMA on that result, and then combines them using a specific formula to cancel out the lag.

**Technical formula:**
$$DEMA = 2 \times EMA_1 - EMA_2$$

Where:

* $EMA_1 = EMA(Price)$
* $EMA_2 = EMA(EMA_1)$

The formula can be derived from the error correction principle. If $EMA_1$ has a lag error $E$, then $EMA_2$ (being an EMA of $EMA_1$) will have roughly twice the lag error ($2E$).
The difference $EMA_1 - EMA_2$ represents the estimated lag error.
Adding this error term back to $EMA_1$ gives:
$$DEMA = EMA_1 + (EMA_1 - EMA_2) = 2 \times EMA_1 - EMA_2$$

> 🔍 **Technical Note:** The implementation leverages the optimized `Ema` class, which uses **Hunter's bias compensation**. This ensures that both the primary and secondary EMAs are initialized correctly from the very first data point, providing accurate DEMA values immediately without a long warmup period.

## C# Implementation

The library provides a high-performance implementation of DEMA that supports both standard period-based initialization and direct alpha specification.

### Usage Examples

```csharp
using QuanTAlib;

// Initialize with period 14
var dema = new Dema(14);

// Or initialize with specific alpha
var demaAlpha = new Dema(0.15);

// Streaming update
TValue result = dema.Update(new TValue(time, price));
Console.WriteLine($"Current DEMA: {result.Value}");

// Batch calculation (TSeries API)
TSeries source = ...;
TSeries results = Dema.Batch(source, 14);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Dema.Batch(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Zero-Allocation Span API

For performance-critical scenarios, the static `Calculate` method uses `ArrayPool` internally to manage the intermediate buffer for the first EMA, ensuring zero heap allocations for the user (beyond the input/output arrays).

```csharp
// Allocate buffers once
double[] source = new double[200000];
double[] demaOutput = new double[200000];

// Zero heap allocation during calculation
Dema.Batch(source.AsSpan(), demaOutput.AsSpan(), period: 50);
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
var dema = new Dema(source, period: 14);

// 3. Optional: Subscribe to indicator's output
dema.Pub += (item) => Console.WriteLine($"DEMA Updated: {item.Value}");

// 4. Ingest data into source
// This triggers the chain: source -> dema -> Console.WriteLine
source.Add(new TValue(DateTime.Now, 100));
source.Add(new TValue(DateTime.Now, 105));
```

This pattern allows building complex, reactive processing pipelines without manual update loops.

### Handling Invalid Values

`Dema` delegates value handling to the underlying `Ema` instances, which use **last-value substitution** for `NaN` or `Infinity`. This ensures continuity and stability in the output series.

## Interpretation Details

* **Trend Direction:** Price above DEMA suggests an uptrend; price below suggests a downtrend.
* **Crossovers:** DEMA crossovers (e.g., DEMA(10) crossing DEMA(20)) can provide faster signals than EMA crossovers.
* **Support/Resistance:** DEMA can act as dynamic support or resistance, often hugging the price action closer than an EMA.
* **Divergence:** Divergence between price and DEMA can signal potential reversals.

## Limitations and Considerations

* **Overshoot:** Because DEMA subtracts lag, it can sometimes overshoot price action during sharp reversals.
* **Noise Sensitivity:** Its high responsiveness means it may be more susceptible to market noise than a standard EMA or SMA.
* **Whipsaws:** In sideways markets, the reduced lag can lead to more frequent false signals (whipsaws).

## References

1. Mulloy, P.G. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1).
2. Murphy, J.J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
