# RSIH: Ehlers Hann-Windowed RSI

> *By replacing Wilder's exponential smoothing with a Hann window, Ehlers produces an RSI that is zero-mean, bounded, and inherently smooth—no supplemental filtering required.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                       |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 14)            |
| **Outputs**      | Single series (Rsih)             |
| **Output range** | [-1, +1]                         |
| **Zero mean**    | Yes                              |
| **Warmup**       | `period + 1` bars                |
| **PineScript**   | [rsih.pine](rsih.pine)           |

- RSIH (Hann-Windowed RSI) is a zero-mean relative strength oscillator that uses Hann window coefficients to weight price differences, producing a bounded [-1, +1] output with inherent smoothing.
- **Similar:** [RSI](../../momentum/rsi/Rsi.md), [LRSI](../lrsi/Lrsi.md), [RRSI](../rrsi/Rrsi.md) | **Complementary:** Moving averages for trend confirmation | **Trading note:** Zero crossings signal direction changes; ±0.5 levels indicate strong momentum.
- No external validation libraries implement RSIH. Validated through self-consistency and behavioral testing.

RSIH applies Hann window weighting to consecutive price differences, computes separate weighted sums of up-moves (CU) and down-moves (CD), then normalizes as (CU - CD) / (CU + CD). The Hann window provides inherent smoothing that eliminates the need for supplemental filtering, while the normalization produces a zero-mean output bounded to [-1, +1].

## Historical Context

The Hann-Windowed RSI was published by John F. Ehlers in the January 2022 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "(Yet Another) Improved RSI." Ehlers observed that classic RSI suffers from two fundamental issues: (1) Wilder's exponential smoothing introduces lag and spectral leakage, and (2) the 0–100 output range obscures the zero-mean nature of momentum. By replacing the smoothing with a Hann window FIR filter and using a symmetric [-1, +1] normalization, Ehlers created an RSI variant that is both mathematically cleaner and practically more responsive.

## Architecture & Physics

RSIH operates as a single-stage FIR filter:

### Hann Window Coefficients

The weighting function is precomputed in the constructor:

$$ w(k) = 1 - \cos\left(\frac{2\pi k}{N + 1}\right) \quad \text{for } k = 1, 2, \ldots, N $$

where $N$ is the period. Note: Ehlers uses $(N + 1)$ in the denominator, not the standard symmetric Hann formula $(N - 1)$.

### Weighted CU/CD Accumulation

For each bar, consecutive price differences are weighted by the Hann coefficients:

$$ \text{CU} = \sum_{k=1}^{N} w(k) \cdot \max(\text{Close}_{t-k+1} - \text{Close}_{t-k}, \; 0) $$

$$ \text{CD} = \sum_{k=1}^{N} w(k) \cdot \max(\text{Close}_{t-k} - \text{Close}_{t-k+1}, \; 0) $$

### Normalization

$$ \text{RSIH}_t = \frac{\text{CU} - \text{CD}}{\text{CU} + \text{CD}} $$

When $\text{CU} + \text{CD} = 0$ (flat market), RSIH returns 0.

Implemented with FMA for the coefficient multiplication:

```csharp
cu = Math.FusedMultiplyAdd(w, diff, cu);
```

## Performance Profile

RSIH is an O(N) FIR filter — each bar requires scanning the full window.

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| **Hann Window Scan** | | | |
| SUB (newer - older) | N | 1 | N |
| CMP (diff > 0, diff < 0) | N | 1 | N |
| FMA (w × diff + acc) | N | 4 | 4N |
| **Normalization** | | | |
| ADD (CU + CD) | 1 | 1 | 1 |
| SUB (CU - CD) | 1 | 1 | 1 |
| DIV (ratio) | 1 | 15 | 15 |
| **Total** | | | **~6N + 17 cycles** |

For default N=14: ~101 cycles per bar.

**Dominant cost:** FMA loop (4N cycles, ~67%)

### Batch Mode (SIMD Analysis)

RSIH is **not SIMD-parallelizable** across bars because each bar's window overlaps with adjacent bars. However, the inner loop (coefficient × difference accumulation) could potentially benefit from SIMD vectorization within a single bar's computation.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Hann window provides excellent spectral properties |
| **Timeliness** | 8/10 | FIR filter with minimal lag for oscillator class |
| **Overshoot** | 9/10 | Bounded [-1, +1] — no possibility of divergence |
| **Smoothness** | 8/10 | Hann window provides inherent anti-aliasing |

## Validation

RSIH is not implemented in mainstream libraries. Validation relies on behavioral testing.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Behavioral** | ✅ | Validated: constant→zero, symmetry, mode consistency |

### Behavioral Test Summary

- **Constant Input → Zero**: Constant close → all diffs = 0 → CU = CD = 0 → RSIH = 0
- **Output Symmetry**: RSIH(ascending) = -RSIH(descending) — output is antisymmetric
- **Bounded Output**: All outputs in [-1, +1] regardless of input magnitude
- **Mode Consistency**: Streaming, batch, span, and event-driven modes produce identical results
- **Bar Correction**: Snapshot/Restore via RingBuffer produces exact rollback

## Common Pitfalls

1. **Warmup Period**: RSIH requires `Period + 1` bars to fill the close buffer for `Period` differences. Use `IsHot` to detect readiness.

2. **Hann Window Denominator**: Ehlers uses `(period + 1)` in the Hann formula, NOT the standard symmetric `(period - 1)`. Using the wrong denominator will produce incorrect coefficients.

3. **Zero-Mean Output**: Unlike classic RSI (0–100), RSIH oscillates around zero with range [-1, +1]. Overbought/oversold levels should be set around ±0.5, not 70/30.

4. **FIR Complexity**: RSIH is O(N) per bar, not O(1) like IIR indicators. For very large periods, this may impact performance in high-frequency applications.

5. **Flat Market Edge Case**: When all prices in the window are identical, CU + CD = 0. The implementation returns 0.0 in this case (using an epsilon floor of 1e-10).

6. **Period Selection**: Ehlers recommends using the dominant cycle period (not half-cycle like classic RSI). Default period of 14 works well for daily charts.

7. **Bar Correction**: Like all QuanTAlib indicators, RSIH supports bar correction via the `isNew` parameter. The RingBuffer `Snapshot()`/`Restore()` mechanism handles this atomically.
