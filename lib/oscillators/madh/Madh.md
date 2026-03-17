# MADH: Ehlers Moving Average Difference with Hann

> *By computing the percentage difference between short and long Hann-windowed FIR averages, Ehlers creates a zero-crossing trend oscillator analogous to MACD but with superior spectral properties.*

| Property         | Value                                        |
| ---------------- | -------------------------------------------- |
| **Category**     | Oscillator                                   |
| **Inputs**       | Source (close)                                |
| **Parameters**   | `shortLength` (default 8), `dominantCycle` (default 27) |
| **Outputs**      | Single series (Madh)                         |
| **Output range** | Unbounded (typically ±5%), zero-centered     |
| **Zero mean**    | Yes                                          |
| **Warmup**       | `LongLength` bars                            |
| **PineScript**   | [madh.pine](madh.pine)                       |

- MADH (Moving Average Difference with Hann) computes the percentage difference between a short and long Hann-windowed FIR moving average, producing a zero-crossing trend oscillator similar in concept to MACD but using FIR filters with no spectral leakage.
- **Similar:** [MACD](../../momentum/macd/Macd.md), [APO](../apo/Apo.md), [DECO](../deco/Deco.md) | **Complementary:** [RSIH](../rsih/Rsih.md ) for momentum confirmation | **Trading note:** Zero crossings signal trend changes; peaks/valleys indicate overbought/oversold.
- No external validation libraries implement MADH. Validated through self-consistency and behavioral testing.

MADH applies two separate Hann FIR filters to the close price — a short window and a long window derived from the dominant cycle estimate — then expresses their difference as a percentage: `100 × (Filt1/Filt2 - 1)`. The Hann window eliminates spectral leakage, making MADH more responsive than EMA-based MACD while avoiding Gibbs ringing artifacts.

## Historical Context

The MADH indicator was published by John F. Ehlers in the November 2021 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "The MAD Indicator, Enhanced." It is an enhancement of the basic MAD indicator from October 2021, replacing simple moving averages with Hann-windowed FIR filters. Ehlers demonstrated that the Hann window provides inherent smoothing without the spectral leakage of rectangular or exponential windows, resulting in cleaner trend signals with fewer whipsaws.

## Architecture & Physics

MADH operates as a dual-FIR comparator:

### Parameter Derivation

The long window length is derived from the short length and dominant cycle estimate:

$$ L_{\text{long}} = \text{IntPortion}\left(L_{\text{short}} + \frac{D}{2}\right) $$

where $L_{\text{short}}$ is the short length (default 8) and $D$ is the dominant cycle (default 27).

### Hann Window Coefficients

Two sets of coefficients are precomputed in the constructor:

$$ w(k) = 1 - \cos\left(\frac{2\pi k}{N + 1}\right) \quad \text{for } k = 1, 2, \ldots, N $$

Note: Ehlers uses $(N + 1)$ in the denominator, not the standard symmetric Hann formula $(N - 1)$.

### Dual FIR Filters

$$ \text{Filt1} = \frac{\sum_{k=1}^{L_{\text{short}}} w_s(k) \cdot \text{Close}_{t-k+1}}{\sum_{k=1}^{L_{\text{short}}} w_s(k)} $$

$$ \text{Filt2} = \frac{\sum_{k=1}^{L_{\text{long}}} w_l(k) \cdot \text{Close}_{t-k+1}}{\sum_{k=1}^{L_{\text{long}}} w_l(k)} $$

### Percentage Difference

$$ \text{MADH}_t = 100 \times \left(\frac{\text{Filt1}}{\text{Filt2}} - 1\right) $$

When $\text{Filt2} = 0$ (degenerate case), MADH returns 0.

Implemented with FMA for coefficient multiplication:

```csharp
filt1 = Math.FusedMultiplyAdd(w, _closeBuf[available - k], filt1);
```

## Performance Profile

MADH is an O(LongLength) FIR filter — each bar requires scanning both windows.

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| **Short Hann Scan** | | | |
| FMA (w × close + acc) | Ls | 4 | 4Ls |
| ADD (coef sum) | Ls | 1 | Ls |
| **Long Hann Scan** | | | |
| FMA (w × close + acc) | Ll | 4 | 4Ll |
| ADD (coef sum) | Ll | 1 | Ll |
| **Normalization** | | | |
| DIV (filt1/coef1, filt2/coef2) | 2 | 15 | 30 |
| DIV (filt1/filt2) | 1 | 15 | 15 |
| MUL (× 100) | 1 | 3 | 3 |
| SUB (- 1) | 1 | 1 | 1 |
| **Total** | | | **~5(Ls + Ll) + 49 cycles** |

For defaults Ls=8, Ll=21: ~194 cycles per bar.

**Dominant cost:** FMA loops (4(Ls + Ll) cycles, ~60%)

### Batch Mode (SIMD Analysis)

MADH is **not SIMD-parallelizable** across bars because each bar's window overlaps with adjacent bars. However, the inner coefficient × price accumulation loops could benefit from SIMD vectorization within a single bar.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Hann window provides excellent spectral properties |
| **Timeliness** | 9/10 | FIR filters with minimal group delay |
| **Overshoot** | 7/10 | Unbounded output; can overshoot during sharp moves |
| **Smoothness** | 8/10 | Hann window provides inherent anti-aliasing |

## Validation

MADH is not implemented in mainstream libraries. Validation relies on behavioral testing.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Behavioral** | ✅ | Validated: constant→zero, symmetry, mode consistency |

### Behavioral Test Summary

- **Constant Input → Zero**: Constant close → Filt1 = Filt2 → ratio = 1 → MADH = 0
- **Trending Input → Non-Zero**: Ascending close → short MA leads long MA → positive MADH
- **Direction Symmetry**: MADH(ascending) > 0 and MADH(descending) < 0
- **Mode Consistency**: Streaming, batch, span, and event-driven modes produce identical results
- **Bar Correction**: Snapshot/Restore via RingBuffer produces exact rollback

## Common Pitfalls

1. **Warmup Period**: MADH requires `LongLength` bars to fill the close buffer. Use `IsHot` to detect readiness. With defaults (8, 27), LongLength = 21.

2. **Hann Window Denominator**: Ehlers uses `(N + 1)` in the Hann formula, NOT the standard symmetric `(N - 1)`. Using the wrong denominator will produce incorrect coefficients.

3. **Unbounded Output**: Unlike RSIH (bounded [-1, +1]), MADH is unbounded. During sharp trends, values can exceed ±5%. Do not use fixed overbought/oversold levels.

4. **LongLength Derivation**: Uses integer division: `LongLength = ShortLength + DominantCycle / 2`. For odd DominantCycle values, the result is truncated (e.g., DominantCycle=27 → 27/2=13 → LongLength=21).

5. **Division Safety**: When Filt2 ≈ 0 (near-zero average price), the ratio is undefined. The implementation returns 0.0 using an epsilon floor of 1e-10.

6. **FIR Complexity**: MADH is O(LongLength) per bar, not O(1) like IIR indicators. For very large dominant cycle values, this may impact performance.

7. **Bar Correction**: Like all QuanTAlib indicators, MADH supports bar correction via the `isNew` parameter. The RingBuffer `Snapshot()`/`Restore()` mechanism handles this atomically.
