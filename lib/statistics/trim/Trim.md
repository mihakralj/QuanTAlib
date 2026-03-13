# TRIM: Trimmed Mean Moving Average

> *Trimmed mean drops the extreme tails before averaging — robust estimation that refuses to let outliers hijack the center.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `trimPct` (default 10.0)                      |
| **Outputs**      | Single series (Trim)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [trim.pine](trim.pine)                       |

- The Trimmed Mean Moving Average computes a rolling average after discarding a configurable percentage of the most extreme values from each tail of ...
- **Similar:** [Wins](../wins/Wins.md), [Median](../median/Median.md) | **Trading note:** Trimmed mean; excludes extreme percentiles. Robust average for volatile data.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Trimmed Mean Moving Average computes a rolling average after discarding a configurable percentage of the most extreme values from each tail of the sorted lookback window. By removing the lowest and highest `trimPct%` of observations, TRIM eliminates the influence of outliers while retaining more information than a pure median. At `trimPct = 0` it degenerates to the SMA; at `trimPct = 50` it becomes the median. The default 10% trim provides a robust central tendency estimator that resists spike contamination with minimal loss of responsiveness, requiring $O(N \log N)$ for the sort plus $O(N)$ for the summation per bar.

## Historical Context

The trimmed mean was introduced by W.J. Dixon (1960) as part of a systematic study of robust estimators for location. John Tukey (1960, 1977) championed its use as part of his program of exploratory data analysis, arguing that no single estimator dominates all contamination models, and the trimmed mean provides a practical compromise between efficiency under normality (where the SMA is optimal) and resistance to outliers (where the median excels).

In financial applications, the trimmed mean addresses a pervasive problem: price series contain erroneous ticks, flash crashes, and gap events that can corrupt moving average calculations. A single outlier in a 20-bar SMA shifts the average by $1/20 = 5\%$ of the outlier magnitude. The trimmed mean bounds this influence: with a 10% trim on 20 bars, the 2 lowest and 2 highest values are discarded, and the remaining 16 values contribute equally. If an outlier falls in a discarded tail, it has zero effect.

The trimmed mean also appears in economic statistics: the Federal Reserve Bank of Cleveland publishes a 16% trimmed-mean CPI as an alternative inflation measure that filters out volatile food and energy prices.

## Architecture and Physics

The computation has three steps per bar:

**Step 1: Collection** gathers the most recent `period` values into an array, substituting 0 for NaN via `nz()`.

**Step 2: Sort** arranges the values in ascending order using Pine's built-in `array.sort()`. This is $O(N \log N)$ and dominates the per-bar cost.

**Step 3: Trimmed average** computes the arithmetic mean of the middle `keepCount` values:

$$\text{trimCount} = \left\lfloor \frac{\text{period} \times \text{trimPct}}{100} \right\rfloor$$

$$\text{keepCount} = \text{period} - 2 \times \text{trimCount}$$

$$\text{TRIM} = \frac{1}{\text{keepCount}} \sum_{i=\text{trimCount}}^{\text{trimCount} + \text{keepCount} - 1} x_{(i)}$$

where $x_{(i)}$ denotes the $i$-th order statistic.

**Edge case**: If `keepCount` would fall below 1 (extreme trim percentage with small period), the implementation clamps it to 1 and adjusts `trimCount` accordingly, effectively returning the median.

**Comparison with WINS**: TRIM discards extreme values entirely, reducing the effective sample size. WINS (Winsorized mean) replaces extremes with boundary values, preserving the full sample size. TRIM has a higher breakdown point for the same percentage, but WINS is more efficient when outliers are moderate rather than extreme.

## Mathematical Foundation

The **$\alpha$-trimmed mean** for a sample of size $n$:

$$\bar{x}_\alpha = \frac{1}{n - 2k} \sum_{i=k+1}^{n-k} x_{(i)}$$

where $k = \lfloor \alpha \cdot n \rfloor$ and $\alpha = \text{trimPct}/100$.

**Influence function**: The trimmed mean has a bounded influence function that equals zero outside the trimmed range:

$$\text{IF}(x; \bar{x}_\alpha) = \begin{cases} 0 & \text{if } x < x_{(\alpha)} \text{ or } x > x_{(1-\alpha)} \\ \frac{x - \bar{x}_\alpha}{1 - 2\alpha} & \text{otherwise} \end{cases}$$

**Breakdown point**: $\alpha$ (the trim fraction). With 10% trim, up to 10% of the data can be arbitrarily corrupted without affecting the estimator.

**Asymptotic efficiency** relative to SMA under normality:

| Trim % | Efficiency |
|--------|-----------|
| 0% | 100% (SMA) |
| 5% | ~98% |
| 10% | ~95% |
| 25% | ~85% |
| 50% | ~64% (median) |

**Parameter constraints**: `period` $\ge 3$, `trimPct` $\in [0, 49]$.

```
TRIM(source, period, trimPct):
    trimCount = floor(period * trimPct / 100)
    keepCount = period - 2 * trimCount
    if keepCount < 1: keepCount = 1

    // Collect and sort
    vals = [source[0], source[1], ..., source[period-1]]
    sort(vals, ascending)

    // Average middle portion
    sum = 0
    for i = trimCount to trimCount + keepCount - 1:
        sum += vals[i]
    return sum / keepCount
```


## Performance Profile

### Operation Count (Streaming Mode)

Trim collects the window, sorts it, discards the tail values, then averages the inner values.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer collect | N | 1 cy | ~N cy |
| Array sort (introsort) | N log N | 2 cy | ~2N log N cy |
| Sum inner values (N - 2k) | N - 2k | 2 cy | ~2(N-2k) cy |
| Divide for mean | 1 | 4 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=20, k=2)** | **O(N log N)** | — | **~220 cy** |

O(N log N) per update due to sort. For small periods (N ≤ 64), the sort cost is dominated by cache effects; practical throughput is ~15 ns/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Window collection | Yes | Gather from ring buffer with SIMD copy |
| Sort | No | Comparison sort is sequential |
| Inner-range sum | Yes | Contiguous range sum with Vector<double> |

No whole-path SIMD; sort blocks vectorization. Batch is a loop of independent sorts — no cross-bar dependency, so outer loop parallelizable with PLINQ for large datasets.

## Resources

- Dixon, W.J. "Simplified Estimation from Censored Normal Samples." Annals of Mathematical Statistics, 1960.
- Tukey, J.W. "Exploratory Data Analysis." Addison-Wesley, 1977.
- Huber, P.J. & Ronchetti, E. "Robust Statistics." 2nd edition, Wiley, 2009.
- Wilcox, R.R. "Fundamentals of Modern Statistical Methods." 2nd edition, Springer, 2010.
- Bryan, M. & Cecchetti, S. "Measuring Core Inflation." In Monetary Policy, NBER, 1994.