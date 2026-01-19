# Aroon

> Price levels are irrelevant. The only thing that matters is *when* they happened. Aroon is a stopwatch for trends.

The Aroon indicator measures the temporal freshness of price extremes. Unlike oscillators that obsess over *how much* price has moved, Aroon asks *how long* it has been since a new high or low. It quantifies the "staleness" of a trend, providing an early warning system for consolidation and reversals.

## Historical Context

Tushar Chande introduced Aroon in *Beyond Technical Analysis* (1995). The name comes from the Sanskrit word for "Dawn's Early Light." Chande's insight was that trends don't just stop; they age. By measuring the time elapsed since the last extreme, Aroon attempts to spot the "dawn" of a new trend rather than just confirming an existing one.

## Architecture & Physics

Aroon is purely time-based. It normalizes the "days since" metric into a 0-100 oscillator.

1. **Time Tracking**: A sliding window of the last $N$ bars is maintained.
2. **Extremum Search**: The index of the highest high and lowest low within that window is located.
3. **Normalization**: The distance (in bars) is converted into a percentage.

### The Logic of Freshness

* **Aroon Up**: Quantifies the recency of the High.
  * 100: New high today.
  * 0: No new high for the entire period.
* **Aroon Down**: Quantifies the recency of the Low.
  * 100: New low today.
  * 0: No new low for the entire period.
* **Oscillator**: The net difference ($Up - Down$), showing the dominant temporal force.

## Mathematical Foundation

The math is a linear decay function based on time.

$$ \text{Aroon Up} = \frac{Period - \text{Days Since High}}{Period} \times 100 $$

$$ \text{Aroon Down} = \frac{Period - \text{Days Since Low}}{Period} \times 100 $$

$$ \text{Oscillator} = \text{Aroon Up} - \text{Aroon Down} $$

## Performance Profile

While memory is O(P), computational complexity is linear with respect to the period due to the min/max search.

### Zero-Allocation Design

The implementation uses a circular buffer (`RingBuffer`) to store historical highs and lows, ensuring O(1) access and zero heap allocations during the update cycle. The min/max search is performed in-place on the buffer.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10ns | 10ns / bar. |
| **Allocations** | 0 | Hot path is allocation-free. |
| **Complexity** | O(P) | Linear scan for extremes. |
| **Accuracy** | 10/10 | Matches standard implementations. |
| **Timeliness** | 10/10 | Reacts immediately to new extremes. |
| **Overshoot** | 0/10 | Bounded 0-100. |
| **Smoothness** | 2/10 | Step-function behavior. |

## Validation

Validation is performed against industry-standard libraries.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Skender** | ✅ | Matches `GetAroon`. |
| **TA-Lib** | ✅ | Matches `TA_AROON` and `TA_AROONOSC`. |
| **Tulip** | ✅ | Matches `ti.aroon` and `ti.aroonosc`. |

| **Ooples** | N/A | Not implemented. |

### Common Pitfalls

* **Single Value Updates**: If you feed Aroon only `Close` prices (instead of High/Low), it degrades into a "Time Since Highest Close" metric. It works, but it loses the nuance of intraday extremes.
* **The 70/30 Rule**: A common interpretation is that a trend is strong only if the primary line is > 70. Values between 30 and 70 often indicate noise or consolidation.
