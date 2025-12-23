# BUTTER: Butterworth Filter

> "Maximally flat frequency response in the passband."

The Butterworth Filter is a signal processing tool designed to provide maximally flat frequency response in the passband. Developed by British engineer Stephen Butterworth in 1930, it offers traders a means to smooth price data without introducing ripples in the frequency response. This implementation provides a 2nd-order low-pass filter that effectively removes high-frequency market noise while preserving lower-frequency trend components. Compared to other filters, Butterworth offers an optimal compromise between smoothing efficiency and signal fidelity, making it a versatile choice for various market conditions.

## Core Concepts

- **Maximally flat response**: Provides smooth frequency response with no ripples in the passband, ensuring consistent filtering across all frequencies below the cutoff.
- **Optimal roll-off**: Offers steeper attenuation of high frequencies than Bessel filters while maintaining better phase characteristics than Chebyshev filters.
- **Market application**: Particularly effective for identifying underlying trends in noisy market conditions while introducing minimal waveform distortion.

The core innovation of the Butterworth filter is its mathematically optimal balance between opposing design constraints. The filter achieves the flattest possible frequency response in the passband without sacrificing roll-off steepness, providing traders with clean signals that maintain essential trend information while effectively eliminating random market noise.

## Mathematical Foundation

The Butterworth filter calculates a smoothed output by considering both the current price and previous filtered values. It applies carefully calculated coefficients to create a balance between smoothness and responsiveness, effectively removing random fluctuations while preserving important market trends.

Implemented as a 2nd-order IIR filter using the difference equation:

$$ y[n] = \frac{b_0 x[n] + b_1 x[n-1] + b_2 x[n-2] - a_1 y[n-1] - a_2 y[n-2]}{a_0} $$

Where coefficients are calculated as:

$$ \omega = \frac{2\pi}{L} $$
$$ \alpha = \frac{\sin(\omega)}{\sqrt{2}} $$
$$ a_0 = 1 + \alpha $$
$$ a_1 = -2 \cos(\omega) $$
$$ a_2 = 1 - \alpha $$
$$ b_0 = \frac{1 - \cos(\omega)}{2} $$
$$ b_1 = 1 - \cos(\omega) $$
$$ b_2 = \frac{1 - \cos(\omega)}{2} $$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 50M ops/s | O(1) complexity, very fast IIR implementation. |
| **Allocations** | 0 | Zero-allocation in hot path. |
| **Complexity** | O(1) | Constant time per bar. |
| **Accuracy** | 9/10 | Maximally flat passband preserves signal integrity. |
| **Timeliness** | 8/10 | Good balance of lag and smoothing. |
| **Overshoot** | 8/10 | Minimal overshoot compared to other filters. |
| **Smoothness** | 9/10 | Excellent noise suppression. |

### Zero-Allocation Design

The implementation uses a fixed-size state structure (`State` record struct) to maintain history, avoiding any heap allocations during the `Update` cycle. The coefficients are pre-calculated and stored, ensuring optimal performance.

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against PineScript reference implementation. |
| **TA-Lib** | - | Not available. |
| **Skender** | - | Not available. |
| **Tulip** | - | Not available. |

## Usage

```csharp
using QuanTAlib;

// Initialize
var butter = new Butter(period: 14);

// Update
double result = butter.Update(price).Value;

// Batch
var series = Butter.Calculate(sourceSeries, period: 14);
