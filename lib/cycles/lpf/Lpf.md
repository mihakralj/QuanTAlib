# LPF: Ehlers Linear Predictive Filter

Griffiths-adapted dominant cycle estimator — uses LMS-predicted filter coefficients as a spectral window.

| Property       | Value                                                |
|:-------------- |:---------------------------------------------------- |
| **Category**   | Cycles                                               |
| **Inputs**     | Single series (close)                                |
| **Parameters** | `lowerBound` (int, 18), `upperBound` (int, 40), `dataLength` (int, 40) |
| **Outputs**    | Dominant Cycle (period), Signal (±1 AGC), Predict    |
| **Output range**| \[lowerBound, upperBound\]                          |
| **Warmup**     | `2 × upperBound`                                     |
| **PineScript** | [lpf.pine](lpf.pine)                                |

- Applies a roofing filter (HP + SuperSmoother) to produce band-limited data, then adapts Griffiths LMS coefficients to minimize one-step prediction error.
- Transforms the adapted coefficients into a frequency-domain power spectrum via DFT, identifying the dominant cycle as the spectral center of gravity.
- Constrains the dominant cycle to change by at most 2 bars per update, preventing erratic mode-switching in noisy data.

## Historical Context

John F. Ehlers introduced the Linear Predictive Filter in "Linear Predictive Filters And Instantaneous Frequency" (*Technical Analysis of Stocks & Commodities*, January 2025). The algorithm adapts Lloyd Griffiths' "Rapid Measurement of Digital Instantaneous Frequency" (IEEE Trans. ASSP-23, 1975) — a time-domain gradient method for adaptive spectral estimation originally developed for radar and sonar signal processing. Ehlers' innovation was combining this with his roofing filter and AGC normalization to create a self-calibrating cycle detector for financial data. Unlike his earlier Autocorrelation Periodogram (ACP), LPF estimates the spectrum from the *predictor coefficients* rather than from autocorrelation lags, yielding sharper spectral resolution with fewer data points.

## Mathematical Foundation

### Stage 1: Roofing Filter (Band-Limiting)

A 2nd-order Butterworth highpass removes trend (periods > `upperBound`), followed by a SuperSmoother lowpass that removes noise (periods < `lowerBound`):

$$\alpha_{HP} = \frac{\cos(0.707 \cdot 2\pi / U) + \sin(0.707 \cdot 2\pi / U) - 1}{\cos(0.707 \cdot 2\pi / U)}$$

$$HP_n = (1-\tfrac{\alpha}{2})^2 (x_n - 2x_{n-1} + x_{n-2}) + 2(1-\alpha)\,HP_{n-1} - (1-\alpha)^2\,HP_{n-2}$$

$$a_1 = e^{-\sqrt{2}\pi/L}, \quad b_1 = 2a_1\cos(\sqrt{2}\pi/L)$$

$$LP_n = (1-b_1+a_1^2)\tfrac{HP_n+HP_{n-1}}{2} + b_1\,LP_{n-1} - a_1^2\,LP_{n-2}$$

### Stage 2: AGC Normalization

$$\text{Peak}_n = \max(0.991 \cdot \text{Peak}_{n-1},\; |LP_n|)$$

$$\text{Signal}_n = LP_n / \text{Peak}_n$$

### Stage 3: Griffiths LMS Predictor

The heart of the algorithm — adaptive coefficient update minimizing prediction error:

$$P_{\text{sig}} = \frac{1}{N}\sum_{i=0}^{N-1} x_i^2, \qquad \mu = \frac{0.25}{P_{\text{sig}} \cdot N}$$

$$\hat{x}_0 = \sum_{i=1}^{N} c_i \cdot x_i, \qquad \varepsilon = x_0 - \hat{x}_0$$

$$c_i \leftarrow c_i + \mu \cdot \varepsilon \cdot x_i \quad \forall\, i \in [1, N]$$

### Stage 4: Spectral Estimation via DFT of Coefficients

$$\text{Pwr}(P) = \left(\sum_{i=1}^{N} c_i \cos\tfrac{2\pi i}{P}\right)^2 + \left(\sum_{i=1}^{N} c_i \sin\tfrac{2\pi i}{P}\right)^2$$

### Stage 5: Dominant Cycle (Center of Gravity)

$$DC = \frac{\sum_{P: \text{Pwr}(P) \geq 0.5} P \cdot \text{Pwr}(P)}{\sum_{P: \text{Pwr}(P) \geq 0.5} \text{Pwr}(P)}, \qquad |\Delta DC| \leq 2$$

### Parameter Mapping

| Parameter     | Effect                              | Recommended           |
|:------------- |:----------------------------------- |:--------------------- |
| `lowerBound`  | Shortest cycle detected             | 18 swing, 8 minimum   |
| `upperBound`  | Longest cycle detected              | 40 swing, 125+ position |
| `dataLength`  | Predictor adaptation window         | Match `upperBound`    |

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation        | Count per bar                        |
|:---------------- |:------------------------------------ |
| HP filter        | 5 mul, 4 add                         |
| SuperSmoother    | 3 mul, 3 add                         |
| AGC              | 2 mul, 1 cmp                         |
| Buffer shift     | N copies                             |
| Signal power     | N mul, N add                         |
| LMS predict      | N mul, N add                         |
| Coef update      | 2N mul, N add                        |
| DFT spectrum     | 2N(U−L) trig, 2N(U−L) mul           |
| CoG              | (U−L) mul, (U−L) add                |
| **Total**        | **O(N² + N(U−L))**                  |

### Batch Mode (SIMD Analysis)

The DFT spectrum loop (Stage 4) is the dominant cost. For typical parameters (N=40, U−L=22), each bar requires ~1,760 trig evaluations. Due to the adaptive coefficient state, vectorization is limited to within-period parallelization.

### Quality Metrics

| Metric                | Value / Estimate             |
|:----------------------|:-----------------------------|
| Spectral resolution   | Higher than ACP for same N   |
| Adaptation speed      | ~N bars to converge          |
| Phase lag             | ≤2 bars (constrained)        |
| Noise sensitivity     | Low (roofing + AGC)          |

## Validation

| Test                   | Input                                    | Expected                    |
|:---------------------- |:---------------------------------------- |:--------------------------- |
| Default parameters     | Close series, 500 bars                   | Dominant cycle in [18, 40]  |
| Pure sine (30-bar)     | sin(2π·n/30) for 500 bars                | Converges near 30           |
| Constant input         | All values = 100.0                       | Stable, no NaN/Inf          |
| Short series (<warmup) | 10 bars                                  | Returns valid values        |
| Signal range           | Any input                                | Signal ∈ [−1, 1]            |

### Behavioral Test Summary

| Behavior              | Assertion                                |
|:----------------------|:-----------------------------------------|
| Monotonic exclusion   | DC stays within [lowerBound, upperBound] |
| Rate-of-change limit  | |ΔDC| ≤ 2 bars per update                 |
| AGC normalization     | |Signal| ≤ 1                             |
| Convergence           | Pure sine → DC ≈ true period            |
| State rollback        | isNew=false restores previous state      |

## Common Pitfalls

| Pitfall                                  | Remedy                                                        |
|:---------------------------------------- |:------------------------------------------------------------- |
| `upperBound` too low for data            | Use ≥ 40 for swing trading, ≥ 125 for position trading       |
| `dataLength` too short                   | Match or exceed `upperBound` for optimal spectral resolution  |
| `lowerBound` < 8                         | Causes aliasing artifacts; enforced minimum is 8              |
| Expecting zero-lag                       | DC lags by up to 2 bars due to rate constraint                |
| Using during non-cyclical regime         | Filter coefficients may not converge; check Signal amplitude  |

## References

- Ehlers, J. F. "Linear Predictive Filters And Instantaneous Frequency." *TASC*, Jan 2025.
- Griffiths, L. J. "Rapid Measurement of Digital Instantaneous Frequency." *IEEE Trans. ASSP-23*, pp. 207–222, April 1975.
- Ehlers, J. F. *Cycle Analytics for Traders*. Wiley, 2013.
- Ehlers, J. F. "LINEAR PREDICTION." *MESA Software Technical Paper*.
