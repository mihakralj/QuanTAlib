# MAENV: Moving Average Envelope

> "The simplest channels are often the most useful - a percentage above and below tells you when price is stretched."

The Moving Average Envelope (MAENV) creates a fixed percentage-based channel around a selectable moving average. Unlike volatility-adaptive channels like Keltner or Bollinger Bands, MAENV maintains constant proportional distance from the middle line, making it useful for mean-reversion strategies where you expect price to oscillate within predictable bounds.

## Historical Context

Moving Average Envelopes are among the oldest channel indicators, predating volatility-based bands by decades. The concept is straightforward: if price tends to revert to a moving average, then defining zones at fixed percentages above and below that average provides natural support and resistance levels.

The choice of moving average type affects responsiveness:

- **SMA**: Equal weighting creates stable, predictable bands but slower reaction to price changes
- **EMA**: Exponential weighting responds faster to recent prices, making bands more dynamic
- **WMA**: Linear weighting provides a middle ground, emphasizing recent data without the sharp responsiveness of EMA

This implementation offers all three options, letting traders choose the smoothing behavior that matches their strategy.

## Architecture & Physics

### 1. Moving Average Calculation

The middle band is computed using the selected MA type:

**SMA (Simple Moving Average)** - O(1) streaming via ring buffer:

$$
\text{SMA}_t = \frac{1}{n} \sum_{i=0}^{n-1} P_{t-i}
$$

Implementation uses circular buffer to maintain running sum, achieving constant-time updates.

**EMA (Exponential Moving Average)** - O(1) with warmup compensation:

$$
\alpha = \frac{2}{n+1}
$$

$$
\text{sum}_t = \text{sum}_{t-1}(1-\alpha) + P_t \cdot \alpha
$$

$$
\text{weight}_t = \text{weight}_{t-1}(1-\alpha) + \alpha
$$

$$
\text{EMA}_t = \frac{\text{sum}_t}{\text{weight}_t}
$$

Warmup compensation ensures accurate values from the first bar by tracking both weighted sum and weight.

**WMA (Weighted Moving Average)** - O(n):

$$
\text{WMA}_t = \frac{\sum_{i=0}^{n-1} w_i \cdot P_{t-i}}{\sum_{i=0}^{n-1} w_i}
$$

where $w_i = (n-i) \times n$ giving highest weight to most recent values.

### 2. Band Calculation

Bands are symmetric percentage-based offsets:

$$
\text{dist}_t = \text{Middle}_t \times \frac{\text{percentage}}{100}
$$

$$
\text{Upper}_t = \text{Middle}_t + \text{dist}_t
$$

$$
\text{Lower}_t = \text{Middle}_t - \text{dist}_t
$$

## Mathematical Foundation

### Band Width Formula

Total band width scales linearly with both the middle value and percentage parameter:

$$
\text{Width}_t = \text{Upper}_t - \text{Lower}_t = 2 \times \text{Middle}_t \times \frac{\text{percentage}}{100}
$$

This creates proportional bands - a 2% envelope means bands are always 4% of the middle value apart.

### EMA Warmup Derivation

Traditional EMA initialization (`EMA_0 = P_0`) creates bias when the first value differs significantly from subsequent values. The warmup compensation tracks:

$$
\text{theoretical\_weight} = \alpha \sum_{i=0}^{t} (1-\alpha)^i = 1 - (1-\alpha)^{t+1}
$$

By dividing sum by actual accumulated weight, the EMA converges to the true value faster and without initialization bias.

## Performance Profile

### Operation Count (Streaming Mode)

| MA Type | Per-Bar Cost | Memory | Complexity |
| :--- | :---: | :---: | :---: |
| SMA | ~5 ops | O(n) buffer | O(1) |
| EMA | ~8 ops | O(1) scalars | O(1) |
| WMA | ~3n ops | O(n) buffer | O(n) |

SMA and EMA achieve constant-time streaming updates. WMA requires linear time due to weighted sum recalculation.

### Batch Mode Performance

For batch processing of 1000 values:

| MA Type | Streaming | Batch (SIMD) | Speedup |
| :--- | :---: | :---: | :---: |
| SMA | ~5000 ops | ~5000 ops | 1× |
| EMA | ~8000 ops | ~8000 ops | 1× |
| WMA | ~3M ops | ~3M ops | 1× |

Limited SIMD benefit due to recursive nature of MA calculations.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact percentage-based calculation |
| **Timeliness** | 7/10 | Depends on MA type (EMA fastest) |
| **Stability** | 9/10 | No volatility-driven expansion |
| **Predictability** | 10/10 | Constant proportional width |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No direct equivalent |
| **Skender** | N/A | No direct equivalent |
| **Tulip** | N/A | No direct equivalent |
| **Ooples** | N/A | No direct equivalent |
| **PineScript** | ✅ | Reference implementation match |

Validation performed against internal manual calculations and PineScript reference. No external library provides identical multi-MA-type envelope implementation.

## Common Pitfalls

1. **MA Type Selection**: SMA provides most stable bands but slowest response. EMA responds quickly but may whipsaw. WMA balances both but costs O(n) per update.

2. **Percentage Calibration**: Optimal percentage varies by instrument volatility. Highly volatile assets need wider envelopes (3-5%), stable assets work with narrow bands (0.5-1%).

3. **False Breakouts**: Fixed percentage bands don't adapt to volatility regime changes. Price may consistently breach bands during high-volatility periods.

4. **Warmup Period**: All MA types need `period` bars for full accuracy. EMA warmup compensation accelerates convergence but initial bars still have reduced effective lookback.

5. **Memory Footprint**: SMA and WMA require period-sized buffers (~8 bytes × period per instance). EMA uses only scalar state (~32 bytes total).

6. **Bar Correction (isNew=false)**: State restoration copies entire buffer for SMA/WMA. For large periods, this adds latency to tick-by-tick updates.

## References

- Murphy, J.J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- TradingView. "Moving Average Envelope." Pine Script Reference.
