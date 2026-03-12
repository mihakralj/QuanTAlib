# EDECAY: Exponential Decay

> *A ratchet that only moves down gradually: price can push it up instantly, but gravity pulls it back at an exponential pace — faster when far from zero, slower as it approaches.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numerics                         |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 5)             |
| **Outputs**      | Single series (Edecay)           |
| **Output range** | Same as input (overlay)          |
| **Warmup**       | `1` bar                          |
| **PineScript**   | [edecay.pine](edecay.pine)                       |

- EDECAY (Exponential Decay) tracks the maximum of the current input and the previous output multiplied by a decay factor of `(period-1)/period`.
- Parameterized by `period` (default 5).
- Output range: Same as input — this is an overlay indicator.
- Requires `1` bar of warmup before first valid output (IsHot = true).
- Validated against Tulip Indicators `ti_edecay` reference algorithm.

EDECAY implements the exponential decaying function. When price is above the decayed level, output snaps to price. When price falls below, the output decays exponentially by multiplying by `(period-1)/period` per bar, creating a ceiling that gradually descends. Unlike linear DECAY which subtracts a fixed amount, EDECAY's multiplicative factor produces a proportional decay rate.

## Historical Context

The exponential decay indicator originates from the Tulip Indicators library, a high-performance C library of technical indicators. It provides a peak-tracking mechanism where the tracked level decays at a proportional rate. The indicator is useful for:

- **Trailing stops**: The decaying level acts as a trailing stop that descends proportionally.
- **Peak detection**: Identifies when price last reached a new high relative to the decay rate.
- **Signal filtering**: Removes noise by requiring price to exceed the decayed level to register as significant.

## Architecture & Physics

### 1. Pure IIR (No Buffer)

The indicator requires no history buffer — only the previous output value is needed:

$$
\text{state} = \{y_{t-1}\}
$$

This makes it O(1) in both time and space.

### 2. Exponential Decay Calculation

$$
y_t = \max(x_t, \; y_{t-1} \cdot \frac{p-1}{p})
$$

where:
- $x_t$ = current input value
- $y_{t-1}$ = previous output value
- $p$ = period parameter
- $\frac{p-1}{p}$ = multiplicative decay factor per bar

### 3. First Bar Initialization

$$
y_0 = x_0
$$

The first bar simply passes through the input value.

### 4. State Management

The indicator uses state rollback for bar correction:

```
if isNew:
    save current state as previous
else:
    restore previous state
```

## Mathematical Foundation

### Core Formula

$$
y_t = \max(x_t, \; y_{t-1} \cdot s)
$$

where $s = \frac{p-1}{p}$ is the multiplicative decay factor.

### Decay Behavior

After a peak at value $v$, with no new inputs exceeding the decayed level, the output follows:

$$
y_{t+k} = v \cdot s^k = v \cdot \left(\frac{p-1}{p}\right)^k
$$

The output asymptotically approaches zero but never reaches it ($v > 0$).

### Comparison with Linear Decay

| Property | DECAY (Linear) | EDECAY (Exponential) |
|----------|----------------|---------------------|
| Formula | $y - \frac{1}{p}$ | $y \cdot \frac{p-1}{p}$ |
| Decay rate | Constant absolute | Proportional to current value |
| Reaches zero | Yes, in finite time | No, asymptotic approach |
| Scale-invariant | No | Yes |

### Properties

| Property | Value |
|----------|-------|
| Lookback | 0 |
| Output ≥ Input | Always (by construction) |
| Decay rate | Proportional $\frac{p-1}{p}$ |
| Monotonic when decaying | Yes (strictly decreasing) |
| Scale-invariant | Yes |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| MUL | 1 | prev_output × scale |
| MAX/CMP | 1 | max(input, decayed) |
| State copy | 1 | rollback support |
| **Total** | **~3 ops** | Extremely lightweight |

### Batch Mode (Span-based)

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Mul + compare |
| Total | O(n) | Linear scan |
| Memory | O(1) | No additional allocation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic, no approximation |
| **Timeliness** | 10/10 | Zero lag on upward moves |
| **Smoothness** | 3/10 | Exponential curve smoother than linear staircase |
| **Simplicity** | 10/10 | Single multiplication + compare |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Tulip** | ✅ | Manual ti_edecay algorithm matches exactly |

## Common Pitfalls

1. **Not a moving average**: Edecay is a peak-tracking/envelope indicator, not a smoothing filter. It only descends when price is below the decayed level.

2. **Proportional decay rate**: Unlike linear DECAY, EDECAY decays proportionally. For a stock at $100 with period=5, the first bar decays by $20; for a stock at $10, it decays by $2. This makes EDECAY scale-invariant.

3. **Period interpretation**: Period=5 means `scale = 4/5 = 0.8`, so each bar retains 80% of the previous value. After 5 bars, approximately 32.8% of the peak value remains.

4. **First bar**: The first bar always equals the input — there is no warmup period in the traditional sense.

5. **Asymmetric behavior**: Upward moves are instant (output = input), but downward moves are rate-limited to multiplication by `(period-1)/period` per bar.

6. **Never reaches zero**: Unlike linear DECAY, exponential decay asymptotically approaches zero but never reaches it (assuming positive values).

## References

- Tulip Indicators Library: https://tulipindicators.org/edecay
- Kegel, L. "Tulip Indicators" — Open-source C library of technical indicators.
