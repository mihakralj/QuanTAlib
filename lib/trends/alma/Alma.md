# ALMA: Arnaud Legoux Moving Average

## Overview and Purpose

The Arnaud Legoux Moving Average (ALMA) is a technical indicator that attempts to bridge the gap between responsiveness and smoothness. It uses a Gaussian distribution to determine the weights of the moving average, allowing the user to shift the peak of the weight distribution (offset) and control the width of the distribution (sigma).

ALMA is designed to reduce lag while maintaining smoothness, making it superior to traditional moving averages like SMA or EMA in many trend-following applications.

## Core Concepts

* **Gaussian Weighting:** Weights are distributed according to a bell curve.
* **Offset Control:** Allows shifting the focus of the average. An offset of 0.5 is a symmetric filter (like SMA/WMA), while an offset closer to 1.0 makes it more responsive to recent prices.
* **Sigma Control:** Controls the "sharpness" of the filter. Higher sigma values include more data points in the calculation, making it smoother but potentially introducing more lag.

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| Period | 9 | The window size for the moving average. |
| Offset | 0.85 | The center of the Gaussian distribution (0.0 to 1.0). |
| Sigma | 6.0 | The standard deviation of the Gaussian distribution. |

## Formula

The weight for the $i$-th element in the window (where $i=0$ is the oldest) is calculated as:

$$W_i = \exp\left(-\frac{(i - \text{offset\_idx})^2}{2\sigma_{idx}^2}\right)$$

Where:

* $\text{offset\_idx} = \lfloor \text{Period} \times \text{Offset} \rfloor$
* $\sigma_{idx} = \text{Period} / \text{Sigma}$

The ALMA value is the weighted sum:

$$ALMA = \frac{\sum_{i=0}^{n-1} P_i \times W_i}{\sum_{i=0}^{n-1} W_i}$$

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

// Initialize with period 9, offset 0.85, sigma 6
var alma = new Alma(9, offset: 0.85, sigma: 6.0);

// Update with new value
TValue result = alma.Update(new TValue(time, price));
Console.WriteLine($"ALMA: {result.Value}");
```

### Zero-Allocation Span API

```csharp
double[] prices = ...;
double[] output = new double[prices.Length];

// Calculate ALMA for the entire array
Alma.Calculate(prices.AsSpan(), output.AsSpan(), period: 9, offset: 0.85, sigma: 6.0);
```

### Bar Correction

```csharp
var alma = new Alma(9);

// Update with initial tick
alma.Update(new TValue(time, 100), isNew: true);

// Update with correction (same bar)
alma.Update(new TValue(time, 101), isNew: false);
```

## Interpretation

* **Trend Following:** Like other moving averages, ALMA helps identify the trend direction.
* **Crossovers:** Price crossing ALMA or two ALMAs crossing each other can signal trend changes.
* **Support/Resistance:** ALMA often acts as dynamic support/resistance.

## References

* Arnaud Legoux and Dimitris Kouzis-Loukas (2009).
