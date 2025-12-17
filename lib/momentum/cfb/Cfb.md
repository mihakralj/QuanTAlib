# CFB - Jurik Composite Fractal Behavior

## Overview and Purpose

Composite Fractal Behavior (CFB) is a sophisticated trend duration index developed by Jurik Research. It measures the "fractal efficiency" of price movements across multiple time scales to determine the quality and duration of a trend. Unlike traditional trend indicators that look at a single period, CFB analyzes a spectrum of lookback periods to create a composite index.

CFB is designed to answer the question: "How long has the market been trending efficiently?" It is particularly useful for:

* Adjusting the period of other indicators (adaptive indicators).
* Filtering out choppy markets.
* Identifying the breakdown of long-term trends.

## Core Concepts

* **Fractal Efficiency:** Measures how "straight" the price movement is. A straight line has high efficiency; a choppy path has low efficiency.
* **Composite Index:** Instead of relying on a single lookback length, CFB evaluates a wide range of lengths (e.g., 4 to 192 bars) and combines them based on their efficiency.
* **Adaptive:** The indicator adapts to the market's current fractal structure, giving more weight to timeframes where trending behavior is evident.
* **Trend Duration:** The output value represents the approximate duration (in bars) of the current trend.

## Common Settings and Parameters

| Parameter | Default | Function |
|-----------|---------|----------|
| Lengths | `[2, 4, ..., 192]` | Array of lookback periods to analyze. Default is a dense array from 2 to 192. |
| Source | Close | Price data used for calculation. |

**Pro Tip:** CFB values typically range from 0 to the maximum lookback length. A rising CFB indicates a strengthening trend (either up or down), while a falling CFB suggests the trend is breaking down or the market is entering a consolidation phase.

## Calculation and Mathematical Foundation

The CFB calculation involves several steps for each lookback length $L$ in the provided set:

1. **Calculate Efficiency Ratio:**
   For each length $L$, calculate the ratio of the net price movement to the total volatility (path length) over that period.
   $$Ratio_L = \frac{|Price_t - Price_{t-L}|}{\sum_{i=0}^{L-1} |Price_{t-i} - Price_{t-i-1}|}$$

2. **Filter:**
   Only consider lengths where the efficiency ratio exceeds a threshold (typically 0.25). This filters out noise and weak trends.

3. **Weighted Average:**
   Calculate the weighted average of the qualifying lengths, using the efficiency ratio as the weight.
   $$CFB = \frac{\sum (L \cdot Ratio_L)}{\sum Ratio_L}$$
   where the summation is over all $L$ such that $Ratio_L > 0.25$.

4. **Decay:**
   If no lengths qualify (i.e., the market is very choppy), the CFB value decays towards 1.0.

## C# Implementation

The library provides a high-performance implementation that uses `RingBuffer` for O(1) updates of the volatility sums.

### Single CFB (`Cfb`)

```csharp
using QuanTAlib;

// Initialize with default lengths
var cfb = new Cfb();

// Or specify custom lengths
var cfbCustom = new Cfb(new int[] { 10, 20, 30, 40, 50 });

// Streaming update
TValue result = cfb.Update(new TValue(time, price));
Console.WriteLine($"Current Trend Duration: {result.Value}");
```

### Zero-Allocation Span API

For performance-critical scenarios:

```csharp
double[] prices = ...;
double[] output = new double[prices.Length];

// Calculate using default lengths
Cfb.Batch(prices.AsSpan(), output.AsSpan());
```

### Bar Correction (isNew Parameter)

`Cfb` supports intra-bar updates:

```csharp
// Real-time: receive initial tick for new bar
cfb.Update(new TValue(time, 100.5), isNew: true);

// Real-time: price updates within same bar
cfb.Update(new TValue(time, 101.0), isNew: false);
```

## Interpretation Details

* **High Values:** Indicate a strong, persistent trend. The value roughly corresponds to the number of bars the trend has been in effect.
* **Low Values:** Indicate a choppy, non-trending market.
* **Rising CFB:** The trend is gaining strength or duration.
* **Falling CFB:** The trend is losing consistency or ending.

CFB is often used as an input to other adaptive indicators (e.g., JMA) to dynamically adjust their smoothing period based on market conditions.

## References

* Jurik Research: [CFB - Composite Fractal Behavior](http://jurikres.com/catalog1/ms_cfb.htm)
