# CCI - Commodity Channel Index

> *CCI measures how far price deviates from its statistical mean, scaled by mean absolute deviation — a z-score with character.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (CCI)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [cci.pine](cci.pine)                       |

- The Commodity Channel Index (CCI) is a versatile momentum-based oscillator developed by Donald Lambert in 1980.
- **Similar:** [RSI](../rsi/Rsi.md), [Stoch](../../oscillators/stoch/Stoch.md) | **Complementary:** ADX for trend filter | **Trading note:** Commodity Channel Index; measures deviation from statistical mean. ±100 = overbought/oversold.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

## Performance Profile

### Operation Count (Streaming Mode)

Each `Update()` call on CCI(N) performs a full O(N) mean-deviation scan over the ring buffer. There is no closed-form running-sum decomposition for mean absolute deviation — the absolute values prevent the cancellation that makes SMA or variance incremental. The RingBuffer manages the sliding window; computing MAD requires visiting every element.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| Running sum update (TP add/subtract) | 2 | 1 | ~2 |
| SMA divide | 1 | 8 | ~8 |
| MAD scan: N subtractions + N ABS | 2N | 2 | ~2N·2 |
| MAD divide | 1 | 8 | ~8 |
| Final scale + divide (0.015×MAD) | 2 | 3 | ~6 |
| **Total** | **2N + 7** | — | **~(4N + 27) cycles** |

O(N) streaming cost per bar. For the default N = 20: ~107 cycles. No incremental shortcut exists for MAD; SIMD vectorization of the scan loop is the primary optimization lever.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Typical price (H+L+C)/3 | Yes | 3-wide FMA, AVX2 vectorizable |
| Rolling SMA via prefix sums | Yes | `VADDPD` on register array |
| MAD inner loop (ABS + accumulate) | Yes | `VABSPD` + `VADDPD`, width-8 per AVX2 lane |
| Final CCI scale | Yes | scalar multiply after reduction |
| State dependency across bars (SMA) | Partial | prefix sum removes dependency; MAD is fully independent per bar |

AVX2 processes 4 doubles per instruction. For the inner MAD loop of N=20, that is 5 SIMD passes vs 20 scalar iterations — roughly 3× throughput gain. The outer bar loop remains SIMD-friendly since each bar's TP is independent once the window positions are known.