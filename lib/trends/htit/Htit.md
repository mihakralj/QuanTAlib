# HTIT: Hilbert Transform Instantaneous Trend

> "John Ehlers brought rocket science to trading. Literally. HTIT uses signal processing to find the trend by removing the cycle. It's not smoothing; it's extraction."

HTIT (Hilbert Transform Instantaneous Trend) is a trend-following indicator that doesn't rely on simple averaging. Instead, it uses the Hilbert Transform to measure the dominant cycle period of the market and then computes a trendline that filters out that specific cycle. It adapts to the market's rhythm rather than imposing a fixed period.

## Historical Context

John Ehlers, a pioneer in applying DSP to trading, introduced this in his book *Rocket Science for Traders*. He recognized that markets have cyclic components (noise) and trend components. By identifying the cycle, you can mathematically subtract it to reveal the pure trend.

## Architecture & Physics

This is a complex, multi-stage signal processing pipeline:

1. **Smooth**: 4-bar WMA to remove high-frequency noise.
2. **Detrend**: High-pass filter to remove the DC component (trend) temporarily to isolate the cycle.
3. **Hilbert Transform**: Compute In-Phase (I) and Quadrature (Q) components.
4. **Period Measurement**: Use the phase rate of change (Homodyne Discriminator) to measure the dominant cycle period.
5. **Trend Extraction**: Average the price over the measured dominant cycle period to cancel out the cycle.

## Mathematical Foundation

The core idea is that if you average a sine wave over exactly one period, the result is 0.

$$ \text{Trend}_t = \frac{1}{\text{DC}} \sum_{i=0}^{\text{DC}-1} P_{t-i} $$

Where $\text{DC}$ is the measured Dominant Cycle period.

### 1. Pre-Smoothing

A 4-tap FIR filter removes high-frequency noise (Nyquist limit) to prevent aliasing before the Hilbert Transform.

$$ \text{Smooth}_t = \frac{4 P_t + 3 P_{t-1} + 2 P_{t-2} + P_{t-3}}{10} $$

### 2. Hilbert Transform & Detrending

The signal is detrended and split into In-Phase ($I$) and Quadrature ($Q$) components using a 7-tap Hilbert Transform. The coefficients are optimized for market cycles (10-40 bars) to minimize passband ripple.

$$ \text{Adj} = 0.075 \cdot \text{Period}_{t-1} + 0.54 $$

$$ \text{Detrender}_t = \left( \frac{5}{52} S_t + \frac{15}{26} S_{t-2} - \frac{15}{26} S_{t-4} - \frac{5}{52} S_{t-6} \right) \cdot \text{Adj} $$

$$ Q_t = \left( \frac{5}{52} D_t + \frac{15}{26} D_{t-2} - \frac{15}{26} D_{t-4} - \frac{5}{52} D_{t-6} \right) \cdot \text{Adj} $$

$$ I_t = D_{t-3} $$

### 3. Homodyne Discriminator

The phase rate of change is calculated using the complex conjugate product of the current and previous phasors.

$$ \Delta \text{Phase} = \arctan\left(\frac{I_t Q_{t-1} - Q_t I_{t-1}}{I_t I_{t-1} + Q_t Q_{t-1}}\right) $$

$$ \text{Period}_t = \frac{2\pi}{\Delta \text{Phase}} $$

### 4. Instantaneous Trend

The trend is extracted by averaging the price over the measured dominant cycle period.

$$ \text{Trend}_t = \frac{1}{\text{Period}_t} \sum_{i=0}^{\text{Period}_t-1} P_{t-i} $$

## Performance Profile

This is an $O(1)$ algorithm, but the constant factor is large due to the many steps.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | [N] ns/bar | Heavy floating-point math per bar |
| **Allocations** | 0 | Stack-based calculations only |
| **Complexity** | O(1) | Pipeline depth is fixed |
| **Accuracy** | 9/10 | Extracts trend by removing cycle |
| **Timeliness** | 7/10 | Adapts, but has some lag |
| **Overshoot** | 8/10 | Generally good, stable trendline |
| **Smoothness** | 9/10 | Very smooth trendline |

## Validation

Validated against Ehlers' original EasyLanguage code and Python ports.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `HtTrendline` exactly |
| **Skender** | ⚠️ | Matches `GetHtTrendline` (~0.32% diff) |
| **Ooples** | ⚠️ | Matches `CalculateEhlersInstantaneousTrendlineV1` (~0.25% diff) |

| **Tulip** | N/A | Not implemented. |
### Common Pitfalls

1. **Warmup**: This indicator needs significant warmup (at least 12 bars, ideally 50+) for the feedback loops (period smoothing) to stabilize.
2. **Lag**: While it adapts, the trendline still lags because it's essentially a dynamic SMA. The advantage is that the period is optimal for the current market condition.
3. **Complexity**: Debugging this is a nightmare. Trust the math.
