# SAK: Swiss Army Knife Indicator

> "John Ehlers unified nine filter types into one second-order IIR framework. Change the coefficients and you get EMA, SMA, Gaussian, Butterworth, smoother, high-pass, 2-pole high-pass, band-pass, or band-stop. One formula to implement them all."

SAK is a unified second-order IIR filter framework where five coefficient sets ($c_0$, $b_0$, $b_1$, $b_2$, $a_1$, $a_2$) determine the filter type. The general form $\text{Filt} = c_0(b_0 x + b_1 x_{t-1} + b_2 x_{t-2}) + a_1 \text{Filt}_{t-1} + a_2 \text{Filt}_{t-2}$ can instantiate nine different filters by selecting the appropriate coefficient derivation. Published by John Ehlers in "Swiss Army Knife Indicator" (*Technical Analysis of Stocks & Commodities*, January 2006).

## Historical Context

John F. Ehlers published the Swiss Army Knife indicator in TASC (January 2006), motivated by the observation that most common technical analysis filters (EMA, SMA, Gaussian, Butterworth, high-pass, band-pass) share the same second-order difference equation structure. Only the coefficients differ. By parameterizing the coefficient derivation, a single implementation can serve as any of nine filter types.

This unification has both practical and theoretical value. Practically, it reduces code duplication: one function with a mode selector replaces nine separate implementations. Theoretically, it reveals the deep connection between seemingly different filters: they are all members of the same family of second-order IIR filters, differing only in their pole and zero placements in the z-plane.

Ehlers derives the coefficients from the cycle period $P$ using trigonometric formulas that place poles/zeros at specific frequencies, ensuring each filter type has its cutoff or center frequency aligned with the user-specified period.

## Architecture & Physics

### 1. Unified Second-Order IIR

$$
\text{Filt}_t = c_0(b_0 x_t + b_1 x_{t-1} + b_2 x_{t-2}) + a_1 \text{Filt}_{t-1} + a_2 \text{Filt}_{t-2}
$$

### 2. Coefficient Derivation by Mode

Three smoothing parameters are computed from the period:
- **EMA/HP/SMA/Smooth modes:** $\alpha = (\cos\theta + \sin\theta - 1)/\cos\theta$, $\theta = 2\pi/P$
- **Gauss/Butter/2PHP modes:** $\beta = 2.415(1 - \cos\theta)$, $\alpha = -\beta + \sqrt{\beta^2 + 2\beta}$
- **BP/BS modes:** $\gamma = 1/\cos(2\pi\delta/P)$, $\beta = \cos(2\pi/P)$, $\alpha = \gamma - \sqrt{\gamma^2 - 1}$

### 3. Nine Filter Types

| Mode | Type | Overlay? |
| :--- | :--- | :---: |
| EMA | Low-pass (1-pole) | Yes |
| SMA | Low-pass (running sum) | Yes |
| Gauss | Low-pass (2-pole Gaussian) | Yes |
| Butter | Low-pass (2-pole Butterworth) | Yes |
| Smooth | Low-pass (FIR-like) | Yes |
| HP | High-pass (1-pole) | No |
| 2PHP | High-pass (2-pole) | No |
| BP | Band-pass | No |
| BS | Band-stop (notch) | No |

## Mathematical Foundation

**Unified transfer function (z-domain):**

$$
H(z) = \frac{c_0(b_0 + b_1 z^{-1} + b_2 z^{-2})}{1 - a_1 z^{-1} - a_2 z^{-2}}
$$

**Coefficient table:**

| Mode | $c_0$ | $b_0$ | $b_1$ | $b_2$ | $a_1$ | $a_2$ |
| :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| EMA | 1 | $\alpha$ | 0 | 0 | $1-\alpha$ | 0 |
| SMA | $1/n$ | 1 | 0 | 0 | 1 | 0 |
| Gauss | $\alpha^2$ | 1 | 0 | 0 | $2(1-\alpha)$ | $-(1-\alpha)^2$ |
| Butter | $\alpha^2/4$ | 1 | 2 | 1 | $2(1-\alpha)$ | $-(1-\alpha)^2$ |
| Smooth | $\alpha^2/4$ | 1 | 2 | 1 | 0 | 0 |
| HP | $1-\alpha/2$ | 1 | $-1$ | 0 | $1-\alpha$ | 0 |
| 2PHP | $(1-\alpha/2)^2$ | 1 | $-2$ | 1 | $2(1-\alpha)$ | $-(1-\alpha)^2$ |
| BP | $(1-\alpha)/2$ | 1 | 0 | $-1$ | $\beta(1+\alpha)$ | $-\alpha$ |
| BS | $(1+\alpha)/2$ | 1 | $-2\beta$ | 1 | $\beta(1+\alpha)$ | $-\alpha$ |

**SMA special path:** Uses $\text{Filt} = \frac{1}{n}x_t + \text{Filt}_{t-1} - \frac{1}{n}x_{t-n}$ (running sum).

**Stability:** All modes produce stable filters for $P > 2$. The Gauss and Butter modes have conjugate poles inside the unit circle; BP/BS modes have poles on the real axis for the specified bandwidth.

**Default parameters:** `filterType = "BP"`, `period = 20`, `n = 10` (SMA only), `delta = 0.1` (BP/BS), `minPeriod = 2`.

**Pseudo-code (streaming):**

```
// Compute alpha, beta, gamma from period and mode
[alpha, beta, gamma] = derive_params(filterType, period, delta)

// Select coefficients by mode
[c0, b0, b1, b2, a1, a2] = select_coeffs(filterType, alpha, beta, gamma, n)

// Apply unified 2nd-order IIR
if filterType == "SMA":
    result = (1/n)*src + result[1] - (1/n)*src[n]
else:
    result = c0*(b0*src + b1*src[1] + b2*src[2]) + a1*result[1] + a2*result[2]
```

## Resources

- Ehlers, J.F. (2006). "Swiss Army Knife Indicator." *Technical Analysis of Stocks & Commodities*, January 2006.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapters 3-4: IIR and FIR filter design.
- Ehlers, J.F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley. Chapter 2: Filters.
