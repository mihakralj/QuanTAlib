# PWMA: Parabolic Weighted Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Pwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [pwma_signature](pwma_signature) |

### TL;DR

- PWMA (Parabolic Weighted Moving Average) applies a parabolic ($i^2$) weighting scheme to the data window.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Linear weighting is for people who think the world is flat. PWMA squares the weights, because recent data isn't just more important—it's exponentially more important."

PWMA (Parabolic Weighted Moving Average) applies a parabolic ($i^2$) weighting scheme to the data window. This assigns massive importance to the most recent data points while still technically including the older data. It's like a WMA on steroids.

## Historical Context

While the WMA uses a linear triangle window ($1, 2, 3, \dots, n$), the PWMA uses a parabolic window ($1^2, 2^2, 3^2, \dots, n^2$). This was developed for traders who found the WMA too slow but the EMA too jittery. It provides a curve that turns faster than a WMA but is smoother than an EMA at the tail.

## Architecture & Physics

The "physics" is defined by the weight function $W_i = i^2$.
This shifts the center of gravity of the filter heavily towards the right (recent data).

## Mathematical Foundation

$$ \text{PWMA} = \frac{\sum_{i=1}^{N} i^2 P_{t-N+i}}{\sum_{i=1}^{N} i^2} $$

The O(1) update logic involves cascading the sums:
$$ S1_{new} = S1_{old} - \text{Oldest} + \text{Newest} $$
$$ S2_{new} = S2_{old} - S1_{old} + N \times \text{Newest} $$
$$ S3_{new} = S3_{old} - 2 S2_{old} + S1_{old} + N^2 \times \text{Newest} $$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

The O(1) algorithm uses triple cascading sums:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 9 | 1 | 9 |
| MUL | 3 | 3 | 9 |
| DIV | 1 | 15 | 15 |
| **Total** | **13** | — | **~33 cycles** |

**Hot path breakdown:**
- S1 update: `S1_new = S1_old - oldest + newest` → 2 ADD/SUB
- S2 update: `S2_new = S2_old - S1_old + N×newest` → 2 ADD/SUB + 1 MUL
- S3 update: `S3_new = S3_old - 2×S2_old + S1_old + N²×newest` → 4 ADD/SUB + 2 MUL
- Final: `PWMA = S3 / divisor` → 1 DIV (divisor precomputed)

**Comparison with naive O(N) implementation:**

| Mode | Complexity | Cycles (Period=100) |
| :--- | :---: | :---: |
| Naive (recalculate) | O(N) | ~700 cycles |
| QuanTAlib O(1) | O(1) | ~33 cycles |
| **Improvement** | **—** | **~21× faster** |

### Batch Mode (SIMD)

PWMA batch can vectorize prefix-sum cascades:

| Operation | Scalar Ops (512 bars) | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| S1 prefix sum | 512 | 64 | 8× |
| S2 cascaded sum | 1024 | 128 | 8× |
| S3 cascaded sum | 1536 | 192 | 8× |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches mathematical definition exactly |
| **Timeliness** | 9/10 | Very fast reaction to new data (heavy recent weighting) |
| **Overshoot** | 3/10 | Parabolic weighting can cause overshoot |
| **Smoothness** | 4/10 | Sensitive to recent noise |

## Validation

Validated against Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Ooples** | ✅ | Matches `CalculateParabolicWeightedMovingAverage` |
| **Skender** | N/A | Not implemented |
| **TA-Lib** | N/A | Not implemented |

| **Tulip** | N/A | Not implemented. |

## C# Implementation Considerations

QuanTAlib's PWMA uses triple cascading sums to achieve O(1) streaming updates. The implementation demonstrates several high-performance patterns:

### State Management

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(
    double Sum,      // Running sum (S1)
    double WSum,     // Weighted sum (S2)
    double PSum,     // Parabolic sum (S3)
    double LastInput,
    double LastValidValue,
    int TickCount)
```

The state captures all three cascading sums needed for the O(1) update formula. `TickCount` tracks iterations for periodic resync.

### Key Optimizations

| Technique | Implementation | Benefit |
| :--- | :--- | :--- |
| **Precomputed divisor** | `_divisor = period * (period + 1.0) * (2.0 * period + 1.0) / 6.0` | Eliminates division in hot path |
| **FMA cascade** | All three sum updates use `FusedMultiplyAdd` | Hardware-accelerated multiply-add |
| **Dual buffer** | `_buffer` + `_p_buffer` for bar correction | O(1) state restoration on `isNew=false` |
| **Periodic resync** | Full recalculation every 1000 ticks | Bounds floating-point drift |
| **stackalloc** | Batch `Calculate` uses stack for period ≤ 512 | Zero heap allocation |

### FMA in Cascade Updates

The cascading sum formulas map directly to FMA operations:

```csharp
// S1 update (simple running sum)
double newSum = _state.Sum - oldest + newest;

// S2 update: S2_new = S2_old - S1_old + N×newest
double newWSum = Math.FusedMultiplyAdd(period, newest, _state.WSum - _state.Sum);

// S3 update: S3_new = S3_old - 2×S2_old + S1_old + N²×newest
double newPSum = Math.FusedMultiplyAdd(period * period, newest,
    _state.PSum - 2 * oldWSum + oldSum);
```

### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `Sum` | double | 8 bytes | Running sum S1 |
| `WSum` | double | 8 bytes | Weighted sum S2 |
| `PSum` | double | 8 bytes | Parabolic sum S3 |
| `LastInput` | double | 8 bytes | Previous input value |
| `LastValidValue` | double | 8 bytes | NaN substitution |
| `TickCount` | int | 4 bytes | Resync counter |
| **State total** | | **44 bytes** | Compiler-aligned |
| `_buffer` | RingBuffer | 24 + 8N | Sliding window |
| `_p_buffer` | RingBuffer | 24 + 8N | Bar correction backup |

### Bar Correction Pattern

```csharp
if (isNew)
{
    _p_state = _state;           // Snapshot for rollback
    _p_buffer.CopyFrom(_buffer); // Buffer snapshot
}
else
{
    _state = _p_state;           // Restore previous state
    _buffer.CopyFrom(_p_buffer); // Restore buffer
}
```

### Periodic Resync

Triple cascading sums accumulate floating-point errors faster than simple running sums. The implementation resyncs every 1000 ticks:

```csharp
if (_state.TickCount >= 1000)
{
    // Full O(N) recalculation to reset drift
    RecalculateFromBuffer();
    _state = _state with { TickCount = 0 };
}
```

### Common Pitfalls

1. **Resync**: Because triple running sums are used, floating-point errors can accumulate faster than in a simple SMA. The implementation automatically resyncs every 1000 ticks to maintain precision.
2. **Sensitivity**: This indicator is very sensitive to the most recent bar. It can "repaint" visually if used on an open bar (though the math is consistent).
