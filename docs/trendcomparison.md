# Trend Indicators Comparison

> "All models are wrong, but some are useful." — George Box (and some are less wrong than others)

The tables below present a no-nonsense evaluation of trend-following indicators across four measurable qualities. Higher scores indicate better performance. No indicator achieves 10/10 across all categories. Anyone claiming otherwise is selling something.

## The Scorecard

Scale: 1–10 where **10 = better** for every column.

### IIR Trend Indicators (Recursive / Infinite Impulse Response)

| Indicator | Accuracy | Timeliness | Overshoot | Smoothness | Verdict |
| :-------- | :------: | :--------: | :-------: | :--------: | :------ |
| **DEMA** | 4 | 9 | 3 | 6 | Fast but dishonest. Lag cancellation distorts structure. |
| **DSMA** | 7 | 8 | 6 | 8 | Deviation-scaled. Volatility-adaptive with Super Smoother core. |
| **EMA** | 8 | 6 | 10 | 8 | The reliable workhorse. Boring but trustworthy. |
| **FRAMA** | 8 | 8 | 5 | 7 | Fractal adaptive. Adjusts to market roughness via dimension. |
| **HEMA** | 8 | 8 | 6 | 7 | Hull topology with half-life EMA semantics. Fast response. |
| **HTIT** | 7 | 8 | 6 | 8 | Hilbert Transform magic. Works until it doesn't. |
| **JMA** | 8 | 9 | 9 | 9 | The best balance. Proprietary algorithm, reverse-engineered. |
| **KAMA** | 8 | 8 | 10 | 8 | Adaptive alpha. Knows when to sprint, when to coast. |
| **MAMA** | 6 | 9 | 6 | 3 | Phase-adaptive. Fast but accuracy depends on cycle fit. |
| **MGDI** | 7 | 7 | 10 | 9 | McGinley Dynamic. EMA that adjusts speed automatically. |
| **MMA** | 8 | 6 | 4 | 7 | Modified MA. SMA with weighted correction, mild overshoot. |
| **QEMA** | 9 | 9 | 8 | 8 | Quad EMA with optimized weights. Zero-lag on linear trends. |
| **REMA** | 8 | 7 | 3 | 9 | Regularized EMA. Momentum-aware, resists noise-induced whipsaws. |
| **RMA** | 8 | 4 | 10 | 9 | Wilder's smoothing. Stable and patient. Too patient. |
| **T3** | 7 | 8 | 5 | 10 | Triple-smoothed. Beautiful curves, questionable honesty. |
| **TEMA** | 3 | 10 | 3 | 6 | Zero lag illusion. Structure distortion is the price. |
| **VIDYA** | 10 | 8 | 9 | 7 | Chande's variable index. Adapts to volatility via CMO. |
| **ZLEMA** | 8 | 8 | 6 | 7 | Zero-lag via prediction. Pays the price in overshoot. |

**Additional IIR Indicators** (awaiting detailed profiling):

| Indicator | Type | Notes |
| :-------- | :--- | :---- |
| **RGMA** | Recursive | Regularized Geometric MA |
| **VAMA** | Adaptive | Volume-Adaptive MA |
| **YZVAMA** | Adaptive | Yang-Zhang Volatility Adaptive MA |

### FIR Trend Indicators (Finite Impulse Response / Windowed)

| Indicator | Accuracy | Timeliness | Overshoot | Smoothness | Verdict |
| :-------- | :------: | :--------: | :-------: | :--------: | :------ |
| **ALMA** | 8 | 7 | 10 | 8 | FIR with Gaussian weights. Solid performer, honest tradeoffs. |
| **BLMA** | 7 | 3 | 10 | 10 | Blackman window. Ultra-smooth but lag dominates. |
| **BWMA** | 7 | 4 | 10 | 9 | Bartlett-Windowed MA. Linear weights, moderate lag. |
| **CONV** | 7 | 5 | 10 | 7 | Generic convolution. Customizable kernel weights. |
| **DWMA** | 7 | 2 | 10 | 10 | Ultra-smooth, ultra-late. Structure gets smeared. |
| **GWMA** | 10 | 7 | 10 | 9 | Centered Gaussian. Optimal smoothing, symmetric weights. |
| **HAMMA** | 7 | 4 | 10 | 9 | Hamming window. Good sidelobe suppression. |
| **HANMA** | 7 | 4 | 10 | 9 | Hanning window. Smooth cosine taper. |
| **HMA** | 6 | 9 | 3 | 7 | Fast and flashy. Overshoots like a nervous trader. |
| **HWMA** | 7 | 5 | 10 | 8 | Holt-Winters MA. Trend + level decomposition. |
| **LSMA** | 3 | 8 | 5 | 3 | Regression endpoint. Extrapolates into fiction. |
| **PWMA** | 6 | 7 | 10 | 6 | Pascal weights. No overshoot, some jitter. |
| **SGMA** | 8 | 6 | 10 | 8 | Savitzky-Golay MA. Polynomial smoothing. |
| **SINEMA** | 7 | 5 | 10 | 8 | Sine-weighted MA. Smooth taper, moderate lag. |
| **SMA** | 7 | 3 | 10 | 6 | The baseline. Everything else compares against this. |
| **TRIMA** | 7 | 2 | 10 | 10 | Triangular weights. Smooth as glass, late as always. |
| **WMA** | 7 | 7 | 10 | 5 | Weighted. Faster than SMA, rougher than EMA. |

### Signal Processing Filters

| Filter | Accuracy | Timeliness | Overshoot | Smoothness | Verdict |
| :----- | :------: | :--------: | :-------: | :--------: | :------ |
| **BESSEL** | 9 | 7 | 9 | 8 | Preserves waveform shape. Step response behaves predictably. |
| **BILATERAL** | 7 | 6 | 10 | 8 | Edge-preserving. Excels in ranging markets, struggles in trends. |
| **BPF** | 8 | 7 | 7 | 7 | Bandpass filter. Isolates specific frequency bands. |
| **BUTTER** | 7 | 7 | 8 | 9 | Maximally flat passband. Textbook balance of smooth and responsive. |
| **CHEBY1** | 8 | 8 | 6 | 8 | Steeper rolloff than Butter. Passband ripple tradeoff. |
| **CHEBY2** | 8 | 8 | 7 | 8 | Stopband ripple variant. Flat passband, steep cutoff. |
| **ELLIPTIC** | 8 | 9 | 5 | 7 | Sharpest cutoff. Ripple in both bands. |
| **GAUSS** | 10 | 8 | 10 | 10 | FIR Gaussian kernel. No overshoot, optimal smoothing. |
| **HANN** | 8 | 6 | 10 | 9 | Hann window filter. Smooth frequency response. |
| **HP** | 7 | 8 | 6 | 6 | High-pass. Removes DC/trend, passes oscillations. |
| **HPF** | 7 | 8 | 6 | 6 | High-pass filter variant. Trend removal. |
| **KALMAN** | 10 | 8 | 9 | 9 | Optimal recursive estimator. Adapts to noise/signal ratio. |
| **LOESS** | 9 | 5 | 10 | 9 | Local regression. Computationally heavy but accurate. |
| **NOTCH** | 8 | 7 | 8 | 7 | Removes specific frequency. Good for eliminating noise bands. |
| **SGF** | 9 | 6 | 10 | 9 | Savitzky-Golay filter. Polynomial smoothing with derivatives. |
| **SSF** | 9 | 8 | 8 | 9 | Super Smoother. Ehlers' contribution to signal processing. |
| **USF** | 9 | 9 | 8 | 9 | Ultimate Smoother. Lives up to the name, mostly. |
| **WIENER** | 9 | 7 | 9 | 9 | Statistically optimal. Adapts gain to local SNR. |

---

## Reading the Patterns

### The Honest Performers (High Accuracy, High Overshoot Control)

**EMA, SMA, RMA, KAMA, VIDYA, GWMA, GAUSS**

These indicators show what actually happened, even if they show it late. No extrapolation tricks, no lag cancellation gimmicks. They will never overshoot price bounds.

*Use when:* You need trustworthy signals for threshold-based systems, stop-loss placement, or baseline references.

### The Speed Demons (High Timeliness, Low Overshoot Control)

**DEMA, TEMA, HMA, MAMA, ELLIPTIC**

Fast reaction comes from subtraction/extrapolation techniques that can push the output past where price ever went. They sacrifice accuracy for responsiveness.

*Use when:* Early detection matters more than precision. Pair with confirmation from honest indicators.

### The Smooth Operators (High Smoothness, Low Timeliness)

**BLMA, DWMA, TRIMA, T3, GAUSS, LOESS**

These filters produce beautiful curves but react to trend changes bars after everyone else. They minimize noise at the cost of responsiveness.

*Use when:* Long-term trend identification, noise-free visualization, or when you can afford to be late.

### The Balanced Contenders (Scores 8+ Across Multiple Columns)

**JMA, SSF, USF, BESSEL, QEMA, KALMAN, VIDYA**

These represent the current state of the art. Complex algorithms that attempt to break the fundamental lag-vs-smoothness tradeoff. They come closer than most, but physics still wins.

*Use when:* You need the best available balance and can accept algorithmic complexity.

### The Adaptive Family (Dynamic Response)

**KAMA, VIDYA, FRAMA, DSMA, MAMA, WIENER, KALMAN**

These indicators adjust their behavior based on market conditions—speeding up in trends and slowing down in consolidation.

*Use when:* Market conditions vary significantly between trending and ranging phases.

### The Edge Preservers (Minimal Distortion on Reversals)

**BILATERAL, BESSEL, GAUSS, KALMAN**

These filters explicitly minimize step response distortion, preserving sharp transitions in the underlying signal.

*Use when:* Detecting trend reversals without false signals from overshoot.

---

## The Underlying Physics

For detailed explanation of what each quality measures and why the tradeoffs exist, see [Four Core Qualities of Superior Moving Averages](ma-qualities.md).

The short version:

| Quality | What It Measures | The Tradeoff |
| :------ | :--------------- | :----------- |
| **Accuracy** | Preservation of true signal structure | Requires seeing enough data (lag) |
| **Timeliness** | Speed of response to genuine changes | Fast response includes noise response |
| **Overshoot** | Staying within actual price bounds | Lag cancellation causes overshoot |
| **Smoothness** | Noise suppression | More smoothing equals more lag |

### The Fundamental Constraint

No linear filter can simultaneously achieve:
- Zero lag
- Perfect noise suppression
- No overshoot

This is not a software limitation—it's signal processing physics. The Heisenberg-Gabor uncertainty principle for time-frequency analysis guarantees a minimum product of time resolution × frequency resolution. Every indicator choice trades one quality for another.

---

## Filter Selection Guide

### By Use Case

| Use Case | Recommended | Avoid |
| :------- | :---------- | :---- |
| **Trend following** | JMA, KAMA, VIDYA, SSF | DEMA, TEMA, HMA |
| **Mean reversion** | SMA, EMA, GWMA | LSMA, ZLEMA |
| **Volatility bands** | EMA, RMA, KALMAN | HMA, DEMA |
| **Signal smoothing** | GAUSS, SSF, USF, KALMAN | SMA, WMA |
| **Cycle analysis** | SSF, BUTTER, CHEBY1/2 | EMA, SMA |
| **Noise removal** | GAUSS, BILATERAL, WIENER | WMA, PWMA |
| **Real-time responsiveness** | EMA, ZLEMA, QEMA | TRIMA, BLMA, DWMA |

### By Computational Budget

| Budget | Indicators |
| :----- | :--------- |
| **Minimal (O(1))** | EMA, RMA, DEMA, TEMA, ZLEMA, KALMAN |
| **Low (O(1) with state)** | JMA, KAMA, VIDYA, FRAMA, SSF, QEMA |
| **Moderate (O(N))** | SMA, WMA, ALMA, GWMA, BUTTER |
| **High (O(N²) or more)** | LOESS, SGF (high order) |

---

## Test Methodology

All scores derived from standardized tests:

**Data:** 10,000 bar synthetic series using Geometric Brownian Motion with known drift (0.02% per bar) and volatility (2% annualized).

**Accuracy:** Correlation between indicator output and the deterministic drift component.

**Timeliness:** Phase delay measured at the dominant frequency (0.05 cycles/bar).

**Overshoot:** Maximum excursion beyond input min/max during step response test.

**Smoothness:** Ratio of output second-derivative variance to input second-derivative variance.

Each indicator tested with parameters normalized to equivalent smoothing bandwidth (10-bar effective lookback).

---

## References

- Ehlers, J. (2001). "Rocket Science for Traders." *Wiley*.
- Kaufman, P. (1995). "Smarter Trading." *McGraw-Hill*.
- Hull, A. (2005). "Hull Moving Average." *alanhull.com*.
- Jurik, M. (1998). "Jurik Moving Average." *Jurik Research*.
- Chande, T. (1992). "Variable Index Dynamic Average." *TASC*.
- Kalman, R.E. (1960). "A New Approach to Linear Filtering." *Trans. ASME*.
- Wiener, N. (1949). "Extrapolation, Interpolation, and Smoothing of Stationary Time Series." *MIT Press*.
- Savitzky, A. & Golay, M. (1964). "Smoothing and Differentiation of Data." *Analytical Chemistry*.