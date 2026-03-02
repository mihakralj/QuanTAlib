# AHRENS: Ahrens Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 9)                      |
| **Outputs**      | Single series (Ahrens)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [ahrens_signature](ahrens_signature) |

### TL;DR

- AHRENS is a recursive IIR filter that adjusts toward the source price minus the midpoint of its current and lagged (by one period) states.
- Parameterized by `period` (default 9).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Richard Ahrens looked at the EMA and thought: what if the correction term accounted for where the average was, not just where it is? The result is a self-referencing IIR filter that uses its own history as a stabilizer."

AHRENS is a recursive IIR filter that adjusts toward the source price minus the midpoint of its current and lagged (by one period) states. The formula $\text{AHRENS}_t = \text{AHRENS}_{t-1} + (\text{source} - \frac{\text{AHRENS}_{t-1} + \text{AHRENS}_{t-N}}{2}) / N$ creates a self-dampening feedback loop: the correction term shrinks as the current and lagged states converge, producing a smoother approach to equilibrium than a standard EMA with less tendency to overshoot on reversals.

## Historical Context

Richard D. Ahrens published "Build A Better Moving Average" in *Stocks & Commodities* magazine (Volume 31, Issue 11, October 2013). The article proposed a modification to the standard recursive moving average that incorporates a lagged copy of the average itself, creating a second-order feedback structure.

The key insight is the midpoint correction: instead of pulling toward the source price directly (as EMA does), Ahrens pulls toward the source minus the midpoint of the current and lagged average. This means the correction is large when the average is changing rapidly (current and lagged states diverge) and small when it is stable (current and lagged states converge). The effect is automatic damping of oscillatory behavior without sacrificing trend-tracking ability.

The lagged state introduces a memory requirement: a circular buffer of $N$ past AHRENS values is needed to retrieve the value from $N$ bars ago. This makes AHRENS O(1) per bar in computation but O(N) in memory, comparable to an SMA but with IIR-like smoothing characteristics.

## Architecture & Physics

### 1. Circular Buffer for Lagged State

A ring buffer of size $N$ stores the most recent $N$ AHRENS output values. The lagged value $\text{AHRENS}_{t-N}$ is retrieved from the buffer before it is overwritten with the current output.

### 2. Midpoint Correction

The correction term is:

$$
\Delta = \frac{\text{source} - \frac{\text{AHRENS}_{t-1} + \text{AHRENS}_{t-N}}{2}}{N}
$$

This blends current and historical average states, creating a damped response.

### 3. Recursive Update

$$
\text{AHRENS}_t = \text{AHRENS}_{t-1} + \Delta
$$

The update is O(1) per bar after buffer retrieval.

## Mathematical Foundation

The Ahrens recursive formula:

$$
\text{AHRENS}_t = \text{AHRENS}_{t-1} + \frac{x_t - \frac{1}{2}\left(\text{AHRENS}_{t-1} + \text{AHRENS}_{t-N}\right)}{N}
$$

Rearranging:

$$
\text{AHRENS}_t = \text{AHRENS}_{t-1} + \frac{x_t}{N} - \frac{\text{AHRENS}_{t-1}}{2N} - \frac{\text{AHRENS}_{t-N}}{2N}
$$

$$
\text{AHRENS}_t = \left(1 - \frac{1}{2N}\right)\text{AHRENS}_{t-1} + \frac{1}{N}x_t - \frac{1}{2N}\text{AHRENS}_{t-N}
$$

**Transfer function analysis:** This is an ARMA(N,0) filter with two autoregressive taps: one at lag 1 with coefficient $(1 - 1/2N)$ and one at lag $N$ with coefficient $-1/2N$. The lag-$N$ tap creates a notch in the frequency response near $f = 1/N$, providing additional suppression of periodic noise at the averaging period.

**Stability:** For $N \geq 1$, the sum of absolute autoregressive coefficients is $|1-1/2N| + |1/2N| = 1$, which is on the stability boundary. The filter is marginally stable and does not diverge, but convergence is slower than a standard EMA.

**Default parameters:** `period = 9`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
buffer ← circular_buffer(period)  // stores past AHRENS values
prev = nz(result, source)
lagged = nz(buffer[head], source)  // AHRENS from N bars ago

midpoint = (prev + lagged) / 2
result = prev + (source - midpoint) / period

buffer[head] = result
head = (head + 1) % period
```

## Resources

- Ahrens, R.D. (2013). "Build A Better Moving Average." *Technical Analysis of Stocks & Commodities*, 31(11).
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 4: Finite and Infinite Impulse Response Filters.

## Performance Profile

### Operation Count (Streaming Mode)

AHRENS(N) requires a ring buffer of its own past output values (length N). The formula `AHRENS[t] = AHRENS[t-1] + (src − (AHRENS[t-1] + AHRENS[t-N]) / 2) / N` is O(1): one ring buffer read (indexed access at the tail), no scan.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push (AHRENS output) | 1 | 3 | ~3 |
| AHRENS[t-N] ring buffer read | 1 | 3 | ~3 |
| Mid-average: (prev + lagged) / 2 | 2 | 3 | ~6 |
| Error: src − mid | 1 | 1 | ~1 |
| Correction: error / N | 1 | 8 | ~8 |
| AHRENS update: prev + correction | 1 | 1 | ~1 |
| **Total** | **7** | — | **~22 cycles** |

O(1) per bar. The ring buffer stores past output values, not input values — a self-referential IIR. The division is the dominant cost. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Error computation (src − mid) | No | Mid depends on AHRENS[t-N] which depends on prior outputs |
| Self-referential IIR update | No | AHRENS[t] depends on AHRENS[t-1] and AHRENS[t-N]; both are computed values |
| Correction divide | No | Alpha depends on computed error; scalar only |

AHRENS is strictly sequential — the output at bar t depends on the output at bar t-1 (direct feedback) AND the output at bar t-N (delayed feedback). No vectorization is possible. Batch mode runs the same scalar kernel as streaming.
