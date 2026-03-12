# ALMA: Arnaud Legoux Moving Average

> *Gaussian distributions govern everything from particle diffusion to the distribution of shoe sizes. Applying them to price action isn't 'technical analysis'; it's just physics with a profit motive.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `offset` (default 0.85), `sigma` (default 6.0)                      |
| **Outputs**      | Single series (Alma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [alma.pine](alma.pine)                       |
| **Signature**    | [alma_signature](alma_signature.md) |

- ALMA is a Finite Impulse Response (FIR) filter that applies a Gaussian window to price data.
- Parameterized by `period`, `offset` (default 0.85), `sigma` (default 6.0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

ALMA is a Finite Impulse Response (FIR) filter that applies a Gaussian window to price data. Unlike the Simple Moving Average (which treats 10-minute-old data with the same reverence as 1-minute-old data) or the Exponential Moving Average (which holds onto history like a hoarder), ALMA allows you to shape the weight distribution precisely. It lets you define the trade-off between smoothness and lag using standard deviation ($\sigma$) and offset, rather than arbitrary periods.

## Historical Context / The Standard

Arnaud Legoux and Dimitris Kouzis-Loukas published ALMA in 2009. The context was a trading world drowning in "adaptive" moving averages (KAMA, FRAMA) that often adapted too late or overshot the turn.

While Hull (HMA) attempted to solve lag through algebraic subtraction (and created overshoot), and Jurik (JMA) hid behind proprietary black-box math, Legoux returned to first principles: Signal Processing. He applied the Gaussian filter—standard in electrical engineering for noise reduction—to financial time series. It is not a "modern" invention so much as the correct application of established math to a messy domain.

## Architecture & Physics

ALMA is a weighted moving average where weights follow a normal distribution (bell curve).

The physics of ALMA rely on shifting the "center of gravity" of the window.

* **SMA:** Center of gravity is always the middle ($0.5$). Lag is fixed.
* **EMA:** Center of gravity is front-loaded but has an infinite tail.
* **ALMA:** You move the center. An offset of $0.85$ pushes the bulk of the weight to the most recent 15% of the window.

This shift allows the indicator to capture momentum (high responsiveness) while the Gaussian decay kills high-frequency noise (smoothness). It behaves less like a lagging indicator and more like a mass-dampener system.

### The Compute Challenge

Naive implementations recalculate the Gaussian weights on every tick. This is CPU suicide.
QuanTAlib precomputes the weight vector $\mathbf{W}$ upon initialization. The runtime operation effectively becomes a dot product of the price buffer and the weight vector.

$$ \text{Runtime Cost} = O(N) \text{ multiplications} $$

While heavier than the recursive EMA ($O(1)$), the memory locality of the arrays allows modern CPUs to vectorise these operations (SIMD), making the penalty negligible for typical window sizes (< 100).

## Mathematical Foundation

The weight calculation relies on three inputs:

1. **Window ($L$)**: The lookback period.
2. **Offset ($o$)**: Where the Gaussian peak sits (0.0 to 1.0). Default is 0.85.
3. **Sigma ($\sigma$)**: The width of the bell curve. Default is 6.0.

### 1. Center and Width Calculation

First, QuanTAlib defines the peak index ($m$) and the spread ($s$):

$$ m = o \cdot (L - 1) $$

$$ s = \frac{L}{\sigma} $$

### 2. Weight Generation

For each index $i$ from $0$ to $L-1$, the unnormalized weight is calculated:

$$ w_i = \exp \left( - \frac{(i - m)^2}{2s^2} \right) $$

### 3. Normalization

The final ALMA value is the weighted sum. The weights are not normalized to sum to 1.0 beforehand; instead, division by the total sum of weights $W_{sum}$ happens at the end.

$$ \text{ALMA}_t = \frac{\sum_{i=0}^{L-1} P_{t-i} \cdot w_{L-1-i}}{W_{sum}} $$

*Note: The weights vector is reversed relative to the price history buffer (most recent price gets the weight at the offset index).*

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Constructor (one-time precomputation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 2N + 2 | 3 | 6N + 6 |
| DIV | N | 15 | 15N |
| EXP | N | 50 | 50N |
| ADD/SUB | 2N | 1 | 2N |
| **Total (init)** | — | — | **~73N cycles** |

For period=20: ~1,460 cycles (one-time).

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

The dot product `∑(buffer[i] × weights[i])` is highly vectorizable:

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
| **Accuracy** | 10/10 | Matches Gaussian definition to `double` precision |
| **Timeliness** | 9/10 | Tunable offset (0.85) minimizes group delay |
| **Overshoot** | 9/10 | Gaussian decay prevents the "whip" effect of HMA |
| **Smoothness** | 8/10 | Dependent on σ; higher σ = sharper filter |

### Implementation Details

```csharp
// Precomputation (Constructor)
double m = offset * (period - 1);
double s = period / sigma;
double wSum = 0;

for (int i = 0; i < period; i++) {
    double weight = Math.Exp(-((i - m) * (i - m)) / (2 * s * s));
    _weights[i] = weight;
    wSum += weight;
}

// Runtime (Update)
double numerator = 0;
// Note: _buffer holds prices. _weights are pre-aligned.
// Modern JIT unrolls this loop efficiently.
for (int i = 0; i < period; i++) {
    numerator += _buffer[i] * _weights[i];
}
return numerator / wSum;
```

## Validation

QuanTAlib validates against reference implementations that respect the Gaussian math, ignoring those that approximate for speed.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against math definition. |
| **Skender** | ✅ | Matches `GetAlma`. |
| **Ooples** | ✅ | Matches `CalculateArnaudLegouxMovingAverage`. |
| **Pandas-TA** | ✅ | Python reference implementation matches. |
| **TA-Lib** | ❌ | Not included in standard C distribution. |
| **Tulip** | ❌ | Not included. |

## C# Implementation Considerations

### Precomputed Gaussian Weights

Weights are computed once in the constructor and stored in a `double[]` array:

```csharp
_weights = new double[period];
ComputeWeights(_weights, period, offset, sigma, out _invWeightSum);
```

The inverse of the weight sum is precomputed for multiplication instead of division in the hot path.

### State Record Struct with Auto Layout

Minimal state for bar correction:

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(double LastValidValue, bool IsInitialized);
```

The `LayoutKind.Auto` lets the JIT optimize field placement for cache efficiency.

### SIMD-Optimized Dot Product

The weighted sum calculation delegates to a SIMD-optimized `DotProduct` extension method:

```csharp
double sum1 = internalBuf.Slice(head, part1Len).DotProduct(_weights.AsSpan(0, part1Len));
double sum2 = internalBuf[..head].DotProduct(_weights.AsSpan(part1Len));
return (sum1 + sum2) * _invWeightSum;
```

The dot product leverages AVX2/AVX-512/NEON intrinsics internally, achieving up to 8× speedup.

### Circular Buffer Handling

The RingBuffer's internal array is accessed directly to split the dot product across the wrap boundary:

```csharp
ReadOnlySpan<double> internalBuf = _buffer.InternalBuffer;
int head = _buffer.StartIndex;
int part1Len = _period - head;

// Part 1: head..end with weights[0..part1Len]
// Part 2: 0..head with weights[part1Len..period]
```

This avoids copying the buffer into a contiguous array.

### Stackalloc/ArrayPool Allocation Strategy

The static `Calculate` method uses stackalloc for small periods and ArrayPool for large:

```csharp
double[]? weightsArray = period > 256 ? ArrayPool<double>.Shared.Rent(period) : null;
Span<double> weights = period <= 256
    ? stackalloc double[period]
    : weightsArray!.AsSpan(0, period);
```

The 256-element threshold balances stack safety with allocation overhead.

### NaN Handling with Initialization Tracking

Non-finite inputs are replaced with the last valid value, with explicit tracking for uninitialized state:

```csharp
private double GetValidValue(double input)
{
    if (double.IsFinite(input))
        return input;
    return _state.IsInitialized ? _state.LastValidValue : double.NaN;
}
```

This prevents NaN propagation while correctly handling series that start with invalid values.

### Incremental Weight Sum for Warmup

During the warmup period, the weight sum is computed incrementally:

```csharp
if (count < period)
{
    count++;
    currentWeightSum += weights[period - count];
}
```

This avoids recalculating the partial sum on each bar during convergence.

### Separate Internal Update Method

The `Update` method has a private overload with a `publish` parameter:

```csharp
private TValue Update(TValue input, bool isNew, bool publish)
```

This allows state restoration after batch processing without firing events.

### Memory Layout

| Component | Size | Purpose |
| :--- | :--- | :--- |
| `_weights` | 8×period bytes | Precomputed Gaussian weights |
| `_buffer` (RingBuffer) | 32 + 8×period bytes | Sliding window history |
| `_state` | ~16 bytes | LastValidValue, IsInitialized |
| `_p_state` | ~16 bytes | Previous state for rollback |
| Scalars | ~40 bytes | Period, offset, sigma, invWeightSum |
| **Total** | **~104 + 16N bytes** | Per-instance footprint |

For ALMA(50), total memory is approximately 900 bytes per instance.

## Common Pitfalls

1. **Offset Abuse**: Setting offset to `0.99` creates a filter that barely filters. It tracks price so closely you might as well use `Price[0]`. Setting it to `0.5` makes it a centered moving average (great for smoothing, terrible for trading due to repainting if used as such, but ALMA does not repaint). The magic is in the `0.85` region.

2. **Sigma Confusion**:
   * $\sigma = 1$: The curve is flat. You have reinvented the Simple Moving Average (badly).
   * $\sigma = 10$: The curve is a needle. You are sampling one specific bar in history.

3. **Cold Start**: ALMA requires a full window ($L$) to be mathematically valid. First $L-1$ bars are convergence noise. Ignore them.
