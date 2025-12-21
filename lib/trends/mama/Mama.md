# MAMA: MESA Adaptive Moving Average

> "John Ehlers again. This time, he built a moving average that doesn't just adapt to volatility—it adapts to the phase of the market cycle. It's like having a GPS for your trend."

MAMA (MESA Adaptive Moving Average) is a unique adaptive moving average that uses the Hilbert Transform to determine the phase rate of change of the market cycle. It produces two outputs: MAMA (the adaptive average) and FAMA (Following Adaptive Moving Average), which acts as a slower, confirming signal.

## Historical Context

Introduced by John Ehlers in *MESA and Trading Market Cycles*, MAMA was designed to solve the problem of lag in a fundamentally different way. Instead of using price volatility (like KAMA or VIDYA), it uses the *cycle period*. When the cycle is short (fast market), MAMA speeds up. When the cycle is long (slow market), MAMA slows down.

## Architecture & Physics

The architecture is a direct application of the Hilbert Transform Homodyne Discriminator.

1. **Hilbert Transform**: Decomposes price into In-Phase (I) and Quadrature (Q) components.
2. **Phase Calculation**: Computes the phase angle from I and Q.
3. **Alpha Adaptation**: The smoothing alpha is derived from the rate of change of the phase.
    - Fast Phase Change = High Alpha (Fast MA).
    - Slow Phase Change = Low Alpha (Slow MA).

## Mathematical Foundation

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

### 4. Adaptive Alpha
The smoothing factor $\alpha$ is inversely proportional to the phase rate of change. When the phase changes rapidly (trend reversal or high volatility), $\alpha$ increases (faster response). When the phase changes slowly (stable trend), $\alpha$ decreases (more smoothing).

$$ \alpha = \frac{\text{FastLimit}}{\Delta \text{Phase}} $$

$$ \alpha = \max(\text{SlowLimit}, \min(\text{FastLimit}, \alpha)) $$

### 5. MAMA & FAMA Calculation
MAMA is an adaptive EMA using the calculated $\alpha$. FAMA (Following Adaptive Moving Average) is a second adaptive EMA applied to MAMA, using half the $\alpha$.

$$ \text{MAMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{MAMA}_{t-1} $$

$$ \text{FAMA}_t = 0.5 \alpha \cdot \text{MAMA}_t + (1 - 0.5 \alpha) \cdot \text{FAMA}_{t-1} $$

## Performance Profile

MAMA is computationally intensive due to the trigonometry (`Atan`, `Sin`, `Cos`) involved in the Hilbert Transform.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | Low | Trigonometry involved |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 8/10 | Adapts to market cycle phase |
| **Timeliness** | 9/10 | Extremely fast response to phase shifts |
| **Overshoot** | 6/10 | Can overshoot on sudden cycle changes |
| **Smoothness** | 6/10 | Can be stepped/jagged in transitions |

## Validation

Validated against Ehlers' original EasyLanguage code.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **Ehlers** | N/A | Logic matches *MESA and Trading Market Cycles* |

### Common Pitfalls

1. **Crossover Signals**: The MAMA/FAMA crossover is the primary signal. MAMA crossing over FAMA is bullish.
2. **Parameters**: `FastLimit` controls the maximum speed (usually 0.5). `SlowLimit` controls the minimum speed (usually 0.05).
3. **Whipsaws**: While adaptive, MAMA can still get chopped up in markets with no clear cycle (white noise).
