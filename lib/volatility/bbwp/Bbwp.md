# BBWP: Bollinger Band Width Percentile

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `multiplier` (default 2.0), `lookback` (default 252)                      |
| **Outputs**      | Single series (Bbwp)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period + lookback` bars                          |
| **PineScript**   | [bbwp.pine](bbwp.pine)                       |

- BBWP (Bollinger Band Width Percentile) measures where the current Bollinger Band Width falls within its historical distribution, expressing the res...
- Parameterized by `period`, `multiplier` (default 2.0), `lookback` (default 252).
- Output range: $\geq 0$.
- Requires `period + lookback` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Where does current volatility rank in the historical distribution? BBWP answers with a percentile."

BBWP (Bollinger Band Width Percentile) measures where the current Bollinger Band Width falls within its historical distribution, expressing the result as a percentile rank between 0 and 1. Unlike BBWN which normalizes using min/max values, BBWP uses percentile ranking which is more robust to outliers.

## Historical Context

BBWP evolved from the need for a more statistically robust volatility indicator than simple min/max normalization. While BBWN can be heavily influenced by a single extreme BBW value in the lookback period, BBWP counts how many historical values fall below the current reading, providing a true percentile rank that is less sensitive to outliers.

The percentile approach aligns with standard statistical practice for comparing a value to a distribution, making BBWP particularly useful for:
- Identifying volatility regime changes
- Setting dynamic stop-loss levels based on historical volatility context
- Generating signals when volatility reaches extreme percentiles (e.g., below 10th or above 90th percentile)

## Architecture & Physics

### 1. BBW Calculation (inherited from BBW)

$$
BBW_t = 2 \cdot k \cdot \sigma_t
$$

where:
- $k$ = standard deviation multiplier (default 2.0)
- $\sigma_t$ = population standard deviation over period $n$

### 2. Percentile Ranking

$$
BBWP_t = \frac{\text{count}(BBW_i < BBW_t)}{N}
$$

where:
- $BBW_i$ = historical BBW values in the lookback window
- $N$ = total count of BBW values in lookback
- The count includes only values strictly less than $BBW_t$

### 3. Edge Cases

When insufficient history exists ($N < 2$), BBWP returns 0.5 (median) as a neutral default.

## Mathematical Foundation

### Standard Deviation (Population)

$$
\sigma = \sqrt{\frac{1}{n}\sum_{i=1}^{n}(x_i - \bar{x})^2}
$$

Using Welford's running algorithm:
$$
\sigma = \sqrt{\frac{\sum x^2}{n} - \left(\frac{\sum x}{n}\right)^2}
$$

### Percentile Rank Formula

For a value $v$ in a dataset of $N$ values:
$$
\text{Percentile} = \frac{\text{count of values} < v}{N}
$$

This is the "exclusive" percentile definition (values strictly less than $v$).

## Performance Profile

### Operation Count (Streaming Mode, per bar)

| Operation | Count | Notes |
|:---|:---:|:---|
| ADD/SUB | 4 | Running sum/sumSq update |
| MUL | 2 | Square calculations |
| DIV | 3 | Mean, variance, percentile |
| SQRT | 1 | Standard deviation |
| CMP | L | Lookback comparisons for percentile |
| **Total** | **~L+10** | Dominated by lookback size |

where L = lookback period (default 252)

### Quality Metrics

| Metric | Score | Notes |
|:---|:---:|:---|
| **Accuracy** | 10/10 | Exact percentile calculation |
| **Robustness** | 9/10 | More outlier-resistant than BBWN |
| **Timeliness** | 8/10 | Reflects current position in distribution |
| **Interpretability** | 10/10 | True statistical percentile |

## Validation

| Library | Status | Notes |
|:---|:---:|:---|
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Internal** | ✅ | Validated against PineScript reference |

## Common Pitfalls

1. **Interpretation difference from BBWN**: BBWP of 0.80 means 80% of historical BBW values were lower, not that BBW is at 80% of its range. These can differ significantly when the distribution is skewed.

2. **Lookback period impact**: Shorter lookbacks (e.g., 50) respond faster but may miss longer-term volatility regimes. Standard practice uses 252 (trading days in a year) for daily data.

3. **Warmup period**: Requires period + lookback bars for statistically meaningful percentiles. Early values default to 0.5.

4. **Zero volatility**: When all prices are identical, BBW=0 and the percentile of 0 among all 0s is 0 (nothing is below 0).

5. **Computational cost**: The percentile calculation requires O(L) comparisons per bar, which can be noticeable for very large lookback values.

6. **Distribution assumptions**: BBWP makes no assumptions about the underlying distribution of BBW values, which is both a strength (non-parametric) and a consideration (may not capture extreme tail behavior well).

## References

- Bollinger, J. (2001). "Bollinger on Bollinger Bands." McGraw-Hill.
- QuanTAlib PineScript reference implementation (bbwp.pine)
