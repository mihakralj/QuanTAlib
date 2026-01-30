# TWAP: Time Weighted Average Price

> "Equal time, equal weight—the simplest benchmark refuses to let any single moment dominate the conversation." — Anonymous Quant

Time Weighted Average Price (TWAP) calculates the average price over a period by giving equal weight to each price point, regardless of volume. Unlike VWAP which emphasizes high-volume periods, TWAP treats every moment as equally important. This makes it a pure temporal benchmark—ideal for evaluating execution quality when volume patterns could bias the analysis.

The elegance of TWAP lies in its simplicity: accumulate prices, count observations, divide. No volume weighting, no complex adjustments. Just a running average that answers the question: "What was the typical price during this period?"

## Historical Context

TWAP emerged from the world of algorithmic trading in the 1990s alongside its volume-weighted sibling, VWAP. While VWAP became the dominant benchmark for evaluating trade execution, TWAP filled a crucial niche:

- Markets with unreliable or absent volume data (forex, some futures)
- Situations where volume manipulation could skew benchmarks
- Academic studies requiring volume-agnostic price measurements
- Low-liquidity instruments where volume spikes create VWAP distortions

The indicator gained renewed interest with the rise of cryptocurrency trading, where volume data quality varies dramatically across exchanges. A TWAP benchmark remains consistent regardless of reported volume, making it valuable for cross-exchange comparisons.

TWAP also serves as the basis for TWAP execution algorithms—strategies that break large orders into equal slices executed at regular intervals, aiming to achieve the time-weighted average price while minimizing market impact.

## Architecture & Physics

TWAP operates as a simple accumulator with optional periodic resets. The state tracks a running sum of prices and a count of observations.

### Component Breakdown

1. **Price Accumulation**: Sum of all prices in the current session
2. **Count Tracking**: Number of observations accumulated
3. **Period Management**: Optional reset at specified intervals
4. **Average Calculation**: Sum divided by count

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| SumPrices | double | Running sum of prices in session |
| Count | int | Number of prices accumulated |
| Index | int | Bar counter for period resets |
| LastValid | double | Fallback for NaN/Infinity handling |
| Twap | double | Current TWAP value |

### Session Reset Behavior

The period parameter controls session boundaries:

- **Period = 0**: Never reset; continuous average from start
- **Period > 0**: Reset sum and count every N bars

Session resets are critical for intraday benchmarking where you want fresh TWAP calculations for each trading session rather than a cumulative average across days.

## Mathematical Foundation

### Running Average Formula

$$
TWAP_t = \frac{\sum_{i=1}^{n} P_i}{n}
$$

where:

- $P_i$ = Price at observation $i$
- $n$ = Number of observations

### Incremental Update (Streaming)

$$
Sum_t = Sum_{t-1} + P_t
$$

$$
Count_t = Count_{t-1} + 1
$$

$$
TWAP_t = \frac{Sum_t}{Count_t}
$$

### With Period Reset

At bar $t$ where $t \mod period = 1$ (first bar of new session):

$$
Sum_t = P_t
$$

$$
Count_t = 1
$$

$$
TWAP_t = P_t
$$

### Price Source

For TBar input, the typical price (HLC3) is used:

$$
P_t = \frac{High_t + Low_t + Close_t}{3}
$$

This provides a better representation of average trading price than using close alone.

## TWAP vs VWAP Comparison

| Aspect | TWAP | VWAP |
| :--- | :--- | :--- |
| Weighting | Equal per observation | Volume-proportional |
| Volume data required | No | Yes |
| Sensitivity to spikes | Time-based only | Volume and price |
| Manipulation resistance | Higher | Lower (volume can be faked) |
| Formula | $\frac{\sum P}{n}$ | $\frac{\sum (P \times V)}{\sum V}$ |
| Use case | Time-based benchmarks | Volume-based benchmarks |

### When TWAP > VWAP

High volume concentrated at lower prices during the session. Interpretation: early buying pressure (accumulation) occurred at cheaper levels.

### When TWAP < VWAP

High volume concentrated at higher prices during the session. Interpretation: buying pressure came at elevated prices (late to the move).

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD | 3 | HLC3 calculation + sum update |
| DIV | 2 | HLC3 calculation + TWAP |
| CMP | 1 | Period boundary check |
| INC | 2 | Count and index increments |
| **Total** | 8 | Per bar, O(1) |

TWAP is one of the simplest indicators computationally—no lookback buffer, no complex mathematics.

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| HLC3 calculation | ✅ | Fully parallel |
| Price accumulation | ❌ | Sequential dependency (running sum) |
| Count tracking | ❌ | Sequential increment |
| Division | ❌ | Depends on running count |

The running sum dependency limits SIMD optimization. However, the HLC3 preprocessing step can be vectorized when processing bar data.

### Memory Footprint

| Scope | Size |
| :--- | :--- |
| Per instance | 56 bytes (State record struct × 2) |
| Buffer requirements | None (O(1) state) |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic computation |
| **Timeliness** | 8/10 | First bar valid; no warmup |
| **Smoothness** | 9/10 | Inherently smoothed by averaging |
| **Noise Filtering** | 6/10 | Moderate; better with more observations |
| **Memory** | 10/10 | O(1) constant regardless of history |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented (VWAP variants only) |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation available |

TWAP is straightforward enough that validation focuses on internal consistency between streaming, batch, and span modes (verified with 1e-9 tolerance) and formula correctness against manual calculations.

## Common Pitfalls

1. **Period Selection**: For intraday trading, set period to match your session length (e.g., 390 for regular US equity session in 1-minute bars). Period = 0 creates a cumulative average that becomes increasingly stable—useful for long-term benchmarks but less responsive for intraday analysis.

2. **HLC3 vs Close**: TWAP uses typical price (HLC3), not close. This better represents the average traded price within each bar but may differ from close-only implementations in other platforms.

3. **Initial Value**: The first bar's TWAP equals that bar's typical price. Unlike moving averages, there's no "warmup" period where values are unreliable.

4. **Comparing Across Sessions**: TWAP values are only meaningful within their session context. Comparing TWAP from yesterday to TWAP from today without considering the reset boundary leads to incorrect conclusions.

5. **TValue Limitations**: When using `Update(TValue)`, you're providing a single price rather than OHLC data. The implementation uses this price directly. For proper TWAP from bar data, use `Update(TBar)`.

6. **Cumulative Nature**: With period = 0, TWAP becomes increasingly stable as more observations accumulate. After 1000 bars, a new bar changes TWAP by only ~0.1%. Consider whether you need this stability or session-based freshness.

7. **Reset Timing**: Period resets occur when the bar count exceeds the period. With period = 5, the 6th bar starts a new session. The reset is on boundary crossing, not modular arithmetic.

8. **isNew Parameter**: Bar correction (isNew = false) properly restores state including accumulated sum and count. Incorrect implementation causes cumulative drift in TWAP values.

## Interpretation Guide

### Execution Quality Analysis

| Execution Price vs TWAP | Interpretation |
| :--- | :--- |
| Buy below TWAP | Good execution (bought cheaper than average) |
| Buy above TWAP | Poor execution (paid premium) |
| Sell above TWAP | Good execution (sold higher than average) |
| Sell below TWAP | Poor execution (sold at discount) |

### Trend Analysis

| Price Position | Market State |
| :--- | :--- |
| Price consistently above TWAP | Bullish session; buyers dominating |
| Price consistently below TWAP | Bearish session; sellers dominating |
| Price oscillating around TWAP | Range-bound; equilibrium |
| Price diverging from TWAP | Trend acceleration |

### TWAP as Support/Resistance

In intraday trading, TWAP often acts as dynamic support/resistance:

- Uptrend: TWAP provides support; pullbacks to TWAP are buying opportunities
- Downtrend: TWAP provides resistance; rallies to TWAP are selling opportunities
- Range: Price reverts to TWAP; fade moves away from it

### Algorithmic Execution Benchmark

For TWAP execution algorithms:

- **Slippage** = Actual Avg Price - TWAP
- **Positive slippage** (for buys): Paid more than benchmark
- **Negative slippage** (for buys): Paid less than benchmark

Target: Minimize absolute slippage to achieve the unbiased average price.

## Parameter Selection Guide

| Use Case | Period Setting | Rationale |
| :--- | :--- | :--- |
| Intraday benchmarking | Session length | Fresh TWAP each session |
| Multi-day analysis | 0 (continuous) | Cumulative average |
| Hourly benchmarks | 60 (for 1-min bars) | Reset every hour |
| Weekly analysis | Bars per week | Weekly TWAP cycles |
| Custom intervals | As needed | Match your trading horizon |

### Session Length Examples

| Market | Bars per Session (1-min) |
| :--- | :--- |
| US Equities (Regular) | 390 |
| US Futures (23-hour) | 1380 |
| Forex (24-hour) | 1440 |
| Crypto (24-hour) | 1440 |

## References

- Almgren, R., & Chriss, N. (2001). "Optimal Execution of Portfolio Transactions." *Journal of Risk*.
- Berkowitz, S., Logue, D., & Noser, E. (1988). "The Total Cost of Transactions on the NYSE." *Journal of Finance*.
- Kissell, R., & Glantz, M. (2003). *Optimal Trading Strategies*. AMACOM.
- TradingView. "PineScript TWAP Implementation." Community Scripts.