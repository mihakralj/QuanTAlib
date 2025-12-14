# RSX - Jurik Relative Strength X

RSX is a noise-free version of the Relative Strength Index (RSI) developed by Mark Jurik. It eliminates the lag and choppiness associated with standard RSI and its smoothed variants. RSX preserves the 0-100 bounded range and turning points of RSI but provides a much smoother signal, making it easier to identify trends and reversals without false signals from whipsaw movements.

## Core Concepts

- **Zero Lag:** Uses a specialized IIR filter chain to smooth the data without introducing significant delay.
- **Noise Reduction:** Filters out high-frequency noise while retaining the underlying trend.
- **Bounded Range:** Output is strictly bounded between 0 and 100, similar to RSI.
- **Smoothness:** Produces a clean, continuous curve suitable for precise peak/valley detection.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Period    | int  | 14      | The smoothing period (typically 8-40). |

## Formula

RSX uses a cascading filter structure. The smoothing factor $\alpha$ is derived from the period:

$$ \alpha = \frac{3}{Period + 2} $$

The algorithm processes price changes ($v_8$) through multiple smoothing stages for both the raw momentum and its absolute value. The final RSX is calculated as:

$$ RSX = \left( \frac{v_{14}}{v_{20}} + 1 \right) \times 50 $$

Where $v_{14}$ is the smoothed momentum and $v_{20}$ is the smoothed absolute momentum.

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

var rsx = new Rsx(14);
var result = rsx.Update(new TValue(DateTime.UtcNow, price));
Console.WriteLine($"RSX: {result.Value}");
```

### Span API (High Performance)

```csharp
double[] prices = { ... };
double[] results = new double[prices.Length];

Rsx.Calculate(prices, results, 14);
```

### Chaining

```csharp
var rsx = new Rsx(14);
var sma = new Sma(rsx, 3); // Smooth the RSX further
```

## Interpretation

- **Overbought/Oversold:** Values above 70 (or 80) indicate overbought conditions, while values below 30 (or 20) indicate oversold conditions.
- **Trend Confirmation:** RSX crossing 50 can signal a trend change.
- **Divergence:** Divergence between price and RSX often precedes a reversal.
- **Smoothness:** Due to its smoothness, RSX slope changes are more significant than RSI slope changes.

## References

- [Jurik Research](http://www.jurikres.com/)
- [ProRealCode - Jurik RSX](https://www.prorealcode.com/prorealtime-indicators/jurik-rsx/)
