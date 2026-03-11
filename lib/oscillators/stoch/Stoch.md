# STOCH: Stochastic Oscillator

> "The Stochastic Oscillator doesn't follow price. It follows the speed, or momentum, of price. Momentum changes direction before price." -- George C. Lane

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Bar series (High, Low, Close) |
| **Parameters** | `kLength` (default 14), `dPeriod` (default 3) |
| **Outputs** | Dual series (%K line, %D signal line) |
| **Output range** | $0$ to $100$ |
| **Warmup** | `kLength` bars |
| **PineScript**   | [stoch.pine](stoch.pine)                       |

### Key takeaways

- Measures where the close sits within the highest-high to lowest-low range, scaled to $[0, 100]$.
- Produces two lines: raw %K (position in range) and %D (SMA of %K, the signal line).
- Uses `MonotonicDeque` pairs for O(1) amortized min/max tracking in streaming mode.
- Zero range (all bars identical) returns $0$ for %K, not $50$ or NaN.
- This is the Fast Stochastic variant. %K is unsmoothed; %D is $\text{SMA}(\%K, d)$.

## Historical Context

George C. Lane developed the Stochastic Oscillator in the late 1950s while working at Investment Educators in Chicago. His core observation was deceptively simple: in uptrends, closing prices tend to cluster near the high of the trading range; in downtrends, they cluster near the low. Quantifying that tendency produces a bounded oscillator that measures momentum rather than price.

Lane was careful to distinguish between Fast and Slow variants. The Fast Stochastic uses the raw %K and its SMA as %D. The Slow Stochastic applies additional smoothing: Slow %K equals Fast %D, and Slow %D is an SMA of Slow %K. This implementation produces the Fast variant. Traders who want Slow Stochastic should wrap the output with an additional SMA pass.

The Stochastic Oscillator and Williams %R share identical mathematics. The only difference is scale: $\text{WillR} = \text{Stoch \%K} - 100$. Lane's version scales $[0, 100]$ with overbought at the top; Williams inverts to $[-100, 0]$. Same information, different packaging.

## What It Measures and Why It Matters

The Stochastic Oscillator measures the closing price's position within the recent high-low range as a percentage. A reading of $100$ means the close equals the highest high over the lookback period. A reading of $0$ means the close equals the lowest low.

The %D signal line smooths %K via a simple moving average, providing crossover signals. When %K crosses above %D, momentum is shifting upward. When %K crosses below %D, momentum is shifting downward. These crossovers are most significant when they occur in overbought ($> 80$) or oversold ($< 20$) territory.

The indicator's real utility is divergence detection. When price makes a new high but %K fails to confirm, buying momentum is weakening. When price makes a new low but %K refuses to follow, selling pressure is exhausting. These divergences often precede reversals by several bars.

## Mathematical Foundation

### Core Formula

$$
HH_n = \max(H_i) \quad \text{for } i \in [t - n + 1, \, t]
$$

$$
LL_n = \min(L_i) \quad \text{for } i \in [t - n + 1, \, t]
$$

$$
\%K_t = 100 \times \frac{C_t - LL_n}{HH_n - LL_n}
$$

$$
\%D_t = \text{SMA}(\%K, d)
$$

where $n$ is `kLength` and $d$ is `dPeriod`.

### Parameter Mapping

| Parameter | Code | Default | Constraints |
|-----------|------|---------|-------------|
| K Length | `kLength` | 14 | `> 0` |
| D Period | `dPeriod` | 3 | `> 0` |

### Warmup Period

$$
W = n
$$

The indicator requires $n$ bars to fill the sliding window for highest-high and lowest-low computation. The %D SMA uses the PineScript convention of pre-filling its buffer with the first %K value, so it produces output from bar 0.

## Architecture & Physics

### 1. MonotonicDeque Streaming

Two `MonotonicDeque` instances provide O(1) amortized min/max tracking:

- **Max deque**: decreasing order of highs; front is always the window maximum.
- **Min deque**: increasing order of lows; front is always the window minimum.
- **Circular buffers** (`_hBuf`, `_lBuf`): store raw H/L values for deque rebuild on bar correction.

### 2. %D Signal Line

A separate circular buffer (`_dBuf`) with running sum computes the SMA of %K in O(1):

- First bar pre-fills the entire buffer with the initial %K value.
- Subsequent bars replace the oldest entry and update the running sum.

### 3. Batch Path

`Batch(ReadOnlySpan, ..., Span, Span, int, int)` delegates to `Highest.Batch()` and `Lowest.Batch()` for vectorized sliding min/max. Intermediate buffers use `stackalloc` for $\leq 256$ elements and `ArrayPool<double>` for larger inputs. The %D SMA uses a local circular buffer.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `kLength <= 0` or `dPeriod <= 0` | `ArgumentException` with `nameof()` |
| `NaN` / `Infinity` input | Substitutes last valid value per channel (H/L/C) |
| All NaN (no valid data yet) | Returns `NaN` for both %K and %D |
| Zero range ($HH = LL$) | %K returns $0$ |
| `isNew = false` | Restores `_ps`, rebuilds both deques from circular buffer |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Overbought | `%K > 80` | Close near period high; potential reversal down |
| Neutral | `20 ≤ %K ≤ 80` | Normal trading range |
| Oversold | `%K < 20` | Close near period low; potential reversal up |

### Signal Patterns

- **%K/%D crossover**: Bullish when %K crosses above %D; bearish when %K crosses below %D. Most reliable in overbought/oversold zones.
- **Divergence**: Price makes new highs while %K does not (bearish) or price makes new lows while %K does not (bullish).
- **Failure swing**: %K reaches an extreme, pulls back, fails to re-reach the extreme, then reverses.
- **Hook**: Short-term reversal when %K or %D hooks at an extreme without completing a full crossover.

### Practical Notes

- In strong trends, %K stays overbought or oversold for extended periods. Fading the trend on %K readings alone produces consistent losses.
- The %D crossover is a lagging signal by design (it's an SMA). Use it for confirmation, not anticipation.
- Fast Stochastic is noisier than Slow Stochastic. If whipsaws are a problem, either increase `kLength` or apply additional smoothing.

## Related Indicators

- [**Willr**](../willr/Willr.md): Identical math with inverted $[-100, 0]$ scale; $\text{WillR} = \text{\%K} - 100$.
- [**Stochf**](../stochf/Stochf.md): Fast Stochastic variant (may differ in %D handling).
- [**KDJ**](../kdj/Kdj.md): Extended stochastic with J-line divergence amplification.
- [**SMI**](../smi/Smi.md): Stochastic Momentum Index, measures distance from range midpoint rather than boundary.

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Skender | ✅ | `GetStoch(kLength, dPeriod, smoothPeriods=1)` matches within `1e-6` after warmup |
| TA-Lib | -- | Not directly validated (separate Stochf tests) |
| Tulip | -- | Not directly validated |
| Ooples | -- | Not validated |

## Performance Profile

### Key Optimizations

- **O(1) amortized streaming**: `MonotonicDeque` avoids full-window scans for min/max on each bar.
- **O(1) %D SMA**: Circular buffer with running sum eliminates iteration over the %D window.
- **Zero allocation**: `Update` uses pre-allocated circular buffers and `record struct State`.
- **Stackalloc/ArrayPool batch**: Intermediate buffers use `stackalloc` for $\leq 256$ elements, `ArrayPool` beyond.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| Comparisons | 2-3 (deque push amortized) |
| Divisions | 2 (range normalization + %D SMA) |
| Multiplications | 1 (`100 *`) |
| Additions/Subtractions | 2 (%D running sum update) |
| NaN checks | 3 (high, low, close) |
| **Total** | **~10 ops** |

### SIMD Analysis (Batch Mode)

| Property | Value |
|----------|-------|
| Vectorizable | Partially (via `Highest.Batch` / `Lowest.Batch`) |
| %K final loop | Scalar: `100 * (close[i] - LL[i]) / (HH[i] - LL[i])` |
| %D computation | Scalar circular buffer with running sum |

## Common Pitfalls

1. **Fast vs Slow confusion**: This implementation outputs Fast Stochastic. Many platforms default to Slow Stochastic, which smooths %K before computing %D. Direct comparison will not match without setting `smoothPeriods=1`.
2. **Zero range returns 0**: When all bars in the window share the same high and low, %K returns $0$. Williams %R returns $-50$ for the same condition. The choice is arbitrary but not interchangeable.
3. **Overbought does not equal sell**: In trending markets, %K stays overbought/oversold for extended periods. Counter-trend trades based solely on Stochastic readings produce drawdowns.
4. **Short lookback noise**: `kLength < 5` creates excessive whipsaws. The default 14 balances responsiveness and noise rejection.
5. **%D warmup convention**: The first %D value pre-fills the SMA buffer with the initial %K, matching PineScript behavior. Other implementations may use NaN until `dPeriod` bars of %K are available.
6. **Bar correction cost**: Correcting a bar (`isNew=false`) triggers O(kLength) deque rebuild. Infrequent in normal streaming but visible when batch-correcting thousands of bars.

## FAQ

**Q: What is the difference between Fast and Slow Stochastic?**
A: Fast Stochastic uses raw %K and SMA(%K) as %D. Slow Stochastic sets Slow %K = Fast %D, then Slow %D = SMA(Slow %K). This implementation is Fast Stochastic. Apply an additional SMA to the output for Slow.

**Q: Why does zero range return 0 instead of 50?**
A: Convention. When the range is zero, the close equals both the high and the low, so the "position in range" is undefined. Returning $0$ matches the PineScript and Skender conventions. Williams %R returns $-50$ for the same condition.

**Q: How does Stoch relate to Williams %R?**
A: They are the same formula with different scales. $\text{WillR} = \text{\%K} - 100$. Stoch scales $[0, 100]$; WillR scales $[-100, 0]$.

## References

- Lane, G. C. "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*, 1984.
- Murphy, J. J. *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
- Appel, G.; Hitschler, F. *Stock Market Trading Systems*. Dow Jones-Irwin, 1980.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
