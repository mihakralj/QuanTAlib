# CWT: Continuous Wavelet Transform

CWT computes the magnitude of the Continuous Wavelet Transform at a specified scale using the Morlet wavelet, providing a time-frequency decomposition that measures the energy content of a specific frequency band at each point in time. Unlike Fourier analysis which loses time localization, the wavelet transform maintains both time and frequency information simultaneously. The output is a non-negative magnitude series where peaks indicate strong presence of the target frequency (determined by the scale parameter) and troughs indicate absence of that frequency component.

## Historical Context

The wavelet transform emerged from seismology and signal processing in the 1980s, with foundational work by Jean Morlet (a geophysicist analyzing seismic reflections) and Alex Grossmann. The Morlet wavelet — a complex sinusoid modulated by a Gaussian envelope — became the standard analyzing wavelet due to its optimal time-frequency resolution (it achieves the Heisenberg uncertainty lower bound). In financial applications, CWT provides multi-resolution analysis: by varying the scale parameter, traders can identify dominant cycles at different timeframes without the windowing artifacts of short-time Fourier transforms. The scale parameter directly controls which frequency band is analyzed: larger scales capture lower frequencies (longer cycles), smaller scales capture higher frequencies (shorter cycles). The relationship between scale $s$ and approximate cycle period is $P \approx \frac{2\pi s}{\omega_0}$ where $\omega_0$ is the central frequency (default 6.0).

## Architecture & Physics

### Morlet Wavelet Convolution

The CWT at scale $s$ is computed as the inner product of the signal with a scaled, translated Morlet wavelet:

$$W(t, s) = \frac{1}{\sqrt{s}} \sum_{k=-K}^{K} x(t-k) \cdot \psi^*\!\left(\frac{k}{s}\right)$$

The Morlet wavelet $\psi(t) = e^{-t^2/2} e^{i\omega_0 t}$ decomposes into real (cosine) and imaginary (sine) parts, both modulated by a Gaussian envelope.

### Implementation Details

- **Half-window:** $K = \text{round}(3s)$, ensuring the Gaussian envelope decays to $<0.01$ at the edges ($e^{-4.5} \approx 0.011$).
- **Real and imaginary sums:** Computed separately, then combined as $|W| = \sqrt{\text{Re}^2 + \text{Im}^2}$.
- **Normalization:** The $1/\sqrt{s}$ factor ensures energy preservation across scales.

### Complexity

$O(K)$ per bar where $K = 6s + 1$. For scale = 10, this is 61 multiply-adds per bar.

## Mathematical Foundation

**Morlet wavelet:**

$$\psi(t) = e^{-t^2/2} \cdot e^{i\omega_0 t}$$

**CWT at scale $s$ and time $t$:**

$$W(t, s) = \frac{1}{\sqrt{s}} \sum_{k=-K}^{K} x_{t+k} \cdot e^{-k^2/(2s^2)} \cdot e^{-i\omega_0 k/s}$$

**Magnitude (power at scale $s$):**

$$|W(t,s)| = \sqrt{\left(\sum_k x_k \cdot g_k \cos\theta_k\right)^2 + \left(\sum_k x_k \cdot g_k \sin\theta_k\right)^2} \cdot \frac{1}{\sqrt{s}}$$

where $g_k = e^{-k^2/(2s^2)}$ and $\theta_k = \omega_0 k / s$

**Scale-to-period relationship:**

$$P \approx \frac{2\pi s}{\omega_0}$$

**Default parameters:** scale = 10.0, omega = 6.0 (corresponding to period $\approx 10.5$ bars).

## Resources

- Morlet, J. et al. (1982). "Wave propagation and sampling theory." *Geophysics*, 47(2): 203-236
- Torrence, C. & Compo, G.P. (1998). "A Practical Guide to Wavelet Analysis." *Bulletin of the American Meteorological Society*
- PineScript reference: [`cwt.pine`](cwt.pine)
