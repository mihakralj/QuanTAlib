# UI: Ulcer Index

> "The ulcer-inducing anxiety of watching your portfolio decline—now quantified."

Ulcer Index (UI) is a downside volatility measure that quantifies the depth and duration of drawdowns from recent highs. Developed by Peter G. Martin in 1987, UI captures what most volatility measures miss: the pain of being underwater. Unlike standard deviation or ATR that treat upside and downside moves equally, UI measures only the decline from peaks—the psychological stress that keeps investors awake at night.

## Historical Context

Peter G. Martin introduced the Ulcer Index in 1987, with the full methodology published in his 1989 book "The Investor's Guide to Fidelity Funds" co-authored with Byron McCann. The name comes from the stress-induced ulcers that investors might develop watching their portfolios decline.

Martin developed UI as a risk metric specifically for evaluating mutual fund performance. He recognized that traditional volatility measures (like standard deviation) penalize upside volatility equally with downside—but investors don't mind upside "volatility." The problem is drawdowns: how far below the recent high, and for how long.

The Ulcer Index became the denominator for the Martin Ratio (also called the Ulcer Performance Index or UPI), a risk-adjusted return measure analogous to the Sharpe Ratio but using UI instead of standard deviation:

$$
\text{Martin Ratio} = \frac{R - R_f}{UI}
$$

This makes UI particularly valuable for comparing investments: lower UI means less "ulcer-inducing" drawdowns.

## Architecture & Physics

### 1. Rolling Maximum (Highest Close)

Track the highest closing price over the lookback period:

$$
H_t = \max(C_{t}, C_{t-1}, \ldots, C_{t-n+1})
$$

where:

- $C_t$ = Close price at time $t$
- $n$ = Period (default 14)

### 2. Percent Drawdown

Calculate how far price has fallen from the rolling high:

$$
D_t = \frac{C_t - H_t}{H_t} \times 100
$$

Note: $D_t \leq 0$ always (price cannot exceed its own maximum).

For computation, we use the absolute percentage:

$$
|D_t| = \left|\frac{C_t - H_t}{H_t}\right| \times 100
$$

### 3. Squared Drawdown

Square the drawdown to penalize larger declines more heavily:

$$
D_t^2 = \left(\frac{C_t - H_t}{H_t} \times 100\right)^2
$$

### 4. Average Squared Drawdown

Calculate the mean of squared drawdowns over the period:

$$
\overline{D^2} = \frac{1}{n}\sum_{i=0}^{n-1} D_{t-i}^2
$$

### 5. Ulcer Index

Take the square root (RMS - root mean square):

$$
UI_t = \sqrt{\overline{D^2}} = \sqrt{\frac{1}{n}\sum_{i=0}^{n-1} D_{t-i}^2}
$$

## Mathematical Foundation

### Why Squared Drawdowns?

The squaring serves two purposes:

1. **Eliminates sign**: All drawdowns become positive contributions
2. **Penalizes large drawdowns**: A 20% drawdown contributes 400 to the sum; a 10% drawdown contributes only 100

This quadratic penalty means UI is highly sensitive to severe drawdowns—exactly what investors fear most.

### RMS Interpretation

The square root at the end returns UI to the same units as the input (percentage). UI can be interpreted as the "typical" percentage drawdown, weighted toward larger declines.

### Example Calculation

Consider a 5-period example:

| Day | Close | Rolling High | Drawdown (%) | Drawdown² |
| :---: | :---: | :---: | :---: | :---: |
| 1 | 100 | 100 | 0 | 0 |
| 2 | 98 | 100 | -2 | 4 |
| 3 | 95 | 100 | -5 | 25 |
| 4 | 97 | 100 | -3 | 9 |
| 5 | 99 | 100 | -1 | 1 |

$$
UI = \sqrt{\frac{0 + 4 + 25 + 9 + 1}{5}} = \sqrt{7.8} \approx 2.79
$$

### Properties

1. **Non-negativity**: $UI_t \geq 0$ always
2. **Zero at peak**: When $C_t = H_t$, drawdown is 0
3. **Units**: Percentage (same as input drawdown)
4. **Asymmetric**: Only measures downside (drawdown), ignores upside
5. **Trend-sensitive**: Prolonged declines accumulate higher UI

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MAX scan (period elements) | n | 1 | n |
| SUB | 2 | 1 | 2 |
| DIV | 2 | 15 | 30 |
| MUL | 2 | 3 | 6 |
| SQRT | 1 | 15 | 15 |
| Ring buffer ops | 2 | 2 | 4 |
| **Total** | — | — | **~57 + n cycles** |

The MAX scan dominates for larger periods. For period=14, approximately 71 cycles per bar.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Rolling max | Complex | Limited | ~2-4× |
| Arithmetic | 4096 | 512 | 8× |
| SQRT | 512 | 64 | 8× |

Rolling maximum has limited SIMD benefit due to sequential dependency, but arithmetic operations vectorize well.

### Memory Profile

- **Per instance:** ~144 bytes (state + two ring buffers of size period)
- **Backup arrays:** 2 × period × 8 bytes (for bar correction)
- **Period 14:** ~368 bytes per instance
- **100 instances:** ~36 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact calculation, matches reference |
| **Timeliness** | 7/10 | Period-based lag |
| **Smoothness** | 7/10 | RMS smoothing, but can change quickly |
| **Interpretability** | 9/10 | Clear meaning: typical drawdown % |
| **Risk Assessment** | 10/10 | Excellent downside risk measure |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | ✅ | Matches calculation |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | ✅ | Matches calculation |
| **PineScript** | ✅ | Matches ui.pine reference |
| **Manual** | ✅ | Validated against Martin's formula |

## Common Pitfalls

1. **Warmup period**: UI requires a full period of data before producing valid results. During warmup, values represent partial-period calculations that may underestimate true UI.

2. **Zero interpretation**: UI=0 means price is at or above the period high—no drawdown. This doesn't mean low risk; the market might be at a blow-off top.

3. **Period selection**: Shorter periods (7-14) react quickly to recent drawdowns but may miss longer declines. Longer periods (21-50) capture extended bear markets but lag on recovery.

4. **Comparison across assets**: UI is percentage-based, so it's comparable across different-priced assets (unlike raw TR or ATR).

5. **Trend bias**: In strong uptrends, UI approaches zero (constantly at new highs). This might mask lurking risk when the trend eventually breaks.

6. **Not a timing indicator**: UI measures risk, not direction. High UI during a decline doesn't predict reversal—it just confirms you're underwater.

## Trading Applications

### Risk-Adjusted Performance (Martin Ratio)

Compare investments using the Martin Ratio:

$$
\text{Martin Ratio} = \frac{\text{Annualized Return} - R_f}{UI}
$$

Higher Martin Ratio = better risk-adjusted returns (more return per unit of "ulcer").

### Portfolio Selection

Filter investments by maximum acceptable UI:

```
If UI > 15: Too volatile for conservative portfolios
If UI < 5: Suitable for risk-averse investors
```

### Position Sizing

Adjust position size based on UI:

```
Position size = Base size × (Target UI / Actual UI)
```

Higher UI assets get smaller allocations.

### Drawdown Monitoring

Track UI in real-time to monitor portfolio stress:

```
If UI crosses above threshold: Consider hedging or reducing exposure
If UI declining from high: Recovery underway
```

### Strategy Evaluation

Compare trading strategies by UI:

```
Strategy A: Return 15%, UI 8 → Martin Ratio = 1.88
Strategy B: Return 12%, UI 4 → Martin Ratio = 3.00
Strategy B is better risk-adjusted despite lower returns
```

## Relationship to Other Indicators

| Indicator | Relationship to UI |
| :--- | :--- |
| **Standard Deviation** | UI measures only downside; StdDev measures both directions |
| **ATR** | ATR is range-based; UI is drawdown-based |
| **Maximum Drawdown** | MDD is the worst single drawdown; UI averages all drawdowns |
| **Sharpe Ratio** | Uses StdDev; Martin Ratio uses UI |
| **Sortino Ratio** | Uses downside deviation; similar philosophy to UI |
| **Calmar Ratio** | Uses max drawdown; UI uses average drawdown |

## References

- Martin, P. G., & McCann, B. B. (1989). *The Investor's Guide to Fidelity Funds*. John Wiley & Sons.
- Martin, P. G. (1987). "Ulcer Index, An Alternative Approach to the Measurement of Investment Risk & Risk-Adjusted Performance."
- Kaufman, P. J. (2013). *Trading Systems and Methods* (5th ed.). Wiley.