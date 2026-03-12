# RV: Realized Volatility

> *The sum of squared returns—a direct measure of how much the market actually moved, free from the assumptions embedded in standard deviation.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 5), `smoothingPeriod` (default 20), `annualize` (default true), `annualPeriods` (default 252)                      |
| **Outputs**      | Single series (Rv)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [rv.pine](rv.pine)                       |

- Realized Volatility (RV) measures price volatility using the sum of squared logarithmic returns over a rolling window, then applying SMA smoothing ...
- Parameterized by `period` (default 5), `smoothingperiod` (default 20), `annualize` (default true), `annualperiods` (default 252).
- Output range: $\geq 0$.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Realized Volatility (RV) measures price volatility using the sum of squared logarithmic returns over a rolling window, then applying SMA smoothing for stability. Unlike traditional Historical Volatility (HV) which calculates standard deviation of returns, RV directly accumulates squared returns—the raw building blocks of variance—providing a more direct measure of realized price variation.

## Historical Context

Realized volatility emerged from the academic literature on high-frequency econometrics in the late 1990s and early 2000s, most notably through the work of Andersen, Bollerslev, Diebold, and Labys (2001). The concept was developed to provide model-free volatility estimates using intraday data, addressing limitations of parametric approaches like GARCH.

The key insight was that as sampling frequency increases, the sum of squared returns converges to the quadratic variation of the price process—the true integrated variance. While the original formulation targeted tick-by-tick or 5-minute returns, the concept applies at any frequency.

This implementation adapts the realized volatility concept to standard bar data:
- Calculate squared log returns within a rolling window (period)
- Take the square root to convert variance to volatility
- Apply SMA smoothing for noise reduction
- Optionally annualize for comparability

## Architecture & Physics

### 1. Log Return Calculation

Each period's return is computed as the natural logarithm of price ratios:

$$
r_t = \ln\left(\frac{P_t}{P_{t-1}}\right)
$$

where:

- $P_t$ = Closing price at time $t$
- $P_{t-1}$ = Closing price at time $t-1$

### 2. Squared Return Accumulation

The realized variance is the sum of squared returns over the rolling window:

$$
RVar_t = \sum_{i=0}^{n-1} r_{t-i}^2
$$

where $n$ = period (default 5).

This differs from standard variance which subtracts the mean:
- Standard variance: $\sigma^2 = E[(X - \mu)^2]$
- Realized variance: $RVar = \sum r^2$ (assumes zero mean over short windows)

### 3. Volatility Conversion

Convert realized variance to volatility (standard deviation scale):

$$
RVol_t = \sqrt{RVar_t}
$$

### 4. SMA Smoothing

Apply simple moving average to smooth the raw volatility:

$$
RV_t = \frac{1}{m} \sum_{i=0}^{m-1} RVol_{t-i}
$$

where $m$ = smoothingPeriod (default 20).

### 5. Optional Annualization

If annualization is enabled:

$$
RV_{annual,t} = RV_t \times \sqrt{N}
$$

where $N$ = annual periods (default 252 trading days).

## Mathematical Foundation

### Theoretical Basis

Under the standard diffusion model $dS = \mu S dt + \sigma S dW$, the quadratic variation is:

$$
\langle \ln S \rangle_T = \int_0^T \sigma^2 dt
$$

The realized variance estimator:

$$
RVar = \sum_{i=1}^{n} r_i^2
$$

is a consistent estimator of integrated variance as the sampling frequency increases.

### Why Sum Squared Returns (Not Standard Deviation)?

The realized volatility approach differs from HV in a subtle but important way:

| Aspect | HV (Standard Deviation) | RV (Sum Squared Returns) |
| :--- | :--- | :--- |
| **Formula** | $\sqrt{\frac{1}{n}\sum(r - \bar{r})^2}$ | $\sqrt{\sum r^2}$ |
| **Mean treatment** | Subtracts sample mean | Assumes zero mean |
| **Interpretation** | Dispersion around mean | Total quadratic variation |
| **Best for** | Longer windows | Short windows, intraday |

For short windows (5-10 bars), the mean return is essentially noise. RV avoids estimating this noisy mean, providing a more stable measure.

### Relationship to HV

For a window with $n$ returns and mean $\bar{r}$:

$$
\sum r_i^2 = n \cdot \sigma_{pop}^2 + n \cdot \bar{r}^2
$$

When the mean is small (short windows, mean-reverting markets), both measures converge. RV will be slightly higher when there's a directional move within the window.

### SMA Smoothing Rationale

Raw realized volatility can be noisy, especially with small period values. The SMA smoothing:
- Reduces day-to-day noise
- Provides more stable signals
- Allows customization (shorter smoothing = more responsive, longer = more stable)

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations after warmup:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| LOG | 1 | 25 | 25 |
| DIV (price ratio) | 1 | 15 | 15 |
| MUL (squared) | 1 | 3 | 3 |
| ADD/SUB (ring buffer) | 2 | 1 | 2 |
| SQRT | 1 | 15 | 15 |
| ADD/SUB (SMA) | 2 | 1 | 2 |
| DIV (SMA) | 1 | 15 | 15 |
| MUL (annualize) | 1 | 3 | 3 |
| **Total** | — | — | **~80 cycles** |

The dominant costs are LOG (31%) and SQRT/DIV (19% each).

### Memory Profile

- **Per instance:** ~120 bytes (state struct + two RingBuffer references)
- **Return buffer:** 8 bytes × period (default 5 = 40 bytes)
- **Volatility buffer:** 8 bytes × smoothingPeriod (default 20 = 160 bytes)
- **100 instances @ defaults:** ~32 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Model-free, converges to integrated variance |
| **Timeliness** | 8/10 | Short period captures recent moves quickly |
| **Smoothness** | 7/10 | SMA smoothing provides stability |
| **Flexibility** | 8/10 | Two parameters allow tuning responsiveness |
| **Simplicity** | 8/10 | Clear interpretation, straightforward implementation |

## Validation

RV is a custom implementation based on the realized volatility literature. Direct library comparisons are not available:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches rv.pine reference |
| **Manual** | ✅ | Validated against formula |

The implementation is validated against the mathematical formula and internal consistency tests (streaming = batch = span).

## Common Pitfalls

1. **Warmup period**: RV requires `period + smoothingPeriod` prices before producing valid results. With defaults (5, 20), you need 25 prices. The `IsHot` property indicates when warmup is complete.

2. **Zero or negative prices**: Log transformation requires positive prices. Zero or negative values trigger last-valid-value substitution to prevent NaN propagation.

3. **Period selection**: The period parameter controls how many squared returns are summed. Shorter periods (3-5) capture recent volatility bursts; longer periods (10-20) provide more stable variance estimates.

4. **Smoothing vs responsiveness trade-off**: Higher smoothingPeriod reduces noise but increases lag. For trading signals, consider shorter smoothing (5-10); for regime detection, longer smoothing (20-50).

5. **Comparison with HV**: RV measures total squared returns; HV measures dispersion around mean. During strong trends, RV > HV because it captures the directional move. Neither is "better"—they measure different things.

6. **Annualization assumptions**: Default annualization assumes 252 trading days. Adjust for intraday data, cryptocurrency (365 days), or weekly data.

7. **Minimum period constraint**: Period must be ≥ 2 to have at least one squared return in the window. SmoothingPeriod must be ≥ 1.

8. **Not the same as VIX methodology**: VIX uses option prices to derive implied volatility. This RV measures realized (historical) volatility from price data only.

## Trading Applications

### Volatility Regime Detection

Track RV percentile over lookback:

```
High RV (>80th percentile): High volatility regime
- Markets are moving significantly
- Consider wider stops, reduced position sizes
- Mean reversion in volatility may be near

Low RV (<20th percentile): Low volatility regime
- Markets are quiet
- Breakout potential increasing
- Options may be cheap
```

### Realized vs Implied Volatility Spread

Compare RV with option-implied volatility:

```
IV > RV (positive spread): Options are relatively expensive
- Volatility selling strategies may be attractive
- Market expects future volatility > recent realized

IV < RV (negative spread): Options are relatively cheap
- Volatility buying strategies may be attractive
- Market may be underpricing risk
```

### Position Sizing

Use RV for volatility-adjusted sizing:

```
Base position × (Target RV / Current RV)

Example: If target is 15% annualized volatility and current RV is 30%:
Position = Base × (0.15 / 0.30) = 50% of base
```

### Volatility Breakout Strategy

```
Entry: RV crosses above X-day high RV
Exit: RV falls below Y-day average RV

The period and smoothingPeriod parameters allow tuning:
- Short period + short smoothing: Catch quick volatility spikes
- Longer period + longer smoothing: Identify sustained regime changes
```

### Comparing Multiple Timeframes

```
RV(5, 5) vs RV(5, 20) vs RV(5, 50)

Converging: Volatility regime is stable
Diverging (short > long): Recent volatility spike
Diverging (short < long): Volatility compression
```

## References

- Andersen, T. G., Bollerslev, T., Diebold, F. X., & Labys, P. (2001). "The Distribution of Realized Exchange Rate Volatility." *Journal of the American Statistical Association*, 96(453), 42-55.
- Andersen, T. G., Bollerslev, T., Diebold, F. X., & Ebens, H. (2001). "The Distribution of Realized Stock Return Volatility." *Journal of Financial Economics*, 61(1), 43-76.
- Barndorff-Nielsen, O. E., & Shephard, N. (2002). "Econometric Analysis of Realized Volatility and Its Use in Estimating Stochastic Volatility Models." *Journal of the Royal Statistical Society: Series B*, 64(2), 253-280.
- McAleer, M., & Medeiros, M. C. (2008). "Realized Volatility: A Review." *Econometric Reviews*, 27(1-3), 10-45.
