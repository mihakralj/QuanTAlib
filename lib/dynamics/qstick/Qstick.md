# QSTICK: Qstick Indicator

> "The average candlestick body reveals the market's true conviction."

The Qstick indicator, developed by Tushar Chande, computes a moving average of the close-minus-open difference over a lookback period, quantifying whether bars are predominantly bullish or bearish. Positive values indicate closes above opens (buying pressure); negative values indicate closes below opens (selling pressure). It supports both SMA (O(N) space via ring buffer) and EMA (O(1) space) smoothing modes and requires TBar input for open/close access.

## Historical Context

Tushar Chande introduced Qstick as part of his candlestick quantification work in *The New Technical Trader* (1994, co-authored with Stanley Kroll). Traditional candlestick analysis relies on visual pattern recognition; Qstick reduces bar body direction and magnitude to a single continuous number suitable for systematic tracking. The indicator addresses a specific gap: close-to-close momentum indicators miss intrabar dynamics captured by the open-to-close differential. The name "Qstick" reflects the "quick stick" reading of candlestick conviction.

## Architecture & Physics

### 1. Body Difference

$$d_t = \text{Close}_t - \text{Open}_t$$

Positive $d_t$ represents a bullish bar (close above open), negative represents bearish, zero represents a doji.

### 2. Moving Average Smoothing

**SMA mode:** Maintains a ring buffer of $N$ differences and a running sum for O(1) incremental updates:

$$\text{Qstick}_t = \frac{1}{N} \sum_{i=0}^{N-1} d_{t-i}$$

**EMA mode:** Standard recursive filter with decay $\alpha = 2/(N+1)$:

$$\text{Qstick}_t = \alpha \cdot d_t + (1 - \alpha) \cdot \text{Qstick}_{t-1}$$

EMA mode uses O(1) space but weights recent bars more heavily than SMA.

### 3. Complexity

| Metric | SMA Mode | EMA Mode |
|:-------|:---------|:---------|
| Time | O(1) per bar | O(1) per bar |
| Space | O(N) ring buffer | O(1) |
| Ops | 1 add, 1 sub, 1 div | 1 sub, 1 mul, 1 FMA |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| period | int | 14 | > 0 | Lookback period for moving average |
| useEma | bool | false | — | Use EMA (true) or SMA (false) |

### Pseudo-code

```
QSTICK(bar, period=14, useEma=false):

  diff = bar.Close - bar.Open

  if useEma:
    // EMA mode
    alpha = 2.0 / (period + 1)
    if count == 0:
      ema_val = diff
    else:
      ema_val = FMA(alpha, diff - ema_val, ema_val)   // alpha*(diff-ema)+ema
    result = ema_val

  else:
    // SMA mode with ring buffer
    if buffer is full:
      running_sum -= buffer.oldest
    buffer.add(diff)
    running_sum += diff
    result = running_sum / min(count, period)

  return result
```

### Zero-Crossing Interpretation

| Condition | Meaning |
|:----------|:--------|
| Qstick > 0 | Closes above opens dominate (net buying pressure) |
| Qstick < 0 | Closes below opens dominate (net selling pressure) |
| Qstick crosses zero | Shift in intrabar momentum direction |
| Qstick rising | Increasing bullish pressure regardless of sign |
| Qstick falling | Increasing bearish pressure regardless of sign |

### Scale Dependence

Qstick values are in absolute price units, not normalized. Cross-instrument comparison requires normalization (e.g., divide by ATR or price level). Short periods (5-8) suit trading signals; longer periods (20+) suit trend identification.

## Resources

- Chande, T. S. & Kroll, S. (1994). *The New Technical Trader*. John Wiley and Sons.
- Kirkpatrick, C. D. & Dahlquist, J. R. (2015). *Technical Analysis: The Complete Resource for Financial Market Technicians*. FT Press.
