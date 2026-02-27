# KST: Know Sure Thing Oscillator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `r1` (default DefaultR1), `r2` (default DefaultR2), `r3` (default DefaultR3), `r4` (default DefaultR4), `s1` (default DefaultS1), `s2` (default DefaultS2), `s3` (default DefaultS3), `s4` (default DefaultS4), `sigPeriod` (default DefaultSigPeriod)                      |
| **Outputs**      | Multiple series (KstValue, Signal)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `Math.Max(Math.Max(r1, r2), Math.Max(r3, r4))
                     + Math.Max(Math.Max(s1, s2), Math.Max(s3, s4))
                     + sigPeriod - 2` bars                          |

### TL;DR

- The Know Sure Thing is a multi-timeframe momentum oscillator that computes four Rate of Change values at progressively longer lookback periods, smo...
- Parameterized by `r1` (default defaultr1), `r2` (default defaultr2), `r3` (default defaultr3), `r4` (default defaultr4), `s1` (default defaults1), `s2` (default defaults2), `s3` (default defaults3), `s4` (default defaults4), `sigperiod` (default defaultsigperiod).
- Output range: Varies (see docs).
- Requires `Math.Max(Math.Max(r1, r2), Math.Max(r3, r4))
                     + Math.Max(Math.Max(s1, s2), Math.Max(s3, s4))
                     + sigPeriod - 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Know Sure Thing is a multi-timeframe momentum oscillator that computes four Rate of Change values at progressively longer lookback periods, smooths each with an independent SMA, then combines them using linearly increasing weights (1, 2, 3, 4) to produce a single composite momentum line. A signal line (SMA of the KST) provides crossover triggers. The weighted summation ensures longer-term momentum dominates the output while shorter-term components contribute responsiveness, creating a momentum indicator that reflects multiple cycle lengths simultaneously.

## Historical Context

Martin Pring developed the KST oscillator in the early 1990s, publishing it in *Technical Analysis of Stocks & Commodities* and later in his comprehensive work on technical analysis. Pring's motivation was to create a single indicator that captured momentum across multiple timeframes, eliminating the need to monitor four separate ROC charts. The name "Know Sure Thing" was somewhat tongue-in-cheek, acknowledging that no indicator provides certainty, but reflecting Pring's confidence that multi-timeframe momentum confirmation produces more reliable signals than any single timeframe. The original design used monthly data with ROC periods of 9, 12, 18, 24 months and SMA periods of 6, 6, 6, 9 months, later adapted to daily timeframes using proportionally scaled periods. The linearly increasing weights (1:2:3:4) were chosen to give progressively more influence to longer-term momentum, reflecting the principle that major market trends are driven by longer-term forces while shorter-term momentum primarily adds noise.

## Architecture & Physics

### Parallel ROC + SMA Pipeline

KST maintains four independent processing channels, each consisting of:

1. **ROC calculation:** $ROC_k = \frac{x_t - x_{t-r_k}}{x_{t-r_k}} \times 100$ for lookback periods $r_1 < r_2 < r_3 < r_4$.

2. **SMA smoothing:** Each ROC is smoothed by an independent SMA with its own circular buffer and running sum, achieving O(1) per bar. The SMA helper function encapsulates buffer management, head pointer, count, and running sum.

### Weighted Combination

The four smoothed ROC values are combined with fixed linear weights:

$$KST = 1 \times SMA(ROC_1) + 2 \times SMA(ROC_2) + 3 \times SMA(ROC_3) + 4 \times SMA(ROC_4)$$

### Signal Line

A fifth SMA is applied to the KST output, using its own circular buffer. Crossovers between KST and signal indicate momentum shifts.

### Total Buffer Count

The implementation maintains 5 independent SMA circular buffers (4 ROC smoothers + 1 signal), each with its own metadata arrays. No ROC circular buffer is needed because PineScript's `source[n]` lookback provides direct access to historical prices.

## Mathematical Foundation

Given source $x_t$, ROC periods $(r_1, r_2, r_3, r_4)$, SMA periods $(s_1, s_2, s_3, s_4)$, signal period $p_s$:

**Rate of Change for each channel:**

$$ROC_k(t) = \frac{x_t - x_{t-r_k}}{x_{t-r_k}} \times 100, \quad k \in \{1,2,3,4\}$$

**SMA smoothing** (O(1) circular buffer per channel):

$$SM_k(t) = \frac{1}{s_k} \sum_{i=0}^{s_k-1} ROC_k(t-i)$$

**KST composite:**

$$KST(t) = 1 \cdot SM_1(t) + 2 \cdot SM_2(t) + 3 \cdot SM_3(t) + 4 \cdot SM_4(t)$$

**Signal line:**

$$Signal(t) = \frac{1}{p_s} \sum_{i=0}^{p_s-1} KST(t-i)$$

**Default parameters:** $r = (10, 15, 20, 30)$, $s = (10, 10, 10, 15)$, $p_s = 9$.

## Performance Profile

### Operation Count (Streaming Mode)

KST sums four weighted ROC values, each smoothed by an SMA. Four SMA instances + four ROC lookback buffers.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ROC × 4 (price/price[N]−1) with 4 ring buffers | 4 | 16 | 64 |
| SMA running sum × 4 (add + oldest sub) | 8 | 1 | 8 |
| MUL × 4 (1/N_sma for each SMA) | 4 | 3 | 12 |
| MUL × 4 (weight each RCMA) | 4 | 3 | 12 |
| ADD × 3 (sum four weighted RCMA) | 3 | 1 | 3 |
| Signal SMA update (add + oldest sub + 1/N) | 3 | 3 | 9 |
| **Total** | **26** | — | **~108 cycles** |

For default parameters (RCM1-4, signal SMA 9): ~108 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| ROC × 4 (lag-offset division) | Yes | VDIVPD on shifted arrays |
| SMA × 4 (prefix-sum windows) | Yes | VADDPD + VSUBPD subtract-lag |
| Weighted sum | Yes | VFMADD across 4 terms |
| Signal SMA | Yes | Same prefix-sum pattern |

Fully vectorizable in batch mode — no recursive dependencies. AVX2 achieves ~4× throughput.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Multiple period averaging reduces single-period bias |
| **Timeliness** | 4/10 | Longest ROC (30) + SMA (15) = 45 bars before full convergence |
| **Smoothness** | 8/10 | Quad-SMA smoothing produces very clean output |
| **Noise Rejection** | 8/10 | Four-period averaging with different weights is robust to outliers |

## Resources

- Pring, M.J. (1992). "The KST System." *Technical Analysis of Stocks & Commodities*
- Pring, M.J. (2002). *Technical Analysis Explained*, 4th ed. McGraw-Hill
- PineScript reference: [`kst.pine`](kst.pine)
