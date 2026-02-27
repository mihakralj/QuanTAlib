# Covariance: Covariance

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `isPopulation` (default false)                      |
| **Outputs**      | Single series (Cov)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- Covariance measures the joint variability of two random variables.
- Parameterized by `period`, `ispopulation` (default false).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Correlation is just covariance normalized by standard deviation. But sometimes you want the raw, unadulterated relationship."

Covariance measures the joint variability of two random variables. It indicates the direction of the linear relationship between variables.

## Architecture & Physics

Covariance is calculated using a sliding window approach. It maintains running sums of $x$, $y$, and $xy$ to allow for $O(1)$ updates.

* **Positive Covariance**: Indicates that the two variables tend to move in the same direction.
* **Negative Covariance**: Indicates that the two variables tend to move in opposite directions.
* **Zero Covariance**: Indicates that the two variables are uncorrelated.

## Mathematical Foundation

### 1. Population Covariance

$$ Cov(X, Y) = \frac{\sum_{i=1}^{n} (x_i - \bar{x})(y_i - \bar{y})}{n} $$

### 2. Sample Covariance

$$ Cov(X, Y) = \frac{\sum_{i=1}^{n} (x_i - \bar{x})(y_i - \bar{y})}{n - 1} $$

### 3. Computational Formula (Running Sums)

$$ Cov(X, Y) = \frac{\sum xy - \frac{(\sum x)(\sum y)}{n}}{n} \quad \text{(or } n-1 \text{)} $$

## Performance Profile

### Operation Count (Streaming Mode)

Covariance uses a dual-input sliding window with running cross-product sums for O(1) update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict (2 inputs) | 2 | 3 cy | ~6 cy |
| Update 3 running sums (Sx, Sy, Sxy) | 3 | 2 cy | ~6 cy |
| Compute covariance formula | 1 | 5 cy | ~5 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~19 cy** |

O(1) per update using online running sums. Periodic resync every 1000 bars prevents floating-point drift accumulation.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | $O(1)$ updates using running sums. |
| **Allocations** | 0 | No heap allocations in hot path. |
| **Complexity** | $O(1)$ | Constant time update regardless of period. |
| **Accuracy** | High | Uses `double` precision; periodic resync prevents drift. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Manual** | ✅ | Verified against manual calculation. |
| **Excel** | ✅ | Matches `COVARIANCE.P` and `COVARIANCE.S`. |

## Usage

```csharp
using QuanTAlib;

// Create a Covariance indicator with period 20 (Sample Covariance by default)
var cov = new Covariance(20);

// Update with new values
cov.Update(price1, price2);

// Access the result
double result = cov.Last.Value;
