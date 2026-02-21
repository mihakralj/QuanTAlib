# HEND: Henderson Moving Average

> "Robert Henderson designed a filter so good that the Australian Bureau of Statistics still uses it a century later. When your smoothing algorithm outlasts empires, you did something right."

HEND is a symmetric FIR filter derived from the Henderson (1916) closed-form weight formula, designed to pass cubic polynomial trends without distortion while maximally suppressing irregular noise. Used as the core smoother in the X-11 and X-13ARIMA-SEATS seasonal adjustment frameworks by statistical agencies worldwide, HEND achieves the theoretically optimal trade-off between smoothness (measured by the sum of squared third differences of the weights) and fidelity for cubic trends. Weights can be negative at the edges, giving the filter a bandpass-like property that sharpens trend-cycle extraction.

## Historical Context

Robert Henderson published the weight formula in 1916 in the *Transactions of the Actuarial Society of America*, motivated by the need to graduate mortality tables without distorting underlying polynomial trends. The U.S. Census Bureau adopted Henderson filters as the trend-cycle component of the X-11 method (Shiskin, Young, and Musgrave, 1967), where 5, 9, 13, and 23-point Henderson filters became standard choices. The Australian Bureau of Statistics (ABS) uses the 13-point Henderson as its default trend estimator for quarterly national accounts.

Henderson's filter has a unique property among polynomial-preserving smoothers: it minimizes the sum of squared third differences of the filter weights subject to the constraint that polynomials up to degree 3 pass through unchanged. This optimality criterion produces smoother weight sequences than Savitzky-Golay filters of the same polynomial order, at the cost of a fixed (non-configurable) smoothness-fidelity balance.

The requirement for odd period length ($N \geq 5$) stems from the symmetric weight structure. Even-length Henderson filters are mathematically possible but break the centered-symmetry property that guarantees zero phase distortion.

## Architecture & Physics

### 1. Weight Computation (One-Time)

Weights are computed from Henderson's closed-form formula:

$$
w(k) = \frac{315 \left[(n-1)^2 - k^2\right]\left[n^2 - k^2\right]\left[(n+1)^2 - k^2\right]\left[3n^2 - 16 - 11k^2\right]}{8n(n^2-1)(4n^2-1)(4n^2-9)(4n^2-25)}
$$

where $n = (N+3)/2$ and $k$ ranges from $-(N-1)/2$ to $(N-1)/2$. Weights are normalized to sum to 1.0 after computation.

### 2. Symmetric Convolution

The filter applies as a standard FIR convolution over the circular buffer. Because weights are symmetric ($w(k) = w(-k)$), the implementation can exploit symmetry to halve multiplications, though the normalization step makes this optional.

### 3. Negative Edge Weights

Unlike most window-based averages, Henderson weights are negative at the extremes of the window. This is not a bug; it is the mechanism by which the filter suppresses low-frequency drift that would distort cubic trends. The negative wings act as a gentle high-pass correction.

## Mathematical Foundation

The Henderson filter minimizes:

$$
\min_{\{w_k\}} \sum_{k} (\Delta^3 w_k)^2 \quad \text{subject to} \quad \sum_{k} k^j w_k = \delta_{j0}, \quad j = 0, 1, 2, 3
$$

where $\Delta^3$ is the third-difference operator. The constraints ensure that constant, linear, quadratic, and cubic polynomials are reproduced exactly.

The closed-form solution with $n = (N+3)/2$, $k \in [-(N-1)/2, (N-1)/2]$:

$$
w(k) = \frac{315 \cdot \left[(n-1)^2 - k^2\right]\left[n^2 - k^2\right]\left[(n+1)^2 - k^2\right]\left[3n^2 - 16 - 11k^2\right]}{8n(n^2-1)(4n^2-1)(4n^2-9)(4n^2-25)}
$$

**Frequency response:** The Henderson filter has zeros at specific frequencies determined by the polynomial-preservation constraints. For the 13-point filter, sidelobe attenuation exceeds $-40$ dB.

**Default parameters:** `period = 7` (must be odd, $\geq 5$).

**Pseudo-code (streaming):**

```
// One-time weight computation
half = (period - 1) / 2
n = (period + 3) / 2
for k = -half to half:
    w[k] = 315 * ((n-1)²-k²) * (n²-k²) * ((n+1)²-k²) * (3n²-16-11k²)
           / [8n(n²-1)(4n²-1)(4n²-9)(4n²-25)]
normalize(w)

// Per-bar convolution
buffer.push(price)
if count < period: return price
result = Σ buffer[j] * w[j] for j = 0..period-1
return result
```

## Resources

- Henderson, R. (1916). "Note on Graduation by Adjusted Average." *Transactions of the Actuarial Society of America*, 17, 43-48.
- Shiskin, J., Young, A.H., & Musgrave, J.C. (1967). "The X-11 Variant of the Census Method II Seasonal Adjustment Program." Technical Paper 15, U.S. Bureau of the Census.
- Hyndman, R.J. (2011). "Moving Averages." In *International Encyclopedia of Statistical Science*. Springer.
- Kenny, P.B. & Durbin, J. (1982). "Local Trend Estimation and Seasonal Adjustment of Economic and Social Time Series." *JRSS Series A*, 145(1), 1-41.
