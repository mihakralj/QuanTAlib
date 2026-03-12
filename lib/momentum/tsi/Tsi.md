# TSI: True Strength Index

> *True Strength Index double-smooths momentum, filtering out noise while preserving the directional signal in price change.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `longPeriod` (default 25), `shortPeriod` (default 13), `signalPeriod` (default 13)                      |
| **Outputs**      | Single series (Tsi)                       |
| **Output range** | $-100$ to $+100$                     |
| **Warmup**       | `longPeriod + shortPeriod + signalPeriod` bars (51 default)                          |
| **PineScript**   | [tsi.pine](tsi.pine)                       |

- The True Strength Index (TSI) is a momentum oscillator developed by William Blau that uses double-smoothed exponential moving averages of price mom...
- Parameterized by `longPeriod` (default 25), `shortPeriod` (default 13), `signalPeriod` (default 13).
- Output range: $-100$ to $+100$.
- Requires `longPeriod + shortPeriod + signalPeriod` bars (51 default) of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The True Strength Index (TSI) is a momentum oscillator developed by William Blau that uses double-smoothed exponential moving averages of price momentum to reduce noise and identify trend strength and direction.

## Historical Context

William Blau introduced the TSI in his 1995 book "Momentum, Direction, and Divergence." The indicator was designed to provide a smoother momentum measure by applying double exponential smoothing to price changes, reducing the whipsaws common in simpler momentum indicators.

## Algorithm and Implementation

### 1. Momentum Calculation

```csharp
mom = Price - Price[1]
absMom = |mom|
```

Price momentum captures the direction and magnitude of price change.

### 2. Double EMA Smoothing

```csharp
// First smoothing with long period
smoothedMomLong = EMA(mom, longPeriod)
smoothedAbsMomLong = EMA(absMom, longPeriod)

// Second smoothing with short period
doubleSmoothedMom = EMA(smoothedMomLong, shortPeriod)
doubleSmoothedAbsMom = EMA(smoothedAbsMomLong, shortPeriod)
```

Double smoothing reduces noise while preserving trend information.

### 3. TSI Calculation

```csharp
TSI = 100 × doubleSmoothedMom / doubleSmoothedAbsMom
```

The ratio normalizes momentum to a percentage scale.

### 4. Signal Line

```csharp
Signal = EMA(TSI, signalPeriod)
```

The signal line provides crossover signals.

## Mathematical Formula

### Core Formula

$$TSI = 100 \times \frac{EMA(EMA(Price_t - Price_{t-1}, long), short)}{EMA(EMA(|Price_t - Price_{t-1}|, long), short)}$$

### Signal Line

$$Signal = EMA(TSI, signalPeriod)$$

### Default Parameters

- Long Period: 25
- Short Period: 13
- Signal Period: 13

## Interpretation

### Range
- TSI oscillates between -100 and +100
- Positive values indicate bullish momentum
- Negative values indicate bearish momentum

### Signals
- **Zero Line Crossover**: TSI crossing above zero is bullish; below zero is bearish
- **Signal Line Crossover**: TSI crossing above signal is bullish; below is bearish
- **Divergence**: Price and TSI moving in opposite directions suggests trend reversal

### Overbought/Oversold
- Commonly used levels: +25/-25 or +30/-30
- Extreme readings suggest potential reversal

## Performance Profile

### Operation Count (Streaming Mode)

TSI(long, short, signal) maintains 5 EMA states: two first-pass EMA smoothers (mom + |mom| on `longPeriod`), two second-pass EMA smoothers (output of first pass on `shortPeriod`), and one signal EMA. All are scalar FMA operations.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Price delta (SUB) | 1 | 1 | ~1 |
| ABS of delta | 1 | 1 | ~1 |
| EMA1 mom (FMA: α_long × delta + decay × prev) | 1 | 4 | ~4 |
| EMA1 abs (FMA: α_long × |delta| + decay × prev) | 1 | 4 | ~4 |
| EMA2 mom (FMA: α_short × EMA1_mom + decay × prev) | 1 | 4 | ~4 |
| EMA2 abs (FMA: α_short × EMA1_abs + decay × prev) | 1 | 4 | ~4 |
| TSI ratio (× 100 + DIV) | 2 | 9 | ~18 |
| Signal EMA (FMA: α_sig × TSI + decay × prev) | 1 | 4 | ~4 |
| **Total** | **9** | — | **~40 cycles** |

O(1) per bar. Default WarmupPeriod = longPeriod + shortPeriod + signalPeriod = 51 bars. The division is the dominant cost; Wilder-smoothed variants can replace all EMAs with RMA (same FMA count, slower convergence).

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Price delta series | Yes | `VSUBPD` across full input span |
| ABS series | Yes | `VABSPD` — single instruction |
| First EMA pass (long period) | No | Recursive IIR; each value depends on previous |
| Second EMA pass (short period) | No | Recursive IIR on output of first pass |
| TSI ratio | Yes | `VMULPD` + `VDIVPD` once both EMA series are computed |
| Signal EMA | No | Recursive IIR |

All three EMA passes are recursive IIR filters — inherently serial. A batch implementation can vectorize the delta and ABS computation (4 bars/cycle on AVX2) before the scalar EMA sweeps. The ratio and optional signal computation can be vectorized after the EMA passes complete. Net batch speedup for long series (~1000 bars): approximately 1.3–1.5× over fully scalar.
## Validation

Cross-validated against:
- TradingView's ta.tsi()
- Stock.Indicators library
- TA-Lib implementations

## Common Pitfalls

1. **Short Warmup**: Ensure sufficient warmup period for convergence
2. **Division by Zero**: When no price movement, denominator approaches zero
3. **Lag Inherent**: Double smoothing introduces lag in trend identification
4. **Parameter Sensitivity**: Results vary significantly with period choices

## References

- Blau, William. "Momentum, Direction, and Divergence." Wiley, 1995
- Blau, William. "True Strength Index." Technical Analysis of Stocks & Commodities, 1991
- [TradingView TSI Documentation](https://www.tradingview.com/support/solutions/43000502302-true-strength-index-tsi/)
