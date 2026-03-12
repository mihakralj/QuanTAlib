# ACCEL: Second Derivative (Acceleration)

> *Velocity tells you where you're going. Acceleration tells you if you're getting there faster or slower.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (ACCEL)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `3` bars                          |
| **PineScript**   | [accel.pine](accel.pine)                       |

- ACCEL measures the rate of change of velocity—the acceleration of a time series.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `3` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

ACCEL measures the rate of change of velocity—the acceleration of a time series. As the second derivative, it reveals momentum shifts before they manifest in price direction. Positive acceleration means velocity is increasing (trend strengthening); negative means velocity is decreasing (trend weakening). This O(1) streaming implementation uses FMA optimization and SIMD batch processing.

## Historical Context

The second derivative appears throughout physics (Newton's F=ma) and signal processing. In financial markets, acceleration precedes velocity, which precedes price. A stock can be rising (positive slope) but decelerating (negative accel)—an early warning of trend exhaustion.

Traders have long recognized this pattern: "the trend is slowing down." ACCEL quantifies that intuition precisely. When price makes higher highs but acceleration turns negative, the rally is losing steam. When price makes lower lows but acceleration turns positive, the selloff is exhausting.

QuanTAlib implements ACCEL as the discrete second difference with FMA optimization, SIMD batch processing, and full bar correction support.

## Architecture & Physics

ACCEL computes the second finite difference with three-point history:

### 1. Second Difference Operation

The fundamental operation:

$$
A_t = V_t - 2V_{t-1} + V_{t-2}
$$

This is algebraically equivalent to:

$$
A_t = (V_t - V_{t-1}) - (V_{t-1} - V_{t-2}) = S_t - S_{t-1}
$$

where $S$ is the first derivative (slope).

### 2. FMA Optimization

The formula $V_t - 2V_{t-1} + V_{t-2}$ is computed using Fused Multiply-Add:

$$
A_t = \text{FMA}(-2, V_{t-1}, V_t + V_{t-2})
$$

This reduces rounding error and may execute in a single CPU cycle on modern hardware.

### 3. State Management

State consists of:
- `Prev1`: The previous input value $V_{t-1}$
- `Prev2`: The value before that $V_{t-2}$
- `LastValidValue`: Last known finite value for NaN/Infinity substitution
- `Count`: Number of values processed (0, 1, 2, or 3+)

The indicator becomes "hot" (fully warmed up) after 3 values.

## Mathematical Foundation

### Discrete Second Derivative

For a time series $V$:

$$
A_t = \frac{d^2V}{dt^2} \approx V_t - 2V_{t-1} + V_{t-2}
$$

This is the central difference approximation of the second derivative.

### Interpretation

| Acceleration Value | Slope Value | Meaning |
| :--- | :--- | :--- |
| $A > 0$ | $S > 0$ | Rising and accelerating (strong uptrend) |
| $A < 0$ | $S > 0$ | Rising but decelerating (weakening uptrend) |
| $A > 0$ | $S < 0$ | Falling but decelerating (weakening downtrend) |
| $A < 0$ | $S < 0$ | Falling and accelerating (strong downtrend) |
| $A = 0$ | any | Constant velocity (linear trend) |

### Inflection Points

Acceleration zero-crossings indicate inflection points—where the trend changes character:

$$
A_t > 0 \text{ and } A_{t-1} < 0 \implies \text{Concave-up inflection (potential bottom)}
$$

$$
A_t < 0 \text{ and } A_{t-1} > 0 \implies \text{Concave-down inflection (potential top)}
$$

### Derivative Chain

ACCEL is the middle link:

$$
\text{Slope}_t = V_t - V_{t-1}
$$

$$
\text{Accel}_t = \text{Slope}_t - \text{Slope}_{t-1} = V_t - 2V_{t-1} + V_{t-2}
$$

$$
\text{Jolt}_t = \text{Accel}_t - \text{Accel}_{t-1}
$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 1 | 4 | 4 |
| ADD | 1 | 1 | 1 |
| MOV (state update) | 3 | 1 | 3 |
| CMP (IsFinite check) | 1 | 1 | 1 |
| **Total** | **6** | — | **~9 cycles** |

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
| Scalar streaming | 9 | 4,608 | 1× |
| AVX-512 SIMD | 1.1 | 563 | 8× |
| AVX SIMD | 2.3 | 1,178 | 4× |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact finite difference |
| **Timeliness** | 10/10 | Zero lag (instantaneous) |
| **Smoothness** | 2/10 | Amplifies noise significantly |
| **Computational Cost** | 10/10 | Single FMA + bookkeeping |
| **Memory** | 10/10 | ~64 bytes state |

## Validation

ACCEL is a fundamental operation. Validation confirms exact match with manual calculation.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented directly |
| **Skender** | N/A | Not implemented directly |
| **Manual Calculation** | ✅ | Exact match |

## Common Pitfalls

1. **Extreme Noise Sensitivity**: Second derivatives amplify noise quadratically. A 1% random wiggle in price becomes a massive acceleration spike. Pre-smooth the input (EMA, SMA) before computing ACCEL for noisy data.

2. **Scale Dependency**: ACCEL output scales with input magnitude squared. A $100 stock has 10,000× larger accelerations than a $1 stock. Normalize if comparing across instruments.

3. **Warmup Period**: ACCEL requires 3 values to produce meaningful output. The first two outputs are always 0.

4. **Sign Interpretation**: Positive acceleration doesn't mean "going up"—it means "velocity increasing." A falling stock with positive acceleration is falling more slowly.

5. **Lagging Confirmation**: By the time acceleration confirms a trend change, much of the move may be over. Use acceleration for early warning, not entry confirmation.

6. **Using isNew Incorrectly**: When processing live ticks within the same bar, use `Update(value, isNew: false)`. When a new bar opens, use `isNew: true` (default).

7. **Memory Footprint**: ~64 bytes per instance. Negligible for most use cases.

## References

- Newton, Isaac. (1687). "Philosophiæ Naturalis Principia Mathematica."
- Numerical Methods: Finite Difference Approximations.
- Murphy, John J. (1999). "Technical Analysis of the Financial Markets."
