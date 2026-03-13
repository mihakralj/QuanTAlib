# HOLT: Holt Exponential Moving Average

> *Single smoothing tracks level. Double smoothing tracks trend. The elegance is not in complexity but in the admission that yesterday's direction matters.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `gamma` (default 0)                      |
| **Outputs**      | Single series (HOLT)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [holt.pine](holt.pine)                       |
| **Signature**    | [holt_signature](holt_signature.md) |

- Holt's exponential smoothing extends simple exponential smoothing (EMA) by adding a second equation that explicitly tracks the local trend.
- **Similar:** [DEMA](../dema/dema.md), [KAMA](../kama/kama.md) | **Complementary:** Forecast accuracy metrics | **Trading note:** Holt exponential smoothing; level + trend components for forecasting.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Overview

Holt's exponential smoothing extends simple exponential smoothing (EMA) by adding a second equation that explicitly tracks the local trend. The result is a 1-step-ahead forecast that adapts to both the level and direction of the time series. When applied to financial data, HOLT produces a trend-following line that anticipates price continuation rather than merely reacting to it.

## Historical Context

Charles C. Holt published the method in 1957 at the Carnegie Institute of Technology, though the work remained an unpublished ONR report until 2004. The method was independently popularized by Peter Winters (who added seasonality, creating Holt-Winters), but the core two-equation system belongs to Holt. In forecasting literature, this is "double exponential smoothing" — not to be confused with DEMA (which is a different construct using two cascaded EMAs with lag compensation).

The crucial difference from DEMA: Holt explicitly decomposes the signal into level and trend components, then recombines them for forecasting. DEMA applies algebraic lag correction without explicit trend modeling.

## Mathematical Foundation

### Level Equation

$$L_t = \alpha \cdot y_t + (1 - \alpha) \cdot (L_{t-1} + B_{t-1})$$

The level $L_t$ is a weighted average of the current observation and the previous level-plus-trend forecast.

### Trend Equation

$$B_t = \gamma \cdot (L_t - L_{t-1}) + (1 - \gamma) \cdot B_{t-1}$$

The trend $B_t$ is a weighted average of the observed level change and the previous trend estimate.

### Output (1-Step-Ahead Forecast)

$$\text{HOLT}_t = L_t + B_t$$

### Parameter Mapping

| Parameter | Formula | Default | Range |
|-----------|---------|---------|-------|
| Alpha ($\alpha$) | $2 / (\text{period} + 1)$ | period=10 → 0.1818 | (0, 1) |
| Gamma ($\gamma$) | User-specified or $\alpha$ | 0 (auto = $\alpha$) | [0, 1] |

### Special Cases

- **$\gamma = 0$ (auto):** Uses $\gamma = \alpha$, providing balanced level/trend tracking
- **$\gamma \to 0$ (manual):** Trend component freezes; degenerates toward pure EMA
- **$\gamma \to 1$:** Trend reacts instantly to level changes; high noise sensitivity

### Initialization

- **Bar 1:** $L_1 = y_1$, $B_1 = 0$, output $= y_1$
- Subsequent bars use the full equations above

## Interpretation

- **Trend following:** When HOLT is above/below price, the trend component provides a directional bias
- **Crossover signals:** HOLT crossing price suggests trend reversal
- **Lead indicator:** Unlike EMA, HOLT anticipates continuation via the trend term

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `period` | int | 10 | Lookback period for alpha calculation |
| `gamma` | double | 0 | Trend smoothing factor; 0 = auto (uses alpha) |

## Performance Profile

### Operation Count (Streaming Mode)

HOLT(N, γ) tracks both level and trend via two EMA-like updates per bar. The dominant cost is two FMAs (level update and trend update) plus a final addition.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Level: FMA(α, src, decay×(level+trend)) | 1 | 4 | ~4 |
| Level delta: new_level − prev_level | 1 | 1 | ~1 |
| Trend: FMA(γ, delta, gammaDecay×trend) | 1 | 4 | ~4 |
| Output: level + trend | 1 | 1 | ~1 |
| **Total** | **4** | — | **~10 cycles** |

O(1) per bar. One of the fastest trends_IIR indicators — only 4 operations per bar. When γ = 0 (γ defaults to α), the trend EMA degenerates to a standard EMA with no trend correction. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Level update | No | Recursive: depends on previous level + trend |
| Trend update | No | Recursive: depends on previous trend and new level |
| Output (level + trend) | Yes | `VADDPD` once level and trend series complete |

Both state variables are recursive. Batch mode provides no SIMD opportunity beyond the final addition. Holt's method is strictly serial.
| Metric | Value |
|--------|-------|
| Time complexity | O(1) per bar |
| Space complexity | O(1) — level + trend only |
| Allocations | Zero in Update hot path |
| FMA usage | Level and trend equations |
| SIMD potential | Limited (serial dependency) |
| Warmup period | Same as `period` parameter |

## Limitations

1. **Trend overshoot:** In ranging markets, the trend component causes systematic bias (output overshoots actual price during reversals)
2. **Gamma sensitivity:** Small gamma changes dramatically alter behavior; requires careful tuning
3. **No mean reversion:** The additive trend model assumes perpetual directional movement
4. **Initialization sensitivity:** First-bar seeding (level=price, trend=0) means early outputs are biased
5. **Not a filter:** Unlike Butterworth or SSF, Holt has no defined frequency response — it is a forecasting model applied as a filter

## References

- Holt, C. C. (1957). "Forecasting Seasonals and Trends by Exponentially Weighted Moving Averages." ONR Research Memorandum No. 52, Carnegie Institute of Technology.
- Holt, C. C. (2004). "Forecasting Seasonals and Trends by Exponentially Weighted Moving Averages." International Journal of Forecasting, 20(1), 5–10. (Republication of the 1957 report)
- Gardner, E. S. (1985). "Exponential Smoothing: The State of the Art." Journal of Forecasting, 4(1), 1–28.
- Hyndman, R. J. & Athanasopoulos, G. (2021). "Forecasting: Principles and Practice." 3rd ed., OTexts.

## See Also

- [EMA](../ema/Ema.md) — Single exponential smoothing (level only)
- [DEMA](../dema/Dema.md) — Double EMA with algebraic lag correction (different approach)
- [TEMA](../tema/Tema.md) — Triple EMA cascade