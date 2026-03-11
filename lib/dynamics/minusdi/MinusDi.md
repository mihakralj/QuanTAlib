# MINUS_DI: Minus Directional Indicator

Measures downward directional movement strength as a percentage (0-100).

## Introduction
The Minus Directional Indicator (-DI) measures the strength of downward price movement relative to the true range. It is one of the components of the Directional Movement System developed by J. Welles Wilder Jr.

When -DI is rising, downward price pressure is increasing. When -DI crosses above +DI, it signals a potential bearish trend. The -DI line is commonly plotted alongside +DI to visualize directional balance.

## Calculation
-DI = Smoothed(-DM) / Smoothed(TR) × 100

Where:
- -DM (Minus Directional Movement) = max(PrevLow - Low, 0) when PrevLow - Low > High - PrevHigh, else 0
- TR (True Range) = max(High - Low, |High - PrevClose|, |Low - PrevClose|)
- Smoothing uses Wilder's method: Smooth = Smooth - Smooth/N + Input

## Parameters
| Parameter | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| Period | 14 | 2-∞ | Wilder smoothing period |

## Interpretation
- **Rising -DI:** Strengthening downward movement
- **-DI > +DI:** Bears dominate; potential downtrend
- **-DI crossover above +DI:** Bearish signal
- **High -DI (>40):** Strong downward momentum

## References
- Wilder, J. Welles Jr. "New Concepts in Technical Trading Systems" (1978)
