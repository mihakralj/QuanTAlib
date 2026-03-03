# Trends (IIR)

> "Recursion trades memory for computation. Single coefficient replaces entire window. But feedback loop carries risk: instability lurks in coefficient choices that FIR designers never face."

Trend indicators based on Infinite Impulse Response (IIR) filters. Recursive architecture uses previous outputs to compute current values, enabling lower lag with fewer coefficients than equivalent FIR filters.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ADXVMA](adxvma/Adxvma.md) | ADX Variable MA | ADX-based adaptive smoothing. Adjusts speed with trend strength. |
| [AHRENS](ahrens/Ahrens.md) | Ahrens MA | Ahrens recursive moving average. Simple IIR with minimal lag. |
| [CORAL](coral/Coral.md) | Coral Trend Filter | Six-stage cascaded EMA with polynomial combination using Constant D parameter for adaptive smoothing. |
| [DECYCLER](decycler/Decycler.md) | Ehlers Decycler | Ehlers Decycler — complementary HP filter that subtracts high-frequency components from price. |
| [DEMA](dema/Dema.md) | Double Exponential MA | Reduces lag by applying double exponential smoothing, enhancing responsiveness while maintaining signal quality. |
| [DSMA](dsma/Dsma.md) | Deviation-Scaled MA | Adaptive IIR filter that adjusts smoothing factor based on market volatility, increasing responsiveness during high-deviation periods. |
| [EMA](ema/Ema.md) | Exponential MA | Applies exponentially decreasing weights to price data, balancing responsiveness and stability. |
| [FRAMA](frama/Frama.md) | Ehlers Fractal Adaptive Moving Average | Adapts smoothing based on fractal dimension analysis, minimizing lag in trends and maximizing smoothing in consolidation. |
| [GDEMA](gdema/Gdema.md) | Generalized Double Exponential MA | Generalized DEMA with configurable volume factor for tunable lag/smoothness trade-off. |
| [HEMA](hema/Hema.md) | Hull Exponential MA | EMA-domain Hull analog using half-life timing and de-lagged EMA cascade. |
| [HOLT](holt/Holt.md) | Holt Exponential Smoothing | Double exponential smoothing with separate level and trend components for adaptive trend-following. |
| [HTIT](htit/Htit.md) | Ehlers Hilbert Transform Instantaneous Trend (also known as HT_TRENDLINE) | Utilizes Hilbert Transform to isolate instantaneous trend component, providing zero-lag trendline with hybrid FIR-in-IIR design. |
| [HWMA](hwma/Hwma.md) | Holt-Winters MA | Triple exponential smoothing. Tracks level, velocity, acceleration. Recursive IIR structure. |
| [JMA](jma/Jma.md) | Jurik MA | Adaptive filter achieving high noise reduction and low phase delay through multi-stage volatility normalization and dynamic parameter optimization. |
| [KAMA](kama/Kama.md) | Kaufman Adaptive MA | Automatically adjusts sensitivity based on market volatility using Efficiency Ratio, balancing responsiveness and stability. |
| [LEMA](lema/Lema.md) | Leader EMA | Dual EMA architecture: primary EMA(source) plus error-correction EMA(source − EMA), reducing lag while maintaining smoothness. |
| [LTMA](ltma/Ltma.md) | Linear Trend MA | Linear trend extraction via recursive IIR smoothing. |
| [MAMA](mama/Mama.md) | Ehlers MESA Adaptive Moving Average | Applies Hilbert Transform for phase-based adaptation, using dual-line system (MAMA/FAMA) for cycle-sensitive smoothing. |
| [MAVP](mavp/Mavp.md) | Moving Average Variable Period | EMA with dynamically varying period per bar, clamped to configurable min/max range. |
| [MCNMA](mcnma/Mcnma.md) | McNicholl EMA | Six cascaded EMA stages forming inner TEMA + outer TEMA, combined as 2×TEMA(src) − TEMA(TEMA(src)) for superior lag reduction. |
| [MGDI](mgdi/Mgdi.md) | McGinley Dynamic Indicator | Adjusts speed based on market volatility using dynamic factor, aiming to hug prices closely. |
| [MMA](mma/Mma.md) | Modified MA | Combines simple and weighted components, emphasizing central values for balanced smoothing. |
| [NMA](nma/Nma.md) | Natural MA | Adaptive IIR filter whose smoothing ratio derives from volatility-weighted sqrt-kernel analysis of log-price movements (Sloman, Ocean Theory). |
| [QEMA](qema/Qema.md) | Quad Exponential MA | Zero-lag filter with four cascaded EMAs using geometrically ramped alphas and minimum-energy weights for DC lag elimination. |
| [REMA](rema/Rema.md) | Regularized Exponential MA | Applies regularization to EMA using lambda parameter, balancing smoothing and momentum-based prediction. |
| [RGMA](rgma/Rgma.md) | Recursive Gaussian MA | Approximates Gaussian smoothing by recursively applying EMA filters multiple times (passes), controlled by adjusted period. |
| [RMA](rma/Rma.md) | wildeR MA | Wilder's smoothing average using specific alpha (1/period), designed for indicators like RSI and ATR. |
| [T3](t3/T3.md) | Tillson T3 MA | Six-stage EMA cascade with optimized coefficients based on volume factor for reduced lag and superior noise reduction. |
| [TEMA](tema/Tema.md) | Triple Exponential MA | Triple-cascade EMA architecture with optimized coefficients (3, -3, 1) for further lag reduction compared to DEMA. |
| [TRAMA](trama/Trama.md) | Trend Regularity Adaptive MA | Adaptive EMA where smoothing derives from the squared fraction of bars producing new highest-highs or lowest-lows within the lookback window. |
| [VAMA](vama/Vama.md) | Volatility Adjusted MA | Dynamically adjusts moving average length based on ATR volatility ratio, shortening during high volatility and lengthening during low volatility. |
| [VIDYA](vidya/Vidya.md) | Variable Index Dynamic Average | Adjusts smoothing factor based on market volatility using Volatility Index (ratio of short-term to long-term standard deviation). |
| [YZVAMA](yzvama/Yzvama.md) | Yang-Zhang Volatility Adjusted MA | Adjusts MA length based on percentile rank of short-term YZV, providing context-aware volatility adaptation for gap-prone markets. |
| [ZLDEMA](zldema/Zldema.md) | Zero-Lag Double Exponential MA | Combines zero-lag preprocessing with dual EMA cascade (DEMA) for faster response than DEMA with moderate smoothing. |
| [ZLEMA](zlema/Zlema.md) | Zero-Lag Exponential MA | Reduces lag by estimating future price based on current momentum, using dynamically calculated lag period. |
| [ZLTEMA](zltema/Zltema.md) | Zero-Lag Triple Exponential MA | Combines zero-lag preprocessing with triple EMA cascade (TEMA) for maximum smoothness with minimal lag. |
