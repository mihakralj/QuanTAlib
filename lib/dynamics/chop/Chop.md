# CHOP: Choppiness Index

The Choppiness Index is a non-directional regime indicator that measures whether the market is trending or trading sideways. It compares total price movement (sum of True Range) to net price movement (high-low channel width) using a logarithmic ratio, producing a bounded value where high readings indicate choppy/consolidating conditions and low readings indicate trending conditions. CHOP does not indicate direction — only whether directional strategies are likely to succeed. The logarithmic scaling normalizes the output to approximately 0-100 regardless of price level or volatility magnitude.

## Historical Context

Australian commodity trader E.W. Dreiss created the Choppiness Index to help traders avoid whipsaw losses by identifying market conditions unsuitable for trend-following strategies. The core insight is geometric: in a perfect trend, total bar-by-bar movement (sum of True Range) roughly equals the net distance traveled (channel width). In a choppy market, total movement greatly exceeds net progress — the market thrashes back and forth, accumulating True Range while the net channel stays narrow. The ratio between these two quantities, log-scaled to normalize across instruments and timeframes, produces a clean regime classifier. The conventional thresholds (38.2 and 61.8) are deliberately chosen as Fibonacci levels, though their efficacy is empirical rather than mathematical.

## Architecture & Physics

### 1. True Range Accumulation

$$TR_t = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

A rolling sum maintains $\sum_{i=1}^{N} TR_i$ over the lookback window.

### 2. Price Channel Width

The net price movement over the same window:

$$\text{Channel} = \max(H_{t-N+1:t}) - \min(L_{t-N+1:t})$$

### 3. Choppiness Index

$$\text{CHOP} = 100 \times \frac{\log_{10}\!\left(\dfrac{\sum TR_N}{\text{Channel}}\right)}{\log_{10}(N)}$$

The denominator $\log_{10}(N)$ normalizes the output so that the theoretical maximum approaches 100 (when $\sum TR = N \times \text{Channel}$, which occurs when every bar traverses the full channel).

### 4. Complexity

- **Time:** $O(N)$ per bar for min/max scanning of high/low buffers; rolling sum is $O(1)$
- **Space:** $O(N)$ — three ring buffers (TR, highs, lows)
- **Warmup:** $N$ bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

### Pseudo-code

```
Initialize:
  trBuf = RingBuffer(period)
  highBuf = RingBuffer(period)
  lowBuf = RingBuffer(period)
  trSum = 0
  prevClose = NaN
  logPeriod = log10(period)

On each bar (high, low, close, isNew):
  if !isNew: restore previous state

  // True Range
  if prevClose is valid:
    TR = max(high - low, |high - prevClose|, |low - prevClose|)
  else:
    TR = high - low

  // Rolling sum update
  if trBuf is full:
    trSum -= trBuf.Oldest
  trBuf.Add(TR)
  trSum += TR

  highBuf.Add(high)
  lowBuf.Add(low)

  // Channel width
  maxHigh = Max(highBuf)
  minLow = Min(lowBuf)
  channel = maxHigh - minLow

  // Choppiness Index
  if channel > 0 AND trSum > 0:
    CHOP = 100 × log10(trSum / channel) / logPeriod
  else:
    CHOP = 50  // neutral fallback

  prevClose = close
  output = Clamp(CHOP, 0, 100)
```

### Interpretation

| CHOP Value | Market Regime | Strategy Implication |
|------------|---------------|---------------------|
| > 61.8 | High choppiness | Avoid trend-following; favor range strategies |
| 38.2 - 61.8 | Ambiguous | Mixed conditions; reduced position sizing |
| < 38.2 | Low choppiness | Market trending; favor momentum/breakout strategies |

### Geometric Intuition

- **Perfect trend (straight line):** $\sum TR \approx \text{Channel}$, so $\log_{10}(1) = 0$, CHOP $\to 0$
- **Maximum chop (full traversal every bar):** $\sum TR \approx N \times \text{Channel}$, so $\log_{10}(N) / \log_{10}(N) = 1$, CHOP $\to 100$

### Non-Directional Property

CHOP is completely direction-agnostic. A strong uptrend and a strong downtrend produce identical low CHOP readings. Direction must be determined by a separate indicator (AMAT, ADX directional components, or simple price comparison).

## Resources

- Dreiss, E.W. — Choppiness Index (original development)
- PineScript reference: `chop.pine` in indicator directory
