# JERK: Third Derivative

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (JERK)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `4` bars                          |
| **PineScript**   | [jerk.pine](jerk.pine)                       |

- JERK measures the rate of change of acceleration—called "jerk" in physics.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `4` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Acceleration tells you the trend is changing. Jerk tells you that change is itself changing—the earliest possible warning."

JERK measures the rate of change of acceleration—called "jerk" in physics. As the third derivative, it detects changes in momentum dynamics before they appear in acceleration, velocity, or price. A positive jerk means acceleration is increasing; negative means acceleration is decreasing. This O(1) streaming implementation uses dual FMA optimization and SIMD batch processing for four-point calculations.

## Historical Context

The third derivative (jerk) appears in mechanical engineering, robotics, and ride comfort analysis. Roller coasters are designed to minimize jerk; elevators smooth their motion to reduce it. In financial markets, jerk reveals sudden shifts in how fast the trend is accelerating or decelerating.

While first and second derivatives see wide use in technical analysis (momentum, ROC, acceleration indicators), the third derivative remains underutilized. This is partly computational—four consecutive points are needed—and partly interpretive: jerk is abstract. Yet it provides the earliest mathematical signal of trend character change.

Consider: price is rising, acceleration is positive (strong uptrend). If jerk turns negative, acceleration will soon decrease, then velocity will peak, then price will top. Jerk leads the entire sequence.

QuanTAlib implements JERK as the discrete third difference with dual FMA optimization, SIMD batch processing, and full bar correction support.

## Architecture & Physics

JERK computes the third finite difference with four-point history:

### 1. Third Difference Operation

The fundamental operation:

$$
J_t = V_t - 3V_{t-1} + 3V_{t-2} - V_{t-3}
$$

This is algebraically equivalent to:

$$
J_t = A_t - A_{t-1}
$$

where $A$ is the second derivative (acceleration).

### 2. Dual FMA Optimization

The formula uses two Fused Multiply-Add operations:

$$
\text{term}_1 = \text{FMA}(-3, V_{t-1}, V_t)
$$

$$
\text{term}_2 = \text{FMA}(3, V_{t-2}, -V_{t-3})
$$

$$
J_t = \text{term}_1 + \text{term}_2
$$

This structure reduces rounding error and leverages pipelined FMA units on modern CPUs.

### 3. State Management

State consists of:
- `Prev1`: The previous input value $V_{t-1}$
- `Prev2`: The value before that $V_{t-2}$
- `Prev3`: The value before that $V_{t-3}$
- `LastValidValue`: Last known finite value for NaN/Infinity substitution
- `Count`: Number of values processed (0, 1, 2, 3, or 4+)

The indicator becomes "hot" (fully warmed up) after 4 values.

## Mathematical Foundation

### Discrete Third Derivative

For a time series $V$:

$$
J_t = \frac{d^3V}{dt^3} \approx V_t - 3V_{t-1} + 3V_{t-2} - V_{t-3}
$$

This is the forward difference approximation of the third derivative.

### Binomial Coefficients

The coefficients $(1, -3, 3, -1)$ are the alternating binomial coefficients for $n=3$:

$$
\binom{3}{0} = 1, \quad -\binom{3}{1} = -3, \quad \binom{3}{2} = 3, \quad -\binom{3}{3} = -1
$$

### Derivative Chain

JERK completes the derivative hierarchy:

$$
\text{Slope}_t = V_t - V_{t-1}
$$

$$
\text{Accel}_t = V_t - 2V_{t-1} + V_{t-2}
$$

$$
\text{Jerk}_t = V_t - 3V_{t-1} + 3V_{t-2} - V_{t-3}
$$

### Interpretation Matrix

| Jerk | Accel | Slope | Meaning |
| :--- | :--- | :--- | :--- |
| $J > 0$ | $A > 0$ | $S > 0$ | Uptrend strengthening at increasing rate |
| $J < 0$ | $A > 0$ | $S > 0$ | Uptrend strengthening but rate slowing |
| $J > 0$ | $A < 0$ | $S > 0$ | Uptrend weakening but rate of weakening slowing |
| $J < 0$ | $A < 0$ | $S > 0$ | Uptrend weakening at increasing rate |
| $J > 0$ | $A < 0$ | $S < 0$ | Downtrend strengthening but rate slowing |
| $J < 0$ | $A < 0$ | $S < 0$ | Downtrend strengthening at increasing rate |
| $J > 0$ | $A > 0$ | $S < 0$ | Downtrend weakening at increasing rate |
| $J < 0$ | $A > 0$ | $S < 0$ | Downtrend weakening but rate slowing |

### Inflection Detection

Jerk zero-crossings can indicate second-order inflection points:

$$
J_t \times J_{t-1} < 0 \implies \text{Acceleration inflection point}
$$

This precedes the acceleration zero-crossing, which precedes the velocity peak/trough.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 2 | 4 | 8 |
| ADD | 1 | 1 | 1 |
| NEG | 1 | 1 | 1 |
| MOV (state update) | 4 | 1 | 4 |
| CMP (IsFinite check) | 1 | 1 | 1 |
| **Total** | **9** | — | **~15 cycles** |

### Batch Mode (512 values, SIMD)

| Architecture | Vector Width | Elements/Op | Total Ops (512 values) |
| :--- | :---: | :---: | :---: |
| AVX-512 | 512 bits | 8 doubles | 64 |
| AVX | 256 bits | 4 doubles | 128 |
| ARM64 Neon | 128 bits | 2 doubles | 256 |
| Scalar | 64 bits | 1 double | 512 |

**Batch efficiency (512 bars):**

| Mode | Cycles/bar | Total (512 bars) | Speedup |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 15 | 7,680 | 1× |
| AVX-512 SIMD | 1.9 | 973 | 8× |
| AVX SIMD | 3.8 | 1,946 | 4× |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact finite difference |
| **Timeliness** | 10/10 | Zero lag (instantaneous) |
| **Smoothness** | 1/10 | Extreme noise amplification |
| **Computational Cost** | 10/10 | Dual FMA + bookkeeping |
| **Memory** | 10/10 | ~80 bytes state |

## Validation

JERK is a fundamental operation. Validation confirms exact match with manual calculation and derivative chain composition.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Manual Calculation** | ✅ | Exact match |
| **Derivative Chain** | ✅ | Jerk = Accel - Accel_{t-1} matches |

## Common Pitfalls

1. **Catastrophic Noise Sensitivity**: Third derivatives amplify noise cubically. A 1% random wiggle becomes a wild jerk spike. Pre-smooth the input significantly (14+ period EMA minimum) before computing JERK.

2. **Scale Dependency**: JERK output scales with input magnitude cubed. A $100 stock has 1,000,000× larger jerks than a $1 stock. Normalization is essential for cross-instrument comparison.

3. **Warmup Period**: JERK requires 4 values to produce meaningful output. The first three outputs are always 0.

4. **Abstract Interpretation**: Jerk doesn't have an intuitive physical meaning for most traders. Use it as an early warning signal, not a direct trading trigger.

5. **Lead Time vs. Reliability**: Jerk provides the earliest signal but is also the most prone to false signals. Combine with lower derivatives for confirmation.

6. **Using isNew Incorrectly**: When processing live ticks within the same bar, use `Update(value, isNew: false)`. When a new bar opens, use `isNew: true` (default).

7. **Memory Footprint**: ~80 bytes per instance. Negligible for most use cases.

8. **Derivative Chain Verification**: JERK should equal the difference of consecutive ACCEL values. Use this identity to verify implementation correctness.

## References

- Newton, Isaac. (1687). "Philosophiæ Naturalis Principia Mathematica."
- Numerical Methods: Finite Difference Approximations.
- Eager, David et al. (2016). "Beyond velocity and acceleration: jerk, snap and higher derivatives." European Journal of Physics.
