# THEIL: Theil's T Index

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Theil)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [theil.pine](theil.pine)                       |

- The Theil T Index is an information-theoretic measure of inequality (or concentration) within a distribution of positive values.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The only useful measure of inequality is one that tells you how much redistribution would make everyone equally well off." — Henri Theil

## Introduction

The Theil T Index is an information-theoretic measure of inequality (or concentration) within a distribution of positive values. Originally developed for income inequality analysis, it quantifies how far a set of values deviates from perfect equality. In financial contexts, it measures the concentration of returns or price magnitudes within a sliding window, producing values ranging from 0 (perfect equality, all values identical) upward with no fixed upper bound. The Theil T Index belongs to the family of generalized entropy indices and is notable for its decomposability property: total inequality can be additively decomposed into between-group and within-group components.

## Historical Context

Henri Theil introduced the T Index in 1967 in his work "Economics and Information Theory," borrowing Shannon's entropy framework to measure economic inequality. Where Shannon entropy measures information content, Theil's adaptation measures the "information content" of observing a particular share of total resources relative to equal shares. The measure gained prominence alongside the Gini coefficient and Atkinson index as a standard tool in welfare economics.

For financial markets, the Theil T Index serves as a concentration detector. A window of prices with roughly equal magnitudes yields T near 0; a window dominated by one extreme value (a spike or crash) produces high T. This makes it useful for detecting regime changes, volatility clustering, and abnormal price behavior that other measures (like standard deviation) may underweight due to squaring.

The key advantage over Gini: decomposability. The key advantage over variance-based measures: scale invariance. Multiplying all prices by a constant leaves T unchanged, measuring only the relative distribution structure.

## Architecture and Physics

### 1. Core Algorithm

The implementation uses a sliding window (RingBuffer) of size `period`. On each update:

1. Add the new value to the buffer (substituting last-valid for NaN/Infinity/non-positive)
2. Compute the mean of all valid positive values in the buffer
3. For each value, compute the ratio $r_i = x_i / \mu$ and accumulate $r_i \cdot \ln(r_i)$
4. Divide the sum by the count of valid values

### 2. Complexity

- **Update:** O(n) per tick where n = period (must scan buffer for mean, then for Theil sum)
- **Memory:** O(period) for the RingBuffer
- No O(1) streaming shortcut exists because the mean changes with every update, invalidating cached ratio computations

### 3. Value Domain

- **Input:** Positive values only (prices, volumes). Non-positive values and NaN/Infinity are replaced with last-valid substitution.
- **Output:** T >= 0. T = 0 for perfect equality. No fixed upper bound; maximum depends on window size and value distribution.

### 4. NaN/Infinity Handling

Non-finite or non-positive inputs are replaced with the last valid positive value. If no valid value has been seen, the value defaults to 0 (which is filtered out in the Theil computation).

## Mathematical Foundation

The Theil T Index (also called GE(1), generalized entropy with parameter 1) is defined as:

$$T = \frac{1}{n} \sum_{i=1}^{n} \frac{x_i}{\mu} \ln\left(\frac{x_i}{\mu}\right)$$

where $\mu = \frac{1}{n}\sum_{i=1}^{n} x_i$ is the arithmetic mean.

### Properties

- **Non-negativity:** $T \geq 0$ always (Jensen's inequality applied to the convex function $f(r) = r \ln r$)
- **Scale invariance:** $T(cx_1, cx_2, \ldots, cx_n) = T(x_1, x_2, \ldots, x_n)$ for any $c > 0$
- **Perfect equality:** $T = 0$ if and only if all $x_i$ are equal
- **Decomposability:** For groups $G_k$ with means $\mu_k$ and sizes $n_k$:

$$T_{total} = T_{between} + \sum_k \frac{n_k}{n} \cdot \frac{\mu_k}{\mu} \cdot T_k$$

### Relationship to Other Measures

| Measure | Sensitivity | Scale Invariant | Decomposable |
|---------|-------------|-----------------|--------------|
| Theil T (GE(1)) | Upper tail | Yes | Yes |
| Theil L (GE(0)) | Lower tail | Yes | Yes |
| Gini | Middle | Yes | No |
| Variance | All | No | Yes |
| Shannon Entropy | Histogram-based | No | N/A |

## Performance Profile

### Operation Count (Streaming Mode)

Theil-Sen slope estimates the median of all pairwise slopes — O(N^2) per bar for exact computation.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Compute N*(N-1)/2 pairwise slopes | N^2/2 | 3 cy | ~1.5N^2 cy |
| Sort slopes for median | N^2/2 log(N^2/2) | 2 cy | ~N^2 log N cy |
| Extract median | 1 | 1 cy | ~1 cy |
| **Total (N=20)** | **O(N^2 log N)** | — | **~7000 cy** |

O(N^2 log N) per update — expensive for N > 30. Use only where robustness to outliers justifies compute cost. Batch pre-computation strongly preferred for historical analysis.

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Update (streaming) | O(n) | Two passes: mean then Theil sum |
| Batch (span) | O(n*m) | n = data length, m = period |
| Memory | O(period) | RingBuffer |
| SIMD potential | Limited | Sequential dependency on mean |

### Quality Metrics

| Metric | Score (1-10) |
|--------|-------------|
| Lag | 8 - Window-based, inherent period/2 lag |
| Noise sensitivity | 7 - Stable; log dampens outlier impact |
| Responsiveness | 6 - Full window recomputation each tick |
| Scale independence | 10 - Perfect scale invariance by construction |
| Mathematical rigor | 10 - Well-established information-theoretic foundation |

## Validation

No external TA library implements Theil T Index directly. Validation relies on mathematical properties.

| Property | Method | Status |
|----------|--------|--------|
| Equal values → T=0 | Unit test | Verified |
| Scale invariance | Multiply by constant, compare | Verified |
| Non-negativity | GBM random walk, 100 bars | Verified |
| Known values (manual) | Hand computation vs output | Verified |
| Streaming == Batch == Span | Three-way consistency | Verified |
| Higher inequality → higher T | Uniform vs skewed distribution | Verified |

## Common Pitfalls

1. **Non-positive values:** The Theil T Index requires strictly positive inputs. Zero or negative values produce undefined logarithms. The implementation substitutes last-valid values, but feeding predominantly non-positive data yields meaningless results.

2. **Confusing T and L:** Theil's T (GE(1)) is sensitive to the upper tail; Theil's L (GE(0), mean log deviation) is sensitive to the lower tail. This implementation computes T only.

3. **Interpreting magnitude:** Unlike Gini (bounded [0,1]), Theil T has no fixed upper bound. Values must be interpreted relative to the data's own history, not against absolute thresholds.

4. **Small windows:** With period=2, only two values are compared. The index becomes highly volatile and loses statistical meaning. Recommend period >= 10 for meaningful results.

5. **All-equal series:** Returns exactly 0. This is correct behavior, not a bug. A constant price series has zero inequality by definition.

6. **Log(1) = 0 effect:** When a value equals the mean exactly, its contribution to T is zero (ratio=1, ln(1)=0). This is mathematically correct but means the index is insensitive to values near the mean.

7. **Comparison with Shannon Entropy:** Shannon entropy measures histogram-based randomness; Theil T measures value-based concentration. They answer different questions about the same data.

## References

- Theil, H. (1967). *Economics and Information Theory*. North-Holland Publishing Company.
- Cowell, F.A. (2011). *Measuring Inequality*. Oxford University Press. 3rd edition.
- Conceicao, P., & Ferreira, P. (2000). "The Young Person's Guide to the Theil Index." UTIP Working Paper No. 14.
- Shorrocks, A.F. (1980). "The Class of Additively Decomposable Inequality Measures." *Econometrica*, 48(3), 613-625.
