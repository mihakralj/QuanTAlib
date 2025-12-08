# HMA: Hull Moving Average

## Overview and Purpose

The Hull Moving Average (HMA) is a technical indicator designed to significantly reduce lag while maintaining smoothness in price data interpretation. Developed by Australian mathematician and trader Alan Hull in 2005, the HMA was created specifically to address the lagging nature of conventional moving averages. Hull sought to create an indicator that maintained effective smoothing capabilities while improving responsiveness, publishing his approach in "Better Trading with the Hull Moving Average" (2005). Through its multi-stage calculation process involving weighted moving averages and square-root period weighting, HMA provides traders with a more responsive tool for identifying trends and potential reversals.

## Core Concepts

* **Reduced lag:** HMA substantially decreases the delay in trend identification compared to traditional moving averages
* **Smoothing preservation:** Maintains effective noise filtering despite its increased responsiveness
* **Market application:** Particularly effective for timing entries and exits in trending markets where minimizing lag is critical
* **Timeframe flexibility:** Functions effectively across all timeframes with period adjustments to suit trading style

The core innovation of HMA is its unique three-stage process that includes weighted averaging at different timeframes, followed by a momentum-enhanced smoothing phase. By applying weight calculations at half the specified period, then taking the difference between this result and the full-period calculation, and finally smoothing that difference with a square-root weighted calculation, HMA creates a moving average that anticipates price movements rather than simply following them.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Length | 9 | Controls the primary calculation period | Increase for smoother signals in volatile markets, decrease for more responsiveness |
| Source | close | Price data used for calculation | Consider using hlc3 for a more balanced price representation |

**Pro Tip:** Using square numbers (4, 9, 16, 25, 36) as periods can produce optimal results with HMA due to the square root operation in the final calculation step.

## Calculation and Mathematical Foundation

**Simplified explanation:**
HMA first calculates two weighted moving averages - one using half the specified period (which responds quickly) and one using the full period (which is smoother). It then doubles the faster WMA and subtracts the slower WMA to create a difference that emphasizes recent price direction. Finally, it applies another weighted moving average using the square root of the original period to smooth this difference.

**Technical formula:**

1. Calculate WMA with period n/2: WMA₁ = WMA(price, n/2)
2. Calculate WMA with period n: WMA₂ = WMA(price, n)
3. Calculate the difference: diff = 2 × WMA₁ - WMA₂
4. Calculate final HMA: HMA = WMA(diff, √n)

Where:

* n is the specified period
* √n is the square root of n (rounded down)

> 🔍 **Technical Note:** The 2× multiplier applied to the faster WMA serves to amplify the momentum component, helping the HMA anticipate rather than just follow price movements.

## Interpretation Details

HMA can be used in various trading strategies:

* **Trend identification:** The direction of HMA indicates the prevailing trend
* **Signal generation:** Crossovers between price and HMA generate trade signals earlier than with traditional moving averages
* **Support/resistance levels:** HMA can act as dynamic support during uptrends and resistance during downtrends
* **Trend strength assessment:** The angle of the HMA line can indicate trend strength
* **Multiple timeframe analysis:** Using HMAs with different periods can confirm trends across different timeframes

## Limitations and Considerations

* **Market conditions:** Less effective in ranging or choppy markets where increased responsiveness may generate false signals
* **Overshooting:** The aggressive lag reduction can cause overshooting during sharp reversals
* **Amplitude distortion:** The 2× multiplier in the formula can exaggerate price movements
* **Gap sensitivity:** More prone to creating gaps in the moving average line during price gaps
* **Complementary tools:** Best used alongside momentum oscillators or volume indicators for confirmation

## References

* Hull, Alan. "Better Trading with the Hull Moving Average." MTA Symposium Proceedings, 2005
