# STC: Schaff Trend Cycle

[Pine Script Implementation of STC](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/stc.pine)

## Overview and Purpose

The Schaff Trend Cycle (STC) is a technical indicator that combines elements of MACD and stochastic oscillators to identify market trends and potential reversal points. Developed by Doug Schaff in the 1990s, this indicator was designed to improve upon traditional momentum oscillators by enhancing cycle identification and reducing false signals. STC transforms the MACD line through a double stochastic process to create an oscillator that moves between 0 and 100, helping traders identify overbought and oversold conditions while maintaining trend sensitivity. Its unique construction makes it particularly effective at capturing market cycles while filtering out random price noise.

## Core Concepts

* **Hybrid oscillator design:** Combines the trend-following capabilities of MACD with the cyclical properties of stochastic indicators, creating a more responsive trend identification tool
* **Double stochastic processing:** Applies stochastic calculations twice to normalize the indicator and enhance cycle detection capabilities
* **Timeframe flexibility:** Works effectively across multiple timeframes, with adjustable parameters to suit different trading styles and market conditions

The core innovation of STC is its application of double stochastic processing to MACD values. This transformation effectively normalizes the MACD to create an oscillator with consistent boundaries, regardless of the underlying price volatility. By applying stochastic calculations twice, STC enhances cycle identification while reducing noise, creating sharper and more reliable signals than either MACD or traditional stochastic indicators alone.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Cycle Length | 10 | Controls the lookback period for stochastic calculations | Increase for smoother signals in volatile markets, decrease for more responsiveness |
| Fast Length | 23 | Period for the fast EMA in MACD calculation | Adjust based on typical cycle duration in the instrument being traded |
| Slow Length | 50 | Period for the slow EMA in MACD calculation | Increase for longer-term trends, decrease for shorter-term analysis |
| Smoothing Type | EMA | Method used to smooth the final output | Choose based on personal preference for signal clarity vs. responsiveness |

**Pro Tip:** The 25-75 threshold pair works well for identifying potential reversals, but using 20-80 can reduce false signals in volatile markets at the cost of slightly later entries and exits.

## Calculation and Mathematical Foundation

**Simplified explanation:**
STC first calculates a MACD line, then transforms it using stochastic formulas—not once, but twice. This double transformation creates a smoother oscillator that moves between 0 and 100, making it easier to identify overbought and oversold conditions as well as potential turning points in the market.

**Technical formula:**
1. MACD = EMA(Source, Fast_Length) - EMA(Source, Slow_Length)
2. Stoch_1 = EMA((MACD - Lowest_MACD)/(Highest_MACD - Lowest_MACD) × 100, 3)
   Where Lowest_MACD and Highest_MACD are over the Cycle_Length period
3. Stoch_2 = (Stoch_1 - Lowest_Stoch_1)/(Highest_Stoch_1 - Lowest_Stoch_1) × 100
   Where Lowest_Stoch_1 and Highest_Stoch_1 are over the Cycle_Length period

> 🔍 **Technical Note:** The optional smoothing methods (None, EMA, Sigmoid, Digital) offer traders flexibility in signal presentation. While EMA provides balanced smoothing, the Sigmoid option creates distinct buy and sell zones, and Digital transforms the indicator into a binary signal for automated systems.

## Interpretation Details

STC can be used in various trading strategies:

* **Trend identification:** Values above 75 suggest a strong uptrend, while values below 25 indicate a strong downtrend
* **Reversal signals:** Crossovers of the 25 and 75 levels can signal potential market reversals
* **Divergence analysis:** Comparing STC movements with price can reveal potential trend exhaustion
* **Range-bound strategies:** Oscillations between 25 and 75 can provide entry and exit points in sideways markets
* **Cross-market analysis:** Using STC across correlated instruments can help identify leading and lagging markets

## Limitations and Considerations

* **False signals:** Can generate misleading signals during strong trends, particularly when using tighter thresholds
* **Parameter sensitivity:** Performance highly dependent on appropriate parameter selection for the specific market
* **Signal lag:** Multiple smoothing operations create inherent lag in signal generation
* **Optimization requirements:** Different markets and timeframes typically require different parameter settings
* **Complementary tools:** Best used alongside price action analysis and other indicators for confirmation

## References

* Schaff, D. "The Schaff Trend Cycle," Technical Analysis of Stocks & Commodities, 2008
* Murphy, J.J. "Technical Analysis of the Financial Markets," New York Institute of Finance, 1999
