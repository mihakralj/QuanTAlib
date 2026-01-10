# Trends (IIR)

Trend indicators based on Infinite Impulse Response (IIR) filters.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [DEMA](lib/trends_IIR/dema/Dema.md) | Double Exponential MA | Reduces lag by applying double exponential smoothing, enhancing responsiveness while maintaining signal quality. |
| [DSMA](lib/trends_IIR/dsma/Dsma.md) | Deviation-Scaled MA | Adaptive IIR filter that adjusts its smoothing factor based on market volatility, increasing responsiveness during high-deviation periods. |
| [EMA](lib/trends_IIR/ema/Ema.md) | Exponential MA | Applies exponentially decreasing weights to price data, balancing responsiveness and stability. |
| [FRAMA](lib/trends_IIR/frama/Frama.md) | Fractal Adaptive MA | Adapts smoothing based on fractal dimension analysis, minimizing lag in trends and maximizing smoothing in consolidation. |
| [HEMA](lib/trends_IIR/hema/Hema.md) | Hull Exponential MA | Hybrid of Hull and exponential moving averages using logarithmic coefficient distribution and cubic acceleration for reduced lag and noise suppression. |
| [HTIT](lib/trends_IIR/htit/Htit.md) | Hilbert Transform Instantaneous Trend | Utilizes Hilbert Transform to isolate the instantaneous trend component, providing a zero-lag trendline with hybrid FIR-in-IIR design. |
| [JMA](lib/trends_IIR/jma/Jma.md) | Jurik MA | Adaptive filter achieving high noise reduction and low phase delay through multi-stage volatility normalization and dynamic parameter optimization. |
| [KAMA](lib/trends_IIR/kama/Kama.md) | Kaufman Adaptive MA | Automatically adjusts sensitivity based on market volatility using an Efficiency Ratio, balancing responsiveness and stability. |
| [LTMA](lib/trends_IIR/ltma/Ltma.md) | Linear Trend MA | Projects the linear trend of price data using linear regression, focusing on the endpoint of the trendline. |
| [MAMA](lib/trends_IIR/mama/Mama.md) | MESA Adaptive MA | Applies Hilbert Transform for phase-based adaptation, using a dual-line system (MAMA/FAMA) for cycle-sensitive smoothing. |
| [MGDI](lib/trends_IIR/mgdi/Mgdi.md) | McGinley Dynamic Indicator | Adjusts speed based on market volatility using a dynamic factor, aiming to hug prices closely. |
| [MMA](lib/trends_IIR/mma/Mma.md) | Modified MA | Combines simple and weighted components, emphasizing central values for balanced smoothing. |
| [QEMA](lib/trends_IIR/qema/Qema.md) | Quadruple Exponential MA | Four-stage cascade architecture for superior lag reduction and noise suppression through progressive smoothing optimization. |
| [REMA](lib/trends_IIR/rema/Rema.md) | Regularized Exponential MA | Applies regularization to EMA using a lambda parameter, balancing smoothing and momentum-based prediction. |
| [RGMA](lib/trends_IIR/rgma/Rgma.md) | Recursive Gaussian MA | Approximates Gaussian smoothing by recursively applying EMA filters multiple times (passes), controlled by an adjusted period. |
| [RMA](lib/trends_IIR/rma/Rma.md) | wildeR MA (SMMA, MMA)| Wilder's smoothing average using a specific alpha (1/period), designed for indicators like RSI and ATR. |
| [T3](lib/trends_IIR/t3/T3.md) | Tillson T3 MA | Six-stage EMA cascade with optimized coefficients based on a volume factor for reduced lag and superior noise reduction. |
| [TEMA](lib/trends_IIR/tema/Tema.md) | Triple Exponential MA | Triple-cascade EMA architecture with optimized coefficients (3, -3, 1) for further lag reduction compared to DEMA. |
| [VAMA](lib/trends_IIR/vama/Vama.md) | Volatility Adjusted MA | Dynamically adjusts moving average length based on ATR volatility ratio, shortening during high volatility and lengthening during low volatility. |
| [VIDYA](lib/trends_IIR/vidya/Vidya.md) | Variable Index Dynamic Average | Adjusts smoothing factor based on market volatility using a Volatility Index (ratio of short-term to long-term standard deviation). |
| [YZVAMA](lib/trends_IIR/yzvama/Yzvama.md) | Yang-Zhang Volatility Adjusted MA | Adjusts MA length based on percentile rank of short-term YZV, providing context-aware volatility adaptation for gap-prone markets. |
| [ZLDEMA](lib/trends_IIR/zldema/Zldema.md) | Zero-Lag Double Exponential MA | Hybrid dual-stage predictive architecture combining two ZLEMAs with optimized 1.5/0.5 coefficients for reduced lag and noise suppression. |
| [ZLEMA](lib/trends_IIR/zlema/Zlema.md) | Zero-Lag Exponential MA | Reduces lag by estimating future price based on current momentum, using a dynamically calculated lag period. |
| [ZLTEMA](lib/trends_IIR/zltema/Zltema.md) | Zero-Lag Triple Exponential MA | Advanced triple-cascade predictive architecture combining three ZLEMAs with optimized 2/2/1 coefficients for maximum lag reduction. |
