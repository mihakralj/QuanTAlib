# NET: Ehlers Noise Elimination Technology

> *Rank the bars. Count the agreements. If the market is trending, the ranks will tell you — without a single moving average.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                           |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 14, ≥ 2)      |
| **Outputs**      | Single series (Net)              |
| **Output range** | [-1, +1]                         |
| **Warmup**       | `period` bars                    |
| **PineScript**   | [net.pine](net.pine)             |

- NET applies Kendall Tau-a rank correlation to a rolling window, measuring the degree of monotonic trend: +1 = perfectly rising, −1 = perfectly falling, 0 = no trend. Unlike Pearson correlation (CTI), Kendall tau is nonparametric and robust to outliers.
- **Similar:** [CTI](../../oscillators/cti/Cti.md) | **Complementary:** Moving averages for trend confirmation | **Trading note:** Values above +0.5 or below −0.5 indicate strong monotonic trend; zero crossings signal direction changes.
- No external validation libraries implement NET. Validated through self-consistency and behavioral testing.

NET measures the degree of monotonic ordering within a rolling window using Kendall's Tau-a concordance statistic. For each pair of bars in the window, it checks whether both price and time agree on direction (concordant) or disagree (discordant). The normalized difference (concordant − discordant) / total_pairs produces a bounded [-1, +1] output with zero lag — no smoothing filters involved.

## Historical Context

Published in *Technical Analysis of Stocks & Commodities* (December 2020), "Noise Elimination Technology — Clarify Your Indicators Using Kendall Correlation." Ehlers applies rank-order statistics to filter noise from any indicator output or price series without adding lag (unlike smoothing filters).

## Architecture & Physics

### Kendall Tau-a Concordance

For a window of $N$ values $X_0$ (newest) through $X_{N-1}$ (oldest), compute all $\binom{N}{2}$ pairs:

$$\tau = \frac{\sum_{i>k} -\text{sgn}(X_i - X_k)}{\frac{N(N-1)}{2}}$$

Where $i$ indexes older values and $k$ indexes newer values. When the series is rising (newer > older), $\text{sgn}(X_i - X_k) < 0$, so $-\text{sgn} > 0$, yielding positive $\tau$.

### No IIR State

NET is purely FIR — the output depends only on the current window contents. No recursive state means:
- Zero floating-point drift
- Perfect reset/restart behavior
- Bar correction is trivial (just replace newest buffer value)

### Bounded Output

The denominator $\frac{N(N-1)}{2}$ equals the total number of pairs. The numerator can range from $-\frac{N(N-1)}{2}$ (all discordant) to $+\frac{N(N-1)}{2}$ (all concordant), so $\tau \in [-1, +1]$.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation             | Count per bar        |
| :-------------------- | :------------------- |
| Comparisons (Sign)    | $\frac{N(N-1)}{2}$  |
| Subtractions          | $\frac{N(N-1)}{2}$  |
| Accumulation          | $\frac{N(N-1)}{2}$  |
| Final division        | 1 multiply           |

For $N = 14$: $\frac{14 \times 13}{2} = 91$ pair evaluations per bar.

### Batch Mode (SIMD Analysis)

Not SIMD-friendly: the inner loop has data-dependent branching (`Math.Sign`). The O(N²) nested loop structure prevents vectorization. For typical $N \leq 20$, the absolute cost is negligible.

### Quality Metrics

| Metric                  | Rating |
| :---------------------- | :----- |
| Lag (bars)              | 0 (no smoothing applied) |
| Overshoot              | None (bounded output) |
| Noise sensitivity      | Low (rank-based, immune to outlier magnitudes) |
| Computational cost     | O(N²) per bar |
| Memory                 | O(N) — one RingBuffer |

## Validation

Validated against mathematical properties of Kendall Tau-a:
- Perfectly rising sequence → $\tau = +1$
- Perfectly falling sequence → $\tau = -1$
- Constant input → $\tau = 0$
- Random input → $|\tau|$ small
- Bounded: all outputs $\in [-1, +1]$

### Behavioral Test Summary

| Test Category          | Tests | Description |
| :--------------------- | :---- | :---------- |
| Constructor            | 3     | Period validation, default values |
| Basic Calculation      | 4     | Core algorithm correctness |
| State / Bar Correction | 4     | Rollback consistency |
| Warmup / Convergence   | 3     | Cold → hot transition |
| Robustness             | 3     | NaN, Infinity, edge cases |
| Consistency            | 4     | All API modes match |
| Span API               | 2     | ReadOnlySpan paths |
| Chainability           | 2     | Event pipeline |
| NET-Specific           | 8     | Kendall properties, boundary conditions |

## Common Pitfalls

1. **Period too large**: O(N²) cost grows quadratically. Keep $N \leq 50$ for real-time use.
2. **Not a smoother**: NET does not smooth the input — it measures monotonic trend strength. Use it to filter *decisions*, not to filter *price*.
3. **Ties**: Tau-a does not adjust for ties. In continuous financial data, exact ties are rare. If ties are common (e.g., rounded data), consider Tau-b.
4. **Zero during warmup**: Before the buffer fills, NET returns 0 (not NaN). Check `IsHot` for valid readings.

## References

- Ehlers, J.F. "Noise Elimination Technology." *Technical Analysis of Stocks & Commodities*, December 2020.
- Kendall, M.G. "A New Measure of Rank Correlation." *Biometrika*, 1938.
