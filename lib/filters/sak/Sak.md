# SAK: Swiss Army Knife

> "Nine filters walk into a bar. The bartender says, 'What'll it be?' They answer in unison: 'Same equation, different coefficients.'"

SAK is John Ehlers' unified second-order IIR filter framework that collapses nine distinct filter types into a single difference equation. Change five coefficients and the same code path produces EMA, SMA, Gaussian, Butterworth, FIR smoother, high-pass, two-pole high-pass, band-pass, or band-stop output. One transfer function. Nine behaviors. Zero code duplication.

## Quick Reference

| Property | Value |
| :--- | :--- |
| **Category** | Filters |
| **Inputs** | `src` (price series) |
| **Parameters** | `filterType` (string, default `"BP"`), `period` (int, default 20), `n` (int, default 10, SMA only), `delta` (float, default 0.1, BP/BS only) |
| **Outputs** | Single `double` per bar |
| **Warmup** | 3 bars (2nd-order IIR), except SMA which needs `n` bars |
| **PineScript**   | [sak.pine](sak.pine)                       |
| **Range** | Overlay (EMA, SMA, Gauss, Butter, Smooth) or oscillator around zero (HP, 2PHP, BP, BS) |

## Key Takeaways

- **One equation, nine filters.** The unified transfer function $H(z) = c_0(b_0 + b_1 z^{-1} + b_2 z^{-2}) / (1 - a_1 z^{-1} - a_2 z^{-2})$ covers all nine modes through coefficient substitution alone.
- **Three alpha families.** EMA/HP/SMA/Smooth share one alpha derivation; Gauss/Butter/2PHP share another; BP/BS use a third with bandwidth parameter $\delta$.
- **SMA takes the back door.** While every other mode flows through the standard IIR path, SMA uses a running-sum recurrence that skips the feedforward section entirely.
- **Stable for $P > 2$.** All modes produce bounded output when the period exceeds two bars. Below that, poles escape the unit circle and the filter diverges.
- **DFT mode deliberately excluded.** Ehlers recommended MESA and Hilbert Transform methods for spectral estimation; the DFT mode from the original framework adds complexity without matching those dedicated tools.

## Historical Context

In May 2004, Richard Lyons and Amy Bell published "The Swiss Army Knife of Digital Networks" in *IEEE Signal Processing Magazine* (pp. 90-100). Their observation: a second-order IIR structure with configurable coefficients could implement low-pass, high-pass, band-pass, and band-stop filters from a single code path. Elegant, but aimed at electrical engineers processing radio signals.

John F. Ehlers read that paper and recognized its relevance to market data. Eight months later, in January 2006, he published "Swiss Army Knife Indicator" in *Technical Analysis of Stocks & Commodities*, translating the Lyons-Bell framework into trading-specific terms. Where Lyons and Bell dealt with sampling rates and Hertz, Ehlers parameterized everything in terms of cycle period $P$ (bars per cycle) and derived alpha coefficients using trigonometric identities that map period to pole/zero placement.

The practical value is immediate. Before SAK, implementing nine filter types meant maintaining nine separate functions with independent alpha computations, state management, and test suites. After SAK, one function with a mode selector replaces all nine. The theoretical value is subtler but equally important: SAK reveals that EMA, Butterworth, Gaussian, high-pass, and band-pass filters are not fundamentally different algorithms. They are the same second-order recursive structure with different pole and zero placements in the z-plane.

Most implementations in the wild reproduce the TASC article verbatim, including the DFT mode. QuanTAlib follows Ehlers' own later recommendation to exclude DFT, since MESA and Hilbert Transform approaches (available as separate indicators) handle spectral estimation with phase-locked precision that DFT cannot match over short windows.

## What It Measures and Why It Matters

SAK does not measure one thing. It measures nine things, depending on the mode. That is the point.

In low-pass modes (EMA, SMA, Gauss, Butter, Smooth), SAK extracts the trend component by attenuating frequencies above the cutoff period. These outputs overlay the price chart. In high-pass modes (HP, 2PHP), SAK isolates the cyclic component by removing the trend. In band-pass mode (BP), it isolates a specific frequency band centered on the period, with bandwidth controlled by $\delta$. In band-stop mode (BS), it does the opposite: removes a specific frequency band and passes everything else.

The practical benefit is not that SAK computes any single filter better than a dedicated implementation. A standalone Butterworth filter will produce identical output. The benefit is that SAK provides a unified interface for switching between filter behaviors at runtime, comparing filter responses on identical data, and understanding the relationships between filter types. When a researcher needs to test whether EMA, Gaussian, or Butterworth smoothing produces better signals for a particular strategy, SAK lets them change a string parameter instead of rewiring their indicator chain.

For adaptive systems, SAK enables dynamic filter selection: use Butterworth during trending markets for its flat passband, switch to band-pass during ranging markets to isolate the dominant cycle, and apply high-pass filtering to detrend before feeding into an oscillator. One indicator instance, multiple behaviors, zero recompilation.

## Mathematical Foundation

### Unified Transfer Function

The z-domain transfer function for all nine modes:

$$
H(z) = \frac{c_0(b_0 + b_1 z^{-1} + b_2 z^{-2})}{1 - a_1 z^{-1} - a_2 z^{-2}}
$$

The corresponding time-domain difference equation:

$$
y_t = c_0(b_0 x_t + b_1 x_{t-1} + b_2 x_{t-2}) + a_1 y_{t-1} + a_2 y_{t-2}
$$

where $x_t$ is the input (price) and $y_t$ is the filtered output.

### Alpha Derivations by Mode Group

**Group 1: EMA, HP, SMA, Smooth**

$$
\theta = \frac{2\pi}{P}
$$

$$
\alpha = \frac{\cos\theta + \sin\theta - 1}{\cos\theta}
$$

**Group 2: Gauss, Butter, 2PHP**

$$
\theta = \frac{2\pi}{P}
$$

$$
\beta = 2.415(1 - \cos\theta)
$$

$$
\alpha = -\beta + \sqrt{\beta^2 + 2\beta}
$$

The constant 2.415 ensures Gaussian roll-off at -3 dB at the cutoff frequency.

**Group 3: BP, BS**

$$
\beta = \cos\left(\frac{2\pi}{P}\right)
$$

$$
\gamma = \frac{1}{\cos(2\pi\delta / P)}
$$

$$
\alpha = \gamma - \sqrt{\gamma^2 - 1}
$$

where $\delta$ controls bandwidth. Larger $\delta$ widens the pass/stop band; smaller $\delta$ narrows it.

### Coefficient Table

| Mode | $c_0$ | $b_0$ | $b_1$ | $b_2$ | $a_1$ | $a_2$ |
| :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| EMA | $1$ | $\alpha$ | $0$ | $0$ | $1-\alpha$ | $0$ |
| SMA | $1/n$ | $1$ | $0$ | $0$ | $1$ | $0$ |
| Gauss | $\alpha^2$ | $1$ | $0$ | $0$ | $2(1-\alpha)$ | $-(1-\alpha)^2$ |
| Butter | $\alpha^2/4$ | $1$ | $2$ | $1$ | $2(1-\alpha)$ | $-(1-\alpha)^2$ |
| Smooth | $\alpha^2/4$ | $1$ | $2$ | $1$ | $0$ | $0$ |
| HP | $1-\alpha/2$ | $1$ | $-1$ | $0$ | $1-\alpha$ | $0$ |
| 2PHP | $(1-\alpha/2)^2$ | $1$ | $-2$ | $1$ | $2(1-\alpha)$ | $-(1-\alpha)^2$ |
| BP | $(1-\alpha)/2$ | $1$ | $0$ | $-1$ | $\beta(1+\alpha)$ | $-\alpha$ |
| BS | $(1+\alpha)/2$ | $1$ | $-2\beta$ | $1$ | $\beta(1+\alpha)$ | $-\alpha$ |

### SMA Special Path

SMA does not use the standard feedforward section. Instead it uses a running-sum recurrence:

$$
y_t = \frac{1}{n} x_t + y_{t-1} - \frac{1}{n} x_{t-n}
$$

This is O(1) per bar regardless of window length $n$, since it adds the newest sample and subtracts the oldest rather than recomputing the full sum.

### Smooth Mode: FIR in IIR Clothing

Smooth mode sets $a_1 = a_2 = 0$, eliminating all feedback. The result is a purely feedforward (FIR) filter:

$$
y_t = \frac{\alpha^2}{4}(x_t + 2x_{t-1} + x_{t-2})
$$

This is a 3-tap triangular window with prescribed gain. No recursion, no stability concerns, no ringing. The trade-off: it provides only modest smoothing compared to genuine IIR modes.

## Architecture and Physics

### 1. Unified IIR Engine

Every mode except SMA flows through the same computation:

```
y[t] = c0 * (b0*x[t] + b1*x[t-1] + b2*x[t-2]) + a1*y[t-1] + a2*y[t-2]
```

The engine stores two previous inputs ($x_{t-1}$, $x_{t-2}$) and two previous outputs ($y_{t-1}$, $y_{t-2}$). Total state: four doubles plus the coefficient set. This is the minimal state for any second-order IIR filter.

### 2. Coefficient Derivation Per Mode Group

The nine modes divide into three groups based on how $\alpha$ is computed:

**Group 1 (EMA/HP/SMA/Smooth):** Uses the EMA alpha formula $\alpha = (\cos\theta + \sin\theta - 1)/\cos\theta$. This places a single real pole at distance $(1-\alpha)$ from the origin. For HP mode, the zero at $z = 1$ blocks the DC component. For Smooth mode, the lack of feedback poles makes it FIR.

**Group 2 (Gauss/Butter/2PHP):** Uses the Gaussian alpha via $\beta = 2.415(1 - \cos\theta)$. This places conjugate complex poles that produce a smoother roll-off than the EMA formula. Butterworth adds feedforward zeros at $z = -1$ to flatten the passband. 2PHP inverts the numerator to create a second-order high-pass response.

**Group 3 (BP/BS):** Uses a bandwidth-dependent alpha with parameter $\delta$. The poles sit on a circle of radius $\alpha$, placed at angle $\beta$ (the center frequency). Band-pass zeros at $z = \pm 1$ create the band-pass shape. Band-stop zeros at angle $\beta$ create the notch.

### 3. SMA Special Path

SMA bypasses the IIR engine entirely. The running-sum recurrence $y_t = (1/n)x_t + y_{t-1} - (1/n)x_{t-n}$ requires a circular buffer of length $n$ to store past inputs. This makes SMA the only mode with O(n) memory rather than O(1).

The reason for the special path: expressing SMA as a pure IIR filter would require $n$ feedback taps (an $n$th-order IIR), which defeats the purpose of a second-order framework. The running-sum trick achieves O(1) computation per bar while keeping SMA within the SAK interface.

### 4. Stability Analysis

For a second-order IIR filter to be stable, all poles of $1 - a_1 z^{-1} - a_2 z^{-2} = 0$ must lie inside the unit circle ($|z| < 1$).

**LP modes (Gauss, Butter):** Poles at $z = (1-\alpha) \pm j\epsilon$. Since $0 < \alpha < 1$ for $P > 2$, the pole modulus $|1-\alpha| < 1$. Stable.

**HP modes (HP, 2PHP):** Same pole placement as LP counterparts. The zeros change (high-pass vs low-pass), but poles remain inside the unit circle. Stable.

**BP/BS modes:** Poles at modulus $\alpha < 1$ for $P > 2$ and valid $\delta$. The condition $\gamma^2 - 1 \geq 0$ requires $\delta/P \leq 0.25$, which is satisfied for all practical bandwidth settings. Stable.

**EMA:** Single pole at $(1-\alpha)$. Since $\alpha \in (0, 1)$ for $P > 2$, the pole is inside the unit circle. Stable.

**SMA:** The running-sum recurrence has a pole at $z = 1$ (marginally stable), but the subtraction of $x_{t-n}$ acts as implicit stabilization. Numerically stable for finite-precision arithmetic.

**Smooth:** No poles (FIR). Always stable.

**Critical boundary:** At $P = 2$, the EMA alpha formula yields $\alpha = 1$ and the filter degenerates. The constraint $P > 2$ must be enforced at the API level.

### 5. Frequency Response Characteristics

| Mode | Passband | Stopband | Roll-off | Phase |
| :--- | :--- | :--- | :--- | :--- |
| EMA | $[0, f_c]$ | $(f_c, f_N]$ | -6 dB/oct | Non-linear |
| SMA | $[0, f_c]$ | $(f_c, f_N]$ | -6 dB/oct (approx) | Linear |
| Gauss | $[0, f_c]$ | $(f_c, f_N]$ | -12 dB/oct | Non-linear |
| Butter | $[0, f_c]$ | $(f_c, f_N]$ | -12 dB/oct | Maximally flat |
| Smooth | $[0, f_c]$ | $(f_c, f_N]$ | -6 dB/oct | Linear (FIR) |
| HP | $(f_c, f_N]$ | $[0, f_c]$ | -6 dB/oct | Non-linear |
| 2PHP | $(f_c, f_N]$ | $[0, f_c]$ | -12 dB/oct | Non-linear |
| BP | $[f_c-\Delta, f_c+\Delta]$ | Outside band | -6 dB/oct per side | Non-linear |
| BS | Outside notch | $[f_c-\Delta, f_c+\Delta]$ | -6 dB/oct per side | Non-linear |

where $f_c = 1/P$ is the cutoff frequency and $f_N$ is the Nyquist frequency.

## Interpretation and Signals

### Overlay Modes (EMA, SMA, Gauss, Butter, Smooth)

These modes output values on the same scale as price. Standard usage:

- **Trend identification:** Price above the filter output suggests uptrend; below suggests downtrend.
- **Support/resistance:** The filter output acts as dynamic support in uptrends, resistance in downtrends.
- **Crossover systems:** Fast SAK(shorter period) crossing slow SAK(longer period) generates signals.
- **Mode comparison:** Run Gauss and Butter on identical data to compare roll-off. Butterworth preserves more passband detail; Gaussian rolls off more gradually.

**Choosing between LP modes:** EMA has more lag than Gauss for the same period but less overshoot. Butterworth provides the flattest passband response (least distortion of low-frequency components). Smooth mode is the cheapest computationally but provides the least attenuation.

### Oscillator Modes (HP, 2PHP, BP, BS)

These modes output values centered around zero.

- **HP/2PHP (detrending):** Removes the trend component, isolating cycles. Useful as a pre-processor before feeding into oscillator indicators. 2PHP provides sharper trend removal (-12 dB/oct vs -6 dB/oct).
- **BP (cycle isolation):** Extracts the component at period $P$ with bandwidth $\delta$. When the dominant market cycle matches $P$, the BP output shows clean sinusoidal swings. Zero-crossings indicate cycle turning points.
- **BS (notch rejection):** Removes a specific frequency while passing everything else. Useful for eliminating known periodic noise (e.g., a daily settlement artifact at a known period).

### Bandwidth Parameter ($\delta$)

For BP and BS modes, $\delta$ controls the width of the pass/stop band:

- $\delta = 0.1$ (default): Narrow band, high selectivity, more ringing
- $\delta = 0.3$: Moderate band, balanced response
- $\delta = 0.5$: Wide band, low selectivity, less ringing

Wider bandwidth trades frequency selectivity for time-domain responsiveness. Narrow bandwidth isolates the target frequency more precisely but introduces more transient ringing when the input changes abruptly.

## Quality Metrics

Quality scores vary by mode. Representative scores for the most commonly used modes:

### Low-Pass Modes

| Metric | EMA | Gauss | Butter | Score Basis |
| :--- | :---: | :---: | :---: | :--- |
| **Lag** | 5/10 | 6/10 | 7/10 | Bars of delay at cutoff |
| **Smoothness** | 6/10 | 8/10 | 9/10 | Stopband attenuation |
| **Overshoot** | 8/10 | 7/10 | 6/10 | Step response ringing |
| **Passband Flatness** | 5/10 | 7/10 | 10/10 | Gain variation in passband |
| **Computational Cost** | 10/10 | 9/10 | 9/10 | Ops per bar (lower = better score) |

### High-Pass and Band-Pass Modes

| Metric | HP | 2PHP | BP | Score Basis |
| :--- | :---: | :---: | :---: | :--- |
| **Trend Rejection** | 6/10 | 9/10 | 8/10 | DC attenuation |
| **Cycle Clarity** | 5/10 | 7/10 | 9/10 | Signal-to-noise at target frequency |
| **Transient Response** | 8/10 | 6/10 | 5/10 | Settling time after step input |
| **Ringing** | 9/10 | 7/10 | 5/10 | Oscillation after impulse |
| **Computational Cost** | 10/10 | 9/10 | 9/10 | Ops per bar |

## Related Indicators

SAK subsumes or closely relates to several standalone indicators in QuanTAlib:

| Indicator | Relationship | Path |
| :--- | :--- | :--- |
| [EMA](../../trends_IIR/ema/Ema.md) | Identical to SAK EMA mode | `lib/trends_IIR/ema/` |
| [SMA](../../trends_FIR/sma/Sma.md) | Identical to SAK SMA mode | `lib/trends_FIR/sma/` |
| [Gauss](../gauss/Gauss.md) | Identical to SAK Gauss mode | `lib/filters/gauss/` |
| [Butter2](../butter2/Butter2.md) | Identical to SAK Butter mode | `lib/filters/butter2/` |
| [Hp](../hp/Hp.md) | Related to SAK HP mode | `lib/filters/hp/` |
| [Hpf](../hpf/Hpf.md) | Related to SAK 2PHP mode | `lib/filters/hpf/` |
| [Bpf](../bpf/Bpf.md) | Related to SAK BP mode | `lib/filters/bpf/` |
| [SSF2](../ssf2/Ssf2.md) | 2-pole super smoother, similar to Butter | `lib/filters/ssf2/` |
| [Notch](../notch/Notch.md) | Related to SAK BS mode | `lib/filters/notch/` |

The standalone implementations may differ slightly in alpha derivation or normalization, but the core IIR structure is identical. SAK's value is the unified interface, not algorithmic novelty.

## Validation

SAK is a multi-mode indicator. Validation must cover each mode independently.

| Mode | Batch | Streaming | Span | Reference |
| :--- | :---: | :---: | :---: | :--- |
| EMA | pending | pending | pending | EMA standalone |
| SMA | pending | pending | pending | SMA standalone |
| Gauss | pending | pending | pending | Gauss standalone |
| Butter | pending | pending | pending | Butter2 standalone |
| Smooth | pending | pending | pending | PineScript reference |
| HP | pending | pending | pending | HP standalone |
| 2PHP | pending | pending | pending | Hpf standalone |
| BP | pending | pending | pending | Bpf standalone |
| BS | pending | pending | pending | PineScript reference |

**Tolerance targets:**

| Reference | Tolerance |
| :--- | :--- |
| QuanTAlib standalone equivalents | $1 \times 10^{-13}$ (bit-exact expected) |
| PineScript reference | $1 \times 10^{-9}$ |

## Performance Profile

### Operation Count (Streaming Mode, Per Bar)

For the standard IIR path (all modes except SMA):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 5 | 3 | 15 |
| ADD/SUB | 4 | 1 | 4 |
| **Total (IIR path)** | **9** | | **~19 cycles** |

For SMA mode (running-sum path):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 2 | 3 | 6 |
| ADD/SUB | 2 | 1 | 2 |
| Memory (ring buffer) | 1 | ~4 | 4 |
| **Total (SMA path)** | **5** | | **~12 cycles** |

Coefficient derivation (once per instance, not per bar):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| COS | 1-2 | 50 | 50-100 |
| SIN | 0-1 | 50 | 0-50 |
| SQRT | 0-1 | 15 | 0-15 |
| MUL/DIV | 3-6 | 3-15 | 9-90 |
| **Total (init)** | | | **~60-255 cycles** |

### SIMD Analysis

The IIR path is inherently recursive: each bar depends on the previous bar's output. Cross-bar SIMD parallelization is not possible.

Within-bar SIMD is also limited because the IIR computation involves only 9 scalar operations. The overhead of loading/storing SIMD registers exceeds any gain from vectorizing 5 multiplications.

**Batch `Calculate(Span)` optimization:** For LP modes that do not use the IIR path's $y_{t-2}$ term (EMA, SMA), the recurrence reduces to first-order, potentially enabling loop unrolling with FMA:

```
y[t] = FMA(y[t-1], decay, alpha * x[t])
```

where `decay = 1 - alpha`. This is a single FMA instruction per bar.

**SIMD-friendly modes:** Smooth mode (FIR, no feedback) can be fully vectorized across 4 bars simultaneously using AVX2 `VFMADD` instructions, yielding ~4x throughput improvement for batch computation.

### Memory Profile

| Component | Size | Notes |
| :--- | :---: | :--- |
| Coefficients ($c_0$, $b_0-b_2$, $a_1$, $a_2$) | 48 bytes | 6 doubles, computed once |
| Input history ($x_{t-1}$, $x_{t-2}$) | 16 bytes | 2 doubles |
| Output history ($y_{t-1}$, $y_{t-2}$) | 16 bytes | 2 doubles |
| State struct overhead | ~8 bytes | Alignment padding |
| **Total (IIR modes)** | **~88 bytes** | |
| Ring buffer (SMA only) | $8n$ bytes | 80 bytes for $n=10$ |
| **Total (SMA mode)** | **~168 bytes** | |

Per-instance memory is minimal. Running 10,000 concurrent SAK instances requires ~860 KB for IIR modes or ~1.6 MB for SMA mode.

## Common Pitfalls

1. **Period must exceed 2.** At $P = 2$, the EMA alpha formula yields $\alpha = 1$ (division by $\cos(\pi) = -1$ produces a sign flip that breaks the derivation). The Gauss/Butter beta formula also degenerates. Enforce $P \geq 3$ in practice, or at minimum validate $P > 2$ at construction. Impact: filter divergence producing `NaN` or `Infinity` output.

2. **SMA mode needs the `n` parameter, not `period`.** The `period` parameter controls the alpha derivation for IIR modes. For SMA, the window length comes from `n`. Setting `period = 50` with `n = 10` produces a 10-bar SMA, not a 50-bar SMA. Confusing these two is the single most common SAK misconfiguration.

3. **BP/BS bandwidth ($\delta$) must satisfy $\delta/P \leq 0.25$.** When $\delta$ is too large relative to $P$, the gamma computation $\gamma = 1/\cos(2\pi\delta/P)$ produces $\gamma < 1$, making $\gamma^2 - 1 < 0$ and the square root undefined. The filter falls back to $\alpha = 0$, producing zero output. Impact: silent failure with no error, just flat-line at zero.

4. **Smooth mode provides minimal smoothing.** Because it has no feedback ($a_1 = a_2 = 0$), Smooth mode is a 3-tap FIR filter with weights $[1, 2, 1]/4$ scaled by $\alpha^2$. Its attenuation at the stopband is roughly -6 dB, compared to -12 dB for Butterworth. Traders expecting strong noise rejection will be disappointed. Use Gauss or Butter for serious smoothing.

5. **HP and 2PHP are pre-processors, not standalone signals.** High-pass output oscillates around zero and contains all market noise above the cutoff frequency. Using raw HP output as a trading signal produces excessive whipsaws. Feed HP output into a secondary smoother or oscillator (band-pass, zero-crossing detector) for actionable signals.

6. **Initial transient corrupts first 2-3 bars.** All IIR modes produce unreliable output until the filter state stabilizes. For two-pole modes (Gauss, Butter, 2PHP, BP, BS), allow at least 3 bars of warmup. For SMA mode, allow $n$ bars. Signals from the transient period have no analytical meaning.

7. **Band-stop mode is not a trend filter.** BS (notch) removes a narrow frequency band and passes everything else, including high-frequency noise. It is not equivalent to a low-pass filter. Traders who want trend extraction should use EMA, Gauss, or Butter modes instead. BS is for removing known periodic interference from a signal that will receive further processing.

## References

- Ehlers, J.F. (2006). "Swiss Army Knife Indicator." *Technical Analysis of Stocks & Commodities*, January 2006.
- Lyons, R. and Bell, A. (2004). "The Swiss Army Knife of Digital Networks." *IEEE Signal Processing Magazine*, May 2004, pp. 90-100.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapters 3-4: IIR and FIR filter design.
- Ehlers, J.F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley. Chapter 2: digital filter fundamentals.
- Ehlers, J.F. (2013). *Cycle Analytics for Traders*. Wiley. Chapter 4: filter comparison and selection criteria.
