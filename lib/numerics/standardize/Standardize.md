# STANDARDIZE: Z-Score Normalization

> "How many standard deviations from the mean? That single number tells you whether a value is ordinary or extraordinary. Statistics does not care about your feelings."

Standardize computes the z-score (standard score) of values over a rolling lookback period, using sample standard deviation with Bessel's correction (N-1 denominator). The output is unbounded and dimensionless, expressing how far the current value deviates from the recent mean in units of standard deviation. Values beyond $\pm 2$ are statistically unusual; beyond $\pm 3$ are rare. This is the foundation of anomaly detection and cross-series comparison in quantitative analysis.

## Historical Context

Z-score normalization originates in the work of Carl Friedrich Gauss (1809) and was formalized by Karl Pearson in the 1890s. The standardization transform $z = (x - \mu) / \sigma$ is one of the most widely used operations in statistics, machine learning, and quantitative finance.

In technical analysis, rolling z-score standardization serves multiple purposes:

- **Anomaly detection**: Values with $|z| > 2$ indicate statistically significant deviations from recent behavior
- **Cross-instrument comparison**: Z-scores are dimensionless and scale-invariant, enabling direct comparison of indicators across instruments with different price ranges and volatilities
- **Mean-reversion signals**: Extreme z-scores suggest potential reversion to the mean
- **Feature normalization**: Required preprocessing for machine learning models applied to financial data

This implementation uses direct calculation from the ring buffer rather than Welford's online algorithm, trading theoretical numerical stability for practical simplicity. For typical financial data ranges and period lengths (2-200), direct calculation provides more than sufficient precision.

## Architecture & Physics

### 1. Rolling Window

A ring buffer of size `period` maintains the lookback window:

$$
W_t = [v_{t-n+1}, v_{t-n+2}, \ldots, v_t]
$$

### 2. Mean Calculation

$$
\bar{x} = \frac{1}{n} \sum_{i=1}^{n} x_i
$$

### 3. Sample Standard Deviation

Using Bessel's correction for unbiased estimation:

$$
s = \sqrt{\frac{\sum_{i=1}^{n} (x_i - \bar{x})^2}{n - 1}} = \sqrt{\frac{n}{n-1} \cdot \left(\frac{\sum x_i^2}{n} - \bar{x}^2\right)}
$$

The implementation computes from sum and sum-of-squares for efficiency, clamping tiny negative variance values to zero for numerical stability.

### 4. Z-Score

$$
z = \frac{x - \bar{x}}{s}
$$

When $s = 0$ (constant data), the result is 0.0 (all values equal the mean).

### 5. State Management

The indicator extends `AbstractBase` with `record struct State` containing `LastValidZScore`, `Sum`, `SumSq`, and `ValidCount`. The `_state` / `_p_state` pattern enables bar correction rollback.

## Mathematical Foundation

### Core Formula

$$
z_t = \frac{x_t - \bar{x}_W}{\hat{\sigma}_W}
$$

where:

- $x_t$ = current value
- $\bar{x}_W$ = mean of the rolling window $W$
- $\hat{\sigma}_W$ = sample standard deviation of $W$

### Sample vs Population Standard Deviation

| Type | Formula | Denominator | Use |
|------|---------|-------------|-----|
| Population | $\sigma = \sqrt{\frac{\sum(x-\mu)^2}{N}}$ | $N$ | Known full population |
| Sample | $s = \sqrt{\frac{\sum(x-\bar{x})^2}{N-1}}$ | $N - 1$ | Estimating from sample |

This implementation uses **sample** standard deviation (N-1), which is standard for rolling windows where the data represents a sample of the larger price series.

### Properties of Z-Scores

For normally distributed data:

| Range | Probability |
|-------|------------|
| $|z| \leq 1$ | 68.27% |
| $|z| \leq 2$ | 95.45% |
| $|z| \leq 3$ | 99.73% |

Financial returns are not normally distributed (fat tails), so extreme z-scores occur more frequently than the table suggests.

### Default Parameters

| Parameter | Default | Purpose |
|-----------|---------|---------|
| period | 20 | Rolling lookback window |

### Constraints

- `period >= 2` (minimum for sample standard deviation)

### Warmup

$$
\text{WarmupPeriod} = \text{period}
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD | n | Sum over buffer |
| MUL | n | Sum of squares over buffer |
| DIV | 3 | Mean, variance, z-score |
| SQRT | 1 | Standard deviation |
| SUB | 1 | x - mean |
| **Total** | **~2n + 5 ops** | Linear in period |

### Batch Mode (Span-based)

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(period) | Re-scan buffer for sum/sumsq |
| Total | O(n * period) | Could be optimized to O(n) with running sums |
| Memory | O(period) | Ring buffer |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Direct calculation, Bessel's correction |
| **Timeliness** | 7/10 | Responds within one period to regime changes |
| **Smoothness** | 6/10 | Depends on input volatility |
| **Simplicity** | 8/10 | Well-understood statistical transform |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | Matches within tolerance |
| **TA-Lib** | N/A | No Standardize function |
| **Tulip** | N/A | No Standardize function |
| **Ooples** | ✅ | Matches within tolerance |

## Common Pitfalls

1. **Period must be >= 2**: Sample standard deviation requires at least 2 data points (N-1 denominator). Period of 1 throws `ArgumentException`.

2. **Zero standard deviation**: When all values in the window are identical, stdev is zero. The implementation returns 0.0 (the value equals the mean). Some implementations return NaN here; this one avoids propagating invalids.

3. **Non-normal distributions**: Z-score thresholds ($\pm 2$, $\pm 3$) assume normality. Financial returns have fat tails, so extreme z-scores are more common than Gaussian theory predicts.

4. **Rolling window lag**: The z-score uses a trailing window. After a regime change (e.g., volatility spike), the z-score stabilizes only after the old regime values have aged out of the window.

5. **Unbounded output**: Unlike bounded oscillators (0-100), z-scores can theoretically be any real number. Flash crashes or gap events can produce z-scores of $\pm 10$ or more.

6. **Scale invariance**: Z-scores are dimensionless. A z-score of 2.0 means the same thing whether the input is a stock price, volume, or volatility measure. This enables cross-indicator comparison.

7. **Not a trading signal**: A high z-score means "unusual," not "sell." In trending markets, extreme z-scores can persist as the new regime becomes the norm within the window.

## References

- Gauss, C. F. (1809). "Theoria Motus Corporum Coelestium."
- Pearson, K. (1895). "Contributions to the Mathematical Theory of Evolution." Philosophical Transactions A.
- Mandelbrot, B. (1963). "The Variation of Certain Speculative Prices." Journal of Business.
- Hamilton, J. D. (1994). "Time Series Analysis." Princeton University Press.
