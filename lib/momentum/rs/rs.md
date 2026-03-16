# RS: Price Relative Strength

> *Price relative strength ratios two instruments, revealing which one leads and which one lags in the race.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `smoothPeriod` (default 1)                      |
| **Outputs**      | Single series (RS)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `smoothPeriod` bars                          |
| **PineScript**   | [rs.pine](rs.pine)                       |

- **Category:** Momentum **Also known as:** Relative Strength Comparison, Price Ratio, Performance Ratio
- **Similar:** [ROC](../roc/Roc.md), [Bias](../bias/Bias.md) | **Complementary:** Relative strength vs benchmark | **Trading note:** Price Relative Strength; ratio or spread between two series. Sector rotation tool.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

**Category:** Momentum  
**Also known as:** Relative Strength Comparison, Price Ratio, Performance Ratio

[Pine Script Implementation of RS](https://github.com/mihakralj/pinescript/blob/main/indicators/momentum/rs.pine)

## Overview

Price Relative Strength (RS) measures the performance of one asset relative to another by calculating the ratio between their prices. This indicator helps identify which asset is outperforming and is fundamental for sector rotation, pairs trading, and relative performance analysis.

Unlike the Relative Strength Index (RSI) which measures momentum within a single asset, RS compares two different price series. A rising RS indicates the base asset is outperforming the comparison asset; a falling RS indicates underperformance.

## Core Concepts

- **Ratio-based:** Simply divides base price by comparison price
- **Trend interpretation:** Rising = outperformance, Falling = underperformance
- **Optional smoothing:** EMA with bias compensation removes noise while maintaining responsiveness
- **Scale-independent:** Can compare assets with vastly different price levels
- **Cross-market analysis:** Compare stocks, indices, commodities, or any tradeable assets

## Parameters

| Parameter | Default | Description | When to Adjust |
|-----------|---------|-------------|----------------|
| **smoothPeriod** | 1 | EMA smoothing period (1 = no smoothing) | Increase for trend analysis, decrease for signal sensitivity |

**Pro Tip:** Use smoothPeriod=1 for raw ratio analysis, smoothPeriod=10-20 for trend identification, and smoothPeriod=50+ for longer-term relative strength trends.

## Calculation

**Simplified Explanation:**
RS divides the base asset's price by the comparison asset's price, then optionally smooths the result with an EMA.

**Technical Formula:**

```
Raw Ratio = Base Price / Comparison Price
Smoothed = EMA(Raw Ratio, smoothPeriod)
```

With bias-compensated EMA for accurate warmup:
```
α = 2 / (smoothPeriod + 1)
EMA_biased = α × (ratio - EMA_prev) + EMA_prev
compensation = 1 / (1 - (1-α)^n)
Smoothed = compensation × EMA_biased
```

> **Implementation Note:** When smoothPeriod=1, the raw ratio is returned without any smoothing. Division by zero (comparison = 0) returns NaN.

## Interpretation Details

**Trend Analysis:**
- **RS rising:** Base asset outperforming comparison (bullish for base)
- **RS falling:** Base asset underperforming comparison (bearish for base)
- **RS flat:** Both assets moving in tandem

**Common Comparisons:**
- Stock vs. sector ETF (stock relative to its sector)
- Sector vs. broad market index (sector rotation)
- Growth vs. Value ETFs (style performance)
- Emerging markets vs. developed markets
- Commodity vs. currency (inflation dynamics)

**Trading Signals:**
- **RS crossover above prior high:** Breakout in relative strength
- **RS crossover below prior low:** Breakdown in relative strength
- **Divergence:** Price makes new high, RS does not = warning

## Performance Profile

### Operation Count (Streaming Mode)

RS with smoothing is three scalar operations: one division for the raw ratio, one FMA for the EMA update, and one divide for the bias compensation factor. Without smoothing (smoothPeriod = 1), the bias step is skipped entirely.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (IsFinite checks) | 2 | 1 | ~2 |
| Raw ratio: base / comparison | 1 | 8 | ~8 |
| EMA update (FMA: α×ratio + decay×prev) | 1 | 4 | ~4 |
| Bias factor update (1 − (1−α)^n) | 1 | 5 | ~5 |
| Compensated output (ema / bias) | 1 | 8 | ~8 |
| **Total** | **6** | — | **~27 cycles** |

O(1) per bar. The dominant cost is the two floating-point divisions (ratio + bias correction). With smoothPeriod = 1, reduces to ~10 cycles (just the ratio division). WarmupPeriod = smoothPeriod.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Raw ratio (base / comp element-wise) | Yes | `VDIVPD` across full span |
| EMA smoothing pass | No | Recursive IIR dependency; each EMA value depends on previous |
| Bias compensation | Partial | Bias factor is a scalar per-bar sequence; precomputable for batch |
| NaN guard (division by zero) | Yes | `VCMPPD` mask + `VBLENDVPD` for zero-denominator replacement |

The SIMD bottleneck is the recursive EMA. A batch-mode implementation can precompute the raw ratio span via vectorized division (`VDIVPD` at 4 doubles/cycle on AVX2), then apply a scalar EMA sweep for the smoothing pass. This hybrid approach achieves roughly 2× throughput versus fully scalar for large series.

## Limitations and Considerations

- **No absolute measure:** Tells you relative performance, not absolute value
- **Base dependency:** Results depend on which asset is the numerator
- **Lagging indicator:** Smoothed version lags the raw ratio
- **Volume ignored:** Pure price comparison, volume not considered
- **Currency effects:** Cross-currency comparisons may include FX impact

## References

- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Pring, M. J. (2002). *Technical Analysis Explained*. McGraw-Hill.
- Sector rotation and relative strength analysis literature

## See Also

- **ROC/ROCP/ROCR:** Single-asset rate of change variants
- **RSI:** Single-asset momentum oscillator
- **Beta:** Statistical measure of relative volatility
- **Correlation:** Measures how two assets move together