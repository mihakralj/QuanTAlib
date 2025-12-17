# SuperTrend

SuperTrend is a trend-following indicator that uses Average True Range (ATR) to define upper and lower bands. It switches between the upper and lower bands based on the closing price relative to the bands, effectively acting as a trailing stop.

## Core Concepts

- **Trend Following:** Identifies the current trend direction (bullish or bearish).
- **Volatility Adjusted:** Uses ATR to adapt to market volatility.
- **Trailing Stop:** The indicator line acts as a dynamic support/resistance level.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Period | int | 10 | The lookback period for ATR calculation. |
| Multiplier | double | 3.0 | The multiplier for ATR to determine band distance. |

## Formula

$$
\begin{aligned}
TR_t &= \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|) \\
ATR_t &= RMA(TR, Period) \\
BasicUpper &= \frac{H_t + L_t}{2} + (Multiplier \times ATR_t) \\
BasicLower &= \frac{H_t + L_t}{2} - (Multiplier \times ATR_t) \\
\end{aligned}
$$

The final bands are calculated by restricting movement against the trend:

- If $BasicUpper < FinalUpper_{t-1}$ or $C_{t-1} > FinalUpper_{t-1}$, then $FinalUpper_t = BasicUpper$, else $FinalUpper_t = FinalUpper_{t-1}$.
- If $BasicLower > FinalLower_{t-1}$ or $C_{t-1} < FinalLower_{t-1}$, then $FinalLower_t = BasicLower$, else $FinalLower_t = FinalLower_{t-1}$.

The SuperTrend value switches between FinalUpper and FinalLower based on the close price.

## C# Implementation

### Standard Usage

```csharp
// Create indicator with period 10 and multiplier 3.0
var super = new Super(10, 3.0);

// Update with TBar
TBar bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
TValue result = super.Update(bar);

Console.WriteLine($"SuperTrend: {result.Value}");
Console.WriteLine($"Upper Band: {super.UpperBand.Value}");
Console.WriteLine($"Lower Band: {super.LowerBand.Value}");
Console.WriteLine($"Is Bullish: {super.IsBullish}");
```

### Batch Calculation

```csharp
// Calculate SuperTrend for an entire series
TBarSeries bars = ...;
TSeries result = Super.Batch(bars, period: 10, multiplier: 3.0);
```

### Bar Correction (isNew)

```csharp
// Update with a new bar
super.Update(bar1, isNew: true);

// Update the same bar (correction)
super.Update(bar1_corrected, isNew: false);
```

## Interpretation

- **Buy Signal:** When the price closes above the SuperTrend line (trend turns bullish).
- **Sell Signal:** When the price closes below the SuperTrend line (trend turns bearish).
- **Support/Resistance:** The SuperTrend line serves as a support level in an uptrend and resistance in a downtrend.

## References

- [Skender.Stock.Indicators - SuperTrend](https://dotnet.stockindicators.dev/indicators/SuperTrend/)
