# ABERR: Aberration Bands

> *Aberration measures the distance between price and its smoothed self — when the gap grows extreme, reversion whispers.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `multiplier` (default 2.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [aberr.pine](aberr.pine)                       |

- ABERR measures price deviation from a central moving average using mean absolute deviation rather than standard deviation, producing dynamic bands that are more robust to outliers than Bollinger Bands.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.
- **Similar indicators:** [BBands](../bbands/bbands.md) (standard deviation bands), [SDChannel](../sdchannel/sdchannel.md) (standard deviation channel), [STBands](../stbands/stbands.md) (Stoller bands using ATR).
- **Complementary indicators:** Pair with a momentum oscillator such as RSI or Stochastic to confirm overbought/oversold signals when price touches the bands; volume indicators help distinguish genuine breakouts from false band penetrations.
- **Trading note:** Because MAD ≈ 0.80σ, ABERR bands are tighter than equivalently-scaled Bollinger Bands and produce fewer false signals during fat-tailed moves like earnings gaps or flash crashes.

ABERR measures price deviation from a central moving average using mean absolute deviation rather than standard deviation, producing dynamic bands that adapt to volatility while remaining robust against extreme outliers. Where Bollinger Bands amplify outliers through squaring (the $L^2$ norm), ABERR uses raw absolute differences (the $L^1$ norm), so bands respond to typical price behavior rather than the occasional spike that yanks everything sideways. For a 20-period window with a 2.0 multiplier, ABERR contains approximately 89% of normally-distributed price action, but its real advantage emerges with fat-tailed distributions where standard deviation overreacts to single-bar anomalies.

## Historical Context

The absolute deviation approach predates Bollinger's work by decades. Mean absolute deviation appears in early 20th-century statistics as a robust alternative to standard deviation, championed by statisticians who recognized that squaring deviations gives disproportionate weight to outliers. In financial markets, applying absolute deviation to band construction arrived after practitioners grew tired of watching Bollinger Bands blow out on single-bar anomalies such as flash crashes, earnings gaps, and fat-finger trades.

No single inventor claims credit for ABERR. The technique spread through trading floors where robustness mattered more than textbook elegance. The mathematical distinction is fundamental: standard deviation is a quadratic spring that amplifies outliers, while mean absolute deviation is a linear damper that treats all deviations proportionally. Under Gaussian assumptions, $\text{MAD} \approx 0.7979 \sigma$, so ABERR with multiplier 2.0 is roughly equivalent to Bollinger Bands with multiplier 1.6. But on real market data with kurtosis > 3, the gap widens in ABERR's favor.

## Architecture & Physics

### 1. Central Tendency (SMA)

The middle band is a Simple Moving Average over the lookback window:

$$\text{Middle}_t = \frac{1}{n} \sum_{i=0}^{n-1} x_{t-i}$$

### 2. Absolute Deviation

Each bar's deviation is measured against the previous middle band value:

$$d_t = |x_t - \text{Middle}_{t-1}|$$

### 3. Average Absolute Deviation

The deviation series is itself averaged over the same window:

$$\text{AvgDev}_t = \frac{1}{n} \sum_{i=0}^{n-1} d_{t-i}$$

### 4. Band Construction

$$\text{Upper}_t = \text{Middle}_t + k \cdot \text{AvgDev}_t$$

$$\text{Lower}_t = \text{Middle}_t - k \cdot \text{AvgDev}_t$$

### 5. Complexity

Both the SMA and the average deviation use circular buffers with running sums, yielding $O(1)$ per bar in streaming mode. The SIMD-accelerable portion is the final band construction step ($\text{Middle} \pm k \cdot \text{AvgDev}$), while the running-sum maintenance is inherently serial.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window for SMA and deviation averaging | 20 | $> 0$ |
| `multiplier` | Band width scale factor ($k$) | 2.0 | $> 0$ |
| `source` | Input price series | close | |
| `ma_line` | Pre-computed moving average (center line) | SMA | configurable |

### Relationship to Standard Deviation

For a normal distribution:

$$\text{MAD} = \sigma \sqrt{\frac{2}{\pi}} \approx 0.7979\,\sigma$$

Therefore ABERR with $k = 2.0$ captures approximately the same range as Bollinger Bands with $k \approx 1.596$.

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | Upper aberration band |
| `lower` | Lower aberration band |
| `avg_dev` | Current average absolute deviation (band half-width before scaling) |

## Performance Profile

### Operation Count (Streaming Mode)

ABERR maintains two running-sum ring buffers (SMA of price and SMA of absolute deviations), each updated in $O(1)$:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (oldest from running sum) | 2 | 1 | 2 |
| ADD (new value to running sum) | 2 | 1 | 2 |
| DIV (sum / count, two SMAs) | 2 | 15 | 30 |
| SUB (price - prevMiddle) | 1 | 1 | 1 |
| ABS (deviation) | 1 | 1 | 1 |
| MUL (multiplier × avgDev) | 1 | 3 | 3 |
| ADD/SUB (middle ± width) | 2 | 1 | 2 |
| **Total (hot)** | **11** | — | **~41 cycles** |

Warmup overhead is negligible: the ring buffer tracks count, adding one CMP per bar until full.

### Batch Mode (SIMD Analysis)

The running-sum SMA is inherently sequential (each bar depends on the previous running sum). SIMD parallelization across bars is not possible for the core SMA path:

| Optimization | Benefit |
| :--- | :--- |
| Band arithmetic (middle ± k × dev) | Vectorizable across output array with `Vector<double>` |
| ABS of deviations | Vectorizable with `Vector.Abs` for batch deviation pass |
| Running-sum maintenance | Sequential; cannot parallelize |

## Resources

- **Pham-Gia, T. & Hung, T.L.** "The Mean and Median Absolute Deviations." *Mathematical and Computer Modelling*, 34(7-8), 2001. (MAD vs. standard deviation theory)
- **Bollinger, J.** *Bollinger on Bollinger Bands*. McGraw-Hill, 2001. (Standard deviation band predecessor)
- **Hampel, F.R.** "The Influence Curve and its Role in Robust Estimation." *Journal of the American Statistical Association*, 69(346), 1974. (Robustness theory for $L^1$ vs $L^2$ norms)
