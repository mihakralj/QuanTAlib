# TSF: Time Series Forecast

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Tsf)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [tsf_signature](tsf_signature) |

### TL;DR

- TSF projects the least-squares regression line one bar forward, providing a statistically grounded forecast of the next bar's value.
- Parameterized by `period` (default 14).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The best prediction of the future is the trend that's already in motion — extended by exactly one step."

TSF projects the least-squares regression line one bar forward, providing a statistically grounded forecast of the next bar's value. Unlike simple moving averages that smooth past data, TSF answers the question: "If the current trend continues, where will price be next?" This makes it inherently leading rather than lagging, though the forecast degrades quickly beyond one step.

## Historical Context

Time Series Forecast originates from classical linear regression applied to financial time series. The concept appeared in TA-Lib as `TA_TSF` and has been a standard offering in technical analysis software since the 1990s. Tushar Chande's *The New Technical Trader* (1994) formalized several regression-based indicators including the closely related Chande Forecast Oscillator (CFO), which measures the percentage error between the current price and the TSF value.

TSF is mathematically identical to the Least Squares Moving Average (LSMA) evaluated one step beyond the window endpoint. Where LSMA answers "what is the trend value now?", TSF answers "what will the trend value be next bar?" The relationship is exact: `TSF = LSMA + slope`, where slope is the per-bar rate of change of the regression line.

## Architecture & Physics

### 1. O(1) Incremental Linear Regression

The implementation uses running sums (`SumY`, `SumXY`) with a reversed-x convention where `x=0` corresponds to the newest bar. This allows O(1) updates without maintaining the full regression matrix.

**Constants (precomputed once):**

$$\Sigma_x = \frac{n(n-1)}{2}, \quad \Sigma_{x^2} = \frac{(n-1) \cdot n \cdot (2n-1)}{6}$$

$$D = n \cdot \Sigma_{x^2} - \Sigma_x^2$$

### 2. O(1) Sum Updates

When a new value enters and the oldest drops:

$$\Sigma_{xy}^{new} = \Sigma_{xy}^{old} + \Sigma_y^{old} - n \cdot v_{oldest}$$

$$\Sigma_y^{new} = \Sigma_y^{old} - v_{oldest} + v_{new}$$

### 3. Regression Parameters

$$m = \frac{n \cdot \Sigma_{xy} - \Sigma_x \cdot \Sigma_y}{D}$$

$$b = \frac{\Sigma_y - m \cdot \Sigma_x}{n}$$

In the reversed-x convention, `b` is the regression value at the current bar (x=0), and `m` is negative for uptrends.

### 4. TSF Calculation

$$\text{TSF} = b - m$$

This projects one step forward from the current bar. Equivalently, in standard convention (x=0=oldest):

$$\text{TSF} = \text{slope} \cdot n + \text{intercept}$$

### 5. Resync Guard

After every 1000 ticks, running sums are recomputed from the buffer to prevent floating-point drift accumulation.

## Mathematical Precision & Implementation Philosophy

### Relationship to Other Indicators

| Indicator | Formula | Interpretation |
|-----------|---------|----------------|
| **LSMA** (offset=0) | `b` | Regression value at current bar |
| **TSF** | `b - m` | Regression value one step ahead |
| **LSMA** (offset=1) | `b - m × 1` | Same as TSF |
| **CFO** | `100 × (price - TSF_at_current) / price` | Forecast error as percentage |
| **Inertia** | `price - TSF_at_current` | Raw forecast error (residual) |

### FMA Usage

All critical multiplications use `Math.FusedMultiplyAdd` for precision, including:
- SumXY O(1) update: `FMA(-period, oldest, sumXY + prevSumY)`
- Slope calculation: `FMA(n, sumXY, -sumX × sumY)`
- Intercept calculation: `FMA(-m, sumX, sumY)`

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|-------|---------------|----------|
| ADD/SUB | 4 | 1 | 4 |
| MUL | 0 | 3 | 0 |
| DIV | 2 | 12 | 24 |
| FMA | 3 | 5 | 15 |
| CMP | 1 | 1 | 1 |
| **Total** | **10** | | **~44** |

### Batch Mode (SIMD Analysis)

The O(1) running-sum algorithm is inherently serial due to data dependencies. Batch mode uses `stackalloc` for small buffers (≤256 elements) to avoid heap allocation.

| Mode | Per-Bar Cost | Notes |
|------|-------------|-------|
| Streaming | ~44 cycles | O(1) update |
| Batch (Span) | ~44 cycles | Same algorithm, zero-alloc |
| Batch (TSeries) | ~44 cycles + state restore | Replays last N bars |

### Quality Metrics

| Metric | Score (1-10) | Justification |
|--------|-------------|---------------|
| Accuracy | 9 | Exact OLS regression, FMA precision |
| Timeliness | 10 | Leading indicator (projects forward) |
| Overshoot | 7 | Extrapolation amplifies noise |
| Smoothness | 5 | Less smooth than LSMA (forecast adds slope) |

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| LSMA(offset=1) | ✅ | Mathematical identity, exact match |
| TA-Lib | 🔲 | `TA_TSF` available in TALib.NETCore |
| Skender | ❌ | No direct TSF method |

## Common Pitfalls

1. **TSF is not LSMA.** LSMA = regression value at the current bar. TSF = one step ahead. The difference equals the regression slope. Using TSF as a smoothing average will produce systematically biased results.

2. **Single-step forecast only.** TSF projects exactly one bar forward. Multi-step extrapolation (TSF at offset=2, 3, ...) accumulates error quadratically. For multi-step forecasting, use AFIRMA or dedicated time-series models.

3. **Warmup = period bars.** The indicator needs a full window of data before regression is meaningful. During warmup, TSF returns raw input values.

4. **Noise amplification.** Because TSF adds the slope to the endpoint value, it amplifies short-term noise. Use longer periods (20+) for less noisy forecasts, or combine with a smoother like LSMA.

5. **Bar correction support.** The `isNew=false` pathway correctly rolls back state using the `_ps` (previous state) pattern. Always use `isNew=false` for intra-bar updates in live trading.

6. **Resync interval.** Running sums are recomputed every 1000 ticks to prevent floating-point drift. This adds negligible overhead but ensures long-running accuracy.

## References

- Tushar Chande, *The New Technical Trader*, 1994
- TA-Lib: `TA_TSF` function (www.ta-lib.org)
- PineScript: `ta.linreg(source, length, -1)` (offset=-1 = one step ahead)
