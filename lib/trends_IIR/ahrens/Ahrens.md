# AHRENS: Ahrens Moving Average

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
