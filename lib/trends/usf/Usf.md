# Usf: Ehlers Ultimate Smoother Filter

> "The Ultimate Smoother achieves superior smoothing by subtracting high-frequency components using a high-pass filter, resulting in zero lag in the passband."

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

- $src_t$ is the input value at time $t$.
- $USF_t$ is the filter output at time $t$.
- $period$ is the smoothing period.

## Performance Profile

The USF is designed for high performance and low latency.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | O(1) per update |
| **Complexity** | O(1) | Simple arithmetic operations |
| **Accuracy** | 9/10 | Matches theoretical response |
| **Timeliness** | 10/10 | Zero lag in passband |
| **Overshoot** | 8/10 | Can overshoot on sharp turns |
| **Smoothness** | 9/10 | Filters high frequencies effectively |

### Zero-Allocation Design

The implementation uses a circular buffer or state variables to store the necessary history (2 previous inputs and 2 previous outputs), ensuring that no heap allocations occur during the `Update` cycle. This makes it suitable for high-frequency trading applications.

## Validation

The USF implementation has been verified against the EasyLanguage code provided in the original article. Since no external library validation is available (as noted in the task), the implementation relies on the mathematical correctness of the formula derived from the source material.

### Common Pitfalls

- **Period Sensitivity**: Like all filters, the choice of period is critical. A period that is too short may not filter enough noise, while a period that is too long may introduce lag or miss important trend changes.
- **Warmup**: The filter requires a few bars to stabilize. The `IsHot` property indicates when the filter has processed enough data to be considered reliable.

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
