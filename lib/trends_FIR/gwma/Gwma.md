# GWMA: Gaussian-Weighted Moving Average

> *The Gaussian distribution shows up everywhere from thermal noise to the central limit theorem. Using it to weight price data isn't magic; it's just applied statistics with a trading account.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `sigma` (default 0.4)                      |
| **Outputs**      | Single series (Gwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [gwma.pine](gwma.pine)                       |
| **Signature**    | [gwma_signature](gwma_signature.md) |

- GWMA is a Finite Impulse Response (FIR) filter that applies a centered Gaussian window to price data.
- Parameterized by `period`, `sigma` (default 0.4).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

GWMA is a Finite Impulse Response (FIR) filter that applies a centered Gaussian window to price data. Unlike ALMA (which allows shifting the Gaussian peak via an offset parameter), GWMA centers the bell curve at the middle of the lookback window. The sigma parameter controls the width of the Gaussian, determining how sharply the weights decay from the center.

## Historical Context

The Gaussian (normal) distribution has been the workhorse of signal processing since Gauss himself used it for astronomical observations in the early 1800s. In the context of moving averages, Gaussian weighting provides a mathematically optimal way to smooth noise while preserving the underlying signal structure.

The key insight is that a Gaussian filter minimizes the product of bandwidth in both time and frequency domains (the Heisenberg-Gabor limit). This makes it theoretically optimal for balancing noise reduction against signal preservation. GWMA applies this principle to financial time series, centering the Gaussian at the window's midpoint.

## Architecture & Physics

GWMA is a weighted moving average where weights follow a normal distribution centered at the middle of the lookback window.

The physics of GWMA differ from ALMA in one critical aspect: **the center of gravity is always at the window midpoint**. This makes GWMA a symmetric filter, which has specific implications:

* **Zero phase distortion** in the frequency domain (no group delay asymmetry)
* **Equal sensitivity** to past and future data around the center point
* **Inherent smoothness** from the Gaussian's infinite differentiability

The sigma parameter ($\sigma$) controls the bell curve width:
* **Small sigma** (e.g., 0.1): Narrow peak, weights concentrated near center, behaves like sampling a single point
* **Large sigma** (e.g., 0.9): Wide bell, weights spread across window, approaches Simple Moving Average behavior
* **Default sigma** (0.4): Balanced curve providing good noise reduction without excessive lag

### The Compute Challenge

Like ALMA, naive implementations recalculate Gaussian weights on every tick. QuanTAlib precomputes the weight vector $\mathbf{W}$ upon initialization. Runtime becomes a dot product of the price buffer and weight vector.

$$ \text{Runtime Cost} = O(N) \text{ multiplications} $$

The memory locality of arrays enables SIMD vectorization, making the O(N) cost negligible for typical window sizes.

## Mathematical Foundation

The weight calculation relies on two inputs:

1. **Window ($L$)**: The lookback period.
2. **Sigma ($\sigma$)**: The width of the bell curve (0 < $\sigma$ ≤ 1). Default is 0.4.

### 1. Center and Width Calculation

QuanTAlib defines the peak index (center) and the spread:

$$ \text{center} = \frac{L - 1}{2} $$

$$ \text{invSigmaP} = \frac{1}{\sigma \cdot L} $$

### 2. Weight Generation

For each index $i$ from $0$ to $L-1$, the unnormalized weight is calculated:

$$ w_i = \exp \left( -\frac{1}{2} \cdot \left( (i - \text{center}) \cdot \text{invSigmaP} \right)^2 \right) $$

This is equivalent to:

$$ w_i = \exp \left( -\frac{(i - \text{center})^2}{2 \cdot (\sigma \cdot L)^2} \right) $$

### 3. Normalization

The final GWMA value is the weighted sum divided by the total sum of weights $W_{sum}$:

$$ \text{GWMA}_t = \frac{\sum_{i=0}^{L-1} P_{t-L+1+i} \cdot w_i}{W_{sum}} $$

### Example Calculation

For period=5, sigma=0.4:
- center = 2
- invSigmaP = 1/(0.4 × 5) = 0.5

| Index | (i - center) × invSigmaP | Weight |
|-------|--------------------------|--------|
| 0 | -1.0 | exp(-0.5) ≈ 0.6065 |
| 1 | -0.5 | exp(-0.125) ≈ 0.8825 |
| 2 | 0.0 | exp(0) = 1.0 |
| 3 | 0.5 | exp(-0.125) ≈ 0.8825 |
| 4 | 1.0 | exp(-0.5) ≈ 0.6065 |

Note the symmetry around the center (index 2).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Constructor (one-time weight precomputation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 2L | 3 | 6L |
| ADD/SUB | L | 1 | L |
| EXP | L | 50 | 50L |
| DIV | 1 | 15 | 15 |
| **Total (init)** | — | — | **~57L + 15 cycles** |

For period=20: ~1,155 cycles (one-time).

**Hot path (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | L + 1 | 3 | 3L + 3 |
| ADD | L | 1 | L |
| **Total** | **2L + 1** | — | **~4L + 3 cycles** |

For period=20: ~83 cycles per bar.

**Hot path breakdown:**
- Dot product: `buffer.DotProduct(weights)` → L MUL + L ADD
- Normalization: `sum × invWeightSum` → 1 MUL (precomputed inverse avoids DIV)

### Batch Mode (SIMD)

The dot product is highly vectorizable:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Weighted products | L | L/8 | 8× |
| Horizontal sum | L | log₂(8) | ~L/3× |

**Batch efficiency (512 bars, period=20):**

| Mode | Cycles/bar | Total | Notes |
| :--- | :---: | :---: | :--- |
| Scalar streaming | ~83 | ~42,496 | O(L) per bar |
| SIMD batch | ~22 | ~11,264 | Vectorized dot product |
| **Improvement** | **~4×** | **~31K saved** | — |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches Gaussian definition to `double` precision |
| **Timeliness** | 7/10 | Centered filter has inherent lag of (period-1)/2 bars |
| **Overshoot** | 10/10 | Symmetric Gaussian prevents overshoot entirely |
| **Smoothness** | 9/10 | Gaussian provides optimal smoothing characteristics |

### Implementation Details

```csharp
// Precomputation (Constructor)
double center = (period - 1) / 2.0;
double invSigmaP = 1.0 / (sigma * period);
double wSum = 0;

for (int i = 0; i < period; i++) {
    double x = (i - center) * invSigmaP;
    double weight = Math.Exp(-0.5 * x * x);
    _weights[i] = weight;
    wSum += weight;
}
_invWeightSum = 1.0 / wSum;

// Runtime (Update)
double sum = _buffer.DotProduct(_weights);
return sum * _invWeightSum;
```

## Comparison: GWMA vs ALMA

| Aspect | GWMA | ALMA |
| :--- | :--- | :--- |
| **Center** | Fixed at (period-1)/2 | Configurable via offset (0-1) |
| **Symmetry** | Always symmetric | Asymmetric when offset ≠ 0.5 |
| **Lag** | Fixed at (period-1)/2 | Reduced when offset > 0.5 |
| **Overshoot** | None | Minimal (depends on offset) |
| **Use Case** | Smoothing, noise reduction | Trend following, responsiveness |

Choose GWMA when you need maximum smoothing and don't mind the inherent lag. Choose ALMA when you need to trade off some smoothing for faster response to price changes.

## Validation

QuanTAlib validates GWMA against its mathematical definition and internal consistency checks.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against math definition. |
| **PineScript** | ✅ | Reference implementation matches. |
| **TA-Lib** | ❌ | Not included in standard C distribution. |
| **Skender** | ❌ | Not included. |
| **Tulip** | ❌ | Not included. |
| **Ooples** | ❌ | Not included. |

### C# Implementation Considerations

The QuanTAlib GWMA implementation optimizes for streaming throughput with precomputed weights and careful state management:

#### Precomputed Weights with Inverse Sum

Gaussian weights and the inverse of their sum are calculated once in the constructor:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static void ComputeWeights(Span<double> weights, int period, double sigma, out double invWeightSum)
{
    double center = (period - 1) / 2.0;
    double invSigmaP = 1.0 / (sigma * period);
    double sum = 0;

    for (int i = 0; i < period; i++)
    {
        double x = (i - center) * invSigmaP;
        double w = Math.Exp(-0.5 * x * x);
        weights[i] = w;
        sum += w;
    }
    invWeightSum = 1.0 / sum;  // Precompute inverse for multiplication
}
```

#### State Record Struct with Auto Layout

State uses `LayoutKind.Auto` for compiler-optimized field arrangement:

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State
{
    public double LastValidValue;
    public bool IsInitialized;
}
private State _state;
private State _p_state;  // Previous state for bar correction
```

#### FusedMultiplyAdd in Warmup Path

The warmup calculation uses FMA for efficient weighted sum accumulation:

```csharp
private static double CalculateWeightedSumWarmup(ReadOnlySpan<double> window, int p, double sigma, double fallbackValue)
{
    double center = (p - 1) * 0.5;
    double invSigmaP = 1.0 / (sigma * p);
    double sum = 0.0;
    double wSum = 0.0;

    for (int i = 0; i < p; i++)
    {
        double x = (i - center) * invSigmaP;
        double w = Math.Exp(-0.5 * x * x);
        sum = Math.FusedMultiplyAdd(window[i], w, sum);  // sum += window[i] * w
        wSum += w;
    }
    return wSum > 0.0 ? sum / wSum : fallbackValue;
}
```

#### Optimized Circular Buffer DotProduct

The hot path handles ring buffer wraparound with two slice dot products:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private double CalculateWeightedSum(double fallbackValue)
{
    if (_invWeightSum == 0.0) return fallbackValue;

    ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;
    int head = _buffer.StartIndex;

    int part1Len = _period - head;
    double sum1 = internalBuf.Slice(head, part1Len).DotProduct(_weights.AsSpan(0, part1Len));
    double sum2 = internalBuf[..head].DotProduct(_weights.AsSpan(part1Len));

    return (sum1 + sum2) * _invWeightSum;
}
```

#### ArrayPool for Large Periods in Batch Mode

The static `Calculate` method uses ArrayPool for periods >256:

```csharp
double[]? weightsArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
Span<double> weights = period <= 256
    ? stackalloc double[period]
    : weightsArray!.AsSpan(0, period);

double[]? ringArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
Span<double> ring = period <= 256
    ? stackalloc double[period]
    : ringArray!.AsSpan(0, period);

try
{
    // Processing loop...
}
finally
{
    if (weightsArray != null) ArrayPool<double>.Shared.Return(weightsArray);
    if (ringArray != null) ArrayPool<double>.Shared.Return(ringArray);
}
```

#### State Restoration After Batch

Batch processing restores streaming state by seeding last valid value and replaying:

```csharp
public override TSeries Update(TSeries source)
{
    Calculate(source.Values, vSpan, _period, _sigma);

    // Restore internal state
    _buffer.Clear();
    int windowSize = Math.Min(len, _period);
    int startIndex = len - windowSize;

    // Seed last valid value from history before replay window
    _state = default;
    if (startIndex > 0)
    {
        for (int i = startIndex - 1; i >= 0; i--)
        {
            if (double.IsFinite(source.Values[i]))
            {
                _state.LastValidValue = source.Values[i];
                _state.IsInitialized = true;
                break;
            }
        }
    }

    // Replay to rebuild buffer state
    for (int i = startIndex; i < len; i++)
    {
        Update(source[i], isNew: true, publish: false);
    }
    return new TSeries(t, v);
}
```

#### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_period` | `int` | 4 | Window length |
| `_sigma` | `double` | 8 | Gaussian width parameter |
| `_weights` | `double[]` | 8 (ref) | Precomputed Gaussian weights |
| `_invWeightSum` | `double` | 8 | Inverse of weight sum |
| `_buffer` | `RingBuffer` | 8 (ref) | Circular price storage |
| `_state` | `State` | 16 | Current state (LastValidValue, IsInitialized) |
| `_p_state` | `State` | 16 | Previous state for rollback |
| **Total** | | **~68 bytes** | Per instance (excluding buffer/array internals) |

**Weight array storage:** `period × 8` bytes (e.g., 160 bytes for period=20)

## Common Pitfalls

1. **Sigma Extremes**:
   * $\sigma = 0.1$: The curve is a narrow spike. You're essentially sampling one bar near the center.
   * $\sigma = 0.9$: The curve is nearly flat. You've approximated a Simple Moving Average (with more computation).

2. **Lag Acceptance**: GWMA has inherent lag of approximately $(L-1)/2$ bars. This is the price of symmetric smoothing. If you need faster response, use ALMA with offset > 0.5.

3. **Cold Start**: GWMA requires a full window ($L$) to be mathematically valid. First $L-1$ bars are convergence noise.

4. **Centered vs Offset**: Don't confuse GWMA with ALMA. GWMA always centers the Gaussian; ALMA lets you shift it. If you find yourself wanting offset control, use ALMA instead.
