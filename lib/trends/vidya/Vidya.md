# VIDYA: Variable Index Dynamic Average

> "Tushar Chande asked: 'Why should I trust a moving average that treats a market crash the same as a lunch break?' VIDYA is the answer."

The Variable Index Dynamic Average (VIDYA) is an adaptive moving average that automatically adjusts its smoothing speed based on market volatility. When the market is trending (high volatility), VIDYA speeds up to capture the move. When the market is ranging (low volatility), it slows down to filter out the noise.

## Historical Context

Developed by Tushar Chande and introduced in *Technical Analysis of Stocks & Commodities* (March 1992). It was one of the first "intelligent" moving averages, using Chande's own Momentum Oscillator (CMO) as the volatility index.

## Architecture & Physics

VIDYA is essentially an EMA where the alpha ($\alpha$) is not constant.
$$ \alpha_{dynamic} = \alpha_{static} \times |CMO| $$

Since $|CMO|$ ranges from 0 to 1:

* **CMO = 0 (No Trend)**: $\alpha = 0$. VIDYA becomes a flat line.
* **CMO = 1 (Strong Trend)**: $\alpha = \alpha_{static}$. VIDYA acts like a standard EMA.

## Mathematical Foundation

### 1. Chande Momentum Oscillator (CMO)

$$ CMO = \frac{\sum Up - \sum Down}{\sum Up + \sum Down} $$

### 2. Dynamic Alpha

$$ \alpha_{static} = \frac{2}{N+1} $$
$$ \alpha_{dynamic} = \alpha_{static} \times |CMO| $$

### 3. The Update

$$ VIDYA_t = (\alpha_{dynamic} \times Price_t) + ((1 - \alpha_{dynamic}) \times VIDYA_{t-1}) $$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 9 | High; O(1) calculation with CMO volatility index. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time regardless of period. |
| **Accuracy** | 10 | Matches reference implementation exactly. |
| **Timeliness** | 8 | Adaptive; speeds up in trends, slows in ranges. |
| **Overshoot** | 2 | Minimal overshoot; constrained by dynamic alpha. |
| **Smoothness** | 7 | Variable; smooth in ranges, responsive in trends. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | N/A | Not implemented. |
| **Skender** | N/A | Not implemented. |
| **Tulip** | ❌ | Uses Standard Deviation ratio (1992), not CMO (1994). |
| **Ooples** | ❌ | Diverges significantly due to volatility logic. |

### Common Pitfalls

1. **Flatlining**: In extremely choppy, sideways markets, CMO can approach 0, causing VIDYA to flatline completely. This is a feature, not a bug.
2. **Sensitivity**: VIDYA is highly sensitive to the period chosen for the CMO. A short period makes it jittery; a long period makes it sluggish.
3. **Comparison**: Often compared to KAMA (Kaufman). KAMA uses Efficiency Ratio (ER); VIDYA uses CMO. They are conceptually similar but mathematically distinct.
