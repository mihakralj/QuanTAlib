# HT_PHANA: Ehlers Phasor Analysis

> *Phasor Analysis uses Pearson correlation as a matched filter — correlating price against cosine and sine reference waves to extract the instantaneous phase of the dominant cycle, revealing valleys at −90° and peaks at +90°.*

| Property         | Value                                  |
| ---------------- | -------------------------------------- |
| **Category**     | Cycles                                 |
| **Inputs**       | Source (close)                         |
| **Parameters**   | `period` (default 28)                  |
| **Outputs**      | Triple: Angle, DerivedPeriod, TrendState |
| **Output range** | Angle: unbounded; DerivedPeriod: [0, 60]; TrendState: {−1, 0, +1} |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [ht_phana.pine](ht_phana.pine)        |

- HT_PHANA computes a phasor by correlating a sliding price window against cosine and negative-sine reference waves at the presumed cycle period. The resulting angle tracks instantaneous cycle phase, from which a derived period and trend state are extracted.
- **Similar:** [CCOR](../ccor/ccor.md) (earlier Ehlers correlation cycle), [HT_PHASOR](../ht_phasor/HtPhasor.md) (Hilbert Transform phasor) | **Complementary:** [EBSW](../ebsw/ebsw.md), [FSI](../fsi/Fsi.md) for cycle confirmation | **Trading note:** Cycle valleys at −90°, peaks at +90°; TrendState = +1/−1 when angle rate ≤ 6°/bar.
- No external validation libraries implement HT_PHANA. Validated through self-consistency and behavioral testing against the published algorithm.

## Historical Context

John F. Ehlers introduced Phasor Analysis in the November 2022 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "Recurring Phase Of Cycle Analysis." The technique uses Pearson correlation as a matched filter to determine how well price data correlates with cosine and sine waves at a presumed cycle period, producing the Real and Imaginary components of a phasor. This approach supersedes his earlier Correlation Cycle (CCOR, TASC June 2020) by adding wraparound compensation, monotonic constraints, and a derived trend state.

## Architecture & Mathematics

### Stage 1: Dual Pearson Correlation

Over a sliding window of `period` bars:

$$\text{Real} = \text{corr}\!\left(\text{price},\; \cos\!\left(\frac{2\pi k}{N}\right)\right)$$

$$\text{Imag} = \text{corr}\!\left(\text{price},\; -\sin\!\left(\frac{2\pi k}{N}\right)\right)$$

Pearson correlation acts as a matched filter — it measures how well the price window matches a pure cosine (Real) or negative-sine (Imaginary) at the assumed cycle period. Pre-computed trig lookup tables eliminate repeated transcendental calls.

### Stage 2: Angle Calculation

$$\text{Angle} = 90° - \arctan\!\left(\frac{\text{Imag}}{\text{Real}}\right)$$

With quadrant correction: if $\text{Real} < 0$, subtract 180°.

### Stage 3: Wraparound Compensation

When the angle crosses the 360° boundary (previous angle > 90° and current < −90°), subtract 360° to maintain continuity. A monotonic constraint prevents the angle from decreasing except at extreme deep-negative regions.

### Stage 4: Derived Period

$$\text{DerivedPeriod} = \frac{360°}{\Delta\text{Angle}}$$

where $\Delta\text{Angle}$ is the per-bar angle change. When $\Delta\text{Angle} \leq 0$, the previous delta is reused. The result is clamped to a maximum of 60.

### Stage 5: Trend State

When the angle rate-of-change ≤ 6°/bar:
- If angle ≥ 90° or ≤ −90° → **+1** (trending long)
- If −90° < angle < 90° → **−1** (trending short)
- Otherwise → **0** (cycling)

## Interpretation

- The phasor angle oscillates between −180° and +180°, completing one full rotation per dominant cycle period.
- **Cycle valleys** correspond to the angle crossing −90°.
- **Cycle peaks** correspond to the angle near +90°.
- **TrendState = +1** indicates the market has transitioned from cycling to trending (long bias).
- **TrendState = −1** indicates trending short bias.
- **DerivedPeriod** provides a real-time estimate of the dominant cycle length — useful for adaptive parameter tuning.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation                 | Count | Notes                              |
|:------------------------- |:----- |:---------------------------------- |
| RingBuffer push           | 1     | Store current price                |
| Dot products × 5          | 5     | Pearson correlation sums (Σx, Σy, Σxy, Σx², Σy²) × 2 sets |
| Division + atan           | 1     | Angle from Real/Imag              |
| Comparisons (quadrant)    | 3     | Quadrant fix, wraparound, monotonic |
| Division (period)         | 1     | 360 / ΔAngle                      |
| **Total per bar**         | **~11** | O(period) due to correlation window |

### Batch Mode

Batch mode computes correlation sums in a single O(n·period) pass. Pre-computed cosine and sine lookup tables are shared across bars. Zero-allocation with `stackalloc` ≤ 256, `ArrayPool` above.

### Quality Metrics

| Metric              | Score   | Notes                                    |
|:------------------- |:------- |:---------------------------------------- |
| Lag                 | Low     | Matched filter responds within 1 bar     |
| Noise rejection     | High    | Pearson correlation rejects non-periodic noise |
| Sensitivity         | High    | Detects phase shifts immediately         |
| Bounded output      | Partial | Angle unbounded; DerivedPeriod/TrendState bounded |
| Parameter count     | 1       | Only period                              |
| Memory footprint    | O(period) | RingBuffer + trig lookup tables        |

## Validation

HT_PHANA is validated through self-consistency tests (streaming ≡ batch ≡ span ≡ eventing) and behavioral tests (constant input → 0° angle, trending signals, cycle detection).

### Behavioral Test Summary

| Test                    | Expected Result               |
|:----------------------- |:----------------------------- |
| Constant input          | Angle → 0, DerivedPeriod → 0 |
| Pure sine wave          | Angle tracks cycle phase      |
| Strong uptrend          | TrendState = +1               |
| Strong downtrend        | TrendState = −1               |
| NaN/Inf input           | Finite output (fallback)      |
| Bar correction (isNew)  | State restored correctly      |

## Common Pitfalls

1. **Period assumption**: The `period` parameter is the *assumed* dominant cycle length, not a smoothing period. If the market's actual cycle differs significantly, the phasor readings will be inaccurate. Use DerivedPeriod to check.

2. **Wraparound at ±180°**: The raw angle can wrap around the ±180° boundary, causing discontinuities. The built-in wraparound compensation handles this, but downstream consumers should not difference the raw angle naively.

3. **TrendState threshold**: The 6°/bar threshold is hardcoded per Ehlers' specification. When the angle advances slowly (< 6°/bar), the market is deemed trending rather than cycling.

4. **Not a Hilbert Transform**: Despite the `HT_` prefix (following naming convention), this is a Pearson-correlation-based phasor analysis, not a Hilbert Transform. It shares the naming pattern with other HT_ indicators for discoverability.
