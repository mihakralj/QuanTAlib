# CONV: Convolution

## Overview and Purpose

The Convolution (CONV) is a flexible technical indicator that allows traders to apply any arbitrary weighting scheme (kernel) to price data. Rooted in signal processing principles developed in the 1950-60s, convolution filtering was later adapted to financial markets in the 1990s as digital signal processing techniques gained popularity in technical analysis. Convolution provides a generalized framework that enables traders to create customized moving averages with specific filtering characteristics, either by designing their own weight distributions or using predefined kernels.

## Core Concepts

* **Customizable weighting:** Convolution allows any sequence of weights to be applied to price data, enabling precise control over filtering behavior.
* **Kernel flexibility:** Supports both simple weight distributions (like those used in SMA) and complex multi-lobe designs with specialized filtering properties.
* **Market application:** Particularly valuable for traders who need to design specialized filters for specific market conditions or trading strategies.
* **Raw Dot Product:** The indicator calculates the dot product of the kernel and the price window. It does not automatically normalize the result, giving the user complete control over the magnitude.

The core innovation of convolution is its implementation of the fundamental convolution operation from signal processing. This provides a unified framework that can replicate many standard moving averages through appropriate kernel selection, while also allowing for experimentation with novel weight distributions that aren't available in standard indicators.

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `kernel` | `double[]` | Array of weights defining the filter. `kernel[0]` applies to the oldest data, `kernel[n-1]` to the newest. |

**Note:** The `period` or `length` of the indicator is determined automatically by the length of the provided kernel array.

## Formula

$$
Conv_t = \sum_{i=0}^{n-1} (kernel_i \times P_{t-(n-1)+i})
$$

Where:

* $n$ is the length of the kernel.
* $P$ is the price series.
* $kernel_i$ is the weight at index $i$.

> ⚠️ **Important:** The implementation calculates the raw dot product. If you intend to create a Moving Average, ensure your kernel weights sum to 1.0. If they sum to something else, the output will be scaled accordingly.

## C# Implementation

### Standard Usage

```csharp
// Create a custom weighted moving average (weights sum to 1.0)
double[] weights = { 0.1, 0.2, 0.3, 0.4 };
var conv = new Conv(weights);

TValue result = conv.Update(new TValue(DateTime.Now, 100.0));
Console.WriteLine(result.Value);
```

### Span API (High Performance)

```csharp
double[] weights = { 0.1, 0.2, 0.3, 0.4 };
ReadOnlySpan<double> input = ...;
Span<double> output = new double[input.Length];

Conv.Batch(input, output, weights);
```

### Bar Correction

```csharp
var conv = new Conv(weights);

// Initial update for the bar
conv.Update(new TValue(time, 100.0), isNew: true);

// Update with corrected price for the same bar
conv.Update(new TValue(time, 101.0), isNew: false);
```

## Interpretation Details

Convolution can be used in various ways depending on the kernel design:

* **Trend identification:** With appropriate kernels (e.g., Gaussian, SMA weights), convolution can identify trends while filtering out noise.
* **Specialized filtering:** Custom kernels can be designed to target specific price patterns or cycles.
* **Moving average replication:** Convolution can replicate virtually any other moving average by using the appropriate kernel.
* **Differentiation:** If weights sum to 0 (e.g., `[-1, 1]`), it acts as a momentum or rate-of-change indicator.
* **Experimental strategies:** Enables testing of novel filtering approaches not available in standard indicators.

## Limitations and Considerations

* **Knowledge requirement:** Requires understanding of convolution and filter design principles.
* **Parameter complexity:** More parameters to optimize compared to standard moving averages.
* **Potential overfitting:** Easy to create kernels that work well on historical data but fail on future data.
* **Computational demands:** Slightly higher computational requirements than hardcoded implementations, though optimized with SIMD in this library.
* **Validation necessity:** Custom kernels require thorough testing to ensure desired filtering characteristics.

## References

* Smith, S.W. "The Scientist and Engineer's Guide to Digital Signal Processing," Chapter 7: Properties of Convolution
* Ehlers, J.F. "Cycle Analytics for Traders," Wiley, 2013
* [Convolution on Wikipedia](https://en.wikipedia.org/wiki/Convolution)
