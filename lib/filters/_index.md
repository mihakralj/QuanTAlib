# Filters

> "All moving averages are low-pass filters. The question is which trade-offs you accept."  John Ehlers

Signal processing filters adapted for financial time series. These are not indicators in the traditional sense: they are building blocks. Low-pass removes noise. High-pass isolates cycles. Band-pass extracts specific frequencies. Each filter type trades off smoothness, lag, and overshoot differently.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [AGC](agc/Agc.md) | Automatic Gain Control | Ehlers. Amplitude normalization via exponential peak tracking. |
| [ALAGUERRE](alaguerre/ALaguerre.md) | Adaptive Laguerre Filter | Ehlers. Variable-alpha Laguerre from tracking-error normalization. |
| [BAXTERKING](baxterking/BaxterKing.md) | Baxter-King Band-Pass Filter | Symmetric FIR band-pass. Ideal for business cycle extraction. |
| [CFITZ](cfitz/Cfitz.md) | Christiano-Fitzgerald Filter | Asymmetric full-sample band-pass. Optimal under random-walk assumption. |
| [EDCF](edcf/Edcf.md) | Ehlers Distance Coefficient Filter | Nonlinear FIR. Distance-weighted smoothing adapts to local structure. |
| [BESSEL](bessel/Bessel.md) | Bessel Filter | Maximally flat group delay. Best phase response. Minimal overshoot. |
| [BILATERAL](bilateral/Bilateral.md) | Bilateral Filter | Edge-preserving smoothing. Adapts to local gradients. |
| [BPF](bpf/Bpf.md) | BandPass Filter | 2nd-order IIR. Cascade of HP + LP. Extracts specific frequency band. |
| [BUTTER](butter/Butter.md) | Butterworth Filter | Maximally flat frequency response. Classic IIR filter. |
| [CHEBY1](cheby1/Cheby1.md) | Chebyshev Type I | Steeper roll-off with passband ripple. Sharper cutoff than Butterworth. |
| [CHEBY2](cheby2/Cheby2.md) | Chebyshev Type II | Equiripple stopband, monotonic passband. Better stopband rejection. |
| [ELLIPTIC](elliptic/Elliptic.md) | Elliptic Filter | Equiripple both bands. Sharpest transition for given order. |
| [GAUSS](gauss/Gauss.md) | Gaussian Filter | Bell-curve weighted smoothing. No overshoot. |
| [HANN](hann/Hann.md) | Hann Filter | Hann window smoothing. Good spectral leakage control. |
| [HP](hp/Hp.md) | Hodrick-Prescott | Causal trend/cycle decomposition. Regularization parameter λ controls smoothness. |
| [HPF](hpf/Hpf.md) | High Pass Filter | Attenuates below cutoff. Isolates fast components. |
| [KALMAN](kalman/Kalman.md) | Kalman Filter | Recursive state estimation. Optimal under Gaussian assumptions. |
| [LAGUERRE](laguerre/Laguerre.md) | Laguerre Filter | Ehlers. 4-element all-pass cascade. γ-controlled smoothing. |
| [LMS](lms/Lms.md) | Least Mean Squares | Widrow-Hoff adaptive FIR. NLMS weight update. O(order) per bar. |
| [RLS](rls/Rls.md) | Recursive Least Squares | Inverse correlation matrix. Faster convergence than LMS. O(order²) per bar. |
| [LOESS](loess/Loess.md) | LOESS Smoothing | Local polynomial regression. Robust to outliers. |
| [NOTCH](notch/Notch.md) | Notch Filter | Band-stop. Removes specific frequency (e.g., 60 Hz noise). |
| [ONEEURO](oneeuro/OneEuro.md) | One Euro Filter | Speed-adaptive low-pass. Adaptive cutoff from signal derivative. |
| [ROOFING](roofing/Roofing.md) | Roofing Filter | Ehlers. HP + SS cascade. Bandpass for cycle extraction. |
| [SGF](sgf/Sgf.md) | Savitzky-Golay | Polynomial smoothing. Preserves higher moments (derivatives). |
| [SPBF](spbf/Spbf.md) | Super Passband Filter | Ehlers. Wide-band bandpass via differenced EMAs with RMS envelope. |
| [SSF](ssf/Ssf.md) | Super Smoother | Ehlers. 2-pole Butterworth variant. Standard cycle pre-filter. |
| [USF](usf/Usf.md) | Ultra Smoother | Ehlers. 3-pole variant. More smoothing than SSF. |
| [VOSS](voss/Voss.md) | Voss Predictive Filter | Ehlers. BPF + negative group delay predictor. Anticipatory cycle extraction. |
| [WAVELET](wavelet/Wavelet.md) | Wavelet Denoising Filter | A trous Haar decomposition + MAD soft thresholding. Edge-preserving. |
| [WIENER](wiener/Wiener.md) | Wiener Filter | Optimal linear filter. Minimizes MSE given signal/noise spectra. |
