# ATRP: Average True Range Percent

> "Volatility without context is noise. ATRP gives you context."

ATRP normalizes the Average True Range (ATR) as a percentage of the closing price. This transforms an absolute volatility measure into a relative one, enabling meaningful comparisons across different price levels and different assets.

A $5 stock and a $500 stock might both have an ATR of 2.0, but their volatility profiles are completely different. ATRP reveals the truth: the $5 stock is moving 40% while the $500 stock is moving 0.4%.

## Historical Context

ATRP is a derivative of J. Welles Wilder Jr.'s ATR, introduced in his 1978 work *New Concepts in Technical Trading Systems*. While Wilder focused on absolute range, traders quickly realized that percentage-based normalization was necessary for portfolio-level analysis and cross-asset comparison.

The indicator gained prominence with the rise of systematic trading strategies that needed to compare volatility across diverse asset classes—equities, commodities, forex—without the distortion of absolute price differences.

## Architecture & Physics

ATRP builds on ATR's foundation and adds a single normalization step:

1. **True Range (TR)**: Captures the "real" distance price traveled, including gaps.
2. **RMA Smoothing**: Wilder's smoothing method ($\alpha = 1/N$) provides the characteristic slow decay.
3. **Percentage Normalization**: Divides by current close price and multiplies by 100.

### Why Percentage Matters

Consider two scenarios:

* **Stock A**: Price = \$100, ATR = 5.0 → ATRP = 5%
* **Stock B**: Price = \$10, ATR = 2.0 → ATRP = 20%

ATR alone suggests Stock A is more volatile. ATRP reveals Stock B moves four times more in percentage terms—critical information for position sizing and risk management.

## Mathematical Foundation

### 1. True Range (TR)

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

Where:

* $H_t$: Current High
* $L_t$: Current Low
* $C_{t-1}$: Previous Close

### 2. Average True Range (ATR)

$$
ATR_t = RMA(TR, N)
$$

Expanding the RMA:

$$
ATR_t = \frac{ATR_{t-1} \times (N-1) + TR_t}{N}
$$

### 3. ATRP (Percentage)

$$
ATRP_t = \frac{ATR_t}{C_t} \times 100
$$

Where $C_t$ is the current closing price.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) calculation via RMA + single division. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time regardless of period. |
| **Accuracy** | 10 | Matches ATR-based calculation exactly. |
| **Timeliness** | 4 | Inherits ATR's lag due to RMA smoothing. |
| **Overshoot** | 0 | Bounded by mathematical definition. |
| **Smoothness** | 8 | Smooth decay from RMA; slight additional noise from close price variation. |

## Validation

ATRP is validated by computing ATR from external libraries and applying the same percentage formula.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Validated via `(TA_ATR / Close) × 100`. |
| **Skender** | ✅ | Validated via `(GetAtr / Close) × 100`. |
| **Tulip** | ✅ | Validated via `(atr / Close) × 100`. |
| **Ooples** | ✅ | Validated via `(CalculateAverageTrueRange / Close) × 100`. |

## Use Cases

### Position Sizing

ATRP enables volatility-adjusted position sizing:

```
Position Size = Risk Capital / (ATRP × Entry Price)
```

This ensures each position carries equivalent percentage risk regardless of the asset's absolute price.

### Cross-Asset Comparison

Compare volatility across:

* Different price levels (penny stocks vs. blue chips)
* Different asset classes (equities vs. commodities)
* Different time periods (adjusting for price drift)

### Regime Detection

* **ATRP < 1%**: Low volatility regime—expect consolidation, mean reversion strategies favored.
* **ATRP 2-4%**: Normal volatility—standard trend-following conditions.
* **ATRP > 5%**: High volatility regime—crisis conditions, wider stops required.

## Common Pitfalls

* **Lag**: ATRP inherits ATR's lag. It tells you what volatility *was*, not what it *will be*.
* **Close Price Sensitivity**: A sharp close price move affects both the numerator (via TR) and denominator (close), creating transient spikes. Use multiple periods for confirmation.
* **Zero/Near-Zero Prices**: Assets approaching zero will show extreme ATRP values. Ensure minimum price thresholds in screeners.
* **Dividend Adjustments**: Unadjusted price data can create artificial gaps around ex-dividend dates, inflating TR.

## Related Indicators

* **ATR**: The absolute volatility measure ATRP normalizes.
* **NATR**: Similar concept; some implementations differ in smoothing or warmup handling.
* **ATRN**: ATR normalized to [0,1] range based on historical min/max.
* **Volatility Ratio**: Compares current TR to average TR for breakout detection.