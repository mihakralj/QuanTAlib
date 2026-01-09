# EACP: Ehlers Autocorrelation Periodogram

## Overview and Purpose

Developed by John F. Ehlers (Technical Analysis of Stocks & Commodities, Sep 2016), the Ehlers Autocorrelation Periodogram (EACP) estimates the dominant market cycle by projecting normalized autocorrelation coefficients onto Fourier basis functions. The indicator blends a roofing filter (high-pass + Super Smoother) with a compact periodogram, yielding low-latency dominant cycle detection suitable for adaptive trading systems. Compared with Hilbert-based methods, the autocorrelation approach resists aliasing and maintains stability in noisy price data.

EACP answers a central question in cycle analysis: “What period currently dominates the market?” It prioritizes spectral power concentration, enabling downstream tools (adaptive moving averages, oscillators) to adjust responsively without the lag present in sliding-window techniques.

## Core Concepts

* **Roofing Filter:** High-pass plus Super Smoother combination removes low-frequency drift while limiting aliasing.
* **Pearson Autocorrelation:** Computes normalized lag correlation to remove amplitude bias.
* **Fourier Projection:** Sums cosine and sine terms of autocorrelation to approximate spectral energy.
* **Gain Normalization:** Automatic gain control prevents stale peaks from dominating power estimates.
* **Warmup Compensation:** Exponential correction guarantees valid output from the very first bar.

## Implementation Notes

**This is not a strict implementation of the TASC September 2016 specification.** It is a more advanced evolution combining the core 2016 concept with techniques Ehlers introduced later. The fundamental Wiener-Khinchin theorem (power spectral density = Fourier transform of autocorrelation) is correctly implemented, but key implementation details differ:

### Differences from Original 2016 TASC Article

1. **Dominant Cycle Calculation:**
   * **2016 TASC:** Uses peak-finding to identify the period with maximum power
   * **This Implementation:** Uses Center of Gravity (COG) weighted average over bins where power ≥ 0.5
   * **Rationale:** COG provides smoother transitions and reduces susceptibility to noise spikes

2. **Roofing Filter:**
   * **2016 TASC:** Simple first-order high-pass filter
   * **This Implementation:** Canonical 2-pole high-pass with √2 factor followed by Super Smoother bandpass
   * **Formula:** `hp := (1-α/2)²·(p-2p[1]+p[2]) + 2(1-α)·hp[1] - (1-α)²·hp[2]`
   * **Rationale:** Evolved filtering provides better attenuation and phase characteristics

3. **Normalized Power Reporting:**
   * **2016 TASC:** Reports peak power across all periods
   * **This Implementation:** Reports power specifically at the dominant period
   * **Rationale:** Provides more meaningful correlation between dominant cycle strength and normalized power

4. **Automatic Gain Control (AGC):**
   * Uses decay factor `K = 10^(-0.15/diff)` where `diff = maxPeriod - minPeriod`
   * Ensures K < 1 for proper exponential decay of historical peaks
   * Prevents stale peaks from dominating current power estimates

### Performance Characteristics

* **Complexity:** O(N²) where N = (maxPeriod - minPeriod)
* **Implementation:** Uses `var` arrays with native PineScript historical operator `[offset]`
* **Warmup:** Exponential compensation (§2 pattern) ensures valid output from bar 1

### Related Implementations

This refined approach aligns with:
* TradingView TASC 2025.02 implementation by blackcat1402
* Modern Ehlers cycle analysis techniques post-2016
* Evolved filtering methods from *Cycle Analytics for Traders*

The code is mathematically sound and production-ready, representing a refined version of the autocorrelation periodogram concept rather than a literal translation of the 2016 article.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Min Period | 8 | Lower bound of candidate cycles | Increase to ignore microstructure noise; decrease for scalping. |
| Max Period | 48 | Upper bound of candidate cycles | Increase for swing analysis; decrease for intraday focus. |
| Autocorrelation Length | 3 | Averaging window for Pearson correlation | Set to 0 to match lag, or enlarge for smoother spectra. |
| Enhance Resolution | true | Cubic emphasis to highlight peaks | Disable when a flatter spectrum is desired for diagnostics. |

**Pro Tip:** Keep `(maxPeriod - minPeriod)` ≤ 64 to control $O(n^2)$ inner loops and maintain responsiveness on lower timeframes.

## Calculation and Mathematical Foundation

**Explanation:**
1. Apply roofing filter to `source` using coefficients $\alpha_1$, $a_1$, $b_1$, $c_1$, $c_2$, $c_3$.
2. For each lag $L$ compute Pearson correlation $r_L$ over window $M$ (default $L$).
3. For each period $p$, project onto Fourier basis:
   $C_p=\sum_{n=2}^{N} r_n \cos\left(\frac{2\pi n}{p}\right)$ and $S_p=\sum_{n=2}^{N} r_n \sin\left(\frac{2\pi n}{p}\right)$.
4. Power $P_p=C_p^2+S_p^2$, smoothed then normalized via adaptive peak tracking.
5. Dominant cycle $D=\frac{\sum p\,\tilde P_p}{\sum \tilde P_p}$ over bins where $\tilde P_p≥0.5$, warmup-compensated.

**Technical formula:**
```
Step 1: hp_t = ((1-α₁)/2)(src_t - src_{t-1}) + α₁ hp_{t-1}
Step 2: filt_t = c₁(hp_t + hp_{t-1})/2 + c₂ filt_{t-1} + c₃ filt_{t-2}
Step 3: r_L = (M Σxy - Σx Σy) / √[(M Σx² - (Σx)²)(M Σy² - (Σy)²)]
Step 4: P_p = (Σ_{n=2}^{N} r_n cos(2πn/p))² + (Σ_{n=2}^{N} r_n sin(2πn/p))²
Step 5: D = Σ_{p∈Ω} p · ĤP_p / Σ_{p∈Ω} ĤP_p with warmup compensation
```

> 🔍 **Technical Note:** Warmup uses $c = 1 / (1 - (1 - \alpha)^{k})$ to scale early-cycle estimates, preventing low values during initial bars.

## Interpretation Details

* **Primary Dominant Cycle:**
  * High $D$ (e.g., > 30) implies slow regime; adaptive MAs should lengthen.
  * Low $D$ (e.g., < 15) signals rapid oscillations; shorten lookback windows.

* **Normalized Power:**
  * Values > 0.8 indicate strong cycle confidence; consider cyclical strategies.
  * Values < 0.3 warn of flat spectra; favor trend or volatility approaches.

* **Regime Shifts:**
  * Rapid drop in $D$ alongside rising power often precedes volatility expansion.
  * Divergence between $D$ and price swings may highlight upcoming breakouts.

## Limitations and Considerations

* **Spectral Leakage:** Limited lag range can smear peaks during abrupt volatility shifts.
* **O(n²) Segment:** Although constrained (≤ 60 loops), wide period spans increase computation.
* **Stationarity Assumption:** Autocorrelation presumes quasi-stationary cycles; regime changes reduce accuracy.
* **Latency in Noise:** Even with roofing, extremely noisy assets may require higher `avgLength`.
* **Downtrend Bias:** Negative trends may clip high-pass output; ensure preprocessing retains signal.

## References

* Ehlers, J. F. (2016). “Past Market Cycles.” *Technical Analysis of Stocks & Commodities*, 34(9), 52-55.
* Thinkorswim Learning Center. “Ehlers Autocorrelation Periodogram.”
* Fab MacCallini. “autocorrPeriodogram.R.” GitHub repository.
* QuantStrat TradeR Blog. “Autocorrelation Periodogram for Adaptive Lookbacks.”
* TradingView Script by blackcat1402. “Ehlers Autocorrelation Periodogram (Updated).”

``` mcp
Validation Sources:
Patterns: §2, §3, §7, §21
Wolfram: "Wiener-Khinchin theorem"
External: "Thinkorswim Ehlers Autocorrelation Periodogram","fabmaccallini autocorrPeriodogram","QuantStrat Autocorrelation Periodogram","TradingView blackcat Autocorrelation Periodogram"
API: ref-tools confirmed input.source/int/bool usage, plot defaults
Planning: phases=design,warmup,validation,docs
