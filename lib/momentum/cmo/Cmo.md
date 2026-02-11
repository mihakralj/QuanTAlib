# CMO (Chande Momentum Oscillator)

The Chande Momentum Oscillator (CMO) is a momentum indicator developed by Tushar Chande. Unlike RSI which uses smoothed averages of gains and losses, CMO uses raw sums of up and down movements, making it more responsive to price changes. The indicator oscillates between -100 and +100.

## Formula

$$CMO = 100 \times \frac{SumUp - SumDown}{SumUp + SumDown}$$

Where:
- **SumUp** = Sum of positive price changes over the period
- **SumDown** = Sum of absolute negative price changes over the period

## Key Characteristics

| Property | Value |
|----------|-------|
| Output Range | **-100 to +100** |
| Zero Line | Neutral momentum |
| Overbought | Above +50 |
| Oversold | Below -50 |
| Default Period | 14 |

## Comparison with RSI

| Feature | CMO | RSI |
|---------|-----|-----|
| Range | [-100, +100] | [0, 100] |
| Smoothing | None (raw sums) | RMA (exponential) |
| Sensitivity | Higher | Lower |
| Zero crossing | Valid signal | N/A (50 is neutral) |

## Usage

```csharp
// Create CMO indicator
var cmo = new Cmo(period: 14);

// Single value update
var result = cmo.Update(new TValue(time, price));

// Batch calculation
var results = Cmo.Batch(priceData, period: 14);

// Subscribe to source
var cmo = new Cmo(sourceIndicator, period: 14);
```

## Interpretation

1. **Overbought/Oversold**
   - CMO > +50: Overbought conditions
   - CMO < -50: Oversold conditions
   - Extreme readings (±70) suggest stronger signals

2. **Zero Line Crossings**
   - Crossing above zero: Bullish momentum
   - Crossing below zero: Bearish momentum

3. **Divergences**
   - Price makes new high, CMO doesn't: Bearish divergence
   - Price makes new low, CMO doesn't: Bullish divergence

4. **Signal Line**
   - Some traders use a 9-period EMA of CMO as a signal line

## Implementation Details

- O(1) streaming updates using circular buffers
- SIMD-optimized batch calculations
- Zero heap allocations in hot paths
- Handles NaN and edge cases gracefully

## Sources

- Chande, Tushar S. "The New Technical Trader" (1994)
- Chande, Tushar S. & Kroll, Stanley. "Beyond Technical Analysis" (1997)
- [StockCharts - CMO](https://school.stockcharts.com/doku.php?id=technical_indicators:chande_momentum_oscillator)
