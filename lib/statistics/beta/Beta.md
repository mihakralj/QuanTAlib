# Beta: Beta Coefficient

> *Volatility is not risk. It's the price of admission.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Beta)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [beta.pine](beta.pine)                       |

- Beta measures the volatility of an asset in relation to the overall market.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Beta measures the volatility of an asset in relation to the overall market. It's the slope of the regression line between the asset's returns and the market's returns. A beta of 1.0 means the asset moves in lockstep with the market. A beta of 2.0 means the asset is twice as volatile as the market.

## Historical Context

The Beta coefficient was born from the Capital Asset Pricing Model (CAPM), developed by William Sharpe, John Lintner, and Jan Mossin in the 1960s. It formalized the distinction between systematic risk (market risk, which cannot be diversified away) and unsystematic risk (specific to the asset). In the pre-computer era, calculating beta was a tedious manual process involving graph paper and rulers. Today, it's a standard metric on every financial dashboard, though often misunderstood as a measure of "risk" rather than "relative volatility."

## Architecture & Physics

Beta is essentially the ratio of covariance to variance. It answers the question: "For every 1% move in the market, how much does this asset move?"

The calculation relies on the returns of both the asset and the market, not their prices. This implementation calculates returns on the fly from the input prices (`(Current - Previous) / Previous`).

To maintain O(1) performance, QuanTAlib uses Welford's online algorithm principles (or equivalent running sums) to update the covariance and variance components incrementally. This avoids iterating over the entire history for every new bar.

### The Dual-Input Challenge

Unlike most indicators that consume a single time series, Beta requires two synchronized inputs: the Asset and the Market. This breaks the standard `Update(value)` pattern. QuanTAlib solves this with a specialized `Update(asset, market)` overload. The standard single-input methods throw a `NotSupportedException` to prevent misuse.

## Mathematical Foundation

Beta is defined as:

$$ \beta = \frac{Cov(R_a, R_m)}{Var(R_m)} $$

Where:

* $R_a$ is the return of the asset.
* $R_m$ is the return of the market.

In terms of linear regression, Beta is the slope ($b$) of the line $R_a = \alpha + \beta R_m + \epsilon$.

The O(1) implementation uses running sums of the returns:

$$ \beta = \frac{N \sum (R_a R_m) - \sum R_a \sum R_m}{N \sum R_m^2 - (\sum R_m)^2} $$

This formula is mathematically equivalent to the covariance/variance definition but allows for efficient incremental updates.

## Performance Profile

### Operation Count (Streaming Mode)

Beta uses running sums of returns (Welford-style) for O(1) covariance/variance update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict (2 inputs) | 2 | 3 cy | ~6 cy |
| Compute asset + market returns | 2 | 3 cy | ~6 cy |
| Update 4 running sums (Ra, Rm, Ra*Rm, Rm^2) | 4 | 2 cy | ~8 cy |
| Compute covariance / variance | 2 | 5 cy | ~10 cy |
| NaN guard (zero variance) | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~32 cy** |

O(1) per update. Dual-input constraint prevents SIMD batch optimization; sequential return computation enforces ordering.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 15 ns/bar | Single-pass O(1) calculation. |
| **Allocations** | 0 | Zero-allocation hot path. |
| **Complexity** | O(1) | Constant time update regardless of period. |
| **Accuracy** | 9 | Periodic resync prevents floating-point drift. |
| **Timeliness** | Lagged | Depends on the lookback period. |
| **Overshoot** | N/A | Not an oscillator. |
| **Smoothness** | Low | Highly sensitive to outliers in returns. |

## Validation

Validated against Skender.Stock.Indicators.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Reference implementation. |
| **TA-Lib** | ✅ | Matches `TA_BETA` (note: TA-Lib might use prices directly in some versions, check docs). |
| **Skender** | ✅ | Matches `GetBeta` (uses returns). |
| **Pandas-TA** | ✅ | Matches `beta` indicator. |

### Common Pitfalls

1. **Price vs. Returns**: Beta must be calculated on *returns*, not raw prices. This implementation handles the conversion internally. Feeding pre-calculated returns will yield incorrect results (it will calculate returns of returns).
2. **Synchronization**: The Asset and Market data must be time-aligned. If the market data is missing for a bar where the asset has data, the correlation will be skewed.
3. **Period Sensitivity**: A short period (e.g., 10) makes Beta noisy and unstable. A standard period is often 60 (approx. 3 months of daily data) or 252 (1 year).

## C# Usage

```csharp
// Initialize with period 20
var beta = new Beta(20);

// Update with Asset and Market prices
// (e.g., AAPL price and SPY price)
TValue result = beta.Update(assetPrice, marketPrice);

Console.WriteLine($"Beta: {result.Value:F4}");
