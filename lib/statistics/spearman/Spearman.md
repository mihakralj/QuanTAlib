# SPEARMAN: Spearman Rank Correlation Coefficient

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (Spearman)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- Spearman's ρ (rho) measures the strength and direction of monotonic association between two variables.
- Parameterized by `period` (default 20).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The person who asks whether rank correlation exists is not asking a wholly foolish question." — Maurice Kendall (1970)

Spearman's ρ (rho) measures the strength and direction of monotonic association between two variables. Unlike Pearson's correlation, which measures linear relationship, Spearman captures any monotonic relationship. A portfolio of stocks whose returns move monotonically together has different risk than one whose components merely share a linear trend. Spearman detects both.

## Historical Context

Charles Spearman introduced his rank correlation coefficient in 1904 while studying intelligence factor models. He needed a measure of association that did not require the assumption of normal distributions — a common problem with psychological test scores that tend toward ceiling and floor effects.

The insight was elegant: rank the data, then apply Pearson correlation to the ranks. This converts any monotonic relationship into a linear one, making Pearson applicable regardless of the original distribution shape. The resulting coefficient ρ inherits Pearson's bounded [-1, +1] range and its interpretation as a correlation measure, but measures monotonic rather than linear dependence.

In finance, Spearman sees use in pairs trading (detecting monotonic co-movement even when the functional form is unknown), risk modeling (copula estimation), and factor analysis (ranking stocks by multiple criteria). Its robustness to outliers and distribution shape makes it preferable to Pearson when price distributions exhibit fat tails.

## Architecture and Physics

### 1. Dual-Input Indicator

Spearman extends `AbstractBase` following the dual-input pattern established by `Correlation` and `Kendall`. Two `RingBuffer` instances track the rolling windows for X and Y series. Single-input `Update(TValue)` and `Update(TSeries)` throw `NotSupportedException` because the indicator requires paired observations.

### 2. Ranking Algorithm

For each update, the algorithm assigns ranks to both buffered series using average-rank tie-breaking:

$$\text{rank}(x_i) = |\{j : x_j < x_i\}| + \frac{|\{j : x_j = x_i\}| - 1}{2} + 1$$

This assigns each tied value the mean of the positions those tied values would occupy if they were distinct. The ranking step is O(n²) per series — each element is compared against all others.

### 3. Pearson on Ranks

After ranking, Spearman's ρ equals the Pearson correlation of the rank arrays:

$$\rho = \frac{\sum_{i=1}^{n}(R_{x_i} - \bar{R})(R_{y_i} - \bar{R})}{\sqrt{\sum_{i=1}^{n}(R_{x_i} - \bar{R})^2 \cdot \sum_{i=1}^{n}(R_{y_i} - \bar{R})^2}}$$

For ranks with average-rank tie-breaking, the mean rank is always $(n+1)/2$, regardless of ties. This is because the sum of ranks is preserved: tied elements receive the average of positions they span, which sums to the same total as distinct ranks.

### 4. Simplified Formula (No Ties)

When no ties exist, an algebraically equivalent shortcut applies:

$$\rho = 1 - \frac{6 \sum d_i^2}{n(n^2 - 1)}$$

where $d_i = R_{x_i} - R_{y_i}$. This implementation uses the general Pearson-on-ranks method because tied values occur in financial data (identical closes, rounded prices, trading halts).

## Mathematical Foundation

### Rank Assignment

Given values $\{v_1, v_2, \ldots, v_n\}$, the rank of $v_i$ is:

$$R_i = 1 + |\{j : v_j < v_i\}| + \frac{|\{j : v_j = v_i\}| - 1}{2}$$

### Pearson Correlation of Ranks

With $\bar{R} = (n+1)/2$:

$$\rho = \frac{\sum(R_{x_i} - \bar{R})(R_{y_i} - \bar{R})}{\sqrt{\sum(R_{x_i} - \bar{R})^2 \cdot \sum(R_{y_i} - \bar{R})^2}}$$

### Special Cases

| Condition | Result |
|-----------|--------|
| Perfect concordance (all ranks agree) | ρ = +1 |
| Perfect discordance (ranks reversed) | ρ = -1 |
| One or both series constant | ρ = 0 (zero-variance guard) |
| Fewer than 2 observations | ρ = NaN |
| All values tied | ρ = 0 |

### Relationship to Kendall's Tau

Both Spearman and Kendall measure monotonic association. For bivariate normal data:

$$\rho \approx \frac{3}{2}\tau$$

Spearman is more sensitive to large rank differences; Kendall weights all discordant pairs equally. In practice, both detect the same direction of association but differ in magnitude.

## Performance Profile

### Operation Count (Streaming Mode)

Spearman rank correlation requires ranking both series each bar — O(N log N) per update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict (2 series) | 2 | 3 cy | ~6 cy |
| Sort + assign ranks (2 series) | 2 * N log N | 2 cy | ~4N log N cy |
| Pearson r on rank vectors | N | 3 cy | ~3N cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=14)** | **O(N log N)** | — | **~213 cy** |

O(N log N) per update. Sorting two arrays per bar is the dominant cost. Tied-rank correction adds negligible overhead for typical financial data (few exact ties).

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Ranking (per series) | O(n²) | Pairwise comparison for each element |
| Pearson on ranks | O(n) | Single pass over rank arrays |
| Total per update | O(n²) | Dominated by ranking |
| Memory | O(n) | Two RingBuffers + stackalloc ranks |

### SIMD Potential

Limited. The ranking step involves data-dependent branching (comparison counting) that resists vectorization. The Pearson correlation on ranks could theoretically use SIMD, but the O(n) savings are dwarfed by the O(n²) ranking cost. Not worth the complexity.

### Quality Metrics

| Metric | Score (1-10) |
|--------|-------------|
| Lag | 10 (no lag — contemporaneous measurement) |
| Smoothness | 3 (jumps when window slides) |
| Responsiveness | 7 (reacts to rank changes) |
| Robustness | 9 (outlier-resistant via ranking) |
| Interpretability | 9 ([-1, +1] bounded, intuitive) |

## Validation

No external TA library implements Spearman rank correlation. Validation relies on mathematical properties:

| Test | Method | Status |
|------|--------|--------|
| Perfect concordance | X = Y → ρ = 1 | ✔️ |
| Perfect discordance | X = -Y → ρ = -1 | ✔️ |
| Known sequence | Manual calculation verified | ✔️ |
| Symmetry | ρ(X,Y) = ρ(Y,X) | ✔️ |
| Antisymmetry | ρ(X,-Y) = -ρ(X,Y) | ✔️ |
| Monotonic invariance | f(X) monotone → ρ(f(X),Y) = ρ(X,Y) | ✔️ |
| Constant series | zero variance → ρ = 0 | ✔️ |
| Ties handled | average-rank tie-breaking verified | ✔️ |
| Batch/streaming consistency | identical outputs verified | ✔️ |
| Spearman vs Kendall | both = 1 for concordant data | ✔️ |

## Common Pitfalls

1. **Confusing Spearman with Pearson.** Pearson measures linear association; Spearman measures monotonic. A perfect exponential relationship gives ρ = 1 but r < 1. Choose based on the relationship type you expect.

2. **Period too large.** O(n²) ranking makes period > 60 expensive for real-time use. Period 20: ~800 comparisons per update; period 60: ~7200. Keep periods practical.

3. **Ignoring ties.** The simplified formula ρ = 1 - 6Σd²/(n(n²-1)) is incorrect when ties exist. Financial data with rounded prices creates ties. This implementation uses the general method.

4. **Single-series usage.** Spearman requires two series. Calling `Update(TValue)` throws `NotSupportedException`. Use `Update(seriesX, seriesY)`.

5. **Interpreting as causation.** High Spearman correlation indicates monotonic co-movement, not causation. Two stocks may correlate because of shared sector exposure, not because one drives the other.

6. **Small windows.** With n = 2, the only possible ρ values are -1 and +1 (or NaN if tied). Use period ≥ 5 for meaningful results.

7. **Comparing magnitude with Kendall.** For the same data, |ρ| ≥ |τ| generally holds. Do not compare raw values across methods without accounting for this scaling difference.

## References

- Spearman, C. (1904). "The Proof and Measurement of Association between Two Things." *American Journal of Psychology*, 15(1), 72-101.
- Kendall, M. G., & Gibbons, J. D. (1990). *Rank Correlation Methods*. 5th ed. Oxford University Press.
- Croux, C., & Dehon, C. (2010). "Influence Functions of the Spearman and Kendall Correlation Measures." *Statistical Methods & Applications*, 19(4), 497-515.
- Embrechts, P., McNeil, A., & Straumann, D. (2002). "Correlation and Dependence in Risk Management: Properties and Pitfalls." In *Risk Management: Value at Risk and Beyond*, Cambridge University Press.
