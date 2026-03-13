# AROON: Aroon Indicator

> *Aroon measures how recently the highest high and lowest low occurred — recency as a proxy for trend vitality.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Multiple series (Up, Down)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [aroon.pine](aroon.pine)                       |

- The Aroon indicator measures the temporal freshness of price extremes, answering not "how much did price move?" but "how long ago did it make a new...
- **Similar:** [AroonOsc](../aroonosc/AroonOsc.md), [ADX](../adx/Adx.md) | **Complementary:** Volume for confirmation | **Trading note:** Aroon Up/Down measures time since highest high or lowest low; 100 = new high/low this period.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Aroon indicator measures the temporal freshness of price extremes, answering not "how much did price move?" but "how long ago did it make a new high or low?" Aroon Up tracks the recency of the highest high within the lookback window; Aroon Down tracks the recency of the lowest low. Both are normalized to 0-100 where 100 means the extreme occurred on the current bar and 0 means it occurred at the far edge of the window. A companion Aroon Oscillator (Up minus Down) provides a single zero-centered metric for trend bias. Unlike recursive indicators that accumulate floating-point drift, Aroon is purely windowed — its value depends only on data within the lookback period, making it immune to initialization artifacts.

## Historical Context

Tushar Chande introduced Aroon in *Beyond Technical Analysis* (1995). The name comes from the Sanskrit word for "Dawn's Early Light," reflecting the indicator's purpose: to spot the dawn of a new trend rather than merely confirm an existing one. Chande's insight was that trends do not simply stop; they age. A trend that has not made a new high in 20 of the last 25 bars is statistically moribund, regardless of how strong the original breakout was. The temporal perspective inverts the usual analysis framework: instead of asking whether price is above or below some average, Aroon asks whether the market is still making progress in a given direction. This makes it particularly effective at identifying the transition zone between trending and ranging regimes.

## Architecture & Physics

### 1. Sliding Window

A circular buffer of size $N+1$ stores the last $N+1$ bars of High and Low values (the current bar plus $N$ historical bars).

### 2. Extremum Search

On each bar, the buffer is scanned to find the index of the highest high and the index of the lowest low within the window.

### 3. Aroon Up

$$\text{AroonUp} = \frac{N - \text{barsSinceHigh}}{N} \times 100$$

where barsSinceHigh is the number of bars elapsed since the highest high.

### 4. Aroon Down

$$\text{AroonDown} = \frac{N - \text{barsSinceLow}}{N} \times 100$$

### 5. Aroon Oscillator

$$\text{AroonOsc} = \text{AroonUp} - \text{AroonDown}$$

Range: $[-100, +100]$.

### 6. Complexity

- **Time:** $O(N)$ per bar for the min/max linear scan (monotonic deque optimization possible for amortized $O(1)$)
- **Space:** $O(N)$ — ring buffers for High and Low
- **Warmup:** $N$ bars to fill the window

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 25 | $N \geq 1$ |

### Interpretation

| Condition | Signal |
|-----------|--------|
| AroonUp > 70, AroonDown < 30 | Strong uptrend (recent highs, stale lows) |
| AroonDown > 70, AroonUp < 30 | Strong downtrend (recent lows, stale highs) |
| Both > 70 | Volatile; both extremes are fresh |
| Both < 30 | Consolidation; both extremes are stale |
| AroonOsc > 0 | Bullish bias |
| AroonOsc < 0 | Bearish bias |

### Step-Function Behavior

Aroon produces discrete jumps rather than smooth curves. When a new extreme occurs, the corresponding line snaps to 100. Between new extremes, the line decays linearly by $100/N$ per bar. This staircase pattern is a natural consequence of the temporal measurement and should not be smoothed away — it carries information about the periodicity of extremes.

## Performance Profile

### Operation Count (Streaming Mode)

Aroon tracks the bar-ago position of the highest high and lowest low using deques (monotone queues) or linear window scans.

**Post-warmup steady state (per bar, deque-based O(1) amortized):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Deque update (high deque, amortized) | 2 | 1 | 2 |
| Deque update (low deque, amortized) | 2 | 1 | 2 |
| Index arithmetic (bars since high/low) | 2 | 1 | 2 |
| MUL × 2 + DIV × 2 (scale to 0–100) | 4 | 5 | 20 |
| **Total** | **10** | — | **~26 cycles** |

~26 cycles per bar at steady state. With naive linear scan: O(N) per bar = 2N comparisons.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Sliding max index (ArgMax) | Partial | SIMD can scan windows in parallel; ArgMax requires horizontal reduction |
| Sliding min index (ArgMin) | Partial | Same as ArgMax |
| Position → percentage scaling | Yes | VMULPD + VDIVPD |

Batch mode with SIMD prefix-max/min and horizontal ArgMax achieves ~4× throughput on vector-length chunks for the scan phase.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact integer arithmetic for positions; no floating-point drift |
| **Timeliness** | 8/10 | N-bar lookback; immediate response when new high/low is set |
| **Smoothness** | 4/10 | Output jumps when extreme prices enter or exit the window |
| **Noise Rejection** | 5/10 | Sensitive to outlier bars that reset the extreme-price position |

## Resources

- Chande, T.S. — *Beyond Technical Analysis* (John Wiley & Sons, 1995)
- Chande, T.S. — *The New Technical Trader* (John Wiley & Sons, 1995)
- PineScript reference: `aroon.pine` in indicator directory