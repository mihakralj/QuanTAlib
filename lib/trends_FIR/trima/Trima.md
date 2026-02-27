# TRIMA: Triangular Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Trima)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `p1 + p2 - 1` bars                          |

### TL;DR

- The Triangular Moving Average (TRIMA) places the majority of its weight on the middle of the data window, tapering off linearly towards the ends.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `p1 + p2 - 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The weighted blanket of moving averages. It doesn't care where the price is going right now; it cares where the price feels most comfortable."

The Triangular Moving Average (TRIMA) places the majority of its weight on the middle of the data window, tapering off linearly towards the ends. This creates a triangular weight distribution (hence the name). It is mathematically equivalent to a double-smoothed SMA.

## Historical Context

TRIMA has been a staple in cycle analysis. By double-smoothing the data, it effectively removes high-frequency noise, making it ideal for identifying dominant market cycles. However, this smoothness comes at the cost of significant lag.

## Architecture & Physics

TRIMA is implemented as a cascade of two Simple Moving Averages.
$$ TRIMA = SMA(SMA(Price, P_1), P_2) $$

Where $P_1$ and $P_2$ are roughly half the total period.

### The Weight Distribution

An SMA has a rectangular weight distribution (all weights equal). A WMA has a linear distribution (heaviest at the end). TRIMA has a triangular distribution (heaviest in the center).

## Mathematical Foundation

### 1. Period Splitting

$$ P_1 = \lfloor \frac{N}{2} \rfloor + 1 $$
$$ P_2 = \lceil \frac{N+1}{2} \rceil $$

### 2. The Cascade

$$ TRIMA = SMA(SMA(Price, P_1), P_2) $$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

TRIMA chains two SMA instances. Each SMA is O(1) with ~17 cycles (see SMA.md).

| Component | Operations | Cost (cycles) |
| :--- | :--- | :---: |
| SMA(P₁) | 2 ADD/SUB, 1 DIV | ~17 |
| SMA(P₂) | 2 ADD/SUB, 1 DIV | ~17 |
| **Total** | **4 ADD/SUB, 2 DIV** | **~34 cycles** |

**Hot path breakdown:**
- First SMA smooths the raw price → ~17 cycles
- Second SMA smooths the first SMA's output → ~17 cycles
- No additional combining math required

### Batch Mode (SIMD)

Each SMA component benefits from SIMD prefix-sum optimization:

| Component | Scalar (512 bars) | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| SMA(P₁) prefix sum | ~8.5K cycles | ~1K cycles | ~8× |
| SMA(P₂) prefix sum | ~8.5K cycles | ~1K cycles | ~8× |
| **Total** | **~17K** | **~2K** | **~8×** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib exactly |
| **Timeliness** | 2/10 | Significant lag; double smoothing delays signals |
| **Overshoot** | 10/10 | Never overshoots input data range (FIR property) |
| **Smoothness** | 9/10 | Very smooth; triangular weighting suppresses noise |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_TRIMA` exactly. |
| **Skender** | ✅ | Matches composite `SMA(SMA)` logic. |
| **Tulip** | ✅ | Matches `trima` exactly. |
| **Ooples** | N/A | Not implemented. |

## C# Implementation Considerations

QuanTAlib's TRIMA uses cascaded SMA composition, achieving O(1) streaming updates by leveraging the O(1) nature of each internal SMA. The implementation demonstrates clean indicator composition:

### Composition Architecture

```csharp
[SkipLocalsInit]
public sealed class Trima : AbstractBase
{
    private readonly Sma _sma1;
    private readonly Sma _sma2;

    public Trima(int period)
    {
        int p1 = (period + 1) / 2;
        int p2 = period / 2 + 1;

        _sma1 = new Sma(p1);
        _sma2 = new Sma(p2);
    }
}
```

TRIMA delegates all complexity to its internal SMA instances. Each SMA maintains its own O(1) running sum, so the cascade is also O(1).

### Key Optimizations

| Technique | Implementation | Benefit |
| :--- | :--- | :--- |
| **SMA delegation** | Two internal `Sma` instances | O(1) streaming via running sums |
| **Zero state** | No additional fields beyond SMAs | Minimal memory footprint |
| **Inline cascade** | `_sma2.Update(_sma1.Update(input))` | No intermediate allocation |
| **ArrayPool** | Batch uses rented buffer for SMA1 output | Zero allocation in batch mode |
| **Warmup composition** | `WarmupPeriod = p1 + p2 - 1` | Correct cascaded warmup |

### Streaming Update

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public override TValue Update(TValue input, bool isNew = true)
{
    _isNew = isNew;
    TValue v1 = _sma1.Update(input, isNew);
    TValue v2 = _sma2.Update(v1, isNew);

    Last = v2;
    PubEvent(Last, isNew);
    return Last;
}
```

The `isNew` flag propagates through both SMAs, enabling bar correction at both levels.

### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_period` | int | 4 bytes | Original period |
| `_sma1` | Sma | ~48 + 8×P₁ bytes | First smoothing stage |
| `_sma2` | Sma | ~48 + 8×P₂ bytes | Second smoothing stage |
| `_handler` | delegate | 8 bytes | Event handler reference |
| `_isNew` | bool | 1 byte | Current bar state |
| **Instance total** | | **~110 + 8N bytes** | N = period |

### Batch Processing

```csharp
public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
{
    int p1 = (period + 1) / 2;
    int p2 = period / 2 + 1;

    double[] tempArray = ArrayPool<double>.Shared.Rent(source.Length);
    Span<double> tempSpan = tempArray.AsSpan(0, source.Length);

    try
    {
        Sma.Batch(source, tempSpan, p1);   // First SMA pass
        Sma.Batch(tempSpan, output, p2);   // Second SMA pass
    }
    finally
    {
        ArrayPool<double>.Shared.Return(tempArray);
    }
}
```

Uses ArrayPool for the intermediate buffer to avoid heap allocation per batch.

### Bar Correction Propagation

The `isNew` flag propagates through both internal SMAs:

```csharp
// isNew=false triggers rollback in BOTH SMAs
TValue v1 = _sma1.Update(input, isNew);  // SMA1 rolls back its running sum
TValue v2 = _sma2.Update(v1, isNew);     // SMA2 rolls back based on corrected SMA1 output
```

This ensures consistent bar correction across the entire cascade.

### Common Pitfalls

1. **Lag**: TRIMA has more lag than SMA, EMA, or WMA. It is a lagging indicator, not a leading one.
2. **Signal Generation**: Due to its lag, TRIMA is poor for crossover signals. It is best used for visual trend identification or as a baseline for envelopes (e.g., TMA Bands).
3. **Even/Odd Periods**: The exact calculation of $P_1$ and $P_2$ differs slightly between implementations for even periods. QuanTAlib matches the standard definition used by TA-Lib.
