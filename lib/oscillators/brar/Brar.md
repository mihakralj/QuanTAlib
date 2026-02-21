# BRAR: Atmosphere and Buying Ratio Indicator

BRAR is a dual-output sentiment oscillator from East Asian technical analysis that decomposes intrabar price dynamics into two independent ratios: AR (Atmosphere Ratio) measuring the relationship between opening price and intrabar range, and BR (Buying Ratio) measuring buying pressure relative to the previous close. The indicator produces two lines oscillating around 100, where AR above 100 indicates bullish intrabar sentiment and BR above 100 indicates net buying pressure over the lookback window.

## Historical Context

BRAR originated in Japanese and Taiwanese equity analysis during the 1980s, where it became a standard feature of domestic charting software before gaining broader recognition in quantitative trading. The indicator belongs to a class of OHLC decomposition oscillators that extract directional information from the relationship between open, high, low, and close prices rather than from close-only series. Unlike Western momentum oscillators that typically operate on a single price input, BRAR requires full OHLC bars, making it structurally similar to Williams %R or Stochastic but with fundamentally different decomposition logic. The "atmosphere" terminology reflects the Japanese market philosophy that open-to-range dynamics capture collective market mood, while the "buying ratio" component captures institutional accumulation pressure relative to settlement prices.

## Architecture & Physics

### Dual-Component Design

BRAR separates intrabar dynamics into two independent measurements:

1. **AR (Atmosphere Ratio):** Measures the open's position within the intrabar range. Numerator accumulates $(H_i - O_i)$ over $n$ bars (upside from open), denominator accumulates $(O_i - L_i)$ (downside from open). The ratio, scaled by 100, indicates whether prices tend to rally or decline from the opening price.

2. **BR (Buying Ratio):** Measures buying pressure relative to the previous close. Numerator accumulates $\max(0, H_i - C_{i-1})$ (gains above prior close), denominator accumulates $\max(0, C_{i-1} - L_i)$ (drops below prior close). The ratio captures net accumulation vs distribution.

### Running Sum Architecture

Both ratios maintain four independent circular buffers with running sums for O(1) streaming updates. When buffer is full, the oldest bar's contribution is subtracted before the new bar's contribution is added. The first close comparison uses open as a fallback when no previous close exists.

### Defensive Division

Both AR and BR return 0.0 when their respective denominators are zero, preventing division-by-zero in flat markets where open equals low (AR) or prior close equals low with no upside (BR).

## Mathematical Foundation

Given OHLC bars $(O_i, H_i, L_i, C_i)$ and lookback period $n$:

**AR (Atmosphere Ratio):**

$$AR = \frac{\sum_{i=1}^{n} (H_i - O_i)}{\sum_{i=1}^{n} (O_i - L_i)} \times 100$$

**BR (Buying Ratio):**

$$BR = \frac{\sum_{i=1}^{n} \max(0,\; H_i - C_{i-1})}{\sum_{i=1}^{n} \max(0,\; C_{i-1} - L_i)} \times 100$$

**Streaming update** (per bar, O(1)):

```text
arNum_new = arNum_old - oldest_arNum + (H - O)
arDen_new = arDen_old - oldest_arDen + (O - L)
brNum_new = brNum_old - oldest_brNum + max(0, H - prevClose)
brDen_new = brDen_old - oldest_brDen + max(0, prevClose - L)

AR = (arDen ≠ 0) ? (arNum / arDen) × 100 : 0
BR = (brDen ≠ 0) ? (brNum / brDen) × 100 : 0
```

**Interpretation reference levels:**

- AR > 100, BR > 100: Strong bullish sentiment
- AR < 100, BR < 100: Strong bearish sentiment
- AR and BR divergence: Potential trend reversal signal

**Default parameters:** period = 26 (approximately one trading month).

## Resources

- Japanese Technical Analysis references on AR/BR sentiment indicators
- Taiwan Stock Exchange historical charting methodology
- PineScript reference: [`brar.pine`](brar.pine)
