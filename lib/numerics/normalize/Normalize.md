# NORMALIZE: Min-Max Normalization

> *Normalization is the art of making apples and oranges comparable—by insisting that everything lives on the same scale from 0 to 1.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Normalize)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [normalize.pine](normalize.pine)                       |

- The Normalize transformer applies min-max scaling to map any value series into the bounded range [0, 1] based on the observed minimum and maximum w...
- **Trading note:** Min-max normalization to [0,1]; makes indicators comparable across different scales.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Normalize transformer applies min-max scaling to map any value series into the bounded range [0, 1] based on the observed minimum and maximum within a rolling lookback window. This technique is fundamental for feature scaling, creating bounded oscillators, and comparing series with different magnitudes.

## Mathematical Foundation

### Core Formula

$$
\text{Norm}_t = \frac{x_t - \min_{[t-n+1, t]}}{\max_{[t-n+1, t]} - \min_{[t-n+1, t]}}
$$

where:
- $x_t$ is the input value at time $t$
- $n$ is the lookback period
- $\min_{[t-n+1, t]}$ is the minimum value in the window
- $\max_{[t-n+1, t]}$ is the maximum value in the window

### Edge Case: Flat Range

When $\max = \min$ (all values identical):

$$
\text{Norm}_t = 0.5
$$

This neutral value is returned since the "position" within a zero-width range is undefined.

### Key Properties

| Property | Value | Description |
|:---------|:------|:------------|
| **Range** | $[0, 1]$ | Guaranteed bounded output |
| **Min maps to** | 0 | Lowest value in window → 0 |
| **Max maps to** | 1 | Highest value in window → 1 |
| **Linear** | Yes | Preserves relative distances within window |
| **Invertible** | Yes* | If you know min/max |

*Given the min and max used, original value = Norm × (max - min) + min

## Financial Applications

### Oscillator Construction

Convert any price-based measure to oscillator form:

$$
\text{NormalizedRSI} = \text{Normalize}(\text{RSI}, 100)
$$

### Cross-Asset Comparison

Compare instruments with different price scales:

$$
\text{RelativeStrength} = \text{Normalize}(\text{Price}_A, n) - \text{Normalize}(\text{Price}_B, n)
$$

### Machine Learning Features

Prepare inputs for models requiring bounded features:

$$
\text{Feature}_i = \text{Normalize}(x_i, \text{lookback})
$$

### Dynamic Range Detection

Identify where price sits within recent range:

$$
\text{Position} = \text{Normalize}(\text{Close}, 20)
$$

Values near 1.0 indicate price at recent highs; near 0.0 at recent lows.

## Parameter Guide

### Period Selection

| Period | Behavior | Use Case |
|:-------|:---------|:---------|
| 5-10 | Highly responsive | Short-term oscillators |
| 14-20 | Standard | General normalization |
| 50-100 | Smooth | Position within broader context |
| 200+ | Very stable | Long-term percentile-like behavior |

### Period Effects

- **Shorter periods**: More volatile output, quicker adaptation to new ranges
- **Longer periods**: Smoother output, but slower to adapt; may stay near extremes longer

## Implementation Details

### Rolling Window Approach

The implementation maintains a ring buffer of size $n$ and recalculates min/max on each update. This provides O(n) complexity per update but ensures correctness with the rolling window semantics.

### Streaming Characteristics

| Metric | Value |
|:-------|:------|
| **Warmup Period** | $n$ (period) |
| **Memory** | O(n) for ring buffer |
| **Complexity** | O(n) per update |

### Precision Considerations

| Scenario | Handling |
|:---------|:---------|
| **Zero range** | Returns 0.5 |
| **Very small range** | Full precision maintained |
| **NaN/Infinity input** | Last valid value substituted |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
|:----------|:-----:|:------|
| Buffer add | 1 | O(1) ring buffer |
| Min scan | n | Linear scan of window |
| Max scan | n | Combined with min scan |
| SUB | 2 | value - min, max - min |
| DIV | 1 | Final division |
| **Total** | O(n) | Dominated by min/max scan |

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | Exact min-max scaling |
| **Boundedness** | 10/10 | Guaranteed [0, 1] output |
| **Adaptability** | 8/10 | Adapts to rolling window |
| **Timeliness** | 7/10 | Requires warmup period |

## Usage Examples

### Basic Usage

```csharp
// Create Normalize with 14-period lookback
var norm = new Normalize(14);

// Feed price data
var price = new TValue(DateTime.UtcNow, 105.0);
var normalized = norm.Update(price);  // Value in [0, 1]
```

### Creating Oscillator from Any Series

```csharp
var rsi = new Rsi(14);
var normRsi = new Normalize(rsi, 100);  // Chain: RSI → Normalize

// RSI output (0-100) gets normalized to [0, 1] over 100 periods
foreach (var bar in data)
{
    rsi.Update(new TValue(bar.Time, bar.Close));
    // normRsi automatically updates via event
}
```

### Comparing Multiple Assets

```csharp
var normA = new Normalize(50);
var normB = new Normalize(50);

// Compare where each asset sits in its own range
var posA = normA.Update(new TValue(now, priceA));
var posB = normB.Update(new TValue(now, priceB));

var relativeStrength = posA.Value - posB.Value;  // [-1, 1]
```

### Span API for Batch Processing

```csharp
double[] prices = GetHistoricalPrices();
double[] normalized = new double[prices.Length];

Normalize.Calculate(prices, normalized, period: 20);
```

## Common Pitfalls

1. **Lookback Dependency**: Output depends heavily on what's in the lookback window. Unusual spikes or crashes in the window can distort normalization for the entire period duration.

2. **Not Truly Bounded During Warmup**: Before the warmup period completes, the window is partial, which may produce less meaningful normalization.

3. **Flat Market Handling**: When a series has no variation over the period, output becomes 0.5. This may need special handling if your strategy interprets 0.5 differently.

4. **Window Lag**: When price breaks out of a long-established range, the old min/max remains in the window until it ages out, causing the normalized value to stay pinned at 0 or 1.

5. **Memory Requirements**: Each instance requires O(period) memory for the ring buffer. For many indicators with long periods, this can add up.

6. **Non-Stationarity**: Min-max normalization assumes the range is representative. In trending markets, the normalization may consistently return values near 0 or 1.

## Validation

| Test | Status |
|:-----|:------:|
| **Output in [0, 1]** | ✅ |
| **Max value → 1** | ✅ |
| **Min value → 0** | ✅ |
| **Flat range → 0.5** | ✅ |
| **Linear mapping** | ✅ |
| **Rolling window correctness** | ✅ |
| **Streaming = Batch** | ✅ |

## References

- Aksoy, S., & Haralick, R. M. (2001). "Feature normalization and likelihood-based similarity measures for image retrieval." *Pattern Recognition Letters*.
- Patro, S., & Sahu, K. K. (2015). "Normalization: A preprocessing stage." *IARJSET*.
- Géron, A. (2019). *Hands-On Machine Learning with Scikit-Learn, Keras, and TensorFlow*. O'Reilly Media.