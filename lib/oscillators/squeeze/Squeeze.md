# SQUEEZE: Squeeze Momentum

> *Squeeze momentum detects compression inside Bollinger-Keltner overlap and then measures the explosive release when bands expand.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20), `bbMult` (default 2.0), `kcMult` (default 1.5)                      |
| **Outputs**      | Single series (Squeeze)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [squeeze.pine](squeeze.pine)                       |

- Squeeze Momentum combines Bollinger Band and Keltner Channel width analysis to detect low-volatility compression ("squeeze") states, while simultan...
- Parameterized by `period` (default 20), `bbmult` (default 2.0), `kcmult` (default 1.5).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Squeeze Momentum combines Bollinger Band and Keltner Channel width analysis to detect low-volatility compression ("squeeze") states, while simultaneously measuring directional momentum via linear regression of a detrended price series. The dual output consists of a momentum histogram and a binary squeeze state indicator. When Bollinger Bands contract inside the Keltner Channel, the market is in a squeeze (coiling volatility); when the squeeze releases, the momentum histogram direction signals the likely breakout direction. The implementation combines five distinct computational stages, each using O(1) streaming techniques.

## Historical Context

John Carter popularized the Squeeze indicator in his 2005 book *Mastering the Trade*, though the core concept of BB-inside-KC squeeze detection predates his work. The underlying principle is that volatility is mean-reverting: periods of unusually low volatility (measured by BB width falling below KC width) tend to precede large directional moves. Carter combined this squeeze detection with a momentum component derived from linear regression to provide directional bias. The specific construction uses the midpoint of a Donchian Channel averaged with SMA as a center line, computes the deviation of price from this averaged midpoint, and applies linear regression to this deviation series. The regression endpoint value serves as the momentum measure. This construction effectively measures detrended momentum, isolating the directional force from the trend component. The color-coded histogram (traditionally four colors based on momentum direction and acceleration) provides visual distinction between momentum increasing and decreasing in both directions.

## Architecture & Physics

### Five Computational Stages

1. **SMA + Standard Deviation** (Bollinger Bands): Circular buffer with running sum and sum-of-squares for O(1) variance computation. BB upper/lower = SMA $\pm$ bbMult $\times$ StdDev.

2. **EMA + ATR via RMA** (Keltner Channel): EMA uses warmup-compensated exponential smoothing. ATR uses Wilder's RMA (also warmup-compensated) of True Range. KC upper/lower = EMA $\pm$ kcMult $\times$ ATR.

3. **Squeeze detection:** Binary comparison: if BB upper < KC upper AND BB lower > KC lower, squeeze is on. This means BB has contracted inside KC.

4. **Donchian midline + delta:** Circular buffers for highest-high and lowest-low over the period, with full O(n) scan per bar for max/min (no O(1) trick for running max). Delta = close $-$ (donchianMid + SMA) / 2.

5. **Linear regression of delta:** Incremental running sums ($\Sigma y$, $\Sigma xy$) for O(1) regression per bar. The momentum output is the regression line evaluated at the most recent point: $\text{slope} \times (n-1) + \text{intercept}$.

### Warmup Compensation

EMA and RMA stages use the $e = \beta^n$ warmup tracking with correction factor $c = 1/(1-e)$ to eliminate initial bias.

## Mathematical Foundation

**Bollinger Bands** (SMA + StdDev via running sums):

$$\mu = \frac{\Sigma x}{n}, \quad \sigma = \sqrt{\frac{\Sigma x^2}{n} - \mu^2}$$

$$BB_{upper} = \mu + m_{bb} \cdot \sigma, \quad BB_{lower} = \mu - m_{bb} \cdot \sigma$$

**Keltner Channel** (EMA + ATR):

$$EMA_t = \frac{\hat{E}_t}{1 - \beta^t}, \quad ATR_t = \frac{\hat{R}_t}{1 - \beta_r^t}$$

$$KC_{upper} = EMA + m_{kc} \cdot ATR, \quad KC_{lower} = EMA - m_{kc} \cdot ATR$$

**Squeeze state:**

$$Squeeze = \begin{cases} 1 & \text{if } BB_{upper} < KC_{upper} \text{ and } BB_{lower} > KC_{lower} \\ 0 & \text{otherwise} \end{cases}$$

**Detrended price (delta):**

$$\delta_t = x_t - \frac{(\text{DonchianMid}_t + \text{SMA}_t)}{2}$$

where $\text{DonchianMid} = \frac{\max(H_{t-n+1..t}) + \min(L_{t-n+1..t})}{2}$

**Momentum (linear regression endpoint of delta):**

$$m = \frac{n \cdot \Sigma_{xy} - \Sigma_x \cdot \Sigma_y}{n \cdot \Sigma_{x^2} - \Sigma_x^2}, \quad b = \frac{\Sigma_y - m \cdot \Sigma_x}{n}$$

$$Momentum_t = m \cdot (t_{\text{last}}) + b$$

**Default parameters:** period = 20, bbMult = 2.0, kcMult = 1.5.

## Performance Profile

### Operation Count (Streaming Mode)

Squeeze Momentum Indicator uses Bollinger Bands, Keltner Channels, and a momentum oscillator based on linear regression delta.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| TR + RMA ATR | 8 | 4 | 32 |
| EMA (KC middle) | 1 | 4 | 4 |
| KC bands (EMA ± ATR×multiplier) | 4 | 3 | 12 |
| SMA variance (O(N) scan) | N+2 | 1 | N+2 |
| SQRT (BB stddev) | 1 | 20 | 20 |
| BB bands (SMA ± k×stddev) | 4 | 3 | 12 |
| CMP × 2 (BB inside KC?) | 2 | 1 | 2 |
| LR oscillator (O(N) scan) | ~3N | 3 | ~3N |
| **Total** | **~4N+22** | — | **~4N+84** |

For default $N=20$: ~164 cycles per bar. O(N) variance + O(N) regression dominate.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| ATR (RMA) | **No** | Recursive IIR |
| BB computation | Yes | Prefix-sum variance; VADDPD/VMULPD/VSQRTPD |
| KC (EMA) | **No** | Recursive IIR |
| LR momentum | Yes | Prefix-sum regression trick; VFMADD |
| Squeeze detection | Yes | VCMPPD |

Mixed: the two IIR passes are sequential; everything else is vectorizable.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | LR oscillator is high-fidelity momentum measure |
| **Timeliness** | 5/10 | N-bar windows on all components add inherent lag |
| **Smoothness** | 8/10 | LR oscillator + squeeze binary filter produces clean output |
| **Noise Rejection** | 8/10 | Dual-channel squeeze gate prevents false momentum signals |

## Resources

- Carter, J. (2005). *Mastering the Trade*. McGraw-Hill, Chapter 11
- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill
- PineScript reference: [`squeeze.pine`](squeeze.pine)
