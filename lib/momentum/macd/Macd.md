# MACD: Moving Average Convergence Divergence

> "The trend is your friend, until it bends." — Ed Seykota

The Moving Average Convergence Divergence (MACD) is a trend-following momentum indicator that shows the relationship between two moving averages of a security's price. Developed by Gerald Appel in the late 1970s, it is one of the most popular and versatile indicators in technical analysis.

## Historical Context

Gerald Appel created the MACD to reveal changes in the strength, direction, momentum, and duration of a trend in a stock's price. It combines the lagging features of moving averages with the leading characteristics of momentum oscillators.

## Architecture & Physics

MACD is composed of three components:

1. **MACD Line**: The difference between a fast EMA and a slow EMA.
2. **Signal Line**: An EMA of the MACD Line.
3. **Histogram**: The difference between the MACD Line and the Signal Line.

* **Inertia**: Moderate (dependent on EMA periods).
* **Momentum**: Tracks the convergence/divergence of trends.
* **Range**: Unbounded.

## Mathematical Foundation

$$ \text{MACD Line} = \text{EMA}_{\text{fast}}(Close) - \text{EMA}_{\text{slow}}(Close) $$
$$ \text{Signal Line} = \text{EMA}_{\text{signal}}(\text{MACD Line}) $$
$$ \text{Histogram} = \text{MACD Line} - \text{Signal Line} $$

Standard parameters are (12, 26, 9):

* Fast EMA: 12 periods
* Slow EMA: 26 periods
* Signal EMA: 9 periods

## Performance Profile

MACD relies on efficient EMA calculations.

### Zero-Allocation Design

The implementation uses three internal `Ema` instances. The `Update` method orchestrates the flow of data between them without creating intermediate objects on the heap.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 26 ns/bar | High performance due to simple EMA calculations. |
| **Allocations** | 0 | Zero heap allocations in hot path. |
| **Complexity** | O(1) | Constant time update per bar. |
| **Accuracy** | 10/10 | Matches external standards exactly. |
| **Timeliness** | 8/10 | Lag is inherent to the moving averages used. |
| **Overshoot** | 5/10 | Can overshoot during strong trends. |
| **Smoothness** | 9/10 | Very smooth due to double smoothing (EMA of EMA). |

## Validation

Validated against multiple external libraries to ensure correctness.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_MACD` exactly. |
| **Skender** | ✅ | Matches `GetMacd` exactly. |
| **Tulip** | ✅ | Matches `macd` exactly. |
| **Ooples** | ✅ | Matches `CalculateMovingAverageConvergenceDivergence`. |

### Common Pitfalls

* **Lag**: As a trend-following indicator based on moving averages, MACD lags price action.
* **Whipsaws**: In sideways markets, MACD can generate false signals (whipsaws) as the moving averages cross frequently.
