# PVT: Price Volume Trend

> *Volume tells you about the intensity of price moves, but PVT tells you what volume is actually accomplishing.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (PVT)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> 2` bars                          |
| **PineScript**   | [pvt.pine](pvt.pine)                       |

- Price Volume Trend refines the OBV concept by weighting volume according to the percentage price change rather than using an all-or-nothing approach.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Price Volume Trend refines the OBV concept by weighting volume according to the percentage price change rather than using an all-or-nothing approach. Where OBV assigns the entire bar's volume to either buyers or sellers, PVT scales the volume contribution by the relative price movement—a 1% move adds only 1% of volume to the running total.

This proportional weighting makes PVT more sensitive to the magnitude of price changes, not just their direction. A large price move with moderate volume registers more strongly than a tiny price move with massive volume—aligning the indicator more closely with price momentum.

## Historical Context

Price Volume Trend emerged as an evolution of On Balance Volume (OBV), addressing what some analysts considered a weakness in Granville's original formulation. The criticism: OBV treats a 0.01% price increase the same as a 10% surge, assigning full volume to either case.

The modification is straightforward: instead of using sign(price change) × volume, use (percentage price change) × volume. This creates a cumulative indicator that:

- Still measures buying/selling pressure via volume
- Weights contributions by the significance of price moves
- Reduces sensitivity to noise (small price changes)
- Amplifies response to significant moves

PVT gained popularity among traders who found OBV too reactive to minor price fluctuations. By incorporating price magnitude, PVT provides a smoother view of volume-weighted momentum while maintaining the cumulative structure that makes divergence analysis effective.

The indicator appears in most major technical analysis platforms under names including "Price Volume Trend," "Volume Price Trend," or simply "PVT."

## Architecture & Physics

PVT operates as a weighted accumulator where each bar's contribution depends on both volume and the percentage change in price. The formula scales volume by the relative price movement, creating a more nuanced measure of buying/selling pressure.

### Component Breakdown

1. **Price Change**: Calculate difference from previous close
2. **Price Change Ratio**: Normalize by previous price (percentage)
3. **Volume Adjustment**: Scale volume by the ratio
4. **Cumulative Total**: Running sum of adjusted volumes

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| PvtValue | double | Current cumulative PVT |
| PrevClose | double | Previous bar's close for ratio calculation |
| LastValidClose | double | Fallback for NaN/Infinity handling |
| LastValidVolume | double | Fallback for NaN/Infinity handling |

## Mathematical Foundation

### Core Formula

$$
PVT_t = PVT_{t-1} + Volume_t \times \frac{Close_t - Close_{t-1}}{Close_{t-1}}
$$

where:

- $PVT_0 = 0$ (starts at zero)
- Division by zero (prev_close = 0) yields no contribution

### Expanded Form

$$
PVT_t = \sum_{i=1}^{t} V_i \times \frac{C_i - C_{i-1}}{C_{i-1}}
$$

This can be rewritten as:

$$
PVT_t = \sum_{i=1}^{t} V_i \times \left(\frac{C_i}{C_{i-1}} - 1\right)
$$

or equivalently:

$$
PVT_t = \sum_{i=1}^{t} V_i \times r_i
$$

where $r_i$ is the simple return at bar $i$.

### Comparison with OBV

| Indicator | Formula | Sensitivity |
| :--- | :--- | :--- |
| **OBV** | $\sum V_i \times \text{sign}(C_i - C_{i-1})$ | Direction only |
| **PVT** | $\sum V_i \times (C_i - C_{i-1}) / C_{i-1}$ | Magnitude weighted |

PVT dampens small moves and amplifies large moves, while OBV treats all directional changes equally.

### Why Percentage-Based?

Using percentage change rather than absolute price change:

- Makes PVT comparable across different price levels
- A $1 move on a $10 stock (10%) contributes more than $1 on a $100 stock (1%)
- Aligns with return-based thinking in portfolio analysis
- Normalizes the indicator across time (stock splits, price drift)

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | Close - PrevClose |
| DIV | 1 | Price change / PrevClose |
| MUL | 1 | Volume × ratio |
| ADD | 1 | Cumulative sum |
| **Total** | 4 | Per bar, O(1) |

Slightly heavier than OBV due to the division, but still extremely lightweight.

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| Price differences | ✅ | Close[i] - Close[i-1] |
| Division | ✅ | Element-wise division |
| Volume scaling | ✅ | Element-wise multiply |
| Cumulative sum | ❌ | Sequential dependency |

Like OBV, the cumulative sum prevents full vectorization. However, the per-element calculations can be vectorized before the final prefix sum.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact floating-point computation |
| **Timeliness** | 8/10 | Immediate response to price changes |
| **Overshoot** | N/A | No bounds; cumulative indicator |
| **Smoothness** | 7/10 | Smoother than OBV for small moves |
| **Memory** | 10/10 | O(1) state: 2-4 scalar values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | ✅ | `Pvt` indicator, exact match |
| **Tulip** | N/A | Not implemented |
| **Ooples** | ✅ | `Pvt` indicator, exact match |
| **PineScript** | ✅ | Custom implementation, exact match |

QuanTAlib implementation validated against Skender and Ooples with tight tolerances (1e-9). PVT is less universally implemented than OBV, but available in major .NET libraries.

## Common Pitfalls

1. **Absolute Value Meaningless**: Like OBV, PVT's numeric value has no intrinsic meaning—only direction and divergences matter. Don't compare PVT values across different securities.

2. **Not Bounded**: PVT can reach any value, positive or negative. There are no overbought/oversold levels.

3. **Scale Depends on Price Level**: While percentage-based, PVT values are still influenced by the absolute price level during calculation. Use relative analysis (slopes, divergences) rather than absolute comparisons.

4. **Division by Zero**: If previous close is zero (rare but possible with some data feeds), the formula produces no contribution. QuanTAlib handles this gracefully.

5. **Small Price Changes Damped**: Unlike OBV, a 0.1% move with huge volume barely registers in PVT. This is a feature for noise reduction but may miss significant volume events with small price impact.

6. **Volume Data Quality**: PVT is only as reliable as volume data. Extended hours, different exchange feeds, or estimated volume can produce misleading signals.

7. **TValue Limitations**: The `Update(TValue)` method cannot compute PVT without volume data. Use `Update(TBar)` for proper calculation.

8. **isNew Parameter**: When correcting bars (isNew=false), the implementation properly restores previous state. Incorrect handling causes cumulative drift.

## Interpretation Guide

### Trend Confirmation

| Price Trend | PVT Trend | Interpretation |
| :--- | :--- | :--- |
| Rising | Rising | Confirmed uptrend, magnitude-weighted |
| Falling | Falling | Confirmed downtrend, magnitude-weighted |
| Rising | Falling | Bearish divergence: large down days outweigh |
| Falling | Rising | Bullish divergence: large up days outweigh |

### PVT vs OBV Signals

| Scenario | OBV | PVT | Interpretation |
| :--- | :--- | :--- | :--- |
| Many small up days | Strong rise | Weak rise | OBV overstates strength |
| Few large up days | Weak rise | Strong rise | PVT captures momentum |
| High volume, tiny move | Large change | Small change | PVT filters noise |

### Divergence Trading

PVT divergences often precede OBV divergences because magnitude weighting reveals conviction earlier:

| Signal | Setup | Action |
| :--- | :--- | :--- |
| Bullish | Price makes lower low, PVT makes higher low | Anticipate reversal up |
| Bearish | Price makes higher high, PVT makes lower high | Anticipate reversal down |

### Signal Line

PVT is often paired with a moving average (signal line) for crossover signals:

- PVT crossing above signal line: bullish
- PVT crossing below signal line: bearish

The default signal period is typically 14-21 bars.

## References

- Achelis, S. (2001). *Technical Analysis from A to Z*. McGraw-Hill.
- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Investopedia. "Price Volume Trend (PVT)." [Definition](https://www.investopedia.com/terms/p/pricevolumetrend.asp)
- StockCharts. "Price Volume Trend." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:price_volume_trend_pvt)
- TradingView. "Volume Indicators." [Reference](https://www.tradingview.com/scripts/volume/)