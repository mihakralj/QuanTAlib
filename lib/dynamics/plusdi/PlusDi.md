# PLUS_DI: Plus Directional Indicator

### TL;DR
Measures upward directional movement strength as a percentage (0-100).

## Introduction
The Plus Directional Indicator (+DI) measures the strength of upward price movement relative to the true range. It is one of the components of the Directional Movement System developed by J. Welles Wilder Jr.

When +DI is rising, upward price pressure is increasing. When +DI crosses above -DI, it signals a potential bullish trend. The +DI line is commonly plotted alongside -DI to visualize directional balance.

## Calculation
+DI = Smoothed(+DM) / Smoothed(TR) × 100

Where:
- +DM (Plus Directional Movement) = max(High - PrevHigh, 0) when High - PrevHigh > PrevLow - Low, else 0
- TR (True Range) = max(High - Low, |High - PrevClose|, |Low - PrevClose|)
- Smoothing uses Wilder's method: Smooth = Smooth - Smooth/N + Input

## Parameters
| Parameter | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| Period | 14 | 2-∞ | Wilder smoothing period |

## Interpretation
- **Rising +DI:** Strengthening upward movement
- **+DI > -DI:** Bulls dominate; potential uptrend
- **+DI crossover above -DI:** Bullish signal
- **High +DI (>40):** Strong upward momentum

## References
- Wilder, J. Welles Jr. "New Concepts in Technical Trading Systems" (1978)
