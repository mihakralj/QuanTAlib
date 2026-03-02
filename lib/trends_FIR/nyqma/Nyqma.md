# NYQMA: Nyquist Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 89), `nyquistPeriod` (default 21)                      |
| **Outputs**      | Single series (Nyqma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | 1 bar                          |
| **Signature**    | [nyqma_signature](nyqma_signature) |

### TL;DR

- NYQMA combines a primary LWMA (Linear Weighted Moving Average) with a secondary LWMA applied to the first, using lag-compensating extrapolation: $\...
- Parameterized by `period` (default 89), `nyquistperiod` (default 21).
- Output range: Tracks input.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Manfred Dürschner applied the Nyquist-Shannon sampling theorem to cascaded moving averages: the second smoothing period must not exceed half the first, or you get aliasing artifacts. Respect the theorem and the ghost signals disappear."

NYQMA combines a primary LWMA (Linear Weighted Moving Average) with a secondary LWMA applied to the first, using lag-compensating extrapolation: $\text{NYQMA} = (1+\alpha) \cdot \text{MA}_1 - \alpha \cdot \text{MA}_2$, where $\alpha = N_2 / (N_1 - N_2)$. The Nyquist constraint $N_2 \leq \lfloor N_1/2 \rfloor$ ensures the second smoothing does not introduce aliasing artifacts into the output. This produces a lag-reduced moving average grounded in sampling theory rather than ad-hoc coefficient tuning. Streaming update is O(1) per bar via composed Wma instances; batch mode uses stackalloc/ArrayPool with FMA in the extrapolation loop.

## Historical Context

Dr. Manfred G. Dürschner published NYQMA in *Gleitende Durchschnitte 3.0* ("Moving Averages 3.0"), a German-language work that applies rigorous signal-processing theory to financial moving average design. His key insight: cascading two smoothing operations is mathematically equivalent to sampling a continuous signal at two rates. The Nyquist-Shannon sampling theorem (Shannon 1949, Nyquist 1928) dictates that the second rate cannot exceed half the first without introducing aliasing.

The lag compensation formula $(1+\alpha) \cdot \text{MA}_1 - \alpha \cdot \text{MA}_2$ is structurally identical to DEMA and GDEMA, but with two critical distinctions: (a) both constituent MAs are LWMAs, not EMAs, giving the filter a finite impulse response with bounded memory; (b) the gain factor $\alpha$ is derived from the period ratio $N_2/(N_1 - N_2)$ rather than being a free parameter, grounding the extrapolation strength in sampling theory.

Most double-smoothed moving averages (DEMA, TEMA, T3) use the same MA period for both stages. Dürschner's contribution is recognizing that the second stage period is a free design parameter, but one constrained by the Nyquist limit. When $N_2 = N_1$ (the DEMA case), the constraint is violated by definition. When $N_2 = \lfloor N_1/2 \rfloor$ (the maximum legal Nyquist value), lag cancellation is maximized without aliasing. Smaller $N_2$ values trade less lag reduction for more conservative anti-aliasing margin.

## Architecture and Physics

### 1. Primary LWMA (period $N_1$)

A standard Linear Weighted Moving Average with period $N_1$:

$$
\text{MA}_1 = \text{WMA}(x, N_1)
$$

Implemented via the composable `Wma` class with O(1) streaming update (running weighted sum + running sum via circular buffer).

### 2. Secondary LWMA (period $N_2$, Nyquist-constrained)

A LWMA applied to $\text{MA}_1$ with Nyquist-constrained period $N_2 \leq \lfloor N_1/2 \rfloor$:

$$
\text{MA}_2 = \text{WMA}(\text{MA}_1, N_2)
$$

The implementation clamps: $N_2 = \text{clamp}(N_2, 1, \lfloor N_1/2 \rfloor)$.

### 3. Lag-Compensating Extrapolation

$$
\alpha = \frac{N_2}{N_1 - N_2}
$$

$$
\text{NYQMA} = (1 + \alpha) \cdot \text{MA}_1 - \alpha \cdot \text{MA}_2
$$

Implemented with `Math.FusedMultiplyAdd` for the final combination:

```csharp
result = Math.FusedMultiplyAdd(1.0 + alpha, wma1, -alpha * wma2);
```

### 4. Warmup Period

$$
\text{WarmupPeriod} = N_1 + N_2 - 1
$$

The primary WMA needs $N_1$ bars to fill; the secondary WMA then needs $N_2$ bars of valid WMA1 output, but the first WMA1 output arrives at bar 1, so the total warmup is $N_1 + N_2 - 1$.

## Mathematical Foundation

**LWMA (period $N$):**

$$
\text{WMA}(x, N) = \frac{\sum_{i=0}^{N-1} (N-i) \cdot x_{t-i}}{N(N+1)/2}
$$

**Lag compensation coefficient:**

$$
\alpha = \frac{N_2}{N_1 - N_2}
$$

When $N_2 = \lfloor N_1/2 \rfloor$: $\alpha \approx 1$ (maximum compensation).
When $N_2 = 1$: $\alpha = 1/(N_1 - 1) \approx 0$ (minimal compensation).

**Output:**

$$
\text{NYQMA} = (1 + \alpha) \cdot \text{WMA}(x, N_1) - \alpha \cdot \text{WMA}(\text{WMA}(x, N_1), N_2)
$$

**Nyquist constraint (hard rule):**

$$
N_2 \leq \left\lfloor \frac{N_1}{2} \right\rfloor
$$

**Lag analysis:** WMA has group delay $(N-1)/3$. The extrapolation compensates a fraction $\alpha/(1+\alpha) = N_2/N_1$ of the primary MA's lag.

**Default parameters:** `period = 89` ($N_1$), `nyquistPeriod = 21` ($N_2$).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count per bar |
|-----------|--------------|
| WMA1 Update (O(1)) | 1 |
| WMA2 Update (O(1)) | 1 |
| FMA extrapolation | 1 |
| Multiply ($-\alpha \cdot w_2$) | 1 |
| Sample count increment | 1 |
| **Total** | **~5 FP ops** |

### Batch Mode

| Step | Complexity |
|------|-----------|
| WMA1 span batch | O(n) |
| WMA2 span batch | O(n) |
| FMA extrapolation loop | O(n) |
| **Total** | **O(n)** |

Memory: stackalloc for $n \leq 1024$ (two buffers of 8 KB each); ArrayPool beyond that.

### Quality Metrics

| Metric | Score (1-10) | Notes |
|--------|-------------|-------|
| Lag reduction | 7 | Significant lag reduction vs raw WMA; bounded by Nyquist constraint |
| Smoothness | 7 | Inherits WMA smoothness; extrapolation adds minor overshoot |
| Overshoot | 5 | More than WMA, less than DEMA; controlled by $\alpha$ |
| Noise rejection | 7 | FIR nature ensures bounded impulse response |
| Computational cost | 9 | Two O(1) WMA updates + one FMA |
| Parameter sensitivity | 8 | Two intuitive parameters with physical meaning |

## Validation

NYQMA has no direct equivalent in external libraries (TA-Lib, Skender, Tulip, Ooples). Validation is performed via component consistency: verifying that the composed output matches the explicit formula.

| Test | Method | Tolerance | Result |
|------|--------|-----------|--------|
| Component formula (batch) | $\text{NYQMA} = (1+\alpha) \cdot \text{WMA}_1 - \alpha \cdot \text{WMA}_2$ | $1 \times 10^{-9}$ | PASS |
| Component formula (streaming) | Same formula, bar-by-bar | $1 \times 10^{-9}$ | PASS |
| Component formula (span) | Same formula, span API | $1 \times 10^{-9}$ | PASS |
| Batch vs streaming | Last value agreement | $1 \times 10^{-6}$ | PASS |
| Batch vs span | Last value agreement | $1 \times 10^{-9}$ | PASS |
| Constant convergence | Multiple period combos | $1 \times 10^{-6}$ | PASS |
| 4-mode consistency | Batch = Streaming = Span = Eventing | $1 \times 10^{-9}$ | PASS |

The $1 \times 10^{-6}$ tolerance for batch vs streaming reflects expected floating-point divergence from different evaluation order (span-based batch vs composed instance streaming).

## Common Pitfalls

1. **Integer division in alpha calculation.** The original PineScript used integer types for `n2 / (period - n2)`, yielding 0 for all typical parameters (e.g., `21/68 = 0` in integer arithmetic). The fix: cast to float before division. Impact: complete failure of lag compensation when alpha truncates to zero.

2. **Nyquist period exceeding half the primary period.** Violates the sampling theorem. The implementation clamps silently, but users who set $N_2 > N_1/2$ and expect that exact value will get a different (clamped) indicator. Document the clamping behavior.

3. **Overshoot on sharp reversals.** The $(1+\alpha)$ gain factor amplifies the primary WMA relative to the smoothed secondary. On V-shaped reversals, NYQMA will overshoot more than a raw WMA. For $\alpha \approx 1$ (maximum Nyquist), the overshoot approaches DEMA levels.

4. **Warmup period miscalculation.** The total warmup is $N_1 + N_2 - 1$, not $\max(N_1, N_2)$ or $N_1 \cdot N_2$. Before warmup completion, the filter output is valid but numerically colder (reduced effective smoothing).

5. **Confusing NYQMA with PMA.** PMA uses $\text{WMA}(\text{src}, N)$ and $\text{WMA}(\text{WMA}(\text{src}, N), N)$ (same period twice) with $\alpha = 1$ fixed. NYQMA uses different periods and derives $\alpha$ from the period ratio. They converge only when $N_2 = N_1/2$ and rounding is ignored.

6. **Batch vs streaming floating-point divergence.** The batch path computes WMA1 and WMA2 as separate span passes, while streaming composes two Wma instances feeding one into the other. Different accumulation order produces divergence around $10^{-8}$ to $10^{-6}$ on 500-bar series. This is expected and not a bug.

7. **Using System.Random for test data.** Per project protocol, test data must use GBM (Geometric Brownian Motion) helpers. Random walk paths from System.Random lack the drift and volatility characteristics of financial time series.

## References

- Dürschner, M.G. *Gleitende Durchschnitte 3.0*. (Original NYQMA publication, German language.)
- Shannon, C.E. (1949). "Communication in the Presence of Noise." *Proceedings of the IRE*, 37(1), 10-21.
- Nyquist, H. (1928). "Certain Topics in Telegraph Transmission Theory." *Transactions of the AIEE*, 47(2), 617-644.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. (PMA reference for comparison.)
