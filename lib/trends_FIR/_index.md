# Trends (FIR)

Finite Impulse Response (FIR) trend indicators. These use fixed-length windows with explicit coefficients. No feedback loops, no recursion. Output depends only on current and past inputs. Always stable. Linear phase possible. SIMD-friendly batch computation.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ALMA](alma/Alma.md) | Arnaud Legoux MA | Gaussian window with offset parameter. Smooth with configurable lag. |
| [BLMA](blma/Blma.md) | Blackman MA | Blackman window. Excellent side-lobe suppression (-58 dB). |
| [BWMA](bwma/Bwma.md) | Bessel-Weighted MA | Bessel window function. Good frequency resolution. |
| [CONV](conv/Conv.md) | Convolution MA | Generic convolution with custom kernel. Building block for others. |
| [CRMA](crma/Crma.md) | Cubic Regression MA | Cubic polynomial regression endpoint. Higher-order trend fit. |
| [DWMA](dwma/Dwma.md) | Double Weighted MA | WMA of WMA. Smoother than single WMA. Triangular-like response. |
| [FWMA](fwma/Fwma.md) | Fibonacci Weighted MA | Fibonacci-number kernel convolution. Golden ratio weighting. |
| [GWMA](gwma/Gwma.md) | Gaussian Weighted MA | Centered Gaussian bell curve. No overshoot. σ controls width. |
| [HAMMA](hamma/Hamma.md) | Hamming MA | Hamming window. -43 dB side lobes. Good general purpose. |
| [HANMA](hanma/Hanma.md) | Hanning MA | Hanning (raised cosine). Zero at edges. Smooth roll-off. |
| [HEND](hend/Hend.md) | Henderson MA | Henderson window. Optimized for trend extraction. Minimal distortion. |
| [HMA](hma/Hma.md) | Hull MA | Reduced lag via weighted average differencing. Can overshoot. |
| [ILRS](ilrs/Ilrs.md) | Integral of Linear Regression Slope | Cumulative linear regression slope. Smooth trend tracking. |
| [KAISER](kaiser/Kaiser.md) | Kaiser Window MA | Kaiser-Bessel window. Adjustable β parameter for sidelobe control. |
| [LANCZOS](lanczos/Lanczos.md) | Lanczos (Sinc) Window MA | Windowed sinc function. Optimal frequency-domain characteristics. |
| [LSMA](lsma/Lsma.md) | Least Squares MA | Linear regression endpoint. Extrapolates trend. |
| [NLMA](nlma/Nlma.md) | Non-Lag MA | Damped cosine kernel convolution. Near-zero lag FIR. |
| [NYQMA](nyqma/Nyqma.md) | Nyquist MA | Dual LWMA cascade. Nyquist-compliant FIR smoothing. |
| [PARZEN](parzen/Parzen.md) | Parzen (de la Vallée-Poussin) Window MA | Parzen window. Piecewise cubic. Good spectral leakage control. |
| [PMA](pma/Pma.md) | Predictive Moving Average | Ehlers predictive filter combining WMA cascade with linear extrapolation. |
| [PWMA](pwma/Pwma.md) | Pascal Weighted MA | Pascal's triangle coefficients. Binomial distribution weights. |
| [QRMA](qrma/Qrma.md) | Quadratic Regression MA | Quadratic polynomial regression endpoint. Captures curvature. |
| [RAIN](rain/Rain.md) | Rainbow MA | 10× cascaded SMA. Extreme smoothing via FIR convolution. |
| [RWMA](rwma/Rwma.md) | Range Weighted MA | Weights derived from bar range. Volatility-adaptive FIR. |
| [SGMA](sgma/Sgma.md) | Savitzky-Golay MA | Polynomial fit. Preserves higher moments. Shape-preserving. |
| [SINEMA](sinema/Sinema.md) | Sine-Weighted MA | Sine wave weighting. Smooth bell-shaped emphasis. |
| [SMA](sma/Sma.md) | Simple MA | Equal weights. Baseline reference. Lag = (N-1)/2. |
| [SP15](sp15/Sp15.md) | Spencer 15-Point MA | Spencer's 15-point symmetric filter. Classic statistical smoothing. |
| [SWMA](swma/Swma.md) | Symmetric Weighted MA | Symmetric linear weights. Equal emphasis on both tails. |
| [TRIMA](trima/Trima.md) | Triangular MA | Triangular weights. SMA of SMA. Emphasizes middle. |
| [TSF](tsf/Tsf.md) | Time Series Forecast | Linear regression projected one step ahead. Extrapolates trend. |
| [TUKEY_W](tukey_w/Tukey_w.md) | Tukey (Tapered Cosine) Window MA | Tukey window with adjustable taper ratio α. Flat-top cosine edges. |
| [WMA](wma/Wma.md) | Weighted MA | Linear weights. Recent prices weighted more. Lag < SMA. |
