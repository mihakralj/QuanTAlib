# Trends (FIR)

> "FIR filters are always stable. The question is how many coefficients you need."  Digital Signal Processing folklore

Finite Impulse Response (FIR) trend indicators. These use fixed-length windows with explicit coefficients. No feedback loops, no recursion. Output depends only on current and past inputs. Always stable. Linear phase possible. SIMD-friendly batch computation.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ALMA](/lib/trends_FIR/alma/Alma.md) | Arnaud Legoux MA | Gaussian window with offset parameter. Smooth with configurable lag. |
| [BLMA](/lib/trends_FIR/blma/Blma.md) | Blackman MA | Blackman window. Excellent side-lobe suppression (-58 dB). |
| [BWMA](/lib/trends_FIR/bwma/Bwma.md) | Bessel-Weighted MA | Bessel window function. Good frequency resolution. |
| [CONV](/lib/trends_FIR/conv/Conv.md) | Convolution MA | Generic convolution with custom kernel. Building block for others. |
| [DWMA](/lib/trends_FIR/dwma/Dwma.md) | Double Weighted MA | WMA of WMA. Smoother than single WMA. Triangular-like response. |
| [GWMA](/lib/trends_FIR/gwma/Gwma.md) | Gaussian Weighted MA | Centered Gaussian bell curve. No overshoot. σ controls width. |
| [HAMMA](/lib/trends_FIR/hamma/Hamma.md) | Hamming MA | Hamming window. -43 dB side lobes. Good general purpose. |
| [HANMA](/lib/trends_FIR/hanma/Hanma.md) | Hanning MA | Hanning (raised cosine). Zero at edges. Smooth roll-off. |
| [HMA](/lib/trends_FIR/hma/Hma.md) | Hull MA | Reduced lag via weighted average differencing. Can overshoot. |
| [HWMA](/lib/trends_FIR/hwma/Hwma.md) | Holt-Winters MA | Triple exponential smoothing. Tracks level, velocity, acceleration. |
| [LSMA](/lib/trends_FIR/lsma/Lsma.md) | Least Squares MA | Linear regression endpoint. Extrapolates trend. |
| [PWMA](/lib/trends_FIR/pwma/Pwma.md) | Pascal Weighted MA | Pascal's triangle coefficients. Binomial distribution weights. |
| [SGMA](/lib/trends_FIR/sgma/Sgma.md) | Savitzky-Golay MA | Polynomial fit. Preserves higher moments. Shape-preserving. |
| [SINEMA](/lib/trends_FIR/sinema/Sinema.md) | Sine-Weighted MA | Sine wave weighting. Smooth bell-shaped emphasis. |
| [SMA](/lib/trends_FIR/sma/Sma.md) | Simple MA | Equal weights. Baseline reference. Lag = (N-1)/2. |
| [TRIMA](/lib/trends_FIR/trima/Trima.md) | Triangular MA | Triangular weights. SMA of SMA. Emphasizes middle. |
| [WMA](/lib/trends_FIR/wma/Wma.md) | Weighted MA | Linear weights. Recent prices weighted more. Lag < SMA. |
