# SUPER: SuperTrend

> "It's not an indicator; it's a trailing stop with a marketing budget. Perfect for traders who want to catch the trend but lack the emotional discipline to hold on."

SuperTrend is a trend-following indicator that overlays the price chart. It uses the Average True Range (ATR) to calculate upper and lower volatility bands, switching between them based on the direction of the closing price. It effectively functions as a trailing stop-loss that adapts to market volatility.

## Historical Context

Created by Olivier Seban. It gained massive popularity in the retail trading community for its visual simplicity: Green line = Buy, Red line = Sell. It combines the volatility measurement of Wilder's ATR with a simple breakout logic.

## Architecture & Physics

SuperTrend is a state machine. It maintains two theoretical bands (Upper and Lower) and a boolean state (`IsBullish`).

### The Ratchet Mechanism

The bands act as a ratchet:

* **Bullish Mode**: The Lower Band (Stop Loss) can only move up. If the calculated Lower Band drops, the indicator ignores it and keeps the previous value.
* **Bearish Mode**: The Upper Band (Stop Loss) can only move down.

The trend flips when the Close price crosses the active band.

## Mathematical Foundation

### 1. Basic Bands

$$ Upper_{basic} = \frac{High + Low}{2} + (Multiplier \times ATR) $$
$$ Lower_{basic} = \frac{High + Low}{2} - (Multiplier \times ATR) $$

### 2. Ratchet Logic (Bullish Example)

$$ Lower_{final} = \begin{cases} Lower_{basic} & \text{if } Lower_{basic} > Lower_{prev} \text{ or } Close_{prev} < Lower_{prev} \\ Lower_{prev} & \text{otherwise} \end{cases} $$

### 3. Trend Logic

$$ SuperTrend = \begin{cases} Lower_{final} & \text{if Bullish} \\ Upper_{final} & \text{if Bearish} \end{cases} $$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 9 | High; O(1) calculation with minimal overhead. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time regardless of period. |
| **Accuracy** | 10 | Matches standard implementations exactly. |
| **Timeliness** | 5 | Lag depends on ATR period and multiplier. |
| **Overshoot** | 0 | Bands are constrained by price action. |
| **Smoothness** | 2 | Step-like behavior; not a smooth curve. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | N/A | Not implemented. |
| **Skender** | ✅ | Matches `GetSuperTrend` exactly. |
| **Tulip** | N/A | Not implemented. |
| **Ooples** | ❌ | Diverges significantly due to initialization logic. |

### Common Pitfalls

1. **Repainting**: SuperTrend does not repaint historical values, but the current bar's value can flip back and forth until the Close is finalized.
2. **Whipsaws**: In ranging markets, SuperTrend will generate frequent false signals, buying the top and selling the bottom. It requires a trend filter (like ADX).
3. **ATR Warmup**: The indicator requires $N$ bars to stabilize the ATR before the bands become accurate.
