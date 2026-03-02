# BLMA: Blackman Window Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Blma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [blma_signature](blma_signature) |

### TL;DR

- The Blackman Window Moving Average (BLMA) applies a triple-cosine window function from digital signal processing to financial time series.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "If you want to filter noise, don't just average it - window it."

The Blackman Window Moving Average (BLMA) applies a triple-cosine window function from digital signal processing to financial time series. Originally developed by **Ralph Beebe Blackman** at Bell Labs in the 1950s for spectral analysis, this filter provides superior noise suppression compared to standard moving averages by minimizing spectral leakage.

## Historical Context

In the early days of signal processing, engineers struggled with **spectral leakage** where energy from one frequency bleeds into others during analysis. Simple rectangular windows (like SMA) caused significant leakage. Blackman proposed a window function with tapered edges that drastically reduced this effect. In trading, "leakage" manifests as market noise distorting the trend signal. BLMA adapts this DSP innovation to create a trend filter that is remarkably smooth yet responsive to significant moves.

## Architecture & Physics

BLMA is a Finite Impulse Response (FIR) filter. Unlike Exponential Moving Averages (IIR) which have infinite memory, BLMA considers only the last $N$ bars.

The "physics" of BLMA relies on its bell-shaped weighting curve. The weights are highest in the center of the window and taper to zero at both ends (newest and oldest data). This symmetry means BLMA has a lag of approximately $N/2$, but it effectively suppresses high-frequency noise (jitter) that often plagues other averages.

### The Zero-Edge Effect

Because the Blackman window tapers to zero at the edges ($w[0] \approx 0$ and $w[N-1] \approx 0$), the most recent price data has very little immediate impact on the indicator value. This creates a "smoothness" that filters out sudden spikes, but it also introduces a specific type of lag where the indicator is slow to react to a sudden trend reversal until the price move enters the "fat" part of the window (the center).

## Mathematical Foundation

The Blackman window weights $w(n)$ for a period $N$ are calculated as:

$$ w(n) = 0.42 - 0.5 \cos\left(\frac{2\pi n}{N-1}\right) + 0.08 \cos\left(\frac{4\pi n}{N-1}\right) $$

Where $0 \le n \le N-1$.

The BLMA value is the weighted average:

$$ BLMA_t = \frac{\sum_{i=0}^{N-1} P_{t-i} \cdot w(i)}{\sum_{i=0}^{N-1} w(i)} $$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Constructor (one-time weight precomputation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| COS | 2N | 40 | 80N |
| MUL | 4N | 3 | 12N |
| ADD/SUB | 3N | 1 | 3N |
| **Total (init)** | — | — | **~95N cycles** |

For period=20: ~1,900 cycles (one-time).

**Hot path (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | N | 3 | 3N |
| ADD | N | 1 | N |
| DIV | 1 | 15 | 15 |
| **Total** | **2N + 1** | — | **~4N + 15 cycles** |

For period=20: ~95 cycles per bar.

**Hot path breakdown:**
- Weighted sum: `∑(buffer[i] × weights[i])` → N MUL + N ADD
- Normalization: `sum / wSum` → 1 DIV (wSum precomputed)

### Batch Mode (SIMD)

The convolution is highly vectorizable:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Weighted products | N | N/8 | 8× |
| Horizontal sum | N | log₂(8) | ~N/3× |

**Batch efficiency (512 bars, period=20):**

| Mode | Cycles/bar | Total | Notes |
| :--- | :---: | :---: | :--- |
| Scalar streaming | ~95 | ~48,640 | O(N) per bar |
| SIMD batch | ~25 | ~12,800 | Vectorized dot product |
| **Improvement** | **~4×** | **~36K saved** | — |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Precise DSP windowing |
| **Timeliness** | 4/10 | Significant lag (N/2) due to symmetric window |
| **Overshoot** | 10/10 | Never overshoots (FIR property) |
| **Smoothness** | 10/10 | Excellent noise suppression (-58dB side-lobes) |

### Zero-Allocation Design

The implementation uses a pre-calculated weights array and a circular buffer (`RingBuffer`) to store price history. The `Update` method performs the weighted sum without allocating any new memory on the heap. For the static `Calculate` method, `stackalloc` is used for weights and temporary buffers for small periods (up to 256), ensuring high performance.

## Validation

BLMA is validated against a reference implementation using the standard Blackman window formula.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Matches theoretical formula. |
| **PineScript** | ✅ | Matches PineScript reference logic. |

### C# Implementation Considerations

The QuanTAlib BLMA implementation emphasizes precomputation and zero-allocation streaming:

#### Precomputed Weights Array

Blackman window weights are calculated once in the constructor and reused for every update:

```csharp
public Blma(int period)
{
    _weights = new double[period];
    _weightSum = CalculateWeights(period, _weights);
}

private static double CalculateWeights(int n, Span<double> weights)
{
    const double a0 = 0.42;
    const double a1 = 0.5;
    const double a2 = 0.08;
    double invNMinus1 = 1.0 / (n - 1);

    for (int i = 0; i < n; i++)
    {
        double ratio = i * invNMinus1;
        double w = a0 - (a1 * Math.Cos(2.0 * Math.PI * ratio))
                      + (a2 * Math.Cos(4.0 * Math.PI * ratio));
        weights[i] = w;
        totalWeight += w;
    }
    return totalWeight;
}
```

#### RingBuffer with DotProduct Extension

The weighted sum uses an optimized dot product that handles circular buffer wraparound:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static double CalculateWeightedSum(RingBuffer buffer, ReadOnlySpan<double> weights)
{
    int start = buffer.StartIndex;
    int count = buffer.Count;
    int capacity = buffer.Capacity;

    if (start + count <= capacity)
    {
        // Contiguous case - single dot product
        return buffer.InternalBuffer.Slice(start, count).DotProduct(weights);
    }

    // Wraparound case - two dot products
    int firstPartLength = capacity - start;
    int secondPartLength = count - firstPartLength;

    double sum1 = buffer.InternalBuffer.Slice(start, firstPartLength).DotProduct(weights[..firstPartLength]);
    double sum2 = buffer.InternalBuffer.Slice(0, secondPartLength).DotProduct(weights[firstPartLength..]);

    return sum1 + sum2;
}
```

#### Dynamic Warmup Weights

During warmup (fewer than `period` bars), weights are calculated dynamically using stackalloc:

```csharp
if (_buffer.Count < _period)
{
    int count = _buffer.Count;
    Span<double> currentWeights = stackalloc double[count];
    double currentWeightSum = CalculateWeights(count, currentWeights);
    result = ComputeWeightedAverage(currentWeightSum, weightedSum, _buffer.Average());
}
```

#### Stackalloc Strategy for Batch Processing

The static `Calculate` method uses stackalloc for small periods (≤256) to avoid heap allocation:

```csharp
Span<double> weights = period <= 256 ? stackalloc double[period] : new double[period];
double weightSum = CalculateWeights(period, weights);

// Buffer for warmup weights to avoid stackalloc in loop
Span<double> warmupWeightsBuffer = period <= 256 ? stackalloc double[period] : new double[period];
```

#### NaN Handling with Last-Valid-Value Substitution

Invalid values are substituted with the last valid value to maintain calculation continuity:

```csharp
double val = input.Value;
if (!double.IsFinite(val))
{
    return Last;  // Return last result without changing state
}
```

In batch mode:
```csharp
double lastValid = double.NaN;
for (int i = 0; i < source.Length; i++)
{
    double val = source[i];
    if (!double.IsFinite(val))
        val = double.IsNaN(lastValid) ? 0 : lastValid;
    else
        lastValid = val;
    // ...
}
```

#### AggressiveInlining on Hot Paths

Critical methods are marked for inlining:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static double ComputeWeightedAverage(double weightSum, double weightedSum, double fallbackAverage)
{
    return Math.Abs(weightSum) < double.Epsilon ? fallbackAverage : weightedSum / weightSum;
}
```

#### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_period` | `int` | 4 | Window size |
| `_buffer` | `RingBuffer` | 8 (ref) | Circular price storage |
| `_weights` | `double[]` | 8 (ref) | Precomputed Blackman weights |
| `_weightSum` | `double` | 8 | Sum of weights (precomputed) |
| **Total** | | **~28 bytes** | Per instance (excluding buffer/array internals) |

**Weight array storage:** `period × 8` bytes (e.g., 160 bytes for period=20)

### Common Pitfalls

* **Lag**: BLMA has more lag than EMA or WMA because it suppresses the most recent data. It is a smoothing filter, not a leading indicator.
* **Warmup**: During the first $N$ bars, the window expands dynamically. The full noise-suppression characteristics are only achieved after $N$ bars.
