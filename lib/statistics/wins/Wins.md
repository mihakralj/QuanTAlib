# WINS: Winsorized Mean Moving Average

The Winsorized Mean Moving Average computes a rolling average after replacing (not discarding) the most extreme values in each tail with the boundary values at the trim point. Unlike the trimmed mean (TRIM) which removes outliers entirely, Winsorization preserves the full sample size by clamping extreme values to the nearest non-extreme observation. At `winPct = 0` it degenerates to the SMA; at `winPct = 50` all values equal the median pair. The default 10% Winsorization provides a robust central tendency estimator that dampens outlier impact while maintaining the statistical efficiency advantages of the full sample size.

## Historical Context

Winsorization is named after Charles P. Winsor, a biostatistician at Harvard, though the technique was popularized by John Tukey (1962) who credited Winsor with the idea. The concept arises naturally from the question: "what if instead of throwing away extreme values, we replace them with the most extreme non-discarded value?" This produces an estimator that is more efficient than the trimmed mean under light contamination models while retaining comparable robustness.

The distinction between trimming and Winsorizing is subtle but consequential. Consider a 20-bar window with 10% processing: TRIM discards the 2 lowest and 2 highest values, averaging the remaining 16. WINS replaces the 2 lowest with the 3rd-lowest value and the 2 highest with the 3rd-highest, averaging all 20. Both have the same breakdown point (10%), but WINS has higher asymptotic efficiency because it uses all $n$ observations in the average.

In financial applications, Winsorization is standard practice in factor modeling: Fama-French factor returns are typically Winsorized at 1% or 5% to prevent a handful of extreme observations from dominating cross-sectional regressions. The Winsorized mean is also used in the construction of robust risk measures like the Winsorized variance and the Winsorized covariance matrix.

## Architecture and Physics

The computation has three steps per bar:

**Step 1: Collection** gathers the most recent `period` values into an array, substituting 0 for NaN via `nz()`.

**Step 2: Sort and clamp** arranges values in ascending order, then replaces the lowest `winCount` values with the value at index `winCount` (the lower boundary) and the highest `winCount` values with the value at index `period - 1 - winCount` (the upper boundary):

$$\text{winCount} = \left\lfloor \frac{\text{period} \times \text{winPct}}{100} \right\rfloor$$

The clamping preserves the boundary values themselves; only values beyond them are replaced.

**Step 3: Average** computes the arithmetic mean of all `period` values (including the replaced ones). Since replaced values equal the boundary values, this is equivalent to:

$$\text{WINS} = \frac{\text{winCount} \cdot x_{(k+1)} + \sum_{i=k+1}^{n-k} x_{(i)} + \text{winCount} \cdot x_{(n-k)}}{n}$$

where $k = \text{winCount}$ and $x_{(i)}$ is the $i$-th order statistic.

**Edge case**: If `winCount` would reach or exceed `period / 2`, it is clamped to `(period - 1) / 2`, producing the median pair (two middle values) replicated across all positions.

## Mathematical Foundation

The **Winsorized mean** for a sample of size $n$ with $k$ replacements per tail:

$$\bar{x}_W = \frac{1}{n}\left[k \cdot x_{(k+1)} + \sum_{i=k+1}^{n-k} x_{(i)} + k \cdot x_{(n-k)}\right]$$

where $x_{(i)}$ is the $i$-th order statistic and $k = \lfloor \alpha n \rfloor$ with $\alpha = \text{winPct}/100$.

**Winsorized variance** (used for inference on the Winsorized mean):

$$s_W^2 = \frac{1}{n-1} \sum_{i=1}^{n} (w_i - \bar{x}_W)^2$$

where $w_i$ are the Winsorized values.

**Influence function**: Bounded like TRIM, but the boundary behavior differs:

$$\text{IF}(x; \bar{x}_W) = \begin{cases} x_{(\alpha)} - \bar{x}_W & \text{if } x \le x_{(\alpha)} \\ x - \bar{x}_W & \text{if } x_{(\alpha)} < x < x_{(1-\alpha)} \\ x_{(1-\alpha)} - \bar{x}_W & \text{if } x \ge x_{(1-\alpha)} \end{cases}$$

**Breakdown point**: $\alpha$ (the Winsorization fraction).

**Asymptotic efficiency** relative to SMA under normality (higher than TRIM at same percentage):

| Win % | WINS Efficiency | TRIM Efficiency |
|-------|----------------|-----------------|
| 0% | 100% | 100% |
| 10% | ~97% | ~95% |
| 25% | ~90% | ~85% |

**Parameter constraints**: `period` $\ge 3$, `winPct` $\in [0, 49]$.

```
WINS(source, period, winPct):
    winCount = floor(period * winPct / 100)
    if winCount >= period/2: winCount = (period-1)/2

    // Collect and sort
    vals = [source[0], source[1], ..., source[period-1]]
    sort(vals, ascending)

    // Replace tails with boundary values
    lowerBound = vals[winCount]
    upperBound = vals[period - 1 - winCount]
    for i = 0 to winCount-1:
        vals[i] = lowerBound
        vals[period - 1 - i] = upperBound

    // Average all values (full sample size)
    return mean(vals)
```

## Resources

- Tukey, J.W. "The Future of Data Analysis." Annals of Mathematical Statistics, 1962.
- Huber, P.J. & Ronchetti, E. "Robust Statistics." 2nd edition, Wiley, 2009.
- Wilcox, R.R. "Introduction to Robust Estimation and Hypothesis Testing." 4th edition, Academic Press, 2017.
- Fama, E.F. & French, K.R. "Common Risk Factors in the Returns on Stocks and Bonds." Journal of Financial Economics, 1993.
- Dixon, W.J. & Tukey, J.W. "Approximate Behavior of the Distribution of Winsorized t." Technometrics, 1968.
