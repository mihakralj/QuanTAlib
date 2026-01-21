# MMCHANNEL: Min-Max Channel

> "The market's true range isn't about averages. It's about extremes—and who's winning."

Min-Max Channel (MMCHANNEL) tracks the highest high and lowest low over a lookback period, creating a pure price envelope without any midpoint calculation. Unlike Donchian Channels which include a middle band, MMCHANNEL delivers only the raw extremes—exactly what breakout traders and range analysis need. This implementation uses monotonic deques for O(1) amortized updates, making it suitable for high-frequency applications and long lookback periods.

## Historical Context

Min-Max channels represent the simplest form of price envelope analysis, predating most technical indicators. The concept is intuitive: track where price has been at its highest and lowest points over a defined period.

The approach gained prominence through Richard Donchian's work in the 1960s and later through the Turtle Trading system. While Donchian Channels include a midpoint average, MMCHANNEL strips this away, focusing purely on support and resistance levels defined by actual price extremes.

Most implementations suffer from O(n) complexity per update—scanning the entire window to find max/min values. For period=200 on tick data, this means 200 comparisons per tick. QuanTAlib uses monotonic deques that maintain sorted order implicitly, achieving O(1) amortized updates regardless of period length.

## Architecture & Physics

MMCHANNEL consists of two components: the upper band (highest high) and lower band (lowest low).

### 1. Upper Band (Highest High)

Tracks the maximum high price over the lookback window using a decreasing monotonic deque:

$$
U_t = \max_{i=0}^{n-1}(H_{t-i})
$$

where $H$ is the high price and $n$ is the period. New highs immediately update the upper band; the band only decreases when the previous maximum exits the lookback window.

**Monotonic deque invariant:** Elements are stored in decreasing order by value. The front element is always the maximum.

### 2. Lower Band (Lowest Low)

Tracks the minimum low price over the lookback window using an increasing monotonic deque:

$$
L_t = \min_{i=0}^{n-1}(L_{t-i})
$$

where $L$ is the low price. New lows immediately update the lower band; the band only increases when the previous minimum exits the window.

**Monotonic deque invariant:** Elements are stored in increasing order by value. The front element is always the minimum.

## Mathematical Foundation

### Monotonic Deque Algorithm

The key insight is maintaining sorted order without explicit sorting:

**For maximum (upper band):**

1. **Back removal:** Remove elements from the back that are ≤ the new value
2. **Insert:** Add the new (value, index) pair to the back
3. **Front expiry:** Remove elements from the front whose indices are outside the window
4. **Query:** The front element is always the maximum

**For minimum (lower band):**

1. **Back removal:** Remove elements from the back that are ≥ the new value
2. **Insert:** Add the new (value, index) pair to the back
3. **Front expiry:** Remove elements from the front whose indices are outside the window
4. **Query:** The front element is always the minimum

**Amortized Analysis:**

Each element enters the deque exactly once and leaves at most once (either from the back during insertion or from the front during expiry). Over $n$ operations, total work is $O(n)$, yielding $O(1)$ amortized per update.

### Channel Width

The distance between bands measures the price range:

$$
W_t = U_t - L_t
$$

Channel width indicates volatility: wider channels suggest larger price swings; narrower channels indicate consolidation.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar cost using monotonic deque optimization:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (deque maintenance) | ~4 | 1 | ~4 |
| Memory access (deque) | ~4 | 3 | ~12 |
| **Total** | **~8** | — | **~16 cycles** |

**Complexity:** O(1) amortized per bar. Worst case O(n) occurs only when a monotonically increasing (for max) or decreasing (for min) sequence forces clearing the entire deque—rare in practice.

### Batch Mode (512 values, SIMD/FMA)

Sliding window max/min has limited SIMD benefit due to sequential dependency in deque operations:

| Operation | Scalar Ops | SIMD Benefit | Notes |
| :--- | :---: | :---: | :--- |
| Deque update | ~8 | 1× | Sequential by nature |
| Index comparison | 2 | 2× | SIMD possible for batch |

**Batch efficiency (512 bars):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 16 | 8,192 | — |
| Partial SIMD | ~14 | ~7,168 | **~12%** |

The monotonic deque algorithm is already highly efficient; SIMD provides marginal gains.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact max/min calculation |
| **Timeliness** | 6/10 | Tracks past extremes, inherently lagging |
| **Overshoot** | 10/10 | No overshoot—bands are actual price levels |
| **Smoothness** | 4/10 | Bands move in discrete steps as extremes exit window |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Dchannel** | ✅ | Exact match for upper/lower bands |
| **Skender** | ✅ | Exact match via Donchian upper/lower |
| **TA-Lib** | ✅ | Exact match via MAX/MIN functions |
| **Tulip** | ✅ | Exact match via max/min functions |

## Common Pitfalls

1. **Stale Extremes:** The bands stay flat until a new extreme occurs or the old extreme exits the window. A band that hasn't moved in 15 bars isn't broken—it's waiting for price to exceed the current extreme or for that extreme to age out.

2. **O(n) Implementation Trap:** Naive implementations rescan the window every bar. For period=200 on 60,000 bars/day, that's 12 million comparisons per symbol. The monotonic deque approach reduces this to ~120,000 operations.

3. **Breakout vs. Touch:** Price touching the upper band differs from breaking out. True breakouts require closes above/below the band. Intrabar spikes that don't close outside the channel often reverse.

4. **No Middle Band:** Unlike Donchian Channels, MMCHANNEL has no middle line. If you need a centerline, use Donchian or compute `(Upper + Lower) / 2` separately.

5. **Asymmetric Movement:** Upper and lower bands move independently. The upper band can rise while the lower band stays flat (or vice versa) depending on where extremes occur in the lookback window.

6. **Gap Handling:** Overnight gaps immediately adjust the relevant band. A gap up extends the upper band; a gap down extends the lower band. These may not represent sustainable price levels.

7. **Memory Footprint:** The monotonic deque stores (value, index) pairs. Worst case is `2 * period` pairs per deque (monotonically decreasing high prices and monotonically increasing low prices). For period=200, budget ~6.4 KB per instance. For 5,000 symbols, ~32 MB total.

8. **Bar Correction:** When `isNew=false`, the indicator must restore prior state before computing. The implementation maintains `_p_state` for this purpose. Failing to handle bar correction causes incorrect extremes when bars update intrabar.

## References

- Donchian, R. (1960). "High Finance in Copper." *Financial Analysts Journal*, 16(6), 133-142.
- Faith, C. (2007). *Way of the Turtle: The Secret Methods that Turned Ordinary People into Legendary Traders*. McGraw-Hill.
- Cormen, T. H., et al. (2009). *Introduction to Algorithms*, 3rd ed. MIT Press. (Monotonic deque analysis)
