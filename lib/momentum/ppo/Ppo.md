# PPO: Percentage Price Oscillator

> *MACD told you the spread in dollars. PPO tells you the spread in percent. One of those actually works across instruments.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `fastPeriod` (default 12), `slowPeriod` (default 26), `signalPeriod` (default 9)                      |
| **Outputs**      | Multiple series (Signal, Histogram)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `slowPeriod + signalPeriod` bars (35 default)                          |
| **PineScript**   | [ppo.pine](ppo.pine)                       |

- PPO (Percentage Price Oscillator) measures the percentage difference between a fast EMA and a slow EMA.
- Parameterized by `fastPeriod` (default 12), `slowPeriod` (default 26), `signalPeriod` (default 9).
- Output range: Varies (see docs).
- Requires `slowPeriod + signalPeriod` bars (35 default) of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

PPO (Percentage Price Oscillator) measures the percentage difference between a fast EMA and a slow EMA. It is functionally equivalent to MACD normalized by the slow EMA, producing values that are comparable across instruments with different price levels. The implementation outputs three components: the PPO line, a signal line (EMA of PPO), and a histogram (PPO minus Signal).

## Historical Context

PPO emerged as a direct answer to MACD's most significant architectural limitation: scale dependency. Gerald Appel's MACD (1979) reports the absolute spread between two EMAs, meaning a MACD value of 2.0 on a \$200 stock represents a 1% divergence, while the same value on a \$20 stock represents 10%. PPO normalizes this by dividing by the slow EMA, producing a percentage that is directly comparable across any price level.

The formula appears in most technical analysis textbooks as the "normalized MACD" or "percentage MACD." StockCharts.com popularized the PPO terminology. The default parameters (12, 26, 9) mirror MACD's defaults, making PPO a drop-in replacement for cross-instrument analysis.

This implementation uses compensated EMAs internally (via the `Ema` class) for improved warmup accuracy, and applies FMA where applicable for performance.

## Architecture & Physics

### 1. Dual EMA Pipeline

Two independent EMA instances process the same input:

$$
\text{FastEMA}_t = \text{EMA}(P_t, \text{fastPeriod})
$$

$$
\text{SlowEMA}_t = \text{EMA}(P_t, \text{slowPeriod})
$$

### 2. Percentage Normalization

$$
\text{PPO}_t = 100 \times \frac{\text{FastEMA}_t - \text{SlowEMA}_t}{\text{SlowEMA}_t}
$$

Division by SlowEMA normalizes the result to a percentage. When SlowEMA is zero (startup edge case), the result defaults to 0.0.

### 3. Signal and Histogram

$$
\text{Signal}_t = \text{EMA}(\text{PPO}_t, \text{signalPeriod})
$$

$$
\text{Histogram}_t = \text{PPO}_t - \text{Signal}_t
$$

### 4. State Management

The indicator delegates state management to three internal `Ema` instances. The `_state` / `_p_state` pattern handles only the `LastValid` value for NaN sanitization.

## Mathematical Foundation

### Core Formulas

$$
\text{PPO}_t = 100 \times \frac{\text{EMA}(P, f)_t - \text{EMA}(P, s)_t}{\text{EMA}(P, s)_t}
$$

where $f$ = fast period, $s$ = slow period.

### Relationship to MACD

| Property | MACD | PPO |
|----------|------|-----|
| Formula | $\text{Fast} - \text{Slow}$ | $100 \times \frac{\text{Fast} - \text{Slow}}{\text{Slow}}$ |
| Units | Price units | Percentage |
| Cross-instrument | No | Yes |
| Zero crossover | Identical timing | Identical timing |
| Signal crossover | Same concept | Same concept |

### Conversion

$$
\text{PPO} = \frac{\text{MACD}}{\text{SlowEMA}} \times 100
$$

### Default Parameters

| Parameter | Default | Purpose |
|-----------|---------|---------|
| fastPeriod | 12 | Fast EMA period |
| slowPeriod | 26 | Slow EMA period |
| signalPeriod | 9 | Signal line EMA period |

### Constraints

- `fastPeriod >= 1`
- `slowPeriod >= 1`
- `fastPeriod < slowPeriod` (enforced in constructor)
- `signalPeriod >= 1`

### Warmup

$$
\text{WarmupPeriod} = \text{slowPeriod} + \text{signalPeriod}
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| EMA updates | 3 | fast + slow + signal |
| SUB | 2 | fast-slow, ppo-signal |
| DIV | 1 | normalization by slow |
| MUL | 1 | scale to percentage |
| **Total** | **~7 ops** | Plus internal EMA ops |

### Batch Mode (Span-based)

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Fixed operations per bar |
| Total | O(n) | Linear scan |
| Memory | O(1) | Internal EMA state only |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Compensated EMA for warmup precision |
| **Timeliness** | 6/10 | EMA lag from both smoothing stages |
| **Smoothness** | 7/10 | Dual EMA provides good noise rejection |
| **Simplicity** | 7/10 | Straightforward composition of EMAs |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | Matches within 1e-9 tolerance |
| **TA-Lib** | ✅ | PPO function matches |
| **Tulip** | ✅ | PPO matches exactly |
| **Ooples** | ✅ | Matches within 1e-6 tolerance |

## Common Pitfalls

1. **Period ordering**: `fastPeriod` must be strictly less than `slowPeriod`. The constructor enforces this with an `ArgumentException`.

2. **Division by zero**: When SlowEMA is zero (only during initial startup), the PPO defaults to 0.0. This is a transient condition that resolves after the first few bars.

3. **Signal vs PPO**: The histogram (`PPO - Signal`) is the derivative of momentum. Histogram shrinking toward zero indicates momentum deceleration, not necessarily a reversal.

4. **Warmup asymmetry**: The fast EMA becomes hot before the slow EMA. `IsHot` requires both EMAs to be warmed up, which depends on the slow period.

5. **Three outputs**: PPO exposes `Last` (PPO line), `Signal`, and `Histogram` as separate `TValue` properties. Consumers must access the appropriate property for their use case.

6. **MACD equivalence**: PPO zero crossovers occur at exactly the same points as MACD zero crossovers. The only difference is the vertical scale.

## References

- Appel, G. (2005). "Technical Analysis: Power Tools for Active Investors." FT Press.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- StockCharts.com: "Percentage Price Oscillator (PPO)" Technical Analysis documentation.
- TA-Lib documentation: PPO function reference.
