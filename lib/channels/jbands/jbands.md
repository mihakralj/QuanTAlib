# JBANDS: Jurik Adaptive Envelope Bands

> *Jurik's adaptive envelope adjusts its width with price dynamics, hugging trends and releasing during consolidation.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `phase` (default 0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `⌈20 + 80 × period^0.36⌉` bars                          |
| **PineScript**   | [Jbands.pine](Jbands.pine)                       |

- JBANDS expose the internal adaptive envelope mechanism of the Jurik Moving Average (JMA), producing asymmetric bands that snap instantly to new pri...
- **Similar:** [BBands](../bbands/bbands.md), [KC](../kc/kc.md) | **Complementary:** Momentum oscillators for reversal timing | **Trading note:** Uses JMA (Jurik Moving Average) as center line for smoother, lower-lag bands compared to standard Bollinger.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

JBANDS expose the internal adaptive envelope mechanism of the Jurik Moving Average (JMA), producing asymmetric bands that snap instantly to new price extremes and decay exponentially during consolidation. Unlike standard volatility bands (Bollinger, Keltner) which maintain symmetric width around a center line, JBANDS feature "snap-and-decay" hysteresis: expansion is instantaneous (plasticity), contraction is gradual (elasticity). The decay rate is dynamically modulated by a two-stage volatility estimator — a 10-bar SMA feeding a 128-bar trimmed mean — making the bands tight during quiet markets and expansive during trends. The center line is the full JMA: a 2-pole IIR filter with phase control and adaptive alpha.

## Historical Context

Mark Jurik of Jurik Research developed the Jurik Moving Average and its associated bands in the 1990s as a proprietary commercial tool optimized for real-world trading. Unlike academic indicators, JMA was designed with emphasis on reducing lag while maintaining smoothness, using adaptive volatility modulation to adjust bandwidth dynamically.

The "snap-and-decay" behavior draws from hysteresis in physics — systems that respond differently to increasing versus decreasing inputs. When price moves to a new extreme, the band deforms immediately (plastic response). When price retreats, the band recovers gradually (elastic response). This asymmetry matches empirical market behavior: breakouts are sudden, consolidations are gradual. The two-stage volatility engine (local deviation → SMA → trimmed mean) provides robust reference volatility that resists contamination by outliers.

## Architecture & Physics

### 1. Local Deviation

The maximum absolute distance from price to either band:

$$d_{\text{local}} = \max(|x_t - \text{Upper}_{t-1}|,\; |x_t - \text{Lower}_{t-1}|) + \epsilon$$

### 2. Two-Stage Volatility Estimation

**Stage 1**: 10-bar SMA of local deviation:

$$\text{highD}_t = \text{SMA}(d_{\text{local}},\; 10)$$

**Stage 2**: 128-bar trimmed mean (discard top/bottom 25% when full, or 25% of available count during warmup):

$$d_{\text{ref}} = \text{TrimmedMean}(\text{highD values},\; 128)$$

### 3. Dynamic Exponent

$$\text{ratio} = \frac{|x_t - \text{band}|}{d_{\text{ref}}}$$

$$d = \min\left(\max\left(\text{ratio}^{P_{\text{exp}}},\; 1\right),\; \log_2\sqrt{\frac{P-1}{2}} + 2\right)$$

Where $P_{\text{exp}} = \max(\log_2\sqrt{(P-1)/2},\; 0.5)$.

### 4. Adaptive Decay

The adaptation factor uses the precomputed $\text{sqrtDiv}$:

$$\alpha_{\text{band}} = \text{sqrtDiv}^{\sqrt{d}}$$

### 5. Snap-and-Decay Band Update

$$\text{Upper}_t = \begin{cases} x_t & \text{if } x_t > \text{Upper}_{t-1} \\ x_t - (x_t - \text{Upper}_{t-1}) \cdot \alpha_{\text{band}} & \text{otherwise} \end{cases}$$

$$\text{Lower}_t = \begin{cases} x_t & \text{if } x_t < \text{Lower}_{t-1} \\ x_t - (x_t - \text{Lower}_{t-1}) \cdot \alpha_{\text{band}} & \text{otherwise} \end{cases}$$

### 6. JMA Center Line (2-Pole IIR)

$$\alpha_{\text{jma}} = \text{lenDiv}^d$$

$$c_0 = (1 - \alpha_{\text{jma}}) \cdot x_t + \alpha_{\text{jma}} \cdot c_{0,t-1}$$

$$c_8 = (x_t - c_0)(1 - \text{lenDiv}) + \text{lenDiv} \cdot c_{8,t-1}$$

$$a_8 = (\text{phase} \cdot c_8 + c_0 - \text{JMA}_{t-1}) \cdot (1 + \alpha_{\text{jma}}^2 - 2\alpha_{\text{jma}}) + \alpha_{\text{jma}}^2 \cdot a_{8,t-1}$$

$$\text{JMA}_t = \text{JMA}_{t-1} + a_8$$

### 7. Complexity

Dominated by the trimmed mean's partial sort: $O(n \log n)$ for the 128-element buffer. All other operations are $O(1)$. In practice, the 128-element sort is fast due to cache-friendly size.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Nominal lookback length | 10 | $> 0$ |
| `phase` | Controls JMA overshoot/smoothness | 0 | $[-100, 100]$ |
| `source` | Input price series | close | |

### Precomputed Constants (from period and phase)

| Constant | Formula |
|----------|---------|
| $\text{\_PHASE}$ | $\text{phase}/100 + 1.5$, clamped to $[0.5, 2.5]$ |
| $\text{\_LEN0}$ | $(P - 1) / 2$ |
| $\text{\_LOG\_PARAM}$ | $\max(\log_2\sqrt{\text{\_LEN0}} + 2,\; 0)$ |
| $\text{\_SQRT\_PARAM}$ | $\sqrt{\text{\_LEN0}} \cdot \text{\_LOG\_PARAM}$ |
| $\text{lenDiv}$ | $\text{\_LEN0} \cdot 0.9 / (\text{\_LEN0} \cdot 0.9 + 2)$ |
| $\text{sqrtDiv}$ | $\text{\_SQRT\_PARAM} / (\text{\_SQRT\_PARAM} + 1)$ |
| $P_{\text{exp}}$ | $\max(\text{\_LOG\_PARAM} - 2,\; 0.5)$ |

### Output Interpretation

| Output | Description |
|--------|-------------|
| `middle` | JMA center line (adaptive low-lag smoothed price) |
| `upper` | Adaptive upper envelope (snaps up, decays down) |
| `lower` | Adaptive lower envelope (snaps down, decays up) |

## Performance Profile

### Operation Count (Streaming Mode)

JBANDS is the most complex channel indicator, combining snap-and-decay bands, a two-stage volatility estimator, and a 2-pole JMA IIR filter:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB + ABS (local deviation, 2 distances) | 3 | 1 | 3 |
| CMP (max of 2 for dLocal) | 1 | 1 | 1 |
| SMA update (10-bar highD, running sum) | 3 | 1 | 3 |
| Partial sort (128-bar trimmed mean) | ~900 | 1 | ~900 |
| DIV (ratio = distance / dRef) | 2 | 15 | 30 |
| POW (ratio^Pexp) | 2 | 30 | 60 |
| SQRT (√d) | 2 | 20 | 40 |
| POW (sqrtDiv^√d for adapt) | 2 | 30 | 60 |
| MUL + SUB (snap-decay, 2 bands) | 4 | 3 | 12 |
| JMA IIR (3 recursion stages) | ~8 | 4 | 32 |
| **Total** | **~930** | — | **~1141 cycles** |

The 128-element trimmed mean (partial sort) dominates. In practice, the sort operates on a cache-friendly 1 KB buffer, making actual latency lower than raw cycle count suggests. The JMA IIR adds ~32 cycles per bar, comparable to a double-EMA.

### Batch Mode (SIMD Analysis)

The JMA IIR and snap-decay bands are recursive, preventing SIMD parallelization across bars. The trimmed mean sort is $O(n \log n)$ on a fixed 128-element buffer:

| Optimization | Benefit |
| :--- | :--- |
| Trimmed mean | Fixed 128 elements; fits in L1 cache; intrinsics-friendly sort |
| JMA 2-pole IIR | Sequential (3-stage recursion) |
| Snap-and-decay bands | Sequential (conditional state updates) |
| POW/SQRT computations | Hardware-accelerated; no vectorization opportunity |

## Resources

- **Jurik, M.** Jurik Research. (Proprietary JMA specification and band logic)
- **Mandelbrot, B.** "The Variation of Certain Speculative Prices." *Journal of Business*, 36(4), 1963. (Fat-tailed distributions motivating adaptive approaches)
