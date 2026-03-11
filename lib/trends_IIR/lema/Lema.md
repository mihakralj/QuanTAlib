# LEMA: Leader Exponential Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Lema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [lema.pine](lema.pine)                       |
| **Signature**    | [lema_signature](lema_signature.md) |

- LEMA (Leader EMA) adds a smoothed error correction to the standard EMA, creating a moving average that anticipates price movement.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "George Siligardos asked a simple question: what if you smoothed the EMA's own error and added it back? The answer is a moving average that leads price changes instead of lagging behind them. The error becomes the signal."

LEMA (Leader EMA) adds a smoothed error correction to the standard EMA, creating a moving average that anticipates price movement. The formula $\text{LEMA} = \text{EMA}(x, N) + \text{EMA}(x - \text{EMA}(x, N), N)$ decomposes price into a smooth component (EMA) and an error component (residual), then re-smooths the error and adds it back. The re-smoothed error represents the systematic part of the EMA's tracking deficit, and adding it back shifts the output toward where the next price is likely to be.

## Historical Context

George E. Siligardos published "Leader of the MACD" in *Technical Analysis of Stocks & Commodities* (Volume 26, Issue 7, July 2008). The article introduced LEMA as part of a broader MACD improvement, but the Leader EMA component proved useful as a standalone indicator.

The mathematical basis is straightforward: the EMA error $e_t = x_t - \text{EMA}_t$ is non-random during trends. When price is rising, the error is consistently positive (EMA lags below price). By smoothing this error and adding it to the EMA, the Leader compensates for the systematic lag component while filtering out the random noise component. The result is a moving average with approximately half the group delay of a standard EMA.

LEMA is structurally similar to DEMA ($2 \cdot \text{EMA} - \text{EMA}(\text{EMA})$), but the computational pathway differs: LEMA smooths the error signal explicitly, while DEMA derives the same correction algebraically. For identical periods, LEMA and DEMA produce similar (but not identical) outputs because the warmup compensation interacts differently with the two formulations.

## Architecture & Physics

### 1. Primary EMA

Standard EMA of the source with warmup compensation:

$$
\text{EMA}_1 = \alpha \cdot x + (1-\alpha) \cdot \text{EMA}_1
$$

### 2. Error Computation

$$
e_t = x_t - \text{EMA}_1[t]
$$

The error captures the tracking deficit: positive during uptrends, negative during downtrends, zero-mean during consolidation.

### 3. Error EMA

A second EMA smooths the error series, extracting the systematic (trend-related) component:

$$
\text{EMA}_2 = \alpha \cdot e_t + (1-\alpha) \cdot \text{EMA}_2
$$

### 4. Leader Output

$$
\text{LEMA}_t = \text{EMA}_1[t] + \text{EMA}_2[t]
$$

Both EMAs use warmup compensation for valid output from bar 1.

## Mathematical Foundation

With $\alpha = 2/(N+1)$ and $\beta = 1-\alpha$:

$$
\text{EMA}_1[t] = \alpha \cdot x_t + \beta \cdot \text{EMA}_1[t-1]
$$

$$
e_t = x_t - \text{EMA}_1[t]
$$

$$
\text{EMA}_2[t] = \alpha \cdot e_t + \beta \cdot \text{EMA}_2[t-1]
$$

$$
\text{LEMA}[t] = \text{EMA}_1[t] + \text{EMA}_2[t]
$$

**Expanding:** Since $e_t = x_t - \text{EMA}_1[t]$:

$$
\text{LEMA} = \text{EMA}_1 + \text{EMA}(x - \text{EMA}_1) = \text{EMA}_1 + \text{EMA}(x) - \text{EMA}(\text{EMA}_1)
$$

This shows LEMA is equivalent to $\text{EMA}_1 + \text{EMA}_1 - \text{EMA}_2 = 2\text{EMA}_1 - \text{EMA}_2$ in steady state, which is the DEMA formula. The distinction lies in the transient behavior during warmup.

**Group delay:** Approximately $\frac{N-1}{4}$ samples (half of EMA's $\frac{N-1}{2}$).

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
alpha = 2/(period+1); beta = 1-alpha

// EMA1 with warmup
ema1 = alpha*(price - ema1) + ema1
e1 *= beta; comp_ema1 = ema1 / (1-e1)

// Error
error = price - comp_ema1

// EMA2 (of error) with warmup
ema2 = alpha*(error - ema2) + ema2
e2 *= beta; comp_ema2 = ema2 / (1-e2)

return comp_ema1 + comp_ema2
```

## Resources

- Siligardos, G.E. (2008). "Leader of the MACD." *Technical Analysis of Stocks & Commodities*, 26(7), 30-37.
- Mulloy, P.G. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1). (DEMA, the algebraic equivalent.)

## Performance Profile

### Operation Count (Streaming Mode)

LEMA(N) runs two EMA stages with bias compensation. Stage 1 tracks source. Stage 2 tracks the tracking error `(src − EMA₁)`. Output = EMA₁ + EMA₂.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| EMA₁: FMA(α, src, decay×ema1) | 1 | 4 | ~4 |
| Bias E₁ update | 1 | 3 | ~3 |
| Error: src − EMA₁ | 1 | 1 | ~1 |
| EMA₂: FMA(α, error, decay×ema2) | 1 | 4 | ~4 |
| Bias E₂ update | 1 | 3 | ~3 |
| Output: EMA₁ + EMA₂ | 1 | 1 | ~1 |
| **Total** | **6** | — | **~16 cycles** |

O(1) per bar. The error-tracking EMA (stage 2) reacts faster than it would as a standard cascade because it processes `src − EMA₁` directly — the residual signal. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| EMA₁ pass | No | Recursive IIR |
| Error series (src − EMA₁) | Yes | `VSUBPD` once EMA₁ series computed |
| EMA₂ pass (on error series) | No | Recursive IIR on error series |
| Final addition | Yes | `VADDPD` once both EMA series computed |

EMA₁ must complete before the error series can be computed, and EMA₂ must complete before the final addition. Single-pass vectorization is impossible. Batch speedup: error subtraction and final addition are vectorizable but represent <10% of total cost.
