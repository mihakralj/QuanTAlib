# HTIT - Ehlers Hilbert Transform Instantaneous Trend

The Ehlers Hilbert Transform Instantaneous Trend (HTIT) is a trend-following indicator developed by John Ehlers. It uses the Hilbert Transform to measure the dominant cycle period of the market and computes an instantaneous trendline. This approach allows the indicator to adapt to changing market cycles, reducing lag while maintaining smoothness compared to traditional moving averages.

## Core Concepts

- **Hilbert Transform:** Used to decompose the price signal into in-phase and quadrature components to measure the dominant cycle period.
- **Adaptive Period:** The trendline calculation adapts its smoothing period based on the measured dominant cycle length.
- **Lag Reduction:** By adapting to the cycle, HTIT aims to provide a trendline that tracks price action more closely than static moving averages.

## Formula

The calculation involves several steps:

1. **Smooth Price:** Apply a 4-bar WMA to the input price.
    $$ Smooth[i] = \frac{4 \cdot Price[i] + 3 \cdot Price[i-1] + 2 \cdot Price[i-2] + Price[i-3]}{10} $$

2. **Detrender:** Remove the trend component to isolate the cycle.
    $$ Detrender[i] = (0.0962 \cdot Smooth[i] + 0.5769 \cdot Smooth[i-2] - 0.5769 \cdot Smooth[i-4] - 0.0962 \cdot Smooth[i-6]) \cdot Adj $$

3. **Hilbert Transform:** Compute In-Phase ($I$) and Quadrature ($Q$) components.
    $$ Q1[i] = (0.0962 \cdot Detrender[i] + 0.5769 \cdot Detrender[i-2] - 0.5769 \cdot Detrender[i-4] - 0.0962 \cdot Detrender[i-6]) \cdot Adj $$
    $$ I1[i] = Detrender[i-3] $$

4. **Period Measurement:** Calculate the dominant cycle period using the phase rate of change (Homodyne Discriminator).

5. **Instantaneous Trend:** Average the price over the dominant cycle period.
    $$ IT[i] = \frac{1}{DC} \sum_{k=0}^{DC-1} Price[i-k] $$

6. **Trendline:** Smooth the instantaneous trend.
    $$ Trendline[i] = \frac{4 \cdot IT[i] + 3 \cdot IT[i-1] + 2 \cdot IT[i-2] + IT[i-3]}{10} $$

## Parameters

HTIT does not have any user-configurable parameters. It automatically adapts to the market data.

## Usage

### CSharp

```csharp
using QuanTAlib;

// Streaming
var htit = new Htit();
TValue result = htit.Update(new TValue(time, price));

// Batch
var series = new TSeries(times, prices);
var resultSeries = Htit.Batch(series);

// Span (Zero-Allocation)
double[] input = ...;
double[] output = new double[input.Length];
Htit.Batch(input, output);
```

## Interpretation

- **Trend Direction:** When the price is above the HTIT line, the trend is considered bullish. When below, it is bearish.
- **Crossovers:** Price crossing the HTIT line can signal a potential trend reversal.
- **Support/Resistance:** The HTIT line often acts as dynamic support or resistance in trending markets.

## References

- Ehlers, John F. "Rocket Science for Traders: Digital Signal Processing Applications."
- [Skender.Stock.Indicators - HT Trendline](https://dotnet.stockindicators.dev/indicators/HtTrendline/)
