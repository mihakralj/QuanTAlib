# Trends (IIR)

Trend indicators based on Infinite Impulse Response (IIR) filters.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [DEMA](dema/Dema.md) | Double Exponential MA | Reduces lag by applying double exponential smoothing, enhancing responsiveness while maintaining signal quality. |
| [DSMA](dsma/Dsma.md) | Deviation-Scaled MA | Adaptive IIR filter that adjusts its smoothing factor based on market volatility, increasing responsiveness during high-deviation periods. |
| [EMA](ema/Ema.md) | Exponential MA | Applies exponentially decreasing weights to price data, balancing responsiveness and stability. |
| [FRAMA](frama/Frama.md) | Fractal Adaptive MA | Adapts smoothing based on fractal dimension analysis, minimizing lag in trends and maximizing smoothing in consolidation. |
| [HEMA](hema/Hema.md) | Hull Exponential MA | EMA-domain Hull analog using half-life timing and a de-lagged EMA cascade. |
| [HTIT](htit/Htit.md) | Hilbert Transform Instantaneous Trend | Utilizes Hilbert Transform to isolate the instantaneous trend component, providing a zero-lag trendline with hybrid FIR-in-IIR design. |
| [JMA](jma/Jma.md) | Jurik MA | Adaptive filter achieving high noise reduction and low phase delay through multi-stage volatility normalization and dynamic parameter optimization. |
| [KAMA](kama/Kama.md) | Kaufman Adaptive MA | Automatically adjusts sensitivity based on market volatility using an Efficiency Ratio, balancing responsiveness and stability. |
| [MAMA](mama/Mama.md) | MESA Adaptive MA | Applies Hilbert Transform for phase-based adaptation, using a dual-line system (MAMA/FAMA) for cycle-sensitive smoothing. |
| [MGDI](mgdi/Mgdi.md) | McGinley Dynamic Indicator | Adjusts speed based on market volatility using a dynamic factor, aiming to hug prices closely. |
| [MMA](mma/Mma.md) | Modified MA | Combines simple and weighted components, emphasizing central values for balanced smoothing. |
| [QEMA](qema/Qema.md) | Quad Exponential MA | Zero-lag filter with four cascaded EMAs using geometrically ramped alphas and minimum-energy weights for DC lag elimination. |
| [REMA](rema/Rema.md) | Regularized Exponential MA | Applies regularization to EMA using a lambda parameter, balancing smoothing and momentum-based prediction. |
| [RGMA](rgma/Rgma.md) | Recursive Gaussian MA | Approximates Gaussian smoothing by recursively applying EMA filters multiple times (passes), controlled by an adjusted period. |
| [RMA](rma/Rma.md) | wildeR MA (SMMA, MMA)| Wilder's smoothing average using a specific alpha (1/period), designed for indicators like RSI and ATR. |
| [T3](t3/T3.md) | Tillson T3 MA | Six-stage EMA cascade with optimized coefficients based on a volume factor for reduced lag and superior noise reduction. |
| [TEMA](tema/Tema.md) | Triple Exponential MA | Triple-cascade EMA architecture with optimized coefficients (3, -3, 1) for further lag reduction compared to DEMA. |
| [VAMA](vama/Vama.md) | Volatility Adjusted MA | Dynamically adjusts moving average length based on ATR volatility ratio, shortening during high volatility and lengthening during low volatility. |
| [VIDYA](vidya/Vidya.md) | Variable Index Dynamic Average | Adjusts smoothing factor based on market volatility using a Volatility Index (ratio of short-term to long-term standard deviation). |
| [YZVAMA](yzvama/Yzvama.md) | Yang-Zhang Volatility Adjusted MA | Adjusts MA length based on percentile rank of short-term YZV, providing context-aware volatility adaptation for gap-prone markets. |
| [ZLEMA](zlema/Zlema.md) | Zero-Lag Exponential MA | Reduces lag by estimating future price based on current momentum, using a dynamically calculated lag period. |
