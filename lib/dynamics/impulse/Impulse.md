# IMPULSE: Elder Impulse System

> "The Impulse System identifies inflection points where a trend speeds up or slows down." -- Alexander Elder, *Come Into My Trading Room*

The Elder Impulse System combines a 13-period EMA (trend inertia) with the MACD(12,26,9) histogram (momentum acceleration) to classify each bar as bullish (+1), bearish (-1), or neutral (0). Both EMA slope and histogram slope must agree for a directional signal; disagreement forces neutral. The system functions as a permission filter rather than a signal generator, requiring 34 bars warmup and running at O(1) per bar through composition of two child indicators.

## Historical Context

Alexander Elder introduced the Impulse System in *Come Into My Trading Room* (2002). A psychiatrist turned trader, Elder designed it as a discipline enforcement mechanism: green bars permit long entries, red bars permit short entries, blue bars prohibit new positions in either direction. The intellectual lineage runs through Gerald Appel's MACD (1979) and Thomas Aspray's MACD Histogram (1986). Elder's contribution was combining first-derivative (EMA slope) and second-derivative (histogram slope) filters into a single ternary decision gate. Most implementations treat this as a visual color overlay; QuanTAlib exposes it as a programmatic discrete signal for algorithmic consumption.

## Architecture & Physics

### 1. EMA Component (Trend Inertia)

The 13-period EMA tracks trend direction via exponential smoothing:

$$\alpha = \frac{2}{n + 1} = \frac{2}{14} \approx 0.1429$$

$$\text{EMA}_t = \alpha \cdot \text{Close}_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

With warmup bias compensation:

$$\text{EMA}_{\text{corrected}} = \frac{\text{EMA}_{\text{raw}}}{1 - (1 - \alpha)^t}$$

The EMA slope $\Delta_{\text{EMA}} = \text{sign}(\text{EMA}_t - \text{EMA}_{t-1})$ represents smoothed trend direction (first derivative of price).

### 2. MACD Histogram Component (Momentum Acceleration)

$$\text{MACD Line} = \text{EMA}(12) - \text{EMA}(26)$$

$$\text{Signal} = \text{EMA}(9,\ \text{MACD Line})$$

$$\text{Histogram} = \text{MACD Line} - \text{Signal}$$

The histogram is the second derivative of price (acceleration). Its slope $\Delta_{\text{Hist}} = \text{sign}(\text{Hist}_t - \text{Hist}_{t-1})$ indicates whether momentum is building or fading.

### 3. Signal Classification

$$\text{Impulse} = \begin{cases} +1 & \text{if } \Delta_{\text{EMA}} > 0 \text{ and } \Delta_{\text{Hist}} > 0 \\ -1 & \text{if } \Delta_{\text{EMA}} < 0 \text{ and } \Delta_{\text{Hist}} < 0 \\ 0 & \text{otherwise} \end{cases}$$

The neutral state fires whenever the two components disagree, identifying transition zones where conviction is insufficient for new entries.

### 4. Warmup

$$W = \max(13, 26) + 9 - 1 = 34 \text{ bars}$$

Both child indicators must be warmed up and at least two comparison values must exist before valid signals emerge.

### 5. Complexity

| Metric | Value |
|:-------|:------|
| Time | O(1) per bar (delegates to child EMA/MACD updates) |
| Space | O(1) (3 internal EMA states + 4 doubles for slope comparison) |
| Allocations | Zero in hot path |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| emaPeriod | int | 13 | > 0 | EMA period for trend inertia |
| macdFast | int | 12 | > 0 | MACD fast EMA period |
| macdSlow | int | 26 | > macdFast | MACD slow EMA period |
| macdSignal | int | 9 | > 0 | MACD signal smoothing period |

### Pseudo-code

```
IMPULSE(close, emaPeriod=13, macdFast=12, macdSlow=26, macdSignal=9):

  // Child indicator updates
  ema_val     = EMA.Update(close, emaPeriod)
  macd_result = MACD.Update(close, macdFast, macdSlow, macdSignal)
  hist_val    = macd_result.Histogram

  // Slope computation (requires previous values)
  ema_slope  = sign(ema_val - prev_ema)
  hist_slope = sign(hist_val - prev_hist)

  // Classification
  if ema_slope > 0 AND hist_slope > 0:
    signal = +1          // Bullish: both inertia and momentum rising
  else if ema_slope < 0 AND hist_slope < 0:
    signal = -1          // Bearish: both inertia and momentum falling
  else:
    signal = 0           // Neutral: disagreement between components

  // State update
  prev_ema  = ema_val
  prev_hist = hist_val

  return signal
```

### Derivative Interpretation

The system combines two derivatives:

- **First derivative** (EMA slope): Is the smoothed trend rising or falling?
- **Second derivative** (histogram slope): Is the rate of MACD convergence/divergence accelerating or decelerating?

Both must confirm for a directional signal. This dual-confirmation suppresses false signals during transitions but introduces lag at inflection points.

## Resources

- Elder, A. (2002). *Come Into My Trading Room*. John Wiley and Sons.
- Appel, G. (1979). "The Moving Average Convergence-Divergence Method."
- Aspray, T. (1986). "MACD Histogram." *Technical Analysis of Stocks and Commodities*.
