# FSI: Ehlers Fourier Series Indicator

## Overview

The Fourier Series Indicator (FSI) decomposes price into its dominant cyclic components using three bandpass filters tuned to the fundamental cycle and its 2nd and 3rd harmonics. The outputs are amplitude-weighted and summed to reconstruct a waveshape that reveals the underlying cycle structure of price.

## Origin

John F. Ehlers, "Fourier Series Model of the Market," *Technical Analysis of Stocks & Commodities* (TASC), June 2019.

## Algorithm

### 1. Ehlers Bandpass Coefficients

For a given period $P$ and bandwidth $\delta$:

$$L = \cos\!\left(\frac{2\pi}{P}\right), \quad G = \cos\!\left(\frac{\delta \cdot 2\pi}{P}\right), \quad S = \frac{1}{G} - \sqrt{\frac{1}{G^2} - 1}$$

### 2. Three Harmonic Bandpass Filters

| Harmonic     | Period | Variables |
|:-------------|:-------|:----------|
| Fundamental  | $x$    | BP1, Q1, P1 |
| 2nd harmonic | $x/2$  | BP2, Q2, P2 |
| 3rd harmonic | $x/3$  | BP3, Q3, P3 |

Each bandpass filter is a 2-pole IIR:

$$BP_k[i] = \tfrac{1}{2}(1 - S_k)\bigl(C[i] - C[i{-}2]\bigr) + L_k(1 + S_k)\,BP_k[i{-}1] - S_k\,BP_k[i{-}2]$$

### 3. Quadrature (90° Phase Shift)

$$Q_k[i] = \frac{x}{2\pi}\bigl(BP_k[i] - BP_k[i{-}1]\bigr)$$

### 4. Power Estimation

$$P_k = \sum_{j=0}^{x-1}\bigl(BP_k[i{-}j]^2 + Q_k[i{-}j]^2\bigr)$$

### 5. Amplitude-Weighted Reconstruction

$$\text{FSI} = BP_1 + \sqrt{\frac{P_2}{P_1}} \cdot BP_2 + \sqrt{\frac{P_3}{P_1}} \cdot BP_3$$

### 6. Complexity

- **Streaming:** O(1) per bar — three IIR evaluations + O(1) rolling power sums
- **Batch (span):** O(N) total, zero allocation via `stackalloc`

## Parameters

| Parameter   | Default | Min   | Description                              |
|:------------|:--------|:------|:-----------------------------------------|
| `period`    | 20      | 6     | Fundamental cycle length in bars         |
| `bandwidth` | 0.1     | 0.001 | Bandpass filter bandwidth (passband width)|

## Output Interpretation

- **Zero crossings:** Potential cycle turning points
- **Peaks/troughs:** Local cycle extremes
- **Amplitude:** Reflects the strength of the dominant cycle
- **The output is zero-centered** — positive values indicate upward cycle phase, negative values indicate downward

## Operation Count (Streaming Mode)

| Operation           | Count |
|:--------------------|:------|
| Multiplications     | 12    |
| Additions           | 12    |
| Square roots        | 2     |
| Comparisons         | 1     |
| **Total per bar**   | ~27   |

## Resources

- John F. Ehlers, "Fourier Series Model of the Market," TASC June 2019
- [MetaStock formula reference](https://forum.metastock.com/posts/m185900findunread-June-2019--Fourier-Series-Model-of-the-Market)
- [thinkorswim FourierSeriesIndicator](https://toslc.thinkorswim.com/center/reference/Tech-Indicators/studies-library/E-F/FourierSeriesIndicator)
