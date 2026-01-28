# MFI: Money Flow Index

> "Volume confirms price, but money flow confirms intent." — Gene Quong & Avrum Soudack

Money Flow Index is the volume-weighted cousin of RSI. While RSI measures the momentum of price changes alone, MFI incorporates volume to determine whether the price movement has conviction behind it. The result is an oscillator that can identify when strong hands are accumulating or distributing.

The innovation of MFI is answering not just "Is price going up?" but "Is significant money pushing price up?" A stock rising on thin volume produces a different MFI reading than one rising on heavy institutional participation.

## Historical Context

Developed by Gene Quong and Avrum Soudack, MFI was introduced as "volume-weighted RSI" to address a fundamental limitation of price-only momentum indicators. RSI treats a 1% move on 100 shares the same as a 1% move on 10 million shares—MFI does not.

The indicator gained popularity because it:
- Incorporates volume into momentum analysis
- Identifies divergences earlier than pure price indicators
- Provides bounded readings (0-100) for consistent interpretation

Traditional interpretation uses:
- MFI > 80: Overbought (potential distribution)
- MFI < 20: Oversold (potential accumulation)
- Divergences: Price makes new high but MFI fails to confirm

## Architecture & Physics

MFI operates on the concept of "money flow"—the product of typical price and volume. By comparing periods where typical price rises (positive money flow) versus falls (negative money flow), MFI measures the balance of buying and selling pressure over a rolling window.

The key insight is **directional volume weighting**. When typical price increases, all volume for that bar is considered "positive money flow." When typical price decreases, all volume becomes "negative money flow." The ratio of these accumulated flows produces the final oscillator value.

### Component Breakdown

1. **Typical Price (TP)**: (High + Low + Close) / 3
2. **Raw Money Flow (RMF)**: TP × Volume
3. **Positive Money Flow**: Sum of RMF when TP increases
4. **Negative Money Flow**: Sum of RMF when TP decreases
5. **Money Flow Ratio**: Positive MF / Negative MF
6. **MFI**: 100 - (100 / (1 + Ratio))

## Mathematical Foundation

### 1. Typical Price

$$
TP_t = \frac{High_t + Low_t + Close_t}{3}
$$

### 2. Raw Money Flow

$$
RMF_t = TP_t \times Volume_t
$$

### 3. Directional Money Flow

$$
PMF_t = \begin{cases}
RMF_t & \text{if } TP_t > TP_{t-1} \\
0 & \text{otherwise}
\end{cases}
$$

$$
NMF_t = \begin{cases}
RMF_t & \text{if } TP_t < TP_{t-1} \\
0 & \text{otherwise}
\end{cases}
$$

Note: When $TP_t = TP_{t-1}$, both PMF and NMF are zero (neutral).

### 4. Money Flow Ratio

$$
MFR_t = \frac{\sum_{i=t-n+1}^{t} PMF_i}{\sum_{i=t-n+1}^{t} NMF_i}
$$

where n is the lookback period (default: 14).

### 5. Money Flow Index

$$
MFI_t = 100 - \frac{100}{1 + MFR_t}
$$

Edge cases:
- If $\sum NMF = 0$ and $\sum PMF > 0$: MFI = 100 (all positive flow)
- If $\sum PMF = 0$ and $\sum NMF > 0$: MFI = 0 (all negative flow)
- If both sums are zero: MFI = 50 (neutral)

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD | 5 | TP calc, rolling sum updates |
| DIV | 3 | TP, ratio, final MFI |
| MUL | 1 | RMF calculation |
| CMP | 2 | TP comparison for direction |
| **Total** | ~11 | Per bar |

### Batch Mode (SIMD)

The TP and RMF calculations are fully vectorizable. The directional classification and rolling sums require sequential processing but maintain O(n) complexity overall.

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Throughput** | 9 | O(1) per bar after warmup |
| **Allocations** | 0 | Two RingBuffers allocated once |
| **Complexity** | O(1) | Rolling sums, not recomputation |
| **Accuracy** | 10 | Matches TA-Lib and Skender |
| **Timeliness** | 8 | Period-bar lag inherent |
| **Overshoot** | 10 | Bounded [0, 100] by construction |
| **Smoothness** | 6 | Smoother than RSI due to volume weighting |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **QuanTAlib** | ✅ | Validated |
| **TA-Lib** | ✅ | Matches `MFI` function exactly |
| **Skender** | ✅ | Matches `GetMfi` exactly |
| **Tulip** | ✅ | Matches implementation |
| **Ooples** | ✅ | Matches implementation |

## Common Pitfalls

1. **Requires OHLCV Data**: Unlike RSI which works on any price series, MFI requires full bar data (High, Low, Close, Volume). The `Update(TValue)` method throws `NotSupportedException`.

2. **Warmup Period**: MFI needs `period` bars before the rolling sums represent a full window. Before that, calculations use available data but may be less stable.

3. **Zero Volume Handling**: Bars with zero volume contribute nothing to money flow. This is mathematically correct but can produce unexpected readings in illiquid markets.

4. **Flat Typical Price**: When consecutive bars have identical typical prices, neither positive nor negative flow accumulates. Extended flat periods push MFI toward 50.

5. **Volume Data Quality**: MFI is only as good as the volume data. Markets with unreliable volume reporting (some crypto exchanges, certain after-hours sessions) can produce misleading MFI readings.

6. **isNew Parameter**: When correcting a bar (isNew=false), the implementation properly rolls back state. Failure to handle this causes cumulative errors in rolling sums.

7. **NaN/Infinity Handling**: Invalid volume values are substituted with the last valid volume to prevent propagation of invalid values through the calculation.

## References

- Quong, G. & Soudack, A. (1989). "Money Flow Index." *Technical Analysis of Stocks & Commodities*.
- Investopedia. "Money Flow Index (MFI)." [Definition](https://www.investopedia.com/terms/m/mfi.asp)
- StockCharts. "Money Flow Index (MFI)." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:money_flow_index_mfi)