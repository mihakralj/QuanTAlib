# QSTICK: Qstick Indicator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default DefaultPeriod), `useEma` (default DefaultUseEma)                      |
| **Outputs**      | Single series (QSTICK)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [qstick.pine](qstick.pine)                       |

- The Qstick indicator, developed by Tushar Chande, computes a moving average of the close-minus-open difference over a lookback period, quantifying ...
- Parameterized by `period` (default defaultperiod), `useema` (default defaultuseema).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

## Performance Profile

### Operation Count (Streaming Mode)

QStick is an SMA (or EMA) of (Close − Open), tracking average body momentum over N bars.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (Close − Open) | 1 | 1 | 1 |
| RingBuffer ADD + oldest SUB (running sum) | 2 | 1 | 2 |
| MUL × 1/N (average) | 1 | 3 | 3 |
| **Total** | **4** | — | **~6 cycles** |

One of the fastest dynamics indicators: a single subtraction plus an O(1) running sum. ~6 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Close − Open differences | Yes | VSUBPD — fully independent |
| Prefix sum | Partial | Sum scan; SIMD prefix-sum pattern |
| Windowed average | Yes | VSUBPD on prefix + VMULPD (×1/N) |

Fully SIMD-vectorizable in batch mode. AVX2 achieves ~4× throughput on large arrays.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic; trivial SMA of differences |
| **Timeliness** | 8/10 | SMA period only; no secondary smoothing lag |
| **Smoothness** | 7/10 | N-period averaging removes single-bar outliers |
| **Noise Rejection** | 6/10 | No adaptive bandwidth; outlier body candles shift the average |

## Resources

- Chande, T. S. & Kroll, S. (1994). *The New Technical Trader*. John Wiley and Sons.
- Kirkpatrick, C. D. & Dahlquist, J. R. (2015). *Technical Analysis: The Complete Resource for Financial Market Technicians*. FT Press.
