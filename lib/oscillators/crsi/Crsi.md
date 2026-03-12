# CRSI: Connors RSI

> *Connors RSI blends classic RSI with streak length and percentile rank, creating a multi-dimensional momentum snapshot.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `rsiPeriod` (default 3), `streakPeriod` (default 2), `rankPeriod` (default 100)                      |
| **Outputs**      | Single series (Crsi)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [crsi.pine](crsi.pine)                       |

- Connors RSI is a composite momentum oscillator that combines three independent measurements of price behavior into a single bounded (0-100) output:...
- Parameterized by `rsiperiod` (default 3), `streakperiod` (default 2), `rankperiod` (default 100).
- Output range: Varies (see docs).
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Connors RSI is a composite momentum oscillator that combines three independent measurements of price behavior into a single bounded (0-100) output: a short-term RSI of price, an RSI of the consecutive up/down streak length, and a percentile rank of the current rate of change within its recent history. The equal-weighted average of these three components produces a mean-reverting oscillator where extreme readings (above 90 or below 10) identify statistically overbought or oversold conditions with higher reliability than single-component RSI alone.

## Historical Context

Larry Connors and Cesar Alvarez introduced Connors RSI in their 2012 publication, building on Connors' earlier research into short-term mean reversion strategies. The indicator addressed a recognized weakness of standard RSI: its tendency to remain in overbought or oversold territory during strong trends without providing actionable reversal signals. By combining three orthogonal measurements of price behavior, each capturing a different aspect of momentum, CRSI reduces the false signal rate inherent in any single oscillator. The streak RSI component was particularly novel, converting the categorical information of consecutive up/down days into a continuous oscillator via a second RSI application. The percent rank component adds a non-parametric statistical dimension that is robust to distribution assumptions. Connors' backtesting showed the composite outperformed standard RSI for mean-reversion entry timing on equity indices and ETFs.

## Architecture & Physics

### Three-Component Pipeline

CRSI combines three independent calculations with equal weighting:

1. **Price RSI** (Component 1): Standard Wilder RSI with exponential smoothing ($\alpha = 1/\text{rsiPeriod}$) applied to the source series. Uses warmup compensation via the decaying exponential $e = \beta^n$ to correct for initial bias, producing valid output from bar 1.

2. **Streak RSI** (Component 2): First computes a consecutive streak counter (positive for up-closes, negative for down-closes, zero for unchanged), then applies the same Wilder RSI to the streak series. This converts run-length information into a bounded oscillator.

3. **Percent Rank** (Component 3): Computes 1-bar ROC, stores in a circular buffer, then counts what percentage of historical ROC values are less than or equal to the current ROC. This is a non-parametric ranking that is distribution-free.

### Warmup Compensation

Both RSI stages use the "section 2" warmup pattern: track $e = \beta^n$ and apply correction factor $c = 1/(1 - e)$ to the raw exponential averages until $e$ drops below $10^{-10}$. This eliminates the startup bias that plagues naive EMA initialization.

### Final Composition

The three components are averaged and clamped to $[0, 100]$:

$$\text{CRSI} = \text{clamp}\!\left(\frac{\text{PriceRSI} + \text{StreakRSI} + \text{PctRank}}{3}, 0, 100\right)$$

## Mathematical Foundation

**Component 1: Price RSI** with Wilder smoothing ($\alpha = 1/p_1$):

$$\overline{G}_t = \alpha \cdot \max(\Delta x_t, 0) + (1-\alpha) \cdot \overline{G}_{t-1}$$

$$\overline{L}_t = \alpha \cdot \max(-\Delta x_t, 0) + (1-\alpha) \cdot \overline{L}_{t-1}$$

$$RSI_1 = \frac{100 \cdot \overline{G}_t}{\overline{G}_t + \overline{L}_t}$$

**Component 2: Streak counter** then RSI:

$$\text{streak}_t = \begin{cases} \text{streak}_{t-1} + 1 & \text{if } x_t > x_{t-1} \text{ and streak}_{t-1} \geq 0 \\ 1 & \text{if } x_t > x_{t-1} \text{ and streak}_{t-1} < 0 \\ \text{streak}_{t-1} - 1 & \text{if } x_t < x_{t-1} \text{ and streak}_{t-1} \leq 0 \\ -1 & \text{if } x_t < x_{t-1} \text{ and streak}_{t-1} > 0 \\ 0 & \text{otherwise} \end{cases}$$

$$RSI_2 = \text{Wilder\_RSI}(\text{streak}_t, p_2)$$

**Component 3: Percent Rank** of 1-bar ROC over window $p_3$:

$$ROC_t = \frac{x_t - x_{t-1}}{x_{t-1}} \times 100$$

$$PctRank_t = \frac{|\{ROC_i : ROC_i \leq ROC_t,\; i \in \text{window}\}|}{|\text{window}|} \times 100$$

**Composite:**

$$CRSI_t = \frac{RSI_1 + RSI_2 + PctRank}{3}$$

**Default parameters:** rsiPeriod = 3, streakPeriod = 2, rankPeriod = 100.

## Performance Profile

### Operation Count (Streaming Mode)

ConnorsRSI = average of RSI(3), StreakRSI(2), PercentRank(100). Three sub-indicators.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RSI(3) update (2 EMA + ratio) | 6 | 4 | 24 |
| Streak count (up/down/flat) | 2 | 1 | 2 |
| StreakRSI(2) update (2 EMA + ratio) | 6 | 4 | 24 |
| PercentRank scan (O(N), N=100) | 100 | 1 | 100 |
| ADD × 2 + MUL ÷3 (average) | 3 | 3 | 9 |
| **Total** | **117** | — | **~159 cycles** |

The O(100) PercentRank linear scan dominates. For N=100: ~159 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| RSI(3) / StreakRSI(2) EMA passes | **No** | Recursive IIR — sequential |
| PercentRank scan | Yes | SIMD comparison count: VCMPPD + VPCNT per window |
| Final averaging | Yes | VADDPD + VMULPD |

PercentRank scan is the only sub-step with meaningful SIMD acceleration potential.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Three independently calibrated sub-signals |
| **Timeliness** | 5/10 | 100-bar PercentRank window dominates warmup |
| **Smoothness** | 7/10 | Averaging three signals reduces individual signal noise |
| **Noise Rejection** | 7/10 | Multi-component design reduces false signals |

## Resources

- Connors, L. & Alvarez, C. (2012). *An Introduction to ConnorsRSI*. TradingMarkets
- Connors, L. (2009). *Short-Term Trading Strategies That Work*. TradingMarkets
- PineScript reference: [`crsi.pine`](crsi.pine)
