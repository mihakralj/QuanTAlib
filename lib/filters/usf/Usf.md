# USF: Ehlers Ultimate Smoother Filter

> *The Ultimate Smoother achieves superior smoothing by subtracting high-frequency components using a high-pass filter, resulting in zero lag in the passband.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Usf)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [usf.pine](usf.pine)                       |
| **Signature**    | [usf_signature](usf_signature.md) |

- The Ultimate Smoother Filter (USF) is a zero-lag smoothing filter introduced by John Ehlers in the April 2024 issue of *Technical Analysis of Stock...
- **Similar:** [SSF2](../ssf2/Ssf2.md), [Kalman](../kalman/Kalman.md) | **Complementary:** ATR for volatility | **Trading note:** Universal Smoothing Filter; combines multiple filter approaches adaptively.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Ultimate Smoother Filter (USF) is a zero-lag smoothing filter introduced by John Ehlers in the April 2024 issue of *Technical Analysis of Stocks & Commodities*. It builds upon the Super Smoother Filter (SSF) by using a high-pass filter to remove high-frequency noise, leaving a smooth low-frequency component with minimal lag.

## Historical Context

John Ehlers is a prolific author and technical analyst known for applying digital signal processing (DSP) techniques to trading. The Ultimate Smoother is one of his latest contributions, designed to overcome the lag inherent in traditional low-pass filters. By subtracting the high-frequency components (noise) from the original signal, the filter isolates the trend component with exceptional fidelity and responsiveness.

## Architecture & Physics

The USF operates on the principle of spectral decomposition. It separates the signal into high-frequency and low-frequency components. The high-frequency component is extracted using a high-pass filter, and this component is then subtracted from the original signal. The result is a low-frequency trend that retains the phase characteristics of the original signal, effectively eliminating lag in the passband.

### Zero-Lag Design

Traditional moving averages (like SMA or EMA) introduce lag because they average past prices. The USF, by contrast, uses a 2-pole Butterworth filter architecture to achieve a sharp cutoff and minimal phase delay. The "ultimate" aspect comes from the specific coefficients and the subtraction method, which Ehlers claims provides the best balance of smoothing and responsiveness.

## Mathematical Foundation

The USF calculation involves several steps to derive the filter coefficients and the final smoothed value.

### 1. Calculate Argument

$$ arg = \frac{\sqrt{2} \cdot \pi}{period} $$

### 2. Calculate Coefficients

$$ c_2 = 2 \cdot e^{-arg} \cdot \cos(arg) $$
$$ c_3 = -e^{-2 \cdot arg} $$
$$ c_1 = \frac{1 + c_2 - c_3}{4} $$

### 3. Calculate USF

$$ USF_t = (1 - c_1) \cdot src_t + (2 \cdot c_1 - c_2) \cdot src_{t-1} - (c_1 + c_3) \cdot src_{t-2} + c_2 \cdot USF_{t-1} + c_3 \cdot USF_{t-2} $$

Where:

* $src_t$ is the input value at time $t$.
* $USF_t$ is the filter output at time $t$.
* $period$ is the smoothing period.

## Performance Profile

### Operation Count (Streaming Mode)

Ehlers Ultimate Smoother Filter (USF): 2-pole IIR low-pass filter with high-pass subtraction for zero-lag smoothing. Five FMA operations per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input combination (3 taps) | 3 | ~4 cy | ~12 cy |
| Feedback (2 taps) | 2 | ~4 cy | ~8 cy |
| State update | 4 | ~1 cy | ~4 cy |
| **Total** | **9** | — | **~24 cycles** |

O(1) per bar. Coefficients derived from period parameter; precomputed. ~24 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| USF recursion | No | y[n] depends on y[n-1] and y[n-2] |

Batch throughput: ~24 cy/bar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) per update. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Simple arithmetic operations. |
| **Accuracy** | 9 | Matches theoretical response. |
| **Timeliness** | 10 | Zero lag in passband. |
| **Overshoot** | 8 | Can overshoot on sharp turns. |
| **Smoothness** | 9 | Filters high frequencies effectively. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | N/A | Not implemented. |
| **Skender** | N/A | Not implemented. |
| **Tulip** | N/A | Not implemented. |
| **Ooples** | N/A | Not implemented. |

### Common Pitfalls

* **Period Sensitivity**: Like all filters, the choice of period is critical. A period that is too short may not filter enough noise, while a period that is too long may introduce lag or miss important trend changes.
* **Warmup**: The filter requires a few bars to stabilize. The `IsHot` property indicates when the filter has processed enough data to be considered reliable.

## C# Usage Examples

```csharp
// Initialize with a period of 20
var usf = new Usf(20);

// Update with new price data
TValue result = usf.Update(new TValue(DateTime.UtcNow, 100.0));

// Access the latest value
Console.WriteLine($"Current USF: {usf.Last.Value}");

// Use in a TSeries chain
var source = new TSeries();
var usfSeries = new Usf(source, 20);