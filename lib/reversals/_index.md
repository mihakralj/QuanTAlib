# Reversals

> "Pivot point is hypothesis, not prophecy. Mathematics identifies levels where crowd psychology may shift. Market decides whether to respect calculation or ignore it entirely."

Tools indicating potential reversals, support/resistance, or pivot points. These indicators identify price levels where trend exhaustion or continuation decisions occur.

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| FRACTALS | Williams Fractals | 📋 | Five-bar pattern identifying local peaks/troughs; marks support/resistance levels. |
| PIVOT | Pivot Points (Classic) | 📋 | Standard floor trader pivots with 7 levels (PP, R1-R3, S1-S3). |
| PIVOTCAM | Camarilla Pivot Points | 📋 | Mean-reversion pivots with 9 levels; R3/S3 are key reversal zones. |
| PIVOTDEM | DeMark Pivot Points | 📋 | Minimalist trend-following pivots with only 3 levels and conditional logic. |
| PIVOTEXT | Extended Traditional Pivots | 📋 | Extended pivots with 11 levels (R1-R5, S1-S5) for volatile markets. |
| PIVOTFIB | Fibonacci Pivot Points | 📋 | Fibonacci-ratio based pivots; Golden Ratio (61.8%) at R2/S2. |
| PIVOTWOOD | Woodie's Pivot Points | 📋 | Weighted close pivots (2× close weight) for intraday trading. |
| PSAR | Parabolic Stop And Reverse | 📋 | Trailing stop indicator that accelerates with trend; provides entry/exit signals via SAR dots. |
| SWINGS | Swing High/Low Detection | 📋 | Identifies significant price reversals and swing points using configurable lookback. |

## Selection Guide

**For intraday trading:** Classic PIVOT provides baseline levels. PIVOTWOOD emphasizes closing price for day-session context. PIVOTCAM targets mean-reversion at R3/S3 zones.

**For swing trading:** PIVOTFIB uses Fibonacci ratios aligned with retracement analysis. PIVOTEXT provides extended levels for multi-day moves. FRACTALS marks structural highs/lows.

**For trend-following:** PSAR provides trailing stop with acceleration. PIVOTDEM uses conditional logic based on prior bar relationship. SWINGS identifies trend reversal points.

**For volatile markets:** PIVOTEXT with 11 levels captures extreme moves. PIVOTCAM's outer levels (R4/S4) act as volatility breakout zones.

## Pivot Point Comparison

| System | Levels | Formula Basis | Trading Style |
| :--- | :---: | :--- | :--- |
| Classic | 7 | (H+L+C)/3 | General purpose |
| Woodie | 7 | (H+L+2C)/4 | Intraday, close-weighted |
| Camarilla | 9 | Range × multipliers | Mean-reversion |
| DeMark | 3 | Conditional on O/C relationship | Trend-following |
| Fibonacci | 7 | PP ± (H-L) × Fib ratios | Retracement alignment |
| Extended | 11 | Classic + outer levels | High volatility |

## Pivot Level Calculations

| Level | Classic | Woodie | Camarilla |
| :--- | :--- | :--- | :--- |
| R4 | — | — | C + (H-L) × 1.5/2 |
| R3 | 2×PP - 2×L | — | C + (H-L) × 1.25/4 |
| R2 | PP + (H-L) | PP + (H-L) | C + (H-L) × 1.1/6 |
| R1 | 2×PP - L | 2×PP - L | C + (H-L) × 1.1/12 |
| PP | (H+L+C)/3 | (H+L+2C)/4 | — |
| S1 | 2×PP - H | 2×PP - H | C - (H-L) × 1.1/12 |
| S2 | PP - (H-L) | PP - (H-L) | C - (H-L) × 1.1/6 |
| S3 | 2×PP - 2×H | — | C - (H-L) × 1.25/4 |
| S4 | — | — | C - (H-L) × 1.5/2 |

## Reversal Pattern Types

| Pattern | Indicator | Bars Required | Signal Type |
| :--- | :--- | :---: | :--- |
| Williams Fractal Up | FRACTALS | 5 | Resistance marked at middle high |
| Williams Fractal Down | FRACTALS | 5 | Support marked at middle low |
| Swing High | SWINGS | Configurable | Local maximum confirmation |
| Swing Low | SWINGS | Configurable | Local minimum confirmation |
| SAR Flip | PSAR | 1 | Trend reversal signal |

## PSAR Mechanics

Parabolic SAR uses acceleration factor that increases with each new extreme:

| Parameter | Default | Range | Effect |
| :--- | :---: | :--- | :--- |
| Initial AF | 0.02 | 0.01-0.05 | Starting sensitivity |
| AF Step | 0.02 | 0.01-0.05 | Acceleration rate |
| Max AF | 0.20 | 0.10-0.30 | Maximum sensitivity |

Higher AF values create tighter stops (more whipsaws, earlier exits). Lower AF values create wider stops (fewer signals, later exits).