# TTM_TREND: TTM Trend

> *The simplest trend indicator is the one you actually follow.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default DefaultPeriod)                      |
| **Outputs**      | Single series (TTM_TREND)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `> 2` bars                          |
| **PineScript**   | [TtmTrend.pine](TtmTrend.pine)                       |

- John Carter's TTM Trend uses a fast EMA (default period 6) applied to typical price (HLC/3) to determine short-term trend direction via slope sign.
- **Similar:** [Impulse](../impulse/Impulse.md), [AMAT](../amat/Amat.md) | **Complementary:** TTM Squeeze for timing | **Trading note:** John Carter's trend indicator; colors bars based on close vs midpoint of prior 5-bar range.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

John Carter's TTM Trend uses a fast EMA (default period 6) applied to typical price (HLC/3) to determine short-term trend direction via slope sign. Output is a ternary trend state: +1 (bullish, EMA rising), -1 (bearish, EMA falling), or 0 (neutral, EMA unchanged). The indicator requires only 2 bars warmup, runs at O(1) per bar with O(1) space, and produces zero allocations in the hot path.

## Historical Context

John Carter developed the TTM (Trade the Markets) Trend indicator as a clean visual tool for identifying short-term trend direction, popularized through *Mastering the Trade* and the thinkorswim platform. Unlike complex multi-component trend systems, TTM Trend reduces trend detection to its minimum viable form: the slope of a fast exponential moving average. The very short default period (6) makes it responsive to recent price action, positioning it as a "first responder" trend filter meant to be combined with Carter's other TTM tools (Squeeze, Wave, LRC). The color-coded output (green/red/gray) provides at-a-glance trend assessment.

## Architecture & Physics

### 1. Typical Price

$$\text{TP}_t = \frac{H_t + L_t + C_t}{3}$$

Using typical price rather than close reduces susceptibility to closing-tick noise.

### 2. EMA Recursion

$$\alpha = \frac{2}{N + 1}$$

$$\text{EMA}_t = \alpha \cdot \text{TP}_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

Or equivalently via FMA:

$$\text{EMA}_t = \text{FMA}(\alpha,\ \text{TP}_t - \text{EMA}_{t-1},\ \text{EMA}_{t-1})$$

### 3. Trend Classification

$$\text{Trend}_t = \text{sign}(\text{EMA}_t - \text{EMA}_{t-1})$$

| Value | State | Color |
|:------|:------|:------|
| +1 | Bullish | Green |
| -1 | Bearish | Red |
| 0 | Neutral | Gray |

### 4. Strength Measurement

$$\text{Strength}_t = \frac{|\text{EMA}_t - \text{EMA}_{t-1}|}{\text{EMA}_{t-1}} \times 100\%$$

This percentage rate-of-change quantifies how aggressively the trend is moving. High strength values indicate strong conviction; near-zero values suggest potential reversal.

### 5. Complexity

| Metric | Value |
|:-------|:------|
| Time | O(1) per bar |
| Space | O(1) (one EMA state + one previous value) |
| Warmup | 2 bars |
| Allocations | Zero in hot path |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| period | int | 6 | > 0 | EMA lookback period (very fast by default) |

### Period Selection

The default period of 6 makes TTM Trend extremely fast-reacting. The EMA half-life is approximately $\ln(2) / \ln(1 + 2/N) \approx 2.4$ bars for $N = 6$. This means the indicator responds within 2-3 bars of a price shift. Longer periods (12, 20) reduce whipsaws but delay detection. Carter's design intent was maximum responsiveness, with noise filtering delegated to companion indicators (Squeeze, Wave).

## Performance Profile

### Operation Count (Streaming Mode)

TTM Trend colors bars based on whether close is above/below a short SMA, with momentum confirmation from a histogram.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer add + oldest sub (running sum) | 2 | 1 | 2 |
| MUL × 1/N (SMA) | 1 | 3 | 3 |
| CMP (close vs SMA) | 1 | 1 | 1 |
| Histogram momentum (FMA EMA update) | 1 | 4 | 4 |
| Color encoding (ternary +1/0/−1) | 1 | 1 | 1 |
| **Total** | **6** | — | **~11 cycles** |

Very cheap: ~11 cycles per bar at steady state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| SMA (rolling sum) | Yes | VADDPD + prefix-sum subtract-lag |
| EMA histogram | **No** | Recursive IIR |
| Bar color comparison | Yes | VCMPPD |

The EMA histogram is the only sequential step. SMA and comparison are fully vectorizable.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | SMA exact arithmetic; EMA FMA-precise |
| **Timeliness** | 8/10 | Short SMA period dominates; near-instantaneous response |
| **Smoothness** | 10/10 | Ternary output — maximally smooth |
| **Noise Rejection** | 6/10 | Short SMA period makes it sensitive to noise in choppy markets |

## Resources

- Carter, J. (2005). *Mastering the Trade*. McGraw-Hill.