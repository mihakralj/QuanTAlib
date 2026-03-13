# PIVOTFIB: Fibonacci Pivot Points

> *Fibonacci pivots project support and resistance from the golden ratio, blending numerology with price structure.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (PIVOTFIB)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [pivotfib.pine](pivotfib.pine)                       |

- Fibonacci Pivot Points apply Fibonacci retracement ratios (38.2%, 61.8%, 100%) to the standard pivot point formula.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Overview
Fibonacci Pivot Points apply Fibonacci retracement ratios (38.2%, 61.8%, 100%) to the standard pivot point formula. The central pivot (PP) uses the classic HLC/3 calculation, while support and resistance levels are derived by adding or subtracting Fibonacci proportions of the previous bar's trading range.

## Origin and Sources
- **Concept**: Adaptation of Leonardo Fibonacci's ratios (derived from the Fibonacci sequence) to traditional pivot point analysis
- **Foundation**: Standard pivot points combined with Fibonacci retracement levels (0.382, 0.618, 1.000)

## Formula

Using previous bar's High (H), Low (L), Close (C):

```
PP    = (H + L + C) / 3
range = H - L

R1 = PP + 0.382 × range       S1 = PP - 0.382 × range
R2 = PP + 0.618 × range       S2 = PP - 0.618 × range
R3 = PP + 1.000 × range       S3 = PP - 1.000 × range
```

### Known Values Example
For H = 110, L = 90, C = 100:
- PP = 100.0, range = 20
- R1 = 107.64, S1 = 92.36
- R2 = 112.36, S2 = 87.64
- R3 = 120.00, S3 = 80.00

## Key Properties
- **Symmetry**: R_n - PP = PP - S_n for all levels
- **Level ordering**: S3 < S2 < S1 < PP < R1 < R2 < R3 (when range > 0)
- **Fibonacci ratios**: Distances from PP are proportional to 0.382, 0.618, and 1.000 of the range
- **Golden ratio relationship**: 0.618 ≈ φ - 1, where φ = (1 + √5) / 2; 0.382 = 1 - 0.618

## Usage
```csharp
// Streaming
var fib = new Pivotfib();
var result = fib.Update(bar);
double pp = fib.PP;
double r1 = fib.R1;  // 38.2% resistance
double r2 = fib.R2;  // 61.8% resistance
double r3 = fib.R3;  // 100% resistance
double s1 = fib.S1;  // 38.2% support
double s2 = fib.S2;  // 61.8% support
double s3 = fib.S3;  // 100% support

// Batch
var results = Pivotfib.Batch(bars);

// All 7 levels at once
Pivotfib.BatchAll(high, low, close, ppOut, r1Out, s1Out, r2Out, s2Out, r3Out, s3Out);
```

## Comparison with Other Pivot Variants

| Variant | R/S Formula | Levels | Ratios Used |
|---------|------------|--------|-------------|
| **PIVOT** (Classic) | Arithmetic from PP | 7 | 1×, 2× range |
| **PIVOTFIB** | Fibonacci × range | 7 | 0.382, 0.618, 1.000 |
| **PIVOTCAM** (Camarilla) | Close ± ratio × range | 9 | 1.1/12 series |
| **PIVOTEXT** (Extended) | Arithmetic extended | 11 | 1×–4× range |
| **PIVOTDEM** (DeMark) | Conditional X/4 | 3 | Direction-based |


## Performance Profile

### Operation Count (Streaming Mode)

PivotFib computes PP and 6 Fibonacci S/R levels from previous bar's HLC — O(1) pure arithmetic.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Store prev bar HLC | 3 | 1 cy | ~3 cy |
| PP = (H + L + C) / 3 | 1 | 2 cy | ~2 cy |
| range = H - L | 1 | 1 cy | ~1 cy |
| R1/S1 via FMA (0.382 * range) | 2 | 1 cy | ~2 cy |
| R2/S2 via FMA (0.618 * range) | 2 | 1 cy | ~2 cy |
| R3/S3 = PP +/- range | 2 | 1 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~14 cy** |

Pure O(1) arithmetic on previous-bar data. Fibonacci multipliers 0.382 and 0.618 are precomputed constants; FMA fuses multiply-add into a single instruction.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| PP computation | Yes | Vector<double> (H+L+C)/3 across all bars |
| Range calculation | Yes | Vector subtract H-L |
| Fibonacci level projection | Yes | FMA with broadcast constants 0.382, 0.618 |
| All 7 output spans | Yes | Full SIMD pass — no data dependencies |

Excellent SIMD candidate — all 7 output levels are independent. BatchAll span overload processes 4 bars per AVX2 cycle. Expected 4× throughput vs scalar.

## Implementation Details
- **WarmupPeriod**: 2 bars (need previous bar's HLC)
- **Parameters**: None
- **Outputs**: 7 (PP, R1, R2, R3, S1, S2, S3)
- **Input**: TBar (OHLCV)
- **Complexity**: O(1) per bar
- **Uses FMA**: `Math.FusedMultiplyAdd` for R1/S1/R2/S2 computations