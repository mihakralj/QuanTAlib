# TEMA: Triple Exponential Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Tema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period * 3` bars                          |

### TL;DR

- The Triple Exponential Moving Average (TEMA) is a lag-reducing filter that combines a single, double, and triple EMA.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period * 3` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Patrick Mulloy looked at the lag of an EMA and took it personally. TEMA is what happens when you apply algebra to impatience."

The Triple Exponential Moving Average (TEMA) is a lag-reducing filter that combines a single, double, and triple EMA. Unlike a simple triple smoothing (which would be incredibly slow), TEMA uses a weighted combination of the three to cancel out the lag, resulting in an indicator that hugs price action tighter than a spandex cycling short.

## Historical Context

Introduced by Patrick Mulloy in *Technical Analysis of Stocks & Commodities* (Jan 1994), "Smoothing Data With Less Lag." Mulloy's goal was to replace the standard moving averages in MACD and other indicators to reduce the delay in signal generation.

## Architecture & Physics

TEMA is not just "EMA applied three times." That would be $EMA(EMA(EMA(x)))$. TEMA is a composite:
$$ TEMA = 3 \cdot EMA_1 - 3 \cdot EMA_2 + EMA_3 $$

This formula effectively projects the trend forward to compensate for the delay inherent in smoothing.

### Convergence Speed

Because of the aggressive weighting, TEMA converges (warms up) faster than a standard EMA. While an EMA takes $\approx 3.45(N+1)$ steps to converge to 99.9%, TEMA stabilizes quicker due to the subtraction terms canceling out the initial error.

## Mathematical Foundation

### 1. The Cascade

$$ EMA_1 = EMA(Price) $$
$$ EMA_2 = EMA(EMA_1) $$
$$ EMA_3 = EMA(EMA_2) $$

### 2. The Combination

$$ TEMA = (3 \times EMA_1) - (3 \times EMA_2) + EMA_3 $$

## Performance Profile

### Operation Count (Streaming Mode)

TEMA requires 3 cascaded EMA updates plus the combination formula:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| EMA update (×3) | 3 | 7 | 21 |
| MUL (3×e1, 3×e2) | 2 | 3 | 6 |
| SUB (3×e1 - 3×e2) | 1 | 1 | 1 |
| ADD (+ e3) | 1 | 1 | 1 |
| **Total (hot)** | **7** | — | **~29 cycles** |

During warmup, each EMA stage has additional compensator overhead (~21 cycles × 3 = ~63 cycles).

**Total during warmup:** ~92 cycles/bar; **Post-warmup:** ~29 cycles/bar.

### Batch Mode (SIMD Analysis)

TEMA is inherently recursive due to cascaded EMAs. SIMD parallelization across bars is not possible. Each EMA stage must complete before feeding the next:

| Optimization | Operations | Cycles Saved |
| :--- | :---: | :---: |
| FMA in each EMA stage | 3 FMA vs 3×(MUL+ADD) | ~6 cycles |
| Inline combination | Avoid intermediate stores | ~2 cycles |

**Per-bar efficiency:** ~29 cycles is 4× EMA cost, as expected for 3 EMA stages + combiner.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib exactly |
| **Timeliness** | 10/10 | Extremely low lag; nearly zero-lag tracking |
| **Overshoot** | 5/10 | Significant overshoot on sharp reversals |
| **Smoothness** | 6/10 | Less smooth than SMA/EMA due to high responsiveness |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~6 ns/bar | 3× EMA overhead |
| **Allocations** | 0 bytes | Zero-allocation in hot paths |
| **Complexity** | O(1) | Constant time regardless of period |
| **State Size** | 96 bytes | Three EMA states (32 bytes each) |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_TEMA` exactly. |
| **Skender** | ✅ | Matches `GetTema` exactly. |
| **Tulip** | ✅ | Matches `tema` exactly. |
| **Ooples** | ❌ | Diverges significantly due to initialization logic. |

## C# Implementation Considerations

### State Management

TEMA maintains six EmaState instances—three current, three previous—enabling atomic rollback on bar corrections:

```csharp
private record struct EmaState(double Ema, double E, bool IsHot, bool IsCompensated);

private EmaState _state1, _state2, _state3;
private EmaState _p_state1, _p_state2, _p_state3;
```

The `E` field tracks bias compensation factor for each EMA stage independently. Each state auto-transitions via `IsCompensated` flag when bias becomes negligible.

### Precomputed Constants

Constructor calculates smoothing constants once:

```csharp
_alpha = 2.0 / (period + 1);
_decay = 1 - _alpha;
```

These constants are reused across all three EMA stages, avoiding repeated division.

### FMA Usage

Each EMA update uses FusedMultiplyAdd for the standard EMA formula:

```csharp
double newEma = Math.FusedMultiplyAdd(state.Ema, _decay, _alpha * input);
```

The final TEMA combination `3*e1 - 3*e2 + e3` could use FMA but the coefficients (3, -3, 1) make chained FMA marginal; current implementation uses direct arithmetic.

### Bar Correction Pattern

TEMA's cascaded structure requires coordinated state rollback:

```csharp
if (isNew)
{
    _p_state1 = _state1;
    _p_state2 = _state2;
    _p_state3 = _state3;
}
else
{
    _state1 = _p_state1;
    _state2 = _p_state2;
    _state3 = _p_state3;
}
```

All three stages rollback atomically, ensuring consistent cascade state when `isNew=false`.

### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_alpha` | double | 8B | Smoothing constant |
| `_decay` | double | 8B | 1 - alpha |
| `_state1` | EmaState | 24B | First EMA state |
| `_state2` | EmaState | 24B | Second EMA state |
| `_state3` | EmaState | 24B | Third EMA state |
| `_p_state1` | EmaState | 24B | Previous state 1 |
| `_p_state2` | EmaState | 24B | Previous state 2 |
| `_p_state3` | EmaState | 24B | Previous state 3 |
| **Total** | | **160B** | Per indicator instance |

Each EmaState contains: Ema (8B), E (8B), IsHot (1B), IsCompensated (1B) + padding (~6B) = ~24B.

### Common Pitfalls

1. **Overshoot**: TEMA is so responsive it can overshoot price turns, creating a "whiplash" effect in volatile markets.
2. **Noise**: By reducing lag, TEMA sacrifices some noise suppression. It is "nervous" compared to an SMA.
3. **Identity Crisis**: Often confused with T3 (Tillson). T3 is a generalized version; TEMA is specifically T3 with $v=1$.
