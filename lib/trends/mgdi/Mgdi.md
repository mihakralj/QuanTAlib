# MGDI: McGinley Dynamic Indicator

> "John McGinley saw moving averages failing in fast markets and said, 'It's not the market's fault, it's the math's fault.' MGDI is the apology."

MGDI (McGinley Dynamic Indicator) looks like a moving average, but it's actually a smoothing mechanism that adjusts itself relative to the speed of the market. It was designed to solve the problem of "lag" and "whipsaw" simultaneously by using a formula that automatically adjusts the smoothing factor based on the distance between the price and the average.

## Historical Context

Published by John McGinley in the *Market Technicians Association Journal* (1991), the Dynamic was created to be a "market tool" rather than just an indicator. McGinley argued that moving averages should not be fixed to a specific time period because the market's speed is not fixed.

## Architecture & Physics

The MGDI formula is unique. It looks like an EMA, but the smoothing constant is dynamic and depends on the ratio of Price to the previous MGDI value.

- **Price > MGDI**: The market is speeding up (or recovering). The denominator grows, slowing the adjustment to prevent overshoot.
- **Price < MGDI**: The market is falling. The formula adapts to hug the price without breaking.

## Mathematical Foundation

$$ \text{MGDI}_t = \text{MGDI}_{t-1} + \frac{P_t - \text{MGDI}_{t-1}}{k \times N \times (\frac{P_t}{\text{MGDI}_{t-1}})^4} $$

Where:

- $N$ is the period (roughly analogous to an EMA period).
- $k$ is a constant (usually 0.6).
- The term $(P_t / \text{MGDI}_{t-1})^4$ is the accelerator/decelerator.

## Performance Profile

This is one of the fastest adaptive indicators available.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | Scalar math |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 9/10 | Hugs price closely without breaking |
| **Timeliness** | 8/10 | Accelerates to catch up to price |
| **Overshoot** | 9/10 | Specifically designed to minimize overshoot |
| **Smoothness** | 9/10 | Visually pleasing, organic curve |

## Validation

Validated against standard definitions and TradingView implementations.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **TradingView** | $10^{-9}$ | Matches `mcginley` |

### Common Pitfalls

1. **Not an EMA**: Do not treat it like an EMA. It does not have a fixed alpha.
2. **Period Meaning**: The "Period" $N$ is a calibration constant, not a hard window size. An MGDI(14) does not "look back" 14 bars in the traditional sense; it's just calibrated to that timeframe.
3. **K Factor**: The constant $k=0.6$ is standard. Changing it changes the sensitivity.
