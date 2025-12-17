# DMX - Jurik Directional Movement Index

DMX is Jurik's advanced replacement for Welles Wilder's DMI/ADX trend indicators. Traditional DMI consists of +DI, -DI (directional movement lines) and ADX (trend strength), but they suffer from noise and lag due to simplistic smoothing (Wilder's moving average). Jurik's DMX addresses this by using the ultra-low-lag Jurik Moving Average (JMA) in place of Wilder's smoothing.

The result: DMX+ and DMX- lines that are significantly smoother than classical +DI/-DI, and a combined DMX oscillator that crosses zero to signal trend direction changes with minimal lag. In fact, DMX is so smooth that a separate ADX line becomes unnecessary – the DMX oscillator itself is both a direction and strength indicator (larger magnitude = stronger trend, sign = trend direction).

## Core Concepts

- **JMA Smoothing:** Uses Jurik Moving Average instead of Wilder's Smoothing for DM+, DM-, and TR.
- **Zero-Lag:** JMA provides superior noise reduction with minimal lag compared to EMA/RMA.
- **Bipolar Oscillator:** DMX is calculated as $DI^+ - DI^-$, resulting in a single oscillator ranging from -100 to +100.
- **Trend Detection:**
  - Positive values indicate an uptrend.
  - Negative values indicate a downtrend.
  - Magnitude indicates trend strength.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Period | int | 14 | The lookback period for JMA smoothing. |

## Formula

1. **Calculate Raw Directional Movement:**
   $$
   UpMove = High_t - High_{t-1}
   $$
   $$
   DownMove = Low_{t-1} - Low_t
   $$
   $$
   DM^+_{raw} = \begin{cases} UpMove & \text{if } UpMove > DownMove \text{ and } UpMove > 0 \\ 0 & \text{otherwise} \end{cases}
   $$
   $$
   DM^-_{raw} = \begin{cases} DownMove & \text{if } DownMove > UpMove \text{ and } DownMove > 0 \\ 0 & \text{otherwise} \end{cases}
   $$

2. **Calculate True Range:**
   $$
   TR_{raw} = \max(High_t - Low_t, |High_t - Close_{t-1}|, |Low_t - Close_{t-1}|)
   $$

3. **Smooth with JMA:**
   $$
   DM^+_{smooth} = JMA(DM^+_{raw}, Period)
   $$
   $$
   DM^-_{smooth} = JMA(DM^-_{raw}, Period)
   $$
   $$
   ATR_{smooth} = JMA(TR_{raw}, Period)
   $$

4. **Calculate Directional Indicators:**
   $$
   DI^+ = 100 \times \frac{DM^+_{smooth}}{ATR_{smooth}}
   $$
   $$
   DI^- = 100 \times \frac{DM^-_{smooth}}{ATR_{smooth}}
   $$

5. **Calculate DMX:**
   $$
   DMX = DI^+ - DI^-
   $$

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

var dmx = new Dmx(14);
var bars = new TBarSeries(); 
// ... add bars ...

foreach(var bar in bars) {
    var result = dmx.Update(bar);
    Console.WriteLine($"DMX: {result.Value}");
}
```

### Batch Processing

```csharp
var resultSeries = Dmx.Batch(bars, 14);
```

## Interpretation

- **Crossover:** DMX crossing above 0 signals a potential uptrend start. Crossing below 0 signals a potential downtrend start.
- **Strength:** Higher absolute values indicate a stronger trend. Values near 0 indicate a ranging market.
- **Divergence:** Divergence between price and DMX can signal potential reversals.

## References

- Jurik Research: [DMX Description](http://www.jurikres.com/catalog/ms_dmx.htm)
