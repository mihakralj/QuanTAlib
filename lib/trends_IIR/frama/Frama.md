# FRAMA: Ehlers Fractal Adaptive Moving Average

> "Markets do not move at one speed. FRAMA listens to the roughness and adjusts the filter."

FRAMA is John Ehlers' fractal adaptive moving average. It estimates a fractal dimension from high and low ranges, then converts that dimension into a dynamic EMA alpha. The result is a moving average that tightens in trends and relaxes in noise.

## Historical Context

FRAMA was introduced in Traders' Tips as an adaptive filter that uses fractal geometry as a proxy for market roughness. It is a classic Ehlers indicator and remains a reference point for adaptive smoothing.

## Architecture & Physics

FRAMA splits the window into two halves, compares the combined range to the full range, and derives a fractal dimension:

1. Compute ranges over the first half, second half, and full window.
2. Convert range ratios to a dimension estimate.
3. Convert dimension to a dynamic alpha.
4. Apply EMA smoothing to HL2 using that alpha.

The implementation follows the strict Ehlers definition:

- Range windows use High and Low, not Close.
- Smoothed price is HL2.
- Period is forced even.
- Alpha is clamped to [0.01, 1.0].

## Math Foundation

Let `N` be even, `h = N/2`. Ranges are:

$$ N_1 = \frac{\max(\text{High}_{t-h+1..t}) - \min(\text{Low}_{t-h+1..t})}{h} $$
$$ N_2 = \frac{\max(\text{High}_{t-2h+1..t-h}) - \min(\text{Low}_{t-2h+1..t-h})}{h} $$
$$ N_3 = \frac{\max(\text{High}_{t-2h+1..t}) - \min(\text{Low}_{t-2h+1..t})}{N} $$

Fractal dimension:

$$ D = \frac{\ln(N_1 + N_2) - \ln(N_3)}{\ln(2)} $$

Alpha and update:

$$ \alpha = \exp(-4.6 \cdot (D - 1)) $$
$$ \alpha = \min(1, \max(0.01, \alpha)) $$
$$ FRAMA_t = \alpha \cdot HL2_t + (1-\alpha) \cdot FRAMA_{t-1} $$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | TBD | Not benchmarked yet |
| **Allocations** | 0 | Streaming update is allocation free |
| **Complexity** | O(N) | Sliding range scan per update |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 8/10 | Adapts to trends quickly |
| **Overshoot** | 5/10 | Can overshoot on sharp reversals |
| **Smoothness** | 7/10 | Smoother than EMA in noise |

*Benchmarks pending. Use the `perf/` harness for exact numbers.*

## Validation

FRAMA is not implemented in the common TA libraries used by QuanTAlib. Validation uses a direct reference implementation that mirrors the PineScript logic.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Matches `lib/trends_IIR/frama/frama.pine` |

## Common Pitfalls

1. **Period parity**: The algorithm requires even `N`. Odd values are rounded up.
2. **Warmup**: Outputs are `NaN` until `N` bars are available.
3. **Range source**: FRAMA uses High and Low ranges. Feeding Close-only data collapses the ranges.
4. **Bar correction**: Use `isNew=false` for corrections so the last bar is recomputed safely.
