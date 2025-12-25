# HTIT: Hilbert Transform Instantaneous Trend

> "John Ehlers brought rocket science to trading. Literally. HTIT uses signal processing to find the trend by removing the cycle. It's not smoothing; it's extraction."

HTIT (Hilbert Transform Instantaneous Trend) is a trend-following indicator that doesn't rely on simple averaging. Instead, it uses the Hilbert Transform to measure the dominant cycle period of the market and then computes a trendline that filters out that specific cycle. It adapts to the market's rhythm rather than imposing a fixed period.

## Historical Context

John Ehlers, a pioneer in applying DSP to trading, introduced this in his book *Rocket Science for Traders*. He recognized that markets have cyclic components (noise) and trend components. By identifying the cycle, you can mathematically subtract it to reveal the pure trend.

Most trend indicators (SMA, EMA) are low-pass filters: they let low frequencies (trend) pass and block high frequencies (noise). The problem is that "noise" in markets isn't random white noise; it's often cyclic. A fixed-period SMA might filter out a 10-day cycle perfectly but amplify a 20-day cycle. HTIT solves this by measuring the cycle first, then tuning the filter to kill exactly that frequency.

## Architecture & Physics

This is a complex, multi-stage signal processing pipeline. It's not just a formula; it's a machine.

1. **Smooth**: 4-bar WMA to remove high-frequency noise (Nyquist limit).
2. **Detrend**: High-pass filter to remove the DC component (trend) temporarily to isolate the cycle.
3. **Hilbert Transform**: Compute In-Phase (I) and Quadrature (Q) components.
4. **Period Measurement**: Use the phase rate of change (Homodyne Discriminator) to measure the dominant cycle period.
5. **Trend Extraction**: Average the price over the measured dominant cycle period to cancel out the cycle.
6. **Post-Smoothing**: 4-bar WMA on the extracted trend for final polish.

The "physics" here is cancellation. If you average a sine wave over exactly one period, the result is zero. If you average Price (Trend + Cycle) over exactly one cycle period, the Cycle cancels out, leaving only the Trend.

## Mathematical Foundation

### 1. Pre-Smoothing

A 4-tap FIR filter removes high-frequency noise to prevent aliasing before the Hilbert Transform.

$$ \text{Smooth}_t = \frac{4 P_t + 3 P_{t-1} + 2 P_{t-2} + P_{t-3}}{10} $$

### 2. Hilbert Transform & Detrending

The signal is detrended and split into In-Phase ($I$) and Quadrature ($Q$) components using a 7-tap Hilbert Transform. The coefficients are optimized for market cycles (10-40 bars).

$$ \text{Adj} = 0.075 \cdot \text{Period}_{t-1} + 0.54 $$

$$ \text{Detrender}_t = \left( \frac{5}{52} S_t + \frac{15}{26} S_{t-2} - \frac{15}{26} S_{t-4} - \frac{5}{52} S_{t-6} \right) \cdot \text{Adj} $$

$$ Q_t = \left( \frac{5}{52} D_t + \frac{15}{26} D_{t-2} - \frac{15}{26} D_{t-4} - \frac{5}{52} D_{t-6} \right) \cdot \text{Adj} $$

$$ I_t = D_{t-3} $$

### 3. Homodyne Discriminator

The phase rate of change is calculated using the complex conjugate product of the current and previous phasors. This is the "Homodyne Discriminator" - a fancy radio term for "measuring frequency by comparing a signal to a delayed version of itself."

$$ \text{Re}_t = (I2_t \cdot I2_{t-1}) + (Q2_t \cdot Q2_{t-1}) $$

$$ \text{Im}_t = (I2_t \cdot Q2_{t-1}) - (Q2_t \cdot I2_{t-1}) $$

The period is derived from the phase angle of this complex product:

$$ \text{Period}_t = \frac{2\pi}{\arctan\left(\frac{\text{Im}_t}{\text{Re}_t}\right)} $$

The period is constrained to [6, 50] bars and smoothed.

### 4. Instantaneous Trend

The trend is extracted by averaging the price over the measured dominant cycle period. This is the magic step.

$$ \text{IT}_t = \frac{1}{\text{DC}} \sum_{i=0}^{\text{DC}-1} P_{t-i} $$

Where $\text{DC}$ is the integer part of the smoothed dominant cycle period.

### 5. Final Output

The Instantaneous Trend is smoothed again using the same 4-bar WMA to remove any residual stepping artifacts from the integer period changes.

$$ \text{HTIT}_t = \frac{4 \text{IT}_t + 3 \text{IT}_{t-1} + 2 \text{IT}_{t-2} + \text{IT}_{t-3}}{10} $$

## Mathematical Precision & Implementation Philosophy

Like our MAMA implementation, QuanTAlib's HTIT prioritizes mathematical correctness over blind porting.

| Aspect                   | Other Libraries    | QuanTAlib               | Rationale                                     |
| :----------------------- | :----------------- | :---------------------- | :-------------------------------------------- |
| **Hilbert Coefficients** | `0.0962`, `0.5769` | `5.0/52.0`, `15.0/26.0` | Exact fractions avoid rounding accumulation   |
| **Adjustment Slope**     | `0.075`            | `3.0/40.0`              | Preserves rational arithmetic precision       |
| **Adjustment Intercept** | `0.54`             | `27.0/50.0`             | Ditto                                         |
| **Arctangent Function**  | `atan(y/x)`        | `atan2(y, x)`           | Proper quadrant handling, no division by zero |
| **Period Calculation**   | `360/atan(...)`    | `2π/atan2(...)`         | Mathematically correct radians                |

We use `atan2` for robust phase calculation and maintain full double precision throughout the pipeline.

## Performance Profile

HTIT is computationally heavier than a simple MA but lighter than MAMA. The main cost is the loop for the Instantaneous Trend calculation, which sums up to 50 past prices.

| Metric          | Score       | Notes                                                        |
| :-------------- | :---------- | :----------------------------------------------------------- |
| **Throughput**  | ~120 ns/bar | Variable cost due to dynamic loop length                     |
| **Allocations** | 0           | Stack-based circular buffers                                 |
| **Complexity**  | O(N)        | Depends on cycle period (max 50 iterations)                  |
| **Accuracy**    | 9/10        | Extracts trend by removing cycle                             |
| **Timeliness**  | 7/10        | Adapts, but has inherent lag from the cycle period averaging |
| **Overshoot**   | 8/10        | Generally good, stable trendline                             |
| **Smoothness**  | 9/10        | Very smooth trendline due to double WMA                      |

## Validation

Validated against TA-Lib, Skender, and Ooples.

| Library       | Status       | Notes                                                            |
| :------------ | :----------- | :--------------------------------------------------------------- |
| **QuanTAlib** | ✅ Reference | Mathematically correct implementation.                           |
| **TA-Lib**    | ✅           | Matches `HtTrendline` exactly (1e-9 precision).                  |
| **Skender**   | ⚠️           | Matches `GetHtTrendline` (~0.32% diff).                          |
| **Ooples**    | ⚠️           | Matches `CalculateEhlersInstantaneousTrendlineV1` (~0.25% diff). |

The differences with Skender and Ooples arise from:

1. **Initialization**: How the first few bars are handled.
2. **Precision**: Hardcoded decimals vs exact fractions.
3. **Period Constraints**: How strictly the [6, 50] bounds are enforced during intermediate steps.

### Common Pitfalls

1. **Warmup**: This indicator needs significant warmup (at least 12 bars, ideally 50+) for the feedback loops (period smoothing) to stabilize. Don't trust the first 50 bars.
2. **Lag**: While it adapts, the trendline still lags because it's essentially a dynamic SMA. The advantage is that the period is optimal for the current market condition, not that it has zero lag.
3. **Complexity**: Debugging this is a nightmare. Trust the math.
4. **Ranging Markets**: In a pure range, the "trend" should be flat. HTIT handles this well because the cycle cancellation works best when the cycle is clear.
