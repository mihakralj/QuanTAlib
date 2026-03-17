# MADH: Ehlers Moving Average Difference with Hann

> *By comparing a short Hann-windowed FIR average to a long one, Ehlers creates a MACD-like oscillator that is pure FIR — no recursive lag, no parameter interaction, and inherently smooth.*

| Property         | Value                                    |
| ---------------- | ---------------------------------------- |
| **Category**     | Oscillator                               |
| **Inputs**       | Source (close)                           |
| **Parameters**   | `shortLength` (default 8), `dominantCycle` (default 27) |
| **Derived**      | `longLength = (int)(shortLength + dominantCycle / 2.0)` |
| **Outputs**      | Single series (Madh)                     |
| **Output range** | Unbounded, zero-centered (typically ±5%) |
| **Zero mean**    | Yes                                      |
| **Warmup**       | `longLength + 1` bars                    |
| **PineScript**   | [madh.pine](madh.pine)                   |

- MADH (Moving Average Difference with Hann) is a zero-centered percentage oscillator comparing two Hann FIR averages of different lengths. Valleys indicate buy signals, peaks indicate sell signals.
- **Similar:** [MACD](../../momentum/macd/Macd.md), [APO](../../momentum/apo/Apo.md), [DECO](../deco/Deco.md) | **Complementary:** Moving averages for trend confirmation | **Trading note:** Zero crossings signal trend changes; extreme values indicate overextension.
- No external validation libraries implement MADH. Validated through self-consistency and behavioral testing.

MADH applies two Hann-windowed FIR filters of different lengths to the close price, then computes the percentage difference. The short filter responds faster to price changes while the long filter represents the trend. Their divergence signals directional momentum.

## Historical Context

The Moving Average Difference with Hann was published by John F. Ehlers in the November 2021 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "The MAD Indicator, Enhanced." This builds on his October 2021 MAD indicator by replacing simple averages with Hann-windowed FIR filters. The Hann window provides optimal spectral properties — eliminating sidelobe leakage that can cause false signals. The long length is derived from the short length plus half the dominant cycle, creating a natural relationship between the two filter windows.

## Architecture & Physics

MADH operates as a dual-stage FIR filter with percentage normalization:

### Hann Window Coefficients

Two sets of coefficients are precomputed in the constructor:

$$ w(k) = 1 - \cos\left(\frac{2\pi k}{N + 1}\right) \quad \text{for } k = 1, 2, \ldots, N $$

where $N$ is the filter length (short or long). Note: Ehlers uses $(N + 1)$ in the denominator.

### Dual FIR Filters

For each bar, two weighted averages are computed:

$$ \text{Filt}_1 = \frac{\sum_{k=1}^{N_s} w_s(k) \cdot \text{Close}_{t-k+1}}{\sum_{k=1}^{N_s} w_s(k)} $$

$$ \text{Filt}_2 = \frac{\sum_{k=1}^{N_l} w_l(k) \cdot \text{Close}_{t-k+1}}{\sum_{k=1}^{N_l} w_l(k)} $$

### Percentage Difference

$$ \text{MADH}_t = 100 \cdot \left(\frac{\text{Filt}_1}{\text{Filt}_2} - 1\right) $$

When $\text{Filt}_2 = 0$, MADH returns 0.

Implemented with FMA for the coefficient multiplication:

```csharp
sumShort = Math.FusedMultiplyAdd(_hannShort[k - 1], val, sumShort);
```

## Performance Profile

MADH is an O(LongLength) FIR filter — each bar requires scanning both filter windows.

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| **Short FIR Scan** | | | |
| FMA (w × val + acc) | Ns | 4 | 4Ns |
| **Long FIR Scan** | | | |
| FMA (w × val + acc) | Nl | 4 | 4Nl |
| **Normalization** | | | |
| DIV (filt1 / coefSum) | 2 | 15 | 30 |
| DIV (filt1 / filt2) | 1 | 15 | 15 |
| SUB + MUL (percentage) | 2 | 2 | 4 |
| **Total** | | | **~4(Ns+Nl) + 49 cycles** |

For defaults Ns=8, Nl=21: ~165 cycles per bar.

**Dominant cost:** FMA loops (~73%)

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Hann window provides excellent spectral properties |
| **Timeliness** | 8/10 | FIR filter with minimal lag for oscillator class |
| **Overshoot** | 7/10 | Unbounded output — percentage can be large |
| **Smoothness** | 9/10 | Hann window provides inherent anti-aliasing |

## Validation

MADH is not implemented in mainstream libraries. Validation relies on behavioral testing.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Behavioral** | ✅ | Validated: constant→zero, mode consistency, trending signals |

### Behavioral Test Summary

- **Constant Input → Zero**: Constant close → Filt1 = Filt2 = constant → MADH = 0
- **Trending Up → Positive**: Short average leads long → positive percentage
- **Trending Down → Negative**: Short average lags below long → negative percentage
- **Mode Consistency**: Streaming, batch, span, and event-driven modes produce identical results
- **Bar Correction**: Snapshot/Restore via RingBuffer produces exact rollback

## Common Pitfalls

1. **Warmup Period**: MADH requires `longLength + 1` bars. Use `IsHot` to detect readiness.

2. **Long Length Derivation**: `longLength = (int)(shortLength + dominantCycle / 2.0)` uses integer truncation, not rounding.

3. **Unbounded Output**: Unlike RSIH, MADH output is not bounded. Extreme trends can produce large percentage values.

4. **Dual FIR Complexity**: MADH is O(Ns + Nl) per bar, scanning both filter windows. For large dominant cycles, this may impact performance.

5. **Dominant Cycle Selection**: Ehlers recommends estimating the dominant cycle from the data. Default of 27 works for typical daily charts.

6. **Zero Crossing**: The primary trading signal. Positive MADH indicates short average > long average (bullish); negative indicates bearish.

7. **Bar Correction**: Like all QuanTAlib indicators, MADH supports bar correction via the `isNew` parameter. The RingBuffer `Snapshot()`/`Restore()` mechanism handles this atomically.
