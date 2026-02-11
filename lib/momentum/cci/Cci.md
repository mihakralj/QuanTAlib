# CCI - Commodity Channel Index

## Overview

The Commodity Channel Index (CCI) is a versatile momentum-based oscillator developed by Donald Lambert in 1980. Originally designed for commodity trading, it measures the deviation of price from its statistical mean, normalized by mean absolute deviation.

## Formula

```
TP = (High + Low + Close) / 3
SMA = Simple Moving Average of TP over period
Mean Deviation = SUM(|TP - SMA|) / period
CCI = (TP - SMA) / (0.015 × Mean Deviation)
```

## Key Characteristics

| Property | Value |
|----------|-------|
| Default Period | 20 |
| Lambert Constant | 0.015 |
| Returns | Unbounded oscillator (typically -300 to +300) |
| Warmup Period | Equal to period |
| Input Data | OHLC bars (uses Typical Price) |

## Signal Interpretation

### Primary Levels
- **Above +100**: Strong uptrend, potentially overbought
- **Below -100**: Strong downtrend, potentially oversold
- **Zero Line**: Centerline crossover indicates trend change

### Trading Strategies
1. **Trend Identification**: Values consistently above/below zero indicate trend direction
2. **Overbought/Oversold**: Extreme readings (+200/-200) suggest reversal potential
3. **Divergence**: Price making new high/low while CCI fails to confirm
4. **Zero-Line Cross**: Bullish when crossing above, bearish when crossing below

## Lambert Constant (0.015)

The 0.015 constant was chosen by Lambert to ensure that approximately 70-80% of CCI values fall between +100 and -100 under normal market conditions. This provides a statistical framework where:
- Values outside ±100 indicate significant price movement
- Extended readings suggest strong trends
- Extreme values (±200 or beyond) are relatively rare

## Usage

### Basic Construction
```csharp
// Create CCI with default 20-period
var cci = new Cci();

// Create CCI with custom period
var cci = new Cci(14);
```

### Streaming Updates
```csharp
foreach (var bar in realTimeData)
{
    TValue result = cci.Update(bar);
    double cciValue = result.Value;
    
    if (cciValue > 100)
        Console.WriteLine("Overbought territory");
    else if (cciValue < -100)
        Console.WriteLine("Oversold territory");
}
```

### Batch Processing
```csharp
TSeries results = Cci.Batch(barSeries, period: 20);
```

## Comparison with Other Oscillators

| Indicator | Bounds | Best For |
|-----------|--------|----------|
| CCI | Unbounded | Trend strength, divergence |
| RSI | 0-100 | Overbought/oversold levels |
| Stochastic | 0-100 | Price position within range |

## Historical Context

- Developed by Donald Lambert (1980)
- Originally published in *Commodities* magazine
- Early application for identifying cyclical trends in commodities
- Now widely used across all asset classes

## References

- Lambert, D.R. (1980). "Commodity Channel Index: Tool for Trading Cyclic Trends"
- [TradingView CCI Documentation](https://www.tradingview.com/support/solutions/43000502001/)
- [Investopedia CCI Guide](https://www.investopedia.com/terms/c/commoditychannelindex.asp)
