# RMA: Running Moving Average

> "Wilder didn't like standard EMA weighting. He wanted history to decay slower. So he invented RMA, which is just EMA with a different alpha, confusing traders for 40 years."

The Running Moving Average (RMA), also known as the Smoothed Moving Average (SMMA) or Wilder's Moving Average, is the backbone of J. Welles Wilder's most famous indicators: RSI, ATR, and ADX. It is functionally identical to an Exponential Moving Average (EMA), but with a smoothing factor ($\alpha$) of $1/N$ instead of $2/(N+1)$. This results in a longer "memory" and slower decay than a standard EMA of the same period.

## Historical Context

Introduced by J. Welles Wilder Jr. in his seminal 1978 book, *New Concepts in Technical Trading Systems*. Wilder developed his systems on a programmable calculator (the HP-67), where memory was scarce. The RMA allowed him to update averages without storing a history buffer, using a simple recursive formula. It remains the standard smoothing method for RSI and ATR.

## Architecture & Physics

RMA is an infinite impulse response (IIR) filter. In QuanTAlib, `Rma` is implemented as a zero-cost wrapper around the `Ema` class. It simply instantiates an `Ema` with a modified alpha.

### The Alpha Confusion

Traders often confuse RMA and EMA.

* **EMA**: $\alpha = \frac{2}{N+1}$
* **RMA**: $\alpha = \frac{1}{N}$

An RMA of period 14 is mathematically equivalent to an EMA of period 27 ($2N-1$).

## Mathematical Foundation

The recursive formula is identical to EMA, differing only in the weight.

### 1. Smoothing Factor

$$ \alpha = \frac{1}{N} $$

### 2. Recursive Update

$$ RMA_t = \alpha \cdot P_t + (1 - \alpha) \cdot RMA_{t-1} $$

Which simplifies to the classic Wilder formula:

$$ RMA_t = \frac{P_t + (N-1) \cdot RMA_{t-1}}{N} $$

## Performance Profile

RMA is extremely lightweight, requiring only a single multiplication and addition per update.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | [N] ns/bar | Scalar math |
| **Allocations** | 0 | Stack-based calculations only |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 9/10 | Standard for RSI/ATR |
| **Timeliness** | 6/10 | Slower than EMA |
| **Overshoot** | 9/10 | Very stable |
| **Smoothness** | 9/10 | Very smooth |

## Validation

Validated against Skender and Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetSmma` |
| **Ooples** | ✅ | Matches `CalculateWellesWilderMovingAverage` |
| **TA-Lib** | N/A | Not implemented |

| **Tulip** | N/A | Not implemented. |
### Common Pitfalls

1. **Initialization**: Like EMA, RMA requires a "warmup" period to converge. Wilder often initialized with a Simple Moving Average (SMA) of the first $N$ bars. QuanTAlib follows this convention.
2. **Naming**: Often called SMMA (Smoothed Moving Average) in other libraries.
3. **Period Mismatch**: Using an EMA(14) where an RMA(14) is expected will result in a much faster-moving line (equivalent to RMA(7.5)).
