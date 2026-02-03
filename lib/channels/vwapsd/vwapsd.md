# VWAPSD: VWAP with Standard Deviation Bands

## Overview and Purpose

The Volume Weighted Average Price with Standard Deviation Bands (VWAPSD) combines two powerful analytical tools: the VWAP and statistical volatility bands. VWAP represents the true average price of a security for a given period, weighted by the volume transacted at each price level, making it particularly valuable for institutional traders and algorithms that benchmark their execution quality.

Unlike simple moving averages that treat all price points equally, VWAP gives more weight to prices where significant volume occurred, providing a more accurate representation of the market's consensus value. The addition of standard deviation bands transforms VWAP from a simple reference line into a complete channel system that measures both central tendency and price dispersion.

VWAPSD is primarily used as an intraday indicator, resetting at the beginning of each trading session (or other configurable periods). This anchored approach ensures that the indicator remains relevant to current market conditions and prevents the accumulation of stale historical data. The standard deviation bands provide dynamic support and resistance levels that expand and contract with market volatility, helping traders identify overbought and oversold conditions relative to the volume-weighted average.

## Core Concepts

* **Volume Weighting:** Unlike arithmetic averages, VWAP weights each price by its corresponding volume, giving more importance to prices where substantial trading activity occurred. This creates a more representative average that reflects actual market participation.

* **Session Anchoring:** VWAP resets at the beginning of each session (configurable from 1-minute to yearly periods), ensuring the indicator reflects current market structure rather than accumulating indefinitely. This makes it particularly effective for intraday analysis.

* **Typical Price (HLC3):** Uses the average of high, low, and close prices to represent each bar's central value, providing a balanced price point that considers the full range of trading activity within the period.

* **Standard Deviation Bands:** Measures the dispersion of prices around the VWAP, with bands typically set at 1, 2, or 3 standard deviations. These bands quantify how far prices are deviating from the volume-weighted mean and adapt dynamically to volatility.

* **Institutional Benchmark:** Large institutional traders use VWAP as an execution benchmark - buying below VWAP or selling above it is considered favorable execution, making VWAP a self-fulfilling support/resistance level.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | source | Data source for VWAP calculation | Use 'close' for closing prices only, 'hlc3' for typical price (most common), 'ohlc4' for full bar average |
| Session Reset | 1D | Determines when VWAP resets | Use '1D' for daily intraday trading, '1W' for weekly swing trading, '1H' for hourly scalping, 'Never' for cumulative since chart start |
| Standard Deviations | 2.0 | Number of standard deviations for upper and lower bands | Use 1.0 for tighter bands (more signals), 2.0 for standard volatility context (95% confidence), 3.0 for extreme moves only (99.7% confidence) |

**Pro Tip:** For day trading, use the '1D' session reset with 2 standard deviation bands. Price touching the upper band often indicates overbought conditions suitable for taking profits or shorting, while touches of the lower band suggest oversold conditions for buying opportunities. Institutional traders often defend VWAP as a key level, making it a natural target for mean reversion strategies. Consider using multiple timeframes: 1D for the primary trend and 1H for intraday structure.

## Calculation and Mathematical Foundation

**Explanation:**
VWAPSD calculates a volume-weighted average price that resets at session boundaries, then adds statistical bands based on the standard deviation of price deviations from this average. The calculation maintains three running sums throughout the session: cumulative price×volume, cumulative volume, and cumulative price²×volume. These sums reset at the beginning of each new session as defined by the session type parameter.

**Technical formula:**

```
Step 1: Calculate typical price for each bar
Typical Price = (High + Low + Close) / 3  [or use selected source]

Step 2: Accumulate weighted sums within session
sum_pv = Σ(Price × Volume)
sum_vol = Σ(Volume)
sum_pv2 = Σ(Price² × Volume)

Step 3: Calculate VWAP
VWAP = sum_pv / sum_vol

Step 4: Calculate variance and standard deviation
Variance = (sum_pv2 / sum_vol) - VWAP²
StdDev = √(max(0, Variance))

Step 5: Calculate bands
Upper Band = VWAP + (num_devs × StdDev)
Lower Band = VWAP - (num_devs × StdDev)

Step 6: Reset on session boundary
When reset_condition = true:
  sum_pv = Price × Volume (initialize with current bar)
  sum_vol = Volume
  sum_pv2 = Price² × Volume
```

> 🔍 **Technical Note:** The implementation uses reset-based accumulation (Pattern §20) for session boundaries, ensuring clean starts each period. Variance is calculated using the weighted formula: E[X²] - E[X]², which is numerically stable and matches the standard deviation pattern (§8). Volume weighting ensures that high-volume price levels contribute more to both the mean and variance calculations. The implementation handles zero or missing volume gracefully by using nz() conversions (Pattern §7) and defensive division (Pattern §6).

## Interpretation Details

**Primary Use - Mean Reversion Trading:**
* Price trading above VWAP with upper band touch suggests overbought conditions - potential shorting opportunity or profit-taking
* Price trading below VWAP with lower band touch suggests oversold conditions - potential buying opportunity
* Price returning to VWAP from extreme bands is a common mean reversion pattern
* Volume confirmation strengthens signals: high volume at bands indicates stronger reversal potential

**Institutional Trading Context:**
* VWAP serves as a benchmark for institutional execution quality
* Large buy orders executed below VWAP are considered favorable (buying at discount)
* Large sell orders executed above VWAP are considered favorable (selling at premium)
* Institutions often defend VWAP as support/resistance, creating self-fulfilling price action

**Trend Identification:**
* Price consistently above VWAP indicates bullish intraday trend
* Price consistently below VWAP indicates bearish intraday trend
* VWAP slope provides additional trend confirmation (rising = bullish, falling = bearish)
* Crossovers of price through VWAP can signal trend changes, especially with volume

**Volatility Analysis:**
* Band width measures current volatility - wide bands indicate high volatility, narrow bands indicate low volatility
* Contracting bands often precede breakout moves (volatility compression)
* Expanding bands during price moves confirm momentum strength
* Multiple touches of bands without breakout suggests ranging market

**Standard Deviation Levels:**
* 1σ bands (~68% of price action): Used for active trading and frequent signals
* 2σ bands (~95% of price action): Standard setting for most trading strategies
* 3σ bands (~99.7% of price action): Extreme moves only, strong reversal signals

## Limitations and Considerations

* **Intraday Focus:** VWAPSD is designed primarily for intraday analysis and loses effectiveness on higher timeframes where session resets become less meaningful. For multi-day analysis, consider anchored VWAP variants.

* **Session Dependency:** The indicator's value depends heavily on the chosen session reset period. Incorrect session selection can produce misleading signals - ensure your session matches your trading timeframe and strategy.

* **Low Volume Periods:** During low volume periods (market open/close, holidays, thin markets), VWAP can be distorted by a few large trades. Standard deviation bands may not accurately reflect true volatility in these conditions.

* **Lagging Nature:** Despite being more responsive than simple moving averages, VWAP is still a lagging indicator based on historical price and volume. It confirms trends rather than predicts them.

* **No Directional Bias:** VWAPSD does not predict direction - it only identifies when price has deviated significantly from the volume-weighted mean. Additional tools (momentum indicators, price action, volume analysis) are needed for directional confirmation.

* **Gap Sensitivity:** Large overnight gaps can distort the morning VWAP calculation until sufficient volume accumulates. Consider waiting for the first 30-60 minutes of trading for VWAP to stabilize.

* **Volume Quality:** VWAP effectiveness depends on volume data quality. In thinly traded securities or markets with unreliable volume data, VWAP may not provide reliable signals.

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 7 | 1 | 7 |
| MUL | 4 | 3 | 12 |
| DIV | 3 | 15 | 45 |
| SQRT | 1 | 15 | 15 |
| **Total** | **15** | — | **~79 cycles** |

**Breakdown:**
- Typical price (HLC3): 2 ADD + 1 DIV = 17 cycles
- Running sums (pv, vol, pv²): 3 ADD + 3 MUL = 12 cycles
- VWAP + variance: 2 DIV + 1 MUL + 1 SUB = 35 cycles
- StdDev + bands: 1 SQRT + 2 ADD = 17 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Running sums, no buffer iteration |
| Batch | O(n) | Linear scan per session |

**Memory**: ~48 bytes (3 running sums × 8 bytes + session state)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | Partial | HLC3 and band calculations vectorizable |
| FMA | ✅ | Band offset: `VWAP + num_devs × StdDev` |
| Batch parallelism | Limited | Session resets create boundaries |

**Note:** VWAPSD uses simple accumulation, making it amenable to partial SIMD. However, session reset boundaries (Pattern §20) prevent full vectorization across the entire series. Intra-session batch processing can achieve ~4× speedup on the typical price and band calculations, with the running sum portion remaining sequential.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Volume-weighted mean is mathematically exact |
| **Timeliness** | 7/10 | Incorporates all data since session start |
| **Overshoot** | 9/10 | Bands based on actual volatility |
| **Smoothness** | 9/10 | Running average smooths noise progressively |

## References

* Berkowitz, S. A., Logue, D. E., & Noser, E. A. (1988). The Total Cost of Transactions on the NYSE. The Journal of Finance, 43(1), 97-112.
* TradingView (2024). Volume Weighted Average Price (VWAP). TradingView Support Documentation.
* thinkorswim Learning Center. VWAP Technical Indicator Reference.
* TheVWAP.com (2024). The Detailed Guide to VWAP. Educational Resource.
* Kissell, R. (2013). The Science of Algorithmic Trading and Portfolio Management. Academic Press.

## Validation Sources

**Patterns:** §20 (reset_accumulation), §7 (na_handling), §8 (variance_calculation), §6 (defensive_division), §11 (multi_return), §15 (first_bar_handling)

**Wolfram:** "standard deviation formula"

**External:** "VWAP standard deviation bands formula", "VWAP typical price calculation" via Tavily; TradingView VWAP documentation, thinkorswim VWAP reference, TrendSpider VWAP with St.Dev Bands guide

**API:** Verified vwap.pine reference implementation (session reset pattern, inline variable declarations), stddev.pine reference implementation (variance formula with math.pow)

**Planning:** Sequential thinking phases: requirements analysis, mathematical foundation, implementation strategy, session reset logic, NA handling, visualization strategy, parameter validation, final checklist