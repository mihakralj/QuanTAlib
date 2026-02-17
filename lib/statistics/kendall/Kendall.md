# KENDALL: Kendall Tau-a Rank Correlation Coefficient

> "The rank is the message." -- adapted from Marshall McLuhan

<!-- QUICK REFERENCE CARD (scan in 5 seconds) -->

| Property     | Value |
|--------------|-------|
| Category     | Statistic |
| Inputs       | Two source series (e.g., Close vs Open, or two separate instruments) |
| Parameters   | `period` (int, default: 20, valid: >= 2) |
| Outputs      | double (single value) |
| Output range | -1 to +1 |
| Warmup       | 2 bars (produces finite output), `period` bars (full window) |

### Key takeaways

- Kendall Tau measures ordinal (rank-based) association between two series by counting concordant and discordant pairs.
- Primary use case: detecting monotonic relationships that Pearson correlation misses, because Kendall ignores magnitude.
- Unlike Pearson, Kendall is robust to outliers and non-linear monotonic relationships.
- O(n^2) per update makes it unsuitable for very large lookback periods (>60 recommended max).
- A Tau-a value of 0 does not mean independence; it means no net concordance/discordance among pairs.

## Historical Context

Maurice Kendall introduced the Tau coefficient in 1938 as a non-parametric measure of ordinal association. While Pearson's correlation (1896) measures linear relationships using raw values, Kendall recognized that many real-world relationships are monotonic but not linear. His approach counts concordant and discordant pairs without any distributional assumptions.

The Tau-a variant is the simplest form that does not adjust for tied pairs. Tau-b and Tau-c provide tie corrections, but for continuous financial data, ties are rare enough that Tau-a suffices. The Pine Script reference implementation uses Tau-a, and this implementation follows that convention.

In quantitative finance, Kendall Tau finds use in pairs trading (measuring rank agreement between two instruments), regime detection (tracking how ordinal relationships change over time), and risk management (capturing non-linear dependence structures that Pearson misses).

## What It Measures and Why It Matters

Kendall Tau answers a specific question: when one series goes up, does the other tend to go up (concordance) or down (discordance)? It does this by examining every possible pair of observations within the lookback window and classifying each as concordant, discordant, or tied.

This matters because financial returns often exhibit monotonic but non-linear relationships. Two assets might move in the same direction without proportional magnitudes. Pearson correlation weights large moves heavily (it uses raw differences from means), while Kendall treats every directional agreement equally. This makes Kendall more robust when you care about directional consistency rather than magnitude scaling.

The coefficient ranges from -1 (every pair disagrees) through 0 (no net tendency) to +1 (every pair agrees). For financial data, values beyond +/-0.5 indicate strong rank association.

## Mathematical Foundation

### Core Formula

$$
\tau_a = \frac{C - D}{\binom{n}{2}} = \frac{C - D}{\frac{n(n-1)}{2}}
$$

where:

- $C$ = number of concordant pairs
- $D$ = number of discordant pairs
- $n$ = number of observations in the lookback window
- $\binom{n}{2}$ = total number of distinct pairs

### Pair Classification

For observations $(x_i, y_i)$ and $(x_j, y_j)$ where $i < j$:

$$
\text{Concordant if } (x_i - x_j)(y_i - y_j) > 0
$$

$$
\text{Discordant if } (x_i - x_j)(y_i - y_j) < 0
$$

$$
\text{Tied if } (x_i - x_j)(y_i - y_j) = 0
$$

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period`  | $n$    | 20      | $n \geq 2$ |

### Warmup Period

$$
\text{WarmupPeriod} = n
$$

The indicator produces finite values after 2 observations, but the full window requires $n$ bars.

## Architecture and Physics

The implementation uses two `RingBuffer` instances (one per series) to maintain the sliding window. On each update, the entire O(n^2) pairwise comparison is recalculated from the buffers.

### Why No Running Sums

Unlike Pearson correlation (which maintains running sums of x, y, xy, x^2, y^2), Kendall Tau cannot be incrementally updated when the window slides. Adding or removing a single observation affects its relationship with every other observation in the window. There is no algebraic shortcut to adjust concordant/discordant counts when one element enters and another leaves.

### Update Flow

1. Sanitize inputs (NaN/Infinity substitution with last valid value)
2. Add to buffers (`isNew=true`) or update newest (`isNew=false`)
3. Iterate all $\binom{n}{2}$ pairs, counting concordant and discordant
4. Compute $\tau = (C - D) / \binom{n}{2}$

### Edge Cases

- **NaN/Infinity inputs**: Substituted with last valid value per series. If no valid value exists, 0.0 is used.
- **Constant series**: All pair products are 0, resulting in $\tau = 0$.
- **Single observation**: Returns NaN (need at least 2 for a pair).
- **Division by zero**: Denominator $n(n-1)/2$ is zero only when $n < 2$, which returns NaN.

## Interpretation and Signals

### Signal Zones

| Zone | Level | Interpretation |
|------|-------|----------------|
| Strong concordance | > 0.5 | Series consistently move in same direction |
| Weak concordance | 0.1 to 0.5 | Mild directional agreement |
| No association | -0.1 to 0.1 | No consistent directional pattern |
| Weak discordance | -0.5 to -0.1 | Mild directional disagreement |
| Strong discordance | < -0.5 | Series consistently move in opposite directions |

### Signal Patterns

- **Regime detection**: Track $\tau$ over time. Sudden drops from positive to negative suggest relationship breakdown, common before market dislocations.
- **Pairs trading**: High positive $\tau$ between two instruments suggests directional co-movement suitable for mean-reversion strategies.
- **Divergence**: When Pearson correlation and Kendall $\tau$ disagree meaningfully, it signals that the relationship is driven by a few large moves (outliers) rather than consistent directional agreement.

### Practical Notes

Kendall Tau works best with moderate lookback periods (10-30). Very short periods yield noisy estimates. Very long periods (>60) become computationally expensive at O(n^2) per bar. For real-time streaming, keep the period under 60 to avoid latency.

## Related Indicators

- **[Correlation](../correlation/Correlation.md)**: Pearson coefficient. Measures linear (not just monotonic) relationships. Faster O(1) updates but sensitive to outliers.
- **[Covariance](../covariance/Covariance.md)**: Unstandardized measure of joint variability. Building block for Pearson but not rank-based.

## Validation

Validated against known mathematical results and properties in `Kendall.Validation.Tests.cs`.
No standard TA library (TA-Lib, Skender, Tulip, Ooples) implements Kendall Tau directly.

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | -- | -- | -- | Not available |
| **Skender** | -- | -- | -- | Not available |
| **Tulip** | -- | -- | -- | Not available |
| **Ooples** | -- | -- | -- | Not available |
| **Math properties** | ✓ | ✓ | ✓ | Known-value, symmetry, antisymmetry validated |

## Performance Profile

### Key Optimizations

- **No SIMD**: The pairwise comparison involves data-dependent branching (concordant vs discordant), making vectorization impractical.
- **Aggressive inlining**: `CalculateTau()` and `SanitizeX/Y` are inlined.
- **No heap allocation**: All state lives in `RingBuffer`; no per-update allocations.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| CMP (pair) | $n(n-1)/2$ | 1 | $\binom{n}{2}$ |
| MUL (product) | $n(n-1)/2$ | 3 | $3\binom{n}{2}$ |
| ADD (counters) | $n(n-1)/2$ | 1 | $\binom{n}{2}$ |
| DIV (final) | 1 | 15 | 15 |
| **Total** | -- | -- | **~5n^2/2** |

For period=20: ~1000 cycles per update. For period=60: ~9000 cycles.

## Common Pitfalls

1. **O(n^2) complexity**: Each `Update()` call performs $n(n-1)/2$ pair comparisons. Keep `period` reasonable (<60) for real-time use.
2. **Tau-a vs Tau-b**: This implements Tau-a, which does not adjust for ties. For discrete data with many ties, Tau-b would be more appropriate (divide by geometric mean of non-tied pairs instead).
3. **isNew parameter**: When `isNew=false`, the newest buffer entries are overwritten (bar correction). Since Kendall recalculates from buffers, this is inherently safe.
4. **Not a test of independence**: $\tau = 0$ means no net concordance/discordance, not statistical independence.
5. **Confidence interpretation**: For small samples ($n < 10$), $\tau$ has high variance. Values should be interpreted cautiously without additional hypothesis testing.
6. **Comparison with Pearson**: Kendall $\tau$ values are typically smaller in magnitude than Pearson $r$ for the same data. Do not compare them directly.
7. **NaN handling**: Non-finite inputs are replaced with the last valid value per series. Extended NaN sequences cause the buffer to fill with repeated values, reducing effective sample size.

## References

- Kendall, M. G. (1938). "A New Measure of Rank Correlation." *Biometrika*, 30(1/2), 81-93.
- Kendall, M. G. (1948). *Rank Correlation Methods*. Charles Griffin & Company.
- Abdi, H. (2007). "Kendall Rank Correlation." In *Encyclopedia of Measurement and Statistics*, Sage Publications.
- [Wikipedia: Kendall rank correlation coefficient](https://en.wikipedia.org/wiki/Kendall_rank_correlation_coefficient)
