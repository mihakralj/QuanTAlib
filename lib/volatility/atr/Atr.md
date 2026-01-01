# ATR: Average True Range

> "Volatility is the only thing that is real. Everything else is just a guess."

The Average True Range (ATR) is the definitive measure of market "heat." It ignores direction completely to focus on the raw magnitude of price movement. When ATR is high, the market is screaming; when it's low, the market is whispering.

Most traders mistakenly use ATR to find entries. Its true power is in **exits** and **sizing**. It answers the critical question: "How far can this asset move against me in a single day?"

## Historical Context

J. Welles Wilder Jr. introduced ATR in his 1978 masterpiece, *New Concepts in Technical Trading Systems*. This is the same book that gave us RSI, ADX, and the Parabolic SAR.

Wilder was a mechanical engineer turned real estate developer turned trader. He approached markets with an engineer's obsession for robust systems. He realized that simply looking at the High-Low range was flawed because it ignored **gaps**. If a stock closes at \$100 and opens at \$110, the High-Low range of the new bar might be small, but the *true* volatility was massive. ATR captures this "invisible" volatility.

## Architecture & Physics

ATR is built on two concepts: **True Range (TR)** and **Wilder's Smoothing (RMA)**.

1. **True Range**: The "real" distance price traveled, accounting for overnight gaps.
2. **RMA**: An exponential moving average with a specific alpha ($\alpha = 1/N$) that places significant weight on history. This gives ATR its characteristic "inertia"—it rises fast on shocks but decays slowly.

### The Gap Problem

Standard range ($High - Low$) fails when markets gap.

* **Scenario**: Close = 100. Next Open = 110. High = 112. Low = 109.
* **Standard Range**: $112 - 109 = 3$.
* **True Range**: $112 - 100 = 12$.

ATR correctly identifies the volatility as 12, not 3.

## Mathematical Foundation

### 1. True Range (TR)

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

Where:

* $H_t$: Current High
* $L_t$: Current Low
* $C_{t-1}$: Previous Close

### 2. Average True Range (ATR)

$$
ATR_t = RMA(TR, N)
$$

Which expands to:

$$
ATR_t = \frac{ATR_{t-1} \times (N-1) + TR_t}{N}
$$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) calculation via RMA. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time regardless of period. |
| **Accuracy** | 10 | Matches TA-Lib exactly. |
| **Timeliness** | 4 | Lags due to RMA smoothing; reflects past volatility. |
| **Overshoot** | 0 | Absolute measure; cannot overshoot. |
| **Smoothness** | 8 | Smooth decay due to RMA inertia. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_ATR` exactly. |
| **Skender** | ✅ | Matches `GetAtr` exactly. |
| **Tulip** | ✅ | Matches `atr` exactly. |
| **Ooples** | ✅ | Matches `CalculateAverageTrueRange`. |

### Common Pitfalls

* **Directionality**: ATR is non-directional. A crashing market has high ATR. A rallying market has high ATR. Do not use it to predict direction.
* **Scale Dependence**: ATR is absolute, not relative. An ATR of 5.0 on a \$100 stock is different from an ATR of 5.0 on a \$10 stock. Use `ATRP` (ATR Percent) for comparisons across assets.
* **Lag**: Because it uses RMA (a slow-decaying average), ATR lags actual volatility spikes. It tells you what *has* happened, not what *will* happen.
