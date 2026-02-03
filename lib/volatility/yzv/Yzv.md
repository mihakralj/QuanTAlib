# YZV: Yang-Zhang Volatility

> "The best volatility estimator uses all the information the market gives you—overnight gaps, intraday swings, and everything in between."

Yang-Zhang Volatility is a sophisticated volatility estimator that combines overnight (close-to-open) returns with Rogers-Satchell intraday volatility to capture the full spectrum of price dynamics. Unlike simple close-to-close volatility that misses overnight gaps, or purely intraday measures that ignore opening moves, Yang-Zhang provides a theoretically unbiased estimate that remains consistent whether markets gap or drift.

## Historical Context

Introduced by Dennis Yang and Qiang Zhang in their 2000 paper "Drift-Independent Volatility Estimation Based on High, Low, Open, and Close Prices," this estimator addressed a fundamental gap in volatility measurement. Traditional close-to-close volatility understates true volatility when significant price movements occur outside trading hours. The Parkinson (1980) and Garman-Klass (1980) estimators used high-low information but assumed continuous trading with no overnight gaps.

Yang and Zhang combined three components:
1. **Overnight volatility** ($\sigma_o^2$): Captures close-to-open gaps
2. **Open-to-close volatility** ($\sigma_c^2$): Captures standard intraday drift
3. **Rogers-Satchell volatility** ($\sigma_{RS}^2$): Captures intraday high-low range accounting for drift

The key innovation was deriving optimal weights that minimize variance while remaining independent of price drift. The resulting estimator is approximately 8× more efficient than close-to-close for capturing true volatility.

## Architecture & Physics

### 1. Log Return Components

For each bar, compute four log returns relative to the previous close and current open:

$$
r_o = \ln\left(\frac{O_t}{C_{t-1}}\right) \quad \text{(overnight return)}
$$

$$
r_c = \ln\left(\frac{C_t}{O_t}\right) \quad \text{(open-to-close return)}
$$

$$
r_h = \ln\left(\frac{H_t}{O_t}\right) \quad \text{(high relative to open)}
$$

$$
r_l = \ln\left(\frac{L_t}{O_t}\right) \quad \text{(low relative to open)}
$$

### 2. Yang-Zhang Weighting Factor

The optimal weight $k$ that minimizes estimator variance:

$$
k = \frac{0.34}{1.34 + \frac{n+1}{n-1}}
$$

where $n$ is the smoothing period. For typical values:
- $n = 10$: $k \approx 0.196$
- $n = 20$: $k \approx 0.215$
- $n = 30$: $k \approx 0.222$

### 3. Daily Variance Components

**Overnight variance:**
$$
\sigma_o^2 = r_o^2
$$

**Open-to-close variance:**
$$
\sigma_c^2 = r_c^2
$$

**Rogers-Satchell variance (drift-independent intraday measure):**
$$
\sigma_{RS}^2 = r_h \cdot (r_h - r_c) + r_l \cdot (r_l - r_c)
$$

### 4. Combined Daily Variance

$$
\sigma_{daily}^2 = \sigma_o^2 + k \cdot \sigma_c^2 + (1 - k) \cdot \sigma_{RS}^2
$$

### 5. Smoothed Volatility Output

Apply exponential smoothing (RMA) to daily variance with bias correction, then take square root:

$$
\text{YZV}_t = \sqrt{\text{RMA}(\sigma_{daily}^2, n)}
$$

## Mathematical Foundation

### Bias-Corrected RMA

The implementation uses RMA (Relative Moving Average, equivalent to EMA with $\alpha = 1/n$) with bias correction to handle the startup period:

$$
\text{RMA}_t = \alpha \cdot x_t + (1 - \alpha) \cdot \text{RMA}_{t-1}
$$

where $\alpha = 1/n$.

**Bias compensator:**
$$
e_t = (1 - \alpha)^t
$$

**Corrected output:**
$$
\text{RMA}_{corrected} = \frac{\text{RMA}_{raw}}{1 - e_t}
$$

This ensures the first few bars don't suffer from initialization bias.

### Rogers-Satchell Properties

The Rogers-Satchell component has elegant properties:
- **Drift-independent**: Provides consistent estimates regardless of price trend
- **Efficiency**: Uses high and low prices for information gain
- **Non-negativity**: Always ≥ 0 when calculated correctly

The formula $r_h(r_h - r_c) + r_l(r_l - r_c)$ can be rewritten as:
$$
\sigma_{RS}^2 = r_h \cdot r_l - r_l \cdot r_c - r_h \cdot r_c + r_h^2 + r_l^2 - r_l^2
$$

### Example Calculation

Period = 2, Bars: [(O=100, H=105, L=98, C=103), (O=102, H=108, L=101, C=106)]

**Bar 1** (assuming previous close = 99):
- $r_o = \ln(100/99) = 0.01005$
- $r_c = \ln(103/100) = 0.02956$
- $r_h = \ln(105/100) = 0.04879$
- $r_l = \ln(98/100) = -0.02020$
- $\sigma_o^2 = 0.0001010$
- $\sigma_c^2 = 0.0008738$
- $\sigma_{RS}^2 = 0.04879(0.04879-0.02956) + (-0.02020)((-0.02020)-0.02956) = 0.001935$
- $k = 0.34/(1.34 + 3/1) = 0.0783$
- $\sigma_{daily}^2 = 0.0001010 + 0.0783(0.0008738) + 0.9217(0.001935) = 0.001953$

**Bar 2** (previous close = 103):
- Similar calculation...
- Apply RMA to variance sequence
- Output = sqrt(smoothed variance)

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| LN (natural log) | 4 | 50 | 200 |
| MUL | 12 | 3 | 36 |
| ADD/SUB | 8 | 1 | 8 |
| DIV | 3 | 15 | 45 |
| SQRT | 1 | 15 | 15 |
| FMA candidates | 3 | 5 | 15 |
| **Total** | — | — | **~319 cycles** |

The logarithm operations dominate the cost.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| LN | 2048 | 256 | 8× |
| Arithmetic | 6144 | 768 | 8× |
| SQRT | 512 | 64 | 8× |

**Per-bar savings with SIMD/FMA:**

| Optimization | Cycles Saved | New Total |
| :--- | :---: | :---: |
| SIMD LN | ~175 | ~144 |
| FMA for compound ops | ~10 | ~134 |
| **Total SIMD/FMA** | **~185 cycles** | **~134 cycles** |

### Memory Profile

- **Per instance:** ~120 bytes (state record + backup)
- **100 instances:** ~12 KB
- **Minimal footprint**: No ring buffers required (RMA is recursive)

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Theoretically optimal, unbiased estimator |
| **Timeliness** | 8/10 | Responds within period bars |
| **Efficiency** | 9/10 | ~8× more efficient than close-to-close |
| **Gap Handling** | 10/10 | Explicitly models overnight returns |
| **Drift Independence** | 10/10 | Rogers-Satchell component is drift-free |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches yzv.pine reference |
| **Self-consistency** | ✅ | Streaming = Batch modes match |

## Common Pitfalls

1. **First bar handling**: On the very first bar, there's no previous close. The implementation uses the current open as the "previous close" for this bar only, meaning $r_o = 0$ for bar 0.

2. **Warmup period**: YZV needs approximately `Period` bars before producing stable estimates. The bias-corrected RMA helps, but early values during warmup may still be less reliable.

3. **Negative variance guard**: Due to floating-point precision, the Rogers-Satchell component can theoretically go slightly negative in edge cases. The implementation guards against this by clamping variance to zero before taking the square root.

4. **Scale interpretation**: YZV output is in the same units as the log-return standard deviation (essentially a percentage in decimal form). A value of 0.02 means ~2% daily volatility.

5. **Parameter sensitivity**: The optimal $k$ weight depends on period. Don't reuse $k$ values calculated for different periods—the formula must be recomputed.

6. **Gap vs no-gap markets**: For instruments that trade 24/7 (crypto, forex), the overnight component may be less meaningful. Consider using only the Rogers-Satchell component for such markets.

## Trading Applications

### Volatility Forecasting

Yang-Zhang provides more accurate current volatility estimates, improving forecasts:

```
Forecast accuracy: YZV > Close-to-close > Parkinson
Use for: Option pricing, VaR calculations, position sizing
```

### Regime Detection

Monitor YZV for volatility regime changes:

```
Rising YZV: Increasing market uncertainty
Falling YZV: Settling market conditions
YZV > 2 × historical average: High-volatility regime
```

### Options Trading

Better IV estimation for pricing and hedging:

```
If Realized_YZV > Implied_Vol: Options may be underpriced
If Realized_YZV < Implied_Vol: Options may be overpriced
```

### Position Sizing

Scale positions inversely with volatility:

```
Position Size = Target $ Risk / (Entry Price × YZV × Multiplier)
```

### Gap Risk Assessment

Compare overnight vs intraday components:

```
If overnight_component > intraday_component: Gap risk elevated
Consider reducing overnight positions or hedging
```

## Relationship to Other Volatility Measures

| Measure | Compared to YZV |
| :--- | :--- |
| **Close-to-Close** | YZV ~8× more efficient; C2C ignores gaps |
| **Parkinson** | Parkinson ignores gaps; YZV handles them |
| **Garman-Klass** | GK handles overnight but not as optimally weighted |
| **Rogers-Satchell** | RS is a component of YZV; doesn't handle gaps |
| **ATR** | ATR is absolute price-based; YZV is log-return based |
| **Historical Volatility** | YZV is a better HV estimator |

## Implementation Notes

### State Management

The indicator maintains a compact state record:
- `RawRma`: Running RMA value (before bias correction)
- `ECompensator`: Bias compensator $(1-\alpha)^n$
- `PrevClose`: Previous bar's close for overnight return
- `LastValidYzv`: Last valid output for NaN handling
- `Count`: Bar count for warmup tracking
- `HasPrevClose`: Flag for first-bar handling

### NaN/Infinity Handling

Invalid OHLC inputs are detected and the last valid YZV is substituted. This prevents NaN propagation through the RMA chain.

### Numerical Stability

The implementation uses:
- Epsilon guard (1e-10) for division safety in bias correction
- Clamping of variance to ≥ 0 before sqrt
- Last-valid substitution for non-finite results

## References

- Yang, D., & Zhang, Q. (2000). "Drift-Independent Volatility Estimation Based on High, Low, Open, and Close Prices." *Journal of Business*, 73(3), 477-491.
- Rogers, L. C. G., & Satchell, S. E. (1991). "Estimating Variance from High, Low and Closing Prices." *Annals of Applied Probability*, 1(4), 504-512.
- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*, 53(1), 61-65.
- Garman, M. B., & Klass, M. J. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*, 53(1), 67-78.