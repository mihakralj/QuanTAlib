# ATRN: Average True Range Normalized

> "Context is everything. A \$5 ATR means nothing until you know the \$5 ATR from last month was \$2."

ATRN transforms the absolute ATR into a relative measure by normalizing it to a [0,1] scale using min-max scaling over a lookback window. This answers the question: "Is current volatility high or low *compared to recent history*?"

While ATR tells you *how much* an asset moves, ATRN tells you *how unusual* that movement is relative to the asset's own recent behavior. A value near 1 means volatility is at its recent high; a value near 0 means volatility is at its recent low; 0.5 means volatility is average.

## Historical Context

ATRN is a practical extension of Wilder's ATR, developed to solve the **context problem** in volatility analysis. Raw ATR values are meaningless in isolation—you need to compare them to something. Some traders compare ATR to price (ATRP/NATR), which gives a percentage. ATRN takes a different approach: it compares ATR to its own recent range.

This normalization approach is common in machine learning and signal processing, where inputs are scaled to [0,1] for better model performance. ATRN applies the same principle to volatility measurement.

## Architecture & Physics

ATRN is built on three components:

1. **True Range (TR)**: Captures the full range of price movement including gaps.
2. **RMA Smoothing**: Wilder's exponential average ($\alpha = 1/N$) to smooth TR into ATR.
3. **Min-Max Normalization**: Scales ATR to [0,1] over a lookback window.

### The Lookback Window

The lookback window is set to $10 \times period$. For the default period of 14:
- Lookback = 140 bars
- This captures roughly 6-7 months of daily data
- Provides stable min/max anchors while remaining responsive to regime changes

### Edge Case: Constant Volatility

When max ATR equals min ATR (perfectly constant volatility), the denominator becomes zero. ATRN returns 0.5 in this case—the midpoint—indicating "average" volatility by default.

## Mathematical Foundation

### 1. True Range (TR)

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

### 2. Average True Range (ATR)

$$
ATR_t = \frac{ATR_{t-1} \times (N-1) + TR_t}{N}
$$

### 3. Min-Max Normalization

$$
ATRN_t = \frac{ATR_t - \min(ATR, W)}{\max(ATR, W) - \min(ATR, W)}
$$

Where:
- $W = 10 \times N$ (lookback window)
- $\min(ATR, W)$ = minimum ATR over last $W$ bars
- $\max(ATR, W)$ = maximum ATR over last $W$ bars

If $\max = \min$:

$$
ATRN_t = 0.5
$$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 9 | High; O(W) for min-max scan per bar. |
| **Allocations** | 0 | Zero-allocation in hot paths via RingBuffer. |
| **Complexity** | O(W) | Linear in lookback window size. |
| **Accuracy** | 10 | Exact min-max normalization. |
| **Timeliness** | 5 | Lags due to RMA + lookback window context. |
| **Overshoot** | 0 | Bounded to [0,1] by construction. |
| **Smoothness** | 8 | Inherits RMA smoothness from ATR. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Reference implementation. |
| **TA-Lib** | N/A | No direct equivalent; underlying ATR validated. |
| **Skender** | N/A | No direct equivalent; underlying ATR validated. |
| **Tulip** | N/A | No direct equivalent. |
| **Ooples** | N/A | No direct equivalent. |

ATRN is a QuanTAlib-specific indicator. Validation confirms:
1. Underlying ATR matches external libraries.
2. Normalization formula produces values in [0,1].
3. Constant volatility produces 0.5.
4. Increasing volatility approaches 1.0.
5. Decreasing volatility approaches 0.0.

## Interpretation Guide

| ATRN Value | Meaning | Trading Implications |
| :--- | :--- | :--- |
| **0.9 - 1.0** | Volatility at recent high | Extreme conditions; expand stops/targets |
| **0.7 - 0.9** | Above average volatility | Trending or volatile market |
| **0.4 - 0.6** | Average volatility | Normal conditions |
| **0.2 - 0.4** | Below average volatility | Consolidation; potential breakout setup |
| **0.0 - 0.2** | Volatility at recent low | Extreme quiet; mean reversion likely |

## Common Pitfalls

* **Scale Independence**: ATRN is relative to the asset's own history. An ATRN of 0.8 on AAPL is not comparable to 0.8 on BTC—they're measuring different things.

* **Lookback Sensitivity**: The 10×period lookback window defines "recent history." Shorter lookbacks react faster but may produce whipsaw signals. The default balances responsiveness and stability.

* **Lag**: Like all smoothed indicators, ATRN lags the actual volatility state. By the time ATRN hits 1.0, the volatility spike may already be fading.

* **Not a Directional Indicator**: ATRN measures the magnitude of volatility, not its direction. High ATRN can occur in both rallies and crashes.

## Use Cases

1. **Position Sizing**: Scale position size inversely with ATRN—smaller positions when ATRN is high, larger when low.

2. **Stop Loss Adaptation**: Tighter stops when ATRN is low (quiet market), wider stops when ATRN is high (volatile market).

3. **Regime Detection**: Use ATRN thresholds to switch between mean-reversion (low ATRN) and trend-following (high ATRN) strategies.

4. **Volatility Breakout**: Look for moves from ATRN < 0.2 to ATRN > 0.5 as potential breakout confirmation.