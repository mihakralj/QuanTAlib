# SLOPE: First Derivative (Velocity)

> *The simplest measure of change reveals the most: is it going up, or going down?*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (SLOPE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [slope.pine](slope.pine)                       |

- SLOPE measures the instantaneous rate of change—the velocity of a time series.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

SLOPE measures the instantaneous rate of change—the velocity of a time series. As the first derivative, it answers the fundamental question: how fast is the value changing right now? A positive slope means ascending; negative means descending; zero means flat. This O(1) streaming implementation uses SIMD optimization for batch calculations and handles bar corrections via state rollback.

## Historical Context

The first derivative appears in Newton's calculus (1687) and forms the foundation of technical analysis. Every momentum indicator, every rate-of-change calculation, every velocity measure reduces to some form of first difference.

In discrete time series, the continuous derivative $\frac{dx}{dt}$ becomes the finite difference $\Delta x = x_t - x_{t-1}$. This simple subtraction underpins RSI's momentum, MACD's signal line, and every trend-following system that asks "which way is it moving?"

QuanTAlib implements SLOPE as a first-class indicator with full streaming support, SIMD batch optimization, and proper state management for bar corrections.

## Architecture & Physics

SLOPE is a memoryless differentiator with minimal state requirements:

### 1. First Difference Operation

The fundamental operation:

$$
S_t = V_t - V_{t-1}
$$

where $V_t$ is the current value and $V_{t-1}$ is the previous value.

### 2. State Management

State consists of:
- `PrevValue`: The previous input value
- `LastValidValue`: Last known finite value for NaN/Infinity substitution
- `Count`: Number of values processed (0, 1, or 2+)

The indicator becomes "hot" (fully warmed up) after 2 values.

### 3. Bar Correction via Rollback

When `isNew=false`, the indicator rolls back to the previous state before recalculating:

$$
\text{State}_{current} \leftarrow \text{State}_{previous}
$$

This enables real-time bar updates without corrupting the running calculation.

## Mathematical Foundation

### Discrete First Derivative

For a time series $V$:

$$
S_t = V_t - V_{t-1}
$$

This is the forward difference approximation of the derivative.

### Interpretation

| Slope Value | Meaning |
| :--- | :--- |
| $S > 0$ | Price ascending (bullish) |
| $S < 0$ | Price descending (bearish) |
| $S = 0$ | Price unchanged (consolidation) |
| $|S|$ large | Fast movement |
| $|S|$ small | Slow movement |

### Relationship to Higher Derivatives

SLOPE forms the basis of the derivative chain:

$$
\text{Accel}_t = \text{Slope}_t - \text{Slope}_{t-1}
$$

$$
\text{Jolt}_t = \text{Accel}_t - \text{Accel}_{t-1}
$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB | 1 | 1 | 1 |
| MOV (state update) | 2 | 1 | 2 |
| CMP (IsFinite check) | 1 | 1 | 1 |
| **Total** | **4** | — | **~4 cycles** |

SLOPE is one of the fastest possible indicators—a single subtraction plus state bookkeeping.

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
| Scalar streaming | 4 | 2,048 | 1× |
| AVX-512 SIMD | 0.5 | 256 | 8× |
| AVX SIMD | 1 | 512 | 4× |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact finite difference |
| **Timeliness** | 10/10 | Zero lag (instantaneous) |
| **Smoothness** | 3/10 | Amplifies noise |
| **Computational Cost** | 10/10 | Single subtraction |
| **Memory** | 10/10 | ~48 bytes state |

## Validation

SLOPE is a fundamental operation. Validation confirms exact match with manual calculation.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Uses ROC (percent change) |
| **Skender** | N/A | Uses Slope regression |
| **Manual Calculation** | ✅ | Exact match |

## Common Pitfalls

1. **Noise Amplification**: First derivatives amplify high-frequency noise. A 1% price wiggle becomes a full slope reversal. Consider smoothing the input or output for noisy data.

2. **Scale Dependency**: SLOPE output depends on input scale. A $100 stock has 100× larger slopes than a $1 stock. Normalize if comparing across instruments.

3. **Warmup Period**: SLOPE requires 2 values to produce meaningful output. The first output is always 0.

4. **Using isNew Incorrectly**: When processing live ticks within the same bar, use `Update(value, isNew: false)`. When a new bar opens, use `isNew: true` (default).

5. **Memory Footprint**: ~48 bytes per instance. Negligible for most use cases.

## References

- Newton, Isaac. (1687). "Philosophiæ Naturalis Principia Mathematica."
- Numerical Methods: Finite Difference Approximations.