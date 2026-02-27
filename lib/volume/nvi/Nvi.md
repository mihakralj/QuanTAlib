# NVI: Negative Volume Index

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `startValue` (default 100.0)                      |
| **Outputs**      | Single series (Nvi)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> 2` bars                          |

### TL;DR

- The Negative Volume Index tracks price changes exclusively on days when trading volume decreases compared to the previous day.
- Parameterized by `startvalue` (default 100.0).
- Output range: Unbounded.
- Requires `> 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Low volume suggests smart money is at work; high volume days are for the crowd." — Norman Fosback

The Negative Volume Index tracks price changes exclusively on days when trading volume decreases compared to the previous day. The underlying theory: institutional investors—the "smart money"—prefer to accumulate or distribute positions during quiet, low-volume periods, while retail traders drive high-volume days with more emotional, less informed decisions.

NVI essentially asks: "What are prices doing when the crowd isn't participating?" If NVI rises while volume falls, smart money may be quietly buying. If NVI falls on low volume, institutions might be exiting positions without attracting attention.

## Historical Context

Paul Dysart developed the Negative Volume Index in the 1930s, making it one of the oldest volume-based indicators still in use. Norman Fosback later popularized and refined the concept in his 1976 book "Stock Market Logic," demonstrating that NVI's long-term trend had predictive value for market direction.

Fosback's research suggested:
- When NVI is above its 1-year moving average: ~96% probability of a bull market
- When NVI is below its 1-year moving average: ~53% probability of a bull market

The indicator's longevity stems from its counterintuitive insight: ignore the noise of high-volume days and focus on what happens when fewer participants are trading. This filtering mechanism was revolutionary for its era and remains relevant today.

NVI is often paired with the Positive Volume Index (PVI), which tracks price changes on high-volume days. Together, they provide a complete picture of how different market participants behave.

## Architecture & Physics

NVI operates as a cumulative price-change tracker with a volume filter. The key design decision: NVI only updates when current volume is strictly less than previous volume. When volume increases or stays the same, NVI remains unchanged.

This binary filtering creates a "quiet day" journal of price movements, isolating institutional activity from retail-driven volatility.

### Component Breakdown

1. **Volume Comparison**: Current volume vs. previous volume
2. **Price Ratio**: Close / Previous Close
3. **Conditional Update**: Apply price ratio only when volume decreases
4. **Cumulative Value**: NVI carries forward when inactive

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| NviValue | double | Current cumulative NVI |
| PrevClose | double | Previous bar's close for ratio |
| PrevVolume | double | Previous bar's volume for comparison |
| StartValue | double | Initial NVI value (default: 100) |

## Mathematical Foundation

### Core Formula

$$
NVI_t = \begin{cases}
NVI_{t-1} \times \frac{Close_t}{Close_{t-1}} & \text{if } Volume_t < Volume_{t-1} \\
NVI_{t-1} & \text{otherwise}
\end{cases}
$$

where:
- $NVI_0 = \text{StartValue}$ (typically 100 or 1000)
- Volume comparison is strict inequality (< not ≤)

### Expanded Form (for low-volume days)

$$
NVI_t = NVI_{t-1} \times \left(1 + \frac{Close_t - Close_{t-1}}{Close_{t-1}}\right)
$$

This shows NVI as a return accumulator:

$$
NVI_t = StartValue \times \prod_{i \in D} \frac{Close_i}{Close_{i-1}}
$$

where $D$ is the set of all days where $Volume_i < Volume_{i-1}$.

### Why Multiplicative?

The multiplicative structure (×) rather than additive (+) ensures:
- Percentage changes compound properly
- Scale invariance with respect to start value
- No artificial bias from absolute price levels

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| CMP | 1 | Volume < PrevVolume |
| DIV | 0-1 | Close / PrevClose (conditional) |
| MUL | 0-1 | NVI × ratio (conditional) |
| **Total** | ~1-3 | Per bar, O(1) |

NVI is exceptionally lightweight—one comparison per bar, with division and multiplication only occurring on low-volume days.

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
| **Timeliness** | 5/10 | Intentionally slow—filters out noise |
| **Overshoot** | N/A | No bounds; cumulative indicator |
| **Smoothness** | 9/10 | Only changes on subset of bars |
| **Memory** | 10/10 | O(1) state: 3 scalar values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | ✅ | Has `nvi` indicator |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation |

QuanTAlib implementation validated against:
- PineScript `ta.nvi()` function
- Manual formula verification
- Edge case testing (equal volumes, zero volume, NaN handling)

## Common Pitfalls

1. **Start Value Matters for Comparison**: Different start values (100 vs 1000) produce proportionally different NVI values. When comparing NVI across instruments or time periods, use consistent start values or normalize.

2. **Not Bounded**: Unlike oscillators (RSI, MFI), NVI has no upper or lower bounds. It can theoretically reach any positive value. Use signal lines (moving averages of NVI) for interpretation rather than absolute levels.

3. **Equal Volume Ignored**: When `Volume_t == Volume_{t-1}`, NVI remains unchanged—same behavior as volume increase. Some implementations use ≤; QuanTAlib uses strict < per the original formula.

4. **Requires Two Bars**: NVI needs at least two bars to make a comparison. First bar always returns the start value.

5. **Volume Data Quality**: NVI is extremely sensitive to volume data quality. Markets with unreliable volume (some crypto exchanges, certain OTC markets) can produce misleading signals.

6. **Long-Term Indicator**: NVI is designed for trend identification over extended periods. Using it for short-term trading generates noise. Fosback recommended comparing NVI to its 1-year moving average.

7. **TValue Limitations**: The `Update(TValue)` method exists for interface compatibility but cannot compute NVI without volume data. Use `Update(TBar)` for proper calculation.

8. **isNew Parameter**: When correcting bars (isNew=false), the implementation properly restores previous state. Incorrect handling causes cumulative drift.

## Interpretation Guide

### Bull vs Bear Market

Compare NVI to its long-term moving average (typically 255-day or 1-year EMA):

| NVI Position | Market Signal |
| :--- | :--- |
| Above moving average | Bullish: smart money accumulating |
| Below moving average | Bearish: smart money distributing |
| Crossing above | Potential trend change to bullish |
| Crossing below | Potential trend change to bearish |

### Divergences

| Price Action | NVI Action | Interpretation |
| :--- | :--- | :--- |
| Higher highs | Lower highs | Bearish divergence: smart money not confirming |
| Lower lows | Higher lows | Bullish divergence: quiet accumulation |

### Pairing with PVI

NVI and PVI provide complementary signals:

| NVI Trend | PVI Trend | Interpretation |
| :--- | :--- | :--- |
| Rising | Rising | Broad participation, strong trend |
| Rising | Falling | Smart money buying, retail selling |
| Falling | Rising | Retail buying, smart money exiting |
| Falling | Falling | Broad distribution, weak market |

## References

- Dysart, P. (1930s). Original development of Negative Volume Index.
- Fosback, N. (1976). *Stock Market Logic*. Institute for Econometric Research.
- Investopedia. "Negative Volume Index (NVI)." [Definition](https://www.investopedia.com/terms/n/nvi.asp)
- StockCharts. "Negative Volume Index (NVI)." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:negative_volume_index)
- TradingView. "PineScript ta.nvi()." [Reference](https://www.tradingview.com/pine-script-reference/v5/#fun_ta{dot}nvi)
