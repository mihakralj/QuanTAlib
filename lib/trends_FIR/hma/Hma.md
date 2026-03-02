# HMA: Hull Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Hma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period + sqrtPeriod - 1` bars                          |
| **Signature**    | [hma_signature](hma_signature) |

### TL;DR

- HMA (Hull Moving Average) is a solution to the eternal struggle between smoothness and lag.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period + sqrtPeriod - 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Alan Hull looked at the lag in moving averages and said, 'I can fix that.' And he did, by making the math do gymnastics."

HMA (Hull Moving Average) is a solution to the eternal struggle between smoothness and lag. Most indicators force you to choose one; HMA gives you both. It achieves this by using weighted moving averages (WMAs) in a clever configuration that cancels out lag while maintaining the smoothing properties of the WMA.

## Historical Context

Developed by Alan Hull in 2005, the HMA was designed to be "responsive, accurate, and smooth." Hull realized that lag is essentially a function of the period, and by combining averages of different periods (specifically, a full period and a half period), he could mathematically offset the lag.

## Architecture & Physics

The HMA is built from three Weighted Moving Averages (WMAs):

1. **WMA(n/2)**: A fast WMA of half the period.
2. **WMA(n)**: A slow WMA of the full period.
3. **WMA(sqrt(n))**: A smoothing WMA applied to the difference.

The core logic is: $2 \times \text{WMA}(n/2) - \text{WMA}(n)$.
This operation "over-weights" the recent data, pushing the average forward to align with the current price. The final WMA smooths out the resulting noise.

## Mathematical Foundation

$$ \text{Raw} = 2 \times \text{WMA}(P, \frac{N}{2}) - \text{WMA}(P, N) $$

$$ \text{HMA} = \text{WMA}(\text{Raw}, \sqrt{N}) $$

Where $N$ is the period.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

HMA chains three WMA instances. Each WMA is O(1) with ~22 cycles (see WMA.md).

| Component | Operations | Cost (cycles) |
| :--- | :--- | :---: |
| WMA(N/2) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| WMA(N) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| Combiner: 2×WMA₁ - WMA₂ | 1 MUL, 1 SUB | ~4 |
| WMA(√N) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| **Total** | **~18 ops** | **~70 cycles** |

**Hot path breakdown:**
- `Raw = 2 × WMA(n/2) - WMA(n)`: 1 MUL + 1 SUB
- Three independent WMA updates execute in sequence
- Each WMA uses O(1) dual running-sum algorithm

### Batch Mode (SIMD)

Each WMA component benefits from SIMD prefix-sum optimization:

| Component | Scalar (512 bars) | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| WMA(N/2) batch | ~11K cycles | ~3K cycles | ~4× |
| WMA(N) batch | ~11K cycles | ~3K cycles | ~4× |
| Combiner | ~2K cycles | ~250 cycles | ~8× |
| WMA(√N) batch | ~11K cycles | ~3K cycles | ~4× |
| **Total** | **~35K** | **~9K** | **~4×** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches Skender, Tulip exactly |
| **Timeliness** | 9/10 | Lag-compensated design; very responsive |
| **Overshoot** | 4/10 | Can overshoot on sharp reversals (algebraic correction side effect) |
| **Smoothness** | 6/10 | Final √N smoothing moderates noise |

### Zero-Allocation Design

HMA is implemented by chaining three `Wma` instances. Since `Wma` is zero-allocation, HMA inherits this property.

## Validation

Validated against Skender, Tulip, and Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetHma`. |
| **Tulip** | ✅ | Matches `hma`. |
| **Ooples** | ✅ | Matches `CalculateHullMovingAverage` (with rounding caveats). |
| **TA-Lib** | ❌ | Not implemented. |

### External Library Discrepancies

**OoplesFinance.StockIndicators**:
Discrepancies exist due to different rounding methods for integer periods.

* **QuanTAlib**: Uses integer truncation (floor) for $N/2$ and $\sqrt{N}$.
* **Ooples**: Uses `Math.Round` (nearest integer).

This results in different effective periods for $N=14$ ($\sqrt{14} \approx 3.74 \to 3$ vs $4$) and others where the fractional part $\ge 0.5$. Validation tests match exactly for periods where rounding logic aligns (e.g., $N=9, 20, 50$).

## C# Implementation Considerations

### Compositional Architecture

HMA composes three independent `Wma` instances rather than implementing custom logic:

```csharp
_wmaFull = new Wma(period);
_wmaHalf = new Wma(halfPeriod);
_wmaSqrt = new Wma(_sqrtPeriod);
```

This leverages WMA's optimized O(1) implementation for each component, maintaining the zero-allocation property.

### Streaming Update Pipeline

The hot path chains the three WMA updates with minimal intermediate allocation:

```csharp
TValue full = _wmaFull.Update(input, isNew);
TValue half = _wmaHalf.Update(input, isNew);
double intermediate = (2.0 * half.Value) - full.Value;
Last = _wmaSqrt.Update(new TValue(input.Time, intermediate), isNew);
```

The intermediate value computation uses scalar arithmetic—no buffer required.

### ArrayPool for Batch Processing

The static `Calculate` method rents arrays from `ArrayPool<double>` for temporary storage:

```csharp
double[] rentedFull = System.Buffers.ArrayPool<double>.Shared.Rent(len);
double[] rentedHalf = System.Buffers.ArrayPool<double>.Shared.Rent(len);
try
{
    Wma.Batch(source, fullWma, period);
    Wma.Batch(source, halfWma, halfPeriod);
    CalculateIntermediate(halfWma, fullWma, intermediate);
    Wma.Batch(intermediate, output, sqrtPeriod);
}
finally
{
    System.Buffers.ArrayPool<double>.Shared.Return(rentedFull);
    System.Buffers.ArrayPool<double>.Shared.Return(rentedHalf);
}
```

Buffer reuse: the `halfWma` array doubles as the `intermediate` buffer since values are consumed before being overwritten.

### SIMD-Accelerated Intermediate Calculation

The combiner step $2 \times \text{WMA}_{half} - \text{WMA}_{full}$ is fully vectorized:

```csharp
if (Avx512F.IsSupported && len >= Vector512<double>.Count)
{
    var vTwo = Vector512.Create(2.0);
    var vResult = Avx512F.Subtract(Avx512F.Multiply(vHalf, vTwo), vFull);
}
else if (Avx2.IsSupported && len >= Vector256<double>.Count)
{
    var vTwo = Vector256.Create(2.0);
    var vResult = Avx.Subtract(Avx.Multiply(vHalf, vTwo), vFull);
}
else if (AdvSimd.Arm64.IsSupported && len >= Vector128<double>.Count)
{
    var vTwo = Vector128.Create(2.0);
    var vResult = AdvSimd.Arm64.Subtract(AdvSimd.Arm64.Multiply(vHalf, vTwo), vFull);
}
```

This achieves 8× throughput on AVX-512, 4× on AVX2, and 2× on NEON.

### Unsafe Memory Access

Direct memory references eliminate bounds checking in the SIMD loops:

```csharp
ref double halfRef = ref MemoryMarshal.GetReference(halfWma);
ref double fullRef = ref MemoryMarshal.GetReference(fullWma);
ref double outRef = ref MemoryMarshal.GetReference(output);

var vHalf = Vector512.LoadUnsafe(ref Unsafe.Add(ref halfRef, i));
```

### State Replay for TSeries Update

After batch calculation, streaming state is restored by replaying the trailing window:

```csharp
int lookback = _period + _sqrtPeriod + 10;
int startIndex = Math.Max(0, len - lookback);
for (int i = startIndex; i < len; i++)
{
    Update(new TValue(source.Times[i], source.Values[i]));
}
```

This ensures subsequent streaming updates produce correct results after a batch operation.

### Integer Period Truncation

Periods use integer truncation (not rounding) for consistent behavior:

```csharp
int halfPeriod = period / 2;
_sqrtPeriod = (int)Math.Sqrt(period);
```

This differs from some implementations that use `Math.Round`, affecting results for certain periods.

### Memory Layout

| Component | Size | Purpose |
| :--- | :--- | :--- |
| `_wmaFull` | ~152 + 8×period bytes | Full-period WMA |
| `_wmaHalf` | ~152 + 4×period bytes | Half-period WMA |
| `_wmaSqrt` | ~152 + 8×√period bytes | Smoothing WMA |
| Scalars | ~32 bytes | Period values, sample count |
| **Total** | **~488 + 12N + 8√N bytes** | Per-instance footprint |

For HMA(100), total memory is approximately 1.7 KB per instance (three WMA instances combined).

### Common Pitfalls

1. **Overshoot**: Like DEMA, HMA can overshoot price turns because of the lag correction.
2. **Period Sensitivity**: The $\sqrt{N}$ smoothing is hardcoded into the definition. You can't easily tweak the smoothing independently of the lag correction without breaking the "Hull" definition.
3. **Integer Math**: The periods $N/2$ and $\sqrt{N}$ are rounded to integers. This can cause slight discrepancies between implementations depending on rounding rules. Standard integer truncation is used in QuanTAlib.
