# UltOsc: Ultimate Oscillator

> "Why use one timeframe when three can save you from yourself?"

The Ultimate Oscillator is Larry Williams' answer to the fundamental flaw of single-period momentum oscillators: they whipsaw. By combining buying pressure across three distinct timeframes with a weighted average, UltOsc filters out the noise that traps traders who rely on RSI or Stochastics alone.

The indicator oscillates between 0 and 100. Readings above 70 suggest overbought conditions; readings below 30 suggest oversold. But the real power lies in **divergence detection**: when price makes a new high but UltOsc does not, the trend is exhausted.

## Historical Context

Larry Williams introduced the Ultimate Oscillator in his 1985 article for *Technical Analysis of Stocks & Commodities* magazine. Williams, a legendary trader who famously turned \$10,000 into over \$1 million in a single year of trading, designed UltOsc to solve a specific problem.

Single-period oscillators like RSI suffer from two fatal flaws:

1. **False signals during trends**: In a strong uptrend, RSI can stay overbought for weeks, generating endless "sell" signals.
2. **Period sensitivity**: A 7-period RSI behaves differently from a 14-period RSI. Which one is "right"?

Williams' solution was elegant: use three periods (7, 14, 28) and weight them so the shortest period has the most influence (4:2:1). This gives responsiveness to recent price action while still respecting the broader context.

## Architecture & Physics

UltOsc is built on two core concepts: **Buying Pressure (BP)** and **True Range (TR)**.

### Buying Pressure

Buying Pressure measures how much of today's price movement was "bought." It is the distance from the True Low (the lower of today's Low or yesterday's Close) to today's Close.

$$
BP = Close - TrueLow
$$

If the close is at the high of the day, BP is maximized. If the close is at the low, BP is zero.

### True Range

True Range captures the full volatility of the day, including overnight gaps.

$$
TR = TrueHigh - TrueLow
$$

Where:

- $TrueHigh = \max(High, Close_{t-1})$
- $TrueLow = \min(Low, Close_{t-1})$

### The Multi-Timeframe Fusion

For each of the three periods, UltOsc calculates the ratio of accumulated Buying Pressure to accumulated True Range:

$$
Avg_n = \frac{\sum_{i=1}^{n} BP_i}{\sum_{i=1}^{n} TR_i}
$$

This ratio represents the "efficiency" of buying over that period. A value of 1.0 means all volatility was captured by buyers; 0.0 means sellers dominated.

The final oscillator applies a 4:2:1 weighting:

$$
UltOsc = 100 \times \frac{4 \times Avg_7 + 2 \times Avg_{14} + 1 \times Avg_{28}}{4 + 2 + 1}
$$

## Mathematical Foundation

### 1. True Low and True High

$$
TrueLow_t = \min(Low_t, Close_{t-1})
$$

$$
TrueHigh_t = \max(High_t, Close_{t-1})
$$

### 2. Buying Pressure and True Range

$$
BP_t = Close_t - TrueLow_t
$$

$$
TR_t = TrueHigh_t - TrueLow_t
$$

### 3. Period Averages

For periods $n_1 = 7$, $n_2 = 14$, $n_3 = 28$:

$$
Avg_n = \frac{\sum_{i=t-n+1}^{t} BP_i}{\sum_{i=t-n+1}^{t} TR_i}
$$

### 4. Ultimate Oscillator

$$
UltOsc = 100 \times \frac{4 \cdot Avg_7 + 2 \cdot Avg_{14} + 1 \cdot Avg_{28}}{7}
$$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 8 | Moderate; requires six running sums (BP and TR for each period). |
| **Allocations** | 0 | Zero-allocation in hot paths using ring buffers. |
| **Complexity** | O(1) | Constant time via running sums. |
| **Accuracy** | 10 | Matches TA-Lib and Skender exactly. |
| **Timeliness** | 6 | Balanced; short-period weighting provides responsiveness. |
| **Overshoot** | 2 | Bounded to [0, 100]; minimal overshoot by design. |
| **Smoothness** | 7 | Multi-period averaging provides inherent smoothing. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_ULTOSC` exactly. |
| **Skender** | ✅ | Matches `GetUltimate` exactly. |
| **Tulip** | ✅ | Matches `ultosc` exactly. |
| **Ooples** | ⚠️ | Minor deviations in warmup period handling. |

### Trading Signals

Williams outlined specific rules for trading UltOsc:

1. **Bullish Divergence**: Price makes a lower low, UltOsc makes a higher low (UltOsc < 30).
2. **Breakout Confirmation**: After divergence, UltOsc breaks above the divergence high.
3. **Exit**: UltOsc reaches 70, or price hits target.

### Common Pitfalls

- **Ignoring Divergence**: UltOsc is designed for divergence trading. Using it as a simple overbought/oversold indicator misses the point.
- **Wrong Timeframes**: The default 7/14/28 works for daily charts. For intraday, consider scaling down proportionally.
- **Trending Markets**: Like all oscillators, UltOsc struggles in strong trends. Use trend filters (ADX, moving averages) to avoid fighting the tide.
- **Division by Zero**: If True Range is zero (flat line), the ratio is undefined. QuanTAlib handles this by returning 0.5 (neutral).
