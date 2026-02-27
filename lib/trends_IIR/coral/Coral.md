# CORAL — Coral Trend Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `cd` (default 0.4)                      |
| **Outputs**      | Single series (Coral)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- The **Coral** filter is a smooth, low-lag trend indicator that chains six cascaded EMA passes and combines stages 3–6 using polynomial coefficients...
- Parameterized by `period`, `cd` (default 0.4).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Overview

The **Coral** filter is a smooth, low-lag trend indicator that chains six cascaded EMA passes and combines stages 3–6 using polynomial coefficients derived from a "Constant D" parameter. Originally adapted by [LazyBear](https://www.tradingview.com/u/LazyBear/) from an MT4 implementation, Coral produces a responsive trend line with significantly less lag than a single EMA of equivalent smoothness.

**Category:** Trends (IIR)
**Minimum bars:** `period`

## Origin and Sources

The Coral filter appeared in TradingView as "Coral Trend Indicator" by LazyBear, who adapted it from MetaTrader 4 code. The algorithm uses 6 cascaded EMAs — a technique similar to T3 (Tillson T3) — combined with polynomial weighting controlled by a single "Constant D" parameter.

The name "Coral" is not an acronym; it refers to the smooth, organic appearance of the resulting trend line.

## Calculation

### Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| period | int | 21 | > 0 | Smoothing period for the EMA cascade |
| cd | double | 0.4 | [0, 1] | Constant D — controls polynomial combination weights |

### Algorithm

**Step 1: Derive EMA alpha**

```
di = (period - 1) / 2 + 1
α  = 2 / (di + 1)
```

**Step 2: Compute polynomial coefficients from Constant D**

```
c3 = 3 × (cd² + cd³)
c4 = -3 × (2cd² + cd + cd³)
c5 = 3cd + 1 + cd³ + 3cd²
```

**Step 3: Cascade 6 EMAs**

```
i1 = α × source + (1-α) × i1[prev]
i2 = α × i1     + (1-α) × i2[prev]
i3 = α × i2     + (1-α) × i3[prev]
i4 = α × i3     + (1-α) × i4[prev]
i5 = α × i4     + (1-α) × i5[prev]
i6 = α × i5     + (1-α) × i6[prev]
```

**Step 4: Polynomial combination of stages 3–6**

```
Coral = -cd³ × i6 + c3 × i5 + c4 × i4 + c5 × i3
```

### Unity DC Gain

The coefficients satisfy:

```
c3 + c4 + c5 + (-cd³) = 1
```

This guarantees that a constant input converges exactly to itself (unity DC gain) — no bias under flat conditions.

### Special Cases

| cd | c3 | c4 | c5 | -cd³ | Coral Reduces To |
|----|----|----|----|----|------------------|
| 0 | 0 | 0 | 1 | 0 | i3 (triple cascaded EMA) |
| 1 | 6 | -15 | 10 | -1 | Weighted combination of all 4 stages |

## Interpretation

The Coral filter is used as a **trend-following overlay**:

- **Trend direction**: Price above Coral = bullish; below = bearish
- **Trend strength**: Steeper Coral slope = stronger trend
- **Support/resistance**: Coral acts as dynamic support in uptrends, resistance in downtrends
- **Signal line**: Coral crossovers with price or another MA generate trading signals

### Constant D Tuning

- **cd = 0**: Minimal smoothing (just triple EMA), fastest response, more noise
- **cd = 0.4**: Default balance of smoothness and responsiveness
- **cd → 1**: Maximum smoothing, smoother line but more lag

## Implementation Details

### Architecture

```
sealed class Coral : AbstractBase
├── State: record struct (I1..I6, Count, IsHot)
├── 6 cascaded EMAs using FMA
├── Polynomial combination via nested FMA
├── Bar correction: _state / _p_state pair
└── NaN handling: last-valid-value substitution
```

### Performance

| Aspect | Detail |
|--------|--------|
| Time complexity | O(1) per update |
| Space complexity | O(1) — 6 doubles + counter |
| FMA usage | All 6 EMA cascades + polynomial combination |
| SIMD | Not applicable (serial dependency chain) |
| Batch optimization | Loop unrolling with `Unsafe.Add` |
| Zero-allocation | `Batch(ReadOnlySpan, Span)` path |

### Quality Metrics

| Metric | Value |
|--------|-------|
| Tests | 30+ (unit + validation + Quantower) |
| Warnings | 0 |
| PineScript validation | Exact match (1e-9 tolerance) |
| Unity DC gain verified | All cd values [0, 1] |

## Comparison with Similar Indicators

| Indicator | Cascades | Coefficients | Parameters |
|-----------|----------|-------------|------------|
| EMA | 1 | n/a | period |
| DEMA | 2 | 2, -1 | period |
| TEMA | 3 | 3, -3, 1 | period |
| T3 | 6 | Volume factor based | period, vfactor |
| **CORAL** | **6** | **cd-polynomial** | **period, cd** |

Coral is most similar to T3 in structure (6 cascaded EMAs), but uses a different coefficient derivation. T3 uses a "volume factor" to compute its combination weights, while Coral uses "Constant D" with a cubic polynomial.

## Pitfalls and Edge Cases

1. **Lag in trending markets**: Like all smoothing indicators, Coral lags behind price. Higher periods and higher cd values increase lag.
2. **Whipsaw in ranging markets**: Frequent crossovers during consolidation can produce false signals.
3. **cd range**: cd must be in [0, 1]. Values outside this range produce invalid coefficients.
4. **Warmup**: The 6-cascade structure means Coral needs more bars than a single EMA to fully stabilize, despite the warmup period being set to `period`.

## Performance Profile

### Operation Count (Streaming Mode)

CORAL(N, cd) runs 6 cascaded EMA stages with a shared alpha. The polynomial combination (bfr = −cd³·I6 + c3·I5 + c4·I4 + c5·I3) uses 4 precomputed coefficients computed at construction — so runtime is just 4 FMAs.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| EMA stage 1: FMA(α, src, decay×I1) | 1 | 4 | ~4 |
| EMA stage 2: FMA(α, I1, decay×I2) | 1 | 4 | ~4 |
| EMA stage 3: FMA(α, I2, decay×I3) | 1 | 4 | ~4 |
| EMA stage 4: FMA(α, I3, decay×I4) | 1 | 4 | ~4 |
| EMA stage 5: FMA(α, I4, decay×I5) | 1 | 4 | ~4 |
| EMA stage 6: FMA(α, I5, decay×I6) | 1 | 4 | ~4 |
| Polynomial combination (4 FMA) | 4 | 4 | ~16 |
| **Total** | **10** | — | **~40 cycles** |

O(1) per bar. Six scalar FMAs for the cascade and 4 FMAs for the polynomial combination. WarmupPeriod = N. The shared alpha `di = (N-1)/2 + 1` slightly lengthens the effective period relative to standard EMA.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| 6 cascaded EMA passes | No | Each stage is a recursive IIR depending on previous output |
| Polynomial combination | Yes | 4 FMAs with constant coefficients; vectorizable across bars once EMA stages are computed |

All 6 EMA stages are recursive IIR — inherently sequential. The polynomial combination is the only vectorizable phase, but it contributes only 4 of the 40 total cycles. Batch mode coefficient: no meaningful SIMD speedup over scalar.

## References

- LazyBear, "Coral Trend Indicator" — [TradingView](https://www.tradingview.com/u/LazyBear/)
- Original MT4 implementation (author unknown)
- Related: Tillson, T. "Smoothing Techniques for More Accurate Signals" — TASC, 1998 (T3 cascade technique)
