# HT_PHASOR: Ehlers Hilbert Transform Phasor Components

> *Phasor components decompose price into in-phase and quadrature parts, mapping the cycle as a rotating vector.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (HT_PHASOR)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `LOOKBACK` bars                          |
| **PineScript**   | [phasor.pine](phasor.pine)                       |

- HT_PHASOR decomposes the price signal into two orthogonal components, InPhase ($I$) and Quadrature ($Q$), using the Hilbert Transform.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HT_PHASOR decomposes the price signal into two orthogonal components, InPhase ($I$) and Quadrature ($Q$), using the Hilbert Transform. Together these form a complex phasor $Z = I + jQ$ that describes the instantaneous amplitude and phase of the dominant market cycle. Compatible with TA-Lib's `HT_PHASOR` function, this dual-output indicator provides the fundamental building blocks for cycle analysis, phasor crossover timing, and instantaneous amplitude measurement.

## Historical Context

John Ehlers introduced phasor decomposition of market data in *Rocket Science for Traders* (2001). In electrical engineering, a phasor represents a sinusoidal signal as a rotating complex vector, separating the cycle's "position" (InPhase) from its "velocity" (Quadrature). Ehlers recognized that this decomposition is the mathematical foundation for all his cycle indicators: HT_SINE, HT_DCPERIOD, HT_DCPHASE, and HOMOD all derive from these same I/Q components. TA-Lib exposes HT_PHASOR to give advanced users direct access to the analytic signal for custom cycle analysis. The InPhase output is delayed by 3 bars to align with the Quadrature component's effective lag from the Hilbert Transform FIR.

## Architecture & Physics

### 1. WMA Smoothing

$$SmoothPrice_t = \frac{4P_t + 3P_{t-1} + 2P_{t-2} + P_{t-3}}{10}$$

### 2. Hilbert Transform FIR

Using Ehlers' coefficients ($A = 0.0962$, $B = 0.5769$), the 4-tap discrete Hilbert approximation generates the detrender, and from it the fundamental In-Phase and Quadrature components ($I_1$, $Q_1$). Further Hilbert transforms of these produce $jI$ and $jQ$.

### 3. Phasor Components

$$I_{2,t} = I_{1,t} - jQ_t, \qquad Q_{2,t} = Q_{1,t} + jI_t$$

Both smoothed with EMA ($\alpha = 0.2$):

$$I_t = 0.2 \cdot I_{2,t} + 0.8 \cdot I_{t-1}$$

$$Q_t = 0.2 \cdot Q_{2,t} + 0.8 \cdot Q_{t-1}$$

### 4. Phase Relationship

$Q$ leads $I$ by $90°$. When $I$ peaks, $Q$ crosses zero downward. When $I$ crosses zero upward, $Q$ peaks. The instantaneous amplitude is $A = \sqrt{I^2 + Q^2}$ and the instantaneous phase is $\phi = \arctan(Q/I)$.

### 5. Complexity

$O(1)$ per bar. Fixed Hilbert cascade with circular buffers. Warmup: 32 bars (TA-Lib lookback).

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

### Phasor Crossover Signals

| Condition | Signal |
|-----------|--------|
| $Q$ crosses $I$ from below | Bullish (anticipates cycle trough) |
| $Q$ crosses $I$ from above | Bearish (anticipates cycle peak) |
| $\sqrt{I^2 + Q^2}$ increasing | Cycle amplitude growing |
| $\sqrt{I^2 + Q^2}$ decreasing | Cycle amplitude fading (trend or noise) |

### Output Interpretation

| Output | Range | Meaning |
|--------|-------|---------|
| `InPhase` | unbounded | Cycle component aligned with price |
| `Quadrature` | unbounded | Rate of change (velocity) of cycle |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| 4-bar WMA | ~5 | 3 MUL + 1 ADD + 1 MUL(×0.1) |
| Hilbert FIR (detrender) | ~7 | 4-tap FIR: 4 MUL + 3 ADD |
| Hilbert FIR (Q1) | ~7 | Same 4-tap structure on det buffer |
| Hilbert FIR (jI) | ~7 | 4-tap on I1 history |
| Hilbert FIR (jQ) | ~7 | 4-tap on Q1 history |
| Phasor EMA (I2, Q2) | ~8 | 2 SUB/ADD + 4 FMA |
| Buffer management | ~10 | 4 circular buffer writes + index arithmetic |
| **Total** | **~51** | **O(1) fixed; no transcendentals (no period/phase extraction)** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | No: cascaded IIR EMA smoothing creates sequential dependencies |
| Bottleneck | Circular buffer indexed lookups for 4 Hilbert FIR passes |
| Parallelism | None: each bar's phasor depends on previous bar's EMA state |
| Memory | O(1): 4 circular buffers (7 elements each) + 2 scalar EMA states (~240 bytes) |
| Throughput | Fastest of the HT family; no transcendental calls (no ATAN/SIN/COS) |

## Resources

- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
- **TA-Lib** `TA_HT_PHASOR()` reference implementation.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.