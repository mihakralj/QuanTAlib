# HHLLS: Higher Highs & Lower Lows Stochastics

> *Standard stochastics apply the same formula regardless of price action. HHLLS introduces a conditional gate — the stochastic fires only on higher highs or lower lows, producing directionally selective momentum readings.*

| Property         | Value                                  |
| ---------------- | -------------------------------------- |
| **Category**     | Oscillator                             |
| **Inputs**       | TBar (high, low)                       |
| **Parameters**   | `period` (default 20)                  |
| **Outputs**      | Dual: HHS (Higher High Stochastic), LLS (Lower Low Stochastic) |
| **Output range** | [0, 100]                               |
| **Warmup**       | `period` bars                          |
| **OB / OS**      | 60 / 10                               |
| **PineScript**   | [hhlls.pine](hhlls.pine)              |

- HHLLS computes conditional stochastic values on High and Low price series — HHS fires only when price makes a higher high, LLS only on lower lows — then smooths both with EMA to produce dual momentum oscillator lines.
- **Similar:** [Stoch](../../oscillators/stoch/) (unconditional stochastic) | **Complementary:** ADX for trend strength | **Trading note:** HHS > LLS = uptrend; crossovers signal reversals; OB/OS at 60/10.
- No external validation libraries implement HHLLS. Validated through self-consistency and behavioral testing against the published algorithm.

## Historical Context

Published in "Higher Highs & Lower Lows" (*Technical Analysis of Stocks & Commodities*, February 2016), Vitali Apirine recognized that standard stochastic oscillators apply uniform calculations regardless of price action. HHLLS introduces a conditional gate: the stochastic is computed only when price makes a **higher high** (for HHS) or a **lower low** (for LLS), otherwise the raw value is zero. EMA smoothing then converts these sparse signals into continuous oscillator lines.

## Architecture & Mathematics

### Stage 1: Sliding Window Extremes (4× MonotonicDeque)

Over a rolling `period`-bar window, compute:
- `HighestHigh` = max(High, period)
- `LowestHigh` = min(High, period)
- `HighestLow` = max(Low, period)
- `LowestLow` = min(Low, period)

### Stage 2: Conditional Stochastic

$$\text{hhRange} = \text{HighestHigh} - \text{LowestHigh}$$

$$\text{llRange} = \text{HighestLow} - \text{LowestLow}$$

If $\text{High} > \text{prevHigh}$ and $\text{hhRange} > 0$:

$$\text{hhRaw} = 100 \times \frac{\text{High} - \text{LowestHigh}}{\text{hhRange}}$$

Otherwise $\text{hhRaw} = 0$.

If $\text{Low} < \text{prevLow}$ and $\text{llRange} > 0$:

$$\text{llRaw} = 100 \times \frac{\text{HighestLow} - \text{Low}}{\text{llRange}}$$

Otherwise $\text{llRaw} = 0$.

### Stage 3: EMA Smoothing

$$\alpha = \frac{2}{\text{period} + 1}$$

$$\text{HHS}_t = \alpha \cdot \text{hhRaw}_t + (1 - \alpha) \cdot \text{HHS}_{t-1}$$

$$\text{LLS}_t = \alpha \cdot \text{llRaw}_t + (1 - \alpha) \cdot \text{LLS}_{t-1}$$

## Interpretation

- **HHS > LLS**: Uptrend. Higher highs dominate.
- **LLS > HHS**: Downtrend. Lower lows dominate.
- **Crossovers**: HHS crossing above LLS = bullish; LLS crossing above HHS = bearish.
- **Divergences**: Price making new highs while HHS declining = bearish divergence (and vice versa).
- **OB/OS**: HHS above 60 = strong uptrend conviction. LLS below 10 = weak selling pressure.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation         | Count | Notes                          |
|:------------------|------:|:-------------------------------|
| MonotonicDeque    |   4   | O(1) amortized push/get        |
| Comparisons       |   2   | Higher high / lower low gate   |
| Divisions          |   2   | Stochastic normalization      |
| FMA               |   2   | EMA smoothing (HHS, LLS)      |
| **Total**         | ~10   | Per bar                        |

### Batch Mode

Uses `Highest.Batch` and `Lowest.Batch` for O(n) sliding window, then single-pass EMA. Zero-allocation with `stackalloc` ≤ 256, `ArrayPool` above.

### Quality Metrics

| Metric            | Value |
|:------------------|:------|
| Memory per bar    | O(period) — 4 deques + 2 circular buffers |
| Steady-state ops  | O(1) per bar |
| Output range      | [0, 100] |
| NaN handling      | Substitutes last valid H/L |

## Validation

HHLLS is validated through self-consistency tests (streaming ≡ batch) and behavioral tests (constant input → 0, trending signals, crossover detection).

### Behavioral Test Summary

| Test              | Expected Result                        |
|:------------------|:---------------------------------------|
| Constant input    | Both outputs converge to 0             |
| Pure uptrend      | HHS rises, LLS decays toward 0        |
| Pure downtrend    | LLS rises, HHS decays toward 0        |
| Streaming=Batch   | Max divergence < 1e-10                 |
| Reset             | State clears, re-converges from scratch |
| Bar correction    | isNew=false restores previous state    |

## Common Pitfalls

1. **Single parameter**: Unlike Stoch, `period` controls both the stochastic window AND the EMA smoothing. There is no separate smoothing parameter.

2. **Sparse signals**: In ranging markets, few bars trigger the conditional gate, so both HHS and LLS stay near zero.

3. **Not symmetric**: HHS uses stochastic-style normalization on Highs; LLS uses Williams %R–style (inverted) on Lows.

4. **Warmup**: First `period` bars needed for sliding window. First bar has no previous High/Low for comparison, so raw = 0.
