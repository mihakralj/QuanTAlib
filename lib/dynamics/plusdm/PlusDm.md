# PLUS_DM: Plus Directional Movement

### TL;DR
Wilder-smoothed upward directional movement in price units (≥0).

## Introduction
Plus Directional Movement (+DM) measures the magnitude of upward price movement, smoothed using Wilder's method. Unlike +DI which normalizes by true range to produce a percentage, +DM outputs raw smoothed values in price units.

+DM captures when the current bar's high exceeds the previous bar's high by more than the previous bar's low exceeds the current bar's low. It is the raw building block of the Directional Movement System.

## Calculation
+DM = max(High - PrevHigh, 0) when High - PrevHigh > PrevLow - Low, else 0

Smoothed using Wilder's method: Smooth = Smooth - Smooth/N + Input

## Parameters
| Parameter | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| Period | 14 | 2-∞ | Wilder smoothing period |

## Interpretation
- **Rising +DM:** Increasing upward price extension
- **+DM > -DM:** Upward movement exceeds downward movement
- **Zero +DM:** No upward directional movement on the bar
- Values are in price units and scale with the instrument

## References
- Wilder, J. Welles Jr. "New Concepts in Technical Trading Systems" (1978)
