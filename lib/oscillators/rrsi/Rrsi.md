# RRSI: Ehlers Rocket RSI

> *Rocket RSI strips noisy momentum down to its cyclic core, then Fisher-transforms it into a Gaussian — because reversals should announce themselves with a bang, not a whisper.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                       |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `smoothLength` (default 10), `rsiLength` (default 10) |
| **Outputs**      | Single series (RocketRSI)        |
| **Output range** | Unbounded (typically -4 to +4)   |
| **Warmup**       | `smoothLength + rsiLength` bars  |
| **PineScript**   | [rrsi.pine](rrsi.pine)           |

- Ehlers' Rocket RSI chains three transformations — momentum extraction, Super Smoother filtering, and Fisher Transform — to produce a Gaussian-distributed oscillator with sharp turning-point signals.
- **Similar:** [Fisher](../fisher/Fisher.md), [StochRSI](../stochrsi/Stochrsi.md) | **Complementary:** Bollinger Bands for volatility context | **Trading note:** Unbounded oscillator; values beyond ±2 indicate statistical extremes. Not Wilder's RSI — uses Ehlers summation-based RSI variant.
- Validated against manual step-by-step reference implementation of the original TASC algorithm.

Rocket RSI solves a fundamental problem with conventional RSI: the bounded [0, 100] output compresses extreme readings into a narrow band, making precise reversal timing ambiguous. By applying the Fisher Transform (arctanh) to a summation-based RSI computed on Super-Smoothed momentum, Rocket RSI produces sharp Gaussian peaks at cyclic turning points. The Super Smoother pre-filter removes aliasing artifacts that corrupt cycle analysis, while the Fisher Transform stretches values near ±1 toward ±∞, creating unambiguous inflection points.

## Historical Context

John Ehlers published Rocket RSI in the May 2018 issue of *Technical Analysis of Stocks & Commodities* magazine. The indicator represents the intersection of three areas Ehlers had refined over two decades: the Super Smoother filter (introduced in *Cybernetic Analysis for Stocks and Futures*, 2004), summation-based RSI (a departure from Wilder's exponential smoothing), and the Fisher Transform (first presented in his November 2002 TASC article). By combining these three techniques into a single pipeline, Ehlers created an oscillator specifically designed for cyclic reversal detection rather than trend-following.

The key insight was that conventional RSI, computed on raw price data, conflates cyclic and trend components. The Super Smoother acts as a low-pass filter that isolates the dominant cycle, and the summation-based RSI provides a signed measure of directional pressure without the lag introduced by Wilder's exponential decay. The Fisher Transform then converts this into a Gaussian distribution where standard deviation has statistical meaning.

## Architecture & Physics

### 1. Momentum Extraction

[`Update()`](Rrsi.cs:107) computes half-cycle momentum as `Close[i] - Close[i - rsiLength + 1]` using a [`RingBuffer`](Rrsi.cs:36) of size `rsiLength` for O(1) lookback access. This captures the price change over approximately one half-cycle of the dominant period.

### 2. Super Smoother Filter (2-Pole Butterworth IIR)

The momentum is smoothed by a 2-pole Butterworth low-pass filter with coefficients computed once in the [constructor](Rrsi.cs:82): `a1 = exp(-1.414π / smoothLength)`. The filter equation uses `(Mom + Mom[prev]) / 2` as input (simple averaging of adjacent momentum values), which provides an additional anti-aliasing effect. The filter history is stored in the [`_filtBuf`](Rrsi.cs:39) RingBuffer for RSI accumulation.

### 3. Ehlers RSI (Summation-Based)

Unlike Wilder's RSI which uses exponential moving averages of gains and losses, Ehlers RSI sums raw up-changes (CU) and down-changes (CD) of the filtered value over the last `rsiLength` bars, then computes `(CU - CD) / (CU + CD)`. This produces a value in [-1, +1] without the asymmetric decay that causes Wilder's RSI to understate momentum reversals.

### 4. Fisher Transform

The RSI value is clamped to ±0.999 (preventing log domain errors) and passed through `arctanh(x) = 0.5 × ln((1 + x) / (1 - x))`. This nonlinear stretching converts the near-uniform RSI distribution into a Gaussian, amplifying values near the extremes where reversals occur.

### 5. State Management

The [`State`](Rrsi.cs:42) record struct holds momentum history, filter state, and bar count. The `_s`/`_ps` pattern enables bar correction: when `isNew = false`, the previous state (`_ps`) is restored before recalculating, ensuring that intra-bar updates do not corrupt the indicator state.

### 6. Edge Cases

- **NaN/Infinity inputs**: [Last-valid substitution](Rrsi.cs:112); falls back to 0.0 if no valid data has been seen.
- **Insufficient history**: Momentum defaults to 0.0 when the close buffer has fewer than `rsiLength` entries; filter passes momentum through directly for the first two bars.
- **Zero denominator**: When CU + CD < 1e-10 (no price movement), RSI defaults to 0.0.

## Mathematical Foundation

### Core Formula

**Step 1 — Half-Cycle Momentum:**

$$\text{Mom}_i = \text{Close}_i - \text{Close}_{i - (\text{rsiLength} - 1)}$$

**Step 2 — Super Smoother Filter** (2-pole Butterworth IIR):

Coefficients (computed once):

$$a_1 = e^{-1.414\pi / \text{smoothLength}}, \quad b_1 = 2 a_1 \cos\!\left(\frac{1.414\pi}{\text{smoothLength}}\right)$$

$$c_2 = b_1, \quad c_3 = -a_1^2, \quad c_1 = 1 - c_2 - c_3$$

Filter recursion:

$$\text{Filt}_i = c_1 \cdot \frac{\text{Mom}_i + \text{Mom}_{i-1}}{2} + c_2 \cdot \text{Filt}_{i-1} + c_3 \cdot \text{Filt}_{i-2}$$

The DC gain constraint $c_1 + c_2 + c_3 = 1$ ensures the filter passes constant signals without attenuation.

**Step 3 — Ehlers RSI** (normalized to ±1):

Over the last `rsiLength` bars of filter differences:

$$CU = \sum_{j=0}^{n-1} \max(\text{Filt}_{i-j} - \text{Filt}_{i-j-1},\ 0)$$

$$CD = \sum_{j=0}^{n-1} \max(\text{Filt}_{i-j-1} - \text{Filt}_{i-j},\ 0)$$

$$\text{RSI} = \frac{CU - CD}{CU + CD} \in [-1, 1]$$

**Step 4 — Fisher Transform:**

$$\text{RocketRSI} = \text{arctanh}\!\left(\text{clamp}(\text{RSI}, \pm0.999)\right) = \frac{1}{2} \ln\!\left(\frac{1 + v}{1 - v}\right)$$

### Parameter Mapping

| Parameter | Formula role | Default | Constraint |
|-----------|-------------|---------|------------|
| `smoothLength` | Cutoff period for the Super Smoother low-pass filter | 10 | > 0 |
| `rsiLength` | Summation window for CU/CD accumulation and momentum lookback | 10 | > 0 |

### Warmup Period

$$W = \text{smoothLength} + \text{rsiLength}$$

Default configuration (10, 10) warms up in 20 bars.

## Interpretation and Signals

### Signal Zones

| Zone | Rocket RSI value | Meaning |
|------|-----------------|---------|
| Strong overbought | > +2.0 | Extreme bullish stretch, reversal probable |
| Overbought | +1.0 to +2.0 | Bullish momentum, watch for exhaustion |
| Neutral | -1.0 to +1.0 | No directional conviction |
| Oversold | -2.0 to -1.0 | Bearish momentum, watch for recovery |
| Strong oversold | < -2.0 | Extreme bearish stretch, reversal probable |

### Signal Patterns

- **Zero-line cross**: Rocket RSI crossing zero indicates a shift in momentum direction; the Super Smoother removes false crossings from noise.
- **Reversal from extreme**: Sharp peak above +2.0 followed by downturn warns of impending sell-off; mirror for buy signals below -2.0.
- **Divergence**: Price making new highs with Rocket RSI making lower highs signals cycle exhaustion.
- **Peak sharpness**: The Fisher Transform creates V-shaped peaks rather than rounded tops, making the exact bar of reversal unambiguous.

### Practical Notes

- The unbounded nature means threshold levels should be calibrated per instrument and timeframe. What constitutes "extreme" for a low-volatility bond ETF differs from a crypto pair.
- Rocket RSI is designed for **cyclic markets**. In strongly trending markets, the oscillator can remain at extreme values for extended periods. Do not fade a trend solely because Rocket RSI appears overbought.
- Both `smoothLength` and `rsiLength` control the effective cycle period. Increasing either parameter makes the indicator more selective (fewer but higher-quality signals) at the cost of lag.
- Unlike Wilder's RSI (0–100), Rocket RSI is centered at zero and unbounded. There is no direct mapping between RSI levels (e.g., 70/30) and Rocket RSI values.

## Related Indicators

- [**Fisher Transform**](../fisher/Fisher.md): Same arctanh step, but applied to min/max-normalized price rather than RSI.
- [**RSI**](../../momentum/rsi/Rsi.md): Wilder's original bounded [0, 100] momentum oscillator.
- [**RSX**](../../momentum/rsx/Rsx.md): Jurik's ultra-smooth RSI variant using cascaded IIR filters.
- [**StochRSI**](../stochrsi/Stochrsi.md): Stochastic applied to RSI output, another approach to sharpening RSI signals.

## Validation

No external C# library implements Rocket RSI. Validation is performed against a manual step-by-step reference implementation of the original Ehlers TASC May 2018 algorithm.

### Internal Consistency

| Check | Status | Notes |
|-------|--------|-------|
| Manual computation cross-check | ✅ | Batch output matches step-by-step ManualRocketRsi() within 1e-9 for 10,000 points |
| Multiple parameter combos | ✅ | Validated across (5,5), (8,10), (10,10), (10,20), (20,10) |
| arctanh identity | ✅ | `0.5 × ln((1+v)/(1-v))` matches `Math.Atanh(v)` within 1e-12 |
| Super Smoother DC gain | ✅ | `c1 + c2 + c3 = 1.0` verified within 1e-12 for periods 5, 8, 10, 20, 50 |
| Streaming vs Batch vs Span | ✅ | All three modes agree within 1e-9 |
| Event-based vs Streaming | ✅ | Identical within 1e-12 |
| All outputs finite | ✅ | Verified for periods (5,5), (10,10), (20,20), (50,10) across 10,000 bars |

## Performance Profile

### Key Optimizations

- **Precomputed IIR coefficients**: `c1`, `c2`, `c3` calculated once in constructor, avoiding repeated transcendental calls.
- **RingBuffer for O(1) access**: Both close history and filter history use RingBuffers; `Add` and `UpdateNewest` are constant-time.
- **ArrayPool in Batch**: Span-based batch uses `ArrayPool<double>.Shared` to avoid heap allocation for temporary arrays.
- **State copy pattern**: `_s`/`_ps` record struct enables bar correction without allocation.
- **`[SkipLocalsInit]`**: Eliminates zero-initialization overhead for stack locals.
- **`[MethodImpl(AggressiveInlining)]`**: Hot-path methods are inlined by the JIT.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Additions | ~rsiLength + 4 (RSI summation + filter + momentum) |
| Multiplications | 3 (filter: c1, c2, c3) |
| Comparisons | rsiLength (CU/CD classification) |
| Log | 1 (arctanh) |
| Division | 1 (RSI ratio) |
| Clamp | 1 |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| Momentum computation | Scalar (lookback dependency) |
| Super Smoother filter | Scalar (IIR recursion, sequential dependency) |
| RSI summation | Scalar (forward-looking accumulation per bar) |
| Fisher Transform | Scalar (`Math.Log`, not vectorizable) |
| Vectorization potential | Low — IIR chain + logarithm prevents SIMD |

## Common Pitfalls

1. **Treating Rocket RSI as bounded.** Unlike Wilder's RSI [0, 100], Rocket RSI output has no fixed upper/lower limit. The ±0.999 clamp limits the theoretical maximum to about ±3.8, but there are no "overbought/oversold lines" that work universally.
2. **Confusing with Wilder's RSI.** Rocket RSI uses summation-based CU/CD (not exponential decay), inputs are Super-Smoothed momentum (not raw price), and the output passes through arctanh. The only shared concept is "relative strength."
3. **Using in trending markets.** Rocket RSI is optimized for cyclic reversals. In strong trends, it can remain at extreme values for many bars. Fading a trend based on Rocket RSI alone is a common source of losses.
4. **Ignoring the warmup.** The first `smoothLength + rsiLength` bars produce unreliable output as the IIR filter and RSI accumulation window are not yet fully populated.
5. **Over-parameterizing.** Both `smoothLength` and `rsiLength` affect the effective cycle period. Changing both simultaneously makes it difficult to attribute signal changes. Adjust one parameter at a time.

## FAQ

**Q: Why is the output unbounded while RSI is bounded?**
A: The Fisher Transform (arctanh) maps (-1, 1) to (-∞, +∞). This is intentional: it amplifies the distinction between "at the extreme of the RSI range" and "moderately positioned," producing sharper reversal signals. The ±0.999 clamp limits the theoretical maximum to about ±3.8.

**Q: Why use summation-based RSI instead of Wilder's?**
A: Wilder's exponential decay gives disproportionate weight to recent changes, which can mask cyclic turning points. Ehlers' summation approach treats all changes within the window equally, providing a cleaner measure of directional pressure over exactly one cycle period.

**Q: How does the Super Smoother differ from a simple moving average?**
A: The Super Smoother is a 2-pole Butterworth IIR filter with unity DC gain. Unlike an SMA, it has a steep frequency rolloff that effectively removes aliasing artifacts above the Nyquist frequency of the sampled cycle. This prevents high-frequency noise from corrupting the RSI calculation.

**Q: What values indicate a reversal?**
A: Values beyond ±2.0 indicate statistically extreme readings (~5% of a Gaussian distribution). Sharp peaks followed by zero-line crosses provide the highest-confidence reversal signals. The exact threshold depends on the instrument's volatility characteristics.

## References

- Ehlers, John F. "Rocket RSI." *Technical Analysis of Stocks & Commodities*, May 2018.
- Ehlers, John F. *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- Ehlers, John F. "Using The Fisher Transform." *Stocks & Commodities*, November 2002.
- [PineScript reference](rrsi.pine)
