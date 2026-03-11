# SimdExtensions Class

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (SimdExtensions)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |

- `SimdExtensions` provides high-performance, SIMD-accelerated extension methods for `ReadOnlySpan<double>`.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

`SimdExtensions` provides high-performance, SIMD-accelerated extension methods for `ReadOnlySpan<double>`. It leverages .NET's `Vector<T>` to achieve 4-8x speedups on supported hardware (AVX2, AVX-512) while automatically falling back to scalar implementations on older hardware.

## Key Features

* **Hardware Acceleration**: Uses CPU vector registers to process multiple elements in parallel.
* **Automatic Fallback**: Gracefully handles non-SIMD hardware or small arrays.
* **Zero-Allocation**: Operates directly on spans without creating new arrays.
* **Aggressive Inlining**: Methods are marked for inlining to minimize call overhead.

## Available Methods

| Method | Description |
| ------ | ------ |
| `ContainsNonFinite()` | Checks if span contains any non-finite values (NaN or Infinity). |
| `SumSIMD()` | Calculates the sum of elements. |
| `MinSIMD()` | Finds the minimum value. |
| `MaxSIMD()` | Finds the maximum value. |
| `MinMaxSIMD()` | Finds both min and max in a single pass (more efficient than separate calls). |
| `AverageSIMD()` | Calculates the arithmetic mean. |
| `VarianceSIMD()` | Calculates the sample variance. |
| `StdDevSIMD()` | Calculates the sample standard deviation. |
| `DotProduct()` | Calculates the dot product of two spans. |

## Performance

On modern CPUs (e.g., Intel Core i7/i9, AMD Ryzen), these methods typically outperform standard LINQ or scalar loops by a factor of 4 to 8 for large arrays.

## Usage

```csharp
using QuanTAlib;

double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0, ... };
ReadOnlySpan<double> span = data;

// Calculate sum
double sum = span.SumSIMD();

// Calculate min and max in one pass
var (min, max) = span.MinMaxSIMD();

// Calculate standard deviation
double stdDev = span.StdDevSIMD();

// Check for valid data
bool hasInvalid = span.ContainsNonFinite();

// Calculate dot product
double dot = span.DotProduct(otherSpan);
```
