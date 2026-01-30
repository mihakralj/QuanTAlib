# VO: Volume Oscillator

> "Volume tells us the conviction behind price moves—the oscillator reveals when that conviction is accelerating or fading."

The Volume Oscillator (VO) measures the difference between two moving averages of volume, expressed as a percentage. It helps identify changes in volume trends and potential momentum shifts by comparing short-term volume activity against longer-term volume norms.

## Historical Context

Volume analysis has been a cornerstone of technical analysis since the early 20th century. Charles Dow emphasized volume as a key confirmation tool for price movements. The Volume Oscillator emerged as traders sought a normalized way to compare volume across different timeframes, similar to how price oscillators like MACD compare price moving averages.

The indicator gained popularity because raw volume numbers vary dramatically across securities and time periods. By expressing the difference between volume averages as a percentage, VO provides a consistent scale for comparison regardless of the underlying security's typical trading volume.

## Architecture & Physics

### 1. Short-Term Volume SMA

The short-term simple moving average captures recent volume activity:

$$
\text{ShortMA}_t = \frac{1}{n_s} \sum_{i=0}^{n_s-1} V_{t-i}
$$

where $n_s$ is the short period (default: 5) and $V$ is volume.

### 2. Long-Term Volume SMA

The long-term simple moving average establishes the volume baseline:

$$
\text{LongMA}_t = \frac{1}{n_l} \sum_{i=0}^{n_l-1} V_{t-i}
$$

where $n_l$ is the long period (default: 10).

### 3. Volume Oscillator Calculation

The oscillator expresses the difference as a percentage:

$$
\text{VO}_t = \frac{\text{ShortMA}_t - \text{LongMA}_t}{\text{LongMA}_t} \times 100
$$

This normalization allows:
- Positive values when short-term volume exceeds long-term average
- Negative values when short-term volume is below long-term average
- Comparable readings across different securities

### 4. Signal Line

An optional signal line smooths the VO for trend identification:

$$
\text{Signal}_t = \frac{1}{n_{sig}} \sum_{i=0}^{n_{sig}-1} \text{VO}_{t-i}
$$

where $n_{sig}$ is the signal period (default: 10).

## Mathematical Foundation

### Running Sum Implementation

For O(1) updates, we maintain running sums rather than recalculating:

$$
\text{Sum}_t = \text{Sum}_{t-1} - V_{t-n} + V_t
$$

where $V_{t-n}$ is the oldest value being removed from the window.

### Division Safety

To prevent division by zero:

$$
\text{VO}_t = \begin{cases}
\frac{\text{ShortMA}_t - \text{LongMA}_t}{\text{LongMA}_t} \times 100 & \text{if } \text{LongMA}_t > 0 \\
0 & \text{otherwise}
\end{cases}
$$

### Period Constraint

The short period must be strictly less than the long period:

$$
n_s < n_l
$$

This ensures the indicator measures the relationship between recent and historical volume, not vice versa.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 6 | 1 | 6 |
| MUL | 1 | 3 | 3 |
| DIV | 3 | 15 | 45 |
| CMP/MOD | 6 | 1 | 6 |
| **Total** | **16** | — | **~60 cycles** |

The running sum approach eliminates the need to iterate over the entire window each update.

### Memory Footprint

Per instance:
- Short buffer: $n_s \times 8$ bytes
- Long buffer: $n_l \times 8$ bytes  
- Signal buffer: $n_{sig} \times 8$ bytes
- State: ~128 bytes

With defaults (5, 10, 10): ~328 bytes per instance.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact SMA calculation |
| **Timeliness** | 7/10 | Inherent SMA lag |
| **Overshoot** | 8/10 | Bounded by percentage scale |
| **Smoothness** | 7/10 | Depends on periods chosen |

## Interpretation

### Signal Reading

| VO Value | Interpretation |
| :--- | :--- |
| **> 0** | Short-term volume above average (accumulation/distribution) |
| **< 0** | Short-term volume below average (consolidation) |
| **Rising** | Volume momentum increasing |
| **Falling** | Volume momentum decreasing |

### Trading Applications

1. **Trend Confirmation**: Rising VO during price uptrends confirms bullish momentum
2. **Divergence**: Price making new highs while VO declining suggests weakening trend
3. **Signal Crossovers**: VO crossing above signal line suggests volume momentum shift
4. **Zero-Line Crossings**: VO crossing above zero indicates short-term volume exceeding long-term average

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation |

## Common Pitfalls

1. **Period Selection**: Short period too close to long period produces noisy signals. Recommend at least 2:1 ratio (e.g., 5 and 10, or 12 and 26).

2. **Zero Volume Handling**: Securities with occasional zero volume bars can distort calculations. Implementation uses minimum volume of 1.0 to avoid division issues.

3. **Warmup Period**: Full accuracy requires at least `longPeriod` bars. Before warmup, results use partial window averages.

4. **Percentage Interpretation**: VO of +20% means short-term volume is 20% above long-term average, not that volume increased by 20%.

5. **Signal Line Lag**: The signal line adds additional smoothing delay. For faster signals, reduce signal period or use VO directly.

6. **Bar Correction**: When using `isNew=false`, all three SMA buffers must be restored for accurate recalculation.

## References

- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Achelis, S. B. (2001). *Technical Analysis from A to Z*. McGraw-Hill.
- PineScript Reference: vo.pine