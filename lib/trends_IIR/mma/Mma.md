# MMA: Modified Moving Average

## A hybrid smoother that blends SMA with a weighted component

> "MMA is a compromise: less lag than SMA, less overshoot than fully weighted filters."

MMA (Modified Moving Average) uses a **simple mean** as a baseline, then adds a **weighted correction** based on recent values. The weighting tilts toward newer bars without fully discarding older ones. It is a pragmatic middle ground for users who want smoother output than EMA but less lag than pure SMA.

## Historical Context

MMA is not a standardized textbook indicator. It is a practical hybrid that appears in multiple custom systems, often labeled "modified" or "balanced" moving average. The version here follows the PineScript reference (`mma.pine`) and is optimized for streaming updates.

## Architecture & Physics

1. Maintain a rolling buffer of the last **N** values.
2. Compute the **SMA** of the buffer.
3. Compute a **weighted sum** of the buffer with decreasing weights.
4. Add the weighted correction to the SMA.

The weighted term acts like a momentum correction, pulling the SMA forward.

## Math Foundation

Let $x_t$ be the input, and $N$ the period. Define:

$$\text{SMA}_t = \frac{1}{N}\sum_{i=0}^{N-1} x_{t-i}$$

Define weights (newest first):

$$w_i = \frac{N - (2i + 1)}{2}$$

Weighted sum:

$$W_t = \sum_{i=0}^{N-1} w_i \cdot x_{t-i}$$

Final MMA:

$$\text{MMA}_t = \text{SMA}_t + \frac{6W_t}{(N+1)N}$$

## Performance Profile

| Metric | Score | Notes |
|:---|:---|:---|
| **Throughput** | TBD | Weighted component is O(N) |
| **Allocations** | 0 | Streaming update is allocation-free |
| **Complexity** | O(N) | Weighted pass over the buffer |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 6/10 | Faster than SMA, slower than EMA |
| **Overshoot** | 4/10 | Mild overshoot on reversals |
| **Smoothness** | 7/10 | Smoother than EMA |

## Validation

MMA is validated against the PineScript reference implementation.

| Library | Status | Tolerance | Notes |
|:---|:---|:---|:---|
| **TA-Lib** | N/A | - | Not implemented |
| **Skender** | N/A | - | Not implemented |
| **Tulip** | N/A | - | Not implemented |
| **Ooples** | N/A | - | Not implemented |
| **PineScript** | ? Passed | 1e-10 | Matches `lib/trends_IIR/mma/mma.pine` |

## Common Pitfalls

1. **Period too large**

   MMA’s weighted component scales linearly with N. Large windows increase CPU cost.

2. **Warmup**

   The weighted correction is unstable until the buffer fills. Use `IsHot` / `WarmupPeriod`.

3. **Non-finite inputs**

   NaN/Infinity is replaced with the last valid value. Before the first valid input, output is `NaN`.
