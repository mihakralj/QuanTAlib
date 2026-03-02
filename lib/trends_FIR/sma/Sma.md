# SMA: Simple Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Sma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [sma_signature](sma_signature) |

### TL;DR

- The Simple Moving Average (SMA) is the unweighted arithmetic mean of the last $N$ data points.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The vanilla ice cream of technical analysis. Boring, ubiquitous, and the only thing your grandfather and your high-frequency trading bot agree on."

The Simple Moving Average (SMA) is the unweighted arithmetic mean of the last $N$ data points. It acts as a low-pass filter, smoothing out high-frequency noise to reveal the underlying trend. While conceptually simple, efficient implementation on modern hardware requires careful attention to memory access patterns and vectorization.

## Historical Context

The concept of a moving average dates back to 1901 (R.H. Hooker) for smoothing weather data, but it became a staple of financial analysis in the mid-20th century. It is the baseline against which all other averages are compared.

## Architecture & Physics

The naive implementation of SMA sums $N$ numbers at every step, resulting in $O(N)$ complexity. QuanTAlib uses an optimized $O(1)$ approach.

### O(1) Running Sum

A running `Sum` and a `RingBuffer` of history are maintained.
$$ Sum_{new} = Sum_{old} - Value_{oldest} + Value_{new} $$
$$ SMA = \frac{Sum_{new}}{N} $$

This ensures that calculating an SMA(200) takes the exact same time as an SMA(10).

### Drift Correction

Floating-point addition is not associative. Repeatedly adding and subtracting values from a running sum introduces cumulative error (drift) over millions of ticks. QuanTAlib implements a periodic **Resync** mechanism (every 1000 ticks) that recalculates the sum from scratch to ensure precision remains within `1e-9` of the true mean.

### SIMD Optimization

For batch processing of large datasets, `Sma.Batch` utilizes `System.Runtime.Intrinsics` (AVX2/AVX-512) to process multiple data points in parallel, significantly outperforming scalar loops.

## Mathematical Foundation

### 1. The Mean

$$ SMA_t = \frac{1}{N} \sum_{i=0}^{N-1} P_{t-i} $$

## Performance Profile

### Operation Count (Streaming Mode, O(1) Running Sum)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (Sum - oldest) | 1 | 1 | 1 |
| ADD (Sum + newest) | 1 | 1 | 1 |
| DIV (Sum / N) | 1 | 15 | 15 |
| **Total (hot)** | **3** | — | **~17 cycles** |

Every 1000 bars, a resync recalculates the sum to prevent drift:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD (N values) | N | 1 | N |
| DIV (Sum / N) | 1 | 15 | 15 |
| **Resync cost** | **N+1** | — | **~N+15 cycles** |

**Amortized cost:** ~17 + (N+15)/1000 ≈ **~17 cycles/bar** for typical use.

### Batch Mode (SIMD Analysis)

SMA batch processing is highly vectorizable using running sum + prefix sum techniques:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Initial N-sum | N | N/8 | 8× |
| Running update (per bar) | 3 | ~1 | ~3× |
| Division | 1 | 1/8 (batched) | 8× |

For 512 bars:

| Mode | Cycles/bar | Total | Notes |
| :--- | :---: | :---: | :--- |
| Scalar streaming | ~17 | ~8,700 | O(1) per bar |
| SIMD batch | ~3 | ~1,500 | Vectorized running sum |
| **Improvement** | **5.8×** | — | Batch wins for large N |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic mean |
| **Timeliness** | 3/10 | Significant lag (~N/2 bars) |
| **Overshoot** | 10/10 | Never overshoots input range |
| **Smoothness** | 5/10 | Smooth but susceptible to drop-off jumps |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~100M bars/sec | SIMD batch mode |
| **Allocations** | 0 bytes | Zero-allocation in hot paths |
| **Complexity** | O(1) | Constant time regardless of period N |
| **State Size** | 8 + 8N bytes | Sum + RingBuffer |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_SMA` exactly. |
| **Skender** | ✅ | Matches `GetSma` exactly. |
| **Tulip** | ✅ | Matches `sma` exactly. |
| **Ooples** | ✅ | Matches `CalculateSimpleMovingAverage`. |

## C# Implementation Considerations

### RingBuffer for O(1) Running Sum

The implementation maintains a `RingBuffer` of the most recent $N$ values alongside a running `Sum`. On each update, the oldest value is subtracted and the newest added—eliminating the need to iterate over the entire window:

```csharp
Sum = Math.FusedMultiplyAdd(-_buffer[^1], 1, Sum + p);
_buffer.Add(p, isNew);
```

Using `FusedMultiplyAdd` for the combined subtraction/addition improves numerical stability compared to separate operations.

### State Record Struct

Minimal state is captured in a `record struct` for efficient bar correction:

```csharp
private record struct State(double Sum, double LastValidValue, int TickCount);
```

When `isNew=false`, the implementation restores `_p_state` to revert any partial calculation—enabling accurate bar correction when the same timestamp updates multiple times.

### Periodic Resync for Drift Correction

Floating-point drift accumulates over millions of additions/subtractions. The implementation resyncs every 1000 ticks:

```csharp
if (_state.TickCount >= ResyncPeriod)
{
    _state = _state with { Sum = _buffer.Span.Sum(), TickCount = 0 };
}
```

This bounds cumulative error to within `1e-9` of true mean regardless of stream length.

### Multi-Architecture SIMD Implementation

The static `Calculate` method dispatches to architecture-specific implementations:

```csharp
if (Avx512F.IsSupported) CalculateAvx512Core(source, output, period);
else if (Avx2.IsSupported) CalculateAvx2Core(source, output, period);
else if (AdvSimd.Arm64.IsSupported) CalculateNeonCore(source, output, period);
else CalculateScalarCore(source, output, period);
```

- **AVX-512**: Processes 8 doubles simultaneously with 512-bit vectors
- **AVX2**: Processes 4 doubles with 256-bit vectors
- **NEON (ARM64)**: Processes 2 doubles with 128-bit vectors
- **Scalar fallback**: Portable loop for unsupported architectures

### Prefix-Sum Vectorization

For batch processing, the SIMD paths use a prefix-sum technique that enables parallel computation of running sums. The initial window sum is computed with vectorized horizontal addition, then subsequent values use the optimized running-sum pattern.

### ArrayPool for Memory Efficiency

Large period buffers are rented from `ArrayPool<double>` rather than allocated, reducing GC pressure during batch operations. Combined with `stackalloc` for small intermediate buffers, this achieves zero-allocation in hot paths.

### NaN Handling with Last-Valid Substitution

Non-finite inputs are replaced with the last valid value stored in state:

```csharp
p = double.IsFinite(p) ? p : _state.LastValidValue;
```

This prevents NaN propagation through the running sum without requiring expensive validation on every buffer access.

### Memory Layout

| Component | Size | Purpose |
| :--- | :--- | :--- |
| `_buffer` (RingBuffer) | 32 + 8×period bytes | Sliding window history |
| `_state` | ~24 bytes | Sum, LastValidValue, TickCount |
| `_p_state` | ~24 bytes | Previous state for rollback |
| Scalars | ~16 bytes | Period, reciprocal |
| **Total** | **~96 + 8N bytes** | Per-instance footprint |

For SMA(200), total memory is approximately 1.7 KB per instance.

### Common Pitfalls

1. **Lag**: SMA has the most lag of all moving averages (Lag $\approx N/2$).
2. **Drop-off Effect**: An old, large outlier dropping out of the window causes the SMA to jump, even if the current price is flat. This "Barker effect" is why EMAs are often preferred.
3. **NaN Handling**: A single `NaN` in the history window corrupts the entire SMA. QuanTAlib handles this by substituting the last valid value.
