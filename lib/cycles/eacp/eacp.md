# EACP: Ehlers Autocorrelation Periodogram

> "The autocorrelation periodogram uses the Wiener-Khinchin theorem to transform autocorrelation into spectral density, revealing the dominant cycle hidden within price noise."

The Ehlers Autocorrelation Periodogram (EACP) is a sophisticated cycle detection algorithm that estimates the dominant market cycle period by computing autocorrelation coefficients and transforming them to the frequency domain via discrete Fourier transform (DFT). Unlike simple period detectors, EACP leverages the mathematical relationship between autocorrelation and power spectral density to identify cyclical behavior even in noisy price data.

## Historical Context

John Ehlers introduced the Autocorrelation Periodogram in his work on digital signal processing applied to trading. The algorithm addresses a fundamental challenge: market cycles are not stationary, and their periods change over time. Traditional Fourier analysis assumes stationarity, making it poorly suited for adaptive cycle detection.

Ehlers' insight was to use the Wiener-Khinchin theorem, which states that the autocorrelation function and power spectral density are Fourier transform pairs. By computing autocorrelation coefficients at various lags and transforming them via DFT, the algorithm produces a power spectrum that reveals dominant frequencies (cycle periods) in the data.

The implementation here follows Ehlers' PineScript version, which includes:
- High-pass filtering to remove DC offset and low-frequency trends
- Super-smoother filtering to reduce high-frequency noise
- Pearson correlation for lag-based autocorrelation
- DFT conversion to power spectrum
- Adaptive maximum power tracking with decay
- Optional cubic enhancement to sharpen spectral peaks

## Architecture & Physics

### 1. High-Pass Filter

The high-pass filter removes DC offset and low-frequency trend components that would otherwise dominate the autocorrelation:

$$
\alpha_{HP} = \frac{\cos(\theta) + \sin(\theta) - 1}{\cos(\theta)}
$$

where $\theta = \sqrt{2} \cdot \frac{\pi}{\text{maxPeriod}}$

The filter is a second-order IIR:

$$
HP_t = (1 - \frac{\alpha_{HP}}{2})^2 (P_t - 2P_{t-1} + P_{t-2}) + 2(1 - \alpha_{HP})HP_{t-1} - (1 - \alpha_{HP})^2 HP_{t-2}
$$

### 2. Super-Smoother Filter

The super-smoother removes high-frequency noise while preserving cyclical content:

$$
a_1 = e^{-\sqrt{2} \cdot \pi / \text{minPeriod}}
$$

$$
b_1 = 2 a_1 \cos\left(\sqrt{2} \cdot \frac{\pi}{\text{minPeriod}}\right)
$$

$$
c_1 = 1 - c_2 - c_3, \quad c_2 = b_1, \quad c_3 = -a_1^2
$$

$$
F_t = \frac{c_1}{2}(HP_t + HP_{t-1}) + c_2 F_{t-1} + c_3 F_{t-2}
$$

### 3. Pearson Autocorrelation

For each lag $\ell$ from 2 to maxPeriod, compute the Pearson correlation between the filtered series and its lagged version:

$$
r_\ell = \frac{n \sum x_i y_i - \sum x_i \sum y_i}{\sqrt{(n \sum x_i^2 - (\sum x_i)^2)(n \sum y_i^2 - (\sum y_i)^2)}}
$$

where $x_i = F_{t-i}$ and $y_i = F_{t-\ell-i}$ for $i \in [0, \text{window})$.

### 4. Discrete Fourier Transform

Convert autocorrelation to power spectrum via DFT:

$$
\text{cosAcc}_p = \sum_{n=2}^{\text{maxPeriod}} r_n \cos\left(\frac{2\pi n}{p}\right)
$$

$$
\text{sinAcc}_p = \sum_{n=2}^{\text{maxPeriod}} r_n \sin\left(\frac{2\pi n}{p}\right)
$$

$$
\text{Power}_p = \text{cosAcc}_p^2 + \text{sinAcc}_p^2
$$

### 5. Smoothed Power Spectrum

Apply EMA-style smoothing to the power spectrum:

$$
S_p = 0.2 \cdot \text{Power}_p^2 + 0.8 \cdot S_{p,\text{prev}}
$$

### 6. Adaptive Maximum Power Tracking

Track the maximum power with decay to normalize the spectrum:

$$
\text{MaxPwr}_t = \begin{cases}
\text{localMax} & \text{if localMax} > \text{MaxPwr}_{t-1} \\
K \cdot \text{MaxPwr}_{t-1} & \text{otherwise}
\end{cases}
$$

where $K = 10^{-0.15 / (\text{maxPeriod} - \text{minPeriod})}$

### 7. Dominant Cycle Extraction

Normalize power and optionally apply cubic enhancement:

$$
\text{pwr}_p = \frac{S_p}{\text{MaxPwr}_t}
$$

$$
\text{pwr}_p = \text{pwr}_p^3 \quad \text{(if enhance = true)}
$$

Compute weighted average of periods with sufficient power:

$$
\text{Dom}_t = \frac{\sum_{p:\text{pwr}_p \geq 0.5} p \cdot \text{pwr}_p}{\sum_{p:\text{pwr}_p \geq 0.5} \text{pwr}_p}
$$

Apply smoothing:

$$
\text{Dom}_t = 0.2 \cdot (\text{baseDom} - \text{Dom}_{t-1}) + \text{Dom}_{t-1}
$$

## Mathematical Foundation

### Wiener-Khinchin Theorem

The theorem establishes that for a wide-sense stationary process:

$$
S(\omega) = \mathcal{F}\{R(\tau)\}
$$

where $S(\omega)$ is the power spectral density and $R(\tau)$ is the autocorrelation function. This means peaks in the autocorrelation at lag $\tau$ correspond to peaks in the power spectrum at frequency $\omega = 2\pi/\tau$.

### Filter Design Rationale

The high-pass filter cutoff at maxPeriod ensures cycles longer than the detection range are attenuated. The super-smoother cutoff at minPeriod removes noise at frequencies higher than the detection range. This creates a bandpass effect that isolates cycles within [minPeriod, maxPeriod].

### Cubic Enhancement

The cubic function $f(x) = x^3$ sharpens peaks because:
- Values near 1 remain close to 1: $0.9^3 = 0.729$
- Values near 0 become much smaller: $0.5^3 = 0.125$

This creates better separation between dominant and spurious cycles.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| HP filter (MUL/ADD) | 8 | 3 | 24 |
| SS filter (MUL/ADD) | 6 | 3 | 18 |
| Autocorrelation loop | O(maxPeriod × avgLength) | 5 | ~1200 |
| DFT loop | O(maxPeriod²) | 10 | ~23000 |
| Power normalization | O(maxPeriod) | 3 | ~150 |
| Weighted average | O(maxPeriod) | 5 | ~250 |
| **Total** | — | — | **~25000 cycles** |

The DFT loop dominates at O(maxPeriod²). For maxPeriod=48, this is ~2300 iterations per bar.

### Batch Mode

Due to the recursive nature of autocorrelation and DFT, SIMD optimization is limited to:
- Vectorized DFT inner products (modest gains)
- Parallel power normalization

Expected speedup: ~1.3x with AVX2 for DFT vectorization.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Good cycle detection for clean signals |
| **Timeliness** | 6/10 | Requires warmup; smoothing adds lag |
| **Overshoot** | 7/10 | Bounded output range prevents extremes |
| **Smoothness** | 8/10 | EMA smoothing reduces jitter |
| **Noise Rejection** | 7/10 | Dual filtering provides good denoising |

## Validation

EACP is a proprietary Ehlers indicator not commonly found in standard libraries.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation |

Validation is performed against:
- Mathematical properties (bounded output, sine wave detection)
- PineScript formula verification
- Streaming vs batch consistency

## Common Pitfalls

1. **Warmup Period**: EACP requires approximately 2×maxPeriod bars to stabilize. During warmup, the dominant cycle estimate is biased toward the midpoint of [minPeriod, maxPeriod]. Always check `IsHot` before using results.

2. **Computational Cost**: The O(maxPeriod²) DFT is expensive. For real-time applications with maxPeriod > 100, consider reducing the period range or increasing the bar interval.

3. **Parameter Sensitivity**: The minPeriod/maxPeriod range must bracket the expected cycle. If the true cycle is outside this range, detection will fail. Start with a wide range (8-48) and narrow based on market characteristics.

4. **Enhance Mode**: While cubic enhancement sharpens peaks, it can also suppress weak-but-valid cycles. Disable enhancement when analyzing low-amplitude cycles or noisy data.

5. **Memory Footprint**: The indicator maintains O(maxPeriod) buffers for correlation, power, and smoothed power. Each instance consumes ~2KB for default parameters.

6. **Non-Stationary Markets**: Markets without clear cyclical behavior will produce unstable dominant cycle estimates. Use normalized power as a confidence metric: high power indicates strong cyclical behavior.

## References

- Ehlers, J.F. (2013). "Cycle Analytics for Traders." Wiley.
- Ehlers, J.F. "Autocorrelation Periodogram." Technical Analysis of Stocks & Commodities.
- Wiener, N. (1930). "Generalized Harmonic Analysis." Acta Mathematica.
- Khinchin, A.Y. (1934). "Korrelationstheorie der stationären stochastischen Prozesse." Mathematische Annalen.