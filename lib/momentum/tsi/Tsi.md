# TSI: True Strength Index

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

## Performance Characteristics

### Operation Count (Streaming Mode)

| Operation | Count |
|-----------|-------|
| Subtractions | 1 |
| Absolute value | 1 |
| EMA updates | 5 |
| Division | 1 |
| Multiplication | 1 |

### Complexity

- Time: O(1) per bar (streaming)
- Space: O(1) - only EMA states maintained

### Warmup Period

warmupPeriod = longPeriod + shortPeriod + signalPeriod

Default: 25 + 13 + 13 = 51 bars

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
