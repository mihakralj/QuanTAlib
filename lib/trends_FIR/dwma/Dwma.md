# DWMA: Double Weighted Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Dwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `(period * 2) - 1` bars                          |
| **PineScript**   | [dwma.pine](dwma.pine)                       |
| **Signature**    | [dwma_signature](dwma_signature.md) |

- DWMA (Double Weighted Moving Average) is exactly what it says on the tin: a Weighted Moving Average of a Weighted Moving Average.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `(period * 2) - 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "If one WMA is good, two must be better. DWMA is for when you want your signal so smooth it looks like it's been sanded, polished, and waxed."

DWMA (Double Weighted Moving Average) is exactly what it says on the tin: a Weighted Moving Average of a Weighted Moving Average. Unlike DEMA, which tries to *remove* lag, DWMA accepts lag as the price of admission for superior noise reduction. It produces a curve that is incredibly smooth, ideal for identifying long-term trends without getting faked out by market chop.

## Historical Context

There is no single "inventor" of DWMA; it's a natural extension of linear filtering. It represents a higher-order filter that prioritizes recent data (via WMA) but applies a second pass to iron out any remaining wrinkles. It's the heavy artillery of smoothing.

## Architecture & Physics

DWMA applies a linear weight kernel (triangle window) twice.

1. **Pass 1**: Calculate WMA of the price.
2. **Pass 2**: Calculate WMA of the result from Pass 1.

The effective window size is roughly $2 \times \text{Period}$, and the lag is cumulative. This is not for high-frequency scalping; this is for determining if the market is actually bullish or just having a manic episode.

## Mathematical Foundation

$$ \text{WMA}_1 = \text{WMA}(P, N) $$

$$ \text{DWMA} = \text{WMA}(\text{WMA}_1, N) $$

The weight profile of a single WMA is triangular. The weight profile of a DWMA approaches a Gaussian-like shape (central limit theorem in action), but heavily skewed towards recent data due to the WMA's linear weighting.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

DWMA chains two WMA instances. Each WMA is O(1) with ~22 cycles (see WMA.md).

| Component | Operations | Cost (cycles) |
| :--- | :--- | :---: |
| WMA₁(Price) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| WMA₂(WMA₁) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| **Total** | **8 ADD/SUB, 2 MUL, 2 DIV** | **~44 cycles** |

**Hot path breakdown:**
- First WMA smooths the raw price → ~22 cycles
- Second WMA smooths the first WMA's output → ~22 cycles
- No additional combining math required

### Batch Mode (SIMD)

Each WMA component benefits from SIMD prefix-sum optimization:

| Component | Scalar (512 bars) | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| WMA₁ prefix sum | ~11K cycles | ~2.8K cycles | ~4× |
| WMA₂ prefix sum | ~11K cycles | ~2.8K cycles | ~4× |
| **Total** | **~22K** | **~5.6K** | **~4×** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches chained WMA exactly |
| **Timeliness** | 3/10 | Significant lag; double smoothing delays signals |
| **Overshoot** | 10/10 | Never overshoots input data range (FIR property) |
| **Smoothness** | 9/10 | Very smooth; approaches Gaussian-like profile |

### Zero-Allocation Design

DWMA is implemented by chaining two `Wma` instances. Since `Wma` is zero-allocation, DWMA inherits this property.

## Validation

Validated against chained WMA implementations in standard libraries.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against `WMA(WMA)`. |
| **Skender** | ✅ | Validated against chained `GetWma`. |
| **TA-Lib** | ✅ | Validated against chained `TA_WMA`. |
| **Tulip** | ✅ | Validated against chained `wma`. |
| **Ooples** | ✅ | Validated against chained `CalculateWeightedMovingAverage`. |

### C# Implementation Considerations

The QuanTAlib DWMA implementation leverages composition by chaining two WMA instances, inheriting their O(1) streaming performance:

#### Composition Pattern

DWMA delegates all calculation to two internal WMA instances:

```csharp
[SkipLocalsInit]
public sealed class Dwma : AbstractBase
{
    private readonly int _period;
    private readonly Wma _wma1;
    private readonly Wma _wma2;

    public Dwma(int period)
    {
        _wma1 = new Wma(period);
        _wma2 = new Wma(period);
        WarmupPeriod = (period * 2) - 1;  // Cumulative warmup
    }
}
```

#### Minimal Update Logic

The streaming update is extremely simple - just two WMA calls:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public override TValue Update(TValue input, bool isNew = true)
{
    if (isNew) _sampleCount++;

    TValue wma1Result = _wma1.Update(input, isNew);
    Last = _wma2.Update(wma1Result, isNew);
    PubEvent(Last, isNew);
    return Last;
}
```

This design automatically inherits WMA's bar correction capability - when `isNew=false` is passed, both internal WMAs correctly roll back their state.

#### ArrayPool for Batch Intermediate Buffer

The static `Calculate` method uses a temporary buffer for the intermediate WMA result:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
{
    int len = source.Length;

    double[]? tempArray = len > 1024 ? ArrayPool<double>.Shared.Rent(len) : null;
    Span<double> temp = len <= 1024
        ? stackalloc double[len]
        : tempArray!.AsSpan(0, len);

    try
    {
        Wma.Batch(source, temp, period);   // First pass
        Wma.Batch(temp, output, period);   // Second pass
    }
    finally
    {
        if (tempArray != null) ArrayPool<double>.Shared.Return(tempArray);
    }
}
```

The threshold (1024) is chosen to balance stack safety vs. allocation overhead.

#### State Restoration After Batch

Batch processing restores streaming state by replaying recent bars:

```csharp
public override TSeries Update(TSeries source)
{
    // Batch calculate
    Calculate(source.Values, vSpan, _period);

    // Reset internal state
    Reset();

    // Replay recent bars to restore streaming state
    int lookback = WarmupPeriod + 10;
    int startIndex = Math.Max(0, len - lookback);
    for (int i = startIndex; i < len; i++)
    {
        Update(new TValue(source.Times[i], source.Values[i]));
    }

    _sampleCount = len;
    return new TSeries(t, v);
}
```

#### Disposal Pattern

Event subscription is properly cleaned up on disposal:

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing && _source != null && _handler != null)
    {
        _source.Pub -= _handler;
    }
    base.Dispose(disposing);
}
```

#### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_period` | `int` | 4 | Window size |
| `_wma1` | `Wma` | 8 (ref) | First WMA stage |
| `_wma2` | `Wma` | 8 (ref) | Second WMA stage |
| `_source` | `ITValuePublisher?` | 8 (ref) | Event source |
| `_handler` | `TValuePublishedHandler?` | 8 (ref) | Event handler |
| `_sampleCount` | `int` | 4 | Sample counter |
| **Total** | | **~40 bytes** | Per instance (excludes WMA internals) |

**Total with WMA internals:** Each WMA instance adds ~48 bytes (see WMA docs), so total is ~136 bytes.

### Common Pitfalls

1. **Lag**: This indicator lags. A lot. Do not use it for entry signals on tight timeframes. Use it for trend filtering (e.g., "only buy if price > DWMA").
2. **Warmup**: It takes roughly $2 \times N$ bars to produce valid data.
3. **Confusion with DEMA**: DEMA = Fast, DWMA = Smooth. Do not mix them up.
