# EPA — Ehlers Phasor Analysis

## Overview

**EPA** (Ehlers Phasor Analysis) extracts cycle phase from price data by computing a phasor using Pearson correlation of a price window against cosine and negative-sine reference waves. The angle of the phasor reveals the current phase position within the dominant cycle, enabling identification of cycle valleys (at −90°) and peaks (at +90°), as well as determining whether the market is cycling or trending.

| Property       | Value                           |
|:-------------- |:------------------------------- |
| **Category**   | Cycles                          |
| **Author**     | John F. Ehlers                  |
| **Source**      | TASC November 2022, "Recurring Phase Of Cycle Analysis" |

## Origin and Sources

John Ehlers introduced Phasor Analysis in the November 2022 issue of *Stocks & Commodities* magazine in the article "Recurring Phase Of Cycle Analysis." The technique uses Pearson correlation as a matched filter to determine how well price data correlates with cosine and sine waves at a presumed cycle period, producing the Real and Imaginary components of a phasor.

## Function Signature

```csharp
// streaming
var epa = new Epa(period: 28);
TValue result = epa.Update(tValue);

// static batch (TSeries)
TSeries output = Epa.Batch(source, period: 28);

// static batch (Span)
Epa.Batch(source, output, period: 28);

// factory
var (results, indicator) = Epa.Calculate(source, period: 28);
```

## Parameters

| Parameter  | Type  | Default | Valid Range | Description                                  |
|:---------- |:----- |:------- |:----------- |:-------------------------------------------- |
| `period`   | int   | 28      | > 1         | Presumed dominant cycle wavelength in bars    |

## Outputs

| Output          | Type   | Description                                                     |
|:--------------- |:------ |:--------------------------------------------------------------- |
| `Angle`         | double | Phasor angle in degrees with wraparound compensation            |
| `DerivedPeriod` | double | Cycle period derived from angle rate-of-change (clamped to 60)  |
| `TrendState`    | int    | +1 = trending long, −1 = trending short, 0 = cycling           |

The primary output (`Last.Value`) is the **Angle**.

## Algorithm

1. **Dual Pearson Correlation** over a sliding window of `period` bars:
   - `Real = corr(price, cos(2πk/N))` — correlation with cosine
   - `Imag = corr(price, -sin(2πk/N))` — correlation with negative sine

2. **Angle Calculation**: `Angle = 90° - atan(Imag/Real)` with quadrant fix: if `Real < 0`, subtract 180°.

3. **Wraparound Compensation**: When the angle crosses the 360° boundary (previous angle > 90° and current < −90°), subtract 360° to maintain continuity.

4. **Monotonic Constraint**: The angle generally cannot decrease, but allows exceptions at extreme regions (when both previous and current angles are in the same deep-negative quadrant).

5. **Derived Period**: Computed as `360 / ΔAngle` where `ΔAngle` is the per-bar angle change. When `ΔAngle ≤ 0`, the previous delta is used. The result is clamped to a maximum of 60.

6. **Trend State**: When the angle rate-of-change ≤ 6°/bar:
   - If angle ≥ 90° or ≤ −90° → **+1** (trending long)
   - If −90° < angle < 90° → **−1** (trending short)
   - Otherwise → **0** (cycling)

## Interpretation

- The phasor angle oscillates between −180° and +180°, completing one full cycle per dominant period.
- **Cycle valleys** correspond to the angle crossing −90°.
- **Cycle peaks** correspond to the angle near +90°.
- The **TrendState** indicates when the market transitions from cycling to trending behavior based on the angle rate slowing.
- The **DerivedPeriod** provides a real-time estimate of the dominant cycle length.

## Properties

| Property       | Value                                       |
|:-------------- |:------------------------------------------- |
| Complexity     | O(period) per bar                            |
| Memory         | O(period) — RingBuffer + trig tables         |
| Warmup         | `period` bars                                |
| Output Range   | Angle: unbounded; DerivedPeriod: [0, 60]; TrendState: {−1, 0, +1} |
| Zero Alloc     | ✅ Hot path allocates nothing                |

## Related Indicators

- [CCOR](../ccor/ccor.md) — Ehlers Correlation Cycle (TASC June 2020) — earlier version with simpler angle logic
- [HT_PHASOR](../ht_phasor/ht_phasor.md) — Hilbert Transform Phasor Components — different algorithm
- [FSI](../fsi/fsi.md) — Ehlers Fourier Series Indicator
- [EBSW](../ebsw/ebsw.md) — Ehlers Even Better Sine Wave
