# MODF: Modular Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `beta` (default 0.8), `feedback` (default false), `fbWeight` (default 0.5)                      |
| **Outputs**      | Single series (MODF)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [modf_signature](modf_signature.md) |

### TL;DR

- MODF is a dual-path adaptive filter that maintains separate upper and lower EMA bands with conditional state selection.
- Parameterized by `period`, `beta` (default 0.8), `feedback` (default false), `fbweight` (default 0.5).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "alexgrover designed a filter with two paths — one tracks uptrends, one tracks downtrends — and a state machine that picks between them. Add a beta knob for aggression and an optional feedback loop, and you get one of the most versatile adaptive filters on TradingView."

MODF is a dual-path adaptive filter that maintains separate upper and lower EMA bands with conditional state selection. The upper band snaps up to price when price exceeds it (tracking rallies), while the lower band snaps down when price drops below it (tracking selloffs). An oscillator state variable determines which band is active, and a beta parameter controls the blend between filter mode (smooth tracking) and trailing-stop mode (step-like following). An optional feedback loop blends the filter's output back into its input for additional smoothing. Developed by alexgrover (CPO at LuxAlgo).

## Historical Context

MODF was published by alexgrover on TradingView as a novel approach to adaptive filtering that combines elements of trailing stops, envelope filters, and state machines. The "modular" name refers to the composable design: the beta parameter morphs the filter continuously between two behaviors (smooth average at $\beta = 1$ and trailing stop at $\beta = 0$), and the feedback option adds a third dimension of control.

The dual-band architecture is reminiscent of Keltner channels and Donchian channels, where upper and lower bands track extremes. MODF's innovation is the conditional snap-to-price behavior: the upper band only updates via EMA when price is below it, but jumps instantly to price when price exceeds it. This creates a band that ratchets upward during trends and smoothly decays during pullbacks, the opposite of a trailing stop but with the same structural mechanism.

The state machine ($os = 1$ when price touches the upper band, $os = 0$ when it touches the lower band) provides regime detection without any lookback or explicit trend measurement. The filter naturally enters "bullish" (upper band active) or "bearish" (lower band active) mode based purely on which extreme price has most recently visited.

## Architecture & Physics

### 1. Dual EMA Bands

- **Upper band ($b$):** EMA of input, but snaps up to input when input exceeds EMA.
- **Lower band ($c$):** EMA of input, but snaps down to input when input falls below EMA.

### 2. Oscillator State

Binary state $os$: 1 if price last touched the upper band, 0 if it last touched the lower band.

### 3. Beta-Weighted Combination

$$
\text{upper\_mix} = \beta \cdot b + (1 - \beta) \cdot c
$$

$$
\text{lower\_mix} = \beta \cdot c + (1 - \beta) \cdot b
$$

### 4. State-Selected Output

$$
\text{MODF} = os \cdot \text{upper\_mix} + (1 - os) \cdot \text{lower\_mix}
$$

### 5. Optional Feedback

When enabled, the input becomes a blend of source and previous output:

$$
a = w \cdot \text{source} + (1 - w) \cdot \text{MODF}_{t-1}
$$

## Mathematical Foundation

With $\alpha = 2/(N+1)$:

**Band updates:**

$$
b_t = \begin{cases} a_t & \text{if } a_t > \alpha \cdot a_t + (1-\alpha) \cdot b_{t-1} \\ \alpha \cdot a_t + (1-\alpha) \cdot b_{t-1} & \text{otherwise} \end{cases}
$$

$$
c_t = \begin{cases} a_t & \text{if } a_t < \alpha \cdot a_t + (1-\alpha) \cdot c_{t-1} \\ \alpha \cdot a_t + (1-\alpha) \cdot c_{t-1} & \text{otherwise} \end{cases}
$$

**State transition:**

$$
os_t = \begin{cases} 1 & \text{if } a_t = b_t \\ 0 & \text{if } a_t = c_t \\ os_{t-1} & \text{otherwise} \end{cases}
$$

**Output:**

$$
\text{MODF}_t = os_t \cdot [\beta b_t + (1-\beta) c_t] + (1-os_t) \cdot [\beta c_t + (1-\beta) b_t]
$$

**Beta interpretation:**

| $\beta$ | Behavior |
| :---: | :--- |
| 1.0 | Pure filter: tracks active band smoothly |
| 0.5 | Balanced: midpoint of both bands |
| 0.0 | Pure trailing stop: follows inactive band |

**Default parameters:** `period = 14`, `beta = 0.8`, `feedback = false`, `fbWeight = 0.5`.

**Pseudo-code (streaming):**

```
alpha = 2/(period+1)

// Optional feedback blend
a = feedback ? fbWeight*src + (1-fbWeight)*ts : src

// Upper band (snaps up)
ema_b = alpha*a + (1-alpha)*b
b = (a > ema_b) ? a : ema_b

// Lower band (snaps down)
ema_c = alpha*a + (1-alpha)*c
c = (a < ema_c) ? a : ema_c

// State machine
os = (a == b) ? 1 : (a == c) ? 0 : os

// Beta-weighted output
upper = beta*b + (1-beta)*c
lower = beta*c + (1-beta)*b
ts = os*upper + (1-os)*lower
```


## Performance Profile

### Operation Count (Streaming Mode)

MODF maintains two conditional EMA bands (upper b, lower c) with snap-to-price logic, a binary state machine (os), beta-blend, and optional feedback. All per-bar work is O(1).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Upper band conditional snap + FMA | 1 | ~5 cy | ~5 cy |
| Lower band conditional snap + FMA | 1 | ~5 cy | ~5 cy |
| State machine update (os) | 1 | ~2 cy | ~2 cy |
| Beta-weighted blend (2x FMA) | 2 | ~4 cy | ~8 cy |
| Output select (os-conditional) | 1 | ~2 cy | ~2 cy |
| Feedback blend (optional, 1 FMA) | 1 | ~4 cy | ~4 cy |
| **Total** | **~7** | — | **~26 cycles** |

O(1) per bar. The conditional snap (max/min vs EMA) is a branchless `Math.Max`/`Math.Min` call. ~26 cycles/bar with feedback disabled; ~30 with feedback.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Upper/lower EMA recursion | No | Each band is a recursive IIR — sequential dependency |
| Snap-to-price conditionals | No | max(x, ema) depends on current ema which depends on prior bar |
| State machine | No | Binary state update is data-dependent |
| Beta blend | Yes | Scalar multiply-add on 2 values; negligible savings |

Fully recursive — no SIMD path available. Batch throughput: ~26-30 cy/bar scalar.

## Resources

- alexgrover (LuxAlgo). "Modular Filter" indicator. Published on TradingView.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 6: Adaptive Filters (general framework).
