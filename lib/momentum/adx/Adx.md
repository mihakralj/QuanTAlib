# ADX - Average Directional Index

The Average Directional Index (ADX) is a technical analysis indicator used to determine the strength of a trend. The trend can be either up or down, and this is shown by two accompanying indicators, the Negative Directional Indicator (-DI) and the Positive Directional Indicator (+DI). Therefore, ADX consists of three separate lines.

## Core Concepts

- **Trend Strength:** ADX measures the strength of the trend, not the direction.
- **Directional Movement:** +DI and -DI show the direction of the trend.
- **Range:** ADX values range from 0 to 100. Values above 25 usually indicate a strong trend.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Period | int | 14 | The number of periods used for the calculation. |

## Formula

1. **Calculate True Range (TR), +DM, and -DM:**
   $$TR = \max(High - Low, |High - PreviousClose|, |Low - PreviousClose|)$$
   $$+DM = \text{if } (High - PreviousHigh) > (PreviousLow - Low) \text{ and } (High - PreviousHigh) > 0 \text{ then } (High - PreviousHigh) \text{ else } 0$$
   $$-DM = \text{if } (PreviousLow - Low) > (High - PreviousHigh) \text{ and } (PreviousLow - Low) > 0 \text{ then } (PreviousLow - Low) \text{ else } 0$$

2. **Smooth TR, +DM, -DM:**
   Using Wilder's Moving Average (RMA) over `Period`.
   $$TR_{smooth} = RMA(TR, Period)$$
   $$+DM_{smooth} = RMA(+DM, Period)$$
   $$-DM_{smooth} = RMA(-DM, Period)$$

3. **Calculate +DI and -DI:**
   $$+DI = \frac{+DM_{smooth}}{TR_{smooth}} \times 100$$
   $$-DI = \frac{-DM_{smooth}}{TR_{smooth}} \times 100$$

4. **Calculate DX:**
   $$DX = \frac{|+DI - -DI|}{+DI + -DI} \times 100$$

5. **Calculate ADX:**
   $$ADX = RMA(DX, Period)$$

## C# Implementation

### Standard Usage

```csharp
// Create ADX with period 14
var adx = new Adx(14);

// Update with TBar
var result = adx.Update(new TBar(time, open, high, low, close, volume));
Console.WriteLine($"ADX: {result.Value}");
```

### Streaming with TBarSeries

```csharp
var adx = new Adx(14);
var series = new TBarSeries();
// ... populate series ...
var results = adx.Update(series);
```

### Batch Calculation

```csharp
var results = Adx.Batch(series, 14);
```

## Interpretation

- **ADX < 20:** Weak trend or non-trending market.
- **ADX > 25:** Strong trend.
- **ADX > 40:** Very strong trend.
- **ADX > 50:** Extremely strong trend.

Traders typically use ADX to determine whether to use a trend-following system or a range-trading system. When ADX is high, trend-following strategies are preferred. When ADX is low, range-trading strategies are preferred.

## References

- [Investopedia - Average Directional Index (ADX)](https://www.investopedia.com/terms/a/adx.asp)
- Wilder, J. Welles. *New Concepts in Technical Trading Systems*. Trend Research, 1978.
