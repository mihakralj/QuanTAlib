# CFB: Jurik Composite Fractal Behavior

> Mark Jurik's CFB is not a momentum indicator. It is a stopwatch for chaos.

The Jurik Composite Fractal Behavior (CFB) index measures the duration of a trend by analyzing the "fractal efficiency" of price movement across multiple time scales. It answers the question: "How long has the market been moving in a straight line?"

Most indicators assume a fixed period (e.g., RSI-14). CFB rejects this rigidity. It scans a massive array of lookback periods simultaneously (by default, from 2 to 192 bars) to find which timeframes are exhibiting efficient trending behavior. It then composites these valid timeframes into a single index representing the current trend's maturity.

## The Jurik Standard

Mark Jurik is the quiet giant of signal processing in finance. His work focuses on low-lag, adaptive algorithms that treat price series as noisy signals rather than accounting ledgers. CFB is designed to be a "modulator"—a signal used to tune other indicators.

## Architecture & Physics

CFB is a massive parallel processor. It doesn't just look at one timeframe; it looks at *all* of them.

1. **Fractal Efficiency**: For every length $L$ in the scan set, the ratio of net price movement to total path length (volatility) is calculated.
2. **Filtering**: Any timeframe where the efficiency is below a threshold (0.25) is discarded. This filters out "meandering" or choppy periods.
3. **Compositing**: A weighted average of the qualifying lengths is taken, with the efficiency ratio itself used as the weight.
4. **Decay**: If no timeframes qualify, the index decays exponentially, reflecting the loss of trend memory.

### The Computational Challenge

A naive implementation of CFB is $O(N \times M)$, where $M$ is the number of lengths scanned (often ~100). This is prohibitively slow for real-time systems.

The QuanTAlib implementation uses a **running-sum algorithm** to maintain $O(1)$ complexity per update. Ninety-six parallel running sums of volatility are maintained, updating incrementally as new bars arrive and old bars drop off.

## Mathematical Foundation

The core concept is the Fractal Efficiency Ratio.

### 1. Efficiency Ratio ($R_L$)

For each length $L$:
$$
R_L = \frac{|P_t - P_{t-L}|}{\sum_{i=0}^{L-1} |P_{t-i} - P_{t-i-1}|}
$$

### 2. Weighting ($w_L$)

$$
w_L = \begin{cases} R_L & \text{if } R_L \ge 0.25 \\ 0 & \text{if } R_L < 0.25 \end{cases}
$$

### 3. Composite Index

$$
CFB = \frac{\sum (L \times w_L)}{\sum w_L}
$$

### 4. Decay

If $\sum w_L \le 0.25$:
$$
CFB_t = \max(1, CFB_{t-1} \times 0.5)
$$

## Performance Profile

Memory is traded for speed. The state object is large (~2KB), but the update loop is extremely fast due to the running-sum optimization.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~50ns / bar | Updates 96 parallel sums per bar |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(1) | Constant time relative to history length |
| **Precision** | `double` | Essential for accurate efficiency ratios |

## Validation

Validation is performed against **Jurik's published methodology**.

- **Adaptivity**: The index correctly identifies trend duration in synthetic geometric brownian motion tests.
- **Decay**: The exponential decay logic ensures the indicator resets quickly when a trend breaks.

### Common Pitfalls

- **Not a Directional Signal**: CFB tells you *how long* a trend has lasted, not which way it is going. A high CFB can occur in a crash or a rally.
- **Modulation**: Its best use is to dynamically adjust the period of other indicators (e.g., `RSI(Period = CFB)`). Using it as a standalone crossover signal is usually a mistake.
