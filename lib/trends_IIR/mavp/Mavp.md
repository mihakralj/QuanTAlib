# MAVP: Moving Average Variable Period

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `minPeriod` (default 2), `maxPeriod` (default 30)                      |
| **Outputs**      | Single series (Mavp)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `maxPeriod` bars                          |
| **PineScript**   | [mavp.pine](mavp.pine)                       |

- MAVP applies an EMA-style exponential smoothing where the period -- and therefore the smoothing constant alpha -- changes on every bar.
- Parameterized by `minperiod` (default 2), `maxperiod` (default 30).
- Output range: Tracks input.
- Requires `maxPeriod` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "You can't fix your moving average period because the market doesn't run at a fixed frequency. MAVP stops pretending it does."

## Introduction

MAVP applies an EMA-style exponential smoothing where the period -- and therefore the smoothing constant alpha -- changes on every bar. Each bar receives an externally supplied period value, clamped to [minPeriod, maxPeriod], producing `alpha = 2 / (period + 1)`. The result is a single-pass O(1) IIR filter with an adaptive warmup compensator that tracks the cumulative product of all per-bar `(1 - alpha)` values. With a fixed period MAVP reduces exactly to standard EMA (validated to 1e-9 tolerance against Skender and TA-Lib EMA). With a time-varying period series, it becomes a general-purpose adaptive smoother controlled entirely by external logic.

## Historical Context

TA-Lib introduced `MAVP` (Moving Average Variable Period) as a meta-indicator: feed it a price series and a period series, and it routes each bar through the selected MA type with that bar's period. The original C implementation supports SMA, EMA, WMA, DEMA, TEMA, TRIMA, KAMA, MAMA, and T3 as backends. Most implementations default to SMA, which requires a sliding window that changes size every bar -- an allocation headache and O(n) per bar.

This implementation takes a different approach. Rather than wrapping arbitrary MA types, it uses a pure EMA core with per-bar alpha adaptation. The advantage: O(1) time, O(1) space, zero allocation, no buffer. The warmup compensator `E = product(1 - alpha_i)` generalizes the standard EMA bias correction `E = (1 - alpha)^n` to handle the non-stationary case where alpha varies.

The trade-off is explicit: you get EMA-type smoothing only. If you need variable-period SMA or WMA, use a windowed approach. But for adaptive trend following where lag minimization matters, EMA with variable alpha is the right primitive.

## Architecture and Physics

### 1. Per-Bar Alpha Computation

The period input is first clamped, then converted to an EMA smoothing constant:

$$p_i = \text{clamp}(\text{period}_i, p_{\min}, p_{\max})$$

$$\alpha_i = \frac{2}{p_i + 1}$$

$$\beta_i = 1 - \alpha_i$$

### 2. EMA Core (IIR First-Order)

The core recursion is identical to standard EMA, but with time-varying alpha:

$$\text{ema}_i = \beta_i \cdot \text{ema}_{i-1} + \alpha_i \cdot x_i$$

Implemented via FMA for numerical precision:

```csharp
ema = Math.FusedMultiplyAdd(ema, beta, alpha * input);
```

### 3. Adaptive Warmup Compensator

Standard EMA bias correction divides by `(1 - (1-alpha)^n)`. With variable alpha, the compensator tracks the cumulative product of all betas:

$$E_i = \prod_{k=0}^{i} \beta_k$$

$$\text{result}_i = \frac{\text{ema}_i}{1 - E_i}$$

When `E <= 1e-10`, compensation is complete and the raw EMA is used directly. IsHot fires when `E <= 0.05` (approximately 95% of steady-state weight accumulated).

### 4. Z-Domain Transfer Function

For a single bar with alpha_i, the transfer function is the standard first-order IIR:

$$H_i(z) = \frac{\alpha_i}{1 - \beta_i z^{-1}}$$

The time-varying system is a sequence of such filters cascaded with changing coefficients. This is a Linear Time-Varying (LTV) system -- not LTI -- so standard frequency-domain analysis does not directly apply. Stability is guaranteed because each individual filter has its pole at `beta_i` which lies in `(0, 1)` for any valid alpha.

## Mathematical Foundation

### EMA Recursion (Variable Alpha)

Given input series $x_0, x_1, \ldots, x_n$ and period series $p_0, p_1, \ldots, p_n$:

$$\alpha_i = \frac{2}{\text{clamp}(p_i, p_{\min}, p_{\max}) + 1}$$

$$\text{ema}_0 = \alpha_0 \cdot x_0$$

$$\text{ema}_i = (1 - \alpha_i) \cdot \text{ema}_{i-1} + \alpha_i \cdot x_i$$

### Bias Correction

$$E_0 = 1 - \alpha_0$$

$$E_i = E_{i-1} \cdot (1 - \alpha_i)$$

$$\text{corrected}_i = \frac{\text{ema}_i}{1 - E_i}$$

### Parameter Mapping

| Parameter | Default | Range | Effect |
|-----------|---------|-------|--------|
| minPeriod | 2 | >= 1 | Fastest response (alpha_max = 2/3) |
| maxPeriod | 30 | >= minPeriod | Slowest response (alpha_min = 2/31) |
| period | per-bar | [minPeriod, maxPeriod] | Smoothing speed for each bar |

### Fixed-Period Equivalence

When `period_i = N` for all `i`, MAVP reduces to standard EMA(N):

$$\alpha = \frac{2}{N+1}, \quad E_i = (1-\alpha)^{i+1}$$

This identity is verified to 1e-9 in validation tests.

## Performance Profile

### Operation Count (Scalar, Per Bar)

| Operation | Count | Cycles (est.) |
|-----------|-------|---------------|
| ADD/SUB | 2 | 2 |
| MUL | 2 | 6 |
| FMA | 1 | 4 |
| DIV | 0-1 | 0-15 |
| CMP | 3 | 3 |
| Total | ~8-9 | ~15-30 |

Division only occurs during warmup (bias correction). Post-warmup: 0 divisions.

### Complexity

| Metric | Value |
|--------|-------|
| Time (Update) | O(1) |
| Space | O(1) -- no buffer |
| Allocations | Zero in hot path |
| SIMD potential | Limited (serial dependency) |

### Quality Metrics

| Metric | Score | Notes |
|--------|-------|-------|
| Accuracy | 8/10 | Matches EMA exactly at fixed period |
| Timeliness | 9/10 | Can track fast alpha changes instantly |
| Overshoot | 7/10 | EMA-inherent; varies with period |
| Smoothness | 7/10 | Depends on period stability |

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Skender (EMA) | Pass | Fixed-period MAVP == EMA, tolerance 1e-9 |
| TA-Lib (EMA) | Pass | Fixed-period MAVP == EMA, tolerance 1e-9 |
| Tulip | N/A | No MAVP equivalent |
| Ooples | N/A | No MAVP equivalent |

TA-Lib's native `MAVP` function uses SMA by default (MAType=0), not EMA. Direct comparison requires MAType=1 (EMA mode), which is validated indirectly through the EMA equivalence proof.

## Common Pitfalls

1. **Assuming MAVP == TA-Lib MAVP**: TA-Lib defaults to SMA-based MAVP; this implementation uses EMA. With `MAType=1` in TA-Lib, results match. Mixing up MA types causes 100% of "validation failure" reports.

2. **Unstable period series**: Rapidly oscillating periods (e.g., period = [2, 30, 2, 30, ...]) create a filter that alternates between very responsive and very sluggish. The output will exhibit ringing. Smooth the period series first if the source is noisy.

3. **Warmup underestimation**: WarmupPeriod is set to maxPeriod, but actual convergence depends on the period sequence. If all periods are maxPeriod, warmup takes ~150 bars. If all periods are minPeriod=2, warmup happens in ~5 bars.

4. **Period clamping ignored**: Periods outside [minPeriod, maxPeriod] are silently clamped. If your external period source produces values like 0.5 or 1000, the effective period will differ from what you expect. Add logging or asserts in your pipeline.

5. **Bar correction with period changes**: When correcting a bar (isNew=false), the period used must be the same period as the original bar. If you change the Period property between the original update and the correction, the rollback restores state but applies a different alpha, producing incorrect results.

6. **Memory of the Period property**: The Period property persists between Update calls. If you set Period=5 for one bar and then call Update without setting Period again, the next bar also uses Period=5. This is by design but can surprise users who expect period to reset.

## References

- Kaufman, P. J. (2013). *Trading Systems and Methods*, 5th Ed. Wiley. Discusses adaptive moving averages.
- TA-Lib documentation: [MAVP - Moving Average with Variable Period](https://ta-lib.org/function.html)
- Ehlers, J. F. (2001). *Rocket Science for Traders*. Wiley. Adaptive smoothing with variable alpha.
