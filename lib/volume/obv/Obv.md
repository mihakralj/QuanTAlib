# OBV: On Balance Volume

> *Volume is the fuel that drives price.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (OBV)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> 2` bars                          |
| **PineScript**   | [obv.pine](obv.pine)                       |

- On Balance Volume distills the relationship between price and volume into a single cumulative indicator.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

On Balance Volume distills the relationship between price and volume into a single cumulative indicator. The premise is elegantly simple: volume flows into a security when it closes higher, and flows out when it closes lower. OBV tracks this flow as a running total, creating a momentum indicator that often leads price movements.

Granville's insight was that volume precedes price. Institutional buying or selling shows up in volume before it manifests in price trends. When OBV rises while price remains flat, accumulation is occurring—a potential bullish signal. When OBV falls despite stable prices, distribution may be underway.

## Historical Context

Joseph Granville introduced On Balance Volume in his 1963 book *Granville's New Key to Stock Market Profits*. The indicator emerged from Granville's observation that volume changes often preceded price changes—what he called "On Balance Volume" because the cumulative total showed whether buying or selling pressure was "on balance" dominant.

Granville was a colorful market technician who made bold predictions and drew large crowds to his seminars. While some of his market calls proved spectacularly wrong, OBV survived and thrived because of its fundamental soundness: it measures the conviction behind price movements.

The indicator became a staple of technical analysis because:

- It requires no parameters—pure price and volume
- It leads price action rather than lagging
- It reveals accumulation/distribution before price confirmation
- It generates clear divergence signals

OBV remains one of the most widely used volume indicators, implemented in virtually every charting platform and technical analysis library.

## Architecture & Physics

OBV operates as a simple accumulator with a directional sign determined by price change. Each bar either adds volume (close > previous close), subtracts volume (close < previous close), or does nothing (unchanged close).

This creates a cumulative "money flow" proxy that tracks whether buying or selling pressure dominates over time.

### Component Breakdown

1. **Price Direction**: Compare current close to previous close
2. **Volume Attribution**: Full volume assigned to winning side
3. **Cumulative Total**: Running sum of signed volumes

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| ObvValue | double | Current cumulative OBV |
| PrevClose | double | Previous bar's close for comparison |
| LastValidClose | double | Fallback for NaN/Infinity handling |
| LastValidVolume | double | Fallback for NaN/Infinity handling |

## Mathematical Foundation

### Core Formula

$$
OBV_t = \begin{cases}
OBV_{t-1} + Volume_t & \text{if } Close_t > Close_{t-1} \\
OBV_{t-1} - Volume_t & \text{if } Close_t < Close_{t-1} \\
OBV_{t-1} & \text{if } Close_t = Close_{t-1}
\end{cases}
$$

where:

- $OBV_0 = 0$ (starts at zero)
- Volume is always non-negative
- Comparison uses strict inequality

### Expanded Form

$$
OBV_t = \sum_{i=1}^{t} V_i \cdot \text{sign}(Close_i - Close_{i-1})
$$

where $\text{sign}(x)$ returns:

- $+1$ if $x > 0$
- $-1$ if $x < 0$
- $0$ if $x = 0$

### Why All-or-Nothing?

Unlike other volume indicators (like Accumulation/Distribution or Chaikin Money Flow) that weight volume by price position within the bar, OBV assigns the entire volume to either buyers or sellers. This binary approach:
- Maximizes sensitivity to direction changes
- Avoids subjective weighting parameters
- Creates clearer divergence signals
- Matches Granville's original conviction thesis

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| CMP | 2 | Close > PrevClose, Close < PrevClose |
| ADD/SUB | 0-1 | Conditional volume addition |
| **Total** | 2-3 | Per bar, O(1) |

OBV is one of the lightest indicators—two comparisons and at most one addition per bar.

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| Price differences | ✅ | Close[i] - Close[i-1] |
| Sign extraction | ✅ | ConditionalSelect |
| Volume signing | ✅ | Multiply by sign |
| Cumulative sum | ❌ | Sequential dependency |

The cumulative nature prevents full SIMD vectorization. However, sign computation can be vectorized, leaving only the prefix sum as scalar.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact integer-like computation |
| **Timeliness** | 8/10 | Responds immediately to direction |
| **Overshoot** | N/A | No bounds; cumulative indicator |
| **Smoothness** | 6/10 | Can be volatile with high volume |
| **Memory** | 10/10 | O(1) state: 2-4 scalar values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | `OBV` function, exact match |
| **Skender** | ✅ | `Obv` indicator, exact match |
| **Tulip** | ✅ | `obv` indicator, exact match |
| **Ooples** | ✅ | `Obv` indicator, exact match |
| **PineScript** | ✅ | `ta.obv()` reference |

QuanTAlib implementation validated against all four external libraries with tight tolerances (1e-9). OBV's simple formula means implementations are highly consistent across libraries.

## Common Pitfalls

1. **Absolute Value Meaningless**: OBV's numeric value has no intrinsic meaning—only its direction and divergences matter. Don't compare OBV values across different securities or time periods.

2. **Not Bounded**: OBV can reach any value, positive or negative. It has no overbought/oversold levels. Use trend analysis, not absolute thresholds.

3. **Sensitive to Starting Point**: Where you begin calculating OBV affects all subsequent values. For consistent analysis, use the same starting date or focus on relative changes.

4. **Gaps Distort**: Large price gaps can assign massive volume to one direction, creating spikes in OBV that may not reflect sustained accumulation/distribution.

5. **Equal Close Ignored**: When close equals previous close (rare but possible), volume is discarded. Some implementations default to adding volume; QuanTAlib follows the original formula with zero change.

6. **Volume Data Quality**: OBV is only as reliable as volume data. Extended hours, different exchange feeds, or estimated volume (some ETFs) can produce misleading signals.

7. **TValue Limitations**: The `Update(TValue)` method exists for interface compatibility but cannot compute OBV without volume data. Use `Update(TBar)` for proper calculation.

8. **isNew Parameter**: When correcting bars (isNew=false), the implementation properly restores previous state. Incorrect handling causes cumulative drift in the running total.

## Interpretation Guide

### Trend Confirmation

| Price Trend | OBV Trend | Interpretation |
| :--- | :--- | :--- |
| Rising | Rising | Confirmed uptrend with volume support |
| Falling | Falling | Confirmed downtrend with volume support |
| Rising | Falling | Bearish divergence: weakness ahead |
| Falling | Rising | Bullish divergence: strength building |

### Breakout Confirmation

OBV breaking to new highs before price suggests accumulation and validates impending breakouts. OBV failing to confirm price breakouts warns of potential false moves.

### Divergence Trading

| Signal | Setup | Action |
| :--- | :--- | :--- |
| Bullish | Price makes lower low, OBV makes higher low | Anticipate reversal up |
| Bearish | Price makes higher high, OBV makes lower high | Anticipate reversal down |

### Trend Strength

The slope of OBV indicates buying/selling intensity:
- Steep OBV rise: aggressive accumulation
- Gentle OBV rise: gradual accumulation
- Flat OBV: equilibrium between buyers/sellers
- Steep OBV fall: aggressive distribution

## References

- Granville, J. (1963). *Granville's New Key to Stock Market Profits*. Prentice Hall.
- Achelis, S. (2001). *Technical Analysis from A to Z*. McGraw-Hill.
- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Investopedia. "On-Balance Volume (OBV)." [Definition](https://www.investopedia.com/terms/o/onbalancevolume.asp)
- StockCharts. "On Balance Volume (OBV)." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:on_balance_volume_obv)
- TradingView. "PineScript ta.obv()." [Reference](https://www.tradingview.com/pine-script-reference/v5/#fun_ta{dot}obv)