# VSTOP — Volatility Stop (Wilder's Volatility System)

## Overview

**VSTOP** is an ATR-based trailing stop indicator created by J. Welles Wilder. It determines trend direction using a "Significant Close" (SIC) concept — the highest close during an uptrend or lowest close during a downtrend. The stop-and-reverse (SAR) line trails price at a fixed ATR multiple distance from the SIC.

When price crosses through the SAR level, the trend flips — making it suitable for trend detection, dynamic stop-loss placement, and reversal signals.

## Formula

### Parameters
- **Period** (`p`): ATR lookback window. Default = 7.
- **Multiplier** (`m`): ATR band width. Default = 3.0.

### Calculation Steps

1. **ATR**: Compute Average True Range using Wilder's smoothing (RMA) over `p` bars.
2. **SIC (Significant Close)**:
   - Uptrend: $\text{SIC} = \max(\text{SIC}, \text{Close})$
   - Downtrend: $\text{SIC} = \min(\text{SIC}, \text{Close})$
3. **SAR**:
   - Uptrend: $\text{SAR} = \text{SIC} - m \times \text{ATR}$
   - Downtrend: $\text{SAR} = \text{SIC} + m \times \text{ATR}$
4. **Reversal**: If Close crosses SAR → flip direction, reset SIC to current Close, recalculate SAR.

### Initial Trend Direction

The initial trend guess is determined by comparing the first Close value with the Close value at the end of the warmup period. If `Close[period] >= Close[0]`, the initial trend is long (uptrend); otherwise short (downtrend).

## Key Properties

| Property | Value |
|:---------|:------|
| **Outputs** | 1 (SAR value) |
| **Output range** | Same as price |
| **Warmup period** | `p` bars |
| **Category** | Reversals |
| **Similar indicators** | SAR, SuperTrend, ATR Trailing Stop |

## Interpretation

- **SAR below price** → Uptrend; SAR serves as trailing stop for long positions.
- **SAR above price** → Downtrend; SAR serves as trailing stop for short positions.
- **SAR flip** → Trend reversal signal; `IsStop = true`.
- **Higher multiplier** → Wider stop distance, fewer reversals (smoother trend).
- **Lower multiplier** → Tighter stop, more sensitive to reversals.

## References

- Wilder, J. Welles, Jr. *New Concepts in Technical Trading Systems* (1978).
- Skender Stock Indicators: [Volatility Stop](https://dotnet.stockindicators.dev/indicators/VolatilityStop/)
