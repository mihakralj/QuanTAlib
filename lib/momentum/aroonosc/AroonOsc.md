# AroonOsc: Aroon Oscillator

> Tushar Chande's Aroon system is a dual-line argument. The Oscillator is the verdict.

The Aroon Oscillator condenses the struggle between the "Aroon Up" and "Aroon Down" lines into a single, normalized value. It quantifies not just the existence of a trend, but its freshness. It answers the question: "Are new highs appearing faster than new lows?"

## Historical Context

Introduced by Tushar Chande in *The New Technical Trader* (1995), the Aroon system was a departure from price-based momentum. It focused on *time*. While RSI asks "how much did price move?", Aroon asks "how long has it been since the last extreme?". The Oscillator is simply the arithmetic difference between the two, providing a zero-centered metric for trend bias.

## Architecture & Physics

The physics of Aroon are temporal, not spatial. It measures the decay of "recency."

1. **Time Measurement**: The bars since the highest high and lowest low within the period are counted.
2. **Normalization**: These counts are converted to a 0-100 scale (100 = happened right now, 0 = happened `Period` bars ago).
3. **Differential**: The Oscillator is `Up - Down`.

### The Drift Resistance

Unlike recursive indicators (EMA, RSI) which accumulate floating-point errors over time, Aroon is stateless in the long term. Its value depends *only* on the data within the lookback window. This makes it mathematically robust and immune to "poisoning" from bad data in the distant past.

## Mathematical Foundation

The math is purely arithmetic.

### 1. Aroon Up

$$ \text{AroonUp} = \frac{\text{Period} - \text{Days Since High}}{\text{Period}} \times 100 $$

### 2. Aroon Down

$$ \text{AroonDown} = \frac{\text{Period} - \text{Days Since Low}}{\text{Period}} \times 100 $$

### 3. The Oscillator

$$ \text{AroonOsc} = \text{AroonUp} - \text{AroonDown} $$

## Performance Profile

The algorithm is $O(N)$ where $N$ is the period, as the window must be scanned for extremes. However, for typical periods (14-25), this is negligible.

### Zero-Allocation Design

The implementation uses a circular buffer (`RingBuffer`) to store historical highs and lows, ensuring O(1) access and zero heap allocations during the update cycle. The min/max search is performed in-place on the buffer.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10ns | 10ns / bar. |
| **Allocations** | 0 | Hot path is allocation-free. |
| **Complexity** | O(P) | Linear scan of the lookback window. |
| **Accuracy** | 10/10 | Matches standard implementations. |
| **Timeliness** | 10/10 | Reacts immediately to new extremes. |
| **Overshoot** | 0/10 | Bounded -100 to +100. |
| **Smoothness** | 2/10 | Step-function behavior. |

## Validation

Validation is performed against industry-standard libraries.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Skender** | ✅ | Matches `GetAroon` (Oscillator). |
| **TA-Lib** | ✅ | Matches `TA_AROONOSC`. |
| **Tulip** | ✅ | Matches `ti.aroonosc`. |
| **Ooples** | ❌ | Deviates significantly from standard. |

### Common Pitfalls

- **Lag**: Because it looks back `Period` bars, it will not signal a reversal until the previous extreme "ages out" or is superseded. It is a lagging indicator of trend changes.
- **Flatlining**: In strong trends, the oscillator can peg at +100 or -100 for extended periods. This is a feature, not a bug—it indicates a "fresh" extreme on every bar.
