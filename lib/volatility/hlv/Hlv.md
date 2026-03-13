# HLV: High-Low Volatility (Parkinson)

> *The simplest solution is often the most elegant. When you only need the peaks and valleys, why ask for the whole journey?*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20), `annualize` (default true), `annualPeriods` (default 252)                      |
| **Outputs**      | Single series (Hlv)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [hlv.pine](hlv.pine)                       |

- *Also known as: PV (Parkinson Volatility)*
- **Similar:** [GKV](../gkv/gkv.md), [ATR](../atr/atr.md) | **Complementary:** ATR comparison | **Trading note:** Parkinson high-low volatility; ~5x more efficient than close-to-close.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

*Also known as: PV (Parkinson Volatility)*


High-Low Volatility (HLV), also known as the Parkinson estimator, is a range-based volatility measure that uses only the high and low prices of each period. Developed by Michael Parkinson in 1980, this estimator achieves approximately 5x better efficiency than close-to-close methods by exploiting the information content in the trading range. The implementation includes RMA (Wilder's) smoothing with bias correction and optional annualization.

## Historical Context

Michael Parkinson introduced this estimator in his 1980 paper "The Extreme Value Method for Estimating the Variance of the Rate of Return," published in the Journal of Business. The paper demonstrated that the high-low range of a diffusion process contains significantly more information about volatility than the closing price alone.

The Parkinson estimator is the simplest of the range-based volatility estimators, requiring only two data points per period (high and low) rather than the four required by Garman-Klass or Yang-Zhang. This simplicity makes it particularly useful when open and close prices are unavailable or unreliable, such as in some commodity markets or older historical data.

The famous coefficient $\frac{1}{4\ln 2} \approx 0.3607$ emerges from the mathematical derivation assuming prices follow a continuous geometric Brownian motion without drift. This coefficient normalizes the squared log range to produce an unbiased variance estimate.

## Architecture & Physics

### 1. Log Price Transformation

All calculations use log prices to normalize percentage returns:

$$
\ln H_t, \ln L_t
$$

where:

- $H_t, L_t$ = High, Low prices at time $t$

Log transformation ensures that equal percentage moves have equal magnitude regardless of price level.

### 2. Parkinson Estimator

The single-period Parkinson variance estimator uses only the log high-low range:

$$
\hat{\sigma}^2_{P,t} = \frac{1}{4\ln 2} \cdot (\ln H_t - \ln L_t)^2
$$

Equivalently:

$$
\hat{\sigma}^2_{P,t} = C \cdot r_{HL}^2
$$

where:

- $r_{HL} = \ln H_t - \ln L_t$ (log high-low range)
- $C = \frac{1}{4\ln 2} \approx 0.36067376$ (Parkinson coefficient)

### 3. RMA Smoothing with Bias Correction

The raw estimator is smoothed using an RMA (Wilder's Moving Average):

$$
RMA_t^{raw} = RMA_{t-1}^{raw} \cdot (1 - \alpha) + \alpha \cdot \hat{\sigma}^2_{P,t}
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

### Parkinson Coefficient Derivation

The coefficient $\frac{1}{4\ln 2}$ arises from the distribution of the range of a standard Brownian motion. For a diffusion process $dS = \sigma S dW$ over time interval $\Delta t$:

The expected value of the squared log range is:

$$
E[(\ln H - \ln L)^2] = 4 \ln 2 \cdot \sigma^2 \cdot \Delta t
$$

Therefore, to obtain an unbiased estimator of variance:

$$
\hat{\sigma}^2 = \frac{(\ln H - \ln L)^2}{4 \ln 2}
$$

The coefficient:

$$
\frac{1}{4\ln 2} = \frac{1}{4 \times 0.693147...} \approx 0.36067376
$$

### Efficiency Comparison

| Estimator | Relative Efficiency | Data Required |
| :--- | :---: | :--- |
| Close-to-Close | 1.0 | C |
| Parkinson (HLV) | 5.2 | H, L |
| Garman-Klass (GKV) | 7.4 | O, H, L, C |
| Rogers-Satchell | 8.4 | O, H, L, C |
| Yang-Zhang | 14.0 | O, H, L, C |

HLV (Parkinson) achieves 5.2x the efficiency of close-to-close, meaning it produces the same statistical precision with 5.2x fewer observations. While less efficient than OHLC-based estimators, it requires only high-low data.

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
| LOG | 2 | 25 | 50 |
| SUB | 1 | 1 | 1 |
| MUL | 2 | 3 | 6 |
| FMA (RMA) | 1 | 4 | 4 |
| DIV (bias) | 1 | 15 | 15 |
| SQRT | 1 | 15 | 15 |
| MUL (annual) | 1 | 3 | 3 |
| **Total** | — | — | **~94 cycles** |

The dominant cost is the two LOG operations (53% of total). HLV is ~37% faster than GKV due to requiring only 2 logs instead of 4.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| LOG (vectorized) | 1024 | 128 | 8× |
| Range calculations | 512 | 64 | 8× |
| RMA (sequential) | 512 | 512 | 1× |
| SQRT (vectorized) | 512 | 64 | 8× |

**Note:** RMA smoothing is inherently sequential, limiting total batch improvement. LOG operations benefit most from SIMD vectorization.

### Memory Profile

- **Per instance:** ~56 bytes (state struct)
- **No ring buffer required** (RMA is recursive)
- **100 instances:** ~5.6 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Optimal under Brownian motion, simpler than GKV |
| **Efficiency** | 8/10 | 5.2x better than close-to-close |
| **Timeliness** | 7/10 | RMA introduces smoothing lag |
| **Smoothness** | 8/10 | RMA provides stable output |
| **Simplicity** | 10/10 | Only requires high-low data |

## Validation

HLV (Parkinson) is well-documented in academic literature but less common in technical analysis libraries:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches hlv.pine reference |
| **Manual** | ✅ | Validated against formula |

The implementation is validated against the original Parkinson 1980 paper formula.

## Common Pitfalls

1. **Warmup period**: HLV requires $period$ bars before producing stable results. With default period=20, the first 19 values are warming up. The `IsHot` property indicates when warmup is complete.

2. **Zero range handling**: When high equals low (no trading range), the log range is zero, producing zero volatility for that bar. This can occur with limit-locked securities or thin markets.

3. **Invalid price data**: Prices must be positive for log transformation. Zero or negative prices, or logically invalid data (high < low) trigger last-valid-value substitution.

4. **Annualization assumption**: Default annualization assumes 252 trading days/year. For other frequencies (hourly, weekly), adjust the `annualPeriods` parameter accordingly.

5. **Drift bias**: The Parkinson estimator assumes zero drift (no trend). During strong trends, the estimator tends to underestimate volatility because trending prices compress the high-low range relative to the true volatility.

6. **No overnight information**: Unlike close-to-close methods, HLV doesn't capture overnight gaps at all. It only measures intraday range volatility, missing inter-day price movements.

7. **Comparison with GKV**: HLV is simpler (2 prices vs 4) but less efficient (5.2x vs 7.4x). Use GKV when OHLC data is available and reliability matters; use HLV when only high-low data exists.

## Trading Applications

### Position Sizing

Use HLV to scale position sizes inversely with volatility:

```
Position size = Risk per trade / (HLV × Price × multiplier)
```

Lower HLV allows larger positions; higher HLV requires smaller positions.

### Volatility Comparison

Compare HLV across similar assets:

```
If Asset A HLV < Asset B HLV: Asset A has lower intraday volatility
Useful for: Sector rotation, pairs trading selection
```

### Range Breakout Calibration

Use HLV to set dynamic breakout thresholds:

```
Breakout threshold = Current price ± (HLV × K × Price)
where K is a multiplier (typically 1.5-3.0)
```

### Volatility Regime Detection

Track HLV percentile rank over lookback period:

```
High rank (>80%): High volatility regime — reduce position size, widen stops
Low rank (<20%): Low volatility regime — potential for breakout
```

### Options Pricing Input

HLV provides a realized volatility estimate for comparison with implied volatility:

```
If IV > HLV significantly: Options may be overpriced (sell vol)
If IV < HLV significantly: Options may be underpriced (buy vol)
Note: HLV may underestimate true volatility due to drift bias
```

## References

- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*, 53(1), 61-65.
- Garman, M. B., & Klass, M. J. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*, 53(1), 67-78.
- Rogers, L. C. G., & Satchell, S. E. (1991). "Estimating Variance from High, Low and Closing Prices." *Annals of Applied Probability*, 1(4), 504-512.
- Alizadeh, S., Brandt, M. W., & Diebold, F. X. (2002). "Range-Based Estimation of Stochastic Volatility Models." *Journal of Finance*, 57(3), 1047-1091.