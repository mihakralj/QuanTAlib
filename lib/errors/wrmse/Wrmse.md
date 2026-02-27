# WRMSE: Weighted Root Mean Squared Error

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Wrmse)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- WRMSE extends the classic RMSE by incorporating weights for each observation, enabling analysts to emphasize critical data points such as recent ob...
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Not all errors are created equal—WRMSE lets you decide which ones matter most."

WRMSE extends the classic RMSE by incorporating weights for each observation, enabling analysts to emphasize critical data points such as recent observations, high-volume periods, or specific market regimes. When all weights are equal, WRMSE reduces exactly to RMSE, making it a strict generalization. This implementation uses dual RingBuffers for O(1) streaming updates with periodic resync to manage floating-point drift.

## Historical Context

Root Mean Squared Error has been a foundational metric in statistics and signal processing since Gauss's work on least squares in the early 19th century. The weighted variant emerged naturally from generalized least squares theory, where heteroscedasticity (non-constant variance) necessitates giving different importance to different observations.

In financial contexts, weighting becomes essential because market conditions vary significantly: a 1% error during a flash crash carries different implications than the same error during low-volatility consolidation. Volume-weighted errors, recency-weighted errors, and regime-adaptive weighting schemes all build on this foundation.

The implementation here maintains exact mathematical equivalence to RMSE when weights are uniform, verified through comparison tests against our standard RMSE indicator.

## Architecture & Physics

The indicator maintains two parallel RingBuffers tracking weighted squared errors and weights separately, enabling proper normalization as the window slides.

### 1. Dual Buffer State Management

The state tracks running sums for both numerator and denominator:

$$
\text{State} = \begin{cases}
\text{WeightedErrorSum} & \sum_{i \in W} w_i \cdot (a_i - p_i)^2 \\
\text{WeightSum} & \sum_{i \in W} w_i
\end{cases}
$$

where $W$ is the current window of observations.

### 2. O(1) Streaming Updates

Each new observation triggers constant-time updates via the sliding window pattern:

$$
\text{WeightedErrorSum}_{t} = \text{WeightedErrorSum}_{t-1} - \text{oldest}_e + w_t \cdot (a_t - p_t)^2
$$

$$
\text{WeightSum}_{t} = \text{WeightSum}_{t-1} - \text{oldest}_w + w_t
$$

### 3. Bar Correction Support

The `isNew=false` pattern enables bar correction for live trading scenarios where the current bar's values may update multiple times before close. State rollback uses `_p_state` (previous valid state) to restore buffers to the pre-correction position.

### 4. Floating-Point Drift Mitigation

Running sums accumulate floating-point errors over time. Periodic resync (every 1000 ticks by default) recalculates sums from buffer contents to bound drift.

## Mathematical Foundation

### Core Formula

$$
\text{WRMSE} = \sqrt{\frac{\sum_{i=1}^{n} w_i \cdot (a_i - p_i)^2}{\sum_{i=1}^{n} w_i}}
$$

where:
- $a_i$ = actual value at position $i$
- $p_i$ = predicted value at position $i$
- $w_i$ = weight at position $i$ (must be non-negative)
- $n$ = window size (period)

### Reduction to RMSE

When all weights are equal ($w_i = c$ for constant $c$):

$$
\text{WRMSE} = \sqrt{\frac{c \cdot \sum_{i=1}^{n} (a_i - p_i)^2}{n \cdot c}} = \sqrt{\frac{\sum_{i=1}^{n} (a_i - p_i)^2}{n}} = \text{RMSE}
$$

### Weight Normalization

The denominator $\sum w_i$ ensures the metric remains scale-invariant with respect to weight magnitude. Doubling all weights produces identical results.

### NaN/Invalid Value Handling

Invalid inputs (NaN, Infinity, negative weights) substitute the last valid value:

$$
v_t = \begin{cases}
v_t & \text{if } v_t \text{ is finite and valid} \\
v_{\text{last valid}} & \text{otherwise}
\end{cases}
$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Validation/NaN checks | 3 | 1 | 3 |
| SUB (diff) | 1 | 1 | 1 |
| MUL (diff², weight×diff²) | 2 | 3 | 6 |
| ADD/SUB (running sums) | 4 | 1 | 4 |
| DIV | 1 | 15 | 15 |
| SQRT | 1 | 15 | 15 |
| CMP (weight threshold) | 1 | 1 | 1 |
| **Total** | **~13** | — | **~45 cycles** |

The dominant costs are DIV and SQRT (67% combined), consistent with other RMSE-family indicators.

### Batch Mode (SIMD/FMA)

For uniform weights, the batch path delegates to the same SIMD infrastructure as RMSE:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Squared error computation | N | N/4 | 4× |
| Rolling mean | N | N (sequential) | 1× |

For weighted batch computation, an additional weight multiplication occurs in the SIMD loop, but the rolling aggregation remains sequential due to the cumulative nature of the window.

**Per-bar efficiency:**

| Mode | Cycles/bar | Notes |
| :--- | :---: | :--- |
| Streaming (uniform weights) | ~45 | Uses default weight 1.0 |
| Streaming (custom weights) | ~48 | Additional weight validation |
| Batch SIMD (uniform) | ~30 | Amortized over 4-wide vectors |
| Batch SIMD (weighted) | ~35 | Weight multiplication in SIMD |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact formula implementation |
| **Flexibility** | 9/10 | Custom weights enable regime adaptation |
| **Interpretability** | 8/10 | Same units as input, but weights add complexity |
| **Robustness** | 9/10 | NaN handling, negative weight rejection |
| **Performance** | 8/10 | O(1) streaming, SIMD batch support |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Internal RMSE** | ✅ | Uniform weights match RMSE within 1e-10 |
| **Manual Calculation** | ✅ | Step-by-step verification in tests |
| **TA-Lib** | N/A | No WRMSE implementation |
| **Skender** | N/A | No WRMSE implementation |
| **Tulip** | N/A | No WRMSE implementation |

WRMSE is validated by:
1. Mathematical equivalence to RMSE with uniform weights (BatchSpan_UniformWeights_MatchesStreaming, UniformWeightsBatch_MatchesRmseBatch)
2. Manual calculation verification (Wrmse_CalculatesCorrectlyWithWeights)
3. Weight influence tests (Wrmse_HigherWeightsHaveMoreInfluence)
4. Streaming/batch parity tests for both uniform and custom weights

## Common Pitfalls

1. **Weight Interpretation**: Weights are multiplied by squared errors, not linear errors. A weight of 2.0 gives that observation twice the influence in the squared error sum, which may not align with intuitive expectations.

2. **Zero Weight Sum**: If all weights in the window sum to effectively zero (< 1e-10), the result is 0.0 to avoid division by zero. This edge case should be rare in practice.

3. **Negative Weights**: Negative weights are rejected and replaced with the last valid weight. Consider using absolute values or clamping in upstream processing if your weight source may produce negatives.

4. **Memory Footprint**: Each instance allocates two RingBuffers of size `period`, doubling memory compared to unweighted RMSE. For a period of 100:
   - 2 buffers × 100 doubles × 8 bytes = 1,600 bytes per instance
   - 10,000 concurrent instances ≈ 15.3 MB

5. **Warmup Period**: The indicator requires `period` observations before `IsHot` becomes true. During warmup, results use the available data but may not represent the full window statistics.

6. **Bar Correction**: When using `isNew=false`, ensure you're correcting the most recent observation. Multiple corrections without intervening new bars work correctly, but the pattern assumes temporal locality.

## References

- Aitken, A.C. (1936). "On Least Squares and Linear Combinations of Observations." *Proceedings of the Royal Society of Edinburgh*.
- Gauss, C.F. (1809). *Theoria Motus Corporum Coelestium*. (Foundation of least squares theory)
- Greene, W.H. (2012). *Econometric Analysis*. 7th ed. Chapter 9: Generalized Least Squares.
