# EDCF: Ehlers Distance Coefficient Filter

## Overview

The **Ehlers Distance Coefficient Filter (EDCF)** is a nonlinear adaptive FIR filter created by John F. Ehlers. Unlike traditional moving averages with fixed or linearly-varying weights, EDCF computes its coefficients dynamically based on the sum of squared price differences across the observation window. This makes the filter highly responsive to price changes while degenerating to a Simple Moving Average when prices are flat.

## Origin

- **Author:** John F. Ehlers
- **Source:** "Ehlers Filters" (MESA Software); "Nonlinear Ehlers Filters" (Stocks & Commodities V.19:4, pp.25-34)
- **Category:** Filters (nonlinear FIR)

## Algorithm

For a window of `Length` samples, the filter computes:

1. **Distance-squared coefficient** for each sample position `i`:

   ```
   Distance2[i] = Σ (Price[i] - Price[i + k])²    for k = 1 to Length-1
   ```

2. **Normalized weighted average**:

   ```
   EDCF = Σ(Distance2[i] × Price[i]) / Σ(Distance2[i])
   ```

### Key Properties

| Property | Behavior |
|----------|----------|
| **Flat prices** | All coefficients are zero → fallback to current price (SMA-like) |
| **Trending prices** | Recent samples with large price changes get higher weights → faster response |
| **Step function** | Responds much faster than SMA to abrupt price changes |
| **Sum of squares** | Uses `Σ(diff²)` instead of `√(Σ(diff²))` to heighten filter response (per Ehlers) |

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `length` | int | 15 | ≥ 2 | Filter window length. Larger = smoother but more lag. |

## Usage

```csharp
// Streaming
var edcf = new Edcf(15);
foreach (var bar in data)
{
    TValue result = edcf.Update(bar);
}

// Batch
TSeries results = Edcf.Batch(series, 15);

// Span
Edcf.Batch(sourceSpan, destinationSpan, 15);

// Calculate (returns both results and indicator)
var (results, indicator) = Edcf.Calculate(series, 15);

// Event-based chaining
var source = new Ema(period: 10);
var edcf = new Edcf(source, 15);
```

## Architecture

```
AbstractBase (ITValuePublisher, IDisposable)
  └── Edcf (sealed)
        ├── RingBuffer[2*length-1]  — price history window
        ├── State record struct     — minimal state (LastValid, Count)
        ├── CalcDistanceFilter()    — O(n²) nested loop computation
        └── Snapshot/Restore        — bar correction support
```

### State Management

- **Record struct** with `LastValid` and `Count` fields
- **Snapshot/Restore** via `_ps`/`_s` swap + `RingBuffer.Snapshot()`/`Restore()` for bar correction
- **No SIMD** possible due to data-dependent coefficient computation

### Complexity

| Operation | Complexity |
|-----------|-----------|
| Per-bar update | O(n²) where n = Length |
| Memory | O(n) — single RingBuffer |
| Warmup | `Length` bars |

## Quality Metrics

| Metric | Value |
|--------|-------|
| WarmupPeriod | `Length` |
| NaN/Infinity handling | Substitutes last-valid value |
| Bar correction | Full save/restore via Snapshot |
| Mode consistency | Streaming = Batch = Span |

## Pitfalls

1. **O(n²) complexity**: For large `Length` values (> 50), the nested loop becomes expensive. Consider keeping Length ≤ 30 for real-time use.
2. **All-zero coefficients**: When all prices in the window are identical, all distance-squared coefficients are zero. The implementation falls back to the current price.
3. **Not an IIR filter**: Despite being classified under Filters, EDCF is a nonlinear FIR filter — it uses a finite observation window with no feedback.
4. **Asymmetric response**: In a strong trend, the filter weights recent bars heavily, creating trailing-stop-like behavior. In ranges, it approximates SMA.

## See Also

- [Wiener Filter](../wiener/Wiener.md) — adaptive noise-reduction filter
- [Laguerre Filter](../laguerre/Laguerre.md) — Ehlers IIR filter with gamma damping
- [LMS Filter](../lms/Lms.md) — Least Mean Squares adaptive filter

## References

1. Ehlers, J. F. "Ehlers Filters." MESA Software. [PDF](https://www.mesasoftware.com/papers/EhlersFilters.pdf)
2. Ehlers, J. F. "Nonlinear Ehlers Filters." *Stocks & Commodities*, V.19:4, pp.25-34.
