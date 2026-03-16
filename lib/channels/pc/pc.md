# PC: Price Channel

> *Price channels frame the trading range by its own high-low extremes, defining the field of play.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [pc.pine](pc.pine)                       |

- Price Channel tracks the highest high and lowest low over a lookback period with a midpoint average, creating a three-line price envelope that defines where the market has been.
- **Similar:** [DC](../dc/dc.md), [MMChannel](../mmchannel/mmchannel.md) | **Complementary:** Volume confirmation on breakouts | **Trading note:** Price channel based on percentage offset from midpoint.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

This represents the equilibrium price of the lookback window. Unlike MMCHANNEL which omits the midpoint, PC always emits all three lines.

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

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Price closes above $U_t$ | Breakout signal (Turtle entry) |
| Price closes below $L_t$ | Breakdown signal |
| $M_t$ rising | Upward drift in the price range |
| $U_t - L_t$ contracting | Consolidation; range tightening |
| $U_t - L_t$ expanding | Volatility expansion |

## Performance Profile

### Operation Count (Streaming Mode)

PC uses two monotonic deques for $O(1)$ amortized sliding-window max/min plus a midpoint:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (expire stale front, max deque) | 1 | 1 | 1 |
| CMP (remove dominated back, max deque) | ~1 avg | 1 | 1 |
| CMP (expire stale front, min deque) | 1 | 1 | 1 |
| CMP (remove dominated back, min deque) | ~1 avg | 1 | 1 |
| ADD (upper + lower) | 1 | 1 | 1 |
| MUL (× 0.5 for middle) | 1 | 3 | 3 |
| **Total (amortized)** | **~6** | — | **~8 cycles** |

Identical to DC in cost. Each element enters and exits each deque exactly once over the full series, yielding $O(N)$ total work across $N$ bars regardless of period.

### Batch Mode (SIMD Analysis)

Monotonic deques are inherently sequential. No SIMD parallelization across bars is possible:

| Optimization | Benefit |
| :--- | :--- |
| Deque operations | Sequential; amortized O(1) already optimal |
| Midpoint computation | Vectorizable in a post-pass with `Vector<double>` |
| Memory layout | Two circular buffers + two deques; cache-friendly |

## Resources

- Donchian, R. (1960). "High Finance in Copper." *Financial Analysts Journal*.
- Faith, C. (2007). *Way of the Turtle: The Secret Methods that Turned Ordinary People into Legendary Traders*. McGraw-Hill.
