# III: Intraday Intensity Index

> "Where the close lands within the day's range tells you who won the battle—bulls or bears. Volume tells you how hard they fought."

The Intraday Intensity Index (III) measures buying and selling pressure by analyzing where the close price falls within the high-low range, weighted by volume. Originally developed by David Bostian, this indicator quantifies whether money is flowing into or out of a security on an intraday basis. Values range from -1 (close at low, maximum selling pressure) to +1 (close at high, maximum buying pressure), multiplied by volume for magnitude.

## Historical Context

David Bostian developed the Intraday Intensity Index in the 1980s as a way to measure money flow within the trading day. The concept builds on the intuition that the closing price's position within the day's range reveals whether buyers or sellers controlled the session.

Unlike indicators that only look at price direction or volume alone, III combines both: a close near the high with heavy volume suggests strong accumulation, while a close near the low with heavy volume indicates distribution. This makes III particularly useful for confirming price trends and identifying potential reversals through divergences.

## Architecture & Physics

The indicator operates in two modes: smoothed (default) and cumulative.

### 1. Position Multiplier

The core calculation determines where the close falls within the high-low range:

$$
PM_t = \begin{cases}
\frac{2 \times C_t - H_t - L_t}{H_t - L_t} & \text{if } H_t \neq L_t \\
0 & \text{if } H_t = L_t
\end{cases}
$$

The position multiplier ranges from:
- **+1**: Close equals High (maximum bullish)
- **0**: Close at midpoint (neutral)
- **-1**: Close equals Low (maximum bearish)

### 2. Raw Intensity

The raw III value multiplies position by volume:

$$
III_{raw,t} = PM_t \times V_t
$$

This weights the directional signal by the conviction behind it (volume).

### 3. Smoothing / Accumulation

In **smoothed mode** (default), a Simple Moving Average is applied:

$$
III_t = \frac{1}{n} \sum_{i=0}^{n-1} III_{raw,t-i}
$$

In **cumulative mode**, values are accumulated over time:

$$
III_{cum,t} = \sum_{i=0}^{t} III_{raw,i}
$$

## Mathematical Foundation

### Position Multiplier Derivation

The formula $(2C - H - L) / (H - L)$ can be rewritten as:

$$
PM = \frac{(C - L) - (H - C)}{H - L} = \frac{2(C - M)}{H - L}
$$

where $M = (H + L) / 2$ is the midpoint. This shows that PM measures how far the close deviates from the midpoint, normalized by the range.

### Interpretation

- **PM > 0**: Close above midpoint → buyers dominated
- **PM < 0**: Close below midpoint → sellers dominated
- **PM × V**: Large volume amplifies the signal

### Smoothed vs Cumulative

- **Smoothed**: Shows recent average buying/selling pressure; oscillates around zero
- **Cumulative**: Shows cumulative money flow over time; trends with price

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB | 3 | 1 | 3 |
| MUL | 1 | 3 | 3 |
| DIV | 1 | 15 | 15 |
| CMP | 1 | 1 | 1 |
| Ring buffer update | 1 | ~5 | 5 |
| **Total** | **7** | — | **~27 cycles** |

The algorithm is simple and efficient with O(1) streaming complexity.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Mathematically exact |
| **Timeliness** | 7/10 | SMA smoothing adds lag |
| **Simplicity** | 9/10 | Intuitive formula |
| **Usefulness** | 8/10 | Good for divergence analysis |

## Validation

III is a relatively uncommon indicator in mainstream libraries, but the algorithm is straightforward.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation |

Validation focuses on internal consistency (streaming vs batch vs span modes).

## Common Pitfalls

1. **Warmup Period**: The indicator requires `period` bars before the SMA is fully primed. Before warmup, values are averaged over available data.

2. **Zero Range**: When High equals Low (flat bars), the position multiplier is undefined. The implementation returns 0 in this case.

3. **Volume Importance**: III is volume-weighted, so low-volume bars contribute less. Zero volume is treated as minimum value of 1 to avoid division issues.

4. **Cumulative vs Smoothed**: Cumulative mode creates a trending line that can grow unbounded; smoothed mode oscillates. Choose based on use case.

5. **Scale Dependency**: Raw III values scale with volume, making cross-security comparison difficult without normalization.

6. **Bar Corrections**: The `isNew=false` parameter allows updating the current bar. State rollback restores the previous state before recalculating.

## References

- Bostian, D. "Intraday Intensity Index." *Technical Analysis of Stocks & Commodities*.
- Arms, R. W. (1989). "The Arms Index (TRIN)." *Dow Jones-Irwin*.