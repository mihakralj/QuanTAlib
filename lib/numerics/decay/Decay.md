# DECAY: Linear Decay

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numerics                         |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 5)             |
| **Outputs**      | Single series (Decay)            |
| **Output range** | Same as input (overlay)          |
| **Warmup**       | `1` bar                          |

### TL;DR

- DECAY (Linear Decay) tracks the maximum of the current input and the previous output minus a fixed absolute step of `1/period`.
- Parameterized by `period` (default 5).
- Output range: Same as input — this is an overlay indicator.
- Requires `1` bar of warmup before first valid output (IsHot = true).
- Validated against Tulip Indicators `ti_decay` reference algorithm.

> "A ratchet that only moves down slowly: price can push it up instantly, but gravity pulls it back at a steady, linear pace."

DECAY implements the Tulip Indicators `ti_decay` function. When price is above the decayed level, output snaps to price. When price falls below, the output decays linearly at a rate of `1/period` per bar, creating a ceiling that gradually descends. This produces a one-sided envelope that hugs price from above.

## Historical Context

The linear decay indicator originates from the Tulip Indicators library, a high-performance C library of technical indicators. It provides a simple peak-tracking mechanism where the tracked level decays at a constant absolute rate. The indicator is useful for:

- **Trailing stops**: The decaying level acts as a simple trailing stop that descends at a fixed rate.
- **Peak detection**: Identifies when price last reached a new high relative to the decay rate.
- **Signal filtering**: Removes noise by requiring price to exceed the decayed level to register as significant.

## Architecture & Physics

### 1. Pure IIR (No Buffer)

The indicator requires no history buffer — only the previous output value is needed:

$$
\text{state} = \{y_{t-1}\}
$$

This makes it O(1) in both time and space.

### 2. Linear Decay Calculation

$$
y_t = \max(x_t, \; y_{t-1} - \frac{1}{p})
$$

where:
- $x_t$ = current input value
- $y_{t-1}$ = previous output value
- $p$ = period parameter
- $\frac{1}{p}$ = fixed decay step per bar

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
y_t = \max(x_t, \; y_{t-1} - s)
$$

where $s = \frac{1}{p}$ is the fixed linear decay rate.

### Decay Behavior

After a peak at value $v$, with no new inputs exceeding the decayed level, the output follows:

$$
y_{t+k} = v - k \cdot s
$$

reaching zero after $k = v \cdot p$ bars (assuming $v > 0$).

### Properties

| Property | Value |
|----------|-------|
| Lookback | 0 |
| Output ≥ Input | Always (by construction) |
| Decay rate | Constant absolute $\frac{1}{p}$ |
| Monotonic when decaying | Yes (strictly decreasing) |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | prev_output - scale |
| MAX/CMP | 1 | max(input, decayed) |
| State copy | 1 | rollback support |
| **Total** | **~3 ops** | Extremely lightweight |

### Batch Mode (Span-based)

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Sub + compare |
| Total | O(n) | Linear scan |
| Memory | O(1) | No additional allocation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic, no approximation |
| **Timeliness** | 10/10 | Zero lag on upward moves |
| **Smoothness** | 2/10 | No smoothing — linear staircase |
| **Simplicity** | 10/10 | Single subtraction + compare |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Tulip** | ✅ | Manual ti_decay algorithm matches exactly |

## Common Pitfalls

1. **Not a moving average**: Decay is a peak-tracking/envelope indicator, not a smoothing filter. It only descends when price is below the decayed level.

2. **Absolute decay rate**: The decay step is `1/period` in absolute terms, regardless of price level. For a stock at $100 with period=5, the decay is $0.20/bar; for a stock at $10, it's the same $0.20/bar. Consider normalizing if comparing across instruments.

3. **Period interpretation**: Period=5 means the output decays by 1.0 over 5 bars (0.2 per bar), not that it looks back 5 bars.

4. **First bar**: The first bar always equals the input — there is no warmup period in the traditional sense.

5. **Asymmetric behavior**: Upward moves are instant (output = input), but downward moves are rate-limited to `1/period` per bar.

## References

- Tulip Indicators Library: https://tulipindicators.org/decay
- Kegel, L. "Tulip Indicators" — Open-source C library of technical indicators.
