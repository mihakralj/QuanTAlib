# VWAPBANDS: VWAP with Dual Standard Deviation Bands

> *VWAP anchored by dual deviation bands reveals where volume-weighted fair value ends and excess begins.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `multiplier` (default DefaultMultiplier)                      |
| **Outputs**      | Multiple series (Upper1, Lower1, Upper2, Lower2, Vwap, StdDev, Width)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [vwapbands.pine](vwapbands.pine)                       |

- VWAP Bands extend the Volume Weighted Average Price with dual standard deviation bands at $\pm 1\sigma$ and $\pm 2\sigma$ levels, creating a five-l...
- Parameterized by `multiplier` (default defaultmultiplier).
- Output range: Tracks input.
- Requires `2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

VWAP Bands extend the Volume Weighted Average Price with dual standard deviation bands at $\pm 1\sigma$ and $\pm 2\sigma$ levels, creating a five-line channel system anchored to volume-weighted fair value. Three running sums (cumulative price×volume, cumulative volume, cumulative price²×volume) enable O(1) streaming updates per bar. A session reset mechanism clears accumulations at configurable intervals, keeping the indicator anchored to current market structure.

## Historical Context

VWAP emerged in the 1980s as institutional traders sought a benchmark reflecting actual market participation rather than simple price averages. Berkowitz, Logue, and Noser (1988) established VWAP as the standard for measuring execution quality: buying below VWAP or selling above it indicates favorable execution relative to the market's true average price.

The extension to standard deviation bands follows the same statistical reasoning as Bollinger Bands: use standard deviation to quantify price dispersion around a central tendency. The critical difference is that VWAP weights by volume, so prices where heavy trading occurred contribute proportionally more to both the average and the deviation. This makes VWAPBANDS particularly meaningful for institutional traders benchmarking execution quality.

The dual-band structure creates distinct statistical zones. The $\pm 1\sigma$ bands capture approximately 68% of price action (normal trading zone). The $\pm 2\sigma$ bands capture approximately 95% (extreme deviation zone). Price beyond $\pm 2\sigma$ represents a statistically significant departure from volume-weighted fair value.

## Architecture & Physics

### 1. Running Sum Accumulation

Three cumulative sums, reset at session boundaries:

$$
\Sigma_{pv} = \sum_{i=1}^{n} P_i \cdot V_i, \quad \Sigma_{v} = \sum_{i=1}^{n} V_i, \quad \Sigma_{p^2v} = \sum_{i=1}^{n} P_i^2 \cdot V_i
$$

where $P_i$ is the source price (typically HLC3) and $V_i$ is volume at bar $i$. Zero-volume bars are skipped.

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

The $\max(0, \cdot)$ guard prevents negative variance from floating-point accumulation errors.

### 4. Dual Band Construction

$$
U_{1,t} = \text{VWAP}_t + k \cdot \sigma_t, \qquad L_{1,t} = \text{VWAP}_t - k \cdot \sigma_t
$$

$$
U_{2,t} = \text{VWAP}_t + 2k \cdot \sigma_t, \qquad L_{2,t} = \text{VWAP}_t - 2k \cdot \sigma_t
$$

where $k$ is the multiplier (default 1.0). With $k = 1$, the bands are at standard $1\sigma$ and $2\sigma$ levels.

### 5. Session Reset

On a reset condition (e.g., new trading day), all running sums restart from zero. This prevents stale historical data from dominating the calculation and keeps the indicator anchored to the current session.

### 6. Complexity

Streaming: $O(1)$ per bar. Three additions to running sums, one division, one square root. No buffers or window scans. Memory: three doubles for running sums plus scalar state.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $k$ | multiplier | 1.0 | $> 0$ | Scales the standard deviation for band width |

### Statistical Zone Interpretation

| Zone | Coverage | Interpretation |
|------|----------|----------------|
| Within $\pm 1\sigma$ | ~68% | Normal trading range; institutional execution zone |
| $\pm 1\sigma$ to $\pm 2\sigma$ | ~27% | Alert zone; elevated deviation from fair value |
| Beyond $\pm 2\sigma$ | ~5% | Extreme deviation; statistically significant move |

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Price above VWAP | Buyers paying above fair value; bullish bias |
| Price below VWAP | Sellers accepting below fair value; bearish bias |
| $\sigma$ increasing | Volume-weighted dispersion growing |
| Bands expanding | Intraday volatility increasing |

## Performance Profile

### Operation Count (Streaming Mode)

VWAPBANDS maintains three cumulative running sums plus variance computation and dual band construction — all $O(1)$:

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
| MUL (k × σ, 2k × σ) | 2 | 3 | 6 |
| ADD/SUB (VWAP ± 1σ, ± 2σ, 4 bands) | 4 | 1 | 4 |
| **Total (hot)** | **17** | — | **~76 cycles** |

Session reset adds a CMP per bar. The two DIV operations and SQRT dominate. No buffers required — purely cumulative sums.

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
