# PCHANNEL: Price Channel

Price Channel tracks the highest high and lowest low over a lookback period with a midpoint average, creating a three-line price envelope that defines where the market has been. Functionally identical to Donchian Channels, the indicator uses actual price extremes rather than volatility estimates, producing bands that represent real support and resistance levels. This implementation uses monotonic deques for O(1) amortized updates instead of the naive O(n) rescan that most platforms use internally.

## Historical Context

Price Channel is the generic name for what Richard Donchian formalized in the 1960s while managing one of the first publicly held commodity funds. The indicator appears under various aliases: Donchian Channels, N-period high/low channels, or breakout bands.

The "4-week rule" (buy on 20-day high, sell on 20-day low) became the foundation for systematic trend-following. The indicator gained fame through the Turtle Trading experiment in 1983, when Richard Dennis and William Eckhardt recruited novice traders and taught them a mechanical system built on channel breakouts. The Turtles reportedly earned over \$100 million using entry signals on 20-day breakouts with exits on 10-day counter-breakouts.

Most implementations compute max/min by scanning the entire lookback window on every bar: $O(n)$ per update, $O(n^2)$ for a series. This works for period 20 but becomes costly for longer windows or real-time feeds. The monotonic deque approach maintains running max/min in $O(1)$ amortized time, enabling period 500+ without performance degradation.

## Architecture & Physics

### 1. Upper Band (Highest High)

Tracks the maximum high price over the lookback window using a decreasing monotonic deque:

$$
U_t = \max_{i=0}^{n-1} H_{t-i}
$$

where $H$ is the high price and $n$ is the period. The upper band moves up immediately on a new high but only drops when the previous highest high exits the lookback window.

### 2. Lower Band (Lowest Low)

Tracks the minimum low price using an increasing monotonic deque:

$$
L_t = \min_{i=0}^{n-1} L_{t-i}
$$

The lower band drops immediately on new lows but only rises when the previous lowest low exits the window.

### 3. Middle Band

The arithmetic mean of the upper and lower bands:

$$
M_t = \frac{U_t + L_t}{2}
$$

This represents the equilibrium price of the lookback window. Unlike MMCHANNEL which omits the midpoint, PCHANNEL always emits all three lines.

### 4. Monotonic Deque Mechanism

Two deques maintain sorted order without explicit sorting:

- **Max deque:** stores indices in decreasing value order; front is always the maximum.
- **Min deque:** stores indices in increasing value order; front is always the minimum.

On each bar: (1) expire stale front indices outside the window, (2) remove back elements superseded by the new value, (3) push the new index to the back.

### 5. Complexity

Streaming: $O(1)$ amortized per bar. Each element enters and exits each deque at most once. Memory: two circular buffers of $n$ floats plus two deques of at most $n$ indices.

## Mathematical Foundation

### Parameters

| Symbol | Name | Constraint | Description |
|--------|------|------------|-------------|
| $n$ | period | $> 0$ | Lookback window size |

### Pseudo-code

```
function pchannel(high[], low[], period):
    max_deque = empty      // decreasing monotonic deque of indices
    min_deque = empty      // increasing monotonic deque of indices
    hbuf = circular_buffer(period)
    lbuf = circular_buffer(period)

    for each bar t:
        hbuf[t mod period] = high[t]
        lbuf[t mod period] = low[t]

        // expire stale front entries
        while max_deque not empty AND max_deque.front <= t - period:
            max_deque.pop_front()
        while min_deque not empty AND min_deque.front <= t - period:
            min_deque.pop_front()

        // remove dominated back entries
        while max_deque not empty AND hbuf[max_deque.back mod period] <= high[t]:
            max_deque.pop_back()
        while min_deque not empty AND lbuf[min_deque.back mod period] >= low[t]:
            min_deque.pop_back()

        max_deque.push_back(t)
        min_deque.push_back(t)

        upper = hbuf[max_deque.front mod period]
        lower = lbuf[min_deque.front mod period]
        middle = (upper + lower) / 2

        emit (upper, middle, lower)
```

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Price closes above $U_t$ | Breakout signal (Turtle entry) |
| Price closes below $L_t$ | Breakdown signal |
| $M_t$ rising | Upward drift in the price range |
| $U_t - L_t$ contracting | Consolidation; range tightening |
| $U_t - L_t$ expanding | Volatility expansion |

## Resources

- Donchian, R. (1960). "High Finance in Copper." *Financial Analysts Journal*.
- Faith, C. (2007). *Way of the Turtle: The Secret Methods that Turned Ordinary People into Legendary Traders*. McGraw-Hill.
