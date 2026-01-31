# WAD: Williams Accumulation/Distribution

> "Volume is the fuel that drives price." — Larry Williams

Williams Accumulation/Distribution (WAD) is Larry Williams' contribution to the volume analysis toolkit. Unlike the standard Accumulation/Distribution Line that uses the close's position within the day's range, WAD incorporates **True Range** concepts. This gives it a different perspective on buying and selling pressure.

## Historical Context

Developed by Larry Williams (of Williams %R fame), WAD was introduced in his 1979 book "How I Made One Million Dollars... Last Year... Trading Commodities." Williams designed the indicator to be more sensitive to actual price movement between periods, not just within a single bar.

The key innovation: WAD compares today's close to yesterday's close, then uses True Range (incorporating gaps) to measure how much of the day's range was "captured" by the movement.

## Architecture & Physics

WAD is a cumulative indicator that measures buying/selling pressure using the relationship between consecutive closes and True Range concepts.

### 1. True Range Boundaries

For each bar, we establish boundaries that account for gaps:

$$
TrueHigh = \max(High_t, Close_{t-1})
$$

$$
TrueLow = \min(Low_t, Close_{t-1})
$$

### 2. Price Movement (PM)

The direction of the close relative to the previous close determines the calculation:

$$
PM_t = \begin{cases}
Close_t - TrueLow & \text{if } Close_t > Close_{t-1} \\
Close_t - TrueHigh & \text{if } Close_t < Close_{t-1} \\
0 & \text{if } Close_t = Close_{t-1}
\end{cases}
$$

### 3. Accumulation/Distribution Value

$$
AD_t = PM_t \times Volume_t
$$

### 4. Williams Accumulation/Distribution

$$
WAD_t = WAD_{t-1} + AD_t
$$

## Mathematical Foundation

The genius of WAD lies in how it handles different market conditions:

**Upward Movement (Close > Previous Close)**:
When price closes higher than yesterday, we measure from the True Low (which could be below the current bar's low if we gapped up). This captures the full extent of buying pressure.

**Downward Movement (Close < Previous Close)**:
When price closes lower than yesterday, we measure from the True High (which could be above the current bar's high if we gapped down). This captures the full extent of selling pressure.

**Unchanged (Close = Previous Close)**:
No price movement detected; no volume impact on WAD.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) calculation with simple comparisons. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time per update. |
| **Accuracy** | 10 | Matches TA-Lib and Ooples implementations. |
| **Timeliness** | 10 | No lag; updates immediately with each bar. |
| **Overshoot** | N/A | Cumulative indicator; concept doesn't apply. |
| **Smoothness** | 2 | Jagged; reflects raw volume and price movement. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | N/A | Not implemented. |
| **Skender** | N/A | Not implemented. |
| **Tulip** | N/A | Not implemented. |
| **Ooples** | ✅ | Matches `CalculateWilliamsAccumulationDistribution`. |

## WAD vs ADL: The Key Differences

| Aspect | WAD | ADL |
| :--- | :--- | :--- |
| **Close Reference** | Previous close | Current bar's H-L range |
| **Gap Handling** | Explicitly incorporated via True Range | Ignored |
| **Volume Multiplier** | Price movement (absolute) | Close Location Value (normalized -1 to +1) |
| **Creator** | Larry Williams (1979) | Marc Chaikin |

## Common Pitfalls

1. **First Bar**: The first bar in a series produces WAD = 0 since there's no previous close. Don't interpret this as meaningful.

2. **Scale Dependency**: Like ADL, the absolute value of WAD depends on starting point and volume magnitude. Focus on trend and divergences.

3. **Volume Magnitude**: WAD values can grow very large because the price movement isn't normalized. A high-volume day with large price movement will dominate the cumulative sum.

4. **Zero Volume**: If volume is zero, the bar contributes nothing to WAD regardless of price movement. Ensure your data source provides valid volume.

5. **Gap Significance**: WAD specifically accounts for gaps through True Range. This makes it more sensitive to overnight gaps than ADL, which can be good or bad depending on your analysis goals.

## References

- Williams, L. (1979). "How I Made One Million Dollars... Last Year... Trading Commodities." Windsor Books.
- https://school.stockcharts.com/doku.php?id=technical_indicators:williams_ad