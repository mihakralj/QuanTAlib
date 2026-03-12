# HAMMA: Hamming-Weighted Moving Average

> *Julius von Hann picked his window function to suppress spectral leakage; we're just using it to smooth price data. Same math, different trading floor.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 10)                      |
| **Outputs**      | Single series (Hamma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [hamma.pine](hamma.pine)                       |
| **Signature**    | [hamma_signature](hamma_signature.md) |

- HAMMA is a Finite Impulse Response (FIR) filter that applies a Hamming window to price data.
- Parameterized by `period` (default 10).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HAMMA is a Finite Impulse Response (FIR) filter that applies a Hamming window to price data. The Hamming window is a raised cosine with specific coefficients (0.54 and 0.46) chosen to minimize the amplitude of the first side lobe in the frequency domain. This makes it particularly effective at separating the signal (trend) from nearby noise frequencies.

## Historical Context

Richard Hamming developed his eponymous window function at Bell Labs in 1977, though it built on earlier work by Julius von Hann (the "Hanning" window, often confused with Hamming). The Hamming window was designed specifically to address spectral leakage in discrete Fourier transforms.

The key insight was that by tweaking the coefficients of the raised cosine window, you could minimize the first side lobe amplitude at the cost of slightly wider main lobe. The result is a window that's excellent at isolating a signal from nearby interfering frequencies—exactly what traders want when separating trend from noise.

In trading applications, HAMMA provides smoother output than SMA while maintaining good responsiveness. Its symmetric weighting gives equal consideration to recent and older prices around the center of the window.

## Architecture & Physics

HAMMA is a weighted moving average where weights follow the Hamming function:

$$ w_i = 0.54 - 0.46 \cdot \cos\left(\frac{2\pi i}{N-1}\right) $$

The physics of HAMMA reveal several key properties:

* **Symmetric weighting**: Center weight is 1.0, edge weights are 0.08
* **First side lobe at -43 dB**: Much better side lobe suppression than rectangular (SMA) or Hanning windows
* **Moderate main lobe width**: Trades some frequency resolution for side lobe suppression
* **Zero phase distortion**: Symmetric filter means no group delay asymmetry

The 0.54/0.46 coefficients are specifically chosen to cancel the first side lobe. Other windows (like Hanning with 0.5/0.5) don't achieve this cancellation, resulting in higher side lobes.

### The Compute Challenge

Like other FIR filters, naive implementations recalculate weights on every tick. QuanTAlib precomputes the weight vector $\mathbf{W}$ upon initialization. Runtime becomes a dot product of the price buffer and weight vector.

$$ \text{Runtime Cost} = O(N) \text{ multiplications} $$

The memory locality of arrays enables SIMD vectorization, making the O(N) cost negligible for typical window sizes.

## Mathematical Foundation

The weight calculation uses the Hamming window formula:

### 1. Weight Generation

For each index $i$ from $0$ to $L-1$:

$$ w_i = 0.54 - 0.46 \cdot \cos\left(\frac{2\pi i}{L-1}\right) $$

Where $L$ is the lookback period.

### 2. Weight Properties

The Hamming coefficients produce these characteristic values:

| Position | Weight |
|----------|--------|
| Edge (i=0, i=L-1) | 0.08 |
| Center (i=(L-1)/2) | 1.00 |

### 3. Normalization

The final HAMMA value is the weighted sum divided by the total sum of weights $W_{sum}$:

$$ \text{HAMMA}_t = \frac{\sum_{i=0}^{L-1} P_{t-L+1+i} \cdot w_i}{W_{sum}} $$

### Example Calculation

For period=5:

| Index | cos(2πi/4) | Weight |
|-------|------------|--------|
| 0 | cos(0) = 1.0 | 0.54 - 0.46(1.0) = 0.08 |
| 1 | cos(π/2) = 0.0 | 0.54 - 0.46(0.0) = 0.54 |
| 2 | cos(π) = -1.0 | 0.54 - 0.46(-1.0) = 1.00 |
| 3 | cos(3π/2) = 0.0 | 0.54 - 0.46(0.0) = 0.54 |
| 4 | cos(2π) = 1.0 | 0.54 - 0.46(1.0) = 0.08 |

Note the symmetry around the center (index 2) with characteristic edge weights of 0.08.

## Performance Profile

HAMMA trades CPU cycles for excellent side lobe suppression.

### Operation Count (Streaming Mode, Scalar)

Per-bar cost for period $L$ (weights precomputed at construction):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | L | 3 | 3L |
| ADD | L | 1 | L |
| MUL (normalize) | 1 | 3 | 3 |
| **Total** | **2L+1** | — | **~4L+3 cycles** |

For a typical period of 14:
- **Total**: ~59 cycles per bar

**Constructor cost** (one-time): ~80L cycles (L cosines at ~80 cycles each + L additions)

**Complexity**: O(L) per bar — linear with period. Weights precomputed, runtime is pure dot product.

### Batch Mode (SIMD/FMA Analysis)

HAMMA's dot product structure enables efficient SIMD vectorization:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| MUL+ADD (FMA) | 2L | L/4 (FMA256) | 8× |
| Final normalize | 1 | 1 | 1× |

**Batch efficiency (512 bars, L=14):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 59 | 30,208 | — |
| SIMD batch (FMA) | ~10 | ~5,120 | **~83%** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches Hamming definition to double precision |
| **Timeliness** | 7/10 | Centered filter has inherent lag of (L-1)/2 bars |
| **Overshoot** | 10/10 | Symmetric window prevents overshoot entirely |
| **Smoothness** | 9/10 | Excellent noise suppression from -43 dB side lobes |

### Implementation Details

```csharp
// Precomputation (Constructor)
double twoPI_N1 = 2.0 * Math.PI / (period - 1);
double wSum = 0;

for (int i = 0; i < period; i++) {
    double weight = 0.54 - 0.46 * Math.Cos(i * twoPI_N1);
    _weights[i] = weight;
    wSum += weight;
}
_invWeightSum = 1.0 / wSum;

// Runtime (Update)
double sum = _buffer.DotProduct(_weights);
return sum * _invWeightSum;
```

## Comparison: Window Functions

| Window | Edge Weight | First Side Lobe | Main Lobe Width | Best For |
| :--- | :--- | :--- | :--- | :--- |
| Rectangular (SMA) | 1.0 | -13 dB | Narrowest | Maximum frequency resolution |
| Hanning | 0.0 | -31 dB | Medium | General purpose smoothing |
| **Hamming** | **0.08** | **-43 dB** | Medium | Side lobe suppression |
| Blackman | 0.0 | -58 dB | Widest | Maximum side lobe suppression |
| Gaussian | Variable | -43 dB typical | Variable | Optimal time-frequency tradeoff |

Choose HAMMA when you need better side lobe suppression than Hanning but don't want the wider main lobe of Blackman.

## Validation

QuanTAlib validates HAMMA against its mathematical definition and internal consistency checks.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against math definition. |
| **PineScript** | ✅ | Reference implementation matches. |
| **TA-Lib** | ❌ | Not included in standard C distribution. |
| **Skender** | ❌ | Not included. |
| **Tulip** | ❌ | Not included. |
| **Ooples** | ❌ | Not included. |

### C# Implementation Considerations

The QuanTAlib HAMMA implementation optimizes Hamming window convolution through precomputation and SIMD-accelerated dot products:

**Precomputed Weights with Inverse Sum**
```csharp
ComputeWeights(_weights, period, out _invWeightSum);
// ...
double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);
for (int i = 0; i < period; i++)
{
    double w = 0.54 - 0.46 * Math.Cos(twoPiOverPm1 * i);
    weights[i] = w;
    sum += w;
}
invWeightSum = 1.0 / sum;
```
Trigonometric operations computed once at construction. Normalization uses multiplication by precomputed inverse rather than division per tick.

**State Record Struct**
```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(double LastValidValue, bool IsInitialized);
private State _state;
private State _p_state;
```
Compiler optimizes field layout. The `IsInitialized` flag tracks whether valid data has been seen for proper NaN handling.

**SIMD-Accelerated Circular Buffer Dot Product**
```csharp
int part1Len = _period - head;
double sum1 = internalBuf.Slice(head, part1Len).DotProduct(_weights.AsSpan(0, part1Len));
double sum2 = internalBuf[..head].DotProduct(_weights.AsSpan(part1Len));
return (sum1 + sum2) * _invWeightSum;
```
Full buffer splits into two `DotProduct` calls to handle circular wrap. The extension leverages AVX2/FMA intrinsics when available.

**Dual Allocation Strategy for Batch**
```csharp
double[]? weightsArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
Span<double> weights = period <= 256
    ? stackalloc double[period]
    : weightsArray!.AsSpan(0, period);
```
Small periods use stack allocation; large periods use `ArrayPool` to avoid heap pressure while respecting stack limits.

**Incremental Weight Sum During Warmup**
```csharp
if (count < period)
{
    count++;
    currentWeightSum += weights[period - count];
}
```
Partial buffer normalization accumulates weight sum incrementally rather than recalculating each tick.

**Memory Layout**

| Field | Type | Size | Notes |
|:------|:-----|-----:|:------|
| `_period` | int | 4B | Window length |
| `_weights` | double[] | 8B + L×8B | Hamming coefficients |
| `_invWeightSum` | double | 8B | Precomputed 1/Σw |
| `_buffer` | RingBuffer | ~40B + L×8B | Circular data buffer |
| `_state` | State | 16B | Last valid + initialized flag |
| `_p_state` | State | 16B | Previous state for rollback |
| **Total** | | ~92B + 2L×8B | Plus object overhead |

For a typical 14-period: ~92 + 224 ≈ **316 bytes** per instance.

## Common Pitfalls

1. **Confusing Hamming and Hanning**: Hamming uses 0.54/0.46 coefficients with edge weights of 0.08. Hanning uses 0.5/0.5 with edge weights of 0.0. They're different windows with different properties.

2. **Lag Acceptance**: HAMMA has inherent lag of approximately $(L-1)/2$ bars. This is the price of symmetric smoothing. If you need faster response, consider asymmetric windows like ALMA.

3. **Cold Start**: HAMMA requires a full window ($L$) to be mathematically valid. First $L-1$ bars are convergence noise.

4. **Small Periods**: With very small periods (e.g., 3), the window shape degenerates. The edge-center-edge pattern becomes less meaningful. Consider period >= 5 for meaningful Hamming characteristics.

5. **Side Lobe Trade-off**: The -43 dB first side lobe comes at the cost of slightly wider main lobe than Hanning. If frequency resolution matters more than side lobe suppression, consider other windows.
