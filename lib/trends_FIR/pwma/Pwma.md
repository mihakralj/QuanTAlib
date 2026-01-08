# PWMA: Parabolic Weighted Moving Average

> "Linear weighting is for people who think the world is flat. PWMA squares the weights, because recent data isn't just more important—it's exponentially more important."

PWMA (Parabolic Weighted Moving Average) applies a parabolic ($i^2$) weighting scheme to the data window. This assigns massive importance to the most recent data points while still technically including the older data. It's like a WMA on steroids.

## Historical Context

While the WMA uses a linear triangle window ($1, 2, 3, \dots, n$), the PWMA uses a parabolic window ($1^2, 2^2, 3^2, \dots, n^2$). This was developed for traders who found the WMA too slow but the EMA too jittery. It provides a curve that turns faster than a WMA but is smoother than an EMA at the tail.

## Architecture & Physics

The "physics" is defined by the weight function $W_i = i^2$.
This shifts the center of gravity of the filter heavily towards the right (recent data).

## Mathematical Foundation

$$ \text{PWMA} = \frac{\sum_{i=1}^{N} i^2 P_{t-N+i}}{\sum_{i=1}^{N} i^2} $$

The O(1) update logic involves cascading the sums:
$$ S1_{new} = S1_{old} - \text{Oldest} + \text{Newest} $$
$$ S2_{new} = S2_{old} - S1_{old} + N \times \text{Newest} $$
$$ S3_{new} = S3_{old} - 2 S2_{old} + S1_{old} + N^2 \times \text{Newest} $$

## Performance Profile

Despite the "parabolic" name, the performance is linear O(1) per update.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | [N] ns/bar | Triple running sum O(1) |
| **Allocations** | 0 | Stack-based calculations only |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 8/10 | Heavily weighted to most recent price |
| **Timeliness** | 9/10 | Very fast reaction to new data |
| **Overshoot** | 3/10 | Parabolic weighting causes overshoot |
| **Smoothness** | 4/10 | Sensitive to recent noise |

## Validation

Validated against Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Ooples** | ✅ | Matches `CalculateParabolicWeightedMovingAverage` |
| **Skender** | N/A | Not implemented |
| **TA-Lib** | N/A | Not implemented |

| **Tulip** | N/A | Not implemented. |
### Common Pitfalls

1. **Resync**: Because triple running sums are used, floating-point errors can accumulate faster than in a simple SMA. The implementation automatically resyncs every 1000 ticks to maintain precision.
2. **Sensitivity**: This indicator is very sensitive to the most recent bar. It can "repaint" visually if used on an open bar (though the math is consistent).
