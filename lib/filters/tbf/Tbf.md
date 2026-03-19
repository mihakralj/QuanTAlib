# TBF: Ehlers Truncated Bandpass Filter

> *Truncation tames the infinite memory of IIR filters, revealing only the cycles that matter now.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                           |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 20), `bandwidth` (default 0.1), `length` (default 10) |
| **Outputs**      | TBF (primary), BP (standard bandpass for comparison) |
| **Output range** | Oscillates around zero           |
| **Warmup**       | `length + 2` bars                |
| **PineScript**   | [tbf.pine](tbf.pine)            |

- The Truncated Bandpass Filter limits the IIR bandpass filter's infinite memory to a fixed window, eliminating initialization transients and dampening false cycle triggers from price shocks.
- **Similar:** [BPF](../bpf/bpf.md), [SPBF](../spbf/Spbf.md) | **Complementary:** Dominant cycle detection for adaptive period | **Trading note:** Oscillator around zero; rising above zero indicates upward cycle phase.
- Based on John F. Ehlers' "Truncated Indicators" — TASC July 2020.

## Historical Context

John F. Ehlers introduced the Truncated Bandpass Filter in his July 2020 article "Truncated Indicators" for *Stocks & Commodities* magazine. The key insight: IIR (Infinite Impulse Response) filters carry a theoretically infinite history of past prices in their feedback terms. In finite backtests this causes two problems:

1. **Initialization errors**: The filter output depends on how far back the data starts, producing different results for the same time window depending on available history.
2. **Transient ringing**: Price shocks propagate indefinitely through the filter's feedback, causing artificial cycles that can trigger false signals.

Ehlers' solution: instead of using the filter's own past outputs (which encode infinite history), recompute the entire filter from scratch each bar over a fixed-length window. By initializing the tail of the window to zero, the filter's "memory" is explicitly bounded to exactly `Length` bars.

## Architecture & Physics

### 1. Bandpass Filter Coefficients

The filter is parameterized by a center period $P$ and fractional bandwidth $\beta$:

$$
L_1 = \cos\!\left(\frac{2\pi}{P}\right), \qquad
G_1 = \cos\!\left(\frac{\beta \cdot 2\pi}{P}\right)
$$

$$
S_1 = \frac{1}{G_1} - \sqrt{\frac{1}{G_1^2} - 1}
$$

Derived recursion coefficients:

$$
a_0 = \tfrac{1}{2}(1 - S_1), \qquad
a_1 = L_1(1 + S_1), \qquad
a_2 = -S_1
$$

### 2. Standard Bandpass (IIR)

The standard 2-pole Ehlers bandpass filter:

$$
BP_t = a_0(P_t - P_{t-2}) + a_1 \cdot BP_{t-1} + a_2 \cdot BP_{t-2}
$$

This is an IIR filter — each output depends on all previous outputs, creating infinite memory.

### 3. Truncated Bandpass

The truncated version limits memory to exactly $L$ bars by recomputing the IIR recursion from scratch each bar:

**Step 1:** Initialize the tail:
$$
T_{L+2} = 0, \qquad T_{L+1} = 0
$$

**Step 2:** Run the recursion forward from oldest to newest:
$$
T_k = a_0(P_{k-1} - P_{k+1}) + a_1 \cdot T_{k+1} + a_2 \cdot T_{k+2}, \qquad k = L, L{-}1, \ldots, 1
$$

where $P_k$ denotes the close price $k$ bars ago.

**Step 3:** Output:
$$
\text{TBF}_t = T_1
$$

### 4. Complexity

Each bar requires $O(L)$ work to recompute the truncated filter. The standard BP is $O(1)$ per bar. Memory: a circular buffer of $L+2$ prices plus a scratch array of $L+3$ doubles (stack-allocated when $L \leq 253$).

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $P$ | period | 20 | $\geq 2$ | Center cycle period |
| $\beta$ | bandwidth | 0.1 | $> 0$ | Fractional bandwidth (0.1 = 10%) |
| $L$ | length | 10 | $\geq 1$ | Truncation window (bars of memory) |

### Transfer Function

The standard bandpass has transfer function:

$$
H(z) = \frac{a_0(1 - z^{-2})}{1 - a_1 z^{-1} - a_2 z^{-2}}
$$

with poles at $z = \frac{a_1 \pm \sqrt{a_1^2 + 4a_2}}{2}$ and zeros at $z = \pm 1$. The truncated version has the same frequency selectivity but finite impulse response over the window, trading spectral resolution for temporal precision.

### Bandwidth Interpretation

The bandwidth parameter $\beta$ controls how narrow the passband is:
- $\beta = 0.1$ (10%): passes cycles from period 18 to 22 (for $P=20$)
- $\beta = 0.3$ (30%): passes cycles from period 14 to 26
- Smaller $\beta$ = narrower band = more ringing in standard BP (but truncation dampens this)

## Performance Profile

### Operation Count (Streaming Mode)

Each bar recomputes the truncated filter over $L$ positions:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (a0 × diff) | $L$ | 3 | $3L$ |
| ADD (src diff) | $L$ | 1 | $L$ |
| FMA (a1 × T[k+1]) | $L$ | 4 | $4L$ |
| FMA (a2 × T[k+2]) | $L$ | 4 | $4L$ |
| Standard BP (O(1)) | 3 | 4 | 12 |
| Buffer add | 1 | 2 | 2 |
| **Total** | **~$4L + 4$** | — | **~$12L + 14$ cycles** |

For default $L = 10$: ~134 cycles/bar. For $L = 50$: ~614 cycles/bar.

### Batch Mode (SIMD Analysis)

The truncated recursion is inherently sequential (each $T_k$ depends on $T_{k+1}$ and $T_{k+2}$), preventing SIMD within a single bar's computation:

| Optimization | Benefit |
| :--- | :--- |
| Truncated IIR recursion | Sequential per bar; O(L) per bar |
| Standard IIR | Sequential; O(1) per bar |
| stackalloc scratch | Zero heap allocation for L ≤ 253 |
| Coefficient precomputation | Amortized once at construction |

## Resources

- Ehlers, J. F. (2020). "Truncated Indicators." *Technical Analysis of Stocks & Commodities*, July 2020.
- [Ehlers' Paper (PDF)](https://www.mesasoftware.com/papers/TRUNCATED%20INDICATORS.pdf)
- Ehlers, J. F. (2013). *Cycle Analytics for Traders*. Wiley.
