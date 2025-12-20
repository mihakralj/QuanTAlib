# KAMA: Kaufman's Adaptive Moving Average

> "Perry Kaufman asked a simple question: 'Why should I use the same smoothing in a trending market as in a chopping market?' KAMA is the answer."

KAMA (Kaufman's Adaptive Moving Average) is an intelligent moving average that adjusts its smoothing speed based on market noise. When the price is moving steadily (high signal-to-noise ratio), KAMA speeds up to capture the trend. When the price is chopping sideways (low signal-to-noise ratio), KAMA slows down to filter out the noise.

## Historical Context

Perry Kaufman introduced KAMA in his book *Smarter Trading* (1998). It was one of the first widely adopted adaptive indicators, solving the problem of "whipsaws" in sideways markets without sacrificing responsiveness in trends.

## Architecture & Physics

KAMA uses an **Efficiency Ratio (ER)** to drive the smoothing constant of an EMA.

1. **Efficiency Ratio (ER)**: Measures the fractal efficiency of price movement.
    - $ER = \frac{\text{Net Change}}{\text{Sum of Absolute Changes}}$
    - ER approaches 1.0 in a straight line trend.
    - ER approaches 0.0 in pure noise.
2. **Smoothing Constant (SC)**: Scales between a "Fast" EMA (e.g., 2-period) and a "Slow" EMA (e.g., 30-period) based on ER.

### Zero-Allocation Design

Our implementation is efficient and allocation-free.

- **RingBuffer**: Stores the price history needed for the ER calculation (Period + 1).
- **Incremental Volatility**: We update the volatility sum incrementally (subtracting the exiting difference, adding the entering difference) to keep complexity O(1).

## Mathematical Foundation

$$ ER = \frac{|P_t - P_{t-n}|}{\sum_{i=0}^{n-1} |P_{t-i} - P_{t-i-1}|} $$

$$ SC = \left( ER \times (\text{FastAlpha} - \text{SlowAlpha}) + \text{SlowAlpha} \right)^2 $$

$$ \text{KAMA}_t = \text{KAMA}_{t-1} + SC \times (P_t - \text{KAMA}_{t-1}) $$

Note the squaring of the SC, which suppresses the response to noise even further.

## Performance Profile

KAMA is very efficient, with O(1) complexity thanks to the incremental volatility update.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | O(1) updates |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 7/10 | Flattens in noise, tracks in trends |
| **Timeliness** | 8/10 | Accelerates quickly in strong trends |
| **Overshoot** | 9/10 | Very stable in sideways markets |
| **Smoothness** | 8/10 | Aggressive noise filtering |

## Validation

Validated against TA-Lib and Skender.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | $10^{-9}$ | Matches `TA_KAMA` |
| **Skender** | $10^{-9}$ | Matches `GetKama` |

### Common Pitfalls

1. **Flatlining**: In very choppy markets, KAMA can become almost horizontal. This is a feature, not a bug—it's telling you to stay out.
2. **Parameters**: The standard settings are (10, 2, 30). 10 is the ER period, 2 is the fast EMA, 30 is the slow EMA. Tweaking the ER period changes the sensitivity to noise.
3. **Trend Following**: KAMA is excellent for trailing stops because it flattens out when momentum stalls.
