# RSV: Rogers-Satchell Volatility

> "The best estimator is one that extracts maximum information from all available data while remaining robust to the noise of market microstructure."

Rogers-Satchell Volatility (RSV) is a drift-adjusted OHLC-based volatility estimator that uses all four price points (Open, High, Low, Close) to provide more accurate volatility estimates than simpler range-based methods. Developed by L.C.G. Rogers and S.E. Satchell in 1991, this estimator is unique in its ability to account for price drift, making it particularly suitable for trending markets. The implementation uses SMA smoothing and optional annualization.

## Historical Context

Rogers and Satchell introduced this estimator in their 1991 paper "Estimating Variance from High, Low and Closing Prices" published in the Annals of Applied Probability. The key innovation was recognizing that the Parkinson and Garman-Klass estimators assume zero drift (no trend), which can introduce bias during trending markets.

The Rogers-Satchell estimator was designed to be independent of the drift rate, meaning it provides unbiased variance estimates regardless of whether the underlying asset is trending up, down, or sideways. This makes it theoretically superior for real-world markets where trends are common.

The estimator achieves approximately 8.4x the efficiency of close-to-close methods, making it one of the more efficient OHLC-based estimators available. It sits between Garman-Klass (7.4x) and Yang-Zhang (14x) in the efficiency hierarchy.

Unlike Parkinson (HLV) which uses only High-Low, or some implementations that focus on the close-to-close return, RSV extracts information from all four price relationships simultaneously: H/O, H/C, L/O, and L/C.

## Architecture & Physics

### 1. Log Price Ratios

All calculations use log ratios to normalize percentage returns:

$$
\ln\frac{H_t}{O_t}, \ln\frac{H_t}{C_t}, \ln\frac{L_t}{O_t}, \ln\frac{L_t}{C_t}
$$

where:

- $O_t, H_t, L_t, C_t$ = Open, High, Low, Close prices at time $t$

Log transformation ensures that equal percentage moves have equal magnitude regardless of price level.

### 2. Rogers-Satchell Variance

The single-period Rogers-Satchell variance estimator:

$$
\hat{\sigma}^2_{RS,t} = \ln\frac{H_t}{O_t} \cdot \ln\frac{H_t}{C_t} + \ln\frac{L_t}{O_t} \cdot \ln\frac{L_t}{C_t}
$$

This formula has a beautiful symmetry: both terms are products of log ratios involving the high or low price against both open and close.

**Key Property:** For valid OHLC data, this variance estimate is always non-negative:

- The first term: $\ln(H/O) \geq 0$ and $\ln(H/C) \geq 0$ (since $H \geq O$ and $H \geq C$)
- The second term: $\ln(L/O) \leq 0$ and $\ln(L/C) \leq 0$ (since $L \leq O$ and $L \leq C$)
- Both products are non-negative, so the sum is non-negative

### 3. SMA Smoothing

Unlike HLV and GKV which use RMA (Wilder's) smoothing, RSV uses a Simple Moving Average with a circular buffer:

$$
SMA_t = \frac{1}{n} \sum_{i=t-n+1}^{t} \hat{\sigma}^2_{RS,i}
$$

where $n$ = period (default 20).

SMA provides:

- Equal weighting of all observations in the window
- Complete adaptation after exactly $n$ bars
- No exponential decay bias requiring correction

### 4. Volatility Calculation

Convert smoothed variance to volatility (standard deviation):

$$
\sigma_t = \sqrt{\max(0, SMA_t)}
$$

The max(0, ...) guard protects against potential floating-point artifacts.

### 5. Optional Annualization

If annualization is enabled (default):

$$
\sigma_{annual,t} = \sigma_t \times \sqrt{N}
$$

where $N$ = annual periods (default 252 trading days).

## Mathematical Foundation

### Drift Independence

The Rogers-Satchell estimator's key mathematical property is its independence from drift. For a geometric Brownian motion:

$$
dS = \mu S dt + \sigma S dW
$$

The standard close-to-close variance estimator:

$$
\hat{\sigma}^2_{CC} = (\ln C_t - \ln C_{t-1})^2
$$

includes the drift term $\mu dt$, biasing the estimate.

The Rogers-Satchell formula, through its specific combination of high-low-open-close ratios, cancels out the drift component mathematically, yielding an unbiased estimate of $\sigma^2$ regardless of $\mu$.

### Why This Formula Works

Consider the log price path within a single bar:

- The high and low represent extrema of the Brownian path
- The open-to-high and close-to-high paths share the high point
- The open-to-low and close-to-low paths share the low point

The product structure $\ln(H/O) \cdot \ln(H/C)$ captures the "spread" between how far the high was from both boundaries (open and close). Similarly for the low. This geometric information is invariant to drift.

### Comparison of Variance Formulas

| Estimator | Formula | Drift Adjustment |
| :--- | :--- | :---: |
| Close-to-Close | $(\ln C/C_{prev})^2$ | None |
| Parkinson | $\frac{1}{4\ln 2}(\ln H/L)^2$ | None |
| Garman-Klass | $\frac{1}{2}(\ln H/L)^2 - (2\ln 2-1)(\ln C/O)^2$ | Partial |
| **Rogers-Satchell** | $\ln(H/O)\ln(H/C) + \ln(L/O)\ln(L/C)$ | **Full** |
| Yang-Zhang | RS + overnight + open-close | Full |

### Efficiency Comparison

| Estimator | Relative Efficiency | Data Required |
| :--- | :---: | :--- |
| Close-to-Close | 1.0 | C |
| Parkinson (HLV) | 5.2 | H, L |
| Garman-Klass (GKV) | 7.4 | O, H, L, C |
| **Rogers-Satchell (RSV)** | **8.4** | **O, H, L, C** |
| Yang-Zhang | 14.0 | O, H, L, C (+ prev C) |

RSV achieves 8.4x the efficiency of close-to-close, meaning it produces the same statistical precision with 8.4x fewer observations.

### SMA Properties

**Period Characteristics:**

| Period | Window Size | Adaptation Time |
| :---: | :---: | :---: |
| 10 | 10 bars | Complete after 10 bars |
| 14 | 14 bars | Complete after 14 bars |
| 20 | 20 bars | Complete after 20 bars |

SMA (unlike RMA) has no exponential tail — old values are completely dropped after exactly $period$ bars.

### Annualization Factor

For daily data with 252 trading days:

$$
\sqrt{252} \approx 15.875
$$

Common annualization factors:

| Data Frequency | Periods/Year | Factor |
| :--- | :---: | :---: |
| Daily | 252 | 15.875 |
| Weekly | 52 | 7.211 |
| Monthly | 12 | 3.464 |
| Hourly (6.5h/day) | 1638 | 40.472 |

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations after warmup:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| DIV | 4 | 15 | 60 |
| LOG | 4 | 25 | 100 |
| MUL | 2 | 3 | 6 |
| ADD | 1 | 1 | 1 |
| Buffer update | 1 | 3 | 3 |
| SMA sum | 1 | 5 | 5 |
| DIV (SMA) | 1 | 15 | 15 |
| SQRT | 1 | 15 | 15 |
| MUL (annual) | 1 | 3 | 3 |
| **Total** | — | — | **~208 cycles** |

The dominant cost is the four LOG operations (48% of total). RSV is ~40% slower than HLV but provides drift independence.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| LOG (vectorized) | 2048 | 256 | 8× |
| DIV (vectorized) | 2048 | 256 | 8× |
| MUL/ADD (FMA) | 1536 | 192 | 8× |
| SMA (sliding sum) | 512 | 512 | 1× |
| SQRT (vectorized) | 512 | 64 | 8× |

**Note:** SMA computation can be optimized with a rolling sum (O(1) per bar) but is inherently sequential for the running state.

### Memory Profile

- **Per instance:** ~88 + 8×period bytes (state + circular buffer)
- **With period=20:** ~248 bytes
- **100 instances (period=20):** ~24.8 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Drift-adjusted, unbiased |
| **Efficiency** | 8/10 | 8.4x better than close-to-close |
| **Timeliness** | 8/10 | SMA provides faster adaptation than RMA |
| **Smoothness** | 7/10 | SMA can be jumpier than RMA |
| **Robustness** | 8/10 | Handles trends well |

## Validation

RSV is well-documented in academic literature but implementations vary:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches rsv.pine reference |
| **Manual** | ✅ | Validated against original paper formula |

The implementation is validated against the original Rogers-Satchell 1991 paper formula.

## Common Pitfalls

1. **Warmup period**: RSV requires $period$ bars before producing stable results. With default period=20, the first 19 values are warming up. The `IsHot` property indicates when warmup is complete.

2. **OHLC data quality**: RSV requires all four OHLC prices. Missing or invalid data (high < low, prices ≤ 0) will trigger last-valid-value substitution. Ensure data quality before feeding to RSV.

3. **Zero prices**: Log ratios require positive prices. Prices of zero are clamped to a small epsilon (1e-10) to prevent log(0) = -∞.

4. **Annualization assumption**: Default annualization assumes 252 trading days/year. For other frequencies (hourly, weekly), adjust the `annualPeriods` parameter accordingly.

5. **SMA vs RMA**: Unlike HLV/GKV which use RMA, RSV uses SMA. This means:
   - Complete adaptation after exactly $period$ bars (faster)
   - No bias correction needed
   - Can be "jumpier" when old high/low variance values drop out of the window

6. **Comparison with HLV**: HLV only uses High-Low, ignoring Open-Close. RSV uses all four prices and is drift-adjusted. Use RSV when OHLC data is available and markets are trending; use HLV when only High-Low data exists.

7. **Comparison with GKV**: Both use OHLC, but RSV is fully drift-adjusted while GKV is only partially. RSV is slightly more efficient (8.4x vs 7.4x) and better for trending markets.

## Trading Applications

### Position Sizing

Use RSV to scale position sizes inversely with volatility:

```
Position size = Risk per trade / (RSV × Price × multiplier)
```

Lower RSV allows larger positions; higher RSV requires smaller positions. RSV's drift adjustment makes it more reliable during trends.

### Volatility Regime Detection

Track RSV percentile rank over lookback period:

```
High rank (>80%): High volatility regime — reduce position size, widen stops
Low rank (<20%): Low volatility regime — potential for breakout
```

### Options Pricing Input

RSV provides a realized volatility estimate for comparison with implied volatility:

```
If IV > RSV significantly: Options may be overpriced (sell vol)
If IV < RSV significantly: Options may be underpriced (buy vol)
```

RSV's drift adjustment makes it preferable to HLV/GKV for options analysis during trending markets.

### Trend Quality Assessment

Compare RSV with simpler estimators during trends:

```
If RSV ≈ HLV: Low drift, range-bound market
If RSV < HLV: Significant drift (trending), HLV is overstating vol
```

### Stop-Loss Calibration

Use RSV to set dynamic stop-loss levels:

```
Stop distance = Entry price ± (RSV × K × Price)
where K is a multiplier (typically 1.5-3.0)
```

RSV's drift adjustment prevents stops from being set too wide during strong trends.

## Implementation Notes

### Circular Buffer for SMA

The implementation uses a fixed-size circular buffer for O(1) updates:

```csharp
// Buffer stores RS variance values
_buffer[_bufferIndex] = rsVariance;
_bufferIndex = (_bufferIndex + 1) % _period;
_bufferSum = _bufferSum - oldest + rsVariance;
```

This avoids O(n) summation on each update.

### Price Protection

All price ratios are protected against zero/negative values:

```csharp
open = Math.Max(open, 1e-10);
close = Math.Max(close, 1e-10);
```

### FMA Optimization

The variance calculation uses FMA where beneficial:

```csharp
rsVariance = Math.FusedMultiplyAdd(lnHO, lnHC, lnLO * lnLC);
```

## References

- Rogers, L. C. G., & Satchell, S. E. (1991). "Estimating Variance from High, Low and Closing Prices." *Annals of Applied Probability*, 1(4), 504-512.
- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*, 53(1), 61-65.
- Garman, M. B., & Klass, M. J. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*, 53(1), 67-78.
- Yang, D., & Zhang, Q. (2000). "Drift-Independent Volatility Estimation Based on High, Low, Open, and Close Prices." *Journal of Business*, 73(3), 477-492.
- Alizadeh, S., Brandt, M. W., & Diebold, F. X. (2002). "Range-Based Estimation of Stochastic Volatility Models." *Journal of Finance*, 57(3), 1047-1091.