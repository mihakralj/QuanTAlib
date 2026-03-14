# EMA: Exponential Moving Average

> *The SMA drops an old price, the average jumps, the signal fires, the market does something unhelpful. The EMA exists because someone finally asked: what if old data just... mattered less?*

## Quick Reference

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (int > 0)               |
| **Outputs**      | Single series (Ema)                    |
| **Output range** | Tracks input                            |
| **Warmup**       | `period` bars                         |
| **PineScript**   | [ema.pine](ema.pine)                       |
| **Signature**    | [ema_signature](ema_signature.md) |

## Key Takeaways

- **Lag Reduction**: Cuts SMA lag by ~50% through exponential weighting
- **Smooth Response**: Reacts faster to price changes than SMA
- **No Drop-off Effect**: Eliminates window boundary discontinuities
- **Bias Compensation**: Mathematically correct warmup (unlike most libraries)
- **Computational Efficiency**: O(1) per update with FMA optimization
- **Universal Standard**: Foundation for MACD, RSI, and countless other indicators

## What It Measures and Why It Matters

The EMA measures the exponentially weighted average trend of price action, giving more importance to recent data while never completely discarding historical information. It matters because traditional simple moving averages suffer from the "drop-off effect"—sudden jumps when old data expires from the calculation window. The EMA's infinite impulse response eliminates this discontinuity, providing smoother, more reliable trend signals. This makes it the gold standard for trend-following systems, serving as the computational backbone for most technical analysis tools.

## Interpretation and Signals

### Trend Direction

- **Above Price**: Potential uptrend (EMA as support)
- **Below Price**: Potential downtrend (EMA as resistance)
- **Slope Analysis**: Positive slope = bullish momentum, negative slope = bearish

### Crossover Signals

- **Price crosses above EMA**: Bullish momentum signal
- **Price crosses below EMA**: Bearish momentum signal
- **Multiple EMAs**: Fast EMA over slow EMA = bullish trend

### Divergence Analysis

- **Bullish Divergence**: Price makes lower low, EMA makes higher low
- **Bearish Divergence**: Price makes higher high, EMA makes lower high
- **Hidden Divergence**: Price makes higher low, EMA makes lower low (continuation)

### Signal Quality Factors

- **Trend Strength**: Distance between price and EMA
- **Slope Steepness**: Rate of EMA angle change
- **Volume Confirmation**: Higher volume validates EMA breakouts

## Historical Context

The EMA entered financial analysis to solve a specific problem with the SMA: window discontinuity. Picture a 20-day SMA cruising along smoothly. Then an outlier price from exactly 20 days ago drops out of the window. The average jumps. The signal fires. The position opens. The market, with characteristic indifference, moves the other way.

This "drop-off effect" made the SMA behave like a meticulously organized filing cabinet that occasionally explodes. By using a recursive formula, the EMA includes *all* past data in its calculation, with weights diminishing exponentially toward zero. No drop-off, no discontinuity. This makes it an Infinite Impulse Response (IIR) filter in signal processing terminology: the impulse response never fully reaches zero, but it gets small enough that even the most pedantic quant can be persuaded to ignore it.

## Architecture & Physics

The EMA is controlled by a single parameter: the smoothing factor $\alpha$.

$$
\alpha = \frac{2}{N + 1}
$$

where $N$ is the "period" (a human-friendly proxy for decay rate).

| Period | Alpha | Half-life (bars) | Behavior |
| -----: | ----: | ---------------: | :------- |
| 5 | 0.333 | ~2.4 | Very responsive, noisy |
| 10 | 0.182 | ~4.4 | Fast, some noise |
| 20 | 0.095 | ~8.7 | Balanced |
| 50 | 0.039 | ~21.8 | Smooth, significant lag |
| 100 | 0.020 | ~43.7 | Very smooth, very laggy |

The half-life formula: $t_{1/2} = \frac{\ln(2)}{\ln(1/(1-\alpha))} \approx \frac{N-1}{2}$

### Warmup Compensation

Standard EMA implementations start at zero (or seed with the first price) and take approximately $3N$ bars to converge within 5% of the true value. During warmup, the output is biased.

QuanTAlib implements a mathematical compensator that corrects for initialization bias:

$$
E_t = (1 - \alpha)^t
$$

$$
\text{Corrected}_t = \frac{\text{Raw}_t}{1 - E_t}
$$

This produces statistically valid output from bar one. The first 14 bars of a 10-period EMA will differ from TA-Lib. TA-Lib uses an approximation (the technical term is "good enough for most purposes, which is precisely the problem"). QuanTAlib uses the mathematically correct value.

## Mathematical Foundation

### Recursive Formula

$$
\text{EMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{t-1}
$$

Rewritten for fused multiply-add optimization:

$$
\text{EMA}_t = \text{FMA}(\text{EMA}_{t-1}, \text{decay}, \alpha \cdot P_t)
$$

where $\text{decay} = 1 - \alpha$.

### Transfer Function

In z-domain:

$$
H(z) = \frac{\alpha}{1 - (1-\alpha) z^{-1}}
$$

This is a first-order IIR low-pass filter with cutoff frequency determined by $\alpha$.

### Frequency Response

The -3dB cutoff frequency:

$$
f_c = \frac{\alpha}{2\pi} \cdot f_s
$$

For a 20-period EMA on daily data: $f_c \approx 0.015$ cycles/day, or roughly a 67-day period.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| FMA | 1 | 4 | 4 |
| MUL | 1 | 3 | 3 |
| **Total (post-warmup)** | **2** | — | **~7 cycles** |

During warmup (first ~3N bars), additional operations for bias compensation:

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| MUL | 1 | 3 | 3 |
| SUB | 1 | 1 | 1 |
| DIV | 1 | 15 | 15 |
| CMP | 2 | 1 | 2 |
| **Warmup overhead** | **5** | — | **~21 cycles** |

**Total during warmup:** ~28 cycles/bar. **Post-warmup:** ~7 cycles/bar.

### SIMD Analysis

EMA is inherently recursive: each value depends on the previous. SIMD parallelization across bars is not possible. The recursive dependency chain cannot be vectorized.

Available optimizations:

| Technique | Benefit |
| :-------- | :------ |
| FMA instruction | ~2 cycles saved vs MUL+ADD |
| Loop unrolling (4×) | Reduced branch overhead |
| Unsafe memory access | Eliminated bounds checking |

### Benchmark Results

Test environment: Apple M4, .NET 10.0, AdvSIMD, 500,000 bars.

| Metric | Value | Notes |
| :----- | ----: | :---- |
| **Span throughput** | 381 μs / 500K bars | 0.76 ns/bar |
| **Streaming throughput** | ~2 ns/bar | Single `Update()` call |
| **Allocations (hot path)** | 0 bytes | Verified via BenchmarkDotNet |
| **Complexity** | O(1) | Per-bar |
| **State size** | 32 bytes | Two doubles + flags |

### Comparative Performance

| Library | Time (500K bars) | Allocated | Relative |
| :------ | ---------------: | --------: | :------- |
| **QuanTAlib (Span)** | 381 μs | 0 B | baseline |
| Tulip | 353 μs | 0 B | 0.93× |
| TA-Lib | 357 μs | 34 B | 0.94× |
| Skender | 10,635 μs | 23.6 MB | 27.9× slower |

QuanTAlib matches C-based libraries (Tulip, TA-Lib) in throughput while providing bias-corrected results and zero allocations.

### Quality Metrics

| Metric | Score | Notes |
| :----- | ----: | :---- |
| **Accuracy** | 8/10 | Reliable trend tracking |
| **Timeliness** | 7/10 | Lag of ~N/2 bars |
| **Overshoot** | 8/10 | Minimal on reversals |
| **Smoothness** | 7/10 | Good noise rejection |

## Related Indicators

- **SMA**: Simple moving average (equal weights, maximum lag)
- **DEMA**: Double exponential (less lag, more overshoot)
- **TEMA**: Triple exponential (minimum lag, maximum overshoot)
- **WMA**: Weighted moving average (linear decay, FIR)
- **KAMA**: Adaptive smoothing based on volatility
- **VIDYA**: Variable index dynamic average
- **HMA**: Hull moving average (triple smoothing)

## Reference Calculation Table

| Period | Price Sequence | α | EMA Values | Notes |
|--------|----------------|---|------------|-------|
| 5 | 10 | 0.333 | 10.00 | Initial value |
| 5 | 10, 20 | 0.333 | 10.00, 13.33 | First calculation |
| 5 | 10, 20, 30 | 0.333 | 10.00, 13.33, 18.52 | Trend acceleration |
| 5 | 10, 20, 30, 40 | 0.333 | 10.00, 13.33, 18.52, 24.69 | Convergence |
| 5 | 10, 20, 30, 40, 50 | 0.333 | 10.00, 13.33, 18.52, 24.69, 31.13 | Full convergence |

*α = 2/(5+1) = 0.333, Decay = 1-α = 0.667*

## FAQ

**Q: How does EMA differ from SMA?**
A: EMA gives exponentially decreasing weights to older data, eliminating the "drop-off effect" where SMA jumps when old data expires. EMA responds faster and smoother.

**Q: Why does QuanTAlib's EMA differ from other libraries initially?**
A: QuanTAlib uses mathematical bias compensation for accurate warmup values. Other libraries approximate. Results converge after ~3×period bars.

**Q: What's the optimal EMA period?**
A: No universal optimum. Shorter periods (<10) for scalping, longer periods (>50) for trend following. Match to your timeframe and strategy horizon.

**Q: Can EMA be used for mean reversion?**
A: Poorly. EMA follows trends. For mean reversion, consider Bollinger Bands or RSI around EMA levels.

**Q: How does bar correction work?**
A: Use `isNew=false` when updating the same bar with revised prices. QuanTAlib maintains previous state for atomic rollback.

## Validation

Validated against external libraries in `Ema.Validation.Tests.cs`. Tests run against 5,000 bars with tolerance of 1e-9.

| Library | Batch | Streaming | Span | Notes |
| :------ | :---: | :-------: | :--: | :---- |
| **TA-Lib** | ✅ | ✅ | ✅ | Matches after warmup (TA-Lib lacks compensator) |
| **Skender** | ✅ | ✅ | ✅ | Matches `GetEma()` |
| **Tulip** | ✅ | ✅ | ✅ | Matches `ema` indicator |
| **Ooples** | ✅ | — | — | Matches `CalculateExponentialMovingAverage()` |

Run validation:

```bash
dotnet test --filter "FullyQualifiedName~EmaValidation"
```

## Common Pitfalls

1. **Warmup Divergence**: QuanTAlib uses bias compensation. Other libraries approximate. The first $N$ bars will differ. After ~3N bars, all libraries converge. Skip the first 3N bars when comparing cross-library results.

2. **Alpha vs. Period Confusion**: `Ema(10)` uses $\alpha = 0.182$. `Ema(0.1)` uses $\alpha = 0.1$, equivalent to period ~19. The constructors accept both formats. They are not equivalent.

3. **Lag Expectations**: A 20-period EMA lags approximately 10 bars behind price. The EMA reduces lag versus SMA but does not eliminate it. Zero-lag filters exist (JMA, Ehlers) but introduce their own complications. There is no free lunch, only differently priced lunches.

4. **Period-Timeframe Mismatch**: An EMA(5) on hourly bars has a half-life of ~2.5 hours. Minor fluctuations become signals. The trading system interprets every coffee break as a trend reversal. Match period length to timeframe and expected signal duration.

5. **Bar Correction Handling**: When processing live ticks within the same bar, use `Update(value, isNew: false)`. Use `isNew: true` (default) only when a new bar opens. Incorrect usage causes the EMA to advance N times faster than intended.

6. **Cross-Library Comparison Window**: When validating against TA-Lib or Tulip, compare only bars after index 3N. Earlier bars will differ due to warmup handling differences.

## Usage Examples

```csharp
// Streaming: one bar at a time
var ema = new Ema(20);
foreach (var bar in liveStream)
{
    var result = ema.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"EMA: {result.Value:F2}");
}

// Alpha-based construction (signal processing convention)
var fastEma = new Ema(0.2);  // α=0.2, roughly period 9

// Batch processing with Span (zero allocation)
double[] prices = LoadHistoricalData();
double[] emaValues = new double[prices.Length];
Ema.Batch(prices.AsSpan(), emaValues.AsSpan(), period: 20);

// Batch processing with TSeries
var series = new TSeries();
// ... populate series ...
var results = Ema.Batch(series, period: 20);

// Event-driven chaining
var source = new TSeries();
var ema20 = new Ema(source, 20);
var ema50 = new Ema(source, 50);
source.Add(new TValue(DateTime.UtcNow, 100.0));  // Both EMAs update

// Pre-load with historical data
var ema = new Ema(20);
ema.Prime(historicalPrices);  // Ready for live data
```

## Implementation Notes

### State Structure

```csharp
private record struct State(double Ema, double E, bool IsHot, bool IsCompensated);
```

| Field | Size | Purpose |
| :---- | ---: | :------ |
| `Ema` | 8 bytes | Running exponential average |
| `E` | 8 bytes | Compensator factor $(1-\alpha)^n$ |
| `IsHot` | 1 byte | Warmup complete flag |
| `IsCompensated` | 1 byte | True when E < 1e-10 |

**Total state:** ~18 bytes per instance. No buffers required regardless of period. IIR filters are inherently self-correcting and do not require periodic resynchronization.

### FMA Optimization

The core update uses `Math.FusedMultiplyAdd` for single-instruction precision:

```csharp
state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input);
```

This computes `Ema * decay + alpha * input` with a single rounding operation instead of two.

### Loop Unrolling

Batch processing unrolls by 4 to reduce branch overhead:

```csharp
for (; i < unrollEnd; i += 4)
{
    state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * Unsafe.Add(ref srcRef, i));
    state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * Unsafe.Add(ref srcRef, i + 1));
    state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * Unsafe.Add(ref srcRef, i + 2));
    state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * Unsafe.Add(ref srcRef, i + 3));
}
```

### Bar Correction

The `_state` / `_p_state` pattern enables correction of the current bar:

```csharp
if (isNew)
{
    _p_state = _state;
    _p_lastValidValue = _lastValidValue;
}
else
{
    _state = _p_state;
    _lastValidValue = _p_lastValidValue;
}
```

### Memory Summary

| Component | Size |
| :-------- | ---: |
| State struct | ~32 bytes |
| Instance fields | ~48 bytes |
| **Total per instance** | **~80 bytes** |
| **Additional buffers** | **0 bytes** |

## References

- Hunter, J. S. (1986). "The Exponentially Weighted Moving Average." *Journal of Quality Technology*, 18(4), 203-210.
- Roberts, S. W. (1959). "Control Chart Tests Based on Geometric Moving Averages." *Technometrics*, 1(3), 239-250.
- Ehlers, J. F. (2001). *Rocket Science for Traders*. John Wiley & Sons. Chapter 3: Smoothing. (The title oversells it slightly, but the content is solid.)
