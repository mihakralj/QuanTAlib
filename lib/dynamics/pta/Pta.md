# PTA: Ehlers Precision Trend Analysis

> *Most trend indicators smooth price and inherit lag as a tax. PTA sidesteps the toll entirely — two highpass filters, one subtraction, and the trend arrives before the moving average even notices it moved.*

| Property         | Value                                                  |
| ---------------- | ------------------------------------------------------ |
| **Category**     | Dynamics                                               |
| **Inputs**       | Source (close)                                         |
| **Parameters**   | `longPeriod` (default 250), `shortPeriod` (default 40) |
| **Outputs**      | Single series (Pta)                                    |
| **Output range** | Unbounded (zero-centered)                              |
| **Warmup**       | `longPeriod` bars                                      |
| **PineScript**   | [pta.pine](pta.pine)                                   |

- PTA (Precision Trend Analysis) applies two 2-pole Butterworth highpass filters with different cutoff periods to the same input, then subtracts: Trend = HP(longPeriod) − HP(shortPeriod). This preserves cyclic components between `shortPeriod` and `longPeriod` bars, producing a zero-centered trend indicator with near-zero phase lag.
- **Similar:** [Decycler](../../trends_IIR/decycler/Decycler.md), [DECO](../../oscillators/deco/Deco.md) | **Complementary:** ADX for trend strength confirmation, SuperTrend for directional bias | **Trading note:** Positive = uptrend, negative = downtrend; zero crossings signal reversals. Not a price overlay — plot in separate window.
- No external validation libraries implement PTA. Validated through self-consistency, behavioral testing, and PineScript reference comparison.

PTA (Precision Trend Analysis) is John F. Ehlers' 2024 approach to extracting market trend with near-zero lag. Published in the September 2024 issue of *Technical Analysis of Stocks & Commodities*, the technique inverts the conventional wisdom: instead of smoothing price with a lowpass filter (which always introduces lag proportional to the filter order), PTA uses two highpass filters — which have almost no lag — and subtracts them to create a bandpass that isolates the trend-relevant frequency band. With default parameters of `longPeriod = 250` (~1 trading year) and `shortPeriod = 40` (~2 months), PTA captures intermediate-term market trends while rejecting both high-frequency noise and ultra-long-term drift. The output is zero-centered and unbounded: positive values indicate uptrend, negative values indicate downtrend, and zero crossings mark trend reversals. Each streaming bar requires only 17 floating-point operations — two IIR evaluations plus one subtraction — making PTA one of the cheapest trend indicators available.

## Historical Context

Ehlers' body of work on digital signal processing applied to financial markets spans three decades, with a consistent theme: treat price as a signal and apply engineering-grade filter design rather than ad hoc smoothing. His earlier contributions — the Decycler (2015), Super Smoother (2013), and various Hilbert Transform indicators — all apply specific filter topologies to extract actionable information from price series.

The Decycler indicator, published in TASC in 2015, subtracts a highpass filter output from the original price to obtain a lowpass-filtered trend. PTA inverts this approach: instead of keeping what the highpass *removes*, PTA operates entirely within the highpass domain. By applying two highpass filters with different cutoff frequencies and subtracting, it creates a bandpass filter that preserves only the frequency band between the two cutoffs. This is the same principle as an analog bandpass filter built from differential highpass stages.

The key innovation of PTA over the Decycler is the elimination of the lowpass path entirely. The Decycler's output tracks price closely (it is a lowpass of price), which makes it useful as a trend overlay but problematic for trend *magnitude* assessment. PTA's output is zero-centered and measures trend *energy* in the selected frequency band, making it a proper trend strength and direction indicator rather than a smoothed price estimate.

The default parameters — `longPeriod = 250` and `shortPeriod = 40` — correspond to approximately one trading year and two trading months respectively. This isolates the intermediate-term trend band that most swing and position traders target. Shorter `shortPeriod` values (e.g., 10–20) capture faster trends; larger `longPeriod` values extend the analysis to secular trends.

## Architecture & Physics

### Stage 1: 2-Pole Butterworth Highpass Coefficients

Both highpass filters use the standard Ehlers 2-pole Butterworth formulation. For a given cutoff period $P$:

$$\alpha = e^{-\sqrt{2} \cdot \pi / P}$$

$$c_2 = 2 \alpha \cos\!\left(\frac{\sqrt{2} \cdot \pi}{P}\right)$$

$$c_3 = -\alpha^2$$

$$c_1 = \frac{1 + c_2 - c_3}{4}$$

The coefficients are precomputed once in the constructor. Two independent sets are stored: $\{c_{1L}, c_{2L}, c_{3L}\}$ for `longPeriod` and $\{c_{1S}, c_{2S}, c_{3S}\}$ for `shortPeriod`.

### Stage 2: Highpass Filter Recurrence

Each filter applies the same 2nd-order IIR recurrence per bar:

$$HP_n = c_1 \cdot (x_n - 2x_{n-1} + x_{n-2}) + c_2 \cdot HP_{n-1} + c_3 \cdot HP_{n-2}$$

where $x_n$ is the current source value. The term $(x_n - 2x_{n-1} + x_{n-2})$ is the discrete second difference — it is shared between both filters since they process the same input, saving 2 operations.

HP1 (long-period) removes only the very lowest frequencies (below $1/\text{longPeriod}$), passing everything above. HP2 (short-period) removes a wider band of low frequencies (below $1/\text{shortPeriod}$), passing only higher frequencies.

### Stage 3: Bandpass via Subtraction

$$\text{PTA} = HP_1 - HP_2$$

HP1 passes frequencies above $f_L = 1/\text{longPeriod}$. HP2 passes frequencies above $f_S = 1/\text{shortPeriod}$ (where $f_S > f_L$). The subtraction cancels the high-frequency components that both filters pass, leaving only the band between $f_L$ and $f_S$ — the trend-relevant frequencies.

### State Management

The indicator maintains a `State` record struct containing:
- `Hp1`, `Hp1_1`: Current and previous HP1 values (long-period filter)
- `Hp2`, `Hp2_1`: Current and previous HP2 values (short-period filter)
- `Src1`, `Src2`: Previous two source values (shared across both filters)
- `Count`: Bar counter for warmup tracking

A shadow state (`_p_state`) enables bar correction — when `isNew = false`, the previous state is restored before recomputing.

## Mathematical Foundation

### Frequency Response

The 2-pole Butterworth highpass has a frequency response magnitude:

$$|H(f)|^2 = \frac{1}{1 + \left(\frac{f_c}{f}\right)^{2n}}$$

where $f_c$ is the cutoff frequency and $n = 2$ (two poles). The −3 dB point occurs at $f = f_c = 1/P$.

The PTA bandpass response is:

$$|H_{\text{PTA}}(f)| = |H_1(f)| - |H_2(f)|$$

This creates a passband centered between $f_L$ and $f_S$ with smooth rolloff determined by the Butterworth characteristic — maximally flat in the passband with no ripple.

### Parameter Mapping

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $P_L$ | longPeriod | 250 | $P_L \geq 3$ |
| $P_S$ | shortPeriod | 40 | $P_S \geq 2$, $P_S < P_L$ |

| Configuration | Passband | Use Case |
|---------------|----------|----------|
| 250 / 40 | 40–250 bars | Position trading, daily charts |
| 125 / 20 | 20–125 bars | Swing trading |
| 60 / 10 | 10–60 bars | Active trading, 4H charts |
| 500 / 100 | 100–500 bars | Secular trend analysis |

### Phase Lag Analysis

Highpass filters have near-zero phase lag for frequencies well above the cutoff. Since PTA operates by subtracting two highpass outputs, the lag of the combined bandpass is dominated by the slower (long-period) filter near its cutoff frequency, but remains negligible for the center of the passband. This is in stark contrast to lowpass-based trend indicators (SMAs, EMAs), which accumulate phase lag proportional to the filter order and period.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation          | Count | Notes                           |
| :----------------- | :---- | :------------------------------ |
| Subtraction (2nd diff) | 2  | $x_n - 2x_{n-1} + x_{n-2}$    |
| Multiplication     | 1    | $2 \cdot x_{n-1}$              |
| FMA (HP1)          | 2    | $c_{1L} \cdot d + c_{2L} \cdot HP_1$ and $c_{3L} \cdot HP_{1,prev}$ |
| FMA (HP2)          | 2    | $c_{1S} \cdot d + c_{2S} \cdot HP_2$ and $c_{3S} \cdot HP_{2,prev}$ |
| Subtraction (PTA)  | 1    | $HP_1 - HP_2$                   |
| State updates      | 6    | Hp1, Hp1_1, Hp2, Hp2_1, Src1, Src2 |
| **Total**          | **~14 FLOPs + 6 stores** |                    |

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
|:----------|:-------------:|:------|
| Second difference | Yes | Independent per bar |
| HP IIR recurrence | **No** | Serial dependency: $HP_n$ depends on $HP_{n-1}$ and $HP_{n-2}$ |
| Final subtraction | Yes | Independent per bar |

The IIR dependency chain prevents SIMD vectorization of the core computation. The batch path uses a scalar FMA loop with zero allocation.

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | Exact IIR arithmetic, FMA precision |
| **Timeliness** | 9/10 | Near-zero phase lag; fastest trend indicator in the library |
| **Smoothness** | 8/10 | Butterworth maximally-flat characteristic |
| **Noise Rejection** | 8/10 | Dual-filter bandpass rejects both HF noise and LF drift |
| **Interpretability** | 7/10 | Zero-centered; positive/negative intuitive, but unbounded magnitude requires context |

## Validation

| Library | Status | Notes |
|:--------|:------:|:------|
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **TradingView** | Reference | Community scripts match the TASC article formula |
| **PineScript** | ✓ | [pta.pine](pta.pine) reference validates algorithm |

### Behavioral Test Summary

| Test | Expected |
|------|----------|
| Constant input | PTA = 0 (zero second difference) |
| Monotonic uptrend | PTA > 0 after warmup |
| Monotonic downtrend | PTA < 0 after warmup |
| Mirrored series | PTA negated (symmetry) |
| LongPeriod ≤ ShortPeriod | Constructor throws `ArgumentOutOfRangeException` |
| ShortPeriod < 2 | Constructor throws `ArgumentOutOfRangeException` |
| LongPeriod < 3 | Constructor throws `ArgumentOutOfRangeException` |
| Bar correction | State rollback produces different result on modified input |
| 4-API consistency | Streaming, batch TSeries, batch Span, Calculate all match |
| Warmup period | `IsHot` transitions to `true` at bar `longPeriod` |

## Common Pitfalls

1. **longPeriod must exceed shortPeriod.** The bandpass is defined as HP(longPeriod) − HP(shortPeriod). If longPeriod ≤ shortPeriod, the bandpass inverts and the output becomes meaningless. The constructor throws `ArgumentOutOfRangeException`.

2. **IIR bootstrap phase.** The first 2 bars always output 0.0 because the second-order difference $(x_n - 2x_{n-1} + x_{n-2})$ requires 3 source values. Full convergence occurs at approximately `longPeriod` bars. The `IsHot` flag indicates when the warmup is complete.

3. **Default 250-bar longPeriod requires substantial history.** For intraday or short-term applications, reduce `longPeriod` to match the analysis horizon. Using 250 on 5-minute bars means the long HP filter doesn't stabilize until ~21 trading hours of data.

4. **Not a price overlay.** PTA output is zero-centered and unbounded. It measures trend energy, not price level. Always plot in a separate window. Overlaying on price produces a visually meaningless flat line near zero.

5. **Magnitude is not normalized.** Unlike bounded oscillators (RSI, USI), PTA's amplitude scales with price volatility. A ±5 reading on a $10 stock is very different from ±5 on a $500 stock. Consider normalizing by ATR or price if comparing across instruments.

6. **Zero crossings can whipsaw.** In ranging markets, PTA oscillates around zero and produces frequent false reversal signals. Filter zero crossings with a dead zone (e.g., PTA must exceed ±threshold before triggering) or confirm with a trend strength indicator like ADX or VHF.

## References

1. Ehlers, J. F. "Precision Trend Analysis." *Technical Analysis of Stocks & Commodities*, September 2024.
2. Ehlers, J. F. "The Decycler." *Technical Analysis of Stocks & Commodities*, September 2015. (Predecessor using HP subtraction from price.)
3. Ehlers, J. F. *Cycle Analytics for Traders*. Wiley, 2013. ISBN: 978-1118728512. (Butterworth filter design for financial signals.)
4. PineScript reference: [pta.pine](pta.pine)
