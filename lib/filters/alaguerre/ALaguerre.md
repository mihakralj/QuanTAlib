# ALAGUERRE: Adaptive Laguerre Filter

> "The best filter is one that knows when to listen closely and when to smooth aggressively." -- John F. Ehlers (paraphrased)

## Introduction

The Adaptive Laguerre Filter extends Ehlers' four-element all-pass cascade by replacing the fixed damping factor with a per-bar adaptive alpha derived from tracking-error normalization. When price diverges from the filter output (trending conditions), alpha increases toward 1 for faster tracking. When price stays near the filter output (ranging conditions), alpha decreases toward 0 for heavier smoothing. The adaptation mechanism uses a highest/lowest normalization of the absolute tracking error over a lookback window, followed by median smoothing to prevent whipsaw in the coefficient.

## Historical Context

Ehlers introduced the fixed-gamma Laguerre Filter in *Cybernetic Analysis for Stocks and Futures* (Wiley, 2004). The adaptive variant appeared in subsequent work and was popularized through WiseStockTrader's Amibroker AFL implementations. The core insight: rather than requiring the user to tune gamma for each instrument and timeframe, let the filter self-adjust based on how well it tracks the input signal.

Conventional adaptive filters (KAMA, FRAMA, VIDYA) adjust a single EMA coefficient. The Adaptive Laguerre applies the same principle to the four-element all-pass cascade, gaining the frequency-dependent delay properties of Laguerre filtering while simultaneously adapting to market conditions.

The key difference from the "Laguerre RSI" adaptive approach (which uses CU/CD ratios from L-element differences) is that this implementation measures the direct tracking error between price and filter output. This produces a more responsive adaptation that tracks the actual filter performance rather than internal state ratios.

## Architecture and Physics

### 1. Tracking Error Computation

Each bar computes the absolute difference between the current price and the previous filter output:

$$\text{Diff}[n] = |x[n] - \text{Filt}[n-1]|$$

This measures how far the filter has drifted from the current price. Large tracking error indicates trending conditions; small error indicates the filter is tracking well (ranging).

### 2. HH/LL Normalization

The tracking error is normalized to [0, 1] using the highest high and lowest low of the Diff history over a lookback window of length $N$:

$$HH = \max(\text{Diff}[n], \text{Diff}[n-1], \ldots, \text{Diff}[n-N+1])$$

$$LL = \min(\text{Diff}[n], \text{Diff}[n-1], \ldots, \text{Diff}[n-N+1])$$

$$\text{coeff}[n] = \frac{\text{Diff}[n] - LL}{HH - LL}$$

When $HH = LL$ (constant tracking error), the coefficient defaults to 0.5.

### 3. Median Smoothing

The normalized coefficient is smoothed via a running median over a second window of length $M$:

$$\alpha[n] = \text{median}(\text{coeff}[n], \text{coeff}[n-1], \ldots, \text{coeff}[n-M+1])$$

The median prevents outlier tracking errors from causing sudden jumps in alpha. Typical values: $N = 20$, $M = 5$.

### 4. Adaptive Laguerre Cascade

With alpha determined, the standard four-element all-pass cascade runs with $\gamma = 1 - \alpha$:

$$L_0[n] = \alpha \cdot x[n] + (1 - \alpha) \cdot L_0[n-1]$$

$$L_1[n] = -(1-\alpha) \cdot L_0[n] + L_0[n-1] + (1-\alpha) \cdot L_1[n-1]$$

$$L_2[n] = -(1-\alpha) \cdot L_1[n] + L_1[n-1] + (1-\alpha) \cdot L_2[n-1]$$

$$L_3[n] = -(1-\alpha) \cdot L_2[n] + L_2[n-1] + (1-\alpha) \cdot L_3[n-1]$$

### 5. Output

$$\text{Filt}[n] = \frac{L_0 + 2L_1 + 2L_2 + L_3}{6}$$

### 6. Adaptive Behavior Summary

| Market Condition | Tracking Error | Alpha | Gamma | Filter Behavior |
|------------------|---------------|-------|-------|-----------------|
| Strong trend | Large | Near 1 | Near 0 | Fast tracking (FIR-like) |
| Range-bound | Small | Near 0 | Near 1 | Heavy smoothing (IIR-like) |
| Transition | Medium | 0.3-0.7 | 0.3-0.7 | Balanced response |

## Mathematical Foundation

### Transfer Function

The time-varying nature of alpha means the filter is technically linear time-varying (LTV), not LTI. At any instant, the transfer function matches the standard Laguerre with the current gamma:

$$A(z) = \frac{\gamma + z^{-1}}{1 + \gamma z^{-1}} \quad \text{where } \gamma = 1 - \alpha[n]$$

### Parameter Mapping

| Parameter | Symbol | Default | Range | Effect |
|-----------|--------|---------|-------|--------|
| Length | $N$ | 20 | 1-200 | HH/LL lookback for tracking error normalization |
| MedianLength | $M$ | 5 | 1-50 | Median window for alpha smoothing |

Shorter length makes alpha more responsive to recent tracking error changes. Longer median length produces smoother alpha transitions but adds lag to the adaptation.

### Warmup Period

The filter requires $\max(4, N)$ bars before producing reliable output. The first bar initializes all Laguerre elements to the input price.

## Performance Profile

| Metric | Value | Notes |
|--------|-------|-------|
| Operations per bar | ~$N + M\log M$ | HH/LL scan + insertion sort for median |
| Memory | $O(N + M)$ | Circular buffers for Diff and coeff history |
| SIMD potential | Low | Sequential dependency chain (IIR + adaptive) |
| Streaming complexity | O(1) amortized | Ring buffer operations |
| Allocation in Update | Zero | Pre-allocated circular buffers |

### Quality Metrics (1-10 Scale)

| Quality | Score | Justification |
|---------|-------|---------------|
| Smoothness | 8 | Adapts smoothing to conditions |
| Lag | 7 | Reduces lag in trends, increases in ranges |
| Overshoot | 6 | Median prevents most whipsaw |
| Adaptiveness | 9 | Core design feature |
| Complexity | 5 | More parameters than fixed Laguerre |
| Robustness | 8 | NaN-safe, bar-correction safe |

## Validation

Since the Adaptive Laguerre Filter is a custom Ehlers indicator not found in standard external libraries (TA-Lib, Skender, Tulip, Ooples), validation focuses on self-consistency:

| Validation Type | Status | Notes |
|-----------------|--------|-------|
| Batch == Streaming | Verified | All 4 modes match to 1e-10 |
| Constant input convergence | Verified | Converges to input value |
| First bar returns input | Verified | All parameter combinations |
| Variance reduction | Verified | Filtered < source variance |
| Bar correction revert | Verified | Exact state restoration |
| Prime == streaming | Verified | Matches to 1e-10 |

## Common Pitfalls

1. **Confusing with Laguerre RSI adaptation.** The CU/CD approach from L-element differences measures internal state ratios. This implementation measures direct tracking error, which is more responsive. Using the wrong algorithm produces a flatline.

2. **Setting length too short.** With length < 5, the HH/LL normalization becomes unstable and alpha oscillates rapidly. Minimum recommended: 10.

3. **Setting medianLength too long.** Large median windows (> 20) add significant lag to the adaptation, defeating the purpose. Keep medianLength at 3-7 for responsive adaptation.

4. **Expecting identical output to fixed Laguerre.** The adaptive variant produces different smoothing at every bar. Direct comparison only works for constant-input scenarios.

5. **Stacking adaptive indicators.** Chaining ALaguerre with other adaptive filters (KAMA, VIDYA) creates double-adaptation that is difficult to reason about. Prefer chaining one adaptive filter with fixed-parameter post-processing.

6. **Ignoring warmup period.** The first $N$ bars use partial tracking error history. The `IsHot` property correctly reflects warmup status.

7. **Not accounting for bar correction.** The `isNew=false` rollback mechanism restores both scalar state and circular buffer state. Always use the `isNew` parameter correctly for real-time data.

## References

- Ehlers, John F. *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004. ISBN: 978-0-471-46307-8.
- WiseStockTrader Amibroker AFL: Adaptive Laguerre Filter implementation.
- Ehlers, John F. "Laguerre Filter." *Technical Analysis of Stocks and Commodities*, various issues.
