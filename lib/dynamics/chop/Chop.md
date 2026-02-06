# Choppiness Index (CHOP)

The **Choppiness Index** is a non-directional volatility indicator developed by Australian commodity trader **E.W. Dreiss**. It measures whether the market is trending or trading sideways (choppy), helping traders identify optimal conditions for trend-following or range-trading strategies.

## Historical Context

E.W. Dreiss created the Choppiness Index to help traders avoid whipsaw losses by identifying market conditions unsuitable for trend-following strategies. The indicator uses a logarithmic relationship between True Range sums and price channel width to quantify market "trendiness."

## Architecture & Physics

### The Physics of Market Trendiness

The Choppiness Index compares the sum of True Range values (total price movement) to the overall price channel (net movement). In a perfect trend, these would be nearly equal—price moves efficiently in one direction. In a choppy market, True Range accumulates rapidly while net movement (price channel) remains small.

```
Trending:  Sum(TR) ≈ Price Channel → Low CHOP
Choppy:    Sum(TR) >> Price Channel → High CHOP
```

### Logarithmic Scaling

The use of LOG10 normalizes the indicator to a 0-100 scale regardless of price level or volatility magnitude:

$$\text{CHOP} = 100 \times \frac{\log_{10}\left(\frac{\sum_{i=1}^{n} TR_i}{\text{MaxHigh}_n - \text{MinLow}_n}\right)}{\log_{10}(n)}$$

## Mathematical Foundation

**True Range (TR):**
$$TR = \max(H - L, |H - C_{prev}|, |L - C_{prev}|)$$

**Choppiness Index:**
$$CHOP = 100 \times \frac{\log_{10}\left(\frac{\sum TR_n}{H_{\max} - L_{\min}}\right)}{\log_{10}(n)}$$

Where:
- $n$ = Lookback period
- $\sum TR_n$ = Sum of True Range over n bars
- $H_{\max}$ = Highest high over n bars
- $L_{\min}$ = Lowest low over n bars

## Performance Profile

| Metric | Value |
|--------|-------|
| Time Complexity | O(n) per update |
| Space Complexity | O(n) ring buffers |
| Memory per Instance | ~24n bytes |
| Allocations | Zero in hot path |

### Zero-Allocation Design

The implementation uses three ring buffers for TR values, highs, and lows. Rolling sum for TR values avoids recalculation. Min/max search is O(n) but cache-friendly due to sequential memory access.

## Interpretation

| Level | Meaning | Strategy |
|-------|---------|----------|
| > 61.8 | High choppiness | Avoid trend strategies, use range trading |
| 38.2 - 61.8 | Neutral | Mixed conditions |
| < 38.2 | Low choppiness | Market trending, use trend-following |

**Key Insight:** CHOP does not indicate direction—only whether the market is trending or consolidating.

## Usage

### Streaming (Bar-by-Bar)
```csharp
var chop = new Chop(14);

foreach (var bar in bars)
{
    TValue result = chop.Update(bar);
    
    if (chop.IsHot)
    {
        if (result.Value < 38.2)
            Console.WriteLine("Trending market - look for trend entries");
        else if (result.Value > 61.8)
            Console.WriteLine("Choppy market - avoid trend trades");
    }
}
```

### Batch Processing
```csharp
var bars = dataSource.GetBars(100);
var chopSeries = Chop.Batch(bars, period: 14);

// Access results
foreach (var value in chopSeries)
{
    Console.WriteLine($"CHOP: {value.Value:F2}");
}
```

### Bar Correction
```csharp
var chop = new Chop(14);

// New bar arrives
chop.Update(bar, isNew: true);

// Bar updates (same bar, corrected values)
chop.Update(correctedBar, isNew: false);
```

## Validation

| Reference | Match | Notes |
|-----------|-------|-------|
| TradingView | ✓ | Standard implementation |
| PineScript | ✓ | Matches chop.pine reference |

## Common Pitfalls

1. **Directional Bias**: CHOP does not indicate trend direction—use with directional indicators.
2. **Lag**: Like all indicators, CHOP lags price action; trend may start before CHOP confirms.
3. **Threshold Sensitivity**: 38.2 and 61.8 are guidelines; optimal levels vary by market.

## Related Indicators

- **ADX**: Another trend strength indicator (directional)
- **ATR**: True Range smoothed (volatility)
- **Aroon**: Trend timing based on high/low recency

## References

- Dreiss, E.W. - Original Choppiness Index development
- [TradingView CHOP Documentation](https://www.tradingview.com/support/solutions/43000501980)
