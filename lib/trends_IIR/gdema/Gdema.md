# GDEMA: Generalized Double Exponential Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 10), `vfactor` (default 1.0)                      |
| **Outputs**      | Single series (Gdema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [gdema.pine](gdema.pine)                       |
| **Signature**    | [gdema_signature](gdema_signature.md) |

- GDEMA extends the standard DEMA (Double Exponential Moving Average) with a tunable gain factor $v$ that controls the aggressiveness of lag compensa...
- Parameterized by `period` (default 10), `vfactor` (default 1.0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Patrick Mulloy created DEMA to cancel first-order lag. GDEMA adds a volume knob: turn it past 1 and you cancel more lag than Mulloy thought possible. Turn it to 0 and you are back to a plain EMA. The generalization is the point."

GDEMA extends the standard DEMA (Double Exponential Moving Average) with a tunable gain factor $v$ that controls the aggressiveness of lag compensation. The formula $\text{GDEMA} = (1+v) \cdot \text{EMA}_1 - v \cdot \text{EMA}_2$ reduces to plain EMA when $v=0$, standard DEMA when $v=1$, and progressively more aggressive lag removal for $v>1$. This parametric flexibility allows traders to dial in the exact smoothness-responsiveness trade-off for their application, rather than being locked into DEMA's fixed 2:1 ratio.

## Historical Context

Patrick G. Mulloy published DEMA in "Smoothing Data with Faster Moving Averages" (*Technical Analysis of Stocks & Commodities*, February 1994). The original DEMA uses the fixed formula $2 \cdot \text{EMA} - \text{EMA}(\text{EMA})$, which cancels the first-order lag of the EMA by subtracting the double-smoothed version.

The generalization to an arbitrary volume factor $v$ is a natural extension that was explored by several authors in the late 1990s. Tim Tillson's T3 indicator (1998) uses a similar parameterized approach with six cascaded EMAs and a volume factor. GDEMA is the simplest member of this family: two cascaded EMAs combined with a single gain parameter.

The mathematical basis is the z-transform lag cancellation technique: EMA has a group delay of approximately $(N-1)/2$ samples. EMA(EMA) has approximately double that delay. The linear combination $(1+v) \cdot \text{EMA} - v \cdot \text{EMA}(\text{EMA})$ cancels $v/(v+1)$ of the total lag. At $v=1$ (DEMA), half the lag is cancelled. At $v=2$, two-thirds is cancelled, but overshoot increases proportionally.

## Architecture & Physics

### 1. Dual Cascaded EMAs

Two EMA stages share the same period $N$ and smoothing constant $\alpha = 2/(N+1)$:
- **EMA1:** Standard EMA of the source.
- **EMA2:** EMA of EMA1 (double-smoothed).

### 2. Warmup Compensation

Both EMAs use the exponential warmup compensator $c = 1/(1-\beta^n)$ to produce valid output from bar 1, eliminating the cold-start bias.

### 3. Parameterized Combination

$$
\text{GDEMA} = (1+v) \cdot \text{EMA}_1 - v \cdot \text{EMA}_2
$$

## Mathematical Foundation

Given smoothing constant $\alpha = 2/(N+1)$, decay $\beta = 1-\alpha$:

$$
\text{EMA}_1[t] = \alpha \cdot x_t + \beta \cdot \text{EMA}_1[t-1]
$$

$$
\text{EMA}_2[t] = \alpha \cdot \text{EMA}_1[t] + \beta \cdot \text{EMA}_2[t-1]
$$

$$
\text{GDEMA}[t] = (1+v) \cdot \text{EMA}_1[t] - v \cdot \text{EMA}_2[t]
$$

**Z-domain transfer function:**

$$
H(z) = (1+v) \cdot \frac{\alpha}{1-\beta z^{-1}} - v \cdot \left(\frac{\alpha}{1-\beta z^{-1}}\right)^2
$$

**Lag characteristics:**

| $v$ | Equivalent | Lag reduction | Overshoot risk |
| :---: | :--- | :---: | :---: |
| 0 | EMA | 0% | None |
| 0.5 | Mild DEMA | 33% | Low |
| 1.0 | Standard DEMA | 50% | Moderate |
| 1.5 | Aggressive | 60% | High |
| 2.0 | Very aggressive | 67% | Very high |

**Default parameters:** `period = 10`, `vfactor = 1.0`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
alpha = 2 / (period + 1); beta = 1 - alpha

// EMA1 with warmup
ema1_raw = alpha * (source - ema1_raw) + ema1_raw
e *= beta
comp = 1 / (1 - e)
ema1 = ema1_raw * comp

// EMA2 with warmup (of compensated EMA1)
ema2_raw = alpha * (ema1 - ema2_raw) + ema2_raw
ema2 = ema2_raw * comp

// Generalized combination
return (1 + v) * ema1 - v * ema2
```

## Resources

- Mulloy, P.G. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1), 11-19.
- Tillson, T. (1998). "Smoothing Techniques for More Accurate Signals." *Technical Analysis of Stocks & Commodities*, 16(1).
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 3: Smoothing Filters.

## Performance Profile

### Operation Count (Streaming Mode)

GDEMA(N, v) runs two cascaded EMA stages. The output is `(1+v)×EMA₁ - v×EMA₂` — a linear combination with precomputed coefficient `_onePlusV`. Both EMAs use bias-compensated warmup (E factor).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| EMA₁: FMA(α, src, decay×ema1) | 1 | 4 | ~4 |
| Bias factor update E₁ | 1 | 3 | ~3 |
| EMA₂: FMA(α, ema1, decay×ema2) | 1 | 4 | ~4 |
| Bias factor update E₂ | 1 | 3 | ~3 |
| Output: FMA(onePlusV, ema1, −v×ema2) | 1 | 4 | ~4 |
| **Total** | **5** | — | **~18 cycles** |

O(1) per bar. Two FMAs for EMA stages, one FMA for the combination. Fastest of the multi-stage EMA indicators. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| EMA₁ pass | No | Recursive IIR |
| EMA₂ pass (depends on EMA₁ output) | No | Sequential dependency on EMA₁ series |
| Output combination (1+v)×E1 − v×E2 | Yes | `VFNMADD231PD` across bar series once EMA passes complete |

Both EMA passes are recursive IIR. The final linear combination is vectorizable after the two EMA sweeps. Net batch speedup: minimal (~1.1×) since combination is only 3 of 18 cycles.
