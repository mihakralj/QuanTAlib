# TR: True Range

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (TR)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `1` bars                          |
| **PineScript**   | [tr.pine](tr.pine)                       |

- True Range (TR) is a volatility measure that captures the maximum price movement for each bar, including any gap from the previous close.
- No configurable parameters; computation is stateless per bar.
- Output range: $\geq 0$.
- Requires `1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The true measure of volatility isn't just where price traveled within the bar, but whether it leaped from where it was."

True Range (TR) is a volatility measure that captures the maximum price movement for each bar, including any gap from the previous close. Developed by J. Welles Wilder Jr. in 1978, TR forms the foundation for Average True Range (ATR) and numerous other volatility-based indicators. Unlike simple High-Low range, TR accounts for overnight gaps and opening jumps, providing a complete picture of price movement.

## Historical Context

J. Welles Wilder Jr. introduced True Range in his seminal 1978 book "New Concepts in Technical Trading Systems." This same work introduced many other foundational indicators including RSI, ATR, Parabolic SAR, and the ADX family.

Wilder recognized that the traditional High-Low range fails to capture the full extent of price movement when markets gap at the open. A stock might have a narrow intraday range but a massive overnight gap—the simple High-Low would miss this volatility entirely. True Range solves this by considering the previous close as a potential extreme.

The elegance of TR lies in its simplicity: take the maximum of three simple calculations. This approach captures all possible price extremes while requiring minimal data (just High, Low, Close, and the previous Close). TR became the building block for ATR, which Wilder used extensively for stop-loss placement and position sizing.

## Architecture & Physics

### 1. Three Range Components

True Range considers three potential extremes:

$$
TR_1 = H_t - L_t
$$

$$
TR_2 = |H_t - C_{t-1}|
$$

$$
TR_3 = |L_t - C_{t-1}|
$$

where:

- $H_t, L_t$ = High, Low of current bar
- $C_{t-1}$ = Close of previous bar

### 2. True Range Calculation

The True Range is the maximum of all three components:

$$
TR_t = \max(TR_1, TR_2, TR_3)
$$

Expanded:

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

### 3. First Bar Handling

For the first bar (no previous close available):

$$
TR_0 = H_0 - L_0
$$

The implementation uses only the High-Low range when there's no history to reference.

### 4. Scenario Analysis

**No Gap (prevClose within H-L range):**

When $L_t \leq C_{t-1} \leq H_t$:

$$
TR_t = H_t - L_t
$$

The intraday range captures all movement since the gap components ($TR_2$, $TR_3$) are smaller.

**Gap Up (prevClose below Low):**

When $C_{t-1} < L_t$:

$$
TR_t = H_t - C_{t-1} = TR_2
$$

The gap up contributes to the range; price moved from yesterday's close to today's high.

**Gap Down (prevClose above High):**

When $C_{t-1} > H_t$:

$$
TR_t = C_{t-1} - L_t = TR_3
$$

The gap down contributes; price moved from yesterday's close to today's low.

## Mathematical Foundation

### Why Three Components?

Consider a price bar with these values:

- Yesterday close: $C_{t-1} = 100$
- Today open: $O_t = 95$ (gap down)
- Today high: $H_t = 98$
- Today low: $L_t = 93$
- Today close: $C_t = 96$

Traditional range: $H_t - L_t = 98 - 93 = 5$

True Range components:

- $TR_1 = 98 - 93 = 5$
- $TR_2 = |98 - 100| = 2$
- $TR_3 = |93 - 100| = 7$

$$
TR_t = \max(5, 2, 7) = 7
$$

The market actually moved 7 points (from 100 down to 93), not just 5. TR captures this correctly.

### Geometric Interpretation

True Range measures the maximum vertical distance price could have traveled from the previous close through today's bar. Visualize it as:

```
Gap Down Case:
                  ┌── prevClose (100)
                  │
                  │  ← TR = 7
                  │
Today's Bar: [93 ──── 98]
                Low    High
```

### Properties

1. **Non-negativity**: $TR_t \geq 0$ always
2. **Lower bound**: $TR_t \geq H_t - L_t$ (always at least the intraday range)
3. **Unit**: Same unit as price (dollars, points, etc.)
4. **No smoothing**: TR is a bar-by-bar calculation with no lookback
5. **Immediate**: TR is "hot" after just one bar (WarmupPeriod = 1)

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB | 3 | 1 | 3 |
| ABS | 2 | 1 | 2 |
| MAX | 2 | 1 | 2 |
| **Total** | — | — | **~7 cycles** |

TR is extremely lightweight—one of the cheapest indicators to compute. No logarithms, no division, no transcendental functions.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Subtractions | 1536 | 192 | 8× |
| Absolute values | 1024 | 128 | 8× |
| Maximum | 1024 | 128 | 8× |

All operations vectorize perfectly with AVX2. No sequential dependencies limit SIMD utilization.

### Memory Profile

- **Per instance:** ~48 bytes (state struct)
- **No ring buffer required** (only needs previous close)
- **100 instances:** ~4.8 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact calculation, no approximations |
| **Timeliness** | 10/10 | Zero lag, bar-by-bar |
| **Smoothness** | 2/10 | Unsmoothed, can be jagged |
| **Simplicity** | 10/10 | Three comparisons, one max |
| **Foundation** | 10/10 | Building block for ATR, etc. |

## Validation

TR is universally implemented with identical formulas:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Exact match (TRANGE function) |
| **Skender** | ✅ | Exact match |
| **Tulip** | ✅ | Exact match |
| **OoplesFinance** | ✅ | Exact match |
| **PineScript** | ✅ | Matches tr.pine reference |
| **Manual** | ✅ | Validated against Wilder formula |

TR is one of the most consistently implemented indicators across all libraries.

## Common Pitfalls

1. **First bar handling**: The first bar has no previous close. The implementation uses High-Low for the first bar. Some implementations return NaN for the first bar—this one returns a valid (though incomplete) value.

2. **Confusing TR with ATR**: TR is the raw, unsmoothed value per bar. ATR is TR smoothed over a period. TR can be very volatile; ATR provides a more stable volatility estimate.

3. **Unit dependency**: TR is in the same units as price. A $500 stock might have TR=10 while a $50 stock has TR=1, even if percentage volatility is identical. Use NATR (Normalized ATR) for percentage-based comparisons.

4. **Gap sensitivity**: TR captures gaps, which may or may not be desirable. For intraday-only volatility, use High-Low range instead.

5. **Comparing across assets**: Don't compare raw TR values across different-priced assets. TR=5 means different things for a $20 stock vs a $200 stock.

6. **Weekend/holiday gaps**: TR will capture large gaps after market closures. This may inflate volatility estimates around holidays. Some strategies filter these bars.

## Trading Applications

### Stop-Loss Placement (via ATR)

TR is the foundation for ATR-based stops:

```
Long stop = Entry - (ATR × multiplier)
Short stop = Entry + (ATR × multiplier)
where ATR = smoothed TR
```

### Position Sizing

Use TR/ATR for volatility-adjusted position sizing:

```
Position size = (Account × Risk%) / (ATR × multiplier)
```

Higher TR means more volatility, so smaller position.

### Breakout Detection

Large TR spikes indicate significant price movement:

```
If TR_today > 2 × ATR_14: Potential breakout
Monitor for continuation or reversal
```

### Volatility Filtering

Filter trades based on minimum TR:

```
If TR < threshold: Skip trade (too quiet, potential whipsaw)
If TR > threshold: Proceed (sufficient volatility for trend)
```

### Gap Analysis

Compare TR to High-Low range to quantify gap impact:

```
Gap contribution = TR - (High - Low)
If gap contribution > 50% of TR: Significant gap move
```

## Relationship to Other Indicators

| Indicator | Relationship to TR |
| :--- | :--- |
| **ATR** | Smoothed TR (RMA/Wilder's MA) |
| **NATR** | ATR / Close × 100 (also known as ATRP) |
| **Keltner Channel** | Uses ATR for band width |
| **Chandelier Exit** | Uses ATR for trailing stop |
| **SuperTrend** | Uses ATR for trend bands |
| **ADX** | Uses TR in denominator for normalization |

## References

- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research.
- Kaufman, P. J. (2013). *Trading Systems and Methods* (5th ed.). Wiley.
- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
