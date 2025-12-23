# Trend Indicators Comparison

Scale 1–10 where **10 = better** for every column. Detailed evaluation criteria at the bottom of this doc.

- **Accuracy**: Preserve true movement structure (major trends and turning points) without distortion or artificial patterns.
- **Timeliness**: Minimal lag. Fast response to genuine movement changes and reversals.
- O**vershoot Control**: Remain within min/max of input, avoid generating artificial over-reaching levels and false threshold triggers.
- **Smoothness**: Noise suppression. Stable output with smooth derivatives (no erratic velocity/acceleration).

| Indicator | Accuracy | Timeliness | Overshoot Control | Smoothness | Notes (revised) |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **ALMA** | 8 | 7 | 10 | 8 | Positive-weight FIR; accurate-ish but still a lag tradeoff. |
| **BESSEL** | 9 | 7 | 9 | 8 | Strong shape/phase preservation; step response is well-behaved. |
| **BILATERAL** | 7 | 6 | 10 | 8 | Edge-preserving; excellent in ranging markets, variable smoothing by design. |
| **BLMA** | 7 | 3 | 10 | 10 | Standard DSP window; superior noise suppression but significant lag. |
| **DEMA** | 4 | 9 | 3 | 6 | Lag-canceling subtraction ⇒ structure distortion + overshoot risk. |
| **DWMA** | 7 | 2 | 10 | 10 | Ultra-smooth, but smears structure heavily (lag dominates). |
| **EMA** | 8 | 6 | 10 | 8 | Convex IIR (monotone) ⇒ faithful & stable, moderate lag. |
| **HMA** | 6 | 9 | 3 | 7 | Very fast but can ring/overshoot; “accurate” depends on regime. |
| **HTIT** | 7 | 8 | 6 | 8 | Trend extraction can be excellent but can distort around turns/cycles. |
| **JMA** | 8 | 9 | 9 | 9 | Great practical trend estimate; adaptive behavior can reshape structure. |
| **KAMA** | 8 | 8 | 10 | 8 | Variable-alpha EMA: stable, good structure, less lag in trends. |
| **LSMA** | 3 | 8 | 5 | 3 | Regression endpoint/projection: can deviate from true path + noisy. |
| **MAMA** | 6 | 9 | 6 | 3 | Phase-adaptive; fast but accuracy varies with cycle model fit. |
| **MGDI** | 7 | 7 | 10 | 9 | Stable “EMA-like” behavior; good smoothing, not especially fast. |
| **PWMA** | 6 | 7 | 10 | 6 | Positive weights (no overshoot) but can be twitchy vs noise. |
| **RMA** | 8 | 4 | 10 | 9 | Slower EMA ⇒ very stable + faithful, but laggier. |
| **SMA** | 7 | 3 | 10 | 6 | Baseline: faithful but slow; smoothness only moderate. |
| **SSF** | 9 | 8 | 8 | 9 | Excellent smoothing with relatively low lag; mild ringing possible. |
| **T3** | 7 | 8 | 5 | 10 | Extremely smooth; overshoot depends on tuning (can behave “too clever”). |
| **TEMA** | 3 | 10 | 3 | 6 | Near-zero lag feel, but structure distortion + overshoot common. |
| **TRIMA** | 7 | 2 | 10 | 10 | Very smooth FIR; structure preserved but delayed a lot. |
| **USF** | 9 | 9 | 8 | 9 | Low-lag smoother; very good overall, slight ringing possible. |
| **VIDYA** | 7 | 8 | 10 | 7 | Variable-alpha EMA: stable, responsive in trends, moderate smoothness. |
| **WMA** | 7 | 7 | 10 | 5 | Faster than SMA; less smooth; still faithful (positive weights). |

## Evaluation Criteria

### Accuracy (preserving large-scale structure)

Moving average should maintain the important underlying structure of price movements (like major trends and cycles) while filtering out all smaller fluctuations; it should faithfully represent the true price trajectory over longer timeframes.

### Timeliness (minimal lag)

Most moving averages lag behind price action - they indicate changes way after they've already happened. A good moving average minimizes this lag, responding quickly to genuine price movements without sacrificing other qualities, providing more actionable signals and earlier entries/exits.

### Minimal overshoot

Overshoot occurs when a highly reactive moving average extends beyond the actual price extremes, creating false impressions of price levels never reached. TEMA, DEMA and HMA are examples of overshooting moving averages; good moving average should avoid this distortion, particularly during price reversals, preventing false triggers when used with threshold-based systems.

### Smoothness (reduced noise)

A quality moving average filters out random price fluctuations (noise) that don't represent meaningful market activity, especially in steady non-volatile periods. This creates a clean, smooth line that clearly shows the underlying price direction without the jagged, erratic movements that could trigger false signals.
