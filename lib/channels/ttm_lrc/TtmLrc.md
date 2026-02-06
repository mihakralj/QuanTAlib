# TTM_LRC: TTM Linear Regression Channel

> **Pending Implementation** - Placeholder for John Carter's TTM LRC indicator

## Historical Context

John Carter's TTM LRC (Linear Regression Channel) provides a clean, statistically-based price channel using linear regression analysis. Unlike Bollinger Bands which measure volatility around a moving average, LRC measures price deviation from the trend line, making it particularly useful for identifying overbought/oversold conditions within a defined trend.

## Algorithm

### Linear Regression Line
```
// Least squares regression over N periods
slope = (N * ΣXY - ΣX * ΣY) / (N * ΣX² - (ΣX)²)
intercept = (ΣY - slope * ΣX) / N
midline = intercept + slope * (current_bar - start_bar)
```

### Standard Deviation Bands
```
residuals = close - linreg_value
stddev = sqrt(Σ(residuals²) / N)

upper_band_1 = midline + 1 * stddev
lower_band_1 = midline - 1 * stddev
upper_band_2 = midline + 2 * stddev
lower_band_2 = midline - 2 * stddev
```

## Default Parameters

| Parameter | Value | Description |
|:----------|:------|:------------|
| Length | 100 | Regression lookback period |
| Deviations | 2.0 | Number of standard deviations for outer bands |
| ShowMidline | true | Display the regression line |
| ShowInnerBands | true | Display ±1σ bands |

## Outputs

| Output | Type | Description |
|:-------|:-----|:------------|
| Midline | double | Linear regression value (trend line) |
| Upper1 | double | +1 standard deviation band |
| Lower1 | double | -1 standard deviation band |
| Upper2 | double | +2 standard deviation band |
| Lower2 | double | -2 standard deviation band |
| Slope | double | Current regression slope (trend direction) |
| RSquared | double | Coefficient of determination (trend quality) |

## Band Interpretation

| Zone | Statistical Meaning | Trading Implication |
|:-----|:--------------------|:--------------------|
| Above +2σ | 2.5% probability | Extremely overbought |
| +1σ to +2σ | 13.5% probability | Overbought |
| -1σ to +1σ | 68% probability | Normal range |
| -2σ to -1σ | 13.5% probability | Oversold |
| Below -2σ | 2.5% probability | Extremely oversold |

## Trading Strategy

1. **Trend Following:** Trade in direction of slope when price bounces off midline
2. **Mean Reversion:** Fade moves to ±2σ bands when R² is high
3. **Breakout:** Watch for sustained moves beyond ±2σ as trend acceleration signals

## Category

**Channels** - Linear regression-based price channel with statistical deviation bands.

## See Also

- [REGCHANNEL: Linear Regression Channel](../regchannel/RegChannel.md)
- [SDCHANNEL: Standard Deviation Channel](../sdchannel/SdChannel.md)
- [BBANDS: Bollinger Bands](../bbands/Bbands.md)
- [TTM_SQUEEZE: TTM Squeeze](../../dynamics/ttm_squeeze/TtmSqueeze.md)
