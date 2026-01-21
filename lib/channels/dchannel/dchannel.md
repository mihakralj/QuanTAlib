# DC: Donchian Channels

> "The Turtles didn't need complex math. They needed to know when price broke out of its cage."

Donchian Channels (DC) track the highest high and lowest low over a lookback period, creating a price envelope that defines where the market has been. Unlike volatility-based bands (Bollinger, Keltner), Donchian uses actual price extremes—no standard deviations, no averages of true range. The result: bands that represent real support and resistance levels traders actually watch. This implementation uses monotonic deques for O(1) amortized updates rather than the naive O(n) rescan that plagues most implementations.

## Historical Context

Richard Donchian developed these channels in the 1960s while managing one of the first publicly held commodity funds. His "4-week rule" (buy on 20-day high, sell on 20-day low) became the foundation for systematic trend-following.

The indicator gained fame through the Turtle Trading experiment in 1983. Richard Dennis and William Eckhardt recruited novice traders and taught them a mechanical system built on Donchian Channel breakouts. The Turtles reportedly made over $100 million. Curtis Faith's book and subsequent leaks revealed the core: enter on 20-day breakouts, exit on 10-day counter-breakouts.

Most implementations compute max/min by scanning the entire lookback window on every bar—O(n) per update, O(n²) for a series. This works for period=20 but becomes painful for longer windows or real-time feeds. QuanTAlib uses monotonic deques that maintain running max/min in O(1) amortized time, enabling period=500+ without performance degradation.

## Architecture & Physics

Donchian Channels consist of three components: upper band (highest high), lower band (lowest low), and middle band (their average).

### 1. Upper Band (Highest High)

Tracks the maximum high price over the lookback window:

$$
U_t = \max_{i=0}^{n-1}(H_{t-i})
$$

where $H$ is the high price and $n$ is the period. The upper band moves up immediately when a new high occurs, but only drops when the previous highest high exits the lookback window.

### 2. Lower Band (Lowest Low)

Tracks the minimum low price over the lookback window:

$$
L_t = \min_{i=0}^{n-1}(L_{t-i})
$$

where $L$ is the low price. The lower band drops immediately on new lows but only rises when the previous lowest low exits the window.

### 3. Middle Band

The arithmetic mean of the upper and lower bands:

$$
M_t = \frac{U_t + L_t}{2}
$$

This represents the "equilibrium" price over the lookback period—not a moving average of closes, but the center of the price range.

## Mathematical Foundation

### Monotonic Deque Algorithm

Instead of rescanning the window on each bar, the implementation maintains two monotonic deques:

**For maximum (upper band):**

1. Remove elements from the back that are smaller than the new value
2. Add the new value with its index to the back
3. Remove elements from the front whose indices are outside the window
4. The front element is always the maximum

**For minimum (lower band):**

1. Remove elements from the back that are larger than the new value
2. Add the new value with its index to the back
3. Remove elements from the front whose indices are outside the window
4. The front element is always the minimum

**Amortized Analysis:**

Each element is added once and removed at most once. Over $n$ operations, total work is $O(n)$, giving $O(1)$ amortized per update.

### Channel Width

The distance between bands measures price range volatility:

$$
W_t = U_t - L_t
$$

Wider channels indicate higher volatility; narrower channels suggest consolidation.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar cost using monotonic deque optimization:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP | 4 | 1 | 4 |
| ADD | 1 | 1 | 1 |
| MUL | 1 | 3 | 3 |
| **Total** | **6** | — | **~8 cycles** |

**Complexity**: O(1) amortized per bar—monotonic deque maintains max/min efficiently.

### Batch Mode (512 values, SIMD/FMA)

Finding max/min over sliding windows has limited SIMD benefit due to sequential dependency:

| Operation | Scalar Ops | SIMD Benefit | Notes |
| :--- | :---: | :---: | :--- |
| Max/Min update | 4 | 1× | Deque-based, sequential |
| Middle band | 2 | 2× | ADD + MUL parallelizable |

**Batch efficiency (512 bars):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 8 | 4,096 | — |
| Partial SIMD | ~7 | ~3,584 | **~12%** |

Donchian Channels are already highly efficient due to the O(1) monotonic deque algorithm.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact max/min calculation |
| **Timeliness** | 6/10 | Tracks past extremes, inherently lagging |
| **Overshoot** | 10/10 | No overshoot—bands are actual price levels |
| **Smoothness** | 5/10 | Bands move in discrete steps as extremes exit window |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Exact match for upper/lower bands |
| **Skender** | ✅ | Exact match within floating-point tolerance |
| **Tulip** | ✅ | Exact match |
| **Ooples** | ✅ | Exact match |

## Common Pitfalls

1. **Stale Extremes**: Donchian bands stay flat until a new extreme occurs or the old extreme exits the window. A band that hasn't moved in 15 bars isn't broken—it's waiting. Traders sometimes mistake this for indicator malfunction.

2. **O(n) Trap**: Naive implementations rescan the full window every bar. For period=200 on tick data (60,000 bars/day), that's 12 million comparisons daily per symbol. The monotonic deque approach reduces this to ~120,000.

3. **Breakout vs. Touch**: Price touching the upper band is not the same as breaking out. True breakouts close above/below the band. Intrabar spikes that don't close outside the channel often fail.

4. **Asymmetric Exit**: The Turtle system used 20-day entry but 10-day exit. Using the same period for both typically underperforms. Consider different periods for entries and exits.

5. **Choppy Markets**: Donchian Channels generate frequent false signals during sideways consolidation. The bands narrow, making breakouts more likely, but these breakouts often fail. Filter with trend confirmation or volatility thresholds.

6. **Gap Behavior**: Overnight gaps can create instant breakouts that reverse quickly. The band immediately adjusts to include the gap, which may not represent sustainable price levels.

7. **Memory Footprint**: The monotonic deque implementation requires storing (value, index) pairs. For period=200, this means up to 400 doubles (3.2 KB) per instance. For 5,000 symbols, budget ~16 MB.

## References

- Donchian, R. (1960). "High Finance in Copper." *Financial Analysts Journal*, 16(6), 133-142.
- Faith, C. (2007). *Way of the Turtle: The Secret Methods that Turned Ordinary People into Legendary Traders*. McGraw-Hill.
- Schwager, J. D. (1989). *Market Wizards: Interviews with Top Traders*. Harper & Row.
- Covel, M. (2007). *The Complete TurtleTrader*. HarperBusiness.
