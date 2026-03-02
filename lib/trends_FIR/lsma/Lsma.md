# LSMA: Least Squares Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `offset` (default 0)                      |
| **Outputs**      | Single series (Lsma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [lsma_signature](lsma_signature) |

### TL;DR

- LSMA (Least Squares Moving Average), also known as the Moving Linear Regression or Endpoint Moving Average, calculates the least squares regression...
- Parameterized by `period`, `offset` (default 0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "If you want to know where the price is going, draw a line through where it's been. LSMA does this for every single bar, tirelessly fitting linear regressions while you sleep."

LSMA (Least Squares Moving Average), also known as the Moving Linear Regression or Endpoint Moving Average, calculates the least squares regression line for the preceding time periods. In plain English: it finds the "best fit" line for the data window and tells you where that line ends.

## Historical Context

Linear regression is as old as Gauss (c. 1809). Applying it as a moving window to financial time series is a more recent development, popularized by traders who realized that a moving average is just a poor man's regression line (specifically, an SMA is a regression line with a slope of 0). LSMA captures both the level and the trend (slope) of the data.

## Architecture & Physics

LSMA is computationally heavier than an SMA because it minimizes the sum of squared errors for a line equation $y = mx + b$.

* **Slope ($m$)**: Represents the trend strength/direction.
* **Intercept ($b$)**: Represents the value at the start of the window.
* **Endpoint**: The value at the current bar ($y = m \times 0 + b$ in our coordinate system where current bar is 0).

## Mathematical Foundation

The regression line is $y = mx + b$.

$$ m = \frac{N \sum xy - \sum x \sum y}{N \sum x^2 - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{N} $$

$$ \text{LSMA} = b - m \times \text{Offset} $$

(Note: In the QuanTAlib implementation, $x$ ranges from $N-1$ (oldest) to $0$ (newest) to simplify the math).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

The O(1) algorithm maintains running sums instead of recomputing the regression on each bar:

**State variables maintained:**
- `sum_x`: Sum of x indices (precomputed constant for fixed period)
- `sum_y`: Running sum of y values
- `sum_xy`: Running sum of x×y products
- `sum_xx`: Sum of x² (precomputed constant)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 6 | 1 | 6 |
| MUL | 4 | 3 | 12 |
| DIV | 2 | 15 | 30 |
| **Total** | **12** | — | **~48 cycles** |

**Hot path breakdown:**
- Update running sums: `sum_y += new - old`, `sum_xy += (N-1)×new - sum_y_old` → 4 ADD/SUB
- Slope calculation: `m = (N×sum_xy - sum_x×sum_y) / denom` → 2 MUL + 1 DIV
- Intercept: `b = (sum_y - m×sum_x) / N` → 1 MUL + 1 SUB + 1 DIV
- Endpoint: `LSMA = b - m×offset` → 1 MUL + 1 SUB

**Comparison with naive O(N) regression:**

| Mode | Complexity | Cycles (Period=100) |
| :--- | :---: | :---: |
| Naive (recompute) | O(N) | ~600 cycles |
| QuanTAlib O(1) | O(1) | ~48 cycles |
| **Improvement** | **—** | **~12× faster** |

### Batch Mode (SIMD)

LSMA batch can vectorize the running sum updates:

| Operation | Scalar Ops (512 bars) | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Running sum updates | 512 | 64 | 8× |
| Slope calculations | 1024 | 128 | 8× |
| Endpoint projections | 512 | 64 | 8× |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Mathematically precise regression endpoint |
| **Timeliness** | 8/10 | Projects trend forward, reducing perceived lag |
| **Overshoot** | 2/10 | Significant overshoot on trend reversals (projects continuation) |
| **Smoothness** | 3/10 | Sensitive to outliers; least-squares fit follows noise |

## Validation

Validated against Skender.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetEpma` |
| **TA-Lib** | N/A | Not implemented |

| **Tulip** | N/A | Not implemented. |
| **Ooples** | N/A | Not implemented. |

### C# Implementation Considerations

The QuanTAlib LSMA implementation achieves O(1) streaming updates through running sum maintenance with several optimizations:

#### O(1) Running Sum Algorithm

The implementation maintains two running sums (`SumY`, `SumXY`) that enable constant-time updates instead of O(N) recalculation:

```csharp
// O(1) update for sum_xy: sum_xy_new = sum_xy_old + sum_y_prev - n * oldest
_state.SumXY = Math.FusedMultiplyAdd(-_period, oldest, _state.SumXY + prev_sum_y);

// O(1) update for sum_y
_state.SumY = _state.SumY - oldest + val;
```

#### Precomputed Constants

Mathematical constants are computed once in the constructor to avoid redundant calculations:

```csharp
// sum_x = 0 + 1 + ... + (n-1) = n(n-1)/2
_sum_x = 0.5 * period * (period - 1);

// sum_x2 = 0² + ... + (n-1)² = (n-1)n(2n-1)/6
double sum_x2 = (period - 1.0) * period * (2.0 * period - 1.0) / 6.0;

// denominator = n * sum_x2 - sum_x²
_denominator = period * sum_x2 - _sum_x * _sum_x;
```

#### State Record Struct

State uses `LayoutKind.Auto` for compiler-optimized field ordering:

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(double SumY, double SumXY, double LastVal, double LastValidValue);
private State _state;
private State _p_state;  // Previous state for bar correction
```

#### FusedMultiplyAdd Usage

FMA is used extensively for slope, intercept, and endpoint calculations:

```csharp
double m = Math.FusedMultiplyAdd(n, _state.SumXY, -sx * _state.SumY) / denom;
double b = Math.FusedMultiplyAdd(-m, sx, _state.SumY) / n;
result = Math.FusedMultiplyAdd(-m, _offset, b);
```

#### Periodic Resync

Running sums accumulate floating-point drift; periodic resync every 1000 ticks corrects this:

```csharp
private const int ResyncInterval = 1000;

private void Resync()
{
    _state.SumY = _buffer.Sum;
    _state.SumXY = 0;
    var span = _buffer.GetSpan();
    for (int i = 0; i < span.Length; i++)
    {
        int x = span.Length - 1 - i;
        _state.SumXY = Math.FusedMultiplyAdd(x, span[i], _state.SumXY);
    }
}
```

#### Stackalloc/ArrayPool Strategy

The static `Calculate` method uses stackalloc for small periods (≤256) to avoid heap allocation:

```csharp
const int StackAllocThreshold = 256;
Span<double> buffer = period <= StackAllocThreshold
    ? stackalloc double[period]
    : new double[period];
```

#### Thread-Safe Disposal

Disposal uses atomic operations for idempotent, thread-safe cleanup:

```csharp
protected override void Dispose(bool disposing)
{
    if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0 && _source != null)
    {
        _source.Pub -= _handler;
        _source = null;
    }
    base.Dispose(disposing);
}
```

#### NaN Handling

Invalid values are replaced with the last valid value to maintain calculation integrity:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private double GetValidValue(double input)
{
    if (double.IsFinite(input))
    {
        _state.LastValidValue = input;
        return input;
    }
    return _state.LastValidValue;
}
```

#### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_period` | `int` | 4 | Lookback window |
| `_offset` | `int` | 4 | Forecast offset |
| `_buffer` | `RingBuffer` | 8 (ref) | Circular storage |
| `_sum_x` | `double` | 8 | Precomputed Σx |
| `_denominator` | `double` | 8 | Precomputed denominator |
| `_state` | `State` | 32 | Current state (SumY, SumXY, LastVal, LastValidValue) |
| `_p_state` | `State` | 32 | Previous state for rollback |
| `_tickCount` | `int` | 4 | Resync counter |
| `_disposed` | `int` | 4 | Atomic disposal flag |
| **Total** | | **~104 bytes** | Per instance (excluding RingBuffer internal storage) |

### Common Pitfalls

1. **Overshoot**: Because it projects a trend, LSMA will overshoot significantly when the trend reverses. It assumes the trend continues.
2. **Offset**: You can use a positive offset to extrapolate into the future (forecasting), or a negative offset to center the average.
3. **Noise**: It is very sensitive to outliers because it tries to fit a line to them.
