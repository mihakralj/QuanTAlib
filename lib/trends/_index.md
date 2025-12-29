# Trend Indicators

> "The trend is your friend, until it bends at the end and liquidates your position."

Trend indicators are the bread and butter of technical analysis—and often just as stale. They attempt to smooth out the chaotic noise of market data to reveal the underlying direction. Most fail, introducing so much lag that by the time they signal "buy," the smart money is already shorting.

"Laggy" smoothing is avoided. QuanTAlib applies mathematically rigorous, zero-allocation smoothing that respects the physics of market momentum.

## The Collection

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| ALLIGATOR | Williams Alligator | |
| [ALMA](alma/Alma.md) | Arnaud Legoux MA | Gaussian distribution weights for the perfect balance of smoothness and responsiveness. |
| AMAT | Archer Moving Averages Trends | |
| [BESSEL](bessel/Bessel.md) | Bessel Filter | 2nd-order Bessel low-pass filter with maximally flat group delay. |
| [BILATERAL](bilateral/Bilateral.md) | Bilateral Filter | Non-linear smoothing that preserves edges by weighting both distance and intensity difference. |
| [BLMA](blma/Blma.md) | Blackman Window MA | Applies a Blackman window for superior noise suppression. |
| BPF | Ehlers Bandpass Filter | |
| [BUTTER](butter/Butter.md) | Butterworth Filter | 2nd-order low-pass filter with maximally flat frequency response in the passband. |
| BWMA | Bessel-Weighted MA | |
| CHEBY1 | Chebyshev Type I Filter | |
| CHEBY2 | Chebyshev Type II Filter | |
| [CONV](conv/Conv.md) | Convolution MA | Applies a custom kernel to the data window. For the signal processing purists. |
| [DEMA](dema/Dema.md) | Double Exponential MA | Reduces lag by placing more weight on recent data than a standard EMA. |
| DSMA | Deviation-Scaled MA | |
| [DWMA](dwma/Dwma.md) | Double Weighted MA | Applies WMA smoothing twice. Because sometimes once isn't enough. |
| ELLIPTIC | Elliptic (Cauer) Filter | |
| [EMA](ema/Ema.md) | Exponential MA | The classic. Weighted average giving more importance to recent price data. |
| FRAMA | Ehlers Fractal Adaptive MA | |
| GAUSS | Gaussian Filter | |
| GWMA | Gaussian-Weighted MA | |
| HAMMA | Hamming Window MA | |
| HANMA | Hanning Window MA | |
| HANN | Hann FIR Filter | |
| HEMA | Hull Exponential MA | |
| [HMA](hma/Hma.md) | Hull MA | Alan Hull's attempt to eliminate lag using weighted averages of weighted averages. |
| HP | Hodrick-Prescott Filter | |
| HPF | Ehlers Highpass Filter | |
| [HTIT](htit/Htit.md) | Hilbert Transform Instantaneous Trend | Uses the Hilbert Transform to extract the dominant cycle and compute the trend. |
| HT_TRENDMODE | Ehlers Hilbert Transform Trend Mode | |
| HWMA | Holt Weighted MA | |
| ICHIMOKU | Ichimoku Cloud | |
| [JMA](jma/Jma.md) | Jurik Moving Average | The gold standard of adaptive smoothing. Minimal lag, maximum noise reduction. |
| [KAMA](kama/Kama.md) | Kaufman Adaptive MA | Adapts smoothing based on an Efficiency Ratio. Smart, but moody. |
| KF | Kalman Filter | |
| LOESS | LOESS/LOWESS Smoothing | |
| [LSMA](lsma/Lsma.md) | Least Squares MA | Calculates the linear regression line for every point. Computationally expensive, visually satisfying. |
| LTMA | Linear Trend MA | |
| [MAMA](mama/Mama.md) | MESA Adaptive MA | John Ehlers' masterpiece. Adapts to market cycles using phase measurement. |
| [MGDI](mgdi/Mgdi.md) | McGinley Dynamic | A moving average that adjusts for shifts in market speed to minimize lag and whipsaws. |
| MMA | Modified MA | |
| NOTCH | Notch Filter | |
| [PWMA](pwma/Pwma.md) | Parabolic Weighted MA | Uses parabolic weighting ($i^2$) to aggressively favor recent data. |
| QEMA | Quadruple Exponential MA | |
| REMA | Regularized Exponential MA | |
| RGMA | Recursive Gaussian MA | |
| [RMA](rma/Rma.md) | wildeR MA | The "Wilder" moving average. An EMA with $\alpha = 1/N$. Simple, robust, slow. |
| SGF | Savitzky-Golay Filter | |
| SGMA | Savitzky-Golay MA | |
| SINEMA | Sine-weighted MA | |
| [SMA](sma/Sma.md) | Simple MA | The unweighted mean. The vanilla ice cream of indicators. |
| [SSF](ssf/Ssf.md) | Ehlers Super Smooth Filter | A 2-pole Butterworth filter using complex conjugate poles for maximal flatness in the passband. |
| [SUPER](super/Super.md) | SuperTrend | Trend-following indicator using ATR bands as a trailing stop. |
| [T3](t3/T3.md) | Tillson T3 MA | Tim Tillson's smooth operator. Uses a smoothing factor to reduce lag. |
| [TEMA](tema/Tema.md) | Triple Exponential MA | Three EMAs in a trench coat, trying to cancel out lag. |
| [TRIMA](trima/Trima.md) | Triangular MA | A double-smoothed SMA. Heavily weighted towards the center. Very smooth, very laggy. |
| TTM | TTM Trend | |
| [USF](usf/Usf.md) | Ehlers Ultimate Smoother Filter | A zero-lag smoothing filter that subtracts high-frequency noise using a high-pass filter. |
| VAMA | Volatility Adjusted Moving Average | |
| [VIDYA](vidya/Vidya.md) | Variable Index Dynamic Average | Chande's adaptive average using the CMO for volatility adjustments. |
| WIENER | Wiener Filter | |
| [WMA](wma/Wma.md) | Weighted MA | Linear weighting. More relevant than SMA, less neurotic than EMA. |
| YZVAMA | Yang-Zhang Volatility Adjusted MA | |
| ZLDEMA | Zero-Lag Double Exponential MA | |
| ZLEMA | Zero-Lag Exponential MA | |
| ZLTEMA | Zero-Lag Triple Exponential MA | |