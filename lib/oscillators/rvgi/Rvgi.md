# RVGI: Ehlers Relative Vigor Index

> *Relative Vigor Index compares the close-open range to the high-low range, measuring conviction in each bar's direction.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 10)                      |
| **Outputs**      | Single series (Rvgi)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [rvgi.pine](rvgi.pine)                       |

- The Relative Vigor Index measures the conviction of a price move by comparing closing strength (close minus open) to the total intrabar range (high...
- **Similar:** [AO](../ao/Ao.md), [Fisher](../fisher/Fisher.md) | **Complementary:** Volume | **Trading note:** Relative Vigor Index; compares close−open to high−low. Rising = bullish vigor. Signal-line crossovers.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Relative Vigor Index measures the conviction of a price move by comparing closing strength (close minus open) to the total intrabar range (high minus low), smoothed through a symmetrically weighted moving average and then averaged over a lookback period. The premise is that in bullish markets, closes tend to occur near highs and opens near lows, producing positive RVGI values, while bearish markets show the opposite pattern. A 4-bar SWMA signal line provides crossover triggers. The indicator oscillates around zero with no fixed bounds.

## Historical Context

John Ehlers introduced the Relative Vigor Index in his 2002 book *Rocket Science for Traders*, drawing on the concept that price vigor (the difference between open and close) relative to the bar's range captures directional conviction more effectively than close-only momentum measures. The design reflects Ehlers' signal processing background: the SWMA (Symmetrically Weighted Moving Average) with weights $[1, 2, 2, 1]/6$ is a 4-tap FIR filter with symmetric coefficients, which guarantees zero phase shift at the cost of minimal lag. This choice was deliberate, as asymmetric weights would introduce phase distortion that corrupts the relationship between the indicator and its signal line. The SMA averaging stage serves as a secondary smoothing filter that reduces noise without further phase impact. The signal line reuses the same SWMA kernel, maintaining phase consistency throughout the entire processing chain.

## Architecture & Physics

### Four-Stage Pipeline

1. **SWMA of (Close - Open):** A fixed 4-bar kernel with weights $[1, 2, 2, 1]/6$ applied to the close-minus-open series. This captures the directional conviction of each bar, smoothed symmetrically.

2. **SWMA of (High - Low):** The same 4-bar kernel applied to the high-minus-low (range) series. This normalizes by volatility.

3. **SMA of numerator and denominator:** Independent SMAs over the specified period, each using a circular buffer with O(1) running sum updates. The ratio $\text{SMA(numerator)} / \text{SMA(denominator)}$ produces the RVGI line.

4. **Signal line:** A 4-bar SWMA of the RVGI output, using three history variables to store the previous three RVGI values. The SWMA kernel is hardcoded: $(rv_3 + 2 \cdot rv_2 + 2 \cdot rv_1 + rv_0) / 6$.

### Defensive Division

When the denominator SMA equals zero (all bars in the window have zero range, i.e., doji sequences), RVGI returns 0 rather than propagating division by zero.

## Mathematical Foundation

Given OHLC bars and lookback period $n$:

**SWMA kernel** (4-tap symmetric FIR):

$$w = \left[\frac{1}{6}, \frac{2}{6}, \frac{2}{6}, \frac{1}{6}\right]$$

**Numerator (closing strength):**

$$N_t = \frac{(C_{t-3} - O_{t-3}) + 2(C_{t-2} - O_{t-2}) + 2(C_{t-1} - O_{t-1}) + (C_t - O_t)}{6}$$

**Denominator (bar range):**

$$D_t = \frac{(H_{t-3} - L_{t-3}) + 2(H_{t-2} - L_{t-2}) + 2(H_{t-1} - L_{t-1}) + (H_t - L_t)}{6}$$

**SMA smoothing** (O(1) circular buffer):

$$\overline{N}_t = \frac{1}{n}\sum_{i=0}^{n-1} N_{t-i}, \quad \overline{D}_t = \frac{1}{n}\sum_{i=0}^{n-1} D_{t-i}$$

**RVGI:**

$$RVGI_t = \begin{cases} \overline{N}_t / \overline{D}_t & \text{if } \overline{D}_t \neq 0 \\ 0 & \text{otherwise} \end{cases}$$

**Signal line:**

$$Signal_t = \frac{RVGI_{t-3} + 2 \cdot RVGI_{t-2} + 2 \cdot RVGI_{t-1} + RVGI_t}{6}$$

**Default parameters:** period = 10.

## Performance Profile

### Operation Count (Streaming Mode)

RVGI (Relative Vigor Index) computes a symmetrically-weighted sum of body changes (4-bar) divided by range changes, then smooths with a SMA-like signal line.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Body/range weighted numerator (4 FMAs) | 4 | 4 | 16 |
| Body/range weighted denominator (4 FMAs) | 4 | 4 | 16 |
| DIV (numerator/denominator) | 1 | 15 | 15 |
| RingBuffer updates × 2 (num, denom) | 4 | 1 | 4 |
| Signal line (4-tap weighted avg) | 4 | 3 | 12 |
| CMP (denom > 0 guard) | 1 | 1 | 1 |
| **Total** | **18** | — | **~64 cycles** |

~64 cycles per bar. Two simultaneous 4-tap FIR convolutions + a division.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| 4-tap weighted sums (FIR) | Yes | VDPPS / manual dot product with VFMADD |
| Division | Yes | VDIVPD |
| Signal line (4-tap FIR) | Yes | Same FIR pattern |

Fully vectorizable — no recursive dependencies. AVX2 achieves ~4× throughput.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Symmetric weighting reduces endpoint bias |
| **Timeliness** | 7/10 | 4-bar FIR window + 4-bar signal = 8-bar warmup only |
| **Smoothness** | 7/10 | Symmetric FIR provides gentle smoothing |
| **Noise Rejection** | 7/10 | Weighted averaging suppresses tick noise |

## Resources

- Ehlers, J.F. (2002). *Rocket Science for Traders*. Wiley, Chapter 12
- Ehlers, J.F. (2001). "The Relative Vigor Index." *Technical Analysis of Stocks & Commodities*
- PineScript reference: [`rvgi.pine`](rvgi.pine)