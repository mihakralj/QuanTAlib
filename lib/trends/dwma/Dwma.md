# DWMA - Double Weighted Moving Average

DWMA is a moving average that applies the Weighted Moving Average (WMA) twice. It provides a smoother curve than a standard WMA but with slightly more lag.

## Core Concepts

* **Double Smoothing:** Applies WMA smoothing twice to reduce noise further.
* **Weighted:** Gives more weight to recent data points, similar to WMA.
* **Recursive Calculation:** Uses the efficient O(1) WMA implementation.

## Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `period` | `int` | - | The lookback period for both WMA passes. |

## Formula

$$
DWMA_t = WMA(WMA(Price, n), n)
$$

Where:

* $WMA$ is the Weighted Moving Average.
* $n$ is the period.

## C# Implementation

### Standard Usage

```csharp
// Create DWMA with period 14
var dwma = new Dwma(14);

// Update with new value
var result = dwma.Update(new TValue(DateTime.Now, 123.45));
Console.WriteLine($"DWMA: {result.Value}");
```

### Span API (High Performance)

```csharp
// Calculate on a span of data
ReadOnlySpan<double> input = ...;
Span<double> output = new double[input.Length];

Dwma.Calculate(input, output, 14);
```

### Bar Correction

```csharp
// Update with a value
dwma.Update(new TValue(time, 100), isNew: true);

// Correct the last value
dwma.Update(new TValue(time, 101), isNew: false);
```

## Interpretation

DWMA is used similarly to other moving averages to identify trends. Due to the double smoothing, it is less susceptible to whipsaws than WMA but reacts slower to price changes.

## References

* [Pine Script Implementation](https://github.com/mihakralj/pinescript/blob/main/indicators/trends_FIR/dwma.md)
