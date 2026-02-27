# NLMA: Non-Lag Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Nlma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | 1 bar                          |

### TL;DR

- NLMA uses a two-phase damped cosine kernel with $5P - 1$ taps (where $P$ is the user period).
- Parameterized by `period` (default 14).
- Output range: Tracks input.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Igorad at TrendLaboratory built a two-phase FIR kernel that uses five times more taps than the period parameter suggests. The extra taps carry negative weights that actively cancel group delay. Most 'non-lag' indicators are marketing. This one is signal processing."

NLMA uses a two-phase damped cosine kernel with $5P - 1$ taps (where $P$ is the user period). Phase 1 builds the initial sweep; Phase 2 extends it through multiple cosine cycles. The kernel's negative weights in the mid-section subtract lagged price components, reducing group delay well below what a positive-only SMA of the same length achieves. Normalization by the signed weight sum preserves DC gain of 1.0. The result is a trend-following filter with moderate overshoot but substantially less lag than conventional moving averages.

## Historical Context

NLMA was developed by Igorad (username on trading forums) at TrendLaboratory, inspired by the FATL/SATL digital filter coefficient sets published by Finware. The FATL (Fast Adaptive Trend Line) and SATL (Slow Adaptive Trend Line) filters use fixed FIR coefficients derived from optimal filter design, with negative weights that provide lag cancellation. Igorad's contribution was to replace the fixed coefficients with a parametric formula, allowing the filter to be configured for any period.

The original MQL4 implementation reveals a design that is far more sophisticated than the simplified "damped cosine" descriptions found on most trading forums. The actual kernel uses:

- A **filter length** of $5P - 1$ taps (not $P$)
- A **two-phase** weight computation with different parametric sweeps
- A **Cycle** constant of 4 and a **Coeff** constant of $3\pi$
- A **gain function** $g(t)$ that transitions from 1.0 to a rational decay at $t = 0.5$

Most third-party implementations (including QuanTAlib's prior version) used a simplified $\cos(2\pi j/N)(1 - j/N)$ kernel with only $P$ taps. This produces a qualitatively different filter: shorter, less selective, and with weaker lag cancellation. The corrected implementation matches Igorad's original MQL4 source.

## Architecture & Physics

### 1. Filter Length

Given user parameter `period` ($P$), the actual FIR filter length is:

$$
N_{\text{flen}} = 5P - 1
$$

For `period = 10`, the kernel has 49 taps. For `period = 14`, it has 69 taps. This expansion is what gives NLMA its selectivity.

### 2. Two-Phase Kernel Construction

The kernel uses two internal constants:

$$
\text{Cycle} = 4, \quad \text{Phase} = P - 1, \quad \text{Coeff} = 3\pi
$$

For each tap index $i = 0, 1, \ldots, N_{\text{flen}} - 1$:

**Phase 1** ($i \leq \text{Phase} - 1$):

$$
t = \frac{i}{\text{Phase} - 1}
$$

**Phase 2** ($i > \text{Phase} - 1$):

$$
t = 1 + \frac{(i - \text{Phase} + 1)(2 \cdot \text{Cycle} - 1)}{\text{Cycle} \cdot P - 1}
$$

### 3. Gain Function

The gain function applies uniform weighting in the first half-cycle and rational decay afterward:

$$
g(t) = \begin{cases}
1 & \text{if } t \leq 0.5 \\[4pt]
\dfrac{1}{\text{Coeff} \cdot t + 1} & \text{if } t > 0.5
\end{cases}
$$

### 4. Weight Computation

Each tap weight combines the gain with a cosine oscillation:

$$
w[i] = g(t_i) \cdot \cos(\pi \cdot t_i)
$$

The cosine oscillation at frequency $\pi$ (not $2\pi/N$) creates a half-wave pattern across the Phase 1 sweep and multiple oscillations across Phase 2. The gain decay in Phase 2 progressively dampens these oscillations.

### 5. Signed-Sum Normalization

$$
\text{NLMA}_t = \frac{\sum_{i=0}^{N_{\text{flen}}-1} w[i] \cdot x_{t-i}}{\sum_{i=0}^{N_{\text{flen}}-1} w[i]}
$$

Because the kernel contains negative weights, $\sum w < \sum |w|$, amplifying the effective contribution of the positive (recent) region.

### 6. Warmup

During the warmup period ($\text{count} < N_{\text{flen}}$), the filter returns the current price. Once $N_{\text{flen}}$ bars are available, the full kernel is applied with precomputed weights.

## Mathematical Foundation

### Weight Structure Analysis

| Region | Tap range | $\cos(\pi t)$ sign | Weight sign | Effect |
| :--- | :---: | :---: | :---: | :--- |
| Recent (Phase 1, $t < 0.5$) | $0 \leq i < P/2$ | + | + | Track price |
| Transition (Phase 1, $t > 0.5$) | $P/2 \leq i < P$ | - | - | Begin lag cancel |
| Extended (Phase 2) | $P \leq i < 5P-1$ | oscillating | alternating | Deep lag cancel with decay |

The Phase 2 oscillations are the core lag-cancellation mechanism. Each negative lobe subtracts a delayed price component. The rational decay $1/(\text{Coeff} \cdot t + 1)$ ensures these lobes diminish progressively, preventing the filter from becoming unstable.

### Frequency Response

The two-phase kernel creates a more selective bandpass characteristic than a simple damped cosine. The extended length ($5P-1$ taps) provides sharper transition bands and deeper stopband attenuation. The negative weights create notches near frequencies that cause the most group delay.

### DC Gain

$$
H(0) = \frac{\sum w[i]}{\sum w[i]} = 1
$$

The signed-sum normalization guarantees unit DC gain regardless of the weight distribution.

### Parameter Summary

| Parameter | Default | Range | Notes |
| :--- | :---: | :---: | :--- |
| `period` | 14 | $\geq 2$ | User parameter; filter length = $5P - 1$ |
| Cycle | 4 | fixed | Number of cosine cycles in Phase 2 |
| Phase | $P - 1$ | derived | Boundary between Phase 1 and Phase 2 |
| Coeff | $3\pi$ | fixed | Gain decay rate in Phase 2 |

### Pseudo-code (streaming)

```text
// Constants
Cycle = 4
Phase = period - 1
Coeff = 3 * PI
flen  = 5 * period - 1

// Precompute weights once
wsum = 0
for i = 0 to flen-1:
    if i <= Phase - 1:
        t = i / (Phase - 1)
    else:
        t = 1 + (i - Phase + 1) * (2*Cycle - 1) / (Cycle * period - 1)

    if t <= 0.5:
        g = 1.0
    else:
        g = 1.0 / (Coeff * t + 1)

    w[i] = g * cos(PI * t)
    wsum += w[i]

// Per bar: insert into circular buffer of size flen
buffer[head] = price
head = (head + 1) % flen

// Warmup: return price when count < flen
if count < flen: return price

// Full convolution
sum = 0
for k = 0 to flen-1:
    sum += buffer[(head+k) % flen] * w[flen-1-k]

return sum / wsum
```

## Performance Profile

### Operation Count (Streaming Mode)

NLMA(period) uses the Igorad two-phase cosine kernel with length `flen = 5×period − 1`. At period = 14, flen = 69 taps. The filter is a standard FIR dot product but with a significantly longer kernel than most window-based MAs. Weights contain both positive and negative values (from the cycle-zone oscillation).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| FIR dot product: flen FMA (flen = 5×N − 1) | 5N−1 | 4 | ~(20N−4) |
| Normalization divide (by signed weight sum) | 1 | 8 | ~8 |
| **Total** | **5N** | — | **~(20N + 7) cycles** |

O(N) per bar with coefficient 5× larger than simple window filters. For period = 14 (flen = 69): ~1387 cycles. WarmupPeriod = flen = 5×period − 1.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR dot product (69-tap for default) | Yes | `VFMADD231PD`; weight array in L1 cache for period ≤ 14 |
| Negative-weight taps | Yes | Signed FMA handles both positive and negative lobes |
| Cross-bar independence | Yes | 4 output bars per AVX2 pass |
| Large kernel (5N taps) | Partial | At large periods, weight array exceeds L1 → cache-miss cost |

For period = 14, the 69-weight array (552 bytes) fits in L1 cache. AVX2 batch throughput: ~17 cycles per bar vs ~1387 scalar — ~80× speedup in the FIR phase. At period > 40 (flen > 200), the weight array spills to L2, reducing speedup to ~20×.

## Common Pitfalls

1. **Using period as filter length.** The actual filter length is $5P - 1$, not $P$. A `period=10` NLMA needs 49 bars of warmup, not 10. Failing to account for this causes premature `IsHot` transitions and incorrect early values.

2. **Simplified kernel formulas.** Many third-party implementations use $\cos(2\pi j/N)(1 - j/N)$ with $N = P$ taps. This is a qualitatively different filter that lacks the extended Phase 2 oscillations and provides weaker lag cancellation.

3. **Period = 1 is invalid.** The formula requires $\text{Phase} = P - 1 \geq 1$, so $P \geq 2$. Period 1 produces a degenerate kernel (division by zero in the $t$ computation).

4. **Overshoot is expected.** NLMA's negative weights mean the output can exceed the input price range. This is the cost of lag reduction, not a bug.

5. **Weight sum can be small.** When positive and negative weights nearly cancel, the normalization divisor $\sum w$ approaches zero. The implementation handles this by returning the raw input when the sum is too small.

6. **Large periods require proportional data.** A `period=200` NLMA has $5 \times 200 - 1 = 999$ taps. Insufficient data will keep the indicator in warmup indefinitely.

7. **Not a convex combination.** Unlike SMA or EMA, NLMA output is not bounded by min/max of the input window. Risk management systems that assume bounded outputs will produce incorrect results.

## Resources

- Igorad / TrendLaboratory. "NonLagMA" original MQL4 source code. The canonical reference for the two-phase kernel formula.
- Finware Ltd. "FATL/SATL Digital Filters." Technical documentation for FinWare trading software. Inspiration for the negative-weight lag cancellation approach.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 4: FIR Filters with Negative Weights.
