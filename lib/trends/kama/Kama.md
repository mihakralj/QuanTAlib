# KAMA: Kaufman's Adaptive Moving Average

## Overview and Purpose

Kaufman's Adaptive Moving Average (KAMA) is an intelligent technical indicator that automatically adjusts its sensitivity based on market conditions. Developed by Perry Kaufman, KAMA solves the fundamental problem of traditional moving averages: their inability to adapt to changing market volatility.

KAMA becomes more responsive during trending markets (high efficiency) and more stable during sideways or choppy conditions (low efficiency). This self-adjusting behavior makes it valuable for traders who need a single moving average that can effectively handle different market environments without manual parameter changes.

## Core Concepts

* **Efficiency Ratio (ER):** Measures the directional movement relative to volatility.
* **Market Adaptation:** Automatically adjusts sensitivity based on current price behavior.
* **Non-linear Response:** Uses a squared smoothing constant to emphasize differences between trending and non-trending states.

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| Period | 10 | The lookback window for the Efficiency Ratio. |
| Fast Period | 2 | The effective EMA period when the market is trending (ER = 1). |
| Slow Period | 30 | The effective EMA period when the market is choppy (ER = 0). |

## Formula

1. **Efficiency Ratio (ER):**
   $$ER = \frac{\text{Change}}{\text{Volatility}}$$
   $$\text{Change} = |P_t - P_{t-n}|$$
   $$\text{Volatility} = \sum_{i=0}^{n-1} |P_{t-i} - P_{t-i-1}|$$

2. **Smoothing Constant (SC):**
   $$SC = \left(ER \times (\alpha_{fast} - \alpha_{slow}) + \alpha_{slow}\right)^2$$
   $$\alpha_{fast} = \frac{2}{\text{FastPeriod} + 1}$$
   $$\alpha_{slow} = \frac{2}{\text{SlowPeriod} + 1}$$

3. **KAMA:**
   $$KAMA_t = KAMA_{t-1} + SC \times (P_t - KAMA_{t-1})$$

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

// Initialize with period 10, fast 2, slow 30
var kama = new Kama(10, fastPeriod: 2, slowPeriod: 30);

// Update with new value
TValue result = kama.Update(new TValue(time, price));
Console.WriteLine($"KAMA: {result.Value}");
```

### Zero-Allocation Span API

```csharp
double[] prices = ...;
double[] output = new double[prices.Length];

// Calculate KAMA for the entire array
Kama.Calculate(prices.AsSpan(), output.AsSpan(), period: 10, fastPeriod: 2, slowPeriod: 30);
```

### Bar Correction

```csharp
var kama = new Kama(10);

// Update with initial tick
kama.Update(new TValue(time, 100), isNew: true);

// Update with correction (same bar)
kama.Update(new TValue(time, 101), isNew: false);
```

## Interpretation

* **Trend Identification:** When price is consistently above KAMA, it indicates an uptrend. Below indicates a downtrend.
* **Trend Strength:** A steep KAMA slope suggests a strong trend. A flat KAMA suggests consolidation.
* **Support/Resistance:** KAMA often acts as dynamic support or resistance, especially during pullbacks in a trend.
* **Filter:** KAMA filters out minor fluctuations during sideways markets while remaining responsive to genuine breakouts.

## References

* Kaufman, P. (1995). *Smarter Trading*. McGraw-Hill.
* Kaufman, P. (2013). *Trading Systems and Methods*, 5th Edition. Wiley Trading.
