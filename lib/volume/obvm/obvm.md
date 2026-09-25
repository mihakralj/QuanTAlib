# OBVM: On-Balance Volume Modified

> *Classic OBV is noisy and unbounded. OBVM applies dual EMA smoothing — one for the OBV line, one for the signal — producing a cleaner volume-momentum indicator with crossover-based trade signals.*

| Property         | Value                                  |
| ---------------- | -------------------------------------- |
| **Category**     | Volume                                 |
| **Inputs**       | TBar (close + volume)                  |
| **Parameters**   | `obvmLength` (default 7), `signalLength` (default 10) |
| **Outputs**      | Dual: OBVM line + Signal line          |
| **Output range** | Unbounded (OBV-scale)                  |
| **Warmup**       | max(obvmLength, signalLength) bars     |
| **PineScript**   | [obvm.pine](obvm.pine)                |

- OBVM computes standard On-Balance Volume, then applies two sequential EMA stages: the first smooths OBV into the OBVM line, the second smooths OBVM into a signal line for crossover detection.
- **Similar:** [OBV](../obv/) (unsmoothed), [AOBV](../aobv/) (archer OBV) | **Complementary:** Price moving averages for trend confirmation | **Trading note:** OBVM above Signal = bullish accumulation; crossover = trade signal; divergences with price signal reversals.
- No external validation libraries implement OBVM. Validated through self-consistency and behavioral testing against the published algorithm.

## Historical Context

Vitali Apirine published the On-Balance Volume Modified indicator in the April 2020 issue of *Technical Analysis of Stocks & Commodities* magazine. The motivation was to address two limitations of classic OBV: (1) the raw cumulative series is noisy, making it difficult to identify clean trend changes, and (2) there is no built-in signal line for crossover-based trading. By applying dual EMA smoothing with separate length parameters, OBVM provides both a smoother trend-following volume line and a confirmation signal.

## Architecture & Mathematics

### Stage 1: On-Balance Volume (OBV)

Standard cumulative OBV:
- If $\text{Close} > \text{PrevClose}$: $\text{OBV} \mathrel{+}= \text{Volume}$
- If $\text{Close} < \text{PrevClose}$: $\text{OBV} \mathrel{-}= \text{Volume}$
- If $\text{Close} = \text{PrevClose}$: OBV unchanged

### Stage 2: OBVM Line (EMA of OBV)

$$\text{OBVM}_t = \alpha_1 \cdot \text{OBV}_t + (1 - \alpha_1) \cdot \text{OBVM}_{t-1}$$

where $\alpha_1 = \frac{2}{\text{obvmLength} + 1}$

### Stage 3: Signal Line (EMA of OBVM)

$$\text{Signal}_t = \alpha_2 \cdot \text{OBVM}_t + (1 - \alpha_2) \cdot \text{Signal}_{t-1}$$

where $\alpha_2 = \frac{2}{\text{signalLength} + 1}$

## Interpretation

- **OBVM above Signal**: Bullish volume momentum — accumulation phase.
- **OBVM below Signal**: Bearish volume momentum — distribution phase.
- **Crossovers**: OBVM crossing above Signal is a buy signal; crossing below is a sell signal.
- **Divergences**: When price makes new highs/lows but OBVM does not, a potential reversal is signalled.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation        | Count | Notes                          |
|:---------------- |------:|:-------------------------------|
| Comparisons      |   2   | close vs prevClose             |
| Additions        |   1   | OBV update                     |
| FMA              |   2   | Two EMA updates                |
| **Total**        |  ~5   | Per bar, O(1)                  |

### Batch Mode

Linear scan over close + volume spans. Two sequential EMA passes. Zero-allocation with `stackalloc` ≤ 256, `ArrayPool` above.

### Quality Metrics

| Metric            | Value                                  |
|:------------------|:---------------------------------------|
| Time complexity   | O(1) per bar                           |
| Space complexity  | O(1) (state record struct)             |
| SIMD-friendly     | ✓ (linear scan over close+volume spans) |
| NaN handling      | Substitutes last valid close/volume    |

## Validation

OBVM is validated through self-consistency tests (streaming ≡ batch) and behavioral tests (constant input → 0, trending signals, crossover detection, dual output correctness).

### Behavioral Test Summary

| Category                  | Tests |
|:--------------------------|------:|
| Constructor validation    |   5   |
| OBV + EMA correctness    |   5   |
| Dual output               |   3   |
| Streaming/batch consistency |  3  |
| Bar correction            |   2   |
| NaN/Infinity handling     |   3   |
| Edge cases                |   3   |

## Common Pitfalls

1. **No volume data**: OBV/OBVM stays unchanged — use TBar input with volume, not TValue.

2. **Short EMA periods**: Very responsive but noisy; increase obvmLength to smooth.

3. **Scale dependence**: OBVM values are on OBV scale (cumulative volume), not normalised. Cross-instrument comparison requires separate normalisation.

4. **EMA seeding**: Both EMAs are seeded at bar 0 with OBV = 0, matching the streaming path where the first bar always has zero OBV (no previous close for comparison).
