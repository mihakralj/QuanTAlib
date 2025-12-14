# VEL - Jurik Velocity

VEL (Jurik's Velocity) is a momentum oscillator that measures the rate of change of price. It is calculated as the difference between a Parabolic Weighted Moving Average (PWMA) and a Weighted Moving Average (WMA) of the same period.

## Core Concepts

- **Momentum:** Measures the speed of price movement.
- **Smoothing:** Uses moving averages to reduce noise compared to raw ROC (Rate of Change).
- **Parabolic vs Linear:** By subtracting a linear weighted average from a parabolic weighted average, VEL isolates the acceleration component of the price movement.

## Formula

$$
VEL_t = PWMA_t(n) - WMA_t(n)
$$

Where:

- $n$ is the period.
- $PWMA_t(n)$ is the Parabolic Weighted Moving Average.
- $WMA_t(n)$ is the Weighted Moving Average.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Period | int | - | The number of data points used in the calculation. Must be >= 1. |

## Usage

### Standard Usage

```csharp
using QuanTAlib;

var vel = new Vel(14);
var result = vel.Update(new TValue(DateTime.UtcNow, 100.0));
Console.WriteLine($"VEL: {result.Value}");
```

### Chaining

```csharp
var source = new Sma(10);
var vel = new Vel(source, 14);
```

### Batch Calculation (Span)

For high-performance scenarios, use the static `Calculate` method with `Span<double>`.

```csharp
double[] prices = { ... };
double[] results = new double[prices.Length];
Vel.Calculate(prices, results, 14);
```

## Interpretation

- **Zero Line Crossovers:** Crossing above zero indicates increasing upward momentum (acceleration). Crossing below zero indicates increasing downward momentum (deceleration).
- **Divergence:** Divergence between price and VEL can signal potential reversals.
- **Extremes:** High positive or negative values indicate strong momentum, which might precede a reversal or consolidation.

## References

- Jurik Research
