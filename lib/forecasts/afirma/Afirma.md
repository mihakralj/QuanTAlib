# AFIRMA: Autoregressive FIR Moving Average

> "Standard Moving Averages assume linear or exponential weights. AFIRMA asks: what if we used signal processing window functions instead?"

AFIRMA is a Windowed Weighted Moving Average that replaces standard linear weighting with weights derived from signal processing window functions (Hanning, Hamming, Blackman, Blackman-Harris). This approach achieves specific frequency response characteristics tailored to noise reduction.

The optional Least Squares mode fits a linear regression to recent bars and blends the fitted values with original data, producing a hybrid smoothed-predicted output.

## Historical Context

Moving averages traditionally use Simple (rectangular window), Weighted (triangular window), or Exponential (recursive) forms. The DSP community solved finite filter design decades ago using window functions to minimize spectral leakage (ringing artifacts at discontinuities).

AFIRMA applies these well-understood coefficients directly to price series. It is effectively an FIR filter where coefficients are determined solely by the chosen window function—no manual coefficient calculation required.

## Architecture & Physics

AFIRMA maintains a sliding window of the last $P$ prices and computes a weighted average using pre-calculated window coefficients.

### Window Functions

Instead of linear weights ($1, 2, 3...$), AFIRMA generates weights using cosine-sum series:

$$
w_k = a_0 + a_1 \cos\left(\frac{2\pi k}{P}\right) + a_2 \cos\left(\frac{4\pi k}{P}\right) + a_3 \cos\left(\frac{6\pi k}{P}\right)
$$

where $P$ is the period and $k$ is the index from $0$ to $P-1$.

| Window | Coefficients | Main Lobe | Side Lobe |
| :--- | :--- | :---: | :---: |
| **Rectangular** | $a_0=1$ | Narrowest | −13 dB |
| **Hanning** | $a_0=0.5$, $a_1=-0.5$ | Medium | −32 dB |
| **Hamming** | $a_0=0.54$, $a_1=-0.46$ | Medium | −43 dB |
| **Blackman** | $a_0=0.42$, $a_1=-0.5$, $a_2=0.08$ | Wide | −58 dB |
| **Blackman-Harris** | $a_0=0.35875$, $a_1=-0.48829$, $a_2=0.14128$, $a_3=-0.01168$ | Widest | −92 dB |

The default **Blackman-Harris** provides maximum side-lobe suppression (−92 dB), ideal for financial data with non-Gaussian noise spikes.

### Least Squares Mode

When `leastSquares=true`, AFIRMA performs an additional step after the base weighted average:

1. **Determine regression window**: $n = \min\left(\lfloor(P-1)/2\rfloor, 50\right)$
2. **Fit linear regression** to the most recent $n$ bars (lags 0 to $n-1$)
3. **Create hybrid buffer**: Use fitted values for lags $0$ to $n-1$, original values for lags $n$ to $P-1$
4. **Average the hybrid buffer**: Simple mean of all $P$ values

This produces a smoothed estimate that incorporates short-term trend extrapolation. The fitted portion projects recent momentum while the original portion anchors to historical context.

Note: Despite some references calling this "cubic polynomial fitting," the actual implementation uses **linear regression** (first-degree polynomial: $y = \text{intercept} + \text{slope} \times x$).

## Mathematical Foundation

### Base Filter Equation

$$
\text{AFIRMA}_t = \frac{\sum_{k=0}^{P-1} w_k \cdot x_{t-k}}{\sum_{k=0}^{P-1} w_k}
$$

where $x$ is the input series and $w_k$ are the window weights.

### Window Coefficient Formulas

**Hanning:**
$$
w_k = 0.5 - 0.5 \cos\left(\frac{2\pi k}{P}\right)
$$

**Hamming:**
$$
w_k = 0.54 - 0.46 \cos\left(\frac{2\pi k}{P}\right)
$$

**Blackman:**
$$
w_k = 0.42 - 0.5 \cos\left(\frac{2\pi k}{P}\right) + 0.08 \cos\left(\frac{4\pi k}{P}\right)
$$

**Blackman-Harris:**
$$
w_k = 0.35875 - 0.48829 \cos\left(\frac{2\pi k}{P}\right) + 0.14128 \cos\left(\frac{4\pi k}{P}\right) - 0.01168 \cos\left(\frac{6\pi k}{P}\right)
$$

### Least Squares Regression

Given regression window size $n$:

$$
S_x = \frac{(n-1)n}{2}, \quad S_{x^2} = \frac{(n-1)n(2n-1)}{6}
$$

$$
S_y = \sum_{i=0}^{n-1} x_{t-i}, \quad S_{xy} = \sum_{i=0}^{n-1} i \cdot x_{t-i}
$$

$$
\text{slope} = \frac{n \cdot S_{xy} - S_x \cdot S_y}{n \cdot S_{x^2} - S_x^2}
$$

$$
\text{intercept} = \frac{S_y - \text{slope} \cdot S_x}{n}
$$

Fitted value at lag $i$: $\hat{x}_i = \text{intercept} + \text{slope} \cdot i$

Final LS output:
$$
\text{AFIRMA}_{LS} = \frac{1}{P} \left( \sum_{i=0}^{n-1} \hat{x}_i + \sum_{i=n}^{P-1} x_{t-i} \right)
$$

## Parameters

| Parameter | Default | Range | Description |
| :--- | :---: | :--- | :--- |
| **Period** | — | ≥ 1 | Window length (number of taps) |
| **Window** | BlackmanHarris | Enum | Window function for weight generation |
| **LeastSquares** | false | bool | Enable linear regression blending |

## Performance Profile

| Metric | Value | Notes |
| :--- | :---: | :--- |
| **Complexity** | O(P) | Convolution per bar |
| **Allocations** | 0 | Zero-allocation in Update and Batch spans |
| **Warmup** | P bars | `WarmupPeriod = period` |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Excellent noise suppression |
| **Timeliness** | 6/10 | Inherent FIR lag (~P/2 bars) |
| **Overshoot** | 2/10 | Minimal; no recursive amplification |
| **Smoothness** | 10/10 | Exceptional with Blackman-Harris |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Pine Script** | ✅ | Matches `afirma.pine` reference |
| **Internal** | ✅ | Batch, Streaming, Span modes consistent |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |

## Window Comparison

For identical period, different windows trade smoothness for responsiveness:

| Window | Smoothness | Lag | Best For |
| :--- | :---: | :---: | :--- |
| Rectangular | Low | Lowest | Equivalent to SMA |
| Hanning | Medium | Medium | Balanced general use |
| Hamming | Medium-High | Medium | Better spectral properties than Hanning |
| Blackman | High | Higher | Noisy trending markets |
| BlackmanHarris | Highest | Highest | Maximum noise rejection |

## Common Pitfalls

1. **Lag Increases with Smoothness**: Blackman-Harris has the best noise rejection but also the most lag. For fast signals, consider Hanning or even Rectangular (which degrades to SMA).

2. **Least Squares Is Not Magic**: LS mode adds trend extrapolation but can overshoot during reversals. It works best in trending markets, not choppy conditions.

3. **Large Periods Amplify Lag**: FIR filters have inherent delay of approximately $P/2$ bars. Period 50 means ~25 bars of lag regardless of window choice.

4. **isNew Parameter Matters**: When processing live ticks within the same bar, use `Update(value, isNew: false)`. When a new bar opens, use `isNew: true` (default). Incorrect usage corrupts internal state.

5. **NaN Handling**: Non-finite inputs (NaN, ±Infinity) are replaced with the last valid value. Consecutive NaN inputs maintain the last known good value. After `Reset()`, the first valid input establishes the baseline.

6. **Warmup Period**: AFIRMA requires `period` bars before `IsHot` becomes true. During warmup, it uses available data with proportionally adjusted weights.

## References

- Harris, F. J. (1978). "On the use of windows for harmonic analysis with the discrete Fourier transform." *Proceedings of the IEEE*, 66(1), 51-83.
- Nuttall, A. H. (1981). "Some windows with very good sidelobe behavior." *IEEE Transactions on Acoustics, Speech, and Signal Processing*, 29(1), 84-91.