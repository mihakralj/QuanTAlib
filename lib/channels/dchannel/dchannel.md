# DCHANNEL: Donchian Channels

Donchian Channels track the highest high and lowest low over a fixed lookback period, defining the absolute price boundaries within which an asset has traded. Unlike volatility-based bands that compute statistical dispersion, Donchian Channels represent actual historical extremes — the literal "price box." The implementation uses monotonic deques for $O(1)$ amortized sliding-window max/min, ensuring that computing a 500-period channel costs no more than a 20-period one. The midpoint of the upper and lower bands serves as a simple trend bias indicator.

## Historical Context

Richard Donchian developed this channel in the 1960s while managing one of the first publicly held commodity funds. Known as the "father of trend following," Donchian pioneered systematic trading in an era dominated by discretionary methods. His "4-week rule" (buy on a 20-day high, sell on a 20-day low) became one of the earliest documented mechanical trading systems.

The indicator achieved legendary status through the Turtle Trading experiment in 1983. Richard Dennis and William Eckhardt recruited novice traders and taught them a mechanical system built on channel breakouts. The Turtles reportedly earned over $100 million. Curtis Faith's *Way of the Turtle* (2007) revealed the core system: enter on 20-day breakouts, exit on 10-day counter-breakouts. The simplicity is the feature: no predictions, no fitting, no optimization — just price breaking through defined boundaries.

## Architecture & Physics

### 1. Upper Band (Sliding Window Maximum)

$$\text{Upper}_t = \max_{i=0}^{n-1}(H_{t-i})$$

### 2. Lower Band (Sliding Window Minimum)

$$\text{Lower}_t = \min_{i=0}^{n-1}(L_{t-i})$$

### 3. Middle Band

$$\text{Middle}_t = \frac{\text{Upper}_t + \text{Lower}_t}{2}$$

### 4. Monotonic Deque Algorithm

The naive approach scans the entire window for each bar: $O(n)$ per update. The monotonic deque (also called a sliding window max/min queue) maintains candidates in sorted order:

**For the max deque (upper band):**

1. Remove indices outside the window from the front
2. Remove values $\leq$ current High from the back (they can never be the maximum again)
3. Push current index to the back
4. The front element is always the maximum

Each element enters exactly once and exits at most once, yielding $O(1)$ amortized per bar over any sequence of $N$ updates.

### 5. Stale Extremes

The bands stay flat until either a new extreme occurs or the old extreme exits the window. A band that hasn't moved in 15 bars is waiting for new information. This piecewise-constant behavior is the defining characteristic: unlike smoothed envelopes, Donchian Channels are discontinuous, stepping only at regime transitions.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window for high/low extremes ($n$) | 20 | $> 0$ |

### Pseudo-code

```
function DCHANNEL(high, low, period):
    validate: period > 0

    // Monotonic deque for max (upper band)
    while max_deque.front is outside window: pop front
    while max_deque.back value ≤ high: pop back
    push high to max_deque back
    upper = max_deque.front value

    // Monotonic deque for min (lower band)
    while min_deque.front is outside window: pop front
    while min_deque.back value ≥ low: pop back
    push low to min_deque back
    lower = min_deque.front value

    middle = (upper + lower) / 2

    return [middle, upper, lower]
```

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | Highest high over the lookback (resistance) |
| `lower` | Lowest low over the lookback (support) |
| `middle` | Midpoint of channel (trend bias) |

## Resources

- **Donchian, R.** "High Finance in Copper." *Financial Analysts Journal*, 16(6), 1960. (Original channel concept)
- **Faith, C.** *Way of the Turtle: The Secret Methods that Turned Ordinary People into Legendary Traders*. McGraw-Hill, 2007. (Turtle Trading system)
- **Covel, M.** *The Complete TurtleTrader*. HarperBusiness, 2007.
- **Cormen, T.H. et al.** *Introduction to Algorithms*. MIT Press, 2009. (Monotonic deque / sliding window algorithms)
