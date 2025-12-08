# HMA: Hull Moving Average

[Pine Script Implementation of HMA](https://github.com/mihakralj/pinescript/blob/main/indicators/trends_FIR/hma.pine)

## Overview and Purpose

The Hull Moving Average (HMA), developed by Alan Hull in 2005, is designed to solve the age-old problem of making a moving average more responsive to current price activity while maintaining curve smoothness. It achieves this by eliminating lag almost entirely and managing to improve smoothing at the same time.

## Core Concepts

* **Lag Reduction:** Uses weighted moving averages (WMA) in a specific combination to offset lag.
* **Smoothness:** The final smoothing step ensures the indicator remains readable and not overly jittery.
* **Formula:** $HMA = WMA(\sqrt{n}, 2 \cdot WMA(n/2, price) - WMA(n, price))$

## Calculation

1. Calculate a WMA with period $n/2$ and multiply by 2.
2. Calculate a WMA with period $n$ and subtract from step 1.
3. Calculate a WMA with period $\sqrt{n}$ using the result of step 2.

## C# Implementation

```csharp
using QuanTAlib;

// Initialize
var hma = new Hma(14);

// Update
var result = hma.Update(new TValue(time, price));

// Batch
var series = Hma.Calculate(sourceSeries, 14);
```

## Performance

* **Streaming:** O(1) complexity per update (uses 3 internal O(1) WMAs).
* **Batch:** Uses SIMD-optimized WMA calculations and vector operations for the intermediate step.
* **Zero Allocation:** Span-based API available for high-performance scenarios.

## References

* [Alan Hull's HMA Description](https://alan.hull.com.au/hma.html)
