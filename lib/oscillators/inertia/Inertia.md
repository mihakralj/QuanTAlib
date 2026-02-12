# Inertia Oscillator

## Overview

The Inertia oscillator measures the raw distance between the current price and its linear regression forecast (Time Series Forecast). It quantifies how far price deviates from its expected trajectory, providing insight into trend strength and potential reversals.

## Origin

The Inertia concept is rooted in Donald Dorsey's work on the Relative Volatility Index (1993), where "inertia" describes the tendency of prices to continue in their current direction. The linear regression residual approach measures this momentum by comparing actual price to the statistically expected value.

## Mathematical Formula

Given a lookback period *n*:

1. **Linear regression** over the last *n* bars:
   - slope = (n·ΣxᵢYᵢ − Σxᵢ·ΣYᵢ) / (n·Σxᵢ² − (Σxᵢ)²)
   - intercept = (ΣYᵢ − slope·Σxᵢ) / n
2. **Time Series Forecast** (regression endpoint):
   - TSF = slope × (n − 1) + intercept
3. **Inertia**:
   - Inertia = source − TSF

Where x = 0 for the oldest bar, x = n−1 for the newest.

### Relationship to CFO

The Chande Forecast Oscillator normalizes the same residual:
- CFO = 100 × (source − TSF) / source
- Inertia = CFO × source / 100

## Interpretation

* **Positive values**: Price is above the regression line — bullish momentum, price exceeding expectations.
* **Negative values**: Price is below the regression line — bearish momentum, price underperforming.
* **Zero crossings**: Potential trend change signals as price crosses its forecast.
* **Magnitude**: Larger absolute values indicate stronger deviation from trend.
* **Divergence**: Price making new highs while Inertia declining suggests weakening trend.

## Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| Period | 20 | 1–500 | Lookback window for linear regression |
| Source | Close | — | Price series to analyze |

## Usage

```csharp
// Streaming
var inertia = new Inertia(period: 20);
inertia.Update(new TValue(time, close));
double value = inertia.Last.Value;

// Batch
var results = Inertia.Batch(source, period: 20);

// Span (zero-allocation)
Inertia.Batch(sourceSpan, outputSpan, period: 20);

// Event chaining
var inertia = new Inertia(source, period: 20);
```

## Limitations

* **Not bounded**: Unlike CFO (percentage) or RSI (0–100), Inertia values are in price units and vary with price level. Comparing across instruments requires normalization.
* **Linear assumption**: Assumes linear price behavior over the lookback period. Non-linear trends produce persistent non-zero residuals.
* **Lag**: The regression line is fitted to past data; rapid reversals may not be captured quickly.
* **Floating-point drift**: O(1) incremental computation may accumulate small errors over very long runs. Periodic resync mitigates this.

## References

- Dorsey, D. "The Relative Volatility Index." *Technical Analysis of Stocks & Commodities*, 1993.
- Chande, T. "The New Technical Trader." John Wiley & Sons, 1994.
- PineScript reference: `inertia.pine`
