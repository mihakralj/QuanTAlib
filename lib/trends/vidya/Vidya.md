# VIDYA (Variable Index Dynamic Average)

## Overview and Purpose

The Variable Index Dynamic Average (VIDYA) is an adaptive technical indicator designed to automatically adjust its sensitivity based on market volatility. Developed by Tushar Chande in the early 1990s and introduced in his 1992 article in *Technical Analysis of Stocks & Commodities* magazine, VIDYA represents a significant innovation in moving average technology.

Unlike traditional moving averages with fixed parameters, VIDYA becomes more responsive during trending, volatile markets and more stable during quiet, sideways markets. This self-adjusting behavior makes it particularly valuable for traders navigating markets that frequently alternate between trending and consolidation phases without requiring manual parameter changes.

## Core Concepts

- **Volatility-based adaptation:** Automatically adjusts the effective smoothing period based on recent market volatility.
- **Dynamic smoothing:** Uses volatility measurements to determine how quickly the moving average responds to price changes.
- **Trend sensitivity:** Becomes more responsive during strong directional price moves and more stable during sideways consolidation.
- **Noise filtering:** Reduces whipsaws during low-volatility periods while capturing significant moves during high-volatility periods.

VIDYA achieves its adaptive nature by scaling the standard exponential moving average (EMA) smoothing factor by a volatility ratio. This creates a moving average that effectively adjusts its own period based on market conditions - shortening during volatile trending markets and lengthening during consolidation.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Period | 14 | Base smoothing period | Increase for less sensitivity to short-term trends, decrease for more responsiveness. |
| Source | Close | Data point used for calculation | Change to HL2 or HLC3 for more balanced price representation. |

**Pro Tip:** Many professional traders find that using the golden ratio (0.618) to determine the relationship between Period and VI Period (e.g., VI Period = Period × 0.382) can enhance performance by creating a more harmonious response to market cycles.

## Calculation and Mathematical Foundation

**Simplified explanation:**
VIDYA works by measuring volatility as the ratio between short-term and longer-term standard deviations. It then uses this ratio to adjust how quickly the moving average responds. When volatility is high, VIDYA follows price more closely; when volatility is low, VIDYA moves more slowly, preserving the prior trend direction.

**Technical formula:**
This implementation uses the Chande Momentum Oscillator (CMO) as the volatility index, as originally proposed by Chande.

$$
\begin{aligned}
\alpha &= \frac{2}{Period + 1} \\
CMO &= \frac{\sum Up - \sum Down}{\sum Up + \sum Down} \\
VI &= |CMO| \\
\alpha_{dynamic} &= \alpha \times VI \\
VIDYA_t &= \alpha_{dynamic} \times Price_t + (1 - \alpha_{dynamic}) \times VIDYA_{t-1}
\end{aligned}
$$

Where:

- $\alpha$ is the base smoothing factor.
- $VI$ is the Volatility Index (normalized to 0-1), derived from the absolute value of CMO.
- $Up$ is the sum of positive price changes over the period.
- $Down$ is the sum of negative price changes (absolute values) over the period.

> 🔍 **Technical Note:** Some implementations of VIDYA use different volatility measurements such as standard deviation ratios or RSI-based volatility. The core concept remains the same - scaling the smoothing factor based on a measure of market activity. This library uses the CMO-based approach for its direct measurement of directional momentum.

## C# Implementation

### Standard Usage

```csharp
// Create VIDYA with period 14
var vidya = new Vidya(14);

// Update with new price
var result = vidya.Update(new TValue(DateTime.UtcNow, 100.0));
Console.WriteLine($"VIDYA: {result.Value}");
```

### Static API (High Performance)

```csharp
// Calculate VIDYA for an entire array
double[] prices = { ... };
double[] results = new double[prices.Length];

Vidya.Batch(prices, results, 14);
```

### Bar Correction (Streaming)

```csharp
// Update with a developing bar (isNew = false)
vidya.Update(new TValue(time, close), isNew: false);
```

## Interpretation Details

VIDYA provides several key insights for traders:

- When price consistently stays above VIDYA, it confirms an uptrend.
- When price consistently stays below VIDYA, it confirms a downtrend.
- When VIDYA's slope is steep, it indicates a strong trend with high volatility.
- When VIDYA flattens despite price fluctuations, it suggests the market is in a low-volatility state.
- Crossovers between price and VIDYA often signal potential trend changes.
- VIDYA tends to act as dynamic support/resistance during trending markets.

VIDYA is particularly valuable in markets that experience varying levels of volatility, as it automatically adjusts its behavior to match current conditions. It excels in trend-following strategies where traditional moving averages might generate false signals during quiet periods or fail to capture explosive moves quickly enough.

## Limitations and Considerations

- **Market conditions:** May still produce some false signals during periods of choppy volatility.
- **Lag factor:** While adaptive, VIDYA still exhibits some lag, especially during the transition from low to high volatility.
- **Parameter sensitivity:** Performance can vary significantly based on both period settings and volatility calculation method.
- **Calculation complexity:** More computationally intensive than standard moving averages.
- **Complementary tools:** Works best when combined with volume analysis or non-volatility based indicators for confirmation.

## References

1. Chande, T. (1992). "Adapting Moving Averages to Market Volatility," *Technical Analysis of Stocks & Commodities*.
2. Chande, T. & Kroll, S. (1994). *The New Technical Trader*. John Wiley & Sons.
3. Kaufman, P. (2013). *Trading Systems and Methods*, 5th Edition. Wiley Trading.
