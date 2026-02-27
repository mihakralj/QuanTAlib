# COPPOCK: Coppock Curve

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `longRoc` (default DefaultLongRoc), `shortRoc` (default DefaultShortRoc), `wmaPeriod` (default DefaultWmaPeriod)                      |
| **Outputs**      | Single series (Coppock)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |

### TL;DR

- The Coppock Curve is a long-term momentum oscillator that applies a Weighted Moving Average to the sum of two Rate of Change calculations at differ...
- Parameterized by `longroc` (default defaultlongroc), `shortroc` (default defaultshortroc), `wmaperiod` (default defaultwmaperiod).
- Output range: Varies (see docs).
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Coppock Curve is a long-term momentum oscillator that applies a Weighted Moving Average to the sum of two Rate of Change calculations at different lookback periods. Originally designed for monthly charts to identify major market bottoms, it produces a single oscillating line where zero-line crossovers from below signal long-term buying opportunities. The dual-ROC architecture captures both intermediate and longer-term momentum dynamics in a single smoothed output.

## Historical Context

Edwin Sedgwick Coppock introduced this indicator in 1962 in *Barron's* magazine, originally calling it the "Trendex Model." Coppock, an economist by training, reportedly derived the 11-month and 14-month ROC periods from Episcopal clergy who told him the average mourning period for a bereavement was 11 to 14 months. He reasoned that market bottoms represented a similar psychological recovery period. The indicator was designed exclusively as a buy signal generator on monthly S&P 500 data, with zero-line crossovers from negative territory signaling major market lows. Later practitioners adapted it to weekly and daily timeframes with scaled parameters, though Coppock himself considered only the monthly application valid. The 10-period WMA smoothing was chosen to filter out intermediate noise while preserving the timing of major turning points.

## Architecture & Physics

### Three-Stage Pipeline

The Coppock Curve processes data through a sequential pipeline:

1. **ROC Stage:** Two independent Rate of Change calculations at different lookback periods extract momentum at two time horizons. Each ROC measures the percentage price change over its respective window: $\text{ROC}(n) = \frac{C_t - C_{t-n}}{C_{t-n}} \times 100$.

2. **Summation Stage:** The two ROC values are added directly, creating a composite momentum measure that captures both intermediate and long-term price velocity.

3. **WMA Stage:** A Weighted Moving Average smooths the combined ROC, using linearly increasing weights that emphasize recent composite momentum while suppressing noise. The WMA implementation uses the dual running sum technique for O(1) per-bar updates.

### Circular Buffer Design

The ROC stage stores historical prices in a circular buffer sized to the maximum of the two ROC periods. The WMA stage maintains its own circular buffer with running weighted and unweighted sums, enabling constant-time updates without recomputation.

## Mathematical Foundation

Given source series $x_t$, long ROC period $L$, short ROC period $S$, and WMA period $W$:

**Rate of Change:**

$$ROC_L(t) = \frac{x_t - x_{t-L}}{x_{t-L}} \times 100, \quad ROC_S(t) = \frac{x_t - x_{t-S}}{x_{t-S}} \times 100$$

**Combined ROC:**

$$R_t = ROC_L(t) + ROC_S(t)$$

**Weighted Moving Average of combined ROC:**

$$\text{Coppock}(t) = \frac{\sum_{i=0}^{W-1} (W - i) \cdot R_{t-i}}{\sum_{i=0}^{W-1} (W - i)}$$

The denominator equals $\frac{W(W+1)}{2}$.

**O(1) WMA streaming update** using dual running sums:

```text
On new value R entering buffer (oldest R_old exits):
  plainSum = plainSum - R_old + R
  weightedSum = weightedSum - plainSum_old + W × R
  norm = W × (W + 1) / 2
  Coppock = weightedSum / norm
```

**Default parameters:** longRoc = 14, shortRoc = 11, wmaPeriod = 10 (original monthly values).

## Performance Profile

### Operation Count (Streaming Mode)

Coppock Curve = WMA of (ROC(11) + ROC(14)). Both ROC values need ring buffers of depth 14; then weighted average of N WMA taps.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ROC × 2 (close[0]/close[N]−1 × 100) | 2 | 16 | 32 |
| ADD (ROC11 + ROC14) | 1 | 1 | 1 |
| WMA accumulation (N=10 weighted taps) | 10 | 3 | 30 |
| DIV (divide by weight sum) | 1 | 15 | 15 |
| **Total (N=10 WMA)** | **14** | — | **~78 cycles** |

For default parameters (ROC 11+14, WMA 10): ~78 cycles per bar. WMA taps dominate.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| ROC computation | Yes | VDIVPD on lag-offset arrays |
| Sum of ROCs | Yes | VADDPD |
| WMA (convolution) | Yes | FIR convolution — VDPPS or manual dot product |

Both ROC and WMA are non-recursive and fully vectorizable. AVX2 dot-product acceleration applies to the WMA convolution.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | WMA exact; ROC division uses hardware FP |
| **Timeliness** | 3/10 | 14+10 = 24 bar minimum warmup before first valid value |
| **Smoothness** | 8/10 | WMA of ROC sum produces smooth momentum curve |
| **Noise Rejection** | 7/10 | Long ROC periods filter short-term noise; WMA further smooths |

## Resources

- Coppock, E.S.C. (1962). "A Guide to the Use of Coppock Curve." *Barron's*
- Kirkpatrick, C. & Dahlquist, J. (2010). *Technical Analysis*, Chapter 15: Momentum
- PineScript reference: [`coppock.pine`](coppock.pine)
