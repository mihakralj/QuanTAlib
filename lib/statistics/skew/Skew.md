# SKEW: Skewness

> "In the land of the blind, the one-eyed man is king. In the land of the normal distribution, the skewed man is profitable."

Skewness measures the asymmetry of the probability distribution of a real-valued random variable about its mean. It tells you where the "tail" of the distribution is.

## Historical Context

Introduced by Karl Pearson in 1895, Skewness (along with Kurtosis) provides the "shape" of the distribution beyond the mean (location) and variance (spread). In finance, it's critical because returns are rarely normally distributed; they often exhibit "negative skew" (frequent small gains, occasional catastrophic losses).

## Architecture & Physics

The `Skew` indicator uses a sliding window (RingBuffer) to maintain the last $N$ samples. To ensure O(1) performance, it maintains running sums of the first three powers of the input:

- $\sum x$
- $\sum x^2$
- $\sum x^3$

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
