# Aroon

> Price levels are irrelevant. The only thing that matters is *when* they happened. Aroon is a stopwatch for trends.

The Aroon indicator measures the temporal freshness of price extremes. Unlike oscillators that obsess over *how much* price has moved, Aroon asks *how long* it has been since a new high or low. It quantifies the "staleness" of a trend, providing an early warning system for consolidation and reversals.

## The 1995 Innovation

Tushar Chande introduced Aroon in *Beyond Technical Analysis* (1995). The name comes from the Sanskrit word for "Dawn's Early Light." Chande's insight was that trends don't just stop; they age. By measuring the time elapsed since the last extreme, Aroon attempts to spot the "dawn" of a new trend rather than just confirming an existing one.

## Architecture & Physics

Aroon is purely time-based. It normalizes the "days since" metric into a 0-100 oscillator.

1. **Time Tracking**: We maintain a sliding window of the last $N$ bars.
2. **Extremum Search**: We locate the index of the highest high and lowest low within that window.
3. **Normalization**: We convert the distance (in bars) into a percentage.

### The Logic of Freshness

- **Aroon Up**: Quantifies the recency of the High.
  - 100: New high today.
  - 0: No new high for the entire period.
- **Aroon Down**: Quantifies the recency of the Low.
  - 100: New low today.
  - 0: No new low for the entire period.
- **Oscillator**: The net difference ($Up - Down$), showing the dominant temporal force.

### Zero-Allocation Design

The implementation is optimized for minimal memory footprint.

- **Storage**: We use two `RingBuffer` instances to store Highs and Lows.
- **Search**: The search for min/max is performed via a linear scan of the internal buffer.
- **Allocations**: The `Update` cycle is strictly zero-allocation.

## Mathematical Foundation

The math is a linear decay function based on time.

$$
\text{Aroon Up} = \frac{Period - \text{Days Since High}}{Period} \times 100
$$

$$
\text{Aroon Down} = \frac{Period - \text{Days Since Low}}{Period} \times 100
$$

$$
\text{Oscillator} = \text{Aroon Up} - \text{Aroon Down}
$$

## Performance Profile

While memory is O(P), computational complexity is linear with respect to the period due to the min/max search.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~10ns / bar | Scales linearly with Period ($O(P)$) |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(P) | Requires scanning the buffer for extremes |
| **Memory** | O(P) | Stores `Period + 1` samples of High and Low |

*Note: For standard periods (14-50), the linear scan is negligible. For massive periods (>1000), the O(P) cost becomes measurable.*

## Validation

We validate against standard reference implementations.

- **Buffer Sizing**: We use `Period + 1` to correctly handle the inclusive range.
- **Tie-Breaking**: If multiple bars share the same extreme value, we use the *most recent* one (yielding a higher Aroon score).

### Common Pitfalls

- **Single Value Updates**: If you feed Aroon only `Close` prices (instead of High/Low), it degrades into a "Time Since Highest Close" metric. It works, but it loses the nuance of intraday extremes.
- **The 70/30 Rule**: A common interpretation is that a trend is strong only if the primary line is > 70. Values between 30 and 70 often indicate noise or consolidation.
