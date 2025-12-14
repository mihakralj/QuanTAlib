# PWMA: Parabolic Weighted Moving Average

## Overview and Purpose

The Parabolic Weighted Moving Average (PWMA) is a technical indicator that applies parabolic weighting to price data, assigning significantly higher importance to the most recent observations. While the Weighted Moving Average (WMA) uses linear weighting ($i$), PWMA uses squared weighting ($i^2$), creating an even stronger recency bias. This results in an indicator that tracks price action with exceptional responsiveness, making it ideal for fast-moving markets and as a component in advanced momentum oscillators like Jurik's Velocity (VEL).

## Core Concepts

* **Parabolic weighting:** Weights follow a squared progression ($1^2, 2^2, \dots, n^2$), drastically emphasizing recent data over older points.
* **Reduced Lag:** The aggressive weighting scheme minimizes lag significantly more than WMA or SMA, allowing for faster trend detection.
* **O(1) Complexity:** This implementation uses a triple running sum technique to ensure constant-time updates, regardless of the period length.
* **Component Indicator:** PWMA is a critical building block for other indicators, most notably serving as the "fast" component in the Velocity (VEL) indicator calculation ($VEL = PWMA - WMA$).

The core innovation of PWMA is its use of squared weights, which shifts the center of gravity of the moving average much closer to the current price than linear methods. This makes it highly sensitive to recent price changes while still providing a smooth curve derived from the entire window.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Period | 14 | Controls the lookback period | Increase for smoother trends, decrease for ultra-fast responsiveness |
| Source | Close | Price data used for calculation | Consider using HLC3 for a more balanced price representation |

**Pro Tip:** Because PWMA is so responsive, it can be prone to overshooting in choppy markets. It is often best used in combination with a slower average (like WMA) to form a MACD-like oscillator or to identify rapid momentum shifts.

## Calculation and Mathematical Foundation

**Simplified explanation:**
PWMA calculates a weighted average where the weight of each price is the square of its position in the window. For a 5-period PWMA, the weights would be $1, 4, 9, 16, 25$ (for the oldest to newest prices respectively).

**Technical formula:**
$$PWMA = \frac{\sum_{i=1}^{n} i^2 \cdot P_i}{\sum_{i=1}^{n} i^2}$$

Where:

* $n$ is the period length
* $P_i$ is the price at position $i$ (oldest to newest)
* $i^2$ is the parabolic weight
* Divisor $= \frac{n(n+1)(2n+1)}{6}$ (sum of squares of first $n$ integers)

**O(1) Optimization - Triple Running Sums:**

To achieve constant-time updates, the algorithm maintains three running sums:

1. **S1 (Simple Sum):** $\sum P_i$
2. **S2 (Linear Weighted Sum):** $\sum i \cdot P_i$
3. **S3 (Parabolic Weighted Sum):** $\sum i^2 \cdot P_i$

The recurrence relations for updating these sums when the window slides are:

$$S_{1,new} = S_{1,old} - P_{oldest} + P_{new}$$
$$S_{2,new} = S_{2,old} - S_{1,old} + n \cdot P_{new}$$
$$S_{3,new} = S_{3,old} - 2 \cdot S_{2,old} + S_{1,old} + n^2 \cdot P_{new}$$

$$PWMA = \frac{S_{3,new}}{divisor}$$

This algebraic expansion allows the indicator to update in constant time (~12 operations) regardless of whether the period is 10 or 10,000.

## C# Implementation

The library provides two implementations: a standard scalar version and a high-performance Span-based static version.

### Single PWMA (`Pwma`)

The `Pwma` class calculates a single parabolic weighted moving average with O(1) update complexity.

```csharp
using QuanTAlib;

// Initialize with period 14
var pwma = new Pwma(14);

// Streaming update
TValue result = pwma.Update(new TValue(time, price));
Console.WriteLine($"Current PWMA: {result.Value}");

// Access properties
Console.WriteLine($"Name: {pwma.Name}");           // "Pwma(14)"
Console.WriteLine($"IsHot: {pwma.IsHot}");          // true when buffer is full

// Batch calculation (TSeries API)
TSeries source = ...;
TSeries results = Pwma.Calculate(source, 14);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Pwma.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Zero-Allocation Span API

For performance-critical scenarios (backtesting, HFT), use the Span-based overload:

```csharp
// Allocate buffers once, reuse across calculations
double[] source = new double[200000];
double[] pwmaOutput = new double[200000];

// Zero heap allocation during calculation
Pwma.Calculate(source.AsSpan(), pwmaOutput.AsSpan(), period: 100);

// Results are written directly to output buffer
Console.WriteLine($"Last PWMA: {pwmaOutput[^1]}");
```

**Benefits:**

* **Zero allocation**: No GC pressure during calculation
* **Cache-friendly**: Sequential memory access patterns
* **O(1) per-bar** via triple running sums
* **Compatible** with `ArrayPool<T>` for buffer management

### Bar Correction (isNew Parameter)

`Pwma` supports intra-bar updates for real-time trading systems:

```csharp
var pwma = new Pwma(14);

// Process historical bars
for (int i = 0; i < historicalBars.Count; i++)
{
    pwma.Update(historicalBars[i], isNew: true);
}

// Real-time: receive initial tick for new bar
pwma.Update(new TValue(time, 100.5), isNew: true);

// Real-time: price updates within same bar
pwma.Update(new TValue(time, 101.0), isNew: false);  // O(1) correction
pwma.Update(new TValue(time, 100.8), isNew: false);  // O(1) correction

// Bar closes, next bar starts
pwma.Update(new TValue(time + 1, 101.2), isNew: true);
```

**Implementation detail:** Bar correction is O(1) using scalar state save/restore.

### Eventing and Reactive Support

This indicator implements the `ITValuePublisher` interface, enabling event-driven and reactive workflows.

* **Subscription:** Can be constructed with an `ITValuePublisher` (e.g., `TSeries`) to automatically update when the source emits a new value.
* **Publication:** Emits a `Pub` event with the new `TValue` whenever it is updated.

```csharp
using QuanTAlib;

// 1. Setup a source (publisher)
var source = new TSeries();

// 2. Create indicator subscribed to source
var pwma = new Pwma(source, period: 14);

// 3. Optional: Subscribe to indicator's output
pwma.Pub += (item) => Console.WriteLine($"PWMA Updated: {item.Value}");

// 4. Ingest data into source
source.Add(new TValue(DateTime.Now, 100));
```

### Handling Invalid Values (NaN/Infinity)

`Pwma` uses **last-value substitution** for handling invalid inputs:

```csharp
var pwma = new Pwma(14);

// Valid values establish baseline
pwma.Update(new TValue(time, 100));

// NaN or Infinity inputs are replaced with last valid value
var result = pwma.Update(new TValue(time, double.NaN));
Console.WriteLine(double.IsFinite(result.Value)); // true
```

**Behavior:**

* When `NaN`, `PositiveInfinity`, or `NegativeInfinity` is encountered, the last valid value is substituted
* This provides output continuity instead of propagating invalid values
* `Reset()` clears the last valid value, so the next valid input establishes a new baseline

## Performance Characteristics

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Update (isNew=true) | O(1) | Triple running sums logic |
| Update (isNew=false) | O(1) | Scalar state restore + recalculate |
| Batch processing | O(n) | Where n is series length |
| Memory (single) | O(period) | One RingBuffer for values |
| Memory (state) | O(1) | Scalar state struct |

The implementation uses:

* **Triple running sums** for O(1) parabolic weighted average calculation
* **Scalar state save/restore** for O(1) bar correction
* **Pinned memory** in RingBuffer for cache-friendly access
* **Periodic Resync** (every 1000 ticks) to prevent floating-point drift

## Interpretation Details

PWMA is primarily used for:

* **High-Speed Trend Detection:** Its low lag makes it excellent for catching trends early.
* **Velocity Calculation:** Used in conjunction with WMA to calculate Velocity ($VEL = PWMA - WMA$).
* **Dynamic Support/Resistance:** Acts as a tighter support/resistance level than SMA or WMA in strong trends.

### PWMA vs WMA vs SMA Comparison

| Aspect | PWMA | WMA | SMA |
|--------|------|-----|-----|
| Weighting | Parabolic ($i^2$) | Linear ($i$) | Equal ($1$) |
| Lag | Lowest | Low | High |
| Sensitivity | Highest | High | Low |
| Noise filtering | Low | Good | Best |
| Best use | Momentum, Velocity | General Trend | Long-term Trend |

## Limitations and Considerations

* **Overshoot:** Due to the aggressive weighting, PWMA can overshoot price targets during sudden reversals.
* **Noise Sensitivity:** It is more sensitive to market noise than WMA or SMA.
* **Drift:** The complex running sum algorithm requires periodic resynchronization (handled internally) to maintain precision over millions of updates.

## References

* Jurik Research (concept of parabolic weighting in Velocity)
* Colby, Robert W. "The Encyclopedia of Technical Market Indicators." McGraw-Hill, 2002
