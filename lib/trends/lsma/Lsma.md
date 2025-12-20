# LSMA: Least Squares Moving Average

> "If you want to know where the price is going, draw a line through where it's been. LSMA does this for every single bar, tirelessly fitting linear regressions while you sleep."

LSMA (Least Squares Moving Average), also known as the Moving Linear Regression or Endpoint Moving Average, calculates the least squares regression line for the preceding time periods. In plain English: it finds the "best fit" line for the data window and tells you where that line ends.

## Historical Context

Linear regression is as old as Gauss (c. 1809). Applying it as a moving window to financial time series is a more recent development, popularized by traders who realized that a moving average is just a poor man's regression line (specifically, an SMA is a regression line with a slope of 0). LSMA captures both the level and the trend (slope) of the data.

## Architecture & Physics

LSMA is computationally heavier than an SMA because it minimizes the sum of squared errors for a line equation $y = mx + b$.

- **Slope ($m$)**: Represents the trend strength/direction.
- **Intercept ($b$)**: Represents the value at the start of the window.
- **Endpoint**: The value at the current bar ($y = m \times 0 + b$ in our coordinate system where current bar is 0).

### Zero-Allocation Design

We use a highly optimized O(1) update algorithm.

- **Running Sums**: We maintain running sums of $y$ (price) and $xy$ (price $\times$ time).
- **Incremental Updates**: Instead of recalculating the regression from scratch (which is O(N)), we update the sums by removing the exiting point and adding the entering point.
- **Resync**: To prevent floating-point drift, we perform a full recalculation every 1000 ticks.

## Mathematical Foundation

The regression line is $y = mx + b$.

$$ m = \frac{N \sum xy - \sum x \sum y}{N \sum x^2 - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{N} $$

$$ \text{LSMA} = b - m \times \text{Offset} $$

(Note: In our implementation, $x$ ranges from $N-1$ (oldest) to $0$ (newest) to simplify the math).

## Performance Profile

Despite the complex math, our O(1) implementation makes it fly.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | O(1) updates |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 8/10 | Mathematically precise regression endpoint |
| **Timeliness** | 8/10 | Projects trend, reducing lag |
| **Overshoot** | 2/10 | Significant overshoot on trend reversals |
| **Smoothness** | 3/10 | Sensitive to outliers and noise |

## Validation

Validated against standard statistical libraries and TradingView's LSMA.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **TradingView** | $10^{-9}$ | Matches `linreg` function |
| **Excel** | $10^{-9}$ | Matches `FORECAST` / `TREND` |

### Common Pitfalls

1. **Overshoot**: Because it projects a trend, LSMA will overshoot significantly when the trend reverses. It assumes the trend continues.
2. **Offset**: You can use a positive offset to extrapolate into the future (forecasting), or a negative offset to center the average.
3. **Noise**: It is very sensitive to outliers because it tries to fit a line to them.
