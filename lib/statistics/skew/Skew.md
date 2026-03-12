# SKEW: Skewness

> *In the land of the blind, the one-eyed man is king. In the land of the normal distribution, the skewed man is profitable.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `isPopulation` (default false)                      |
| **Outputs**      | Single series (Skew)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [skew.pine](skew.pine)                       |

- Skewness measures the asymmetry of the probability distribution of a real-valued random variable about its mean.
- Parameterized by `period`, `ispopulation` (default false).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Skewness measures the asymmetry of the probability distribution of a real-valued random variable about its mean. It tells you where the "tail" of the distribution is.

## Historical Context

Introduced by Karl Pearson in 1895, Skewness (along with Kurtosis) provides the "shape" of the distribution beyond the mean (location) and variance (spread). In finance, it's critical because returns are rarely normally distributed; they often exhibit "negative skew" (frequent small gains, occasional catastrophic losses).

## Architecture & Physics

The `Skew` indicator uses a sliding window (RingBuffer) to maintain the last $N$ samples. To ensure O(1) performance, it maintains running sums of the first three powers of the input:

* $\sum x$
* $\sum x^2$
* $\sum x^3$

This allows calculating the 2nd and 3rd central moments instantly without re-iterating the buffer.

### Stability

Calculating higher moments (like $x^3$) can lead to precision issues with large numbers. The implementation uses `double` precision and a periodic `Resync()` (every 1000 ticks) to correct any floating-point drift.

## Mathematical Foundation

We use the **Fisher-Pearson Coefficient of Skewness** (Sample Skewness), which is the standard in statistical software (like Excel's `SKEW`, Python's `scipy.stats.skew(bias=False)`).

### 1. Moments

First, we calculate the raw moments from the running sums:
$$ \text{Mean} (\bar{x}) = \frac{\sum x}{n} $$
$$ \text{Variance} (m_2) = \frac{\sum x^2 - \frac{(\sum x)^2}{n}}{n} $$
$$ \text{3rd Moment} (m_3) = \frac{\sum x^3 - 3\bar{x}\sum x^2 + 2n\bar{x}^3}{n} $$

### 2. Population Skewness ($g_1$)

$$ g_1 = \frac{m_3}{m_2^{3/2}} $$

### 3. Sample Skewness ($G_1$)

For sample skewness (unbiased estimator), we apply a correction factor:
$$ G_1 = \frac{\sqrt{n(n-1)}}{n-2} \cdot g_1 $$

## Performance Profile

### Operation Count (Streaming Mode)

Skewness uses running sums of powers 1–3 over the sliding window for O(1) update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Update 3 power sums (x, x^2, x^3) | 3 | 3 cy | ~9 cy |
| Compute skewness formula | 1 | 8 cy | ~8 cy |
| NaN guard + N >= 3 guard | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~22 cy** |

O(1) per update using 3rd-moment running sums. The sample skewness correction factor N/((N-1)(N-2)) is precomputed in the constructor.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 8 ns/bar | O(1) update using running sums. |
| **Allocations** | 0 | Zero-allocation hot path. |
| **Complexity** | O(1) | Independent of period length. |
| **Accuracy** | 9/10 | Periodic resync handles drift. |

## Validation

Validated against Python's `scipy.stats.skew`.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Scipy** | ✅ | Matches `skew(..., bias=False)`. |
| **Excel** | ✅ | Matches `SKEW()`. |

## Usage

```csharp
using QuanTAlib;

// Create a 14-period Skewness indicator
var skew = new Skew(14);

// Update with new value
var result = skew.Update(new TValue(DateTime.UtcNow, 105.5));

// Result > 0: Positive skew (tail on right)
// Result < 0: Negative skew (tail on left)
// Result = 0: Symmetric
Console.WriteLine($"Skewness: {result.Value:F4}");
