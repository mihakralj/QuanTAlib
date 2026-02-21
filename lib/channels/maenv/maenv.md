# MAENV: Moving Average Envelope

Moving Average Envelope (MA Envelope) constructs symmetric bands at a fixed percentage distance above and below a moving average center line. Unlike volatility-adaptive channels (Bollinger, Keltner, ATR Bands) where band width varies with market conditions, MA Envelope uses a constant percentage offset, creating bands whose absolute width scales only with price level. The indicator supports configurable moving average types (SMA, EMA, WMA) for the center line, allowing users to trade off between lag, smoothness, and responsiveness.

## Historical Context

Moving Average Envelopes are one of the oldest band-type indicators, predating Bollinger Bands by decades. The concept is straightforward: if a moving average represents "fair value," then price consistently trading a certain percentage above or below that average represents overbought or oversold conditions. The fixed-percentage approach was the standard technique before John Bollinger introduced standard deviation-based adaptive bands in the 1980s.

The simplicity is both the strength and weakness. Percentage envelopes require manual calibration for each instrument and timeframe: a 1% envelope works for low-volatility large-cap equities but is meaningless for cryptocurrency. The percentage must match the asset's typical volatility. Despite this limitation, fixed envelopes remain popular in institutional settings where the known percentage corresponds to a specific risk threshold or margin requirement.

## Architecture & Physics

### 1. Center Line (Configurable MA)

Three moving average types are supported:

**SMA** (type = 0): $O(1)$ via circular buffer

$$\text{Middle}_t = \frac{1}{n} \sum_{i=0}^{n-1} x_{t-i}$$

**EMA** (type = 1): $O(1)$ via recursive update with warmup compensation

$$\alpha = \frac{2}{n + 1}$$

$$\text{Middle}_t = \frac{\alpha \cdot x_t + (1-\alpha) \cdot \text{raw}_{t-1}}{w_t}$$

**WMA** (type = 2): $O(n)$ weighted sum

$$\text{Middle}_t = \frac{\sum_{i=0}^{n-1} (n-i) \cdot n \cdot x_{t-i}}{\sum_{i=0}^{n-1} (n-i) \cdot n}$$

### 2. Fixed Percentage Offset

$$\text{distance}_t = \text{Middle}_t \times \frac{P}{100}$$

$$\text{Upper}_t = \text{Middle}_t + \text{distance}_t$$

$$\text{Lower}_t = \text{Middle}_t - \text{distance}_t$$

### 3. Scale Invariance

Because the offset is a percentage of the MA value, the bands automatically scale with price level. A 1% envelope on a $100 stock produces $1 bands; on a $10 stock, $0.10 bands. This is multiplicative scaling, not additive.

### 4. Complexity

$O(1)$ for SMA and EMA modes. $O(n)$ for WMA mode due to the weighted sum.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback for the moving average ($n$) | 20 | $> 0$ |
| `percentage` | Band distance as percent of MA ($P$) | 1.0 | $> 0$ |
| `ma_type` | Moving average type: 0=SMA, 1=EMA, 2=WMA | 1 (EMA) | $\{0, 1, 2\}$ |
| `source` | Input price series | close | |

### Pseudo-code

```
function MAENV(source, period, percentage, ma_type):
    validate: period > 0, percentage > 0

    // Compute center line based on MA type
    if ma_type == 0: middle = SMA(source, period)
    if ma_type == 1: middle = EMA(source, period)  // with warmup
    if ma_type == 2: middle = WMA(source, period)

    // Fixed percentage offset
    dist = middle * percentage / 100
    upper = middle + dist
    lower = middle - dist

    return [middle, upper, lower]
```

### Output Interpretation

| Output | Description |
|--------|-------------|
| `middle` | Moving average center line |
| `upper` | MA + fixed percentage (overbought threshold) |
| `lower` | MA - fixed percentage (oversold threshold) |

## Resources

- **Murphy, J.J.** *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999. (Moving average envelope fundamentals)
- **Bollinger, J.** *Bollinger on Bollinger Bands*. McGraw-Hill, 2001. (Adaptive alternative that replaced fixed envelopes)
