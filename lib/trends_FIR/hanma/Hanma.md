# HANMA: Hanning-Weighted Moving Average

> "Julius von Hann deserves credit for the window that bears his name—even if autocomplete keeps trying to change it to 'Hamming.' The zero-edge weights aren't a bug; they're the whole point."

HANMA is a Finite Impulse Response (FIR) filter that applies a Hanning (Hann) window to price data. The Hanning window is a pure raised cosine with edge weights of exactly zero, which provides excellent side lobe suppression while maintaining a narrower main lobe than Hamming. It's particularly effective when you want to eliminate boundary discontinuities entirely.

## Historical Context

Julius von Hann, an Austrian meteorologist, developed this window function in the late 19th century for smoothing meteorological data. The window was later adopted by signal processing engineers and became one of the most widely used window functions in spectral analysis.

The Hanning window is sometimes called "Hann" to avoid confusion with Hamming (a different window with different coefficients). The key distinction: Hanning uses 0.5/0.5 coefficients producing edge weights of exactly zero, while Hamming uses 0.54/0.46 coefficients producing edge weights of 0.08.

In trading applications, HANMA provides smooth output with no boundary artifacts. The zero edge weights mean the first and last samples in the window contribute nothing—a property that eliminates discontinuities when the window slides across the data.

## Architecture & Physics

HANMA is a weighted moving average where weights follow the Hanning function:

$$ w_i = 0.5 \cdot \left(1 - \cos\left(\frac{2\pi i}{N-1}\right)\right) $$

The physics of HANMA reveal several key properties:

* **Zero edge weights**: Edge weights are exactly 0.0, eliminating boundary discontinuities
* **Center weight of 1.0**: Maximum weight at window center
* **First side lobe at -32 dB**: Good side lobe suppression (vs -13 dB for rectangular/SMA)
* **Narrower main lobe than Hamming**: Better frequency resolution
* **Zero phase distortion**: Symmetric filter means no group delay asymmetry

The 0.5 coefficient on both terms creates a pure raised cosine that touches zero at both endpoints. This is mathematically equivalent to $\sin^2(\pi i / (N-1))$.

### The Compute Challenge

Like other FIR filters, naive implementations recalculate weights on every tick. QuanTAlib precomputes the weight vector $\mathbf{W}$ upon initialization. Runtime becomes a dot product of the price buffer and weight vector.

$$ \text{Runtime Cost} = O(N) \text{ multiplications} $$

The memory locality of arrays enables SIMD vectorization, making the O(N) cost negligible for typical window sizes.

## Mathematical Foundation

The weight calculation uses the Hanning window formula:

### 1. Weight Generation

For each index $i$ from $0$ to $L-1$:

$$ w_i = 0.5 \cdot \left(1 - \cos\left(\frac{2\pi i}{L-1}\right)\right) $$

Where $L$ is the lookback period.

### 2. Weight Properties

The Hanning coefficients produce these characteristic values:

| Position | Weight |
|----------|--------|
| Edge (i=0, i=L-1) | 0.00 |
| Center (i=(L-1)/2) | 1.00 |

### 3. Normalization

The final HANMA value is the weighted sum divided by the total sum of weights $W_{sum}$:

$$ \text{HANMA}_t = \frac{\sum_{i=0}^{L-1} P_{t-L+1+i} \cdot w_i}{W_{sum}} $$

### Example Calculation

For period=5:

| Index | cos(2πi/4) | Weight |
|-------|------------|--------|
| 0 | cos(0) = 1.0 | 0.5 × (1 - 1.0) = 0.00 |
| 1 | cos(π/2) = 0.0 | 0.5 × (1 - 0.0) = 0.50 |
| 2 | cos(π) = -1.0 | 0.5 × (1 - (-1.0)) = 1.00 |
| 3 | cos(3π/2) = 0.0 | 0.5 × (1 - 0.0) = 0.50 |
| 4 | cos(2π) = 1.0 | 0.5 × (1 - 1.0) = 0.00 |

Note the symmetry around the center (index 2) with characteristic edge weights of exactly 0.

## Performance Profile

HANMA trades CPU cycles for smooth, artifact-free output.

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

HANMA's dot product structure enables efficient SIMD vectorization:

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
| **Accuracy** | 10/10 | Matches Hanning definition to double precision |
| **Timeliness** | 7/10 | Centered filter has inherent lag of (L-1)/2 bars |
| **Overshoot** | 10/10 | Symmetric window prevents overshoot entirely |
| **Smoothness** | 9/10 | Excellent noise suppression from zero-edge property |

### Implementation Details

```csharp
// Precomputation (Constructor)
double twoPI_N1 = 2.0 * Math.PI / (period - 1);
double wSum = 0;

for (int i = 0; i < period; i++) {
    double weight = 0.5 * (1.0 - Math.Cos(i * twoPI_N1));
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
| **Hanning** | **0.0** | **-32 dB** | Medium | Zero-edge smoothing |
| Hamming | 0.08 | -43 dB | Medium | Maximum side lobe suppression |
| Blackman | 0.0 | -58 dB | Widest | Maximum side lobe suppression |
| Gaussian | Variable | -43 dB typical | Variable | Optimal time-frequency tradeoff |

Choose HANMA when you need zero-edge weights to eliminate boundary discontinuities. Choose HAMMA (Hamming) when you need better side lobe suppression but can tolerate small edge weights.

## Validation

QuanTAlib validates HANMA against its mathematical definition and internal consistency checks.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against math definition. |
| **PineScript** | ✅ | Reference implementation matches. |
| **TA-Lib** | ❌ | Not included in standard C distribution. |
| **Skender** | ❌ | Not included. |
| **Tulip** | ❌ | Not included. |
| **Ooples** | ❌ | Not included. |

### C# Implementation Considerations

The QuanTAlib HANMA implementation optimizes Hanning window convolution through precomputation and SIMD-accelerated dot products:

**Precomputed Weights with Inverse Sum**
```csharp
ComputeWeights(_weights, period, out _invWeightSum);
// ...
double twoPiOverPm1 = 2.0 * Math.PI / (period - 1);
for (int i = 0; i < period; i++)
{
    double w = 0.5 * (1.0 - Math.Cos(twoPiOverPm1 * i));
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

**Zero-Weight Edge Case Handling**
```csharp
if (wSum <= 0)
{
    double avg = 0;
    for (int i = 0; i < count; i++)
        avg += bufferSpan[i];
    return avg / count;
}
```
Hanning's zero edge weights can cause zero weight sum during warmup. Falls back to simple average when weight sum is zero.

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

**Memory Layout**

| Field | Type | Size | Notes |
|:------|:-----|-----:|:------|
| `_period` | int | 4B | Window length |
| `_weights` | double[] | 8B + L×8B | Hanning coefficients |
| `_invWeightSum` | double | 8B | Precomputed 1/Σw |
| `_buffer` | RingBuffer | ~40B + L×8B | Circular data buffer |
| `_state` | State | 16B | Last valid + initialized flag |
| `_p_state` | State | 16B | Previous state for rollback |
| **Total** | | ~92B + 2L×8B | Plus object overhead |

For a typical 14-period: ~92 + 224 ≈ **316 bytes** per instance.

## Common Pitfalls

1. **Confusing Hanning and Hamming**: Hanning uses 0.5 coefficient with edge weights of exactly 0.0. Hamming uses 0.54/0.46 with edge weights of 0.08. They're different windows with different properties.

2. **Zero Edge Weights**: The edge weights being exactly zero means the first and last prices in the window are ignored completely. This is intentional—it eliminates boundary discontinuities.

3. **Lag Acceptance**: HANMA has inherent lag of approximately $(L-1)/2$ bars. This is the price of symmetric smoothing. If you need faster response, consider asymmetric windows like ALMA.

4. **Cold Start**: HANMA requires a full window ($L$) to be mathematically valid. First $L-1$ bars are convergence noise.

5. **Small Periods**: With very small periods (e.g., 3), the window shape degenerates. A period of 3 produces weights [0, 1, 0]—essentially just the middle value. Consider period >= 5 for meaningful Hanning characteristics.

6. **Side Lobe Trade-off**: The -32 dB first side lobe is worse than Hamming's -43 dB, but the narrower main lobe provides better frequency resolution. Choose based on whether you prioritize frequency resolution or side lobe suppression.