# VWAPBANDS: Volume Weighted Average Price with Dual Standard Deviation Bands

## Overview and Purpose

Volume Weighted Average Price Bands (VWAPBANDS) extends the standard VWAP indicator by adding two levels of standard deviation bands: ±1σ and ±2σ. This dual-band approach provides traders with a complete volatility framework, distinguishing between normal price fluctuations (within 1σ bands, ~68% of price action) and statistically significant moves (beyond 2σ bands, ~95% confidence level).

Unlike simple VWAP with single bands, VWAPBANDS creates distinct trading zones. The region between VWAP and ±1σ represents the "normal trading zone" where institutional algorithms typically execute. The area between ±1σ and ±2σ serves as an "alert zone" indicating elevated but not extreme deviation. Price beyond ±2σ signals statistically significant moves that often precede reversals or continuation breakouts.

The indicator maintains cumulative calculations from session start, with optional reset capability for multi-session analysis. Volume weighting ensures that prices where significant trading activity occurred contribute proportionally more to both the average and the deviation calculations, making VWAPBANDS particularly valuable for institutional traders benchmarking execution quality.

## Core Concepts

* **Dual Band System:** Provides two standard deviation levels (1σ and 2σ) creating three distinct trading zones above and below VWAP, enabling graduated position sizing and risk assessment based on statistical probability.

* **Volume-Weighted Statistics:** Both the average price and the standard deviation are calculated using volume weights, ensuring that high-volume price levels contribute more to all statistical measures.

* **HLC3 Typical Price:** Uses the average of high, low, and close prices as the representative price for each bar, providing a balanced measure that considers the full trading range.

* **Session Reset:** Optional reset capability allows VWAP to restart calculations at session boundaries, keeping the indicator relevant to current market conditions.

* **Width Measurement:** The full channel width (Upper2 - Lower2) provides a single metric for overall volatility, useful for comparing volatility across sessions or instruments.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Multiplier | 1.0 | Scales the standard deviation for band width | Use 1.0 for standard statistical bands, 2.0 for wider bands on volatile instruments, 0.5 for tighter bands on low-volatility instruments |

**Pro Tip:** The multiplier affects all bands proportionally. With multiplier = 1.0, Upper1/Lower1 are at ±1σ and Upper2/Lower2 are at ±2σ. Setting multiplier = 2.0 places them at ±2σ and ±4σ respectively. For most trading applications, keep the multiplier at 1.0 and interpret the bands as standard statistical levels.

## Calculation and Mathematical Foundation

**Explanation:**
VWAPBANDS calculates a volume-weighted average price with two levels of standard deviation bands. The implementation maintains three running sums: cumulative price×volume, cumulative volume, and cumulative price²×volume. These enable O(1) streaming updates while providing mathematically correct variance calculation.

**Technical formula:**

```
Step 1: Calculate typical price for each bar
Typical Price = (High + Low + Close) / 3

Step 2: Accumulate weighted sums (optionally reset on session boundary)
sum_pv = Σ(Price × Volume)
sum_vol = Σ(Volume)
sum_pv2 = Σ(Price² × Volume)

Step 3: Calculate VWAP
VWAP = sum_pv / sum_vol

Step 4: Calculate volume-weighted variance and standard deviation
Variance = (sum_pv2 / sum_vol) - VWAP²
StdDev = √(max(0, Variance))

Step 5: Calculate dual bands
Upper1 = VWAP + (1 × Multiplier × StdDev)
Lower1 = VWAP - (1 × Multiplier × StdDev)
Upper2 = VWAP + (2 × Multiplier × StdDev)
Lower2 = VWAP - (2 × Multiplier × StdDev)

Step 6: Calculate channel width
Width = Upper2 - Lower2 = 4 × Multiplier × StdDev
```

> 🔍 **Technical Note:** The variance formula uses the algebraic identity Var(X) = E[X²] - E[X]², which is numerically stable and computationally efficient for streaming updates. The implementation guards against negative variance (which can occur due to floating-point precision) by using max(0, variance) before taking the square root.

## Interpretation Details

**Zone-Based Trading:**

* **Inside ±1σ (Normal Zone):** ~68% of price action. Normal trading range where institutional algorithms execute without concern. Low signal value for mean reversion.

* **Between ±1σ and ±2σ (Alert Zone):** ~27% of price action. Elevated deviation suggesting caution. Consider reducing position size or preparing for reversal.

* **Beyond ±2σ (Extreme Zone):** ~5% of price action. Statistically significant move. High probability of mean reversion or continuation breakout.

**Institutional Execution Context:**

* Price at VWAP represents "fair" execution for institutional orders
* Execution below VWAP on buys (or above on sells) is considered favorable
* The ±1σ bands define the acceptable execution range for most algorithms
* Price beyond ±2σ may trigger algorithmic rebalancing

**Mean Reversion Signals:**

* Touch of Upper2 with declining momentum → Potential short entry
* Touch of Lower2 with rising momentum → Potential long entry
* Price returning to VWAP from ±2σ → Classic mean reversion play
* Multiple touches of ±2σ without breakout → Ranging market, fade extremes

**Trend Following Signals:**

* Price consistently above Upper1 → Strong bullish trend, buy pullbacks to VWAP
* Price consistently below Lower1 → Strong bearish trend, sell rallies to VWAP
* Breakout above Upper2 with increasing volume → Potential trend continuation
* Sequential touches of Upper1 → Upper2 → Higher → Trend acceleration

**Volatility Analysis:**

* Wide bands (large Width) → High volatility, larger position sizing risk
* Narrow bands (small Width) → Low volatility, potential breakout setup
* Expanding bands → Increasing volatility, trend may be developing
* Contracting bands → Decreasing volatility, consolidation phase

## Limitations and Considerations

* **Intraday Focus:** VWAPBANDS is primarily designed for intraday analysis. Without session resets, cumulative calculations can become less responsive on multi-day charts as early data dominates.

* **Volume Dependency:** The indicator requires reliable volume data. On instruments with unreliable or no volume (some forex, index CFDs), VWAP-based indicators may not provide accurate signals.

* **Early Session Instability:** At session start, VWAP and bands can be volatile due to limited data. Consider waiting 30-60 minutes for stabilization.

* **Gap Sensitivity:** Large overnight gaps distort morning VWAP calculations. The indicator needs time to incorporate sufficient volume for meaningful statistics.

* **No Directional Prediction:** VWAPBANDS identifies deviation from mean, not direction. Use with momentum indicators or price action for directional bias.

* **Multiplier Interpretation:** Non-standard multiplier values (≠1.0) change the statistical meaning of bands. Document your multiplier choice when backtesting or sharing strategies.

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 9 | 1 | 9 |
| MUL | 6 | 3 | 18 |
| DIV | 3 | 15 | 45 |
| SQRT | 1 | 15 | 15 |
| **Total** | **19** | — | **~87 cycles** |

**Breakdown:**

* Typical price (HLC3): 2 ADD + 1 DIV = 17 cycles
* Running sums (pv, vol, pv²): 3 ADD + 3 MUL = 12 cycles
* VWAP + variance: 2 DIV + 1 MUL + 1 SUB = 35 cycles
* StdDev: 1 SQRT = 15 cycles
* Dual bands + width: 4 ADD + 2 MUL = 10 cycles (with FMA optimization)

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Running sums, constant per bar |
| Batch | O(n) | Linear scan required |

**Memory:** ~80 bytes per instance (state struct with running sums, last valid values, and output properties)

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Mathematically exact volume-weighted statistics |
| **Timeliness** | 7/10 | Incorporates all session data, becomes stable over time |
| **Overshoot** | 9/10 | Bands adapt to actual volume-weighted volatility |
| **Smoothness** | 9/10 | Running sums provide inherent smoothing |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No VWAP bands implementation |
| **Skender** | N/A | Has VWAP but not with dual bands |
| **Tulip** | N/A | No VWAP implementation |
| **Ooples** | N/A | No dual-band VWAP |
| **TradingView** | ✅ | Reference: vwapbands.pine |

## Common Pitfalls

1. **Session Reset Timing:** Failing to reset VWAP at session boundaries causes stale historical data to dominate calculations. Use the reset parameter for intraday strategies.

2. **Multiplier Confusion:** The multiplier scales both band levels proportionally. Multiplier = 2.0 does not give you 2σ bands; it gives you 2σ and 4σ bands. Keep multiplier = 1.0 for standard statistical interpretation.

3. **Early Session Trading:** VWAP bands are unstable in the first 15-30 minutes of a session. Avoid trading based on band touches until sufficient volume accumulates.

4. **Zero Volume Handling:** Bars with zero volume are handled by substituting last valid values, but extended periods of zero volume degrade indicator quality.

5. **Memory for Reset Sessions:** When using session resets, ensure your trading system properly tracks session boundaries. Incorrect reset timing corrupts VWAP calculations.

6. **API Usage:** The `isNew` parameter controls bar correction. Use `isNew=false` when updating the current bar's value (same timestamp), `isNew=true` for new bars. The `reset` parameter should only be true at session boundaries.

## References

* TradingView (2024). VWAP Standard Deviation Bands. Pine Script Reference.
* Berkowitz, S. A., Logue, D. E., & Noser, E. A. (1988). The Total Cost of Transactions on the NYSE. The Journal of Finance, 43(1), 97-112.
* Kissell, R. (2013). The Science of Algorithmic Trading and Portfolio Management. Academic Press.

## Validation Sources

**Patterns:** Running sum accumulation, variance calculation (E[X²] - E[X]²), defensive division, NaN/Infinity handling

**External:** TradingView vwapbands.pine reference implementation

**API:** Verified against TradingView VWAP with standard deviation bands functionality