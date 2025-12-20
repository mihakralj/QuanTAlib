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

### Zero-Allocation Design

We maintain the complex state required for the Hilbert Transform without heap allocations.

- **RingBuffers**: For the delay lines needed by the Hilbert Transform.
- **State Struct**: Stores the phasors (I, Q, Re, Im) and previous values.
- **Fixed Pipeline**: The DSP pipeline is fixed-length, allowing for static optimization.

## Mathematical Foundation

$$ \text{Phase} = \arctan(Q / I) $$

$$ \alpha = \frac{\text{FastLimit}}{\Delta \text{Phase}} $$

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
