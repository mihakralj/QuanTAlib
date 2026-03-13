# MSTOCH: Ehlers MESA Stochastic

> *MESA Stochastic applies Ehlers' cycle measurement to stochastic normalization, binding the oscillator to the dominant market rhythm.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `stochLength` (default 20), `hpLength` (default 48), `ssLength` (default 10)                      |
| **Outputs**      | Single series (Mstoch)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [mstoch.pine](mstoch.pine)                       |

- The MESA Stochastic applies John Ehlers' Roofing Filter as a preprocessing stage before computing a stochastic oscillator, then smooths the stochas...
- **Similar:** [Stoch](../stoch/Stoch.md), [KDJ](../kdj/Kdj.md) | **Complementary:** MACD | **Trading note:** Modified Stochastic; enhanced stochastic oscillator with additional smoothing or lookback variation.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The MESA Stochastic applies John Ehlers' Roofing Filter as a preprocessing stage before computing a stochastic oscillator, then smooths the stochastic output with a Super Smoother. The Roofing Filter removes both low-frequency trend components (via highpass) and high-frequency noise (via Super Smoother), isolating the dominant cycle. The stochastic calculation on this filtered data produces a clean 0-to-1 oscillator that responds to cycle turning points rather than trend or noise, with substantially reduced whipsaw compared to conventional stochastic indicators.

## Historical Context

John Ehlers introduced the MESA Stochastic in his 2013 book *Cycle Analytics for Traders*, as part of his systematic framework for applying digital signal processing to market data. The "MESA" prefix references Maximum Entropy Spectral Analysis, Ehlers' preferred technique for estimating dominant cycle periods. The key innovation is the Roofing Filter preprocessing: by bandpass-filtering the data before applying the stochastic calculation, the oscillator responds to cycle extremes rather than trend extremes. Conventional stochastic indicators on raw price tend to saturate at 0 or 100 during trends (the "stochastic pop" failure mode), but the Roofing Filter removes the trend component entirely, so the stochastic operates on stationary cycle data. Ehlers demonstrated that this produces fewer false signals in trending markets while maintaining responsiveness at genuine cycle turning points. The Super Smoother stages use 2-pole Butterworth-derived coefficients that provide superior smoothing characteristics compared to simple or exponential moving averages.

## Architecture & Physics

### Three-Stage Pipeline

1. **Stage 1: Roofing Filter** (Highpass + Super Smoother). The highpass is a 2-pole Butterworth filter that removes cycles longer than `hpLength`, eliminating trend. The Super Smoother is a 2-pole lowpass filter that removes cycles shorter than `ssLength`, eliminating noise. Together they form a bandpass that isolates the dominant cycle band.

2. **Stage 2: Stochastic on filtered data.** A standard highest-high / lowest-low stochastic over `stochLength` bars of the roofing-filtered output. Because the input is zero-mean (trend removed), the stochastic operates on cycle oscillations rather than trending prices.

3. **Stage 3: Super Smoother of stochastic.** The same 2-pole smoothing filter applied to the raw stochastic, removing stochastic noise while preserving the timing of overbought/oversold transitions. Output is clamped to $[0, 1]$.

### IIR Filter Coefficients

Both the highpass and Super Smoother stages use coefficients derived from 2-pole Butterworth prototypes:

$$\text{arg} = \frac{\sqrt{2}\pi}{P}, \quad e^{-\text{arg}}, \quad c_2 = 2 e^{-\text{arg}} \cos(\text{arg}), \quad c_3 = -e^{-2\text{arg}}$$

The highpass uses $c_1 = (1 + c_2 - c_3)/4$ with a second-difference input $(x - 2x_{-1} + x_{-2})$.
The Super Smoother uses $c_1 = 1 - c_2 - c_3$ with an averaged input $(x + x_{-1})/2$.

## Mathematical Foundation

**Roofing Filter highpass** (removes trend, cutoff period $P_{hp}$):

$$HP_t = c_1^{hp}(x_t - 2x_{t-1} + x_{t-2}) + c_2^{hp} \cdot HP_{t-1} + c_3^{hp} \cdot HP_{t-2}$$

where $c_1^{hp} = \frac{1 + c_2^{hp} - c_3^{hp}}{4}$

**Super Smoother** (removes noise, cutoff period $P_{ss}$):

$$F_t = c_1^{ss} \cdot \frac{HP_t + HP_{t-1}}{2} + c_2^{ss} \cdot F_{t-1} + c_3^{ss} \cdot F_{t-2}$$

where $c_1^{ss} = 1 - c_2^{ss} - c_3^{ss}$

**Stochastic on filtered data:**

$$S_t = \frac{F_t - \min(F_{t-k}, \ldots, F_t)}{\max(F_{t-k}, \ldots, F_t) - \min(F_{t-k}, \ldots, F_t)}$$

where $k = \text{stochLength} - 1$. If range is zero, $S_t = 0.5$.

**Final smoothing:**

$$MSTOCH_t = c_1^{ss} \cdot \frac{S_t + S_{t-1}}{2} + c_2^{ss} \cdot MSTOCH_{t-1} + c_3^{ss} \cdot MSTOCH_{t-2}$$

$$\text{Output} = \text{clamp}(MSTOCH_t, 0, 1)$$

**Default parameters:** stochLength = 20, hpLength = 48, ssLength = 10.

## Performance Profile

### Operation Count (Streaming Mode)

Modified Stochastic uses RingBuffers for high/low windows with O(1) sum-based smoothing.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer deque update (high window) | 2 | 1 | 2 |
| RingBuffer deque update (low window) | 2 | 1 | 2 |
| SUB (high − low = range) | 1 | 1 | 1 |
| SUB (close − low = position) | 1 | 1 | 1 |
| DIV (raw %K = position/range) | 1 | 15 | 15 |
| FMA × 2 (smoothed %K, %D EMA updates) | 2 | 4 | 8 |
| CMP (range > 0 guard) | 1 | 1 | 1 |
| **Total** | **10** | — | **~30 cycles** |

~30 cycles per bar. Two EMA instances on top of a sliding window min/max.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Sliding high/low | Partial | Lemire deque O(n); SIMD scan for ArgMax/Min |
| Raw %K | Yes | VSUBPD + VDIVPD |
| EMA smoothing × 2 | **No** | Recursive IIR — sequential |

EMA smoothing blocks full vectorization; window extrema and division are SIMD-friendly.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact window extrema; FMA EMA smoothing |
| **Timeliness** | 6/10 | Period + EMA smoothing period determines lag |
| **Smoothness** | 8/10 | Double EMA smoothing produces stable %K/%D lines |
| **Noise Rejection** | 7/10 | EMA smoothing removes stochastic choppiness |

## Resources

- Ehlers, J.F. (2013). *Cycle Analytics for Traders*. Wiley, Chapter 6
- Ehlers, J.F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley
- PineScript reference: [`mstoch.pine`](mstoch.pine)