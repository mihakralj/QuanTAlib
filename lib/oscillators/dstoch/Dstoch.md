# DSTOCH — Double Stochastic (Bressert DSS)

## Overview

**DSTOCH** (Double Stochastic / DSS Bressert) applies the Stochastic oscillator formula twice with EMA smoothing between stages, producing a momentum indicator bounded between 0 and 100. Developed by Walter Bressert, it is more responsive than standard Stochastic while remaining bounded.

| Property   | Value           |
| :--------- | :-------------- |
| Category   | Oscillator      |
| Output     | Single (DSS)    |
| Range      | [0, 100]        |
| Default    | period = 21     |
| Input      | TBar (HLC)      |
| Hot after  | period bars     |

**Source:** [Dstoch.cs](Dstoch.cs) · [PineScript](dstoch.pine)

---

## Formula

### Stage 1: Raw %K

$$
\text{rawK}_t = \begin{cases}
100 \cdot \frac{C_t - LL_t}{HH_t - LL_t} & \text{if } HH_t \neq LL_t \\
0 & \text{otherwise}
\end{cases}
$$

where $HH_t$ and $LL_t$ are the highest high and lowest low over the last $n$ bars.

### Stage 1: EMA Smoothing

$$
\text{smoothK}_t = \alpha \cdot \text{rawK}_t + (1 - \alpha) \cdot \text{smoothK}_{t-1}
$$

where $\alpha = \frac{2}{n + 1}$.

### Stage 2: Stochastic of smoothK

$$
\text{dsRaw}_t = \begin{cases}
100 \cdot \frac{\text{smoothK}_t - \min(\text{smoothK}, n)}{\max(\text{smoothK}, n) - \min(\text{smoothK}, n)} & \text{if range} > 0 \\
0 & \text{otherwise}
\end{cases}
$$

### Stage 2: EMA Smoothing (Final Output)

$$
\text{DSS}_t = \alpha \cdot \text{dsRaw}_t + (1 - \alpha) \cdot \text{DSS}_{t-1}
$$

---

## Interpretation

| Zone      | Meaning                                |
| :-------- | :------------------------------------- |
| DSS > 80  | Overbought — potential bearish reversal|
| DSS < 20  | Oversold — potential bullish reversal  |
| Cross 50↑ | Bullish momentum shift                 |
| Cross 50↓ | Bearish momentum shift                 |

The double application of the Stochastic formula makes DSTOCH more sensitive to short-term price changes than the standard Stochastic oscillator.

---

## Implementation Details

### 1. MonotonicDeque Streaming (Stage 1)

Two `MonotonicDeque` instances provide O(1) amortized min/max tracking for HH/LL:

- **Max deque**: decreasing order of highs; front is always the window maximum.
- **Min deque**: increasing order of lows; front is always the window minimum.
- **Circular buffers** (`_hBuf`, `_lBuf`): store raw H/L values for deque rebuild on bar correction.

### 2. MonotonicDeque Streaming (Stage 2)

A second pair of `MonotonicDeque` instances tracks `smoothK` values:

- **`_skMaxDeque`**: highest smoothK over the window.
- **`_skMinDeque`**: lowest smoothK over the window.
- **`_skBuf`**: circular buffer for smoothK values.

### 3. EMA Smoothing

Both EMA stages use `Math.FusedMultiplyAdd` for optimal precision:

```csharp
smoothK = Math.FusedMultiplyAdd(prev_smoothK, decay, alpha * rawK);
```

### 4. Bar Correction

On `isNew=false`, all four deques are rebuilt from their circular buffers via `RebuildMax`/`RebuildMin`, and the scalar state is restored from `_ps`.

### 5. Batch Path

The batch implementation uses `Highest.Batch` / `Lowest.Batch` for both stages, with `stackalloc` for ≤ 256 elements and `ArrayPool` beyond.

---

## Complexity Analysis

| Operation              | Complexity     |
| :--------------------- | :------------- |
| Per-update (amortized) | O(1)           |
| Per-update (worst)     | O(n)           |
| Bar correction         | O(n) × 4 deques|
| Batch (N bars)         | O(N)           |
| Memory (streaming)     | O(n) × 3 buffers + 4 deques |

---

## References

- Bressert, W. (1998). *The Power of Oscillator/Cycle Combinations*
- TradingView: DSS Bressert indicator
- Investopedia: Double Smoothed Stochastic
