# PVI: Positive Volume Index

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `startValue` (default 100.0)                      |
| **Outputs**      | Single series (Pvi)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> 2` bars                          |

### TL;DR

- The Positive Volume Index tracks price changes exclusively on days when trading volume increases compared to the previous day.
- Parameterized by `startvalue` (default 100.0).
- Output range: Unbounded.
- Requires `> 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "High volume days reveal where retail traders swarm; smart money prefers the quiet." — Norman Fosback

The Positive Volume Index tracks price changes exclusively on days when trading volume increases compared to the previous day. The underlying theory: retail investors—the "uninformed crowd"—drive high-volume trading days, often reacting emotionally to news and price movements. Institutional investors prefer to operate during quieter periods to avoid moving markets.

PVI essentially asks: "What are prices doing when the crowd is most active?" If PVI rises on high volume, retail enthusiasm is driving prices up. If PVI falls on high volume, retail panic may be pushing prices down. Either way, this represents the emotional, less-informed segment of the market.

## Historical Context

Paul Dysart developed the Positive Volume Index alongside the Negative Volume Index in the 1930s. Norman Fosback later popularized both indicators in his 1976 book "Stock Market Logic," demonstrating their complementary nature for analyzing market behavior.

While NVI focuses on smart money activity during quiet periods, PVI captures the retail investor's footprint. Fosback's research showed that PVI alone has less predictive power than NVI because retail-driven moves are more random and noise-filled. However, PVI becomes valuable when combined with NVI to paint a complete picture of market participation.

The key insight: divergences between PVI and NVI often signal significant market transitions. When smart money (NVI) and retail (PVI) disagree, one group is likely wrong—and it's usually the crowd.

## Architecture & Physics

PVI operates as a cumulative price-change tracker with a volume filter. The key design decision: PVI only updates when current volume is strictly greater than previous volume. When volume decreases or stays the same, PVI remains unchanged.

This binary filtering creates a "busy day" journal of price movements, capturing retail-driven volatility and emotional trading.

### Component Breakdown

1. **Volume Comparison**: Current volume vs. previous volume
2. **Price Ratio**: Close / Previous Close
3. **Conditional Update**: Apply price ratio only when volume increases
4. **Cumulative Value**: PVI carries forward when inactive

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| PviValue | double | Current cumulative PVI |
| PrevClose | double | Previous bar's close for ratio |
| PrevVolume | double | Previous bar's volume for comparison |
| StartValue | double | Initial PVI value (default: 100) |

## Mathematical Foundation

### Core Formula

$$
PVI_t = \begin{cases}
PVI_{t-1} \times \frac{Close_t}{Close_{t-1}} & \text{if } Volume_t > Volume_{t-1} \\
PVI_{t-1} & \text{otherwise}
\end{cases}
$$

where:
- $PVI_0 = \text{StartValue}$ (typically 100 or 1000)
- Volume comparison is strict inequality (> not ≥)

### Expanded Form (for high-volume days)

$$
PVI_t = PVI_{t-1} \times \left(1 + \frac{Close_t - Close_{t-1}}{Close_{t-1}}\right)
$$

This shows PVI as a return accumulator:

$$
PVI_t = StartValue \times \prod_{i \in D} \frac{Close_i}{Close_{i-1}}
$$

where $D$ is the set of all days where $Volume_i > Volume_{i-1}$.

### Why Multiplicative?

The multiplicative structure (×) rather than additive (+) ensures:
- Percentage changes compound properly
- Scale invariance with respect to start value
- No artificial bias from absolute price levels

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| CMP | 1 | Volume > PrevVolume |
| DIV | 0-1 | Close / PrevClose (conditional) |
| MUL | 0-1 | PVI × ratio (conditional) |
| **Total** | ~1-3 | Per bar, O(1) |

PVI is exceptionally lightweight—one comparison per bar, with division and multiplication only occurring on high-volume days.

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| Volume comparison | ✅ | Embarrassingly parallel |
| Price ratios | ✅ | When masked |
| Cumulative update | ❌ | Sequential dependency |

The cumulative nature prevents full SIMD vectorization, but preprocessing volume comparisons and ratios can still provide modest speedup.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Simple formula, exact computation |
| **Timeliness** | 6/10 | Responds to crowd activity |
| **Overshoot** | N/A | No bounds; cumulative indicator |
| **Smoothness** | 8/10 | Only changes on subset of bars |
| **Memory** | 10/10 | O(1) state: 3 scalar values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | ✅ | Has `pvi` indicator |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation |

QuanTAlib implementation validated against:
- PineScript `ta.pvi()` function
- Manual formula verification
- Edge case testing (equal volumes, zero volume, NaN handling)

## Common Pitfalls

1. **Start Value Matters for Comparison**: Different start values (100 vs 1000) produce proportionally different PVI values. When comparing PVI across instruments or time periods, use consistent start values or normalize.

2. **Not Bounded**: Unlike oscillators (RSI, MFI), PVI has no upper or lower bounds. It can theoretically reach any positive value. Use signal lines (moving averages of PVI) for interpretation rather than absolute levels.

3. **Equal Volume Ignored**: When `Volume_t == Volume_{t-1}`, PVI remains unchanged—same behavior as volume decrease. Some implementations use ≥; QuanTAlib uses strict > per the original formula.

4. **Requires Two Bars**: PVI needs at least two bars to make a comparison. First bar always returns the start value.

5. **Volume Data Quality**: PVI is extremely sensitive to volume data quality. Markets with unreliable volume (some crypto exchanges, certain OTC markets) can produce misleading signals.

6. **Noisier Than NVI**: Because PVI tracks retail activity, it tends to be noisier and less predictive than NVI. Consider using longer smoothing periods or focus on PVI-NVI divergences rather than PVI alone.

7. **TValue Limitations**: The `Update(TValue)` method exists for interface compatibility but cannot compute PVI without volume data. Use `Update(TBar)` for proper calculation.

8. **isNew Parameter**: When correcting bars (isNew=false), the implementation properly restores previous state. Incorrect handling causes cumulative drift.

## Interpretation Guide

### Retail Sentiment

PVI reflects retail trader behavior:

| PVI Action | Interpretation |
| :--- | :--- |
| Rising sharply | Retail enthusiasm, possible FOMO buying |
| Falling sharply | Retail panic, emotional selling |
| Flat or choppy | Mixed retail sentiment |

### Divergences with Price

| Price Action | PVI Action | Interpretation |
| :--- | :--- | :--- |
| Higher highs | Lower highs | Retail losing enthusiasm for rally |
| Lower lows | Higher lows | Retail buying the dip |

### Pairing with NVI

PVI and NVI provide complementary signals:

| NVI Trend | PVI Trend | Interpretation |
| :--- | :--- | :--- |
| Rising | Rising | Broad participation, strong trend |
| Rising | Falling | Smart money buying, retail selling |
| Falling | Rising | Retail buying, smart money exiting (caution!) |
| Falling | Falling | Broad distribution, weak market |

The most valuable signal: **NVI rising while PVI falling**. This suggests smart money accumulation during retail pessimism—often precedes significant rallies.

The danger signal: **PVI rising while NVI falling**. Retail enthusiasm without institutional support—a setup for potential corrections.

## References

- Dysart, P. (1930s). Original development of Positive Volume Index.
- Fosback, N. (1976). *Stock Market Logic*. Institute for Econometric Research.
- Investopedia. "Positive Volume Index (PVI)." [Definition](https://www.investopedia.com/terms/p/pvi.asp)
- StockCharts. "Positive Volume Index (PVI)." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:positive_volume_index)
- TradingView. "PineScript ta.pvi()." [Reference](https://www.tradingview.com/pine-script-reference/v5/#fun_ta{dot}pvi)
