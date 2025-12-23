# HMA: Hull Moving Average

> "Alan Hull looked at the lag in moving averages and said, 'I can fix that.' And he did, by making the math do gymnastics."

HMA (Hull Moving Average) is a solution to the eternal struggle between smoothness and lag. Most indicators force you to choose one; HMA gives you both. It achieves this by using weighted moving averages (WMAs) in a clever configuration that cancels out lag while maintaining the smoothing properties of the WMA.

## Historical Context

Developed by Alan Hull in 2005, the HMA was designed to be "responsive, accurate, and smooth." Hull realized that lag is essentially a function of the period, and by combining averages of different periods (specifically, a full period and a half period), he could mathematically offset the lag.

## Architecture & Physics

The HMA is built from three Weighted Moving Averages (WMAs):

1. **WMA(n/2)**: A fast WMA of half the period.
2. **WMA(n)**: A slow WMA of the full period.
3. **WMA(sqrt(n))**: A smoothing WMA applied to the difference.

The core logic is: $2 \times \text{WMA}(n/2) - \text{WMA}(n)$.
This operation "over-weights" the recent data, pushing the average forward to align with the current price. The final WMA smooths out the resulting noise.

## Mathematical Foundation

$$ \text{Raw} = 2 \times \text{WMA}(P, \frac{N}{2}) - \text{WMA}(P, N) $$

$$ \text{HMA} = \text{WMA}(\text{Raw}, \sqrt{N}) $$

Where $N$ is the period.

## Performance Profile

HMA is computationally more intensive than a simple WMA due to the three passes, but our implementation optimizes the intermediate step.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ★★★★☆ | 3x WMA cost + vector math. |
| **Allocations** | ★★★★★ | 0 bytes; hot path is allocation-free. |
| **Complexity** | ★★★★★ | O(1) constant time update. |
| **Precision** | ★★★★★ | `double` precision. |

### Zero-Allocation Design

HMA is implemented by chaining three `Wma` instances. Since `Wma` is zero-allocation, HMA inherits this property.

## Validation

Validated against Skender, Tulip, and Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetHma`. |
| **Tulip** | ✅ | Matches `hma`. |
| **Ooples** | ✅ | Matches `CalculateHullMovingAverage` (with rounding caveats). |
| **TA-Lib** | ❌ | Not implemented. |

### External Library Discrepancies

**OoplesFinance.StockIndicators**:
Discrepancies exist due to different rounding methods for integer periods.

* **QuanTAlib**: Uses integer truncation (floor) for $N/2$ and $\sqrt{N}$.
* **Ooples**: Uses `Math.Round` (nearest integer).

This results in different effective periods for $N=14$ ($\sqrt{14} \approx 3.74 \to 3$ vs $4$) and others where the fractional part $\ge 0.5$. Validation tests match exactly for periods where rounding logic aligns (e.g., $N=9, 20, 50$).

### Common Pitfalls

1. **Overshoot**: Like DEMA, HMA can overshoot price turns because of the lag correction.
2. **Period Sensitivity**: The $\sqrt{N}$ smoothing is hardcoded into the definition. You can't easily tweak the smoothing independently of the lag correction without breaking the "Hull" definition.
3. **Integer Math**: The periods $N/2$ and $\sqrt{N}$ are rounded to integers. This can cause slight discrepancies between implementations depending on rounding rules. Standard integer truncation is used in QuanTAlib.
