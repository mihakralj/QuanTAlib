# Filters

> "All moving averages are low-pass filters. The question is which trade-offs you accept."  John Ehlers

Signal processing filters adapted for financial time series. These are not indicators in the traditional sense: they are building blocks. Low-pass removes noise. High-pass isolates cycles. Band-pass extracts specific frequencies. Each filter type trades off smoothness, lag, and overshoot differently.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [BESSEL](lib/filters/bessel/Bessel.md) | Bessel Filter | Maximally flat group delay. Best phase response. Minimal overshoot. |
| [BILATERAL](lib/filters/bilateral/Bilateral.md) | Bilateral Filter | Edge-preserving smoothing. Adapts to local gradients. |
| [BPF](lib/filters/bpf/Bpf.md) | BandPass Filter | 2nd-order IIR. Cascade of HP + LP. Extracts specific frequency band. |
| [BUTTER](lib/filters/butter/Butter.md) | Butterworth Filter | Maximally flat frequency response. Classic IIR filter. |
| [CHEBY1](lib/filters/cheby1/Cheby1.md) | Chebyshev Type I | Steeper roll-off with passband ripple. Sharper cutoff than Butterworth. |
| [CHEBY2](lib/filters/cheby2/Cheby2.md) | Chebyshev Type II | Equiripple stopband, monotonic passband. Better stopband rejection. |
| [ELLIPTIC](lib/filters/elliptic/Elliptic.md) | Elliptic Filter | Equiripple both bands. Sharpest transition for given order. |
| [GAUSS](lib/filters/gauss/Gauss.md) | Gaussian Filter | Bell-curve weighted smoothing. No overshoot. |
| [HANN](lib/filters/hann/Hann.md) | Hann Filter | Hann window smoothing. Good spectral leakage control. |
| [HP](lib/filters/hp/Hp.md) | Hodrick-Prescott | Causal trend/cycle decomposition. Regularization parameter λ controls smoothness. |
| [HPF](lib/filters/hpf/Hpf.md) | High Pass Filter | Attenuates below cutoff. Isolates fast components. |
| [KALMAN](lib/filters/kalman/Kalman.md) | Kalman Filter | Recursive state estimation. Optimal under Gaussian assumptions. |
| [LOESS](lib/filters/loess/Loess.md) | LOESS Smoothing | Local polynomial regression. Robust to outliers. |
| [NOTCH](lib/filters/notch/Notch.md) | Notch Filter | Band-stop. Removes specific frequency (e.g., 60 Hz noise). |
| [SGF](lib/filters/sgf/Sgf.md) | Savitzky-Golay | Polynomial smoothing. Preserves higher moments (derivatives). |
| [SSF](lib/filters/ssf/Ssf.md) | Super Smoother | Ehlers. 2-pole Butterworth variant. Standard cycle pre-filter. |
| [USF](lib/filters/usf/Usf.md) | Ultra Smoother | Ehlers. 3-pole variant. More smoothing than SSF. |
| [WIENER](lib/filters/wiener/Wiener.md) | Wiener Filter | Optimal linear filter. Minimizes MSE given signal/noise spectra. |
