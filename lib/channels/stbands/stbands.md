# STBANDS: Super Trend Bands

Super Trend Bands provide ATR-based dynamic support and resistance levels with asymmetric ratchet logic: the upper band only tightens downward during downtrends, and the lower band only tightens upward during uptrends. This creates natural trailing stop-loss levels that respect market momentum. A trend direction signal ($+1$ or $-1$) flips when price breaches the opposite band. The ATR is computed as a simple moving average of True Range via a ring buffer with running sum, providing O(1) streaming updates.

## Historical Context

The SuperTrend indicator emerged from the trading community's need for a volatility-adaptive trend-following tool. Olivier Seban popularized the concept, building on Wilder's ATR foundation to create bands that respect trend direction rather than blindly following price symmetrically.

Traditional channel indicators like Bollinger Bands expand and contract symmetrically around price. SuperTrend takes a different approach: once a band establishes a level favorable to the trend, it refuses to retreat. This asymmetric "ratchet effect" means the lower band in an uptrend can only rise (never fall), creating a progressively tighter trailing stop. The band resets only when price violates it, triggering a trend reversal.

The implementation here follows the canonical PineScript algorithm, using a simple moving average of True Range rather than Wilder's exponentially smoothed ATR. This produces slightly more responsive bands since the SMA gives equal weight to all TR values in the window, while Wilder's RMA carries heavier memory of older values.

## Architecture & Physics

### 1. True Range

True Range captures the full extent of price movement including gaps:

$$
TR_t = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)
$$

### 2. Average True Range (SMA of TR)

The implementation uses a simple moving average of TR via ring buffer with running sum:

$$
ATR_t = \frac{1}{\min(t+1, n)} \sum_{i=0}^{\min(t,n-1)} TR_{t-i}
$$

The running sum provides $O(1)$ updates: subtract the oldest TR leaving the window, add the new TR.

### 3. Basic Band Calculation

Bands center on HL2 (the bar midpoint):

$$
\text{HL2}_t = \frac{H_t + L_t}{2}
$$

$$
\text{BasicUpper}_t = \text{HL2}_t + k \cdot ATR_t
$$

$$
\text{BasicLower}_t = \text{HL2}_t - k \cdot ATR_t
$$

where $k$ is the multiplier (default 3.0).

### 4. Ratchet Logic (Final Bands)

The defining characteristic. Bands only move in the trend-favorable direction:

$$
\text{Upper}_t = \begin{cases}
\text{BasicUpper}_t & \text{if } \text{BasicUpper}_t < \text{Upper}_{t-1} \;\text{OR}\; C_{t-1} > \text{Upper}_{t-1} \\
\text{Upper}_{t-1} & \text{otherwise}
\end{cases}
$$

$$
\text{Lower}_t = \begin{cases}
\text{BasicLower}_t & \text{if } \text{BasicLower}_t > \text{Lower}_{t-1} \;\text{OR}\; C_{t-1} < \text{Lower}_{t-1} \\
\text{Lower}_{t-1} & \text{otherwise}
\end{cases}
$$

In words: the upper band adopts the new (lower) basic value only if it tightens, or if price already broke above the previous upper band (resetting it). The lower band rises only if the new basic value is higher, or if price already broke below the previous lower band.

### 5. Trend Determination

Trend flips when price breaches the opposite band:

$$
\text{Trend}_t = \begin{cases}
+1 & \text{if } C_t \leq \text{Lower}_t \\
-1 & \text{if } C_t \geq \text{Upper}_t \\
\text{Trend}_{t-1} & \text{otherwise}
\end{cases}
$$

$+1$ = bullish (price is above the lower band trailing stop), $-1$ = bearish (price is below the upper band trailing stop).

### 6. Complexity

Streaming: $O(1)$ per bar. The TR running sum uses a ring buffer; the ratchet logic and trend determination are constant-time comparisons. Memory: one ring buffer of $n$ floats plus scalar state for previous bands, trend, and close.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n$ | period | 10 | $> 0$ | ATR lookback period |
| $k$ | multiplier | 3.0 | $> 0$ | ATR multiplier for band distance from HL2 |

### Pseudo-code

```
function stbands(high[], low[], close[], period, multiplier):
    tr_buf = ring_buffer(period)
    tr_sum = 0, count = 0
    prev_close = close[0]
    final_upper = NaN, final_lower = NaN
    trend = +1

    for each bar t:
        h = high[t], l = low[t], c = close[t]

        // True Range
        tr = max(h - l, abs(h - prev_close), abs(l - prev_close))

        // ATR via running sum ring buffer
        if tr_buf.is_full:
            tr_sum -= tr_buf.oldest
            count  -= 1
        tr_buf.add(tr)
        tr_sum += tr
        count  += 1
        atr = tr_sum / count

        // Basic bands centered on HL2
        hl2 = (h + l) / 2
        basic_upper = hl2 + multiplier * atr
        basic_lower = hl2 - multiplier * atr

        if t == 0:
            final_upper = basic_upper
            final_lower = basic_lower
            trend = +1
        else:
            // Ratchet: upper only tightens or resets on breakout
            if basic_upper < final_upper OR prev_close > final_upper:
                final_upper = basic_upper
            // otherwise hold

            // Ratchet: lower only tightens or resets on breakdown
            if basic_lower > final_lower OR prev_close < final_lower:
                final_lower = basic_lower
            // otherwise hold

            // Trend flip
            if c <= final_lower:
                trend = +1
            else if c >= final_upper:
                trend = -1
            // otherwise hold previous trend

        prev_close = c
        emit (final_upper, final_lower, trend)
```

### Band State Transitions

| Condition | Upper Band | Lower Band |
|-----------|-----------|------------|
| Uptrend, price rising | Holds | Rises (tightens) |
| Uptrend, price breaks upper | Resets to basic | Holds |
| Downtrend, price falling | Drops (tightens) | Holds |
| Downtrend, price breaks lower | Holds | Resets to basic |

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Trend = $+1$ | Bullish; lower band acts as trailing stop |
| Trend = $-1$ | Bearish; upper band acts as trailing stop |
| Trend flip $+1 \to -1$ | Bearish reversal; price breached upper band |
| Trend flip $-1 \to +1$ | Bullish reversal; price breached lower band |
| Band width contracting | ATR falling; volatility decreasing |

## Resources

- Seban, O. SuperTrend Indicator methodology.
- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research.
- TradingView. "SuperTrend." Pine Script Reference.
