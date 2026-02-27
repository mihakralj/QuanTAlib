# EACP: Ehlers Autocorrelation Periodogram

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `minPeriod` (default 8), `maxPeriod` (default 48), `avgLength` (default 3), `enhance` (default true)                      |
| **Outputs**      | Single series (Eacp)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `maxPeriod * 2` bars                          |

### TL;DR

- EACP estimates the dominant cycle period of a financial time series by computing autocorrelation across multiple lags and transforming the result i...
- Parameterized by `minperiod` (default 8), `maxperiod` (default 48), `avglength` (default 3), `enhance` (default true).
- Output range: Varies (see docs).
- Requires `maxPeriod * 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

EACP estimates the dominant cycle period of a financial time series by computing autocorrelation across multiple lags and transforming the result into a power spectrum via the Wiener-Khinchin theorem. The output is a continuously updating cycle period measurement (in bars) that can adaptively tune other indicators to the market's current rhythm, making fixed-period assumptions unnecessary.

## Historical Context

John Ehlers introduced the Autocorrelation Periodogram to solve the fundamental problem of cycle measurement in noisy financial data. Traditional spectral methods (FFT) assume stationarity and require long data windows, making them impractical for real-time trading. Ehlers leveraged the Wiener-Khinchin theorem, which establishes that a signal's autocorrelation function and its power spectral density form a Fourier transform pair. By computing autocorrelation in the time domain and transforming to frequency via a discrete cosine transform, the algorithm identifies spectral peaks corresponding to dominant periodicities. The center-of-gravity weighting of spectral peaks provides a robust, noise-tolerant period estimate. This enables truly adaptive trading systems where RSI, Stochastic, or moving average periods track the market's actual cycle length rather than relying on fixed parameters.

## Architecture & Physics

### 1. Signal Pre-processing

A high-pass filter removes the DC (trend) component, and a Super-Smoother filter attenuates aliasing noise above the Nyquist frequency:

$$HP_t = (1 - \alpha_{HP}/2)^2 (P_t - 2P_{t-1} + P_{t-2}) + 2(1 - \alpha_{HP}) HP_{t-1} - (1 - \alpha_{HP})^2 HP_{t-2}$$

The Super-Smoother then applies a 2-pole Butterworth low-pass to $HP_t$.

### 2. Autocorrelation

For each lag $k$ from 0 to MaxPeriod, the normalized Pearson autocorrelation is computed over an averaging window of $M$ samples:

$$R_k = \frac{\sum_{i=0}^{M-1} (x_i - \bar{x})(x_{i-k} - \bar{x})}{\sqrt{\sum (x_i - \bar{x})^2 \sum (x_{i-k} - \bar{x})^2}}$$

A high $R_k$ at lag 20 implies a 20-bar cycle is present.

### 3. Power Spectrum (DFT of Autocorrelation)

For each candidate period $p$ in [MinPeriod, MaxPeriod]:

$$P_p = \left(\sum_{k=0}^{M-1} R_k \cos\!\left(\frac{2\pi k}{p}\right)\right)^2$$

Smoothed with exponential decay: $S_p = 0.2 \cdot P_p + 0.8 \cdot S_{p,prev}$

### 4. Dominant Cycle Extraction

Center-of-gravity weighting across spectral peaks:

$$DC = \frac{\sum S_p \cdot p}{\sum S_p}$$

### 5. Optional Enhancement

When `enhance=true`, spectral values are cubed before CG weighting, sharpening peaks but increasing sensitivity to noise.

### 6. Complexity

$O(N \times M)$ per bar where $N$ is the period range and $M$ is the averaging length. This is one of the most computationally expensive indicators due to nested correlation and DFT loops. Memory is $O(N)$ for correlation and power arrays.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `minPeriod` | Minimum period to evaluate | 8 | $\geq 3$ |
| `maxPeriod` | Maximum period to evaluate | 48 | $> minPeriod$ |
| `enhance` | Apply cubic emphasis to spectral peaks | true | |

### Pseudo-code

```
function EACP(source, minPeriod, maxPeriod, enhance):
    N ← maxPeriod - minPeriod + 1
    M ← maxPeriod        // averaging window
    hpBuf ← HighPassFilter(source)
    ssfBuf ← SuperSmoother(hpBuf)

    power[N] ← {0}
    smoothPower[N] ← {0}

    for each bar:
        // Autocorrelation for each lag
        corr[0..maxPeriod] ← PearsonAutocorrelation(ssfBuf, M)

        // DFT: convert autocorrelation to power spectrum
        for p = minPeriod to maxPeriod:
            cosPower ← 0
            for k = 0 to M-1:
                cosPower += corr[k] * cos(2π * k / p)
            power[p] ← cosPower²

        // Exponential smoothing of spectrum
        for p = minPeriod to maxPeriod:
            smoothPower[p] ← 0.2 * power[p] + 0.8 * smoothPower[p]

        // Optional cubic enhancement
        if enhance:
            for p: smoothPower[p] ← smoothPower[p]³

        // AGC normalization
        maxPow ← max(smoothPower)
        for p: smoothPower[p] /= maxPow   // normalize to [0, 1]

        // Center-of-gravity dominant cycle
        num ← 0; den ← 0
        for p = minPeriod to maxPeriod:
            num += smoothPower[p] * p
            den += smoothPower[p]
        dominantCycle ← (den > 0) ? num / den : (minPeriod + maxPeriod) / 2

        emit dominantCycle
```

### Output Interpretation

| Output | Meaning |
|--------|---------|
| `dominantCycle` | Estimated dominant period in bars (use to tune other indicators) |
| Stable value | Market exhibiting regular cyclical behavior |
| Rapidly changing value | Market transitioning between regimes |
| Pegged at maxPeriod | No clear cycle detected; likely trending |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| HP filter (2-pole IIR) | ~8 | Pre-processing trend removal |
| Super-Smoother (2-pole IIR) | ~6 | Anti-aliasing low-pass |
| Pearson autocorrelation | ~5M | Mean, variance, cross-product over M samples per lag |
| Autocorrelation loop (N lags) | ~5NM | Nested: N lags × M-sample windows |
| DFT cosine transform | ~3NM | N periods × M cosine multiply-accumulates |
| Cosine evaluation | NM | `Math.Cos` calls (expensive transcendental) |
| Exponential smoothing | ~2N | FMA per period bin |
| Cubic enhancement | ~2N | Two multiplies per bin (when enabled) |
| AGC normalization | ~2N | Max scan + N divides |
| Center-of-gravity | ~3N | Weighted sum + division |
| **Total (default N=41, M=48)** | **~16,000** | **Dominated by autocorrelation + DFT** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Partially: inner DFT cosine loops vectorizable; autocorrelation outer loop sequential |
| Bottleneck | Pearson autocorrelation: N×M multiply-accumulates with data-dependent means |
| Parallelism | DFT accumulation per period is independent; `Vector<double>` applicable to inner sums |
| Memory | O(N) power arrays + O(M) circular buffer for SSF history |
| Throughput | ~100-200× slower than O(1) IIR indicators; most expensive cycle indicator |

## Resources

- **Ehlers, J.F.** *Cycle Analytics for Traders*. Wiley, 2013.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Wiener, N.** "Generalized Harmonic Analysis." *Acta Mathematica*, 55(1), 1930.
- **Khinchin, A.** "Korrelationstheorie der stationären stochastischen Prozesse." *Mathematische Annalen*, 109(1), 1934.
