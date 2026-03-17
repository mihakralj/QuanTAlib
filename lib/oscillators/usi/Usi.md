# USI: Ehlers Ultimate Strength Index

> *Where RSI plods with Wilder's smoothing, USI sprints with the UltimateSmoother — symmetric, lag-free, and ready for the modern trader.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                       |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 28)            |
| **Outputs**      | Single series (Usi)              |
| **Output range** | Bounded [-1, +1]                |
| **Warmup**       | `period + 4` bars                |
| **PineScript**   | [usi.pine](usi.pine)            |

- USI (Ultimate Strength Index) replaces the RSI's Wilder smoothing with Ehlers' UltimateSmoother filter, producing a symmetric oscillator bounded [-1, +1] with significantly reduced lag.
- **Similar:** [RSI](../../momentum/rsi/Rsi.md), [RRSI](../rrsi/Rrsi.md), [RSIH](../rsih/Rsih.md) | **Complementary:** ADX for trend confirmation | **Trading note:** Bullish above 0, bearish below 0; ±0.4 levels indicate strong momentum. Typically uses longer periods than RSI (28 vs 14) for comparable behavior.
- No external validation libraries implement USI. Validated through self-consistency and behavioral testing.

USI is Ehlers' 2024 reimagining of the classic RSI. Instead of Wilder's exponential smoothing, it applies the UltimateSmoother filter — which subtracts high-frequency noise via a highpass filter — to the short-term average of upward and downward price movements. The result is an oscillator that responds to trend changes faster than RSI while maintaining comparable smoothness with a longer lookback.

## Historical Context

John F. Ehlers published the Ultimate Strength Index in the November 2024 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "Ultimate Strength Index (USI)." The article presents USI as a direct replacement for Wilder's RSI, leveraging the UltimateSmoother filter (introduced earlier in April 2024 TASC) to achieve dramatically reduced lag. Unlike RSI's 0-100 range, USI is centered at zero with a [-1, +1] range, making bullish/bearish conditions immediately apparent.

## Architecture & Physics

### Stage 1: Strength Extraction

$$\text{SU} = \max(0, \text{Close} - \text{Close}[1])$$
$$\text{SD} = \max(0, \text{Close}[1] - \text{Close})$$

Simple decomposition of price changes into upward (SU) and downward (SD) components, identical to the RSI approach.

### Stage 2: Short-Term Averaging (SMA-4)

$$\text{avgSU} = \frac{1}{4}\sum_{k=0}^{3}\text{SU}[k]$$
$$\text{avgSD} = \frac{1}{4}\sum_{k=0}^{3}\text{SD}[k]$$

A 4-bar simple moving average smooths the binary SU/SD signals into continuous streams before the UltimateSmoother processes them.

### Stage 3: UltimateSmoother Filter

$$\arg = \frac{\sqrt{2}\pi}{\text{period}}$$
$$a_1 = e^{-\arg}, \quad c_2 = 2a_1\cos(\arg), \quad c_3 = -a_1^2$$
$$c_1 = \frac{1 + c_2 - c_3}{4}$$

$$\text{USU} = (1 - c_1) \cdot \text{avgSU} + (2c_1 - c_2) \cdot \text{avgSU}[1] - (c_1 + c_3) \cdot \text{avgSU}[2] + c_2 \cdot \text{USU}[1] + c_3 \cdot \text{USU}[2]$$
$$\text{USD} = (1 - c_1) \cdot \text{avgSD} + (2c_1 - c_2) \cdot \text{avgSD}[1] - (c_1 + c_3) \cdot \text{avgSD}[2] + c_2 \cdot \text{USD}[1] + c_3 \cdot \text{USD}[2]$$

The same UltimateSmoother IIR filter is applied independently to both the SU and SD paths.

### Stage 4: Symmetric Normalization

$$\text{USI} = \frac{\text{USU} - \text{USD}}{\text{USU} + \text{USD}}, \quad \text{when USU} > \varepsilon \text{ and USD} > \varepsilon$$

The normalization produces a [-1, +1] range (unlike RSI's [0, 100]). When the denominator is near zero or either component is below the minimum threshold (ε = 0.01), the previous USI value is held.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Step | Multiplications | Additions | Total |
|------|----------------|-----------|-------|
| SU/SD extraction | 0 | 1 comparison + 1 subtraction | 2 |
| SMA(4) buffer update | 1 | 3 | 4 |
| USF for SU path | 5 FMA | 0 | 5 |
| USF for SD path | 5 FMA | 0 | 5 |
| Normalization | 1 | 2 | 3 |
| **Total** | **~12** | **~6** | **~19 ops** |

### Batch Mode (SIMD Analysis)

The UltimateSmoother is an IIR filter (output depends on previous outputs), limiting SIMD vectorization. However, the SU/SD extraction and SMA averaging stages could benefit from SIMD in large batches. Current implementation uses scalar FMA for maximum precision.

### Quality Metrics

| Metric | Value |
|--------|-------|
| Complexity | O(1) per bar |
| Memory | ~160 bytes (State struct, no heap) |
| Allocations | Zero in hot path |
| FMA usage | 10 FMA operations (5 per USF path) |

## Validation

| Method | Result |
|--------|--------|
| 4-API consistency | Streaming, batch TSeries, batch Span, Calculate all match |
| Bar correction | State rollback via `_s`/`_ps` pattern |
| Constant input | USI → 0 (equal SU and SD after smoothing converges) |
| Monotonic trend | USI → +1 (all SU, no SD) |
| Monotonic downtrend | USI → -1 (all SD, no SU) |
| NaN handling | Non-finite inputs substituted with last valid value |
| Bounded output | Always in [-1, +1] after warmup |

### Behavioral Test Summary

| Test | Expected |
|------|----------|
| Constant series | USI = 0 (no strength differential) |
| Strong uptrend | USI approaches +1 |
| Strong downtrend | USI approaches -1 |
| Alternating up/down | USI oscillates near 0 |
| Long period smoother | Slower, less noisy response |
| Short period sharper | Faster, more responsive |

## Common Pitfalls

1. **Period too short**: With period < 10, the UltimateSmoother provides insufficient smoothing and USI becomes noisy.
2. **Comparing to RSI periods**: USI typically needs a longer period than RSI for comparable behavior (28 vs 14 is a common mapping).
3. **Zero-denominator regime**: When prices are flat (both USU and USD near zero), USI holds its previous value rather than computing an undefined ratio.
4. **Bootstrap phase**: The first ~8 bars use pass-through instead of the IIR filter, producing unreliable values during warmup.

## References

1. Ehlers, J.F. (2024). "Ultimate Strength Index (USI)." *Technical Analysis of Stocks & Commodities*, November 2024.
2. Ehlers, J.F. (2024). "The Ultimate Smoother." *Technical Analysis of Stocks & Commodities*, March/April 2024.
3. PineScript implementation: [usi.pine](usi.pine)
