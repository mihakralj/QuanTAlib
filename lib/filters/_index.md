# Filters

> "All moving averages are low-pass filters. The question is which trade-offs you accept."  John Ehlers

Signal processing filters adapted for financial time series. These are not indicators in the traditional sense: they are building blocks. Low-pass removes noise. High-pass isolates cycles. Band-pass extracts specific frequencies. Each filter type trades off smoothness, lag, and overshoot differently.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [Bessel](lib/filters/bessel/Bessel.md) | Bessel Filter |  | Maximally flat group delay. Best phase response. Minimal overshoot. |
| [Bilateral](lib/filters/bilateral/Bilateral.md) | Bilateral Filter |  | Edge-preserving smoothing. Adapts to local gradients. |
| [BPF](lib/filters/bpf/Bpf.md) | BandPass Filter |  | 2nd-order IIR. Cascade of HP + LP. Extracts specific frequency band. |
| [Butter](lib/filters/butter/Butter.md) | Butterworth Filter |  | Maximally flat frequency response. Classic IIR filter. |
| Cheby1 | Chebyshev Type I | =Ë | Steeper roll-off with passband ripple. Sharper cutoff than Butterworth. |
| Cheby2 | Chebyshev Type II | =Ë | Equiripple stopband, monotonic passband. Better stopband rejection. |
| [Elliptic](lib/filters/elliptic/Elliptic.md) | Elliptic Filter |  | Equiripple both bands. Sharpest transition for given order. |
| [Gauss](lib/filters/gauss/Gauss.md) | Gaussian Filter |  | Bell-curve weighted smoothing. No overshoot. |
| [Hann](lib/filters/hann/Hann.md) | Hann Filter |  | Hann window smoothing. Good spectral leakage control. |
| [Hp](lib/filters/hp/Hp.md) | Hodrick-Prescott |  | Causal trend/cycle decomposition. Regularization parameter » controls smoothness. |
| [Hpf](lib/filters/hpf/Hpf.md) | High Pass Filter |  | Attenuates below cutoff. Isolates fast components. |
| [Kalman](lib/filters/kalman/Kalman.md) | Kalman Filter |  | Recursive state estimation. Optimal under Gaussian assumptions. |
| [Loess](lib/filters/loess/Loess.md) | LOESS Smoothing |  | Local polynomial regression. Robust to outliers. |
| [Notch](lib/filters/notch/Notch.md) | Notch Filter |  | Band-stop. Removes specific frequency (e.g., 60 Hz noise). |
| [SGF](lib/filters/sgf/Sgf.md) | Savitzky-Golay |  | Polynomial smoothing. Preserves higher moments (derivatives). |
| [SSF](lib/filters/ssf/Ssf.md) | Super Smoother |  | Ehlers. 2-pole Butterworth variant. Standard cycle pre-filter. |
| [USF](lib/filters/usf/Usf.md) | Ultra Smoother |  | Ehlers. 3-pole variant. More smoothing than SSF. |
| Wiener | Wiener Filter | =Ë | Optimal linear filter. Minimizes MSE given signal/noise spectra. |

**Status Key:**  Implemented | =Ë Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| General smoothing | Butter, SSF | Good balance of smoothing and lag. |
| Minimal overshoot | Bessel, Gauss | Bessel: best phase. Gauss: no overshoot by design. |
| Sharp cutoff | Elliptic, Cheby1 | Elliptic: sharpest. Cheby1: simpler. |
| Cycle extraction | BPF, Hp | BPF for specific band. Hp for trend/cycle split. |
| Noise spike removal | Notch | Surgical removal of specific frequency. |
| Outlier robustness | Bilateral, Loess | Adapt to local structure. Ignore outliers. |
| Derivative preservation | SGF | Polynomial fit preserves shape. |
| Adaptive estimation | Kalman | Updates estimate as new data arrives. Optimal under model. |

## Filter Characteristics

| Filter | Type | Order | Overshoot | Lag | Sharpness |
| :--- | :--- | :---: | :---: | :---: | :---: |
| Butter | IIR LP | 2 | Low | Medium | Medium |
| Bessel | IIR LP | 2 | Minimal | Higher | Low |
| Cheby1 | IIR LP | 2 | Higher | Lower | High |
| Elliptic | IIR LP | 2 | Higher | Lowest | Highest |
| SSF | IIR LP | 2 | Low | Low | Medium |
| USF | IIR LP | 3 | Lower | Medium | Medium |
| Gauss | FIR LP | N | None | Higher | Low |
| SGF | FIR LP | N | Low | Medium | Low |

Higher order = more smoothing but more lag. IIR filters have minimal coefficients but can overshoot. FIR filters are always stable with linear phase but need more coefficients.

## Filter Design Principles

| Principle | Trade-off | QuanTAlib Approach |
| :--- | :--- | :--- |
| Smoothness vs lag | More smoothing = more lag | Parameterized period/cutoff |
| Sharpness vs ripple | Sharper cutoff = more ripple | Choose filter type for application |
| Stability | IIR can be unstable | All implementations verified stable |
| Causality | Real-time requires causal filters | All filters are causal (no lookahead) |