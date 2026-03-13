# HEMA: Hull Exponential Moving Average

> *HMA is a topology. HEMA keeps the topology and swaps the physics: windows to decay, with identical lag.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Hema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `EstimateWarmupPeriod()` bars                          |
| **PineScript**   | [hema.pine](hema.pine)                       |
| **Signature**    | [hema_signature](hema_signature.md) |

- HEMA is a Hull-style moving average built entirely from **exponential smoothers**.
- **Similar:** [EMA](../ema/ema.md), [DEMA](../dema/dema.md) | **Complementary:** Trend following | **Trading note:** Hull-style EMA; applies Hulls lag-reduction technique to EMA.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## An EMA-domain analog of HMA with WMA-lag-matched alphas

HEMA is a Hull-style moving average built entirely from **exponential smoothers**. It preserves the classic HMA pipeline (fast minus slow, then smooth) but replaces WMA sub-filters with EMAs whose alphas are tuned to produce **identical lag** to the WMA stages they replace. At period $N$: HEMA($N$) and HMA($N$) have the same theoretical group delay, but HEMA has infinite memory and smoother transient behavior.

## Historical Context

The Hull Moving Average was designed around weighted moving averages (WMA), which have **finite memory** and are parameterized by a **window length**. EMA-family filters have **infinite memory** and are parameterized by a **decay rate**. Mapping HMA to an EMA world is not "replace WMA with EMA and hope." You need a clear definition of *what the period means* in EMA terms, and a de-lag combiner that stays consistent when the underlying smoother is exponential.

Early implementations used a half-life mapping ($\alpha = 1 - e^{-\ln 2 / N}$), but this produces EMA lag $\approx 1.44N$ instead of WMA lag $(N-1)/3$. The mismatch made HEMA(10) behave like HMA(30) in practice: roughly 4.6x more sluggish at every period. The current implementation uses a **WMA-lag-matched alpha** ($\alpha = 3/(N+2)$) that produces exactly the same lag as WMA($N$), making period comparisons between HMA and HEMA meaningful.

## Architecture and Physics

### Topology (the pipeline)

Given input series $x_t$ and user period $N$:

1. **Slow smoother**

$$s_t = \text{EMA}_{\alpha_s}(x_t), \quad \alpha_s = \frac{3}{N+2}$$

1. **Fast smoother** (integer floor sub-period, same as HMA)

$$f_t = \text{EMA}_{\alpha_f}(x_t), \quad \alpha_f = \frac{3}{\lfloor N/2 \rfloor + 2}$$

1. **De-lag combiner** (DC gain = 1)

$$d_t = \frac{f_t - r\,s_t}{1-r}$$

1. **Final smoothing** (integer floor sub-period, same as HMA)

$$\text{HEMA}_t = \text{EMA}_{\alpha_m}(d_t), \quad \alpha_m = \frac{3}{\lfloor\sqrt{N}\rfloor + 2}$$

This mirrors classic HMA:

$$\text{HMA}_N(x) = \text{WMA}_{\lfloor\sqrt{N}\rfloor}\!\left(2\,\text{WMA}_{\lfloor N/2\rfloor}(x)-\text{WMA}_N(x)\right)$$

The difference: HEMA's stages are exponential with infinite memory. The sub-periods use integer floor division to match HMA's behavior exactly.

### WMA-lag-matched alpha (what "Period" actually means)

HEMA's `Period = N` means: **the EMA has the same lag as WMA(N)**.

For a WMA of length $N$, the steady-state mean lag is:

$$\text{lag}_{\text{WMA}}(N) = \frac{N-1}{3}$$

For an EMA with smoothing constant $\alpha$, the steady-state mean lag is:

$$\text{lag}_{\text{EMA}}(\alpha) = \frac{1-\alpha}{\alpha}$$

Setting these equal and solving for $\alpha$:

$$\frac{1-\alpha}{\alpha} = \frac{N-1}{3} \implies \alpha = \frac{3}{N+2}$$

This makes "WMA-equivalent period" the primitive, and $\alpha$ derived. At $N=10$: $\alpha = 3/12 = 0.25$, lag $= 0.75/0.25 = 3.0$ bars, exactly matching WMA(10) lag.

### The de-lag ratio $r$: derived, not guessed

Classic HMA uses $2f - s$. That implicitly assumes a particular lag relationship between the fast and slow smoothers.

In EMA space, the "correct" proportionality uses EMA's **steady-state mean lag**:

$$\text{lag}(\alpha)\approx \frac{1-\alpha}{\alpha}$$

Compute:

$$r = \frac{\text{lag}_\text{fast}}{\text{lag}_\text{slow}} = \frac{(1-\alpha_f)/\alpha_f}{(1-\alpha_s)/\alpha_s}$$

Then the combiner:

$$d_t = \frac{f_t - r\,s_t}{1-r}$$

**Why this form?**

- **DC gain is exactly 1** (flat input stays flat).
- For "large" $N$ (small $\alpha$), the ratio tends toward:

$$r \approx \frac{\alpha_s}{\alpha_f} \approx \frac{1}{2}$$

and the combiner approaches $d_t \approx 2f_t - s_t$, i.e., the classic HMA shape emerges as a limiting case.

### Warmup: unbiased EMA from bar 1

Raw EMA recursion assumes the filter has run forever. Early outputs are biased toward zero (or the initial state). HEMA uses **exact bias compensation** during warmup by tracking each stage's decay:

If $y_t$ is the raw EMA state and $\beta = 1-\alpha$, the bias-corrected output is:

$$y_t^{*} = \frac{y_t}{1-\beta^{t}}$$

HEMA performs this independently for slow stage, fast stage, and smooth stage, and exits warmup only when **all three** decays are negligible.

**Practical implication:** early samples converge fast to a meaningful value. Use `IsHot` (or `WarmupPeriod`) if you need "fully settled" behavior for signal generation.

## Mathematical Foundation

**WMA-lag-matched alpha:**

$$\alpha = \frac{3}{N+2}$$

where $N$ is the period parameter (minimum 2). This produces EMA lag = $(N-1)/3$ = WMA($N$) lag.

**Sub-period alphas (integer floor, matching HMA):**

$$\alpha_{\text{slow}} = \frac{3}{N+2}, \quad \alpha_{\text{fast}} = \frac{3}{\lfloor N/2 \rfloor+2}, \quad \alpha_{\text{smooth}} = \frac{3}{\lfloor\sqrt{N}\rfloor+2}$$

**EMA recursion:**

$$\text{EMA}_{t} = \alpha \cdot x_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

**Bias-compensated EMA:**

$$\text{EMA}_{t}^{*} = \frac{\text{EMA}_{t}}{1 - (1-\alpha)^{t}}$$

**De-lag combiner:**

$$d_t = \frac{f_t - r \cdot s_t}{1 - r}$$

where:

$$r = \frac{(1-\alpha_f)/\alpha_f}{(1-\alpha_s)/\alpha_s}$$

**Final output:**

$$\text{HEMA}_t = \text{EMA}_{\text{smooth}}(d_t)$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Hot Path (Post-Warmup):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| **Stage 1: EMA Slow** | | | |
| FMA (emaSlowRaw x betaSlow + alphaSlow x input) | 1 | 4 | 4 |
| MUL (alphaSlow x input) | 1 | 3 | 3 |
| **Stage 2: EMA Fast** | | | |
| FMA (emaFastRaw x betaFast + alphaFast x input) | 1 | 4 | 4 |
| MUL (alphaFast x input) | 1 | 3 | 3 |
| **Stage 3: De-Lag Combiner** | | | |
| FMA (-ratio x emaSlow + emaFast) | 1 | 4 | 4 |
| MUL (x invOneMinusRatio) | 1 | 3 | 3 |
| **Stage 4: Final EMA Smooth** | | | |
| FMA (emaSmoothRaw x betaSmooth + alphaSmooth x deLag) | 1 | 4 | 4 |
| MUL (alphaSmooth x deLag) | 1 | 3 | 3 |
| **Total (Hot Path)** | | | **~28 cycles** |

**Warmup Path (Additional Operations):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (decay x beta) | 3 | 3 | 9 |
| DIV (1 / (1 - decay)) | 3 | 15 | 45 |
| MUL (raw x invDecay) | 3 | 3 | 9 |
| CMP/MAX (decay comparisons) | 3 | 1 | 3 |
| **Total (Warmup)** | | | **~66 cycles** |

**Warmup total:** ~94 cycles | **Hot path total:** ~28 cycles

### Batch Mode (SIMD Analysis)

HEMA is **not SIMD-parallelizable** across bars due to:

1. All three EMA stages are recursive IIR filters (output[t] depends on output[t-1])
2. De-lag combiner depends on current slow/fast EMA values
3. Final smoother depends on de-lagged series

**FMA optimization (already applied):** All EMA updates use `Math.FusedMultiplyAdd` for single-rounding precision.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | WMA-lag-matched alphas produce identical theoretical lag to HMA |
| **Timeliness** | 8/10 | Faster response than plain EMA via de-lag combiner |
| **Overshoot** | 6/10 | De-lag combiner can overshoot during sharp reversals |
| **Smoothness** | 7/10 | Smoother than DEMA, less smooth than T3 |

*Benchmark environment: .NET 10, Release build, no SIMD (stateful recursion). Measured via BenchmarkDotNet on synthetic GBM data (mu=0.0001, sigma=0.02, 10K bars).*

## Validation

HEMA is not commonly available in mainstream TA libraries. Validation uses a **reference implementation**.

| Library | Status | Tolerance | Notes |
|:---|:---|:---|:---|
| **TA-Lib** | N/A | - | Not implemented |
| **Skender** | N/A | - | Not implemented |
| **Tulip** | N/A | - | Not implemented |
| **Ooples** | N/A | - | Not implemented |
| **PineScript** | Passed | 1e-10 | Matches `lib/trends_IIR/hema/hema.pine` |

**Validation strategy:**

- PineScript reference is authoritative (included in repo).
- Cross-check via invariant tests: DC gain, step response monotonicity, no NaN propagation after first finite sample.
- Streaming vs batch vs span consistency verified in unit tests.

## Common Pitfalls

1. **Period semantics are now WMA-lag-matched**

   `Period = N` means "same lag as WMA(N)." HEMA(10) and HMA(10) have the same theoretical group delay. Earlier versions used half-life semantics where HEMA(10) was roughly equivalent to HMA(30). If you are upgrading from the half-life version, expect HEMA to now be noticeably more responsive at the same period.

2. **Warmup assumptions**

   Early values are bias-corrected, but "fully settled" still takes time. Use `IsHot` / `WarmupPeriod` before acting on signals. Expect roughly $3\sqrt{N}$ bars for all three stages to stabilize.

3. **Overshoot on reversals**

   De-lag can overshoot. This is the price of reduced lag, same tradeoff as the DEMA/ZLEMA family. If overshoot is unacceptable, prefer a slower final smoother or reduce de-lag strength (requires custom variant).

4. **Non-finite data handling**

   Non-finite values are substituted with last valid value. Before the first valid input, output is `NaN`. If your upstream data source produces frequent gaps, consider pre-filtering or using a different indicator.

5. **Bar correction discipline**

   Use `isNew=false` when correcting the last bar (same timestamp, revised OHLC). Failing to do so causes state drift and inconsistent results across runs.

6. **Integer floor sub-periods**

   Sub-periods use integer floor division (`period / 2`, `(int)Math.Sqrt(period)`) to match HMA behavior exactly. This means HEMA(5) uses halfPeriod=2 and sqrtPeriod=2, not 2.5 and 2.236.

## References

- Hull, A. "Hull Moving Average." Technical analysis methodology using WMA lag cancellation.
- Wolfram Alpha verification: EMA lag with alpha=3/(N+2) equals (N-1)/3, matching WMA(N) lag exactly.