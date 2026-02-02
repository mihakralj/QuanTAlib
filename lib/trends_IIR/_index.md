# Trends (IIR)

> "Recursion trades memory for computation. Single coefficient replaces entire window. But feedback loop carries risk: instability lurks in coefficient choices that FIR designers never face."

Trend indicators based on Infinite Impulse Response (IIR) filters. Recursive architecture uses previous outputs to compute current values, enabling lower lag with fewer coefficients than equivalent FIR filters.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [DEMA](/lib/trends_IIR/dema/Dema.md) | Double Exponential MA | Reduces lag by applying double exponential smoothing, enhancing responsiveness while maintaining signal quality. |
| [DSMA](/lib/trends_IIR/dsma/Dsma.md) | Deviation-Scaled MA | Adaptive IIR filter that adjusts smoothing factor based on market volatility, increasing responsiveness during high-deviation periods. |
| [EMA](/lib/trends_IIR/ema/Ema.md) | Exponential MA | Applies exponentially decreasing weights to price data, balancing responsiveness and stability. |
| [FRAMA](/lib/trends_IIR/frama/Frama.md) | Fractal Adaptive MA | Adapts smoothing based on fractal dimension analysis, minimizing lag in trends and maximizing smoothing in consolidation. |
| [HEMA](/lib/trends_IIR/hema/Hema.md) | Hull Exponential MA | EMA-domain Hull analog using half-life timing and de-lagged EMA cascade. |
| [HTIT](/lib/trends_IIR/htit/Htit.md) | Hilbert Transform Instantaneous Trend | Utilizes Hilbert Transform to isolate instantaneous trend component, providing zero-lag trendline with hybrid FIR-in-IIR design. |
| [JMA](/lib/trends_IIR/jma/Jma.md) | Jurik MA | Adaptive filter achieving high noise reduction and low phase delay through multi-stage volatility normalization and dynamic parameter optimization. |
| [KAMA](/lib/trends_IIR/kama/Kama.md) | Kaufman Adaptive MA | Automatically adjusts sensitivity based on market volatility using Efficiency Ratio, balancing responsiveness and stability. |
| [MAMA](/lib/trends_IIR/mama/Mama.md) | MESA Adaptive MA | Applies Hilbert Transform for phase-based adaptation, using dual-line system (MAMA/FAMA) for cycle-sensitive smoothing. |
| [MGDI](/lib/trends_IIR/mgdi/Mgdi.md) | McGinley Dynamic Indicator | Adjusts speed based on market volatility using dynamic factor, aiming to hug prices closely. |
| [MMA](/lib/trends_IIR/mma/Mma.md) | Modified MA | Combines simple and weighted components, emphasizing central values for balanced smoothing. |
| [QEMA](/lib/trends_IIR/qema/Qema.md) | Quad Exponential MA | Zero-lag filter with four cascaded EMAs using geometrically ramped alphas and minimum-energy weights for DC lag elimination. |
| [REMA](/lib/trends_IIR/rema/Rema.md) | Regularized Exponential MA | Applies regularization to EMA using lambda parameter, balancing smoothing and momentum-based prediction. |
| [RGMA](/lib/trends_IIR/rgma/Rgma.md) | Recursive Gaussian MA | Approximates Gaussian smoothing by recursively applying EMA filters multiple times (passes), controlled by adjusted period. |
| [RMA](/lib/trends_IIR/rma/Rma.md) | wildeR MA | Wilder's smoothing average using specific alpha (1/period), designed for indicators like RSI and ATR. |
| [T3](/lib/trends_IIR/t3/T3.md) | Tillson T3 MA | Six-stage EMA cascade with optimized coefficients based on volume factor for reduced lag and superior noise reduction. |
| [TEMA](/lib/trends_IIR/tema/Tema.md) | Triple Exponential MA | Triple-cascade EMA architecture with optimized coefficients (3, -3, 1) for further lag reduction compared to DEMA. |
| [VAMA](/lib/trends_IIR/vama/Vama.md) | Volatility Adjusted MA | Dynamically adjusts moving average length based on ATR volatility ratio, shortening during high volatility and lengthening during low volatility. |
| [VIDYA](/lib/trends_IIR/vidya/Vidya.md) | Variable Index Dynamic Average | Adjusts smoothing factor based on market volatility using Volatility Index (ratio of short-term to long-term standard deviation). |
| [YZVAMA](/lib/trends_IIR/yzvama/Yzvama.md) | Yang-Zhang Volatility Adjusted MA | Adjusts MA length based on percentile rank of short-term YZV, providing context-aware volatility adaptation for gap-prone markets. |
| [ZLEMA](/lib/trends_IIR/zlema/Zlema.md) | Zero-Lag Exponential MA | Reduces lag by estimating future price based on current momentum, using dynamically calculated lag period. |
