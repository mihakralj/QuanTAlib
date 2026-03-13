# RVI: Relative Volatility Index

> *Not all volatility is created equal—upward volatility feels like profit, downward volatility feels like loss. RVI separates these psychological experiences into a quantifiable measure.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar) or single price (TValue) |
| **Parameters**   | `stdevLength` (default 10), `rmaLength` (default 14)                      |
| **Outputs**      | Single series (Rvi)                       |
| **Output range** | $0$ to $100$                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [rvi.pine](rvi.pine)                       |

- The Relative Volatility Index (RVI) implements Dorsey's **revised (1995)** version: computes original RVI separately on High and Low series, then averages.
- When fed single-price data (TValue), both channels receive the same value, reducing to the original (1993) formula.
- **Similar:** [HV](../hv/hv.md), [ATR](../atr/atr.md) | **Complementary:** Bollinger Bands | **Trading note:** Relative Volatility Index; direction of volatility.
- Validated against FM Labs revised RVI specification.

The Relative Volatility Index (RVI) is a directional volatility oscillator that distinguishes between upward and downward price volatility. Originally developed by Donald Dorsey in 1993 using close prices only, RVI was **revised in 1995** to compute separate RVI values on the High and Low price series and average them. This implementation follows the revised version: when fed OHLCV bars (TBar), it runs independent RVI channels on High and Low; when fed single prices (TValue), both channels receive the same value, reducing to the original formula.

## Historical Context

Donald Dorsey introduced the Relative Volatility Index in the June 1993 issue of *Technical Analysis of Stocks & Commodities* magazine. Dorsey designed RVI as a confirmation indicator rather than a standalone signal generator, intending it to be used alongside RSI to confirm trend strength and momentum.

The key innovation was separating volatility into directional components. Traditional volatility measures (standard deviation, ATR) treat upward and downward price movements identically. Dorsey recognized that traders experience these movements differently: upward volatility in a long position feels like opportunity, while downward volatility feels like risk.

The original 1993 formula used a 10-period standard deviation of closing prices with 14-period Wilder's smoothing (RMA). In 1995, Dorsey revised the formula to average RVI computed independently on the High and Low series, capturing volatility structure across the full price range rather than just closes.

FM Labs documents both versions: the original (close-only) and the revised (high+low average). This implementation follows the **revised** version with bias-corrected RMA for proper warmup behavior.

## Architecture & Physics

### 1. Dual-Channel Architecture (Revised 1995)

The revised RVI computes the original RVI algorithm independently on two channels:
- **High channel:** uses bar High prices
- **Low channel:** uses bar Low prices

The final RVI is their average:

$$
\text{RVI}_{\text{revised}} = \frac{\text{RVI}_{\text{high}} + \text{RVI}_{\text{low}}}{2}
$$

When fed single prices (TValue), both channels receive the same value: $\text{RVI} = \frac{\text{RVI}_p + \text{RVI}_p}{2} = \text{RVI}_p$ (original behavior).

### 2. Per-Channel: Rolling Population Standard Deviation

For each channel, compute the population standard deviation over `stdevLength` periods:

$$
\sigma_t = \sqrt{\frac{\sum_{i=0}^{n-1}(P_{t-i} - \bar{P})^2}{n}}
$$

where:

- $P_t$ = Closing price at time $t$
- $\bar{P}$ = Mean of prices in the window
- $n$ = `stdevLength` (default 10)

The computational form uses running sums for O(1) updates:

$$
\sigma_t = \sqrt{\frac{\sum P_i^2}{n} - \left(\frac{\sum P_i}{n}\right)^2}
$$

### 3. Directional Classification

Based on price change direction, assign the volatility to either upward or downward:

$$
\text{upStd}_t = \begin{cases}
\sigma_t & \text{if } P_t > P_{t-1} \\
0 & \text{otherwise}
\end{cases}
$$

$$
\text{downStd}_t = \begin{cases}
\sigma_t & \text{if } P_t < P_{t-1} \\
0 & \text{otherwise}
\end{cases}
$$

Note: When $P_t = P_{t-1}$ (unchanged), both upStd and downStd are zero. The volatility is "orphaned" rather than assigned to either direction.

### 4. Bias-Corrected RMA Smoothing

Both directional volatilities are smoothed using Wilder's RMA (Exponential Moving Average with $\alpha = 1/n$) with bias correction for proper warmup:

**Raw RMA update:**

$$
\text{raw}_t = \frac{\text{raw}_{t-1} \cdot (n-1) + x_t}{n}
$$

**Bias correction factor:**

$$
e_t = (1 - \alpha) \cdot e_{t-1}
$$

**Corrected output:**

$$
\text{avgStd}_t = \begin{cases}
\frac{\text{raw}_t}{1 - e_t} & \text{if } e_t > \epsilon \\
\text{raw}_t & \text{otherwise}
\end{cases}
$$

where $\alpha = 1/\text{rmaLength}$ and $\epsilon = 10^{-10}$.

This bias correction compensates for the zero initialization of raw RMA, preventing artificially low values during warmup.

### 5. Per-Channel RVI

$$
\text{RVI}_{\text{channel}} = \begin{cases}
100 \times \frac{\text{avgUpStd}_t}{\text{avgUpStd}_t + \text{avgDownStd}_t} & \text{if sum} > 0 \\
50 & \text{otherwise}
\end{cases}
$$

### 6. Final Revised RVI

$$
\text{RVI}_t = \frac{\text{RVI}_{\text{high}} + \text{RVI}_{\text{low}}}{2}
$$

## Mathematical Foundation

### Relationship to RSI

RVI shares structural similarity with RSI, but measures different quantities:

| Aspect | RSI | RVI |
| :--- | :--- | :--- |
| **Measures** | Price changes | Price volatility |
| **Up component** | Positive price change | Stddev when price rises |
| **Down component** | Negative price change | Stddev when price falls |
| **Formula** | $100 \times \frac{\text{avgGain}}{\text{avgGain} + \text{avgLoss}}$ | $100 \times \frac{\text{avgUpStd}}{\text{avgUpStd} + \text{avgDownStd}}$ |
| **Range** | 0-100 | 0-100 |

Both use RMA smoothing for the components.

### Bias Correction Derivation

Standard RMA initialized to zero produces biased estimates during warmup. After $t$ updates:

$$
\text{bias} = (1 - \alpha)^t
$$

The correction factor $1/(1-e_t)$ cancels this bias, ensuring unbiased estimates from the first bar.

### Interpretation Zones

| RVI Range | Interpretation |
| :---: | :--- |
| 80-100 | Strong upward volatility dominance |
| 60-80 | Moderate upward volatility |
| 40-60 | Neutral/balanced volatility |
| 20-40 | Moderate downward volatility |
| 0-20 | Strong downward volatility dominance |

### Warmup Period

RVI requires `stdevLength` bars for the standard deviation calculation plus additional bars for RMA convergence. Effective warmup:

$$
\text{warmup} \approx \text{stdevLength} + 3 \times \text{rmaLength}
$$

With defaults (10, 14): approximately 52 bars for 95% convergence.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations after warmup:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 8 | 1 | 8 |
| MUL | 6 | 3 | 18 |
| DIV | 5 | 15 | 75 |
| SQRT | 1 | 15 | 15 |
| CMP | 3 | 1 | 3 |
| **Total** | — | — | **~119 cycles** |

Dominant cost: five divisions (63%) for variance calculation and RMA updates.

### Memory Profile

- **State struct:** ~88 bytes (stddev buffer, RMA states, counters)
- **RingBuffer:** 8 bytes × stdevLength (default 80 bytes)
- **100 instances @ defaults:** ~16.8 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Precise directional volatility measurement |
| **Timeliness** | 7/10 | RMA smoothing introduces lag |
| **Responsiveness** | 7/10 | Responds to volatility regime changes |
| **Smoothness** | 8/10 | Double smoothing (stddev + RMA) |
| **Interpretability** | 9/10 | Clear 0-100 scale with intuitive meaning |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **FM Labs** | ✅ | Matches revised (1995) dual-channel specification |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | ❔ | Different algorithm (RSI-based) |
| **PineScript** | ✅ | Matches rvi.pine reference (original algorithm per channel) |

Note: Some libraries implement "RVI" as a different indicator (often RSI applied to volatility). FM Labs distinguishes between original (1993, close-only) and revised (1995, high+low average). This implementation follows the **revised** version.

## Common Pitfalls

1. **Confusion with other RVI indicators**: "RVI" name is used for at least three different indicators. Dorsey's original (this implementation) uses directional standard deviation. Others use RSI-like calculations on price or volume. Verify the algorithm before comparing values.

2. **Unchanged price handling**: When price doesn't change ($P_t = P_{t-1}$), the volatility is orphaned (neither up nor down). Extended flat periods push RVI toward 50 regardless of prior trend.

3. **Warmup period**: With defaults, RVI needs ~52 bars to converge. Early values may be misleading. The `IsHot` property indicates warmup completion.

4. **Not a standalone signal**: Dorsey designed RVI as a confirmation indicator. Use with RSI or trend indicators, not alone. High RVI confirms uptrend strength; low RVI confirms downtrend strength.

5. **Volatility vs direction confusion**: RVI measures which direction has MORE volatility, not which direction price is moving. A slow steady uptrend with occasional sharp drops can show low RVI despite rising prices.

6. **Parameter sensitivity**: Shorter stdevLength increases noise sensitivity. Longer rmaLength increases lag. Default 10/14 balances responsiveness and stability.

7. **Zero denominator**: When both avgUpStd and avgDownStd approach zero (flat market), RVI defaults to 50. This is mathematically correct but may mask the lack of volatility.

## Trading Applications

### Trend Confirmation (Dorsey's Original Use)

Combine with RSI for confirmation:

```
RSI > 50 AND RVI > 50: Confirmed uptrend
RSI < 50 AND RVI < 50: Confirmed downtrend
RSI > 50 AND RVI < 50: Divergence - uptrend weakening
RSI < 50 AND RVI > 50: Divergence - downtrend weakening
```

### Volatility Regime Detection

Track RVI for directional volatility shifts:

```
RVI crossing above 60: Upward volatility expanding
RVI crossing below 40: Downward volatility expanding
RVI oscillating 40-60: Balanced/consolidating
```

### Entry/Exit Filters

Use RVI as a filter for other signals:

```
Only take long entries when RVI > 50 (upward volatility dominance)
Only take short entries when RVI < 50 (downward volatility dominance)
```

### Divergence Trading

RVI divergences from price can signal reversals:

```
Price making higher highs + RVI making lower highs: Bearish divergence
Price making lower lows + RVI making higher lows: Bullish divergence
```

## References

- Dorsey, D. (1993). "The Relative Volatility Index." *Technical Analysis of Stocks & Commodities*, 11(6), 253-256.
- Dorsey, D. (1995). "Refining the Relative Volatility Index." *Technical Analysis of Stocks & Commodities*, 13(9).
- FM Labs. "Relative Volatility Index." https://www.fmlabs.com/reference/RVI.htm (Original vs Revised versions).
- TradingView. (2024). "PineScript Reference Implementation." rvi.pine source file.