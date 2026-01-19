# HOMOD: Homodyne Discriminator Dominant Cycle

## Overview and Purpose

The Homodyne Discriminator (HOMOD) is a cycle measurement technique introduced by John F. Ehlers in *Rocket Science for Traders* (2001) and expanded in the November 2000 *Traders’ Tips* column. It applies a Hilbert Transform framework to detect the instantaneous dominant cycle present in price data while minimizing lag.

Unlike fixed-length filters, HOMOD continuously adapts to current market rhythm by converting the in-phase and quadrature components into a complex phasor pair, multiplying them homodynally, and extracting period information from the resulting phase angle. This makes it ideal for adaptive indicators and systems requiring dynamic lookback lengths.

## Core Concepts

* **Homodyne Multiplication:** Complex multiply of current and prior phasors to isolate instantaneous frequency
* **Hilbert FIR Kernel:** Ehlers 0.0962/0.5769 coefficients producing 90° phase shift with minimal distortion
* **Quadrature Rotation:** Phase-advanced components (jI, jQ) enabling orthogonal phasor construction
* **Cycle Clamping:** Limiting detected periods to realistic bounds (default 6–50 bars)
* **Warmup Compensation:** Exponential correction ensuring stable output from bar one

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | hlc3 | Input series analyzed for cycle period | Switch to close for end-of-day signals or to custom synthetic blends |
| Min Period | 6 | Lower bound for detected cycle length | Increase to ignore ultrashort noise-dominated cycles |
| Max Period | 50 | Upper bound for detected cycle length | Raise for weekly/monthly studies; lower for intraday scalping |

**Pro Tip:** Align downstream indicators (e.g., RSI, moving averages) to the live HOMOD period by rounding to the nearest integer—this maintains resonance with the market’s dominant rhythm.

## Calculation and Mathematical Foundation

**Explanation:**
HOMOD smooths price, applies a Hilbert Transform to obtain in-phase (I) and quadrature (Q) components, rotates them by 90°, forms phasors, multiplies each phasor by its predecessor, and derives period length from the resulting phase angle. Subsequent smoothing and clamping stabilize measurements.

**Technical formula:**

1. **Weighted smoothing and detrending**
   $$
   SmoothPrice_t = \frac{4P_t + 3P_{t-1} + 2P_{t-2} + P_{t-3}}{10}
   $$
   $$
   Detrender_t = \left(0.0962\,SP_t + 0.5769\,SP_{t-2} - 0.5769\,SP_{t-4} - 0.0962\,SP_{t-6}\right)\cdot B_t
   $$
   where $B_t = 0.075\cdot Period_{t-1} + 0.54$.

2. **Quadrature pair and phase advance**
   $$
   Q1_t = (0.0962\,Det_t + 0.5769\,Det_{t-2} - 0.5769\,Det_{t-4} - 0.0962\,Det_{t-6})\cdot B_t
   $$
   $$
   I1_t = Det_{t-3}
   $$
   $$
   jI_t = (0.0962\,I1_t + 0.5769\,I1_{t-2} - 0.5769\,I1_{t-4} - 0.0962\,I1_{t-6})\cdot B_t
   $$
   $$
   jQ_t = (0.0962\,Q1_t + 0.5769\,Q1_{t-2} - 0.5769\,Q1_{t-4} - 0.0962\,Q1_{t-6})\cdot B_t
   $$

3. **Phasor construction**
   $$
   I2_t = 0.2\,(I1_t - jQ_t) + 0.8\,I2_{t-1},\quad Q2_t = 0.2\,(Q1_t + jI_t) + 0.8\,Q2_{t-1}
   $$

4. **Homodyne product and smoothing**
   $$
   Re_t = 0.2\,(I2_t I2_{t-1} + Q2_t Q2_{t-1}) + 0.8\,Re_{t-1}
   $$
   $$
   Im_t = 0.2\,(I2_t Q2_{t-1} - Q2_t I2_{t-1}) + 0.8\,Im_{t-1}
   $$

5. **Period extraction, clamp, warmup**
   $$
   \theta_t = \operatorname{atan2}(Im_t, Re_t)
   $$
   $$
   Period^\*_{t} = \frac{2\pi}{\theta_t}
   $$
   $$
   Period_t = \operatorname{clip}(|Period^\*_t|,\ Min,\ Max)
   $$
   $$
   SmoothPeriod_t = SmoothPeriod_{t-1} + 0.33\,(Period_t - SmoothPeriod_{t-1})
   $$

## Interpretation Details

* **Cycle Tracking**
  * 6–12 bars: fast oscillatory regimes suited to scalping and short-term countertrend trades
  * 12–30 bars: medium cycles aligning with swing-trading horizons
  * 30–60 bars: slow cycles highlighting macro rhythm or trend exhaustion zones

* **Adaptive Parameterization**
  * Use rounded SmoothPeriod as the lookback for RSI, stochastic, ATR channels, etc.
  * Match moving-average lengths to maintain coherence between filters and underlying price rhythm.

* **Regime Analysis**
  * Stable plateau in period → consistent cycle regime
  * Rising period → trend elongation or consolidation broadening
  * Falling period → volatility expansion, choppy markets, or nascent rotational phases

## Limitations and Considerations

* **Warmup Demand:** Requires ~60 bars for fully stable phasor history; early readings should be treated cautiously
* **Trend Dominance:** Persistent directional moves degrade cycle definition, causing erratic period swings
* **Noise Sensitivity:** Despite smoothing, extremely noisy instruments may oscillate near Min Period consistently
* **Clamp Bias:** Hard limits prevent detection of cycles outside bounds; adjust for instruments with known longer rhythms
* **Computational Intensity:** Multiple FIR taps and state variables raise per-bar workload versus simpler averages

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | ~25 | 1 | 25 |
| MUL | ~30 | 3 | 90 |
| DIV | 2 | 15 | 30 |
| ATAN2 | 1 | 80 | 80 |
| **Total** | **~58** | — | **~225 cycles** |

**Breakdown:**
- Weighted smooth (4-point): 3 MUL + 3 ADD + 1 DIV = 17 cycles
- Detrender FIR (4 taps): 5 MUL + 3 ADD = 18 cycles
- Q1 FIR (4 taps): 5 MUL + 3 ADD = 18 cycles
- jI/jQ FIRs (8 taps total): 10 MUL + 6 ADD = 36 cycles
- I2/Q2 IIR phasor smoothing: 4 MUL + 4 ADD = 16 cycles
- Homodyne Re/Im: 6 MUL + 4 ADD = 22 cycles
- Period extraction (atan2 + div): 1 ATAN2 + 1 DIV = 95 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Fixed FIR taps (6-deep) + IIR states |
| Batch | O(n) | Linear scan, constant work per bar |

**Memory**: ~128 bytes (6-bar FIR history × 4 series + IIR states)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | Limited | FIR taps vectorizable, IIRs sequential |
| FMA | ✅ | Hilbert kernel: `0.0962×x + 0.5769×x[2] - ...` |
| Batch parallelism | ❌ | IIR feedback prevents cross-bar parallelism |

**Optimization Notes:** The atan2 call dominates (~35% of cost). Consider:
- Fast atan2 approximation if <1° accuracy acceptable
- Precompute 2π constant, use reciprocal for division

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Hilbert Transform is mathematically rigorous |
| **Timeliness** | 7/10 | FIR kernel introduces ~3 bar delay |
| **Overshoot** | 8/10 | Smoothed period output is stable |
| **Smoothness** | 8/10 | IIR smoothing reduces jitter |

## References

* Ehlers, J. F. (2001). *Rocket Science for Traders: Digital Signal Processing Applications*. Wiley.
* Ehlers, J. F. (2000). *Traders’ Tips – Homodyne Discriminator*. *Technical Analysis of Stocks & Commodities*.
* blackcat1402. (2023). *Ehlers Homodyne Discriminator Period Measurer* (TradingView script).
* MrTools. (2025). *Homodyne Discriminator.mq4*. Forex-Station Forums.
* Mladen. (2019). *Adaptive Lookback Indicators – Homodyne Update*. MQL5 Forums.
* 3Jane. (2024). *tindicators hd.cc Implementation*. GitHub.

## Validation Sources

```mcp
Validation Sources:
Patterns: §2, §6, §7, §16, §17, §18, §19
Wolfram: "atan2(y,x)"
External: "TradingView Homodyne Discriminator","Forex-Station Homodyne Discriminator","MQL5 Adaptive Lookback Homodyne","tindicators hd.cc"
Planning: phases=function,main_loop,docs,index