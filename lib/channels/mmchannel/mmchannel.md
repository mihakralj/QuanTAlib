# MMCHANNEL: Min-Max Channel

Min-Max Channel tracks the highest high and lowest low over a lookback period, creating a pure price envelope without any midpoint calculation. Unlike Donchian Channels which include a middle band, MMCHANNEL delivers only the raw extremes. The implementation uses monotonic deques for O(1) amortized updates: each element enters the deque once and leaves at most once, so total work over $N$ bars is $O(N)$ regardless of period length.

## Historical Context

Min-max channels are the simplest form of price envelope analysis, predating most technical indicators. The concept is elemental: track where price has been at its highest and lowest points over a defined window.

The approach gained prominence through Richard Donchian's commodity trading work in the 1960s and later through the Turtle Trading system in 1983. Curtis Faith's public disclosure of the Turtle rules revealed that a 20-day breakout channel formed the core entry signal. While Donchian Channels add a midpoint average, MMCHANNEL strips this away, focusing purely on the support and resistance levels defined by actual price extremes.

Most naive implementations suffer from $O(n)$ complexity per update, rescanning the entire window to locate max/min values. For period 200 on tick data, that means 200 comparisons per tick. The monotonic deque approach maintains sorted order implicitly, reducing amortized cost to $O(1)$ per bar.

## Architecture & Physics

### 1. Upper Band (Sliding Window Maximum)

The upper band tracks the maximum high price over the lookback window using a decreasing monotonic deque:

$$
U_t = \max_{i=0}^{n-1} H_{t-i}
$$

where $H$ is the high price and $n$ is the period. New highs immediately update the upper band; the band only decreases when the previous maximum exits the lookback window.

### 2. Lower Band (Sliding Window Minimum)

The lower band tracks the minimum low price using an increasing monotonic deque:

$$
L_t = \min_{i=0}^{n-1} L_{t-i}
$$

where $L$ is the low price. New lows immediately update the lower band; the band only increases when the previous minimum exits the window.

### 3. Monotonic Deque Invariants

The maximum deque stores (value, index) pairs in decreasing order by value; the front element is always the current maximum. The minimum deque stores pairs in increasing order; the front element is always the current minimum. No explicit sorting is needed because superseded elements are removed on insertion.

### 4. No Middle Band

Unlike DCHANNEL and PCHANNEL, MMCHANNEL emits only upper and lower bands. If a midpoint is needed, compute $(U_t + L_t) / 2$ externally.

### 5. Complexity

Streaming: $O(1)$ amortized per bar (each element enters/exits the deque at most once). Worst case $O(n)$ occurs only on monotonically increasing/decreasing sequences that flush the entire deque. Memory: two deques of at most $n$ (value, index) pairs plus two circular buffers of $n$ floats.

## Mathematical Foundation

### Parameters

| Symbol | Name | Constraint | Description |
|--------|------|------------|-------------|
| $n$ | period | $> 0$ | Lookback window size |

### Monotonic Deque Algorithm

For the **maximum** (upper band), on each new bar with high value $h$:

```
push h into circular buffer at (bar_index mod period)

// expire stale front
while deque not empty AND front index <= bar_index - period:
    remove front

// remove dominated back elements
while deque not empty AND buffer[back index mod period] <= h:
    remove back

push (bar_index) to back
upper = buffer[front index mod period]
```

For the **minimum** (lower band), the same structure with $\geq$ replacing $\leq$ in the back-removal step.

### Amortized Analysis

Each element is pushed to the deque exactly once and popped at most once (either from the back during insertion or from the front during expiry). Over $N$ operations, total work is $O(N)$, yielding $O(1)$ amortized cost per update.

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| $U_t$ rising | New highs being set within the window |
| $U_t$ flat | No new high; previous extreme still in window |
| $L_t$ falling | New lows being set within the window |
| $U_t - L_t$ contracting | Consolidation; range tightening |
| $U_t - L_t$ expanding | Volatility expansion; breakout potential |

## Resources

- Donchian, R. (1960). "High Finance in Copper." *Financial Analysts Journal*, 16(6).
- Faith, C. (2007). *Way of the Turtle*. McGraw-Hill.
- Cormen, T. et al. (2009). *Introduction to Algorithms*, 3rd ed. MIT Press. (Monotonic deque analysis)
