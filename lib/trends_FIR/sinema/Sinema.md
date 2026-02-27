# SINEMA: Sine-Weighted Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Sinema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- The Sine-Weighted Moving Average (SINEMA) applies sine-wave weighting to data points within the lookback window.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Nature doesn't do straight lines, and neither should your weights."

The Sine-Weighted Moving Average (SINEMA) applies sine-wave weighting to data points within the lookback window. Weights follow the formula $w_i = \sin(\pi \cdot (i+1) / N)$, creating a smooth bell-shaped distribution that emphasizes middle values while gracefully tapering at the edges. Unlike SMA's uniform weighting or WMA's linear ramp, sine weighting provides a natural transition that reduces high-frequency noise while preserving mid-frequency trends.

## Historical Context

Sine-weighted smoothing emerges from signal processing, where windowing functions shape the frequency response of filters. The sine window (also called the cosine window when phase-shifted) is a member of the generalized cosine window family. Its application to financial moving averages provides a middle ground between the harsh cutoff of rectangular windows (SMA) and the aggressive center-weighting of triangular windows (TRIMA).

## Architecture & Physics

### 1. Weight Calculation

For a period $N$, the weight at position $i$ (0-indexed) is:

$$
w_i = \sin\left(\frac{\pi \cdot (i+1)}{N}\right)
$$

This produces a half-sine wave: weights start small, peak at the center, and taper back down. For period 5: weights ≈ [0.588, 0.951, 1.0, 0.951, 0.588].

### 2. Normalization

The weighted average normalizes by the sum of weights:

$$
\text{SINEMA}_t = \frac{\sum_{i=0}^{N-1} P_{t-i} \cdot w_i}{\sum_{i=0}^{N-1} w_i}
$$

### 3. Warmup Adaptation

During warmup (fewer than $N$ values), weights are recalculated for the current buffer size $k$:

$$
w_i^{(k)} = \sin\left(\frac{\pi \cdot (i+1)}{k}\right)
$$

This ensures smooth output from the first bar rather than waiting for a full window.

## Mathematical Foundation

### Weight Distribution

The sine weight function produces:
- **Symmetric weighting**: Equal emphasis on equidistant past values
- **Smooth edges**: No abrupt transitions at window boundaries
- **Peak at center**: Maximum weight at position $\lfloor N/2 \rfloor$

### Frequency Response

As an FIR filter, SINEMA has linear phase response (no phase distortion) but $O(N)$ complexity per bar in streaming mode. The sine window provides moderate side-lobe suppression (~23 dB), better than rectangular (SMA) but less than Hamming or Blackman windows.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD | N | 1 | N |
| MUL | N | 3 | 3N |
| DIV | 1 | 15 | 15 |
| **Total** | **2N+1** | — | **~4N+15 cycles** |

Pre-calculated weights eliminate `sin()` calls in steady state.

### Batch Mode (SIMD)

The batch calculation uses `stackalloc` for buffers ≤256 elements and `ArrayPool` for larger periods. SIMD vectorization is limited due to the weighted sum's data dependency, but memory locality is optimized.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact weighted mean calculation |
| **Timeliness** | 4/10 | Moderate lag (~N/3 due to center weighting) |
| **Overshoot** | 0/10 | Never exceeds input data range |
| **Smoothness** | 7/10 | Smoother than SMA; less prone to drop-off jumps |

## Validation

SINEMA is not implemented in standard technical analysis libraries.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation matches |

Validation tests verify:
- Sine weight mathematical correctness
- Constant input produces constant output
- Batch/Streaming/Span mode consistency
- Output bounded by input range
- Warmup weight adaptation

## C# Implementation Considerations

QuanTAlib's SINEMA uses precomputed sine weights with O(N) convolution. The implementation demonstrates several high-performance patterns:

### State Management

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(double LastValidValue);
private State _state;
private State _p_state;
```

Minimal state—only the last valid value needs tracking since weights are precomputed and buffer handles windowing.

### Key Optimizations

| Technique | Implementation | Benefit |
| :--- | :--- | :--- |
| **Precomputed weights** | `_weights[]` array at construction | Eliminates `sin()` calls in hot path |
| **Cached weight sum** | `_weightSum` stored at construction | Division uses constant denominator |
| **ArrayPool hybrid** | stackalloc ≤256, ArrayPool >256 | Zero allocation for typical periods |
| **RingBuffer** | `UpdateNewest` for bar correction | O(1) correction without buffer copy |
| **Adaptive warmup** | Recalculates weights for partial buffer | Valid output from first bar |

### Constructor Weight Precomputation

```csharp
public Sinema(int period)
{
    _weights = new double[period];
    double sum = 0;
    for (int i = 0; i < period; i++)
    {
        _weights[i] = Math.Sin(Math.PI * (i + 1) / period);
        sum += _weights[i];
    }
    _weightSum = sum;
}
```

All `sin()` calls happen once at construction, not per-update.

### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_period` | int | 4 bytes | Window size |
| `_weights` | double[] | 8N bytes | Precomputed sine weights |
| `_weightSum` | double | 8 bytes | Cached Σw for normalization |
| `_buffer` | RingBuffer | 24 + 8N | Sliding window |
| `_state.LastValidValue` | double | 8 bytes | NaN substitution |
| `_p_state.LastValidValue` | double | 8 bytes | Bar correction backup |
| **Instance total** | | **~52 + 16N bytes** | N = period |

### Bar Correction Pattern

```csharp
if (isNew)
{
    _p_state = _state;
    _buffer.Add(val);
}
else
{
    _state = _p_state;
    _buffer.UpdateNewest(val);
}
```

Simple state backup; RingBuffer's `UpdateNewest` handles in-place modification.

### Batch Processing Memory Strategy

```csharp
const int StackAllocThreshold = 256;
double[]? rentedBuffer = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;
double[]? rentedWeights = period > StackAllocThreshold ? ArrayPool<double>.Shared.Rent(period) : null;

Span<double> buffer = rentedBuffer != null
    ? rentedBuffer.AsSpan(0, period)
    : stackalloc double[period];

try { /* process */ }
finally
{
    if (rentedBuffer != null) ArrayPool<double>.Shared.Return(rentedBuffer);
    if (rentedWeights != null) ArrayPool<double>.Shared.Return(rentedWeights);
}
```

### Warmup Weight Adaptation

During warmup, weights are dynamically recalculated for the partial buffer:

```csharp
if (count < _period)
{
    for (int j = 0; j < count; j++)
    {
        double w = Math.Sin(Math.PI * (j + 1) / count);
        sum += buffer[j] * w;
        weightSum += w;
    }
}
```

This produces valid, smooth output from bar 1 without waiting for a full window.

## Common Pitfalls

1. **O(N) Complexity**: Unlike SMA's O(1) running sum, SINEMA requires O(N) operations per bar. For very long periods (>500), consider whether the smoothness benefits justify the cost.

2. **Warmup Behavior**: The adaptive warmup recalculates weights for partial buffers. This produces valid output from bar 1 but with different effective weighting than steady state.

3. **Weight Pre-calculation**: Weights are computed once at construction. Changing the period requires a new indicator instance.

4. **NaN Propagation**: A single NaN in the window corrupts the result. QuanTAlib substitutes the last valid value to prevent this.

5. **Memory**: Each instance stores a pre-calculated weight array of size $N$. For many concurrent indicators with large periods, memory adds up.

## References

- Harris, F. J. (1978). "On the use of windows for harmonic analysis with the discrete Fourier transform." *Proceedings of the IEEE*, 66(1), 51-83.
- Oppenheim, A. V., & Schafer, R. W. (2010). *Discrete-Time Signal Processing* (3rd ed.). Pearson.
