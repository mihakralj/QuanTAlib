# HPF - Highpass Filter (2-Pole)

> "Noise is just signal you haven't figured out how to filter yet. Or maybe, it's the only signal that matters."

The 2-Pole Highpass Filter (HPF) is designed to separate high-frequency components (like cycles and noise) from the underlying trend. By suppressing low-frequency movements, it acts as a "detrender," making it invaluable for oscillator construction and cycle analysis.

## Historical Context

Highpass filters are fundamental in signal processing, complementing lowpass filters (like SMAs or EMAs). While moving averages smooth out noise to reveal the trend, highpass filters remove the trend to reveal the noise (or cycles). This specific implementation produces a 2-pole Infinite Impulse Response (IIR) filter, offering a steeper cutoff and better frequency separation than simple difference methods (like `Close - SMA`).

## Architecture & Physics

This filter uses a recursive 2-pole structure derived from a cosine-based alpha approximation. It is architected for O(1) streaming performance, updating its state with constant complexity regardless of the cutoff length.

### Frequency Response

The filter is tuned via a `Lengths` parameter which defines the cutoff period.

- Frequencies **lower** than the cutoff (longer trends) are attenuated.
- Frequencies **higher** than the cutoff (shorter cycles) are passed.

## Mathematical Foundation

The filter coefficients are derived from the cutoff length $L$:

### 1. Alpha Calculation

The filter uses a specific bandwidth tuning typically associated with the Ehlers Roofing Filter.

$$ \omega = \frac{0.707 \cdot 2\pi}{L} $$
$$ \alpha = \frac{\cos(\omega) + \sin(\omega) - 1}{\cos(\omega)} $$

### 2. Coefficients

$$ \beta = \alpha / 2 $$
$$ c_1 = (1 - \beta)^2 $$
$$ c_2 = 2(1 - \alpha) $$
$$ c_3 = (1 - \alpha)^2 $$

### 3. Recursive Update

$$ y_t = c_1(x_t - 2x_{t-1} + x_{t-2}) + c_2 y_{t-1} - c_3 y_{t-2} $$

Where:

- $y_t$: Output (highpass component) at time $t$
- $x_t$: Input signal at time $t$
- $L$: Length (cutoff period)

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10/10 | O(1) complexity, efficient IIR structure. |
| **Allocations** | 0 | Zero-allocation in hot path. |
| **Complexity** | O(1) | Constant time per update. |
| **Accuracy** | 9/10 | Precise frequency separation. |
| **Timeliness** | 8/10 | Minimal lag for passed frequencies. |
| **Smoothness** | 8/10 | Output is oscillatory (by design). |

## Parameters

| Name | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `length` | `int` | 40 | Cutoff period. Minimum 2. |

## C# Usage

```csharp
// Initialize with length 40
var hpf = new Hpf(40);

// Update with streaming data
TValue cycle = hpf.Update(new TValue(time, price));

// Static batch calculation
double[] prices = ...;
double[] cycle = new double[prices.Length];
Hpf.Calculate(prices, cycle, 40);
