# ADX: Average Directional Index

> "Is the market trending?" is the only question that matters. ADX answers it, loudly.

The Average Directional Index (ADX) is the industry-standard filter for trend strength. It ignores direction entirely, focusing solely on the velocity of price expansion. It allows systems to switch context: deploying trend-following logic when the market moves, and mean-reversion logic when it chops.

## The 1978 Standard

J. Welles Wilder Jr. was a mechanical engineer, and it shows. Introduced in *New Concepts in Technical Trading Systems* (1978), the ADX is a machine built from moving parts. It doesn't just smooth price; it deconstructs range expansion, normalizes it against volatility, and then smooths the result twice.

It is not a modern, low-lag indicator. It is a heavy, momentum-based flywheel that takes time to spin up and time to spin down.

## Architecture & Physics

The ADX is a "derivative of a derivative." The calculation pipeline is deep, which creates significant lag but offers exceptional noise reduction.

1. **Decomposition**: Price action is broken into Directional Movement (+DM, -DM) and Volatility (True Range).
2. **Normalization**: Raw movement is meaningless without context. DM is normalized by TR to get Directional Indicators (+DI, -DI).
3. **Oscillation**: The Directional Index (DX) is derived from the ratio of the difference to the sum of the DIs.
4. **Smoothing**: Finally, the DX is smoothed to get ADX.

### The Stability Problem

Because ADX relies on recursive smoothing (RMA) at multiple stages, it is notoriously slow to converge. A "cold" start requires at least $2 \times Period$ bars to produce data that even remotely resembles a mature series, and often $3-4 \times Period$ to match external libraries (like TA-Lib) within 4 decimal places.

The QuanTAlib implementation handles this by tracking the "warmup" state explicitly. Garbage is not output during the convergence phase if it can be avoided, but users must be aware that ADX is history-dependent.

## Mathematical Foundation

The math is classic Wilder: recursive, stateful, and robust.

### 1. Directional Movement (DM)

Today's range is compared to yesterday's.
$$
\text{UpMove} = H_t - H_{t-1}
$$
$$
\text{DownMove} = L_{t-1} - L_t
$$

$$
+DM = \begin{cases} \text{UpMove} & \text{if } \text{UpMove} > \text{DownMove} \text{ and } \text{UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}
$$

$$
-DM = \begin{cases} \text{DownMove} & \text{if } \text{DownMove} > \text{UpMove} \text{ and } \text{DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}
$$

### 2. Smoothing (RMA)

Wilder's Moving Average (RMA) is an exponential moving average with $\alpha = 1/N$. The series $+DM$, $-DM$, and $TR$ (True Range) are smoothed using this operator.

$$
+DM_{smoothed} = RMA(+DM, N)
$$
$$
-DM_{smoothed} = RMA(-DM, N)
$$
$$
TR_{smoothed} = RMA(TR, N)
$$

### 3. Directional Indicators (DI)

$$
+DI = 100 \times \frac{+DM_{smoothed}}{TR_{smoothed}}
$$
$$
-DI = 100 \times \frac{-DM_{smoothed}}{TR_{smoothed}}
$$

### 4. The Index (DX and ADX)

$$
DX = 100 \times \frac{|+DI - -DI|}{+DI + -DI}
$$
$$
ADX = RMA(DX, N)
$$

## Performance Profile

Throughput is optimized. The recursive nature of RMA allows for O(1) updates, but the initial calculation over a span requires O(N).

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | 5ns / bar | Measured on Apple M1 Max, .NET 8.0 |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(1) | Streaming updates are constant time |
| **Precision** | `double` | Necessary to prevent drift in recursive sums |

## Validation

Validation is performed against **TA-Lib** (the industry reference).

- **Convergence**: Matches TA-Lib to within `1e-9` after ~100 bars of warmup.
- **Edge Cases**: Handles `NaN` inputs by carrying forward the last valid state, preventing the "poisoning" of the recursive chain.
- **Drift**: Periodic re-summation is not required here as RMA is self-correcting over time, unlike simple accumulation.

### Common Pitfalls

- **Period Sensitivity**: The standard period is 14. Lowering it (e.g., 7) makes ADX twitchy and prone to false positives. Raising it (e.g., 30) turns it into a geological indicator—accurate, but late.
- **The "Turn"**: ADX peaks *after* the trend has exhausted. It is a lagging indicator of trend strength, not a leading indicator of price reversal.
