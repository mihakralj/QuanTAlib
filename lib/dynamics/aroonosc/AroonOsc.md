# AROONOSC: Aroon Oscillator

The Aroon Oscillator condenses the dual-line Aroon system into a single zero-centered value by computing $\text{AroonUp} - \text{AroonDown}$. This distills the temporal battle between fresh highs and fresh lows into a bounded $[-100, +100]$ metric where positive values indicate bullish recency dominance and negative values indicate bearish. Unlike recursive indicators that accumulate floating-point drift, the Aroon Oscillator is purely windowed — its value depends only on data within the lookback period, making it stateless in the long term and immune to initialization poisoning. The step-function output reflects discrete events (new extremes appearing or aging out) rather than smooth price trajectories.

## Historical Context

Tushar Chande introduced the Aroon system in *The New Technical Trader* (1995) as a departure from price-magnitude momentum. While RSI and MACD ask "how much did price move?", Aroon asks "how long has it been since the last extreme?" The Oscillator is the net verdict of this temporal argument. Chande's key observation was that the recency of extremes carries more information about trend health than the magnitude of movements. A market making new highs every few bars is trending up regardless of the size of each increment. The Oscillator pegs at +100 when a new high appears on every bar within the window (maximum bullish freshness), and at -100 when new lows dominate. The middle ground (values near zero) indicates neither extreme is particularly fresh — the temporal signature of consolidation.

## Architecture & Physics

### 1. Sliding Window Buffers

Two ring buffers of size $N+1$ store the last $N+1$ bars of High and Low values.

### 2. Extremum Location

On each bar, scan the buffers to locate the index of the highest high and the lowest low.

### 3. Aroon Components

$$\text{AroonUp} = \frac{N - \text{barsSinceHigh}}{N} \times 100$$

$$\text{AroonDown} = \frac{N - \text{barsSinceLow}}{N} \times 100$$

### 4. Oscillator

$$\text{AroonOsc} = \text{AroonUp} - \text{AroonDown}$$

### 5. Complexity

- **Time:** $O(N)$ per bar for min/max scanning
- **Space:** $O(N)$ — two ring buffers
- **Warmup:** $N$ bars to fill the window

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 25 | $N \geq 1$ |

### Pseudo-code

```
Initialize:
  highBuf = RingBuffer(period + 1)
  lowBuf = RingBuffer(period + 1)
  bar_count = 0

On each bar (high, low, isNew):
  if !isNew: restore previous state

  highBuf.Add(high)
  lowBuf.Add(low)
  bar_count++

  len = min(bar_count, period)

  // Scan for extremes
  maxIdx = index of maximum in highBuf over last (len + 1) entries
  minIdx = index of minimum in lowBuf over last (len + 1) entries

  barsSinceHigh = len - maxIdx
  barsSinceLow = len - minIdx

  AroonUp = (len - barsSinceHigh) / len × 100
  AroonDown = (len - barsSinceLow) / len × 100

  output = AroonUp - AroonDown
```

### Drift Immunity

Unlike EMA-based oscillators that accumulate rounding errors across thousands of bars, the Aroon Oscillator is computed fresh each bar from a finite window. There is no recursive state that can diverge. This makes it architecturally robust for long-running production systems where indicator drift is a concern.

### Interpretation

| AroonOsc Value | Meaning |
|----------------|---------|
| +100 | New high every bar; no new lows (maximum bullish) |
| +50 to +100 | Strong bullish bias; highs are fresh |
| 0 | Balanced; both extremes equally stale/fresh |
| -50 to -100 | Strong bearish bias; lows are fresh |
| -100 | New low every bar; no new highs (maximum bearish) |

### Flatlining Behavior

In strong trends, the oscillator can hold +100 or -100 for sustained periods. This indicates a continuously refreshing extreme — the market is making a new high (or low) on virtually every bar. This is not saturation; it is the temporal signature of a parabolic move.

## Performance Profile

### Operation Count (Streaming Mode)

AroonOsc = Aroon Up − Aroon Down, computed via the same deque-based window extremum tracking.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Aroon Up + Down computation | 1 | ~26 | 26 |
| SUB (AroonUp − AroonDown) | 1 | 1 | 1 |
| **Total** | **Aroon+1** | — | **~27 cycles** |

AroonOsc is essentially free on top of Aroon. ~27 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Aroon pipeline | Partial | See Aroon profile |
| Subtraction | Yes | VSUBPD once both Aroon arrays exist |

Trivially parallelizable subtraction step after Aroon computation.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic; integer positions |
| **Timeliness** | 8/10 | Crossover signals arrive with N/2 average lag |
| **Smoothness** | 5/10 | Oscillator can swing sharply as extremes roll through the window |
| **Noise Rejection** | 5/10 | No smoothing; single-bar outliers shift the signal |

## Resources

- Chande, T.S. — *The New Technical Trader* (John Wiley & Sons, 1995)
- Chande, T.S. — *Beyond Technical Analysis* (John Wiley & Sons, 1995)
- PineScript reference: `aroonosc.pine` in indicator directory
