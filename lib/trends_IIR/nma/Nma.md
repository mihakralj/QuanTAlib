# NMA: Natural Moving Average

> "Jim Sloman looked at how volatility distributes across a window and asked: if the most volatile bars are recent, should the filter not respond faster? NMA derives its smoothing constant from the volatility profile itself, weighted by a square-root kernel that emphasizes recent action."

NMA is an adaptive IIR filter whose smoothing ratio is derived from a volatility-weighted square-root kernel analysis of log-price movements over a lookback window. When volatility concentrates in recent bars, the ratio approaches 1.0 (fast tracking). When volatility is spread uniformly, the ratio approaches $1/\sqrt{N}$ (heavy smoothing). The square-root kernel $(\sqrt{i+1} - \sqrt{i})$ gives a concave-down weighting that gently emphasizes recency, while the log-price transformation normalizes for price level, making the adaptation scale-invariant.

## Historical Context

Jim Sloman introduced the Natural Moving Average in *Ocean Theory* (pages 63-70), a book that applied chaos and complexity theory metaphors to financial markets. The NMA was designed as a "natural" filter that lets the market's own volatility structure determine the smoothing rate, rather than imposing an arbitrary period.

The core innovation is the square-root differencing kernel $\sqrt{i+1} - \sqrt{i}$ as the weighting function for volatility. This kernel has the property that its cumulative sum $\sqrt{N}$ grows sublinearly, meaning each additional bar in the lookback contributes less weight than the previous one. This creates a "diminishing returns" effect: extending the lookback adds context without drowning out recent information.

The log-price transformation ($\ln(\text{price}) \times 1000$) serves two purposes: (1) it makes the volatility measure proportional to percentage moves rather than absolute dollar moves, and (2) the scaling factor of 1000 brings typical values into a numerically convenient range for the ratio computation.

NMA belongs to the family of adaptive moving averages alongside KAMA, VIDYA, and ADXVMA, but uses a unique adaptation mechanism based on the spatial distribution of volatility rather than a single efficiency or strength metric.

## Architecture & Physics

### 1. Log-Price Buffer

A circular buffer of size $N+1$ stores $\ln(\text{price}) \times 1000$ for each bar, providing the lookback data for volatility computation.

### 2. Volatility-Weighted Square-Root Ratio

For each bar $i$ in the lookback:

$$
o_i = |\ln_i - \ln_{i+1}|
$$

$$
\text{num} = \sum_{i=0}^{N-1} o_i \cdot \left(\sqrt{i+1} - \sqrt{i}\right)
$$

$$
\text{denom} = \sum_{i=0}^{N-1} o_i
$$

$$
\text{ratio} = \frac{\text{num}}{\text{denom}}
$$

### 3. Adaptive EMA Step

$$
\text{NMA}_t = \text{NMA}_{t-1} + \text{ratio} \times (x_t - \text{NMA}_{t-1})
$$

## Mathematical Foundation

**Log-price volatility:**

$$
o_i = \left|\ln(x_{t-i}) - \ln(x_{t-i-1})\right| \times 1000
$$

**Square-root kernel weights:**

$$
\phi_i = \sqrt{i+1} - \sqrt{i} = \frac{1}{\sqrt{i+1} + \sqrt{i}}
$$

Note: $\phi_i \approx \frac{1}{2\sqrt{i}}$ for large $i$, confirming the $1/\sqrt{i}$ decay rate.

**Adaptive ratio:**

$$
r = \frac{\sum_{i=0}^{N-1} o_i \cdot \phi_i}{\sum_{i=0}^{N-1} o_i}
$$

**Ratio bounds:**

- If all volatility is at $i = 0$ (most recent): $r = \phi_0 = \sqrt{1} - \sqrt{0} = 1$
- If volatility is uniform: $r = \frac{\sum \phi_i}{N} = \frac{\sqrt{N}}{N} = \frac{1}{\sqrt{N}}$
- For $N = 40$: uniform ratio $\approx 0.158$, equivalent to EMA period $\approx 11$

**IIR update:**

$$
\text{NMA}_t = \text{NMA}_{t-1} + r_t \cdot (x_t - \text{NMA}_{t-1})
$$

**Default parameters:** `period = 40`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
// Store scaled log-price
lnBuf[head] = log(src) * 1000

// Compute volatility-weighted ratio
num = 0; denom = 0
for i = 0 to bars-1:
    oi = |lnBuf[t-i] - lnBuf[t-i-1]|
    num   += oi * (sqrt(i+1) - sqrt(i))
    denom += oi

ratio = denom != 0 ? num/denom : 0

// Adaptive EMA step
result = result + ratio * (src - result)
```

## Resources

- Sloman, J. *Ocean Theory*. Pages 63-70. (Original NMA description.)
- Kaufman, P.J. (2013). *Trading Systems and Methods*, 5th ed. Wiley. Chapter 7: Adaptive Moving Averages.
- Chande, T.S. & Kroll, S. (1994). *The New Technical Trader*. Wiley. (Adaptive filter framework.)
