# WMA: Weighted Moving Average

> *Because yesterday matters more than last Tuesday. WMA is the linear answer to the question: 'What have you done for me lately?'*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Wma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [wma.pine](wma.pine)                       |
| **Signature**    | [wma_signature](wma_signature.md) |

- The Weighted Moving Average (WMA) assigns a linearly decreasing weight to data points.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Weighted Moving Average (WMA) assigns a linearly decreasing weight to data points. The most recent price gets weight $N$, the one before it $N-1$, down to 1. This makes it more responsive to recent price changes than an SMA, but without the infinite tail of an EMA.

## Historical Context

WMA is the "finite impulse response" (FIR) counterpart to the EMA. It was developed to reduce the lag of the SMA while maintaining a finite window of influence.

## Architecture & Physics

A naive WMA implementation is $O(N)$, requiring a full loop over the history window for every update. QuanTAlib uses a dual running-sum algorithm to achieve $O(1)$ complexity.

### The O(1) Algorithm

Two sums are maintained:

1. `Sum`: The simple sum of values (like SMA).
2. `WSum`: The weighted sum.

$$ WSum_{new} = WSum_{old} - Sum_{old} + (N \times Price_{new}) $$
$$ Sum_{new} = Sum_{old} - Price_{oldest} + Price_{new} $$

This allows calculating a WMA(1000) as fast as a WMA(10).

### SIMD Optimization

For batch processing, `Wma.Batch` uses advanced vectorization (AVX2/AVX-512/Neon). It computes prefix sums and weighted updates in parallel, achieving throughputs that scalar code cannot touch.

## Mathematical Foundation

### 1. The Formula

$$ WMA = \frac{\sum_{i=0}^{N-1} (N-i) \times P_{t-i}}{\frac{N(N+1)}{2}} $$

The denominator is the sum of the weights (triangular number).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

The O(1) algorithm eliminates the $O(N)$ weighted sum on each bar:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 4 | 1 | 4 |
| MUL | 1 | 3 | 3 |
| DIV | 1 | 15 | 15 |
| **Total** | **6** | — | **~22 cycles** |

**Hot path breakdown:**
- `WSum_new = WSum_old - Sum_old + (N × Price_new)`: 2 SUB + 1 MUL
- `Sum_new = Sum_old - Price_oldest + Price_new`: 2 SUB
- `WMA = WSum / divisor`: 1 DIV (divisor is precomputed constant)

**Comparison with naive O(N) implementation:**

| Mode | Complexity | Cycles (Period=100) |
| :--- | :---: | :---: |
| Naive (recalculate) | O(N) | ~400 cycles |
| QuanTAlib O(1) | O(1) | ~22 cycles |
| **Improvement** | **—** | **~18× faster** |

### Batch Mode (SIMD/FMA)

WMA batch uses prefix sums for both `Sum` and `WSum`, enabling vectorization:

| Operation | Scalar Ops (512 bars) | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Prefix sum (Sum) | 512 | 64 | 8× |
| Weighted prefix sum | 512 | 64 | 8× |
| Final divisions | 512 | 64 | 8× |

The batch path achieves near-linear scaling for large datasets.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib, Skender, Tulip exactly |
| **Timeliness** | 6/10 | Linear weighting improves responsiveness over SMA |
| **Overshoot** | 10/10 | Never overshoots input data range (FIR property) |
| **Smoothness** | 4/10 | Less smooth than SMA; follows price closely |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_WMA` exactly. |
| **Skender** | ✅ | Matches `GetWma` exactly. |
| **Tulip** | ✅ | Matches `wma` exactly. |
| **Ooples** | ✅ | Matches `CalculateWeightedMovingAverage`. |

## C# Implementation Considerations

### Dual Running-Sum O(1) Algorithm

The implementation maintains both a simple `Sum` and a weighted `WSum` for O(1) updates. The elegant recurrence relation avoids recalculating weights:

```csharp
double oldSum = _state.Sum;
double oldest = _buffer.Oldest;
_state.Sum = Math.FusedMultiplyAdd(-1.0, oldest, _state.Sum + val);
_state.WSum = Math.FusedMultiplyAdd(-1.0, oldSum, _state.WSum + _period * val);
```

Using `FusedMultiplyAdd` for combined add-subtract operations improves numerical precision.

### State Record Struct with Auto Layout

State is captured for efficient bar correction:

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(double Sum, double WSum, double LastInput, 
                            double LastValidValue, int TickCount, bool HasSeenValidData);
```

The `LayoutKind.Auto` allows the JIT to optimize field arrangement for cache efficiency.

### Periodic Resync for Drift Correction

With dual running sums, drift accumulates faster than SMA. The implementation resyncs every 10,000 ticks:

```csharp
if (needResync || (isNaN && double.IsFinite(val)))
{
    double recalcSum = 0;
    double recalcWsum = 0;
    int weight = 1;
    foreach (double item in _buffer)
    {
        recalcSum += item;
        recalcWsum = Math.FusedMultiplyAdd(weight, item, recalcWsum);
        weight++;
    }
    _state.Sum = recalcSum;
    _state.WSum = recalcWsum;
}
```

### Multi-Architecture SIMD with Prefix Sums

The static `Batch` method dispatches to architecture-specific implementations:

```csharp
if (Avx512F.IsSupported && len >= simdThreshold && !source.ContainsNonFinite())
    CalculateAvx512Core(source, output, period);
else if (Avx2.IsSupported && len >= simdThreshold && !source.ContainsNonFinite())
    CalculateSimdCore(source, output, period);
else if (AdvSimd.Arm64.IsSupported && len >= simdThreshold && !source.ContainsNonFinite())
    CalculateNeonCore(source, output, period);
else
    CalculateScalarCore(source, output, period);
```

### AVX-512 Vectorized Prefix Sums

The AVX-512 path uses pre-computed shuffle indices and masks for efficient prefix-sum computation:

```csharp
private static readonly Vector512<long> V512Idx1 = Vector512.Create(0L, 0, 1, 2, 3, 4, 5, 6);
private static readonly Vector512<double> V512Mask1 = Vector512.Create(0.0, 1, 1, 1, 1, 1, 1, 1);
```

The weighted sum update uses `FusedMultiplySubtract` for the formula $U_i = N \times P_i - S_{i-1}$:

```csharp
var vU = Avx512F.FusedMultiplySubtract(vPeriod, vNew, vSumsShifted);
```

### AVX2 Loop Unrolling

The AVX2 path processes 8 elements per iteration (2 vectors of 4 doubles) for improved throughput:

```csharp
for (; idx <= unrolledSync; idx += 2 * vectorWidth)
{
    var vNew1 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx));
    var vNew2 = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, idx + vectorWidth));
    // Process both vectors simultaneously
}
```

This maximizes instruction-level parallelism by overlapping independent operations.

### NEON ARM64 Implementation

The ARM64 NEON path processes 2 doubles at a time with explicit scalar prefix-sum accumulation:

```csharp
double u0 = Math.FusedMultiplyAdd(period, vNew.GetElement(0), -sumState);
double u1 = Math.FusedMultiplyAdd(period, vNew.GetElement(1), -ps0);
```

### NaN Handling with Fallback Tracking

Non-finite inputs are replaced with the last valid value, with explicit tracking for first-value edge case:

```csharp
public double DefaultLastValidValue { get; set; } = double.NaN;

private double GetValidValue(double input)
{
    if (double.IsFinite(input))
    {
        _state.LastValidValue = input;
        _state.HasSeenValidData = true;
        return input;
    }
    return _state.HasSeenValidData ? _state.LastValidValue : DefaultLastValidValue;
}
```

### Stackalloc for Scalar Batch Buffer

The scalar path uses `stackalloc` for small periods to avoid heap allocation:

```csharp
Span<double> buffer = period <= 512 ? stackalloc double[period] : new double[period];
```

### Memory Layout

| Component | Size | Purpose |
| :--- | :--- | :--- |
| `_buffer` (RingBuffer) | 32 + 8×period bytes | Sliding window history |
| `_state` | ~48 bytes | Sum, WSum, LastInput, LastValidValue, TickCount, flags |
| `_pState` | ~48 bytes | Previous state for rollback |
| Scalars | ~24 bytes | Period, divisor, source reference |
| **Total** | **~152 + 8N bytes** | Per-instance footprint |

For WMA(200), total memory is approximately 1.75 KB per instance.

### Common Pitfalls

1. **Drift**: Like SMA, the O(1) algorithm is susceptible to floating-point drift. QuanTAlib resets the sums every 10,000 ticks to guarantee accuracy.
2. **Aggressiveness**: WMA reacts faster than SMA but can be "twitchy." It is often used as a component in other indicators (e.g., HMA) rather than a standalone trend filter.
3. **Weights**: Users sometimes confuse WMA (linear weights) with EMA (exponential weights) or VWAP (volume weights).
