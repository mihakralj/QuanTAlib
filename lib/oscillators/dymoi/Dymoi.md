# DYMOI: Dynamic Momentum Index

> "The market is not a fixed-frequency oscillator. Why would you analyze it with one?" — Tushar Chande & Stanley Kroll, *The New Technical Trader*, 1994

DYMOI is a volatility-adaptive RSI: when recent price swings are large relative to longer-term swings, the RSI period shortens and the indicator becomes more responsive; when price action tightens, the period extends and the output smooths. The result is an oscillator that self-adjusts its sensitivity to the market's current state, avoiding both the lag of long fixed-period RSIs in trending regimes and the noise of short-period RSIs in ranging ones.

## Historical Context

Tushar Chande and Stanley Kroll introduced DYMOI in *The New Technical Trader* (1994) as a practical answer to a genuine problem: the standard RSI's fixed period is a blunt instrument. A 14-bar RSI responds identically whether the market has been oscillating ±5% per day or ±0.2%. Chande and Kroll observed that a shorter period in high-volatility environments catches reversals earlier; a longer period in quiet conditions eliminates whipsaws.

The mechanism they chose was straightforward: compute the ratio of short-term to long-term price standard deviation. When this ratio exceeds 1, the market is more volatile than its recent baseline — shorten the period. When the ratio is below 1, lengthen it. The result gets clamped to a configurable `[minPeriod, maxPeriod]` range, and a standard Wilder RSI runs on the resulting dynamic period.

The indicator has no widely adopted C# open-source implementation, which is why cross-library validation is self-consistency only. The original book uses population standard deviation over rolling windows — this implementation matches that specification.

### Comparison with Related Indicators

| Indicator | Adaptation Mechanism | Output Range | Warmup |
| :--- | :--- | :---: | :---: |
| RSI (Wilder) | None — fixed period | 0–100 | period+1 |
| CRSI (Connors) | Three-component composite, no period adaptation | 0–100 | rankPeriod+rsiPeriod |
| DYMOI (Chande/Kroll) | Dual StdDev ratio drives period selection | 0–100 | longPeriod+maxPeriod |
| LRSI (Ehlers Laguerre) | Cycle-adaptive Laguerre filter stages | 0–1 | 4 |

## Architecture & Physics

### 3.1 Stage 1: Dual Circular-Buffer Standard Deviation

Two O(1) StdDev estimators maintain running sums for windows of `shortPeriod` and `longPeriod` bars respectively. Each bar, the oldest value is evicted and the new value is ingested:

$$\bar{x} = \frac{\sum x_i}{n}, \quad \sigma = \sqrt{\frac{\sum x_i^2}{n} - \bar{x}^2}$$

This form avoids rescanning the window on every bar. Floating-point drift is inherent but bounded — the window size keeps the accumulated error small in practice (typical window sizes 5–30 bars).

### 3.2 Stage 2: Volatility Ratio → Dynamic Period

$$V = \frac{\sigma_{\text{short}}}{\sigma_{\text{long}}}$$

$$n_{\text{dyn}} = \operatorname{clamp}\!\left(\operatorname{round}\!\left(\frac{n_{\text{base}}}{V}\right),\; n_{\text{min}},\; n_{\text{max}}\right)$$

When $V = 0$ (both windows have identical prices, e.g., a flat series), $n_{\text{dyn}}$ defaults to $n_{\text{max}}$ as the safest fallback. When $V \leq 10^{-10}$ (effectively zero), the same clamp applies.

The clamp ensures the RSI period cannot collapse to 1 (which is numerically unstable and meaningless) or expand to absurd lengths. Default bounds [3, 30] match Chande and Kroll's original recommendation.

### 3.3 Stage 3: Wilder RMA RSI with Adaptive Alpha

Per-bar, a new alpha is derived from the current $n_{\text{dyn}}$:

$$\alpha = \frac{1}{n_{\text{dyn}}}, \quad \beta = 1 - \alpha$$

The Wilder smoothing (RMA) of gains and losses then updates:

$$\overline{G}_t = \beta \cdot \overline{G}_{t-1} + \alpha \cdot \max(\Delta p, 0)$$

$$\overline{L}_t = \beta \cdot \overline{L}_{t-1} + \alpha \cdot \max(-\Delta p, 0)$$

$$\text{RSI} = 100 \cdot \frac{\overline{G}}{\overline{G} + \overline{L}}$$

FMA is used in the hot path to reduce rounding error:

```csharp
s.AvgGain = Math.FusedMultiplyAdd(s.AvgGain, beta, alpha * gain);
s.AvgLoss = Math.FusedMultiplyAdd(s.AvgLoss, beta, alpha * loss);
```

### 3.4 Warmup Compensation

A warmup compensator tracks the accumulated decay $e_t = \beta^t$ and scales the raw RMA values to produce valid output from bar 1:

$$\hat{G}_t = \frac{\overline{G}_t}{1 - e_t}, \quad \hat{L}_t = \frac{\overline{L}_t}{1 - e_t}$$

Once $e_t \leq 10^{-10}$, the compensator deactivates and standard Wilder smoothing proceeds. This is the same design used throughout QuanTAlib's RSI-based oscillators (CRSI, QQE, DOSC).

### 3.5 Bar Correction (isNew Rollback)

The streaming `Update(TValue, bool isNew)` contract requires:

- `isNew = true`: snapshot state and both circular buffers, then advance.
- `isNew = false`: restore state and buffers from snapshot, recompute with new value.

Since `RingBuffer` instances are heap objects that cannot be rolled back via struct copy alone, explicit `Array.Copy` snapshots (`_shortBufSnap`, `_longBufSnap`) are maintained alongside the `State` record struct.

## Mathematical Foundation

### Full Derivation

Given close prices $c_1, c_2, \ldots, c_t$, let windows be $W_s$ of size $n_s$ and $W_l$ of size $n_l$, with $n_s < n_l$:

**Population variance (O(1) form):**

$$\sigma^2 = \frac{\sum_{i \in W} c_i^2}{|W|} - \left(\frac{\sum_{i \in W} c_i}{|W|}\right)^2$$

**Volatility ratio:**

$$V_t = \begin{cases} \sigma_s / \sigma_l & \text{if } \sigma_l > 10^{-10} \\ 1 & \text{otherwise} \end{cases}$$

**Dynamic period:**

$$n_t = \operatorname{clamp}\!\left(\left\lfloor \frac{n_{\text{base}}}{V_t} + 0.5 \right\rfloor,\; n_{\min},\; n_{\max}\right)$$

**Wilder RSI at bar $t$ with adaptive alpha $\alpha_t = 1 / n_t$:**

$$\overline{G}_t = \alpha_t \cdot G_t + (1 - \alpha_t) \cdot \overline{G}_{t-1}$$

$$\text{DYMOI}_t = 100 \cdot \frac{\overline{G}_t}{\overline{G}_t + \overline{L}_t}$$

### Degenerate Cases

| Condition | $V$ | $n_{\text{dyn}}$ | Effect |
| :--- | :---: | :---: | :--- |
| $\sigma_l = 0$ (constant prices) | — | $n_{\max}$ | Maximally smooth; RSI→50 |
| $\sigma_s \gg \sigma_l$ ($V \gg 1$) | large | $n_{\min}$ | Fastest possible RSI |
| $\sigma_s \ll \sigma_l$ ($V \ll 1$) | small | $n_{\max}$ | Slowest possible RSI |
| $n_{\min} = n_{\max} = n_{\text{base}}$ | any | $n_{\text{base}}$ | Identical to RSI($n_{\text{base}}$) |

## Performance Profile


### Operation Count (Streaming Mode)

DYMOI computes a dynamic momentum oscillator using an EMA-smoothed velocity + acceleration blend.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA × 2 (fast/slow EMA updates) | 2 | 4 | 8 |
| SUB (velocity = fast − slow EMA) | 1 | 1 | 1 |
| FMA (acceleration = EMA of velocity) | 1 | 4 | 4 |
| FMA (blend velocity + acceleration) | 1 | 4 | 4 |
| **Total** | **5** | — | **~17 cycles** |

Three EMA instances. ~17 cycles per bar at steady state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| All EMA passes × 3 | **No** | Recursive IIR — sequential |
| Subtraction + blend | Yes | VSUBPD + VFMADD after EMA arrays known |

Operations per bar (streaming `Update`):

| Operation | Count |
| :--- | ---: |
| Short StdDev O(1) update (evict + insert + recompute mean/var) | 6 |
| Long StdDev O(1) update | 6 |
| Division (vol ratio) | 1 |
| Round + clamp | 3 |
| FMA ×2 (gain/loss Wilder) | 2 |
| RSI formula | 3 |
| Array.Copy (isNew snapshots, amortized) | ~2n/bar |
| **Total arithmetic** | **~23 + 2n copy** |

SIMD is not applicable to the streaming `Update` path because the period changes per bar, breaking vectorization. The static `Batch(Span)` path processes the entire series in a single loop with O(1) arithmetic per bar; AVX2 vectorization of the StdDev summation is structurally possible but not implemented, as the gains are marginal for typical window sizes (5–30).

**Complexity:** O(1) per bar for `Update`; O(n) total for `Batch`.

**Memory:** O(shortPeriod + longPeriod) for buffers; O(1) state beyond that.

**Quality metrics (1–10):**

| Attribute | Score | Note |
| :--- | :---: | :--- |
| Adaptiveness | 9 | Period covers minPeriod–maxPeriod range continuously |
| Smoothness | 7 | Wilder smoothing inherits lag characteristics |
| Responsiveness | 8 | Shortens on volatility spikes |
| Noise rejection | 7 | Clamp prevents degenerate periods |
| Interpretability | 8 | [0,100] RSI scale is familiar |

## Validation

No external C# library (Skender, TA-Lib, Tulip, Ooples) implements DYMOI. Validation is self-consistency only.

| Test | Method | Tolerance | Result |
| :--- | :--- | :---: | :--- |
| Streaming == Batch (TSeries) | GBM 300 bars | 1e-10 | Pass |
| Streaming == Batch (Span) | GBM 300 bars | 1e-10 | Pass |
| Streaming == Eventing | GBM 200 bars | 1e-10 | Pass |
| Output ∈ [0,100] | GBM 500 bars, σ=0.5 | — | Pass |
| Constant price → RSI=50 | 100 bars @ 100.0 | 1e-6 | Pass |
| Fixed period identity | minPeriod=maxPeriod=basePeriod | 1e-9 | Pass |
| Determinism | Two identical GBM seeds | 1e-10 | Pass |

**Mathematical identity test:** When `minPeriod == maxPeriod == basePeriod`, the dynamic period is always fixed at `basePeriod` regardless of the volatility ratio. Under this constraint, DYMOI produces output numerically identical to `Rsi(basePeriod)` (verified at tolerance 1e-9).

## Common Pitfalls

1. **`longPeriod <= shortPeriod`**: The constructor throws `ArgumentException` if this constraint is violated. The volatility ratio is undefined when both windows cover the same bars.

2. **Zero-variance series (flat price)**: When `σ_long = 0`, the ratio is undefined; the implementation defaults to `V = 1` → `n_dyn = n_base`. This is correct — a flat series should produce neutral RSI(=50) at the base period rate, not a degenerate output.

3. **Warmup period misinterpretation**: `WarmupPeriod = longPeriod + maxPeriod`. The dominant warmup is the Wilder RMA, which takes `maxPeriod` bars to settle after the long StdDev window fills. Using DYMOI output before `IsHot = true` will produce compensated but less accurate values.

4. **Period clamp masking pathology**: If `minPeriod` and `maxPeriod` are very close (e.g., both 14), the adaptive behavior is effectively disabled and DYMOI degenerates to standard RSI. This is a valid use case but should be intentional.

5. **Floating-point drift in running sums**: The O(1) variance formula $E[x^2] - E[x]^2$ is numerically unstable for large values or large windows — specifically, catastrophic cancellation can occur. For price data in the range [0.01, 100000] and periods ≤ 100, drift is negligible in practice. For exotic inputs, a periodic full-recalculation reset (every N steps) would be appropriate; the current implementation does not perform this.

6. **Assumption of IID returns**: The period-selection formula $n_{\text{dyn}} = n_{\text{base}} / V$ implicitly assumes that the volatility ratio directly translates to an appropriate lookback scaling. This holds approximately for Gaussian returns but can under- or over-shoot in heavy-tailed regimes where short spikes inflate $V$ transiently.

7. **`Array.Copy` cost on rollback**: Each `isNew = false` call copies two arrays of size `shortPeriod` and `longPeriod`. For default periods (5+10=15 doubles = 120 bytes), this is negligible. For periods > 256, the copy still occurs on heap memory and remains fast relative to any downstream computation.

## References

- Chande, T. & Kroll, S. (1994). *The New Technical Trader*. John Wiley & Sons. Ch. 3: Dynamic Momentum Index.
- Wilder, J.W. (1978). *New Concepts in Technical Trading Systems*. Trend Research. (RSI original source)
- Connors, L. & Alvarez, C. (2012). *An Introduction to ConnorsRSI*. TradingMarkets. (CRSI comparison reference)
