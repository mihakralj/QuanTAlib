# ATRSTOP — ATR Trailing Stop

## Overview

**ATRSTOP** is a dynamic trailing stop indicator created by Welles Wilder. It uses Average True Range (ATR) band thresholds to determine the primary trend and generates stop levels that ratchet in the trend direction. Unlike the simpler Volatility Stop (VSTOP), ATRSTOP maintains separate upper and lower bands that tighten independently, providing more nuanced trend tracking.

## Formula

### Parameters
- **Period** (`p`): ATR lookback window. Default = 21.
- **Multiplier** (`m`): ATR band width multiplier. Default = 3.0.
- **UseHighLow** (`hl`): If true, offset from High/Low; otherwise from Close. Default = false.

### Calculation Steps

1. **ATR**: Compute Average True Range using Wilder's smoothing (RMA) over `p` bars.

2. **Potential Bands** (per bar):
   - Close mode: $\text{upperEval} = \text{Close} + m \times \text{ATR}$, $\text{lowerEval} = \text{Close} - m \times \text{ATR}$
   - HighLow mode: $\text{upperEval} = \text{High} + m \times \text{ATR}$, $\text{lowerEval} = \text{Low} - m \times \text{ATR}$

3. **Band Ratcheting**:
   - Upper band tightens (decreases): $\text{UpperBand} = \text{upperEval}$ if $\text{upperEval} < \text{UpperBand}$ OR $\text{PrevClose} > \text{UpperBand}$
   - Lower band tightens (increases): $\text{LowerBand} = \text{lowerEval}$ if $\text{lowerEval} > \text{LowerBand}$ OR $\text{PrevClose} < \text{LowerBand}$

4. **Stop Assignment**:
   - Bullish: Stop = LowerBand (trailing below price)
   - Bearish: Stop = UpperBand (trailing above price)

5. **Reversal**:
   - If bullish and $\text{Close} \leq \text{LowerBand}$ → flip to bearish
   - If bearish and $\text{Close} \geq \text{UpperBand}$ → flip to bullish

## Key Properties

| Property | Value |
|:---------|:------|
| **Outputs** | 1 (stop value) |
| **Output range** | Same as price |
| **Warmup period** | `p + 1` bars |
| **Category** | Reversals |
| **Similar indicators** | VSTOP, SAR, SuperTrend, Chandelier Exit |

## Interpretation

- **Stop below price** → Bullish trend; use as trailing stop for long positions.
- **Stop above price** → Bearish trend; use as trailing stop for short positions.
- **Band ratcheting** → Bands only tighten toward price, never widen, until broken.
- **Close mode** → More responsive to price action.
- **HighLow mode** → Accounts for intrabar volatility, wider bands.

## References

- Wilder, J. Welles, Jr. *New Concepts in Technical Trading Systems* (1978).
- Skender Stock Indicators: [ATR Trailing Stop](https://dotnet.stockindicators.dev/indicators/AtrStop/)
