# VWAPSD: VWAP with Standard Deviation Bands

VWAP with Standard Deviation Bands combines the Volume Weighted Average Price with a single configurable standard deviation band pair, providing a simpler alternative to VWAPBANDS (which uses dual $\pm 1\sigma$ and $\pm 2\sigma$ levels). Three running sums enable O(1) streaming updates. A session reset mechanism clears accumulations at configurable intervals, keeping the indicator anchored to current market structure. The configurable deviation parameter allows traders to select their desired confidence level ($1\sigma$ ≈ 68%, $2\sigma$ ≈ 95%, $3\sigma$ ≈ 99.7%).

## Historical Context

VWAP emerged in the 1980s as institutional traders needed a benchmark reflecting actual market participation. Berkowitz, Logue, and Noser (1988) established VWAP as the standard for measuring execution quality. The concept is straightforward: weight each price by the volume traded at that price, producing an average that reflects where the most conviction-backed trading occurred.

The standard deviation extension follows the same reasoning as Bollinger Bands but applied to volume-weighted statistics. By adding bands at $n$ standard deviations from VWAP, the indicator creates a statistically grounded channel that adapts to actual volume-weighted volatility.

VWAPSD differs from VWAPBANDS only in output structure: VWAPSD emits one band pair at a configurable distance, while VWAPBANDS always emits two band pairs ($\pm 1\sigma$ and $\pm 2\sigma$). The underlying VWAP and variance calculations are identical.

## Architecture & Physics

### 1. Running Sum Accumulation

Three cumulative sums, reset at session boundaries:

$$
\Sigma_{pv} = \sum_{i=1}^{n} P_i \cdot V_i, \quad \Sigma_v = \sum_{i=1}^{n} V_i, \quad \Sigma_{p^2v} = \sum_{i=1}^{n} P_i^2 \cdot V_i
$$

where $P_i$ is the source price (typically HLC3) and $V_i$ is volume. Zero-volume bars are skipped to prevent distortion.

### 2. VWAP (Center Line)

$$
\text{VWAP}_t = \frac{\Sigma_{pv}}{\Sigma_v}
$$

### 3. Volume-Weighted Standard Deviation

Using the computational identity $\text{Var}(X) = E[X^2] - (E[X])^2$:

$$
\sigma^2 = \frac{\Sigma_{p^2v}}{\Sigma_v} - \text{VWAP}^2
$$

$$
\sigma = \sqrt{\max(0,\;\sigma^2)}
$$

### 4. Band Construction

$$
U_t = \text{VWAP}_t + k \cdot \sigma_t
$$

$$
L_t = \text{VWAP}_t - k \cdot \sigma_t
$$

where $k$ is the number of standard deviations (default 2.0).

### 5. Session Reset

On a reset condition, all running sums restart from zero. Configurable reset intervals include intraday (1m through 4H), daily, weekly, monthly, quarterly, semi-annual, annual, or never.

### 6. Complexity

Streaming: $O(1)$ per bar. Three additions to running sums, one division, one square root. Memory: three doubles for running sums plus scalar state (~64 bytes per instance).

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $k$ | numDevs | 2.0 | $0.1$ – $5.0$ | Number of standard deviations for bands |

### Pseudo-code

```
function vwapsd(source[], volume[], reset[], numDevs):
    sum_pv = 0, sum_vol = 0, sum_pv2 = 0

    for each bar t:
        price = source[t]
        vol   = volume[t]

        if reset[t]:
            if vol > 0:
                sum_pv  = price * vol
                sum_vol = vol
                sum_pv2 = price * price * vol
            else:
                sum_pv = 0, sum_vol = 0, sum_pv2 = 0
        else:
            if vol > 0:
                sum_pv  += price * vol
                sum_vol += vol
                sum_pv2 += price * price * vol

        vwap = sum_vol > 0 ? sum_pv / sum_vol : price

        variance = sum_vol > 0 ? sum_pv2 / sum_vol - vwap * vwap : 0
        stddev = sqrt(max(0, variance))

        upper = vwap + numDevs * stddev
        lower = vwap - numDevs * stddev

        emit (vwap, upper, lower)
```

### VWAPSD vs VWAPBANDS

| Aspect | VWAPSD | VWAPBANDS |
|--------|--------|-----------|
| Band pairs | 1 (configurable $k\sigma$) | 2 ($\pm 1\sigma$ and $\pm 2\sigma$) |
| Default deviation | 2.0 | 1.0 (inner); 2.0 (outer) |
| VWAP calculation | Identical | Identical |
| Variance calculation | Identical | Identical |

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Price above VWAP | Buyers paying above fair value; bullish intraday bias |
| Price below VWAP | Sellers accepting below fair value; bearish intraday bias |
| Price at upper band | Overextended above volume-weighted mean by $k\sigma$ |
| Price at lower band | Overextended below volume-weighted mean by $k\sigma$ |
| Band width expanding | Intraday volume-weighted dispersion increasing |
| Band width near zero | Very tight price clustering around VWAP |

## Performance Profile

### Operation Count (Streaming Mode)

VWAPSD is slightly simpler than VWAPBANDS (one band pair instead of two), with identical VWAP and variance computation:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (price × vol for sum_pv) | 1 | 3 | 3 |
| MUL (price² × vol for sum_pv2) | 2 | 3 | 6 |
| ADD (3 running sums) | 3 | 1 | 3 |
| DIV (sum_pv / sum_vol for VWAP) | 1 | 15 | 15 |
| DIV (sum_pv2 / sum_vol for E[X²]) | 1 | 15 | 15 |
| MUL (VWAP² for variance) | 1 | 3 | 3 |
| SUB (E[X²] - VWAP²) | 1 | 1 | 1 |
| SQRT (σ) | 1 | 20 | 20 |
| MUL (k × σ) | 1 | 3 | 3 |
| ADD/SUB (VWAP ± k·σ) | 2 | 1 | 2 |
| **Total (hot)** | **14** | — | **~71 cycles** |

Saves ~5 cycles vs VWAPBANDS by emitting 2 bands instead of 4. Session reset adds one CMP per bar.

### Batch Mode (SIMD Analysis)

Cumulative sums are inherently sequential. Band arithmetic is vectorizable:

| Optimization | Benefit |
| :--- | :--- |
| Running sum accumulation | Sequential (prefix sum dependency) |
| Variance → SQRT → bands | Vectorizable in a batch post-pass |
| Session reset detection | Sequential (comparison per bar) |

## Resources

- Berkowitz, S., Logue, D. & Noser, E. (1988). "The Total Cost of Transactions on the NYSE." *The Journal of Finance*, 43(1), 97–112.
- Kissell, R. (2013). *The Science of Algorithmic Trading and Portfolio Management*. Academic Press.
