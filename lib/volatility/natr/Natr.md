# NATR: Normalized Average True Range

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Natr)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | 1 bar                          |

### TL;DR

- NATR normalizes the Average True Range (ATR) as a percentage of the closing price.
- Parameterized by `period` (default 14).
- Output range: $\geq 0$.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The same volatility reads different on different price scales. NATR speaks the universal language of percentages."

NATR normalizes the Average True Range (ATR) as a percentage of the closing price. This is mathematically identical to ATRP (Average True Range Percent)—both compute `(ATR / Close) × 100`. The difference is purely nomenclature: NATR is the term used in TA-Lib and many charting platforms.

## Historical Context

NATR derives from J. Welles Wilder Jr.'s ATR, introduced in his 1978 *New Concepts in Technical Trading Systems*. While Wilder's original ATR provided absolute volatility in price units, traders and quantitative analysts quickly recognized the need for percentage-based normalization.

The "Normalized" moniker became standard in the TA-Lib open-source library, which formalized the calculation as `NATR = (ATR / Close) × 100`. This naming convention spread through the algorithmic trading community, creating the parallel terminology alongside "ATRP" (Average True Range Percent) used in other contexts.

Both names describe the same mathematical transformation: making volatility comparable across instruments with different price levels.

## Architecture & Physics

NATR consists of three cascaded components:

### 1. True Range (TR)

Captures the actual price movement including gaps:

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

Where:

- $H_t$: Current high
- $L_t$: Current low
- $C_{t-1}$: Previous close

First bar uses simple range: $TR_0 = H_0 - L_0$

### 2. RMA Smoothing (Wilder's Method)

ATR smooths TR using Wilder's RMA with $\alpha = 1/N$:

$$
ATR_t = \alpha \cdot TR_t + (1 - \alpha) \cdot ATR_{t-1}
$$

With warmup compensation to eliminate initialization bias:

$$
e_t = e_{t-1} \cdot (1 - \alpha), \quad e_0 = 1
$$

$$
ATR_{compensated} = \frac{ATR_{raw}}{1 - e_t} \quad \text{when } e_t > \epsilon
$$

### 3. Percentage Normalization

$$
NATR_t = \frac{ATR_t}{C_t} \times 100
$$

This transforms absolute volatility into relative volatility, enabling cross-asset comparison.

## Mathematical Foundation

### Complete Formula Chain

Given period $N$:

1. **Parameters**: $\alpha = \frac{1}{N}$, $\text{decay} = 1 - \alpha$

2. **True Range**:
$$
TR_t = \begin{cases}
H_t - L_t & \text{if } t = 0 \\
\max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|) & \text{otherwise}
\end{cases}
$$

3. **RMA with FMA optimization**:
$$
ATR_{raw,t} = \text{FMA}(ATR_{raw,t-1}, \text{decay}, \alpha \cdot TR_t)
$$

4. **Warmup compensation**:
$$
ATR_t = \frac{ATR_{raw,t}}{1 - e_t}
$$

5. **Normalization**:
$$
NATR_t = \frac{ATR_t}{C_t} \times 100
$$

### Warmup Period

Convergence threshold: $e < 0.05$ (5% remaining bias)

$$
\text{WarmupPeriod} = \left\lceil \frac{\ln(0.05)}{\ln(1 - \alpha)} \right\rceil
$$

For $N = 14$: $\text{WarmupPeriod} \approx 42$ bars.

## Performance Profile

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Throughput** | 10/10 | O(1) calculation via RMA + single division |
| **Allocations** | 0 | Zero-allocation streaming; state in record struct |
| **Complexity** | O(1) | Constant time regardless of period |
| **Accuracy** | 10/10 | Exact mathematical computation |
| **Timeliness** | 4/10 | Inherits ATR's lag from RMA smoothing |
| **Overshoot** | 0/10 | Mathematically bounded |
| **Smoothness** | 8/10 | Smooth RMA decay; minor noise from close price variation |

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 3 | H-L, H-PrevC, L-PrevC |
| ABS | 2 | Gap calculations |
| MAX | 2 | True Range selection |
| FMA | 1 | RMA update |
| MUL | 1 | Decay for warmup |
| DIV | 2 | Warmup compensation + percentage |
| MUL | 1 | × 100 |
| **Total** | ~12 ops | Dominated by FMA and divisions |

## Validation

NATR is validated by computing ATR from external libraries and applying the same percentage formula.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **QuanTAlib** | ✅ | Native implementation |
| **TA-Lib** | ✅ | Via `(ATR / Close) × 100`; tolerance 0.10 for warmup divergence |
| **Skender** | ✅ | Via `(GetAtr / Close) × 100` |
| **Tulip** | ✅ | Via `(atr / Close) × 100` |
| **Ooples** | ✅ | Via `(CalculateAverageTrueRange / Close) × 100` |

Note: QuanTAlib's warmup-compensated RMA may diverge 4-7% from classic Wilder implementations over long histories. Both approaches are mathematically valid; QuanTAlib prioritizes accurate early-series values.

## Use Cases

### Cross-Asset Volatility Comparison

Compare volatility across different price scales:

| Asset | Price | ATR | NATR |
| :--- | :---: | :---: | :---: |
| Penny Stock | $2.50 | 0.25 | 10.0% |
| Mid-Cap | $150 | 4.50 | 3.0% |
| Blue Chip | $500 | 5.00 | 1.0% |

ATR suggests Blue Chip is most volatile. NATR reveals Penny Stock has 10× the relative volatility.

### Volatility-Adjusted Position Sizing

```
Position Size = (Account Risk %) / NATR
```

Ensures equal percentage risk per position regardless of asset price.

### Regime Detection

| NATR Range | Interpretation | Strategy Implication |
| :--- | :--- | :--- |
| < 1% | Low volatility | Mean reversion, tight stops |
| 1-3% | Normal | Standard trend-following |
| 3-5% | Elevated | Wider stops, reduced size |
| > 5% | High volatility | Crisis mode, capital preservation |

## Common Pitfalls

1. **Lag Inheritance**: NATR inherits ATR's smoothing lag. It measures recent volatility, not current or future volatility.

2. **Close Price Spikes**: A sharp close creates transient NATR spikes since it affects both TR (numerator) and the denominator simultaneously.

3. **Near-Zero Prices**: Assets approaching zero produce extreme NATR values. Implement minimum price thresholds.

4. **Gap Sensitivity**: Large overnight gaps inflate TR significantly. Consider using gap-adjusted data for equity analysis.

5. **Warmup Period**: The first 40+ bars (for period=14) contain warmup bias. Use `IsHot` to filter unreliable values.

6. **OHLC Requirement**: NATR requires bar data (Open, High, Low, Close). It cannot be computed from close prices alone. Use `Update(TBar)` not `Update(TValue)`.

## Related Indicators

- **ATR**: Absolute volatility measure NATR normalizes
- **ATRN**: ATR normalized to [0,1] based on historical min/max (different algorithm)
- **CV**: Coefficient of Variation—alternative percentage volatility measure
- **HV**: Historical Volatility—annualized standard deviation approach

## References

- Wilder, J.W. (1978). *New Concepts in Technical Trading Systems*. Trend Research.
- TA-Lib documentation: NATR function specification
- TradingView PineScript: `ta.natr()` implementation
