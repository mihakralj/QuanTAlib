# Trends (FIR)

Trend indicators based on Finite Impulse Response (FIR) filters.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ALMA](lib/trends_FIR/alma/Alma.md) | Arnaud Legoux Moving Average | Smoothness and responsiveness using Gaussian window. |
| [BLMA](lib/trends_FIR/blma/Blma.md) | Blackman Moving Average | MA using Blackman window. |
| [BWMA](lib/trends_FIR/bwma/Bwma.md) | Bessel-Weighted Moving Average | MA using Bessel window function. |
| [Conv](lib/trends_FIR/conv/Conv.md) | Convolution Moving Average | MA based on signal convolution. |
| [DWMA](lib/trends_FIR/dwma/Dwma.md) | Double Weighted Moving Average | Recursive WMA application. |
| [GWMA](lib/trends_FIR/gwma/Gwma.md) | Gaussian Weighted Moving Average | MA using centered Gaussian bell curve weighting. |
| [HAMMA](lib/trends_FIR/hamma/Hamma.md) | Hamming Moving Average | MA using Hamming window with -43dB side lobe suppression. |
| [HANMA](lib/trends_FIR/hanma/Hanma.md) | Hanning Moving Average | MA using Hanning window with zero-edge weights. |
| [HMA](lib/trends_FIR/hma/Hma.md) | Hull Moving Average | Reduced lag MA using weighted averages. |
| [HWMA](lib/trends_FIR/hwma/Hwma.md) | Holt-Winters Moving Average | Triple exponential smoothing with level, velocity, and acceleration. |
| [LSMA](lib/trends_FIR/lsma/Lsma.md) | Least Squares Moving Average | MA based on linear regression. |
| [PWMA](lib/trends_FIR/pwma/Pwma.md) | Pascal Weighted Moving Average | MA using Pascal's triangle coefficients. |
| [SGMA](lib/trends_FIR/sgma/Sgma.md) | Savitzky-Golay Moving Average | MA using polynomial fitting for shape-preserving smoothing. |
| [SINEMA](lib/trends_FIR/sinema/Sinema.md) | Sine-Weighted Moving Average | MA using sine-wave weighting for smooth bell-shaped emphasis. |
| [SMA](lib/trends_FIR/sma/Sma.md) | Simple Moving Average | The unweighted mean of the previous m data points. |
| [TRIMA](lib/trends_FIR/trima/Trima.md) | Triangular Moving Average | Weighted average emphasizing the middle of the window. |
| [WMA](lib/trends_FIR/wma/Wma.md) | Weighted Moving Average | Weighted average of the last n prices. |