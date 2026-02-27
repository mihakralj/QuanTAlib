# TRAMA: Trend Regularity Adaptive Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Trama)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- TRAMA is an adaptive EMA where the smoothing factor derives from the "trend regularity" of the lookback window, measured as the fraction of bars th...
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "LuxAlgo counted how often price makes new highs and new lows within a window, squared that fraction, and used it as an EMA smoothing constant. Trending markets produce frequent HH/LLs and the filter tracks fast. Ranging markets produce few, and the filter stops moving. Simple, effective, elegant."

TRAMA is an adaptive EMA where the smoothing factor derives from the "trend regularity" of the lookback window, measured as the fraction of bars that produce either a new highest-high (HH) or a new lowest-low (LL). This fraction is squared to create a convex penalty: low regularity (ranging) produces near-zero smoothing (filter barely moves), while high regularity (trending) produces aggressive smoothing (filter tracks closely). Developed by LuxAlgo (TradingView, December 2020).

## Historical Context

TRAMA was published by LuxAlgo on TradingView in December 2020 as a novel approach to adaptive smoothing. While earlier adaptive MAs (KAMA, VIDYA, ADXVMA) derive their adaptation from efficiency ratios, standard deviations, or ADX, TRAMA uses a purely non-parametric measure: the frequency of new extremes.

The key insight is that trending markets are characterized by a high rate of new highest-highs and lowest-lows, while ranging markets produce new extremes only occasionally (at the range boundaries). This binary event (new extreme or not) is robust to the magnitude of price changes and immune to the scale issues that affect volatility-based adaptive methods.

The squaring of the trend coefficient $tc = [\text{SMA}(\text{HH or LL occurred}, N)]^2$ is critical to TRAMA's behavior. Without squaring, a market with 50% HH/LL bars (typical for a mild trend) would use $tc = 0.5$, producing moderate smoothing. With squaring, $tc = 0.25$, producing heavier smoothing. This convex penalty ensures that TRAMA switches between "tracking" and "holding" regimes more sharply than a linear adaptation would, reducing whipsaws in ambiguous market conditions.

## Architecture & Physics

### 1. Extreme Detection

On each bar, check whether the rolling highest-high or lowest-low (over the lookback period) has changed:

$$
\text{HH} = \max\left(\text{sign}\left(\Delta\, \text{Highest}(N)\right), 0\right)
$$

$$
\text{LL} = \max\left(\text{sign}\left(-\Delta\, \text{Lowest}(N)\right), 0\right)
$$

### 2. Trend Regularity Coefficient

$$
tc = \left[\text{SMA}\left(\text{HH or LL} \neq 0 \;\;?\;\; 1 : 0, \;\;N\right)\right]^2
$$

This gives the squared fraction of bars with new extremes.

### 3. Adaptive EMA Step

$$
\text{TRAMA}_t = \text{TRAMA}_{t-1} + tc \times (x_t - \text{TRAMA}_{t-1})
$$

## Mathematical Foundation

**Extreme indicators:**

$$
\text{HH}_t = \begin{cases} 1 & \text{if } \max(x_{t}, \ldots, x_{t-N+1}) > \max(x_{t-1}, \ldots, x_{t-N}) \\ 0 & \text{otherwise} \end{cases}
$$

$$
\text{LL}_t = \begin{cases} 1 & \text{if } \min(x_{t}, \ldots, x_{t-N+1}) < \min(x_{t-1}, \ldots, x_{t-N}) \\ 0 & \text{otherwise} \end{cases}
$$

**Trend coefficient (squared SMA of binary events):**

$$
tc_t = \left[\frac{1}{N}\sum_{i=0}^{N-1} \mathbf{1}\left(\text{HH}_{t-i} \vee \text{LL}_{t-i}\right)\right]^2
$$

**Adaptive update:**

$$
\text{TRAMA}_t = \text{TRAMA}_{t-1} + tc_t \cdot (x_t - \text{TRAMA}_{t-1})
$$

**Regime behavior:**

| Market Regime | HH/LL frequency | Raw $tc$ | Squared $tc$ | Equivalent EMA period |
| :--- | :---: | :---: | :---: | :---: |
| Strong trend | ~80% | 0.8 | 0.64 | ~2.6 |
| Moderate trend | ~50% | 0.5 | 0.25 | ~7 |
| Mild trend | ~30% | 0.3 | 0.09 | ~20 |
| Range-bound | ~10% | 0.1 | 0.01 | ~199 |

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
// Detect new highest-high or lowest-low
hh = max(sign(change(highest(src, length))), 0)
ll = max(sign(change(lowest(src, length)) * -1), 0)

// Trend regularity: fraction of bars with HH or LL, squared
tc = sma((hh or ll) ? 1 : 0, length) ^ 2

// Adaptive EMA
trama = trama[1] + tc * (src - trama[1])
```

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
| :--- | :---: | :--- |
| RingBuffer.Add (prices) | 1 | Rolling window update |
| RingBuffer.Max() | 1 | O(N) scan for highest |
| RingBuffer.Min() | 1 | O(N) scan for lowest |
| Comparison (HH detect) | 1 | currentHighest > prevHighest |
| Comparison (LL detect) | 1 | currentLowest < prevLowest |
| RingBuffer.Add (events) | 1 | Binary event push |
| Running sum update | 1 | O(1) via add-subtract |
| Division (SMA) | 1 | eventSum / period |
| Multiply (square) | 1 | tc = sma * sma |
| FMA (EMA step) | 1 | FusedMultiplyAdd for adaptive update |
| State copy (isNew) | 1 | Snapshot/Restore for bar correction |
| **Total** | **~11 + 2N** | **Dominated by Max/Min scans** |

Streaming complexity: **O(N)** per bar due to RingBuffer.Max()/Min() linear scans. The EMA step itself is O(1). For typical periods (14-50), the constant factor is small. A monotonic deque optimization could reduce to amortized O(1) but adds implementation complexity for marginal gain at these window sizes.

### Batch Mode (SIMD Analysis)

| Component | SIMD candidate? | Reason |
| :--- | :---: | :--- |
| Rolling max/min | ⚠️ Partial | Sliding window max/min has data dependencies; vectorizable within window scan |
| HH/LL detection | ✔️ Yes | Independent comparisons across bars |
| Event SMA | ⚠️ Partial | Running sum is sequential; initial accumulation vectorizable |
| Squaring tc | ✔️ Yes | Independent multiply |
| Adaptive EMA | ❌ No | Output[t] depends on output[t-1]; inherently sequential |
| **Overall** | **Limited** | **IIR feedback loop blocks full vectorization** |

Batch implementation uses ArrayPool-rented circular buffers for prices and events, avoiding heap allocations for the common case. The sequential dependency in the adaptive EMA step prevents SIMD acceleration of the core output loop, consistent with all IIR-class filters in QuanTAlib.

### Quality Metrics

| Metric | Score (1-10) | Notes |
| :--- | :---: | :--- |
| Lag reduction | 8 | Excellent in trends; near-zero movement in ranges |
| Noise suppression | 7 | Strong in consolidation; less filtering in trends (by design) |
| Whipsaw resistance | 9 | Squaring penalty sharply separates trend/range regimes |
| Responsiveness | 8 | Fast adaptation when HH/LL frequency increases |
| Parameter sensitivity | 7 | Single parameter (period); robust across 10-50 range |
| Computational cost | 6 | O(N) per bar from max/min scans; acceptable for typical periods |

## Resources

- LuxAlgo (2020). "TRAMA - Trend Regularity Adaptive Moving Average." TradingView. Published December 2020.
- Kaufman, P.J. (1995). *Smarter Trading*. McGraw-Hill. Chapter 7: Adaptive Techniques. (KAMA framework, precursor to adaptive MA design.)
- Chande, T.S. (1997). *Beyond Technical Analysis*, 2nd ed. Wiley. (VIDYA and adaptive smoothing theory.)
