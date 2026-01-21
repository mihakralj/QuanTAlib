# JBANDS: Jurik Adaptive Envelope Bands

> "Volatility is the only free lunch in finance—if you know how to digest it."

JBANDS exposes the internal adaptive envelope tracking from Jurik's Moving Average algorithm as a channel indicator. Unlike fixed-width bands, these envelopes snap instantly to new price extremes but decay smoothly back toward price during consolidations. The result: volatility-responsive channels that widen during breakouts and contract during ranging periods, with JMA's signature smoothness in both the middle band and envelope decay.

## Historical Context

Mark Jurik introduced JMA in the mid-1990s as a proprietary alternative to exponential moving averages. While JMA itself became well-known for its low-lag characteristics, the internal envelope bands received less attention. These bands emerged from Jurik's volatility estimation mechanism—a necessary component for adaptive smoothing that happened to create excellent dynamic support/resistance levels.

The envelope mechanism differs fundamentally from Bollinger Bands or Keltner Channels. Those indicators apply symmetric volatility measures around a central average. JMA's envelopes track actual price extremes and decay asymmetrically—upper bands decay downward while lower bands decay upward, each at rates determined by current volatility conditions. This creates channels that respond to market structure rather than statistical assumptions about price distribution.

Traditional channel indicators assume volatility is symmetric and normally distributed. Price data rarely cooperates. JMA's bands adapt to actual price behavior: when price breaks to new highs, the upper band jumps immediately; when price consolidates, both bands gradually converge toward the smoothed price.

## Architecture & Physics

JBANDS consists of four interconnected subsystems:

### 1. Local Deviation Tracker

The first stage computes local deviation from the current envelope boundaries:

$$
d_{local} = \max(|P_t - U_{t-1}|, |P_t - L_{t-1}|)
$$

where $U$ is the upper band and $L$ is the lower band. This captures how far price has moved from the nearest envelope boundary—essential for determining whether to expand or contract the channel.

### 2. Volatility Estimation (10-Bar SMA + 128-Bar Trimmed Mean)

Local deviations feed a two-stage volatility estimator:

**Stage A: 10-bar SMA of local deviation**

$$
V_{short,t} = \frac{1}{10}\sum_{i=0}^{9} d_{local,t-i}
$$

**Stage B: 128-sample trimmed mean**

The middle 65 samples from the 128-sample volatility history provide the reference volatility:

$$
V_{ref} = \text{trimmed-mean}_{65}(\{V_{short,t-127}, ..., V_{short,t}\})
$$

This trimmed mean rejects outliers while maintaining responsiveness to genuine volatility shifts.

### 3. Dynamic Exponent Calculation

The ratio of current deviation to reference volatility determines the adaptive exponent:

$$
r_t = \frac{d_{local}}{V_{ref}}
$$

$$
d_t = \text{clamp}(r_t^{P_{exp}}, 1, \text{logParam})
$$

where:

- $P_{exp} = \max(\text{logParam} - 2, 0.5)$
- $\text{logParam} = \log_2(\sqrt{(period-1)/2}) + 2$

Higher volatility ratios produce larger exponents, causing faster band adaptation.

### 4. Band Update Logic (Snap and Decay)

The core envelope behavior:

$$
\alpha_{band} = e^{\text{logSqrtDivider} \cdot \sqrt{d_t}}
$$

$$
U_t = \begin{cases}
P_t & \text{if } P_t > U_{t-1} \\
\alpha_{band} \cdot U_{t-1} + (1 - \alpha_{band}) \cdot P_t & \text{otherwise}
\end{cases}
$$

$$
L_t = \begin{cases}
P_t & \text{if } P_t < L_{t-1} \\
\alpha_{band} \cdot L_{t-1} + (1 - \alpha_{band}) \cdot P_t & \text{otherwise}
\end{cases}
$$

Bands snap instantly to new extremes (breakout detection) but decay smoothly toward price during consolidations. The decay rate adapts to current volatility—faster decay during quiet periods, slower during volatile ones.

### 5. Middle Band (JMA IIR Filter)

The middle band uses JMA's 2-pole IIR filter with phase adjustment:

$$
\alpha = e^{\text{logLengthDivider} \cdot d_t}
$$

$$
C_0 = \alpha \cdot C_{0,t-1} + (1-\alpha) \cdot P_t
$$

$$
C_8 = \text{lengthDivider} \cdot C_{8,t-1} + (1-\text{lengthDivider}) \cdot (P_t - C_0)
$$

$$
A_8 = \alpha^2 \cdot A_{8,t-1} + (\text{phaseParam} \cdot C_8 + C_0 - JMA_{t-1}) \cdot (1 - 2\alpha + \alpha^2)
$$

$$
JMA_t = JMA_{t-1} + A_8
$$

The phase parameter maps from [-100, 100] to [0.5, 2.5], controlling overshoot characteristics.

## Mathematical Foundation

### Adaptive Smoothing Factor

The core innovation lies in how smoothing adapts to volatility:

$$
\text{lengthParam} = \frac{period - 1}{2}
$$

$$
\text{logParam} = \max(0, \log_2(\sqrt{\text{lengthParam}}) + 2)
$$

$$
\text{sqrtParam} = \sqrt{\text{lengthParam}} \cdot \text{logParam}
$$

$$
\text{lengthDivider} = \frac{0.9 \cdot \text{lengthParam}}{0.9 \cdot \text{lengthParam} + 2}
$$

$$
\text{sqrtDivider} = \frac{\text{sqrtParam}}{\text{sqrtParam} + 1}
$$

### Phase Mapping

The phase parameter transforms user input to internal coefficient:

$$
\text{phaseParam} = \begin{cases}
0.5 & \text{if phase} < -100 \\
2.5 & \text{if phase} > 100 \\
\text{phase} \cdot 0.01 + 1.5 & \text{otherwise}
\end{cases}
$$

Lower phase values reduce overshoot; higher values increase responsiveness at the cost of potential ringing.

## Performance Profile

### Operation Count (Streaming Mode, Per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 18 | 1 | 18 |
| MUL | 12 | 3 | 36 |
| DIV | 3 | 15 | 45 |
| CMP/ABS | 8 | 1 | 8 |
| SQRT | 2 | 15 | 30 |
| EXP | 2 | 50 | 100 |
| POW | 1 | 60 | 60 |
| LOG (precomputed) | 0 | 0 | 0 |
| **Total** | **46** | — | **~297 cycles** |

**Dominant cost:** Transcendental functions (EXP, POW, SQRT) account for 64% of computational cost. The log-based parameters are precomputed in the constructor.

### Batch Mode (SIMD Limitations)

Due to the recursive IIR filter and stateful band tracking, SIMD vectorization provides limited benefit for JBANDS. The algorithm is inherently sequential—each bar's output depends on the previous bar's state. However, the span-based Calculate API avoids heap allocations during batch processing.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact JMA algorithm reproduction |
| **Timeliness** | 9/10 | Near-zero effective lag in band adaptation |
| **Overshoot** | 8/10 | Phase parameter provides control |
| **Smoothness** | 9/10 | JMA's hallmark characteristic |
| **Adaptivity** | 10/10 | True volatility-responsive behavior |

## Validation

JBANDS is a novel extraction of JMA internals. No external library exposes these bands directly.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | — | No JMA or JBANDS implementation |
| **Skender** | — | No JMA or JBANDS implementation |
| **Tulip** | — | No JMA or JBANDS implementation |
| **Ooples** | — | Has JMA, but no band extraction |
| **JMA Middle Band** | ✅ | Validated against standalone JMA |

Internal validation confirms the middle band exactly matches the standalone JMA indicator for all period/phase combinations.

## Common Pitfalls

1. **Warmup period underestimation.** JBANDS requires approximately $20 + 80 \cdot period^{0.36}$ bars for the volatility estimation buffers to stabilize. For period=14, this means ~52 bars; for period=50, ~87 bars. Using the indicator before warmup produces erratic band behavior.

2. **Phase parameter confusion.** Phase affects the middle band (JMA), not the envelope bands. Negative phase reduces overshoot; positive phase increases responsiveness. The envelope snap-and-decay behavior is controlled by the period parameter and volatility conditions.

3. **Band interpretation.** Unlike Bollinger Bands where touches indicate overbought/oversold, JBANDS touches indicate breakout detection. When price exceeds the upper band, the band snaps to the new level—this signals strength, not reversal.

4. **Memory footprint.** Each JBANDS instance maintains 128 + 10 = 138 double values in ring buffers plus scalar state. Memory per instance: ~1.3 KB. Scale accordingly for multi-instrument deployments.

5. **Computational cost.** At ~297 cycles per bar, JBANDS is 3-4x more expensive than simple channel indicators (Donchian, Keltner). The cost comes from JMA's sophisticated volatility estimation. Budget accordingly for high-frequency applications.

6. **isNew parameter.** Bar correction (isNew=false) triggers full state rollback and recalculation. This is essential for real-time chart updates but adds overhead. For historical backtesting with clean data, always pass isNew=true.

## References

- Jurik, M. (1995). "JMA: Jurik Moving Average." Jurik Research.
- Ehlers, J. (2001). "Rocket Science for Traders." Wiley. (Discussion of adaptive smoothing techniques)
- QuanTAlib JMA implementation: [lib/trends_IIR/jma/Jma.md](../../trends_IIR/jma/Jma.md)
