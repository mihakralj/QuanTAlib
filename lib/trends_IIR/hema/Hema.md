# HEMA: Hull Exponential Moving Average

## An EMA-domain analog of HMA using half-life semantics

> "HMA is a topology. HEMA keeps the topology and swaps the physics: windows → decay."

HEMA is a Hull-style moving average built entirely from **exponential smoothers**. It preserves the classic HMA pipeline—**fast minus slow, then smooth**—but defines timing in **half-life** (exponential decay) rather than finite window length. The result is a **lag-reduced trend line** with consistent behavior across instruments and sampling rates (when you think in "how fast memory fades," not "how wide the window is").

## Historical Context

The Hull Moving Average was designed around weighted moving averages (WMA), which have **finite memory** and are parameterized by a **window length**. EMA-family filters have **infinite memory** and are parameterized by a **decay rate**. Mapping HMA to an EMA world is not "replace WMA with EMA and hope"—you need a clear definition of *what the period means* (HEMA uses **half-life**), and a de-lag combiner that stays consistent when the underlying smoother is exponential.

HEMA is exactly that: **HMA topology, EMA half-life semantics**.

## Architecture & Physics

### Topology (the pipeline)

Given input series $x_t$ and user period $N$ (interpreted as **half-life in bars**):

1. **Slow smoother**

$$s_t = \text{EMA}_{\text{hl}=N}(x_t)$$

2. **Fast smoother**

$$f_t = \text{EMA}_{\text{hl}=N/2}(x_t)$$

3. **De-lag combiner** (DC gain = 1)

$$d_t = \frac{f_t - r\,s_t}{1-r}$$

4. **Final smoothing**

$$\text{HEMA}_t = \text{EMA}_{\text{hl}=\sqrt{N}}(d_t)$$

This mirrors classic HMA:

$$\text{HMA}_N(x) = \text{WMA}_{\sqrt{N}}\left(2\,\text{WMA}_{N/2}(x)-\text{WMA}_N(x)\right)$$

The difference: HEMA's stages are exponential and its timing is defined by half-life.

### Half-life semantics (what "Period" actually means)

HEMA's `Period = N` is **not** a window length.

- Half-life $N$ means: after $N$ bars, the contribution of a past sample decays to **50%** (relative to the next bar's contribution), in the exponential weighting sense.
- This is often a more intuitive and stable control knob than "window length," especially across different bar sizes.

**Half-life → EMA alpha:**

For an EMA written as:

$$y_t = y_{t-1} + \alpha(x_t - y_{t-1})$$

half-life mapping is:

$$\alpha = 1 - e^{-\ln(2)/\text{hl}}$$

This makes "half-life" the primitive, and $\alpha$ derived.

**Numerical note:** for large $\text{hl}$, use `-Math.Expm1(-ln2/hl)` instead of `1-Math.Exp(-ln2/hl)` to avoid catastrophic cancellation.

### The de-lag ratio $r$: derived, not guessed

Classic HMA uses $2f - s$. That implicitly assumes a particular lag relationship between the fast and slow smoothers.

In EMA half-life space, the "correct" proportionality is best expressed using an EMA's **steady-state mean lag** approximation:

$$\text{lag}(\alpha)\approx \frac{1-\alpha}{\alpha}$$

Compute:

$$r = \frac{\text{lag}_\text{fast}}{\text{lag}_\text{slow}} = \frac{(1-\alpha_f)/\alpha_f}{(1-\alpha_s)/\alpha_s}$$

Then the combiner:

$$d_t = \frac{f_t - r\,s_t}{1-r}$$

**Why this form?**

- **DC gain is exactly 1** (flat input stays flat).
- For "large" $N$ (small $\alpha$), the ratio tends toward:

$$r \approx \frac{\alpha_s}{\alpha_f} \approx \frac{1}{2}$$

and the combiner approaches:

$$d_t \approx 2f_t - s_t$$

i.e., the classic HMA shape emerges as a limiting case.

### Warmup: unbiased EMA from bar 1

Raw EMA recursion assumes the filter has run forever. Early outputs are biased toward zero (or the initial state). HEMA uses **exact bias compensation** during warmup by tracking each stage's decay:

If $y_t$ is the raw EMA state and $\beta = 1-\alpha$, the bias-corrected output is:

$$y_t^{*} = \frac{y_t}{1-\beta^{t}}$$

HEMA performs this independently for slow stage, fast stage, and smooth stage, and exits warmup only when **all three** decays are negligible.

**Practical implication:** early samples converge *fast* to a meaningful value. Use `IsHot` (or `WarmupPeriod`) if you need "fully settled" behavior for signal generation.

## Math Foundation

**Half-life to alpha conversion:**

$$\alpha = 1 - e^{-\ln(2) / \text{halfLife}}$$

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

| Metric | Score | Notes |
|:---|:---|:---|
| **Throughput** | ~15 ns/bar | Intel i7-9700K @3.6GHz, AVX2 disabled (scalar only) |
| **Allocations** | 0 | Streaming update is allocation-free |
| **Complexity** | O(1) | Constant work per update |
| **Accuracy** | 8/10 | Matches PineScript reference implementation |
| **Timeliness** | 8/10 | Faster response than plain EMA, smoother than raw price |
| **Overshoot** | 6/10 | De-lag combiner can overshoot during sharp reversals |
| **Smoothness** | 7/10 | Smoother than DEMA, less smooth than T3 |

*Benchmark environment: .NET 10, Release build, no SIMD (stateful recursion). Measured via BenchmarkDotNet on synthetic GBM data (μ=0.0001, σ=0.02, 10K bars).*

## Validation

HEMA is not commonly available in mainstream TA libraries. Validation uses a **reference implementation**.

| Library | Status | Tolerance | Notes |
|:---|:---|:---|:---|
| **TA-Lib** | N/A | — | Not implemented |
| **Skender** | N/A | — | Not implemented |
| **Tulip** | N/A | — | Not implemented |
| **Ooples** | N/A | — | Not implemented |
| **PineScript** | ✅ Passed | 1e-10 | Matches `lib/trends_IIR/hema/hema.pine` |

**Validation strategy:**

- PineScript reference is authoritative (included in repo).
- Cross-check via invariant tests: DC gain, step response monotonicity, no NaN propagation after first finite sample.
- Streaming vs batch vs span consistency verified in unit tests.

## Common Pitfalls

1. **Period semantics mismatch**

   `Period` is half-life (decay), not window length (finite history). Comparing "period=20" between HMA and HEMA is not apples-to-apples. HEMA's 20-bar half-life corresponds to roughly 28–30 bars of HMA window length in steady-state lag, but the transient behavior differs.

2. **Warmup assumptions**

   Early values are bias-corrected, but "fully settled" still takes time. Use `IsHot` / `WarmupPeriod` before acting on signals. Expect roughly $3\sqrt{N}$ bars for all three stages to stabilize.

3. **Overshoot on reversals**

   De-lag can overshoot. This is the price of reduced lag—same tradeoff as DEMA/ZLEMA family. If overshoot is unacceptable, prefer a slower final smoother or reduce de-lag strength (requires custom variant).

4. **Non-finite data handling**

   Non-finite values are substituted with last valid value. Before the first valid input, output is `NaN`. If your upstream data source produces frequent gaps, consider pre-filtering or using a different indicator.

5. **Bar correction discipline**

   Use `isNew=false` when correcting the last bar (same timestamp, revised OHLC). Failing to do so causes state drift and inconsistent results across runs.

## Implementation Notes

- Uses `Math.FusedMultiplyAdd` for tighter numerics and throughput in EMA recursions.
- Warmup compensation uses per-stage decay tracking (`Math.Pow(1-alpha, t)`) to produce unbiased EMAs from bar 1.
- Constructor validates `period > 0` and throws `ArgumentException(nameof(period))` for invalid input (MA0001-compliant).
- Internal state uses `private record struct State` for rollback support (`isNew=false`).
- `GetFiniteValue` helper ensures NaN/Infinity never contaminate state.

**C# snippet (FMA pattern):**

```csharp
// EMA update: ema = ema + alpha * (input - ema)
// Rewritten as FMA: ema = ema * (1-alpha) + alpha * input
_stateSlow.Ema = Math.FusedMultiplyAdd(_stateSlow.Ema, _decaySlow, _alphaSlow * input);
```

Consider using `-Math.Expm1(-Ln2/hl)` in `AlphaFromHalfLife()` for accuracy at large periods (avoids catastrophic cancellation in `1 - Exp(x)` when `x` is near zero).
