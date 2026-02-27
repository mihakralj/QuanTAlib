# PIVOTWOOD: Woodie's Pivot Points

## Overview

Woodie's Pivot Points weight the closing price twice in the pivot calculation, biasing the central pivot toward where the market actually settled rather than treating high, low, and close equally. This close-weighted approach gives more emphasis to recent price action, making the pivot levels more responsive to the prior bar's close.

## Origin and Sources

- **Creator**: Ken Wood (Woodie), active trader and educator
- **Foundation**: Modification of classic floor-trader pivot points with double-weighted close
- **Philosophy**: The close is the most important price of the bar because it represents consensus; weighting it twice reflects this belief

## Formula

Using previous bar's High (H), Low (L), Close (C):

```
PP    = (H + L + 2×C) / 4
range = H - L

R1 = 2×PP - L                    S1 = 2×PP - H
R2 = PP + range                   S2 = PP - range
R3 = H + 2×(PP - L)              S3 = L - 2×(H - PP)
```

### Known Values Example

For H = 110, L = 90, C = 100:

- PP = (110 + 90 + 200) / 4 = 100.0, range = 20
- R1 = 200 - 90 = 110.0, S1 = 200 - 110 = 90.0
- R2 = 100 + 20 = 120.0, S2 = 100 - 20 = 80.0
- R3 = 110 + 2×10 = 130.0, S3 = 90 - 2×10 = 70.0

### Close-Weight Effect

When close differs from the midpoint of the range, Woodie's PP shifts toward the close:

- Classic PP (H=120, L=80, C=110): (120+80+110)/3 = 103.33
- Woodie PP (H=120, L=80, C=110): (120+80+220)/4 = 105.00

The 1.67-point difference biases all derived levels toward the close.

## Key Properties

- **Close bias**: PP shifts toward close when close != (H+L)/2
- **Level ordering**: S3 < S2 < S1 < PP < R1 < R2 < R3 (when range > 0)
- **R2/S2 symmetry**: R2 - PP = PP - S2 = range (always symmetric)
- **R1/S1 asymmetry**: R1 - PP = PP - L, PP - S1 = H - PP (asymmetric unless close = midpoint)
- **R3/S3**: Widest levels, incorporating both the previous high/low and the pivot distance

## Usage

```csharp
// Streaming
var wood = new Pivotwood();
var result = wood.Update(bar);
double pp = wood.PP;
double r1 = wood.R1;  // First resistance
double r2 = wood.R2;  // Second resistance
double r3 = wood.R3;  // Third resistance
double s1 = wood.S1;  // First support
double s2 = wood.S2;  // Second support
double s3 = wood.S3;  // Third support

// Batch
var results = Pivotwood.Batch(bars);

// All 7 levels at once
Pivotwood.BatchAll(high, low, close, ppOut, r1Out, s1Out, r2Out, s2Out, r3Out, s3Out);
```

## Comparison with Other Pivot Variants

| Variant | PP Formula | R/S Formula | Levels | Key Difference |
|---------|-----------|------------|--------|----------------|
| **PIVOT** (Classic) | (H+L+C)/3 | Arithmetic from PP | 7 | Equal HLC weight |
| **PIVOTWOOD** | (H+L+2C)/4 | Mixed arithmetic | 7 | Close weighted 2x |
| **PIVOTFIB** | (H+L+C)/3 | Fibonacci x range | 7 | 0.382, 0.618, 1.000 ratios |
| **PIVOTCAM** (Camarilla) | (H+L+C)/3 | Close +/- ratio x range | 9 | 1.1/12 series |
| **PIVOTEXT** (Extended) | (H+L+C)/3 | Arithmetic extended | 11 | 1x-4x range |
| **PIVOTDEM** (DeMark) | Conditional X/4 | X/2 based | 3 | Direction-based |


## Performance Profile

### Operation Count (Streaming Mode)

Pivot Woodie uses a distinctive formula weighting Close *2 in the pivot — O(1) arithmetic on previous bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Store prev bar OHLC | 4 | 1 cy | ~4 cy |
| PP = (H + L + 2*C) / 4 (FMA) | 1 | 1 cy | ~1 cy |
| R1 = 2*PP - L | 1 | 2 cy | ~2 cy |
| S1 = 2*PP - H | 1 | 2 cy | ~2 cy |
| R2 = PP + (H - L) | 1 | 2 cy | ~2 cy |
| S2 = PP - (H - L) | 1 | 2 cy | ~2 cy |
| R3/S3 additional levels | 2 | 2 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~19 cy** |

O(1) per bar. Woodie pivot uses `(H + L + 2×Close) / 4` instead of `(H + L + C) / 3`, giving close price double weight. FMA-friendly.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| PP with FMA (2*C+H+L)/4 | Yes | Vector<double> FMA with broadcast constant |
| R1/S1 subtraction | Yes | Vector arithmetic, no dependencies |
| R2/S2 range-based | Yes | Vector<double> subtract and add |
| All output spans | Yes | Full SIMD pass across all bars |

Full vectorization possible. All output levels computed from previous-bar constants — no streaming dependency between bars in batch mode.

## Implementation Details

- **WarmupPeriod**: 2 bars (need previous bar's HLC)
- **Parameters**: None
- **Outputs**: 7 (PP, R1, R2, R3, S1, S2, S3)
- **Input**: TBar (OHLCV)
- **Complexity**: O(1) per bar
- **Uses FMA**: `Math.FusedMultiplyAdd` for R1, S1, R3, S3 computations
