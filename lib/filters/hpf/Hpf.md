# HPF: Ehlers Highpass Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `length` (default 40)                      |
| **Outputs**      | Single series (HPF)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | 1 bar                          |
| **Signature**    | [hpf_signature](hpf_signature.md) |

### TL;DR

- The 2-Pole Highpass Filter (HPF) is designed to separate high-frequency components (like cycles and noise) from the underlying trend.
- Parameterized by `length` (default 40).
- Output range: Oscillates around zero (detrended signal).
- Requires `length` bars of warmup before first valid output (IsHot = true). Default: **40 bars**.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

### Operation Count (Streaming Mode)

High-Pass Filter (HPF): 2nd-order IIR; output = input minus the low-pass component. Detrending architecture requires only one IIR recursion.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| LP IIR update (2 FMA) | 2 | ~4 cy | ~8 cy |
| HP output = input - LP | 1 | ~2 cy | ~2 cy |
| State update | 2 | ~1 cy | ~2 cy |
| **Total** | **5** | — | **~12 cycles** |

O(1) per bar. Subtract-from-LP architecture means only one IIR recursion needed. ~12 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| LP recursion | No | Sequential IIR |
| Subtraction | Yes | Element-wise; trivial vectorization |

Batch throughput: ~12 cy/bar.

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
