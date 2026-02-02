# GKV: Garman-Klass Volatility

> "Why settle for closing prices when you have the full trading range? It's like judging a book by its last page."

Garman-Klass Volatility (GKV) is a range-based volatility estimator that uses all four OHLC prices to provide more efficient volatility estimates than traditional close-to-close methods. Developed by Mark Garman and Michael Klass in 1980, this estimator achieves theoretical efficiency gains of 7-8x over simple close-to-close variance by incorporating intraday price information. The implementation includes RMA (Wilder's) smoothing with bias correction and optional annualization.

## Historical Context

Mark B. Garman and Michael J. Klass introduced this estimator in their 1980 paper "On the Estimation of Security Price Volatilities from Historical Data," published in the Journal of Business. Their work addressed a fundamental inefficiency: close-to-close volatility estimators discard valuable intraday price information.

The Garman-Klass estimator derives from the theory of diffusion processes, assuming prices follow geometric Brownian motion. The key insight is that the high-low range contains significant information about volatility that close-to-close methods ignore. By weighting the log-range and open-close components appropriately, the GK estimator achieves near-optimal efficiency under the assumption of continuous trading with no drift.

The famous coefficient $2\ln(2) - 1 \approx 0.386$ emerges from the mathematical derivation as the optimal weight for the open-close component. This specific value minimizes the variance of the estimator under the Brownian motion assumption.

## Architecture & Physics

### 1. Log Price Transformation

All calculations use log prices to normalize percentage returns:

$$
\ln H_t, \ln L_t, \ln O_t, \ln C_t
$$

where:

- $H_t, L_t, O_t, C_t$ = High, Low, Open, Close prices at time $t$

Log transformation ensures that equal percentage moves have equal magnitude regardless of price level.

### 2. Garman-Klass Estimator

The single-period GK variance estimator combines two terms:

$$
\hat{\sigma}^2_{GK,t} = 0.5 \cdot (\ln H_t - \ln L_t)^2 - (2\ln 2 - 1) \cdot (\ln C_t - \ln O_t)^2
$$

Equivalently:

$$
\hat{\sigma}^2_{GK,t} = \underbrace{0.5 \cdot r_{HL}^2}_{\text{term1}} - \underbrace{0.386 \cdot r_{CO}^2}_{\text{term2}}
$$

where:

- $r_{HL} = \ln H_t - \ln L_t$ (log high-low range)
- $r_{CO} = \ln C_t - \ln O_t$ (log close-open return)
- $2\ln 2 - 1 \approx 0.38629436$ (Garman-Klass coefficient)

### 3. RMA Smoothing with Bias Correction

The raw estimator is smoothed using an RMA (Wilder's Moving Average):

$$
RMA_t^{raw} = RMA_{t-1}^{raw} \cdot (1 - \alpha) + \alpha \cdot \hat{\sigma}^2_{GK,t}
$$

where:

- $\alpha = 1 / period$
- Default $period = 20$

Bias correction compensates for the exponential startup:

$$
e_t = (1 - \alpha)^t
$$

$$
RMA_t^{corrected} = \frac{RMA_t^{raw}}{1 - e_t}
$$

### 4. Volatility Calculation

Convert variance to volatility (standard deviation):

$$
\sigma_t = \sqrt{RMA_t^{corrected}}
$$

### 5. Optional Annualization

If annualization is enabled (default):

$$
\sigma_{annual,t} = \sigma_t \times \sqrt{N}
$$

where $N$ = annual periods (default 252 trading days).

## Mathematical Foundation

### Garman-Klass Coefficient Derivation

The coefficient $2\ln 2 - 1$ arises from minimizing the variance of the estimator under Brownian motion assumptions. For a diffusion process $dS = \sigma S dW$:

$$
E[(\ln H - \ln L)^2] = 4 \ln 2 \cdot \sigma^2 \cdot \Delta t
$$

$$
E[(\ln C - \ln O)^2] = \sigma^2 \cdot \Delta t
$$

The optimal combination that minimizes estimator variance yields the coefficient:

$$
c = 2\ln 2 - 1 \approx 0.38629436
$$

### Efficiency Comparison

| Estimator | Relative Efficiency |
| :--- | :---: |
| Close-to-Close | 1.0 |
| Parkinson (High-Low) | 5.2 |
| Garman-Klass (OHLC) | 7.4 |
| Rogers-Satchell | 8.4 |
| Yang-Zhang | 14.0 |

GKV achieves 7.4x the efficiency of close-to-close, meaning it produces the same statistical precision with 7.4x fewer observations.

### RMA Properties

**Smoothing Factor:**

$$
\alpha = \frac{1}{period}
$$

| Period | α | Half-life (bars) |
| :---: | :---: | :---: |
| 10 | 0.100 | 6.6 |
| 14 | 0.071 | 9.4 |
| 20 | 0.050 | 13.5 |
| 30 | 0.033 | 20.5 |

RMA (Wilder's) is more responsive than SMA but slower than EMA with equivalent period.

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
| LOG | 4 | 25 | 100 |
| SUB | 3 | 1 | 3 |
| MUL | 3 | 3 | 9 |
| FMA (RMA) | 1 | 4 | 4 |
| DIV (bias) | 1 | 15 | 15 |
| SQRT | 1 | 15 | 15 |
| MUL (annual) | 1 | 3 | 3 |
| **Total** | — | — | **~149 cycles** |

The dominant cost is the four LOG operations (67% of total).

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| LOG (vectorized) | 2048 | 256 | 8× |
| Range calculations | 1536 | 192 | 8× |
| RMA (sequential) | 512 | 512 | 1× |
| SQRT (vectorized) | 512 | 64 | 8× |

**Note:** RMA smoothing is inherently sequential, limiting total batch improvement. LOG operations benefit most from SIMD vectorization.

### Memory Profile

- **Per instance:** ~72 bytes (state struct)
- **No ring buffer required** (RMA is recursive)
- **100 instances:** ~7.2 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Theoretically optimal under Brownian motion |
| **Efficiency** | 9/10 | 7.4x better than close-to-close |
| **Timeliness** | 7/10 | RMA introduces smoothing lag |
| **Smoothness** | 8/10 | RMA provides stable output |
| **Robustness** | 7/10 | Sensitive to gaps and overnight moves |

## Validation

GKV is well-documented in academic literature but less common in technical analysis libraries:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches gkv.pine reference |
| **Manual** | ✅ | Validated against formula |

The implementation is validated against the original Garman-Klass 1980 paper formula.

## Common Pitfalls

1. **Warmup period**: GKV requires $period$ bars before producing stable results. With default period=20, the first 19 values are warming up. The `IsHot` property indicates when warmup is complete.

2. **Negative estimator values**: The GK estimator can produce negative values when the close-open range dominates the high-low range (e.g., gap days with narrow trading range). The implementation handles this by returning zero volatility for negative variance.

3. **Invalid OHLC data**: Prices must be positive for log transformation. Zero or negative prices, or logically invalid OHLC (high < low, etc.) trigger last-valid-value substitution.

4. **Annualization assumption**: Default annualization assumes 252 trading days/year. For other frequencies (hourly, weekly), adjust the `annualPeriods` parameter accordingly.

5. **Drift assumption**: The GK estimator assumes zero drift (no trend). During strong trends, the estimator may underestimate volatility. Consider Rogers-Satchell or Yang-Zhang for trending markets.

6. **Gap sensitivity**: Unlike close-to-close methods, GKV doesn't directly capture overnight gaps. A stock that gaps up 5% then trades in a narrow range will show low GKV despite the gap.

7. **Not a trading signal**: GKV measures volatility magnitude, not direction. High volatility can precede moves in either direction; use with directional indicators for trading signals.

## Trading Applications

### Position Sizing

Use GKV to scale position sizes inversely with volatility:

```
Position size = Risk per trade / (GKV × Price × ATR multiplier)
```

Lower GKV allows larger positions; higher GKV requires smaller positions.

### Options Pricing Input

GKV provides a realized volatility estimate for comparison with implied volatility:

```
If IV > GKV significantly: Options may be overpriced (sell vol)
If IV < GKV significantly: Options may be underpriced (buy vol)
```

### Regime Detection

Track GKV percentile rank over lookback period:

```
High rank (>80%): High volatility regime — reduce position size, widen stops
Low rank (<20%): Low volatility regime — potential for breakout
```

### Volatility Breakout Filter

Combine GKV with rate-of-change:

```
Signal: GKV crosses above 20-bar high → volatility expansion
Confirmation: Wait for directional move
```

## References

- Garman, M. B., & Klass, M. J. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*, 53(1), 67-78.
- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*, 53(1), 61-65.
- Rogers, L. C. G., & Satchell, S. E. (1991). "Estimating Variance from High, Low and Closing Prices." *Annals of Applied Probability*, 1(4), 504-512.
- Yang, D., & Zhang, Q. (2000). "Drift-Independent Volatility Estimation Based on High, Low, Open, and Close Prices." *Journal of Business*, 73(3), 477-491.