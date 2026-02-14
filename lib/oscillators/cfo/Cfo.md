# CFO: Chande Forecast Oscillator

> "The distance between where you are and where regression says you should be tells you everything about momentum." -- Tushar Chande, paraphrased

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | Source (close) |
| Parameters   | `period` (int, default: 14, valid: > 0) |
| Outputs      | double (single value, percentage) |
| Output range | Unbounded, centered at zero (typically -10 to +10) |
| Warmup       | `period` bars (default: 14) |

### Key takeaways

- CFO measures the percentage difference between the current price and its linear regression forecast (Time Series Forecast), quantifying how far price has deviated from its own trend.
- Primary use: detecting momentum via regression deviation. Positive CFO means price is above where the trend line predicts; negative means it has fallen behind.
- Unlike moving average oscillators (MACD, APO), CFO uses least-squares regression, which is mathematically optimal for fitting a straight line to the lookback window and produces zero output on a perfect linear trend.
- The implementation achieves O(1) per update via incremental sumY/sumXY maintenance, avoiding the O(N) cost of a full regression recalculation each bar.
- When source price is exactly zero, CFO returns NaN to avoid division by zero.

## Historical Context

Tushar Chande introduced the Forecast Oscillator in *The New Technical Trader* (1994). Chande's innovation was combining linear regression (traditionally a statistical tool) with the oscillator framework that traders were familiar with. Rather than just plotting the regression line on a chart, he normalized the deviation as a percentage of price, creating a bounded-ish oscillator that is directly comparable across time periods.

The Forecast Oscillator is not widely implemented in standard TA libraries. No direct equivalent exists in TA-Lib, Tulip, Skender, or Ooples. Validation relies on cross-referencing the Time Series Forecast component against QuanTAlib's own `LinReg` class (which computes the same regression) and known mathematical properties (CFO = 0 for a perfect linear trend, CFO = 0 for constant input).

Most textbook implementations compute a full least-squares regression each bar at O(N) cost. QuanTAlib uses an incremental algorithm from the PineScript reference that maintains running `sumY` and `sumXY` accumulators, achieving O(1) per update after warmup. The trade-off is floating-point drift in the running sums, bounded by periodic resynchronization every 1,000 ticks.

## What It Measures and Why It Matters

CFO answers: "How far is the current price from where linear regression says it should be?" If a stock has been climbing steadily at $1/bar, the regression line predicts the next value at the current level. If actual price is $2 above the line, CFO is positive -- price is running ahead of its fitted trend. If price is $2 below, CFO is negative -- price has fallen behind.

The percentage normalization ($100 \times (P - TSF) / P$) makes CFO somewhat comparable across different price levels, though it is not perfectly bounded. On a $100 stock, a $5 deviation (CFO ≈ 5%) is a moderate signal. On a $10 stock, the same $5 deviation (CFO ≈ 50%) would be extreme. In practice, CFO values between -5 and +5 represent normal fluctuations around the trend; values beyond that range suggest significant deviation.

CFO is most useful for trend-following systems where you want to quantify whether momentum is above or below the statistically best-fit trend. It is more mathematically rigorous than EMA-based oscillators (which use exponential weighting) because ordinary least-squares regression minimizes the sum of squared errors. The cost is that regression is more sensitive to outliers at the window boundary (the oldest point exiting and the newest point entering have outsized influence on the slope).

## Mathematical Foundation

### Core Formula

CFO is computed from a linear regression over the lookback window:

**Step 1: Precomputed Constants** (computed once in constructor)

$$
S_x = \frac{N(N-1)}{2}, \quad S_{x^2} = \frac{N(N-1)(2N-1)}{6}, \quad D = N \cdot S_{x^2} - S_x^2
$$

**Step 2: Running Statistics** (maintained O(1) per bar)

$$
S_y = \sum_{i=0}^{N-1} P_{t-i}, \quad S_{xy} = \sum_{i=0}^{N-1} i \cdot P_{t-i}
$$

**Step 3: Regression**

$$
\text{slope} = \frac{N \cdot S_{xy} - S_x \cdot S_y}{D}
$$

$$
\text{intercept} = \frac{S_y - \text{slope} \cdot S_x}{N}
$$

**Step 4: Time Series Forecast**

$$
TSF_t = \text{slope} \cdot (N - 1) + \text{intercept}
$$

**Step 5: Forecast Oscillator**

$$
CFO_t = 100 \times \frac{P_t - TSF_t}{P_t}
$$

where:

- $P_t$ = source price at bar $t$
- $N$ = lookback period (default 14)
- When $P_t = 0$, $CFO = NaN$

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 14 | $N > 0$ |

### Warmup Period

$$
\text{WarmupPeriod} = N
$$

The `IsHot` flag activates when the internal `RingBuffer` is full (after `period` bars). Pre-warmup output is 0.0.

## Architecture & Physics

CFO uses O(1) incremental regression with precomputed x-axis constants and running y-axis accumulators.

```
Source ──→ RingBuffer(period) ──→ Incremental SumY/SumXY ──→ Slope, Intercept ──→ TSF ──→ CFO%
```

### 1. O(1) Incremental SumXY Maintenance

The key optimization is maintaining `SumXY` without recomputing the full dot product each bar. When the buffer is full:

1. Remove oldest value from `SumY`
2. Subtract `SumY` from `SumXY` (effectively shifts all x-indices down by 1)
3. Add `(period - 1) × newValue` to `SumXY` (new value enters at the highest x-index)
4. Add `newValue` to `SumY`

During warmup (buffer filling): `SumXY += count × newValue; count++; SumY += newValue`.

### 2. FMA for TSF

The Time Series Forecast uses `Math.FusedMultiplyAdd(slope, period - 1, intercept)` to compute `slope × (N-1) + intercept` in a single fused operation.

### 3. Periodic Resynchronization

Floating-point drift accumulates in the running sums. Every 1,000 ticks, `RecalculateSums()` recomputes `SumY` and `SumXY` from the ring buffer. This bounds the maximum accumulated error to approximately 1,000 additions' worth of ULP drift.

### 4. Edge Cases

- **NaN/Infinity inputs**: Substituted with `LastValid` (or 0.0 if no valid input exists).
- **Zero price**: Returns NaN (division by zero in the percentage normalization).
- **Perfect linear trend**: CFO = 0 exactly, since the regression line passes through all points.
- **Constant price**: CFO = 0, since slope = 0 and TSF = mean = source.
- **Bar correction**: `isNew=false` restores `_p_state`, updates newest buffer entry, and triggers full recalculation.

## Interpretation and Signals

### Signal Zones

| Zone | Level | Interpretation |
|------|-------|----------------|
| Strong bullish | CFO > +5 | Price significantly above regression forecast |
| Bullish | CFO > 0 | Price above forecast; momentum above trend |
| Neutral | CFO ≈ 0 | Price tracking the regression line |
| Bearish | CFO < 0 | Price below forecast; momentum below trend |
| Strong bearish | CFO < -5 | Price significantly below regression forecast |

### Signal Patterns

- **Zero-line crossover**: CFO crossing from negative to positive signals that price has moved from below to above its regression forecast. This is the primary signal, analogous to a MACD zero-line cross but using regression rather than EMA.
- **Divergence**: Price making new highs while CFO makes lower highs indicates the regression slope is steepening -- the trend is extrapolating further but price is not keeping up with its own acceleration. This suggests the trend may be exhausting.
- **CFO = 0 on a trend**: In a perfectly linear uptrend, CFO remains at zero. Deviations from zero measure how "non-linear" the current move is -- acceleration (CFO > 0) or deceleration (CFO < 0) relative to the fitted line.

### Practical Notes

CFO is best used on daily or weekly charts where price tends to follow more linear trends. On intraday charts, price paths are less linear, producing noisier CFO readings. Pair CFO with a trend indicator (SMA direction, ADX) to filter signals: zero-line crossovers in the direction of the larger trend are more reliable than counter-trend crossovers. The default period of 14 provides a good balance between responsiveness and smoothness; shorter periods amplify noise, longer periods increase lag.

## Related Indicators

- **[DPO](../dpo/Dpo.md)**: Detrended Price Oscillator. DPO removes trend by subtracting a shifted SMA; CFO removes trend by subtracting a regression forecast. Different detrending methods, similar concept.
- **[APO](../apo/Apo.md)**: Absolute Price Oscillator. APO uses EMA difference for momentum; CFO uses regression deviation. CFO is mathematically more rigorous but more computationally complex.
- **[Inertia](../inertia/Inertia.md)**: Uses RVI (Relative Vigor Index) smoothed by linear regression. CFO and Inertia both leverage regression but in different ways.

## Validation

Validated via cross-referencing in [`Cfo.Validation.Tests.cs`](Cfo.Validation.Tests.cs).

| Library | Status | Notes |
|---------|:------:|-------|
| **LinReg cross-validation** | ✓ | CFO reconstructed from `LinReg.Last.Value` (TSF), tolerance 1e-6, periods 5/10/14/20/50 |
| **Self-consistency** | ✓ | Batch and span agree to 1e-12; streaming agrees to 1e-4 (drift between resyncs) |
| **Known values** | ✓ | Perfect linear trend yields CFO = 0 to 1e-10 |
| **Multi-period** | ✓ | Different periods produce different results (no parameter aliasing) |
| **TA-Lib** | -- | Not implemented |
| **Tulip** | -- | Not implemented |

The streaming vs batch tolerance is 1e-4 (not 1e-9) because the O(1) incremental sumXY maintenance accumulates floating-point cancellation drift between the 1,000-tick resync intervals. Batch and span use the same code path and agree to 1e-12.

## Performance Profile

### Key Optimizations

- **O(1) incremental regression**: Running `SumY` and `SumXY` avoid O(N) full regression each bar.
- **Precomputed x-axis constants**: `_sumX` and `_denomX` are computed once in the constructor and stored as `readonly` fields.
- **FMA for TSF**: `Math.FusedMultiplyAdd(slope, period - 1, intercept)` eliminates one intermediate rounding step.
- **Resync guard**: Every 1,000 ticks, sums are recalculated from the ring buffer to bound drift.
- **Aggressive inlining**: `Update`, `Handle`, and `Batch(Span)` are decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| ADD/SUB   | 4     | 1             | 4        |
| MUL       | 3     | 3             | 9        |
| FMA       | 1     | 4             | 4        |
| DIV       | 3     | 15            | 45       |
| **Total** | **11** | --           | **~62**  |

The three DIVs (slope denominator, intercept mean, CFO percentage) dominate. The incremental update itself (steps 1-4) costs only ~4 ADD/SUBs.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| Incremental sumY/sumXY | No | Sequential ring buffer dependency |
| Regression computation | No | Depends on running sums |
| CFO normalization | No | Depends on per-element regression output |

CFO is fully sequential: the running sums have sequential dependencies, and all subsequent computations depend on them. No SIMD vectorization is possible.

## Common Pitfalls

1. **Division by zero at source = 0**: When price is exactly zero, CFO returns NaN. Downstream logic must handle this. For instruments that can trade at zero (rare), filter out NaN values.

2. **Unbounded output**: CFO is expressed as a percentage but is not bounded to [-100, +100]. During volatile periods or with very short lookback periods, values can be extreme. Do not assume fixed overbought/oversold thresholds without calibration.

3. **Warmup is 14 bars with defaults**: Before the ring buffer fills, CFO returns 0.0. This is a sentinel value, not a meaningful reading.

4. **Streaming vs batch drift**: The O(1) incremental maintenance accumulates floating-point drift (~1e-5) between the 1,000-tick resync intervals. Batch computation does not share this drift because it also runs incrementally but resets at the start. Validation tests use 1e-4 tolerance for streaming.

5. **Short periods amplify noise**: Period 1 or 2 produces mathematically valid but extremely noisy output. Regression over 2 points is a line between them -- any deviation is captured as a large percentage. Use period >= 5 for meaningful signals.

6. **Bar correction triggers full recalculation**: `isNew=false` restores the previous state and calls `RecalculateSums()` from the ring buffer, degrading to O(N). In high-frequency scenarios with many corrections per bar, this can be costly.

## FAQ

**Q: How does CFO differ from the R-squared indicator?**
A: Both use linear regression, but they measure different things. CFO measures the percentage deviation of price from the regression line. R-squared measures how well the regression fits the data (1.0 = perfect fit). CFO tells you "how far off the trend are you?" while R-squared tells you "how trending is the data?"

**Q: Can I chain CFO after another indicator?**
A: Yes. Use the chaining constructor: `new Cfo(source, 14)`. CFO subscribes to the source's `Pub` event via the `Handle` method inherited from `AbstractBase`.

**Q: Why does CFO use source price for normalization instead of TSF?**
A: Chande's original definition normalizes by `source`: `100 × (source - TSF) / source`. This ensures that when price and TSF agree, CFO is exactly zero, and the deviation is expressed relative to the current price level.

## References

- Chande, T. (1994). *The New Technical Trader*. Wiley. Forecast Oscillator chapter.
- Chande, T. & Kroll, S. (1994). *The New Technical Trader*. Wiley. Companion strategies and applications.
- [PineScript reference](cfo.pine) -- original O(1) incremental implementation.
