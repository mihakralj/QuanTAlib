# FRAMA: Ehlers Fractal Adaptive Moving Average

> *Markets do not move at one speed. FRAMA listens to the roughness and adjusts the filter.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Frama)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `pe` bars                          |
| **PineScript**   | [frama.pine](frama.pine)                       |
| **Signature**    | [frama_signature](frama_signature.md) |

- FRAMA is John Ehlers' fractal adaptive moving average.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `pe` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

### Operation Count (Streaming Mode, Scalar)

**Hot path (buffer full, period=20):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP | 3×N | 1 | 60 |
| ADD/SUB | 6 | 1 | 6 |
| DIV | 3 | 15 | 45 |
| LOG | 2 | 40 | 80 |
| EXP | 1 | 50 | 50 |
| MUL | 2 | 3 | 6 |
| FMA | 1 | 4 | 4 |
| **Total** | — | — | **~251 cycles** |

The hot path consists of:
1. HL2 price: `(high + low) * 0.5` — 1 ADD + 1 MUL
2. Range scans (3 windows): min/max over N, N/2, N/2 — 3×N CMP (60 for period=20)
3. Range normalization: 3 DIV operations
4. Fractal dimension: `(ln(N1+N2) - ln(N3)) / ln(2)` — 2 LOG + 1 ADD + 1 SUB + 1 DIV
5. Alpha calculation: `exp(-4.6 * (D - 1))` — 1 EXP + 1 MUL + 1 SUB
6. EMA update: `FMA(prev, 1-alpha, alpha * price)` — 1 FMA + 1 MUL

**Complexity note:** Range scans are O(N) per update. For period=20, this is ~60 comparisons. For period=50, ~150 comparisons.

**Warmup path:**

During warmup (bars < period), only buffer fills occur — O(1) per bar.

### Batch Mode (SIMD Analysis)

FRAMA is an IIR filter with sliding window min/max — **not vectorizable** across bars due to:
1. Recursive EMA state dependency
2. O(N) range scans that don't benefit from SIMD without monotonic deque optimization

| Optimization | Potential Benefit |
| :--- | :--- |
| Monotonic deque | O(1) amortized min/max (not implemented) |
| FMA instructions | ~2 cycle savings in final update |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 8/10 | Adapts to trends quickly |
| **Overshoot** | 5/10 | Can overshoot on sharp reversals |
| **Smoothness** | 7/10 | Smoother than EMA in noise |

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
