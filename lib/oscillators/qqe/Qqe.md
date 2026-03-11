# QQE: Quantitative Qualitative Estimation

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `rsiPeriod` (default 14), `smoothFactor` (default 5), `qqeFactor` (default 4.236)                      |
| **Outputs**      | Single series (Qqe)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `rsiPeriod + smoothFactor + darPeriod * 2` bars                          |
| **PineScript**   | [qqe.pine](qqe.pine)                       |

- Quantitative Qualitative Estimation applies a multi-stage smoothing pipeline to RSI and then constructs dynamic volatility-based trailing bands aro...
- Parameterized by `rsiPeriod` (default 14), `smoothFactor` (default 5), `qqeFactor` (default 4.236).
- Output range: Varies (see docs).
- Requires `rsiPeriod + smoothFactor + darPeriod * 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Quantitative Qualitative Estimation applies a multi-stage smoothing pipeline to RSI and then constructs dynamic volatility-based trailing bands around the smoothed result. The output is a dual-line system: the QQE line (smoothed RSI) and a trailing level that follows price directionally, similar to Parabolic SAR logic. Crossovers between the QQE line and its trailing level signal momentum shifts, while crossovers of the QQE line above and below 50 indicate trend direction. The trailing level adapts to volatility through a double-EMA of RSI absolute changes, making band width contract in quiet markets and expand during volatile conditions.

## Historical Context

QQE emerged from the forex trading community in the mid-2000s, attributed to an anonymous developer and popularized through MetaTrader forums. The indicator extends Wilder's RSI concept by addressing two of its primary limitations: noise in the RSI signal and fixed overbought/oversold thresholds. The first problem is solved by EMA smoothing of the RSI output; the second by replacing static thresholds with adaptive trailing bands derived from RSI volatility. The "Quantitative Qualitative" name reflects the dual nature of the system: the quantitative RSI measurement combined with qualitative trend-following logic in the trailing level. The trailing level mechanism borrows from Welles Wilder's Parabolic SAR: it follows the smoothed RSI directionally, only reversing when the RSI breaks through. The default QQE factor of 4.236 (the square of the golden ratio $\phi^2 = 2.618... \times 1.618...$) has no documented mathematical justification but has become canonical through widespread adoption.

## Architecture & Physics

### Four-Stage Pipeline

1. **Stage 1: Wilder RSI** via RMA ($\alpha = 1/\text{rsiPeriod}$) with warmup compensation. The exponential decay factor $e = \beta^n$ tracks convergence, applying correction $c = 1/(1-e)$ until $e < 10^{-10}$.

2. **Stage 2: EMA smoothing** of RSI ($\alpha = 2/(\text{SF}+1)$) with the same warmup compensation. Produces `rsiMA`, the primary QQE line.

3. **Stage 3: Dynamic Average Range (DAR).** Computes $|\Delta \text{rsiMA}|$ bar-to-bar, then applies two consecutive EMAs with period $2 \times \text{SF} - 1$. Both EMAs use warmup compensation. The double smoothing produces a stable volatility estimate analogous to ATR but operating on the RSI domain.

4. **Stage 4: Trailing level.** Constructs upper/lower bands at $\text{rsiMA} \pm \text{qqeFactor} \times \text{DAR}$. The trailing logic follows directionally:
   - If rsiMA is above the trail and was above previously: trail = max(trail, lowerBand) (ratchets up)
   - If rsiMA is below the trail and was below previously: trail = min(trail, upperBand) (ratchets down)
   - On crossover: trail flips to the opposite band

## Mathematical Foundation

**Stage 1: RSI** ($\alpha_r = 1/p_r$, $\beta_r = 1 - \alpha_r$):

$$\hat{G}_t = \beta_r \hat{G}_{t-1} + \alpha_r \max(\Delta x_t, 0), \quad e_r = \beta_r^t$$

$$RSI_t = \frac{100 \cdot \hat{G}_t / (1-e_r)}{\hat{G}_t/(1-e_r) + \hat{L}_t/(1-e_r)}$$

**Stage 2: EMA of RSI** ($\alpha_s = 2/(SF+1)$):

$$\hat{M}_t = \beta_s \hat{M}_{t-1} + \alpha_s \cdot RSI_t, \quad rsiMA_t = \hat{M}_t / (1 - \beta_s^t)$$

**Stage 3: Double EMA of |delta|** ($\alpha_d = 2/(2 \cdot SF)$):

$$D_t = |rsiMA_t - rsiMA_{t-1}|$$

$$\hat{d}_1 = \beta_d \hat{d}_1 + \alpha_d D_t, \quad dar_1 = \hat{d}_1 / (1 - \beta_d^t)$$

$$\hat{d}_2 = \beta_d \hat{d}_2 + \alpha_d \cdot dar_1, \quad DAR_t = \hat{d}_2 / (1 - \beta_d^t)$$

**Stage 4: Trailing level:**

```text
band = qqeFactor × DAR
upper = rsiMA + band
lower = rsiMA - band

if rsiMA > trail AND prev_rsiMA > trail:
    trail = max(trail, lower)
elif rsiMA < trail AND prev_rsiMA < trail:
    trail = min(trail, upper)
else:
    trail = (rsiMA > trail) ? lower : upper
```

**Default parameters:** rsiPeriod = 14, smoothFactor = 5, qqeFactor = 4.236.

## Performance Profile

### Operation Count (Streaming Mode)

QQE applies a Wilder RSI, then smooths the RSI with two layers of EMA, and computes an ATR-derived trailing stop band.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RSI Wilder EMA × 2 (AvgU, AvgD) | 2 | 4 | 8 |
| RSI division + scaling | 2 | 16 | 32 |
| EMA of RSI (smoothing pass 1) | 1 | 4 | 4 |
| EMA of EMA-RSI (smoothing pass 2) | 1 | 4 | 4 |
| ABS (RSI delta) | 1 | 1 | 1 |
| EMA of ABS-delta (ATR proxy) | 1 | 4 | 4 |
| MUL (factor × ATR proxy = QQE band) | 1 | 3 | 3 |
| Trailing stop ratchet (MAX/MIN + CMP) | 4 | 1 | 4 |
| **Total** | **13** | — | **~60 cycles** |

Six EMA instances + one ratchet. ~60 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| All EMA/RMA passes × 6 | **No** | Recursive IIR — sequential |
| RSI division | Yes | VDIVPD after EMA pass |
| Band arithmetic | Yes | VMULPD + VADDPD/VSUBPD |
| Ratchet state | **No** | State-dependent MAX/MIN |

Recursive EMA chains and ratchet state block vectorization. Band arithmetic is vectorizable post-EMA.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Triple EMA filtering produces stable output |
| **Timeliness** | 4/10 | Three sequential smoothing passes add significant lag |
| **Smoothness** | 9/10 | Triple-smoothed RSI is one of the smoothest oscillators |
| **Noise Rejection** | 9/10 | ATR-derived adaptive band suppresses whipsaws effectively |

## Resources

- Wilder, J.W. (1978). *New Concepts in Technical Trading Systems*. Trend Research (RSI foundation)
- MetaTrader community documentation on QQE implementation
- PineScript reference: [`qqe.pine`](qqe.pine)
