# NYQMA: Nyquist Moving Average

> "Manfred Dürschner applied the Nyquist-Shannon sampling theorem to cascaded moving averages: the second smoothing period must not exceed half the first, or you get aliasing artifacts. Respect the theorem and the ghost signals disappear."

NYQMA combines a primary LWMA (Linear Weighted Moving Average) with a secondary LWMA applied to the first, using lag-compensating extrapolation: $\text{NYQMA} = (1+\alpha) \cdot \text{MA}_1 - \alpha \cdot \text{MA}_2$, where $\alpha = N_2 / (N_1 - N_2)$. The Nyquist constraint $N_2 \leq \lfloor N_1/2 \rfloor$ ensures the second smoothing does not introduce aliasing artifacts ("ghost signals") into the output. This produces a lag-reduced moving average grounded in sampling theory rather than ad-hoc coefficient tuning.

## Historical Context

Dr. Manfred G. Dürschner published NYQMA in *Gleitende Durchschnitte 3.0* ("Moving Averages 3.0"), a German-language work that applies rigorous signal-processing theory to financial moving average design. Dürschner's key insight was that cascading two smoothing operations is mathematically equivalent to sampling a continuous signal at two rates, and the Nyquist-Shannon sampling theorem dictates that the second rate cannot exceed half the first without introducing aliasing.

The Nyquist-Shannon theorem (1949) states that a signal must be sampled at more than twice its highest frequency to avoid aliasing. In the context of cascaded MAs, the "sampling rate" analogy maps to the smoothing period: a primary MA with period $N_1$ has an effective frequency cutoff, and the secondary MA with period $N_2$ must have a cutoff at no more than half that frequency (i.e., $N_2 \leq N_1/2$) to avoid passing through frequency components that the first MA was designed to suppress.

The lag compensation formula $(1+\alpha) \cdot \text{MA}_1 - \alpha \cdot \text{MA}_2$ is structurally identical to DEMA and GDEMA, but with the critical distinction that both MAs are LWMAs (not EMAs) and the gain factor $\alpha$ is derived from the period ratio rather than being a free parameter.

## Architecture & Physics

### 1. Primary LWMA

A standard Linear Weighted Moving Average with period $N_1$:

$$
\text{MA}_1 = \text{WMA}(x, N_1)
$$

### 2. Secondary LWMA

A LWMA applied to $\text{MA}_1$ with Nyquist-constrained period $N_2 \leq \lfloor N_1/2 \rfloor$:

$$
\text{MA}_2 = \text{WMA}(\text{MA}_1, N_2)
$$

### 3. Lag-Compensating Extrapolation

$$
\alpha = \frac{N_2}{N_1 - N_2}
$$

$$
\text{NYQMA} = (1 + \alpha) \cdot \text{MA}_1 - \alpha \cdot \text{MA}_2
$$

### 4. Nyquist Enforcement

The implementation clamps $N_2 = \min(N_2, \lfloor N_1/2 \rfloor)$ to enforce the sampling constraint.

## Mathematical Foundation

**LWMA (period $N$):**

$$
\text{WMA}(x, N) = \frac{\sum_{i=0}^{N-1} (N-i) \cdot x_{t-i}}{\sum_{i=0}^{N-1} (N-i)} = \frac{\sum_{i=0}^{N-1} (N-i) \cdot x_{t-i}}{N(N+1)/2}
$$

**Lag compensation coefficient:**

$$
\alpha = \frac{N_2}{N_1 - N_2}
$$

**Output:**

$$
\text{NYQMA} = (1 + \alpha) \cdot \text{WMA}(x, N_1) - \alpha \cdot \text{WMA}(\text{WMA}(x, N_1), N_2)
$$

**Nyquist constraint (hard rule):**

$$
N_2 \leq \left\lfloor \frac{N_1}{2} \right\rfloor
$$

**Lag analysis:** WMA has group delay $(N-1)/3$. The extrapolation compensates a fraction $\alpha/(1+\alpha) = N_2/N_1$ of the primary MA's lag.

**Default parameters:** `period = 89` ($N_1$), `nyquist_period = 21` ($N_2$), `minPeriod = 2`.

**Pseudo-code (streaming):**

```
n2 = min(nyquist_period, period / 2)  // enforce Nyquist
ma1 = WMA(src, period)
ma2 = WMA(ma1, n2)
alpha = n2 / (period - n2)
return (1 + alpha) * ma1 - alpha * ma2
```

## Resources

- Dürschner, M.G. *Gleitende Durchschnitte 3.0*. (Original NYQMA publication, German language.)
- Shannon, C.E. (1949). "Communication in the Presence of Noise." *Proceedings of the IRE*, 37(1), 10-21.
- Nyquist, H. (1928). "Certain Topics in Telegraph Transmission Theory." *Transactions of the AIEE*, 47(2), 617-644.
