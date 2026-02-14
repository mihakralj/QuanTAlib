# INERTIA: Inertia Oscillator

> "Price tends to keep doing what it's been doing — until it doesn't. Inertia measures the gap between reality and the regression's expectations."

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Single value (Close) |
| **Parameters** | `period` (default 20) |
| **Outputs** | Inertia value (`Last`) |
| **Output range** | Unbounded (price units) |
| **Warmup period** | `period` |

### Key takeaways

- Measures the **raw residual** between current price and its Time Series Forecast (linear regression endpoint): `Inertia = source - TSF`.
- Positive values mean price is **above** the regression line (bullish inertia); negative values mean price is **below** (bearish inertia).
- Unlike CFO which normalizes to percentage, Inertia preserves the **absolute price units**, making magnitudes directly comparable to price moves.
- Uses **O(1) incremental** sumY/sumXY maintenance for the linear regression, avoiding the O(period) recalculation cost on each bar.
- Periodic resync every 1000 ticks limits floating-point drift from the incremental accumulation.

## Historical Context

The Inertia concept draws from Donald Dorsey's work on the Relative Volatility Index (1993), where "inertia" describes the tendency of prices to continue moving in their current direction. The idea is straightforward: fit a least-squares regression line to the recent lookback window, project it to the current bar, and measure how far price has deviated from that projection. If price consistently sits above the regression endpoint, the trend has upward inertia. If price sits below, the trend has downward inertia.

This indicator is the un-normalized sibling of the Chande Forecast Oscillator (CFO). Where CFO divides the residual by the source price and multiplies by 100 to produce a percentage, Inertia reports the raw difference in price units. Both extract identical information from the regression; the distinction is purely about scaling. Inertia's absolute values are more meaningful for position sizing (how many dollars/points am I from forecast?), while CFO's percentage is more meaningful for cross-instrument comparison.

## What It Measures and Why It Matters

Inertia quantifies the deviation between actual price and the linear regression's best-fit endpoint. A zero reading means price is exactly where the regression expects it. Positive readings mean price is outperforming the linear projection; negative readings mean it is underperforming. The magnitude tells you how many price units that outperformance or underperformance amounts to.

Zero crossings are the primary signals: when Inertia crosses from negative to positive, price has moved from below its regression forecast to above it, suggesting a bullish shift. The reverse crossing signals a bearish shift. Unlike bounded oscillators, Inertia provides no inherent overbought/oversold thresholds. What constitutes "extreme" depends entirely on the instrument's typical price range and volatility.

## Mathematical Foundation

### Core Formula

Fit a least-squares line over the last $n$ bars using indices $x = 0, 1, \ldots, n-1$:

$$slope = \frac{n \sum x_i y_i - \sum x_i \sum y_i}{n \sum x_i^2 - (\sum x_i)^2}$$

$$intercept = \frac{\sum y_i - slope \cdot \sum x_i}{n}$$

Compute the Time Series Forecast (regression endpoint):

$$TSF = slope \cdot (n - 1) + intercept$$

The Inertia value is the residual:

$$Inertia = source - TSF$$

### Parameter Mapping

| Parameter | Formula role | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window $n$ for linear regression | 20 | > 0 |

### Warmup Period

$$W = period$$

Default configuration (period=20) warms up in 20 bars.

## Architecture & Physics

### 1. O(1) Incremental Linear Regression

Rather than recomputing $\sum y_i$ and $\sum x_i y_i$ from scratch each bar, the implementation maintains running accumulators. When the [`RingBuffer`](lib/oscillators/inertia/Inertia.cs:26) is full, the oldest value is subtracted from `SumY`, then `SumXY` is decremented by `SumY` (shifting all x-indices down by one) and incremented by `(period - 1) * newValue`. This is the PineScript algorithm adapted for C#.

### 2. Precomputed Constants

The sums $\sum x_i$ and $\sum x_i^2$ are constants for a fixed period. [`_sumX`](lib/oscillators/inertia/Inertia.cs:29) and [`_denomX`](lib/oscillators/inertia/Inertia.cs:30) are computed once in the constructor to eliminate redundant arithmetic on every update.

### 3. FMA for TSF

The regression endpoint computation uses [`Math.FusedMultiplyAdd`](lib/oscillators/inertia/Inertia.cs:131) for `slope * (period - 1) + intercept`, combining the multiply-add into a single fused operation.

### 4. Periodic Resync

Incremental floating-point accumulation drifts over thousands of updates. [`RecalculateSums`](lib/oscillators/inertia/Inertia.cs:150) performs a full O(period) resync every 1000 ticks, bounding drift to approximately 1e-5 between resyncs.

### 5. Edge Cases

- **Buffer not full**: Returns 0.0 until the buffer fills (period bars).
- **NaN/Infinity inputs**: Last-valid substitution; falls back to 0.0 if no valid data has been seen.
- **Perfect linear trend**: Regression fits exactly, producing Inertia = 0.0.
- **Correction (isNew=false)**: Full recalculation via `RecalculateSums` to avoid drift in corrected state.

## Interpretation and Signals

### Signal Zones

| Zone | Value | Meaning |
|------|-------|---------|
| Strong bullish | >> 0 | Price significantly above regression forecast |
| Mild bullish | > 0 | Price slightly above forecast |
| Neutral | ≈ 0 | Price at regression expectation |
| Mild bearish | < 0 | Price slightly below forecast |
| Strong bearish | << 0 | Price significantly below regression forecast |

### Signal Patterns

- **Zero-line crossover (bullish)**: Inertia crosses from negative to positive; price has risen above its linear forecast.
- **Zero-line crossover (bearish)**: Inertia crosses from positive to negative; price has fallen below its forecast.
- **Divergence**: Price making new highs while Inertia makes lower highs warns that the uptrend is decelerating relative to its regression.
- **Magnitude expansion**: Increasing absolute Inertia values indicate trend acceleration beyond what the regression captures.

### Practical Notes

- Because output is in price units, "large" values depend on the instrument. A 2.0 Inertia on a $10 stock is 20% deviation; on a $2000 index, it is 0.1%. Use CFO for cross-instrument comparison.
- Shorter periods (5-10) produce noisier signals but react faster to reversals. Longer periods (30-50) produce smoother readings but lag behind sharp moves.
- Combining Inertia with a trend filter (e.g., ADX or moving average slope) reduces false zero-crossover signals in choppy markets.

## Related Indicators

- [**CFO**](../cfo/Cfo.md): Normalized version of Inertia: `CFO = 100 × Inertia / source`.
- [**DPO**](../dpo/Dpo.md): Detrended Price Oscillator, removes trend using a displaced moving average rather than regression.
- [**TRIX**](../trix/Trix.md): Triple EMA rate-of-change, another approach to measuring momentum.

## Validation

No external library provides a direct Inertia equivalent. Validation uses mathematical identity checks and cross-mode consistency.

| Check | Status | Notes |
|-------|--------|-------|
| Streaming vs Batch vs Span | ✅ | Batch and Span match within 1e-12; streaming within 1e-4 (drift between resyncs) |
| CFO relationship | ✅ | `Inertia ≈ CFO × source / 100` within 1e-6 for periods 5, 10, 14, 20, 50 |
| Perfect linear trend | ✅ | Inertia = 0.0 within 1e-10 |
| Manual OLS last window | ✅ | Matches hand-computed regression within 1e-6 |
| Multi-period consistency | ✅ | Different periods produce distinct results |

## Performance Profile

### Key Optimizations

- **O(1) streaming update**: Incremental sumY/sumXY maintenance avoids re-scanning the buffer.
- **Precomputed regression constants**: `_sumX` and `_denomX` computed once.
- **FMA for TSF**: `Math.FusedMultiplyAdd(slope, period - 1, intercept)`.
- **Periodic resync**: Full recalculation every 1000 ticks bounds drift without constant O(period) cost.
- **State copy pattern**: `_state`/`_p_state` record struct for zero-allocation bar correction.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Additions | 3 (sumY, sumXY maintenance, inertia) |
| Subtractions | 2 (oldest removal from sumY, sumXY) |
| Multiplications | 3 (slope numerator, intercept, TSF) |
| Divisions | 2 (slope, intercept) |
| FMA calls | 1 (TSF) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| sumY/sumXY accumulation | Scalar (sequential dependency via incremental update) |
| Regression computation | Scalar (3 divisions per bar) |
| Residual subtraction | Scalar (trivial, not worth vectorizing alone) |
| Vectorization potential | Low — sequential accumulation prevents SIMD |

## Common Pitfalls

1. **Comparing Inertia across instruments.** The output is in price units. A 5.0 Inertia on a $50 stock means something very different from 5.0 on a $5000 index. Use CFO for normalized comparison.
2. **Expecting bounded overbought/oversold zones.** Inertia has no fixed upper or lower limit. Setting hardcoded thresholds will produce inconsistent results across different instruments and timeframes.
3. **Ignoring the streaming drift.** The O(1) incremental accumulation drifts between resyncs (every 1000 bars). For ultra-long-running streams, the 1e-5 tolerance is acceptable for trading but not for scientific benchmarks.
4. **Short periods on noisy data.** Period=5 on intraday tick data produces extremely noisy zero-crossings. Increase the period or add a smoothing layer.
5. **Confusing Inertia with momentum.** Inertia measures deviation from regression, not rate of change. A stock moving steadily upward at the exact regression rate shows Inertia ≈ 0, while momentum indicators would show a strong positive reading.

## FAQ

**Q: How does Inertia differ from CFO?**
A: They compute the same residual (source - TSF). CFO divides by source and multiplies by 100 to produce a percentage. Inertia reports the raw difference in price units. Choose based on whether you need absolute deviation (Inertia) or normalized comparison (CFO).

**Q: Why does streaming diverge from batch after many bars?**
A: The O(1) incremental sumXY maintenance accumulates floating-point cancellation errors over time. The periodic resync (every 1000 bars) resets these accumulators from the buffer, bounding the drift to approximately 1e-5 between resyncs.

**Q: What does Inertia = 0 mean?**
A: Price is exactly at the linear regression's forecast point. This happens when price perfectly follows the regression trend. It does not mean "no momentum" — a strongly trending stock can have Inertia near zero if the trend is perfectly linear.

## References

- Dorsey, Donald. "The Relative Volatility Index." *Technical Analysis of Stocks & Commodities*, 1993.
- Chande, Tushar. *The New Technical Trader*. John Wiley & Sons, 1994.
- [PineScript reference](inertia.pine)
