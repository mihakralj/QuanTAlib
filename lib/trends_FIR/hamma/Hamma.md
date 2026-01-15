# HAMMA: Hamming-Weighted Moving Average

> "Julius von Hann picked his window function to suppress spectral leakage; we're just using it to smooth price data. Same math, different trading floor."

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

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 35ns/bar | Similar to other FIR filters; linear with window size. |
| **Allocations** | 0 | Weights precomputed. Buffer is circular. |
| **Complexity** | $O(N)$ | Linear with window size. SIMD-vectorizable. |
| **Accuracy** | 10/10 | Matches Hamming definition to `double` precision. |
| **Timeliness** | 7/10 | Centered filter has inherent lag of (period-1)/2 bars. |
| **Overshoot** | 10/10 | Symmetric window prevents overshoot entirely. |
| **Smoothness** | 9/10 | Excellent noise suppression from side lobe characteristics. |

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

## Common Pitfalls

1. **Confusing Hamming and Hanning**: Hamming uses 0.54/0.46 coefficients with edge weights of 0.08. Hanning uses 0.5/0.5 with edge weights of 0.0. They're different windows with different properties.

2. **Lag Acceptance**: HAMMA has inherent lag of approximately $(L-1)/2$ bars. This is the price of symmetric smoothing. If you need faster response, consider asymmetric windows like ALMA.

3. **Cold Start**: HAMMA requires a full window ($L$) to be mathematically valid. First $L-1$ bars are convergence noise.

4. **Small Periods**: With very small periods (e.g., 3), the window shape degenerates. The edge-center-edge pattern becomes less meaningful. Consider period >= 5 for meaningful Hamming characteristics.

5. **Side Lobe Trade-off**: The -43 dB first side lobe comes at the cost of slightly wider main lobe than Hanning. If frequency resolution matters more than side lobe suppression, consider other windows.