# FISHER: Ehlers Fisher Transform

> "The Fisher Transform turns price into a well-behaved Gaussian — because sometimes, the best way to see a reversal is to force the data to confess."

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Single value (Close or HL2) |
| **Parameters** | `period` (default 10), `alpha` (default 0.33) |
| **Outputs** | Fisher line (`Last`), Signal line |
| **Output range** | Unbounded (typically -3 to +3) |
| **Warmup period** | `period` |

### Key takeaways

- Applies the **inverse hyperbolic tangent** (arctanh) to normalized price, converting a near-uniform distribution into a Gaussian with sharp peaks and troughs.
- Produces **abrupt turning points** at extremes, making reversals easier to identify than with conventional bounded oscillators.
- The output is **unbounded**, routinely reaching values beyond ±2, which distinguishes overbought/oversold conditions more cleanly than clamped indicators.
- An **EMA-smoothed signal line** (same α) provides crossover signals analogous to MACD's signal line.
- Domain protection clamps the pre-transform value to (-0.999, 0.999) to prevent the logarithm from diverging to infinity.

## Historical Context

John Ehlers introduced the Fisher Transform in his 2002 article "Using The Fisher Transform" for *Stocks & Commodities* magazine. His insight was that price data, while not normally distributed, can be forced into an approximately Gaussian shape through a nonlinear transformation. The arctanh function stretches values near ±1 toward infinity while compressing values near zero, which means that when price reaches the extremes of its recent range, the Fisher output spikes sharply rather than rolling over gently.

The transformation solved a practical problem: most oscillators (RSI, Stochastic, CCI) produce rounded peaks and troughs that make timing entries and exits ambiguous. The Fisher Transform's sharp turns create unambiguous inflection points. The trade-off is that the output is unbounded, so traders cannot rely on fixed overbought/oversold thresholds across all instruments and timeframes. In practice, values beyond ±1.5 to ±2.0 indicate extremes, but the exact levels depend on the instrument's volatility characteristics.

## What It Measures and Why It Matters

The Fisher Transform measures where price sits within its recent range, normalizes that position to [-1, 1], smooths the result with an EMA, then stretches the smoothed value through arctanh. The normalization step answers "where is price relative to its recent range?" while the arctanh stretching answers "how extreme is that position?" The EMA smoothing between normalization and transformation prevents the raw jitter of bar-to-bar normalization from producing false spikes.

The signal line is simply an EMA of the Fisher output using the same smoothing factor. Crossovers between the Fisher and Signal lines serve as entry/exit triggers, similar to MACD crossovers but with the advantage that the underlying distribution is Gaussian, making the crossover points statistically more meaningful.

## Mathematical Foundation

### Core Formula

Normalize price to $[-1, 1]$:

$$v_{norm} = 2 \times \frac{price - LL_n}{HH_n - LL_n} - 1$$

Apply EMA smoothing:

$$v_{ema} = \alpha \cdot v_{norm} + (1 - \alpha) \cdot v_{ema,prev}$$

Clamp to the valid arctanh domain:

$$v_{clamp} = \text{clamp}(v_{ema}, \, -0.999, \, 0.999)$$

Apply the Fisher Transform (arctanh):

$$Fisher = \frac{1}{2} \ln\!\left(\frac{1 + v_{clamp}}{1 - v_{clamp}}\right)$$

Compute the signal line:

$$Signal = \alpha \cdot Fisher + (1 - \alpha) \cdot Signal_{prev}$$

### Parameter Mapping

| Parameter | Formula role | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window for $HH_n$ / $LL_n$ normalization | 10 | > 0 |
| `alpha` | EMA smoothing factor for both normalization and signal | 0.33 | (0, 1] |

### Warmup Period

$$W = period$$

Default configuration (period=10) warms up in 10 bars.

## Architecture & Physics

### 1. Normalization via RingBuffer

The [`RingBuffer`](lib/oscillators/fisher/Fisher.cs:27) stores the last `period` values. On each update, the buffer is scanned for highest/lowest to normalize price to [-1, 1]. This O(period) scan runs on each bar; for typical period values (5-50), the cost is negligible.

### 2. EMA Pre-Smoothing

Before applying arctanh, the normalized value is smoothed with an EMA using [`Math.FusedMultiplyAdd`](lib/oscillators/fisher/Fisher.cs:133) for the `decay * prev + alpha * input` pattern. This prevents single-bar noise from producing false Fisher spikes.

### 3. Domain Clamping

The arctanh function diverges at ±1. [`Math.Clamp`](lib/oscillators/fisher/Fisher.cs:136) restricts the EMA-smoothed value to (-0.999, 0.999), ensuring finite output. The 0.001 margin is sufficient for double-precision arithmetic.

### 4. Dual Output

[`FisherValue`](lib/oscillators/fisher/Fisher.cs:88) holds the primary Fisher Transform output; [`Signal`](lib/oscillators/fisher/Fisher.cs:93) holds the EMA-smoothed signal line. Both are updated atomically on each [`Update`](lib/oscillators/fisher/Fisher.cs:97) call.

### 5. Edge Cases

- **Zero range**: When all values in the buffer are identical, normalized value is 0.0, producing Fisher ≈ 0.0.
- **NaN/Infinity inputs**: Last-valid substitution; falls back to 0.0 if no valid data has been seen.
- **Extreme EMA values**: Clamping to ±0.999 prevents log domain errors regardless of input sequence.

## Interpretation and Signals

### Signal Zones

| Zone | Fisher value | Meaning |
|------|-------------|---------|
| Strong overbought | > +2.0 | Extreme bullish stretch, reversal probable |
| Overbought | +1.0 to +2.0 | Bullish momentum, watch for exhaustion |
| Neutral | -1.0 to +1.0 | No directional conviction |
| Oversold | -2.0 to -1.0 | Bearish momentum, watch for recovery |
| Strong oversold | < -2.0 | Extreme bearish stretch, reversal probable |

### Signal Patterns

- **Fisher crosses above Signal**: Bullish entry, strongest when both are below -1.0.
- **Fisher crosses below Signal**: Bearish entry, strongest when both are above +1.0.
- **Fisher reversal from extreme**: Sharp peak above +2.0 followed by downturn warns of sell-off.
- **Divergence**: Price making new highs with Fisher making lower highs signals trend exhaustion.
- **Zero-line cross**: Fisher crossing zero indicates a shift in the directional bias.

### Practical Notes

- The unbounded nature means threshold levels (±1.5, ±2.0) should be calibrated per instrument and timeframe. What constitutes "extreme" for a low-volatility bond ETF differs from a crypto pair.
- The α = 0.33 default provides a balance between responsiveness and noise rejection. Lower alpha values (0.1-0.2) produce smoother but slower signals.
- In strongly trending markets, Fisher can remain at extreme values for extended periods. Do not fade a trend solely because Fisher appears overbought.

## Related Indicators

- [**Stochastic**](../stoch/Stoch.md): Linear normalization without the arctanh stretching.
- [**StochRSI**](../stochrsi/Stochrsi.md): Stochastic of RSI, another approach to sharpening oscillator signals.
- [**CFO**](../cfo/Cfo.md): Chande Forecast Oscillator, unbounded momentum measure using linear regression.

## Validation

No standard TA-Lib implementation matches this exact formulation (Ehlers' EMA-smoothed variant with configurable alpha). Validation is performed against manual arctanh computation and cross-mode consistency.

| Check | Status | Notes |
|-------|--------|-------|
| Manual arctanh identity | ✅ | `0.5 × ln((1+v)/(1-v))` matches `Math.Atanh(v)` within 1e-12 |
| Manual computation | ✅ | Batch matches step-by-step manual for periods 5, 10, 20 |
| Multiple periods (5, 10, 20, 50) | ✅ | All outputs finite across all periods |
| Streaming vs Batch vs Span | ✅ | All three modes agree within 1e-9 |
| Event-based vs Streaming | ✅ | Identical within 1e-12 |

## Performance Profile

### Key Optimizations

- **FMA in EMA updates**: Both normalization EMA and signal EMA use `Math.FusedMultiplyAdd`.
- **Precomputed decay**: `_decay = 1.0 - alpha` calculated once in constructor.
- **RingBuffer for O(1) update**: `Add` and `UpdateNewest` are constant-time; only the min/max scan is O(period).
- **State copy pattern**: `_state`/`_p_state` record struct enables bar correction without allocation.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Comparisons | 2 × period (min/max scan) |
| Multiplications | 3 (normalize + 2× EMA) |
| Additions | 3 (normalize + 2× EMA) |
| FMA calls | 2 (normalization EMA, signal EMA) |
| Log | 1 (arctanh) |
| Clamp | 1 |
| Division | 1 (normalization) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| Min/max scan | Scalar (RingBuffer-based, O(period) per bar) |
| Normalization | Scalar (data-dependent division) |
| EMA smoothing | Scalar (IIR recursion, sequential dependency) |
| arctanh | Scalar (`Math.Log`, not vectorizable) |
| Vectorization potential | Low — logarithm + IIR chain prevents SIMD |

## Common Pitfalls

1. **Treating Fisher as bounded.** Unlike RSI or Stochastic, Fisher output has no fixed upper/lower limit. Using hardcoded ±100 thresholds (or any fixed number) will fail across different instruments.
2. **Ignoring the alpha parameter.** The default α = 0.33 is a reasonable starting point but may over-smooth volatile instruments or under-smooth stable ones. Test α values in [0.1, 0.5] for your specific use case.
3. **Confusing Fisher with raw arctanh.** The EMA pre-smoothing step is critical. Applying arctanh directly to the normalized price without smoothing produces extreme noise sensitivity.
4. **Fading strong trends.** Fisher can remain above +2.0 or below -2.0 for many bars during a strong trend. Selling solely because Fisher is "overbought" in a strong uptrend is a common source of losses.
5. **Missing the clamping boundary.** The ±0.999 clamp compresses extreme EMA values. If Fisher seems to plateau at roughly ±3.8 (arctanh of 0.999), this is by design, not a bug.

## FAQ

**Q: Why is the output unbounded while most oscillators are bounded?**
A: The arctanh function maps (-1, 1) to (-∞, +∞). This is intentional: it amplifies the distinction between "near the extremes of the range" and "moderately positioned," producing sharper reversal signals. The ±0.999 clamp limits the theoretical maximum to about ±3.8.

**Q: What does the signal line add over just using the Fisher line?**
A: The signal line smooths the Fisher output with the same EMA factor. Crossovers between Fisher and Signal provide timing precision analogous to MACD signal crossovers, reducing the impact of single-bar spikes in the Fisher output.

**Q: How does this differ from Ehlers' original PineScript version?**
A: The core mathematics is identical. The implementation differs in using a configurable alpha (Ehlers used a fixed 0.33), adding NaN/Infinity handling, and providing both streaming and batch APIs with bar correction support.

## References

- Ehlers, John F. "Using The Fisher Transform." *Stocks & Commodities*, November 2002.
- Ehlers, John F. *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- [PineScript reference](fisher.pine)
