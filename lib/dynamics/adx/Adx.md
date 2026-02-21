# ADX: Average Directional Index

The Average Directional Index is the industry-standard measure of trend strength, ignoring direction entirely to focus on the velocity of price expansion. Wilder's pipeline decomposes range into directional movement (+DM, -DM), normalizes against True Range to produce directional indicators (+DI, -DI), derives a directional index (DX) from their ratio, then smooths DX with a final RMA pass. The double-smoothed architecture creates significant lag but exceptional noise rejection, making ADX a regime filter rather than a timing tool. Output is unbounded above 0, with readings above 25 conventionally indicating trending conditions and below 20 indicating choppy markets.

## Historical Context

J. Welles Wilder Jr. introduced ADX in *New Concepts in Technical Trading Systems* (1978). Wilder was a mechanical engineer, and the design reflects that discipline: a machine built from modular components where each stage has a defined transfer function. The indicator does not attempt to predict direction. It answers a single question — "Is the market trending?" — and answers it with ruthless indifference to which way.

ADX is a "derivative of a derivative." The calculation pipeline is deep: price range decomposes into directional movement, directional movement normalizes into directional indicators, directional indicators compress into DX, and DX smooths into ADX. Each layer strips noise at the cost of latency. A "cold" start requires at least $2N$ bars to produce statistically meaningful output, and often $3\text{--}4N$ bars to converge to within 4 decimal places of a mature series. The QuanTAlib implementation tracks warmup state explicitly — garbage is not published during convergence.

## Architecture & Physics

### 1. Directional Movement

Today's range expansion is compared to yesterday's:

$$\text{UpMove} = H_t - H_{t-1}$$

$$\text{DownMove} = L_{t-1} - L_t$$

$$+DM = \begin{cases} \text{UpMove} & \text{if UpMove} > \text{DownMove and UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

$$-DM = \begin{cases} \text{DownMove} & \text{if DownMove} > \text{UpMove and DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

Only one of +DM or -DM can be non-zero per bar — the dominant direction wins.

### 2. Wilder Smoothing (RMA)

All three series (+DM, -DM, TR) are smoothed using Wilder's Moving Average with $\alpha = 1/N$:

$$+DM_{\text{smooth}} = \text{RMA}(+DM, N)$$

$$-DM_{\text{smooth}} = \text{RMA}(-DM, N)$$

$$TR_{\text{smooth}} = \text{RMA}(TR, N)$$

### 3. Directional Indicators

Normalize smoothed movement against smoothed volatility:

$$+DI = 100 \times \frac{+DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

$$-DI = 100 \times \frac{-DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

### 4. Directional Index and ADX

$$DX = 100 \times \frac{|+DI - (-DI)|}{+DI + (-DI)}$$

$$ADX = \text{RMA}(DX, N)$$

### 5. Complexity

- **Time:** $O(1)$ per bar — all RMA updates are recursive
- **Space:** $O(1)$ — scalar state only (no buffers)
- **Warmup:** $\approx 2N$ bars minimum; $3\text{--}4N$ for full convergence

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

### Pseudo-code

```
Initialize:
  α = 1 / period
  smoothPlusDM = smoothMinusDM = smoothTR = 0
  adx = 0
  prevHigh = prevLow = NaN
  bar_count = 0

On each bar (high, low, close, isNew):
  if !isNew: restore previous state

  TR = max(high - low, |high - prevClose|, |low - prevClose|)

  upMove = high - prevHigh
  downMove = prevLow - low

  +DM = (upMove > downMove AND upMove > 0) ? upMove : 0
  -DM = (downMove > upMove AND downMove > 0) ? downMove : 0

  // Wilder smoothing (RMA)
  smoothPlusDM = FMA(smoothPlusDM, 1 - α, α × +DM)
  smoothMinusDM = FMA(smoothMinusDM, 1 - α, α × -DM)
  smoothTR = FMA(smoothTR, 1 - α, α × TR)

  // Directional Indicators
  +DI = 100 × smoothPlusDM / smoothTR
  -DI = 100 × smoothMinusDM / smoothTR

  // Directional Index
  diSum = +DI + -DI
  DX = diSum > 0 ? 100 × |+DI - -DI| / diSum : 0

  // Final smoothing
  ADX = FMA(ADX, 1 - α, α × DX)

  prevHigh = high
  prevLow = low
  prevClose = close
  output = ADX
```

### The Stability Problem

Because ADX relies on recursive RMA at multiple stages, convergence is slow. Period 14 needs roughly 40-56 bars before matching TA-Lib to 4 decimal places. The first $2N$ values are mathematically correct but statistically immature — treat them as warmup artifacts.

### ADX Interpretation

| ADX Value | Market Regime |
|-----------|---------------|
| < 20 | Absent or weak trend (range-bound) |
| 20–25 | Emerging trend |
| 25–50 | Strong trend |
| 50–75 | Very strong trend |
| > 75 | Extremely strong (rare) |

ADX peaks *after* the trend has exhausted — it is a lagging indicator of trend strength, not a leading indicator of reversal.

## Resources

- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- PineScript reference: `adx.pine` in indicator directory
