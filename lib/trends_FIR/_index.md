# Trends (FIR)

> "FIR filters are always stable. The question is how many coefficients you need."  Digital Signal Processing folklore

Finite Impulse Response (FIR) trend indicators. These use fixed-length windows with explicit coefficients. No feedback loops, no recursion. Output depends only on current and past inputs. Always stable. Linear phase possible. SIMD-friendly batch computation.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ALMA](alma/Alma.md) | Arnaud Legoux MA | Gaussian window with offset parameter. Smooth with configurable lag. |
| [BLMA](blma/Blma.md) | Blackman MA | Blackman window. Excellent side-lobe suppression (-58 dB). |
| [BWMA](bwma/Bwma.md) | Bessel-Weighted MA | Bessel window function. Good frequency resolution. |
| [CONV](conv/Conv.md) | Convolution MA | Generic convolution with custom kernel. Building block for others. |
| [DWMA](dwma/Dwma.md) | Double Weighted MA | WMA of WMA. Smoother than single WMA. Triangular-like response. |
| [GWMA](gwma/Gwma.md) | Gaussian Weighted MA | Centered Gaussian bell curve. No overshoot. σ controls width. |
| [HAMMA](hamma/Hamma.md) | Hamming MA | Hamming window. -43 dB side lobes. Good general purpose. |
| [HANMA](hanma/Hanma.md) | Hanning MA | Hanning (raised cosine). Zero at edges. Smooth roll-off. |
| [HMA](hma/Hma.md) | Hull MA | Reduced lag via weighted average differencing. Can overshoot. |
| [LSMA](lsma/Lsma.md) | Least Squares MA | Linear regression endpoint. Extrapolates trend. |
| [FWMA](fwma/Fwma.md) | Fibonacci Weighted MA | Fibonacci-number kernel convolution. Golden ratio weighting. |
| [NLMA](nlma/Nlma.md) | Non-Lag MA | Damped cosine kernel convolution. Near-zero lag FIR. |
| NYQMA | Nyquist MA | Dual LWMA cascade. Nyquist-compliant FIR smoothing. |
| [PMA](pma/Pma.md) | Predictive Moving Average | Ehlers predictive filter combining WMA cascade with linear extrapolation. |
| [PWMA](pwma/Pwma.md) | Pascal Weighted MA | Pascal's triangle coefficients. Binomial distribution weights. |
| RAIN | Rainbow MA | 10× cascaded SMA. Extreme smoothing via FIR convolution. |
| [SGMA](sgma/Sgma.md) | Savitzky-Golay MA | Polynomial fit. Preserves higher moments. Shape-preserving. |
| [SINEMA](sinema/Sinema.md) | Sine-Weighted MA | Sine wave weighting. Smooth bell-shaped emphasis. |
| [SMA](sma/Sma.md) | Simple MA | Equal weights. Baseline reference. Lag = (N-1)/2. |
| [TRIMA](trima/Trima.md) | Triangular MA | Triangular weights. SMA of SMA. Emphasizes middle. |
| [TSF](tsf/Tsf.md) | Time Series Forecast | Linear regression projected one step ahead. Extrapolates trend. |
| [WMA](wma/Wma.md) | Weighted MA | Linear weights. Recent prices weighted more. Lag < SMA. |
