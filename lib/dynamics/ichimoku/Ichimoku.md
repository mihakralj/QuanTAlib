# ICHIMOKU: Ichimoku Kinko Hyo

> *Five lines, one cloud, and a time-shifted perspective — Ichimoku maps support, resistance, and momentum in a single glance.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `tenkanPeriod`, `kijunPeriod`, `senkouBPeriod`, `displacement`                      |
| **Outputs**      | Multiple series (Tenkan, Kijun, SenkouA, SenkouB, Chikou)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `maxPeriod` bars                          |
| **PineScript**   | [ichimoku.pine](ichimoku.pine)                       |

- Ichimoku Kinko Hyo ("One Glance Equilibrium Chart") is a comprehensive trend-following system that provides five distinct components revealing tren...
- Parameterized by `tenkanperiod`, `kijunperiod`, `senkoubperiod`, `displacement`.
- Output range: Varies (see docs).
- Requires `maxPeriod` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Ichimoku Kinko Hyo ("One Glance Equilibrium Chart") is a comprehensive trend-following system that provides five distinct components revealing trend direction, momentum, support/resistance levels, and potential future price zones simultaneously. The Tenkan-sen and Kijun-sen are midpoints of high-low ranges at different timescales (not moving averages of closes). Senkou Span A and B form the "cloud" (Kumo) — a projected equilibrium zone displaced forward in time. Chikou Span is simply the current close displaced backward. All components use sliding window min/max arithmetic, producing step-function behavior on breakouts rather than the smooth curves of EMA-based systems. The system requires OHLC bar input.

## Historical Context

Japanese journalist Goichi Hosoda (pen name "Ichimoku Sanjin") began developing the system in 1935, enlisting university students to hand-calculate the indicator across historical data decades before computers. He published the complete system in a seven-volume series in 1969. Ichimoku remained largely unknown outside Japan until the 1990s when international traders translated and popularized it. The original parameters (9, 26, 52) were calibrated to the Japanese six-day trading week: 9 days (1.5 weeks), 26 days (one month), and 52 days (two months). While modern five-day weeks would suggest different values, the original parameters remain standard due to widespread adoption and their self-fulfilling nature in liquid markets. Some practitioners use (7, 22, 44) for cryptocurrency markets with seven-day trading weeks. The system is unique among technical indicators in projecting values into the future (Senkou spans displaced 26 periods forward) and into the past (Chikou span displaced 26 periods back), providing a temporal dimension most indicators lack.

## Architecture & Physics

### 1. Tenkan-sen (Conversion Line)

Short-term equilibrium — midpoint of the highest high and lowest low over the Tenkan period:

$$\text{Tenkan}_t = \frac{\max(H_{t-8:t}) + \min(L_{t-8:t})}{2}$$

This is a range midpoint, not a moving average. It responds to breakouts (new highs or lows entering the window) rather than gradual price changes.

### 2. Kijun-sen (Base Line)

Medium-term equilibrium — same formula over a longer period:

$$\text{Kijun}_t = \frac{\max(H_{t-25:t}) + \min(L_{t-25:t})}{2}$$

Kijun-sen serves as the primary trend reference and key support/resistance level. Flat Kijun indicates a balanced market within that timeframe.

### 3. Senkou Span A (Leading Span A)

The average of Tenkan and Kijun, displayed forward:

$$\text{SenkouA}_t = \frac{\text{Tenkan}_t + \text{Kijun}_t}{2} \quad \text{(plotted at } t + \text{displacement)}$$

The more reactive cloud boundary. Changes faster due to Tenkan's shorter period.

### 4. Senkou Span B (Leading Span B)

Long-term midpoint, displayed forward:

$$\text{SenkouB}_t = \frac{\max(H_{t-51:t}) + \min(L_{t-51:t})}{2} \quad \text{(plotted at } t + \text{displacement)}$$

The slower, flatter cloud boundary. Provides strong support/resistance levels.

### 5. Chikou Span (Lagging Span)

Current close plotted backward:

$$\text{Chikou}_t = C_t \quad \text{(plotted at } t - \text{displacement)}$$

Confirms trend by comparing current price to the price from 26 bars ago.

### 6. Complexity

- **Time:** $O(N_{\text{senkou}})$ per bar for min/max scanning over the longest window (52)
- **Space:** $O(N_{\text{senkou}})$ — ring buffers for high and low histories
- **Warmup:** $\max(N_{\text{tenkan}}, N_{\text{kijun}}, N_{\text{senkou}}) = 52$ bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Original Meaning |
|--------|-----------|---------|-----------------|
| $N_t$ | tenkanPeriod | 9 | 1.5 trading weeks |
| $N_k$ | kijunPeriod | 26 | 1 trading month |
| $N_s$ | senkouBPeriod | 52 | 2 trading months |
| $d$ | displacement | 26 | 1 trading month forward/back |

### Cloud (Kumo) Interpretation

| Condition | Meaning |
|-----------|---------|
| SenkouA > SenkouB | Bullish cloud (green) — uptrend structure |
| SenkouA < SenkouB | Bearish cloud (red) — downtrend structure |
| Cloud twist (A crosses B) | Potential trend reversal (leading signal) |
| Thick cloud | Strong support/resistance zone |
| Thin cloud | Weak equilibrium — vulnerable to breakout |
| Price above cloud | Bullish bias |
| Price below cloud | Bearish bias |
| Price inside cloud | Indeterminate — consolidation |

### Displacement Note

Senkou Span A and B are computed at the current bar but displayed shifted forward by `displacement` bars on charts. Chikou Span is the current close displayed shifted backward. The implementation computes current-bar values only — the charting layer handles the visual displacement.

### Multi-Output Structure

The indicator produces five simultaneous values per bar. The primary output (`Last`) returns Kijun-sen as the default reference line.

## Performance Profile

### Operation Count (Streaming Mode)

Ichimoku draws five lines from three sliding window min/max operations and two EMA values for the Cloud. Three RingBuffers track highs/lows for Tenkan (9), Kijun (26), and Senkou B (52).

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer updates × 3 (T/K/S highs+lows) | 6 | 1 | 6 |
| Window max/min scans × 3 pairs (amortized O(1) with deques) | 6 | 1 | 6 |
| ADD + MUL×0.5 × 3 (midpoints for T, K, SB) | 6 | 3 | 18 |
| ADD + MUL×0.5 (Senkou A = (T+K)/2) | 2 | 3 | 6 |
| ADD + MUL×0.5 (Chikou = close[26]) | 2 | 1 | 2 |
| Buffer shift reads × 2 (Senkou A/B lag 26) | 2 | 1 | 2 |
| **Total** | **24** | — | **~40 cycles** |

Five output lines, three window scans, two lag buffers. For default periods (9/26/52): ~40 cycles per bar at steady state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Sliding max/min (3 windows) | Partial | Lemire deque O(n) total; scan phase SIMD-friendly |
| Midpoint arithmetic | Yes | VADDPD + VMULPD (×0.5) |
| Lag buffer reads | Yes | Array offset memory access |

Three independent window extremum computations can be parallelized. The midpoint and lag arithmetic is trivially vectorizable.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact integer-position midpoints; no floating-point drift |
| **Timeliness** | 3/10 | Senkou B requires 52-bar window + 26-bar projection = 78 bars to stability |
| **Smoothness** | 7/10 | Midpoint lines are inherently smooth; Cloud edges can gap |
| **Noise Rejection** | 7/10 | Window midpoints average out bar-to-bar noise by construction |

## Resources

- Hosoda, G. — *Ichimoku Kinko Hyo* (7-volume series, Tokyo, 1969)
- Patel, M. — *Trading with Ichimoku Clouds* (John Wiley & Sons, 2010)
- Elliott, N. — *Ichimoku Charts: An Introduction* (Harriman House, 2007)
- PineScript reference: `ichimoku.pine` in indicator directory
