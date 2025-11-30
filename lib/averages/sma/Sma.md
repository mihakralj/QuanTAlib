# SMA: Simple Moving Average

## Overview and Purpose

The Simple Moving Average (SMA) is one of the most fundamental and widely used technical indicators in financial analysis. It calculates the arithmetic mean of a selected range of prices over a specified number of periods. Developed in the early days of technical analysis, the SMA provides traders with a straightforward method to identify trends by smoothing price data and filtering out short-term fluctuations.

Unlike the Exponential Moving Average (EMA) which gives more weight to recent data, the SMA treats all data points in the window equally. This equal weighting makes the SMA particularly intuitive to understand, as it simply represents the average price over the specified time period. Due to its simplicity and effectiveness, it remains a cornerstone indicator that forms the basis for numerous other technical analysis tools.

## Core Concepts

* **Equal weighting:** SMA gives equal importance to each price point in the calculation period, unlike weighted averages that emphasize certain data points
* **Noise reduction:** Smooths price fluctuations to help identify the underlying trend direction
* **Timeframe flexibility:** Effective across all timeframes, with shorter periods for short-term analysis and longer periods for identifying major trends
* **Foundation indicator:** Serves as the mathematical basis for Bollinger Bands, moving average envelopes, and other derived indicators

The core principle of SMA is its unbiased approach to price data. By treating all prices within the lookback period with equal importance, SMA creates a balanced view of recent market activity. This equal weighting makes the SMA particularly intuitive to understand, as it simply represents the average price over the specified time period.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Period | 20 | Controls the lookback period | Increase for smoother signals in volatile markets, decrease for responsiveness |
| Source | Close | Price data used for calculation | Consider using HLC3 for a more balanced price representation |

**Pro Tip:** For trend following strategies, consider using two SMAs with different periods (e.g., 50 and 200) – crossovers between these can identify significant trend changes while filtering out minor fluctuations. This "golden cross" (50 crossing above 200) and "death cross" (50 crossing below 200) are among the most watched signals in technical analysis.

## Calculation and Mathematical Foundation

**Simplified explanation:**
SMA adds up the prices for a specific number of periods and divides by that number. For example, a 10-period SMA adds the last 10 closing prices and divides by 10 to find the average.

**Technical formula:**
The standard calculation:
$$SMA = \frac{P_1 + P_2 + ... + P_n}{n} = \frac{1}{n}\sum_{i=1}^{n}P_i$$

An optimized recursive calculation used in the implementation:
$$SMA_t = SMA_{t-1} + \frac{P_t - P_{t-n}}{n}$$

Where:

* $P_1, P_2, ..., P_n$ are price values in the lookback window
* $n$ is the period length
* $P_{t-n}$ is the oldest price leaving the window

> 🔍 **Technical Note:** The SMA has a precisely defined lag of $(n-1)/2$ periods, meaning a 21-period SMA lags behind price by 10 bars. This consistent, deterministic lag makes its behavior predictable across all market conditions. The implementation uses a running sum approach for O(1) update complexity regardless of period length.

## C# Implementation

The library provides two implementations: a standard scalar version and a multi-period vector version for calculating multiple SMAs simultaneously.

### Single SMA (`Sma`)

The `Sma` class calculates a single simple moving average with O(1) update complexity.

```csharp
using QuanTAlib;

// Initialize with period 10
var sma = new Sma(10);

// Streaming update
TValue result = sma.Update(new TValue(time, price));
Console.WriteLine($"Current SMA: {result.Value}");

// Access properties
Console.WriteLine($"Name: {sma.Name}");           // "Sma(10)"
Console.WriteLine($"WarmupPeriod: {sma.WarmupPeriod}");  // 10
Console.WriteLine($"IsHot: {sma.IsHot}");          // true when buffer is full

// Batch calculation
TSeries source = ...;
TSeries results = Sma.Calculate(source, 10);
```

### Multi-Period SMA (`SmaVector`)

The `SmaVector` class calculates multiple SMAs with different periods on the same input series simultaneously.

```csharp
using QuanTAlib;

// Initialize with multiple periods
int[] periods = { 5, 10, 20 };
var smaVector = new SmaVector(periods);

// Streaming update
TValue[] results = smaVector.Update(new TValue(time, price));

// Access values
Console.WriteLine($"SMA(5): {results[0].Value}");
Console.WriteLine($"SMA(10): {results[1].Value}");
Console.WriteLine($"SMA(20): {results[2].Value}");

// Batch calculation
TSeries source = ...;
TSeries[] seriesResults = smaVector.Calculate(source);
```

### Bar Correction (isNew Parameter)

Both `Sma` and `SmaVector` support intra-bar updates for real-time trading systems:

```csharp
var sma = new Sma(10);

// Process historical bars
for (int i = 0; i < historicalBars.Count; i++)
{
    sma.Update(historicalBars[i], isNew: true);
}

// Real-time: receive initial tick for new bar
sma.Update(new TValue(time, 100.5), isNew: true);

// Real-time: price updates within same bar
sma.Update(new TValue(time, 101.0), isNew: false);  // O(1) correction
sma.Update(new TValue(time, 100.8), isNew: false);  // O(1) correction

// Bar closes, next bar starts
sma.Update(new TValue(time + 1, 101.2), isNew: true);
```

**Implementation detail:** Bar correction is O(1) using scalar state save/restore, not buffer copying.

### Handling Invalid Values (NaN/Infinity)

Both `Sma` and `SmaVector` use **last-value substitution** for handling invalid inputs:

```csharp
var sma = new Sma(10);

// Valid values establish baseline
sma.Update(new TValue(time, 100));
sma.Update(new TValue(time, 110));

// NaN or Infinity inputs are replaced with last valid value (110)
var result = sma.Update(new TValue(time, double.NaN));
Console.WriteLine(double.IsFinite(result.Value)); // true

// Works identically for batch operations
var series = new TSeries();
series.Add(time, 100);
series.Add(time + 1, double.NaN);  // Will use 100
series.Add(time + 2, 120);
var results = sma.Update(series);  // All values are finite
```

**Behavior:**

* When `NaN`, `PositiveInfinity`, or `NegativeInfinity` is encountered, the last valid value is substituted
* This provides output continuity instead of propagating invalid values
* `Reset()` clears the last valid value, so the next valid input establishes a new baseline

### Performance Characteristics

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Update (isNew=true) | O(1) | Running sum: `sum = sum - oldest + newest` |
| Update (isNew=false) | O(1) | Scalar state restore + recalculate |
| Batch processing | O(n) | Where n is series length |
| Memory (single) | O(period) | One RingBuffer for values |
| Memory (state) | O(1) | 6 doubles for bar correction |

The implementation uses:

* **Running sum** for O(1) average calculation
* **Scalar state save/restore** for O(1) bar correction
* **Pinned memory** in RingBuffer for cache-friendly access
* **CollectionsMarshal.SetCount** for zero-allocation batch processing

## Interpretation Details

SMA can be used in various trading strategies:

* **Trend identification:** The direction of SMA indicates the prevailing trend
* **Signal generation:** Crossovers between price and SMA generate basic trade signals
* **Support/resistance levels:** SMA can act as dynamic support during uptrends and resistance during downtrends
* **Multiple timeframe analysis:** Using SMAs with different periods can confirm trends across different timeframes
* **Moving average crossovers:** When a shorter-period SMA crosses above a longer-period SMA, it signals a potential uptrend (and vice versa)

### SMA vs EMA Comparison

| Aspect | SMA | EMA |
|--------|-----|-----|
| Weighting | Equal for all values | Recent values weighted more |
| Lag | Higher: $(n-1)/2$ bars | Lower due to recent weighting |
| Sensitivity | Slower to react | Faster reaction to changes |
| Noise | Better noise filtering | More responsive but noisier |
| Sudden changes | Abrupt when oldest value exits | Smooth exponential decay |
| Best use | Long-term trends, support/resistance | Short-term signals, momentum |

## Limitations and Considerations

* **Market conditions:** Less effective in choppy, sideways markets where price oscillates around the average
* **Lag factor:** Significant lag in responding to rapid price changes means SMA will always be late to signal reversals
* **Equal weighting:** Treats recent and older prices equally, which may not reflect current market dynamics
* **Sudden changes:** When a price point leaves the calculation window, it can cause abrupt changes in the SMA
* **Complementary tools:** Best used with momentum oscillators, volume indicators, or other trend confirmation tools

## References

1. Edwards, R.D. and Magee, J. (2007). *Technical Analysis of Stock Trends*. CRC Press.
2. Murphy, J.J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
3. Kaufman, P. (2013). *Trading Systems and Methods*, 5th Edition. Wiley Trading.
