# EMA: Exponential Moving Average

> "The AK-47 of technical indicators. It's been around forever, everyone uses it, and it gets the job done. It's not fancy, but it works."

EMA (Exponential Moving Average) is the standard by which all other averages are judged. Unlike the SMA, which treats data from 10 days ago with the same reverence as data from 10 seconds ago, the EMA understands that in markets, recency is relevance. It applies an exponentially decaying weight to older prices, reacting faster to new information.

## Historical Context

The EMA was brought to the financial world to solve the "drop-off effect" of the SMA (where an old price dropping out of the window causes the average to jump). By using a recursive formula, the EMA includes *all* past data in its calculation, with weights diminishing to infinity. It is the infinite impulse response (IIR) filter of the trading world.

## Architecture & Physics

The EMA is defined by its smoothing factor, $\alpha$.

- **High $\alpha$**: Fast decay, responsive, noisy.
- **Low $\alpha$**: Slow decay, smooth, laggy.

Our implementation includes a **Compensator** for the warmup phase. A standard EMA starts at 0 (or the first price) and takes time to converge. We mathematically correct this early-stage bias so the EMA is accurate from the very first few bars, rather than waiting for $3 \times N$ bars to stabilize.

### Zero-Allocation Design

The EMA is the poster child for efficiency.

- **State**: Requires only the previous EMA value and a compensator state.
- **No Buffers**: No arrays, no lists, no history. Just one `double`.
- **Inlining**: The update method is aggressive inlined for maximum throughput.

## Mathematical Foundation

The standard recursive formula:

$$ \alpha = \frac{2}{N + 1} $$

$$ \text{EMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{t-1} $$

### The Compensator (Warmup Correction)

To handle the initialization bias (where $\text{EMA}_0$ is unknown), we track the sum of weights:

$$ E_t = (1 - \alpha)^t $$

$$ \text{Corrected EMA}_t = \frac{\text{Uncorrected EMA}_t}{1 - E_t} $$

This ensures the EMA is statistically valid even during the warmup period.

## Performance Profile

This is as fast as it gets.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | Extreme | Single multiplication and addition |
| **Complexity** | O(1) | Recursive calculation |
| **Accuracy** | 7/10 | Standard baseline, tracks trends well |
| **Timeliness** | 6/10 | Lags, but less than SMA |
| **Overshoot** | 10/10 | No overshoot, asymptotically approaches price |
| **Smoothness** | 7/10 | Good balance, but can be noisy with small N |

## Validation

Validated against TA-Lib, Skender, and every other library in existence.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | $10^{-9}$ | Matches `TA_EMA` |
| **Skender** | $10^{-9}$ | Matches `GetEma` |

### Common Pitfalls

1. **The "First Value" Problem**: Most libraries seed the EMA with the first price or an SMA of the first N prices. We use a mathematical compensator. Our results during the first N bars will be *more accurate* than TA-Lib, which might look like a discrepancy. It's not; we're right, they're approximating.
2. **Alpha vs. Period**: Remember that $N$ is just a proxy for $\alpha$. You can construct an EMA directly with an $\alpha$ (e.g., 0.1) if you prefer signal processing terminology over trader terminology.
