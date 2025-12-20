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

### Zero-Allocation Design

Despite the complexity, we maintain zero allocations.

- **RingBuffers**: We use multiple small `RingBuffer`s for the various stages (smooth, detrend, I/Q, period).
- **State Struct**: Complex state (phasors, periods) is managed in a value type.
- **Fixed Buffers**: The pipeline depth is constant, allowing for static buffer sizing.

## Mathematical Foundation

The core idea is that if you average a sine wave over exactly one period, the result is 0.

$$ \text{Trend}_t = \frac{1}{\text{DC}} \sum_{i=0}^{\text{DC}-1} P_{t-i} $$

Where $\text{DC}$ is the measured Dominant Cycle period.

The Hilbert Transform is used to find $\text{DC}$ dynamically:

$$ \text{Phase} = \arctan(Q / I) $$

$$ \text{DC} = \frac{2\pi}{\Delta \text{Phase}} $$

## Performance Profile

This is an $O(1)$ algorithm, but the constant factor is large due to the many steps.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | Moderate | Heavy floating-point math per bar |
| **Complexity** | O(1) | Pipeline depth is fixed |
| **Accuracy** | 9/10 | Extracts trend by removing cycle |
| **Timeliness** | 7/10 | Adapts, but has some lag |
| **Overshoot** | 8/10 | Generally good, stable trendline |
| **Smoothness** | 9/10 | Very smooth trendline |

## Validation

Validated against Ehlers' original EasyLanguage code and Python ports.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **Ehlers** | N/A | Logic matches *Rocket Science for Traders* |

### Common Pitfalls

1. **Warmup**: This indicator needs significant warmup (at least 12 bars, ideally 50+) for the feedback loops (period smoothing) to stabilize.
2. **Lag**: While it adapts, the trendline still lags because it's essentially a dynamic SMA. The advantage is that the period is optimal for the current market condition.
3. **Complexity**: Debugging this is a nightmare. Trust the math.
