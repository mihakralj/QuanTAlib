# HP - Hodrick-Prescott Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `lambda` (default 1600.0)                      |
| **Outputs**      | Single series (HP)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | 1 bar                          |
| **Signature**    | [hp_signature](hp_signature.md) |


### TL;DR

- The Hodrick-Prescott (HP) filter is a widely used tool in macroeconomics for separating the cyclical component of a time series from raw data.
- Parameterized by `lambda` (default 1600.0).
- Output range: Tracks input.
- Requires `⌈2√λ⌉` bars of warmup before first valid output (IsHot = true). Default: **~80 bars** (λ=1600).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Trends are not lines; they are curves that we simplify for our sanity, often at the cost of reality."

The Hodrick-Prescott (HP) filter is a widely used tool in macroeconomics for separating the cyclical component of a time series from raw data. While the standard HP filter is non-causal (requiring future data), this implementation uses a causal approximation suitable for real-time streaming analysis.

## Historical Context

Developed by Robert Hodrick and Edward Prescott in 1980 (published 1997), the HP filter became the standard for detrending economic series like GDP. The original formulation solves an optimization problem to minimize the variance of the cyclical component subject to a penalty for variation in the second difference of the trend component. The causal approximation used here allows it to be applied in trading without look-ahead bias.

## Architecture & Physics

The causal HP filter approximates the spectral properties of the two-sided HP filter using a recursive IIR (Infinite Impulse Response) structure. It acts as a low-pass filter, passing long-term trends while suppressing high-frequency noise (cycles).

### Inertia and Smoothing

The filter's behavior is governed by the smoothing parameter $\lambda$ (lambda).
- Higher $\lambda$: Stiffer trend, more smoothing (allows lower frequencies).
- Lower $\lambda$: Flexible trend, less smoothing (allows higher frequencies).

The coefficient $\alpha$ is derived from $\lambda$ to approximate the frequency response of the original filter.

## Mathematical Foundation

The causal HP filter is implemented as a 2nd-order recursive equation:

### 1. Alpha Calculation

$$ \alpha = \frac{\sqrt{\lambda} \cdot 0.5 - 1.0}{\sqrt{\lambda} \cdot 0.5 + 1.0} $$
*Clamped to [0.0001, 0.9999]*

### 2. Recursive Update

$$ y_t = (1 - \alpha)x_t + \alpha y_{t-1} + 0.5\alpha(y_{t-1} - y_{t-2}) $$

Where:
- $y_t$: Trend component at time $t$
- $x_t$: Input price at time $t$
- $\lambda$: Smoothing parameter (default 1600)

## Performance Profile

### Operation Count (Streaming Mode)

Hodrick-Prescott filter: minimizes the sum of squared deviations plus a penalty on second differences. Streaming approximation via an IIR; O(1) per bar in approximation mode.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| HP IIR approximation (3 FMA, 3-point recursion) | 3 | ~4 cy | ~12 cy |
| State update (prev 2 outputs) | 2 | ~1 cy | ~2 cy |
| **Total** | **5** | — | **~14 cycles** |

O(1) per bar in the IIR approximation mode. True HP requires O(N) matrix solve at each bar, making it unsuitable for streaming. ~14 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| IIR approximation recursion | No | Sequential dependency |
| Full HP matrix solve (batch only) | Partial | Banded matrix system; parallelizable with LAPACK |

Streaming approximation: ~14 cy/bar scalar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10/10 | O(1) complexity, single recursive step. |
| **Allocations** | 0 | Zero-allocation in hot path. |
| **Complexity** | O(1) | Constant time per update. |
| **Accuracy** | 8/10 | Good approximation of standard HP for trading purposes. |
| **Timeliness** | 7/10 | Causal filter introduces phase lag, adjustable via $\lambda$. |
| **Smoothness** | 10/10 | produces very smooth trend lines. |

## Parameters

| Name | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `lambda` | `double` | 1600 | Smoothing parameter. |

Common $\lambda$ values:
- **1600**: Quarterly data (Classic macro defaults)
- **14400**: Monthly data
- **6.25**: Annual data
- **100-1000**: Common for daily trading data smoothing

## C# Usage

```csharp
// Initialize with lambda=1600
var hp = new Hp(1600);

// Update with streaming data
TValue trend = hp.Update(new TValue(time, price));

// Static batch calculation
double[] prices = ...;
double[] trend = new double[prices.Length];
Hp.Calculate(prices, trend, 1600);
