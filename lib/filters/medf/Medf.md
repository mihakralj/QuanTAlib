# MEDF: Moving Median Filter

> "The median is the only filter that can remove a spike without flinching. SMA smears it, EMA decays it over time, but the median simply ignores it. For impulse noise in financial data — bad ticks, flash crashes, fat-finger errors — the median is the correct tool."

MEDF outputs the median of the most recent $N$ values in a sliding window, providing a nonlinear filter that is robust to impulse noise and outliers while preserving edges and steps better than any linear filter. Unlike SMA or EMA, which spread the effect of a single outlier across the entire window (SMA) or decay it exponentially (EMA), the median completely rejects outliers that do not constitute a majority of the window. This makes MEDF the filter of choice for cleaning price data contaminated with bad ticks or anomalous prints.

## Historical Context

The running median was introduced by John Tukey in *Exploratory Data Analysis* (1977) as a fundamental tool for resistant smoothing. Tukey recognized that the arithmetic mean (and by extension, linear filters like SMA and EMA) is highly sensitive to outliers: a single extreme value can shift the mean arbitrarily far from the "typical" value. The median, being the 50th percentile, requires more than $N/2$ values to be corrupted before it fails.

In signal processing, median filters gained prominence in image processing (Huang, Yang, and Tang, 1979), where they excel at removing "salt and pepper" noise while preserving sharp edges. The same property applies to financial time series: price levels often exhibit step-like behavior (e.g., after a gap or news event), and the median preserves these steps while linear filters blur them.

The computational cost of a naive median filter is $O(N \log N)$ per bar (sort the window, extract the middle). More efficient algorithms exist: the rolling median via two heaps achieves $O(\log N)$ per bar, and Huang's histogram method achieves $O(1)$ amortized for integer-valued data. The Pine implementation uses a sort-based approach.

## Architecture & Physics

### 1. Circular Buffer

A ring buffer of size $N$ stores the most recent $N$ values.

### 2. Window Extraction and Sort

Each bar, the buffer contents are copied to a temporary array and sorted. This is $O(N \log N)$ via array sort.

### 3. Median Extraction

For odd $N$: the middle element is the median. For even $N$: the average of the two middle elements.

## Mathematical Foundation

The median of a set $\{x_1, x_2, \ldots, x_N\}$ is:

$$
\text{median}(X) = \begin{cases} X_{[(N+1)/2]} & N \text{ odd} \\ \frac{X_{[N/2]} + X_{[N/2+1]}}{2} & N \text{ even} \end{cases}
$$

where $X_{[k]}$ denotes the $k$-th order statistic (sorted value).

**Key properties:**

| Property | Median | SMA | EMA |
| :--- | :---: | :---: | :---: |
| Outlier rejection | Complete (if $< N/2$ outliers) | None | Partial (decays) |
| Edge preservation | Yes | Blurs edges | Blurs edges |
| Linearity | Nonlinear | Linear | Linear |
| Frequency response | No closed form | Sinc | Exponential decay |
| Idempotent | No | No | No |

**Breakdown point:** The median has a 50% breakdown point, meaning up to $\lfloor N/2 \rfloor$ values can be arbitrarily corrupted without affecting the output (assuming the remaining values are within the signal range). This is the highest possible breakdown point for any estimator.

**Default parameters:** `period = 5`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
buffer[head] = src
head = (head + 1) % period
count = min(count + 1, period)

sorted = sort(buffer[0..count-1])
if count is odd:
    return sorted[count / 2]
else:
    return (sorted[count/2 - 1] + sorted[count/2]) / 2
```

## Resources

- Tukey, J.W. (1977). *Exploratory Data Analysis*. Addison-Wesley. Chapter 7: Resistant Smoothing.
- Huang, T.S., Yang, G.J., & Tang, G.Y. (1979). "A Fast Two-Dimensional Median Filtering Algorithm." *IEEE Trans. Acoust., Speech, Signal Process.*, 27(1), 13-18.
- Yin, L. et al. (1996). "Weighted Median Filters: A Tutorial." *IEEE Trans. Circuits and Systems II*, 43(3), 157-192.
