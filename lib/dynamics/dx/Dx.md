# DX: Directional Movement Index

The Directional Movement Index is the raw, unsmoothed measure of trend strength from Wilder's directional movement system. It decomposes price expansion into +DM and -DM, normalizes against True Range using RMA smoothing to produce +DI and -DI, then computes the ratio $DX = 100 \times |{+DI - {-DI}}| / ({+DI + {-DI}})$. Unlike ADX, which applies a final RMA pass to DX, the raw DX responds immediately to changes in directional dominance — making it noisier but approximately one full period faster. Output ranges from 0 to 100, where high values indicate strong directional movement regardless of up/down direction. DX is the building block from which ADX is derived.

## Historical Context

J. Welles Wilder Jr. introduced the complete Directional Movement System in *New Concepts in Technical Trading Systems* (1978). The system's pipeline produces several intermediate values — +DM, -DM, TR, +DI, -DI, DX — before reaching the final ADX. Most traders skip directly to ADX, but DX occupies a useful middle ground: it contains all the directional normalization logic (the hard part) without the final smoothing layer (which adds lag). For traders who can tolerate more noise in exchange for faster response, DX provides trend strength signals roughly $N$ bars ahead of ADX. The tradeoff is straightforward: DX spikes on volatile bars and can produce false readings during whipsaw, while ADX absorbs these transients through its additional RMA pass.

## Architecture & Physics

### 1. Directional Movement

$$\text{UpMove} = H_t - H_{t-1}, \quad \text{DownMove} = L_{t-1} - L_t$$

$$+DM = \begin{cases} \text{UpMove} & \text{if UpMove} > \text{DownMove and UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

$$-DM = \begin{cases} \text{DownMove} & \text{if DownMove} > \text{UpMove and DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

### 2. True Range

$$TR = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

### 3. Wilder Smoothing (RMA)

All three series use Wilder's smoothing with $\alpha = 1/N$:

$$+DM_{\text{smooth}} = \text{RMA}(+DM, N)$$

$$-DM_{\text{smooth}} = \text{RMA}(-DM, N)$$

$$TR_{\text{smooth}} = \text{RMA}(TR, N)$$

### 4. Directional Indicators

$$+DI = 100 \times \frac{+DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

$$-DI = 100 \times \frac{-DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

### 5. DX (No Final Smoothing)

$$DX = 100 \times \frac{|+DI - (-DI)|}{+DI + (-DI)}$$

When $+DI + (-DI) = 0$ (no directional movement), DX = 0.

### 6. Complexity

- **Time:** $O(1)$ per bar — all RMA updates are recursive
- **Space:** $O(1)$ — scalar state only
- **Warmup:** $N$ bars

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
  prevHigh = prevLow = prevClose = NaN

On each bar (high, low, close, isNew):
  if !isNew: restore previous state

  // True Range
  TR = max(high - low, |high - prevClose|, |low - prevClose|)

  upMove = high - prevHigh
  downMove = prevLow - low

  +DM = (upMove > downMove AND upMove > 0) ? upMove : 0
  -DM = (downMove > upMove AND downMove > 0) ? downMove : 0

  // Wilder smoothing
  smoothPlusDM = FMA(smoothPlusDM, 1 - α, α × +DM)
  smoothMinusDM = FMA(smoothMinusDM, 1 - α, α × -DM)
  smoothTR = FMA(smoothTR, 1 - α, α × TR)

  // Directional Indicators
  +DI = smoothTR > 0 ? 100 × smoothPlusDM / smoothTR : 0
  -DI = smoothTR > 0 ? 100 × smoothMinusDM / smoothTR : 0

  // DX (raw, no final RMA)
  diSum = +DI + -DI
  DX = diSum > 0 ? 100 × |+DI - -DI| / diSum : 0

  prevHigh = high
  prevLow = low
  prevClose = close

  output:
    DX = DX          // trend strength (0-100)
    DiPlus = +DI     // bullish directional indicator
    DiMinus = -DI    // bearish directional indicator
```

### DX vs ADX

| Property | DX | ADX |
|----------|-----|------|
| Smoothing | RMA on components only | RMA on components + RMA on DX |
| Response | Immediate to bar-level changes | Lagged by $\approx N$ bars |
| Noise | High; can spike on volatile bars | Low; smooth, stable signal |
| Use case | Fast trend detection, signal generation | Regime classification, filter |

### Interpretation

| DX Value | Trend Strength |
|----------|----------------|
| 0-15 | No meaningful trend |
| 15-25 | Developing trend |
| 25-50 | Strong trend |
| 50-75 | Very strong trend |
| 75-100 | Extreme (rare, usually transient) |

DX measures trend *strength*, not direction. Direction is determined by comparing +DI vs -DI: if $+DI > -DI$, the trend is up; if $-DI > +DI$, the trend is down. DI crossovers signal potential trend reversals.

## Performance Profile

### Operation Count (Streaming Mode)

DX is an intermediate step in the ADX calculation: it computes the directional movement index without the final ADX smoothing pass.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB × 5 (TR, DM moves) | 5 | 1 | 5 |
| ABS × 2 (TR components) | 2 | 1 | 2 |
| MAX × 2 (TR) | 2 | 1 | 2 |
| CMP × 2 (DM guards) | 2 | 1 | 2 |
| FMA × 3 (RMA TR, +DM, −DM) | 3 | 4 | 12 |
| DIV × 2 (+DI, −DI) | 2 | 15 | 30 |
| MUL × 2 (×100) | 2 | 3 | 6 |
| ABS + ADD + DIV (DX formula) | 3 | 16 | 16 |
| **Total** | **23** | — | **~75 cycles** |

DX requires N bars warmup (vs 2N for ADX). ~75 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| TR/DM initial computation | Yes | VSUBPD + VABSPD + VMAXPD |
| RMA smoothing × 3 | **No** | Recursive IIR |
| DX formula | Yes | VABSPD + VADDPD + VDIVPD post-RMA |

Same RMA bottleneck as ADX. The DX formula itself is fully vectorizable.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | FMA smoothing; exact TR computation |
| **Timeliness** | 7/10 | N-bar warmup; more responsive than ADX |
| **Smoothness** | 6/10 | Raw DX is noisier than ADX; typically used as input to ADX |
| **Noise Rejection** | 6/10 | Single RMA layer; moderate noise suppression |

## Resources

- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- PineScript reference: `dx.pine` in indicator directory
