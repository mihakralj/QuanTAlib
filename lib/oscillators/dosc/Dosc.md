# DOSC: Derivative Oscillator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `rsiPeriod` (default 14), `ema1Period` (default 5), `ema2Period` (default 3), `sigPeriod` (default 9)                      |
| **Outputs**      | Single series (Dosc)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `rsiPeriod + sigPeriod` bars                          |

### TL;DR

- The Derivative Oscillator applies a four-stage signal processing pipeline to extract momentum inflection points: RSI via Wilder's smoothing, double...
- Parameterized by `rsiperiod` (default 14), `ema1period` (default 5), `ema2period` (default 3), `sigperiod` (default 9).
- Output range: Varies (see docs).
- Requires `rsiPeriod + sigPeriod` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Derivative Oscillator applies a four-stage signal processing pipeline to extract momentum inflection points: RSI via Wilder's smoothing, double EMA smoothing of the RSI, an SMA signal line of the double-smoothed result, and finally the difference between the smoothed RSI and its signal. The histogram output crosses zero at momentum turning points, offering earlier signals than raw RSI by isolating the rate of change of the smoothed momentum rather than the momentum level itself.

## Historical Context

Constance Brown introduced the Derivative Oscillator in her 1994 work on advanced oscillator techniques, positioning it as a refinement of standard RSI analysis. The core insight was that RSI levels alone provide trend information but not inflection information: a rising RSI at 60 tells you momentum is bullish, but not whether it is accelerating or decelerating. By taking what amounts to the "derivative" of RSI (via the difference between the double-smoothed RSI and its moving average), the oscillator isolates the acceleration component. The double-EMA smoothing stage was borrowed from MACD-style signal extraction, while the final SMA subtraction mirrors the MACD histogram concept. Brown's original parameters (RSI 14, EMA1 5, EMA2 3, Signal 9) were calibrated for daily equity charts and remain the standard defaults. The indicator found adoption among fixed-income and commodity traders where RSI divergence analysis is common but requires confirmation of momentum turning points.

## Architecture & Physics

### Four-Stage Pipeline

1. **Stage 1: Wilder RSI.** Standard RSI using Wilder's RMA smoothing ($\alpha = 1/\text{rsiPeriod}$) for average gain and average loss. Output range: 0-100.

2. **Stage 2: First EMA.** Exponential moving average of the RSI output with $\alpha_1 = 2/(\text{ema1Period} + 1)$. This removes high-frequency RSI noise while preserving the momentum signal.

3. **Stage 3: Second EMA (double smoothing).** A second EMA with $\alpha_2 = 2/(\text{ema2Period} + 1)$ applied to the Stage 2 output. The double smoothing creates a zero-lag-adjusted smoother that tracks RSI trends with minimal overshoot.

4. **Stage 4: SMA signal line.** A simple moving average of the double-smoothed RSI, implemented via circular buffer with running sum for O(1) updates. The SMA period controls the signal line's responsiveness.

### Output

The Derivative Oscillator is the difference: $\text{DOSC} = \text{EMA2}(\text{EMA1}(\text{RSI})) - \text{SMA}(\text{EMA2}(\text{EMA1}(\text{RSI})))$. Zero crossings mark momentum inflection points. Positive values indicate accelerating RSI; negative values indicate decelerating RSI.

## Mathematical Foundation

**Stage 1: Wilder RSI** ($\alpha_r = 1/p_r$):

$$\overline{G}_t = \alpha_r \cdot \max(\Delta x_t, 0) + (1-\alpha_r) \cdot \overline{G}_{t-1}$$

$$\overline{L}_t = \alpha_r \cdot \max(-\Delta x_t, 0) + (1-\alpha_r) \cdot \overline{L}_{t-1}$$

$$RSI_t = 100 - \frac{100}{1 + \overline{G}_t / \overline{L}_t}$$

**Stage 2: First EMA** ($\alpha_1 = 2/(p_1 + 1)$):

$$E_1(t) = \alpha_1 \cdot RSI_t + (1-\alpha_1) \cdot E_1(t-1)$$

**Stage 3: Second EMA** ($\alpha_2 = 2/(p_2 + 1)$):

$$E_2(t) = \alpha_2 \cdot E_1(t) + (1-\alpha_2) \cdot E_2(t-1)$$

**Stage 4: SMA signal line** (period $p_s$):

$$S(t) = \frac{1}{\min(k, p_s)} \sum_{i=0}^{\min(k, p_s)-1} E_2(t-i)$$

where $k$ is the count of available values (warmup-aware).

**Derivative Oscillator:**

$$DOSC_t = E_2(t) - S(t)$$

**Default parameters:** rsiPeriod = 14, ema1Period = 5, ema2Period = 3, signalPeriod = 9.

## Performance Profile

### Operation Count (Streaming Mode)

Detrended Oscillator subtracts a shifted (N/2+1) SMA from current price.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer add + oldest sub (SMA sum) | 2 | 1 | 2 |
| MUL × 1/N (SMA) | 1 | 3 | 3 |
| RingBuffer read (shift N/2+1 bars back) | 1 | 1 | 1 |
| SUB (price − shifted SMA) | 1 | 1 | 1 |
| **Total** | **5** | — | **~7 cycles** |

One of the cheapest oscillators: ~7 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Rolling SMA | Yes | Prefix-sum subtract-lag; VADDPD/VSUBPD |
| Shift and subtraction | Yes | Array offset read + VSUBPD |

Fully SIMD-vectorizable. AVX2 achieves near-4× throughput.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact SMA arithmetic |
| **Timeliness** | 7/10 | N/2 shift is the dominant lag |
| **Smoothness** | 6/10 | No built-in smoothing of the oscillator output |
| **Noise Rejection** | 5/10 | Sensitive to bar-level noise without external smoothing |

## Resources

- Brown, C. (1994). *Technical Analysis for the Trading Professional*. McGraw-Hill
- Brown, C. (1999). *Technical Analysis for the Trading Professional*, 2nd ed. McGraw-Hill
- PineScript reference: [`dosc.pine`](dosc.pine)
