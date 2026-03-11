# DMX: Directional Movement Index (Jurik)

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Dmx)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [dmx.pine](dmx.pine)                       |

- The DMX is Mark Jurik's modernized overhaul of Wilder's Directional Movement system, replacing the sluggish RMA smoothing with the Jurik Moving Ave...
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The DMX is Mark Jurik's modernized overhaul of Wilder's Directional Movement system, replacing the sluggish RMA smoothing with the Jurik Moving Average (JMA) to achieve faster trend detection with superior noise rejection. The core directional movement logic (+DM, -DM, True Range) is preserved faithfully from Wilder, but the three parallel smoothing passes use JMA's adaptive bandwidth instead of RMA's fixed $\alpha = 1/N$. The result is a directional indicator that reacts 3-5 bars earlier to trend changes than standard DMI while filtering out more noise during consolidation. Output is the difference between smoothed directional indicators: $DMX = DI^+ - DI^-$, positive for uptrends and negative for downtrends.

## Historical Context

Wilder's original ADX/DMI system (1978) is foundational but mathematically primitive — its RMA smoothing introduces substantial lag that delays trend detection. Jurik's contribution was recognizing that the directional movement decomposition itself is sound; only the smoothing pipeline needed upgrading. JMA is an adaptive filter that tracks signal closely during transitions (low lag) and smooths aggressively during stable periods (high noise reduction). This dynamic behavior means DMX signals trend changes significantly earlier than DMI without the whipsaw penalty typically associated with faster indicators. DMX is not available in standard TA libraries (TA-Lib, Skender, Tulip) since JMA is a proprietary algorithm. The QuanTAlib implementation uses its own JMA recreation.

## Architecture & Physics

### 1. Directional Movement (Wilder's Original)

$$\text{UpMove} = H_t - H_{t-1}, \quad \text{DownMove} = L_{t-1} - L_t$$

$$+DM = \begin{cases} \text{UpMove} & \text{if UpMove} > \text{DownMove and UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

$$-DM = \begin{cases} \text{DownMove} & \text{if DownMove} > \text{UpMove and DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

### 2. True Range

$$TR = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

### 3. JMA Smoothing (Replaces RMA)

Three parallel JMA filters replace Wilder's three RMA passes:

$$+DM_{\text{smooth}} = \text{JMA}(+DM, N)$$

$$-DM_{\text{smooth}} = \text{JMA}(-DM, N)$$

$$TR_{\text{smooth}} = \text{JMA}(TR, N)$$

### 4. Directional Indicators

$$DI^+ = 100 \times \frac{+DM_{\text{smooth}}}{TR_{\text{smooth}}}, \quad DI^- = 100 \times \frac{-DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

### 5. DMX Output

$$DMX = DI^+ - DI^-$$

Positive values indicate bullish directional dominance; negative values indicate bearish.

### 6. Complexity

- **Time:** $O(1)$ per bar — three JMA updates (each $O(1)$)
- **Space:** $O(1)$ — JMA maintains fixed-size internal state
- **Warmup:** $\approx N$ bars (JMA converges faster than RMA)

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

### Pseudo-code

```
Initialize:
  jmaPlusDM = new JMA(period)
  jmaMinusDM = new JMA(period)
  jmaTR = new JMA(period)
  prevHigh = prevLow = prevClose = NaN

On each bar (high, low, close, isNew):
  if !isNew: restore previous state

  // Wilder's directional movement decomposition
  TR = max(high - low, |high - prevClose|, |low - prevClose|)

  upMove = high - prevHigh
  downMove = prevLow - low

  +DM = (upMove > downMove AND upMove > 0) ? upMove : 0
  -DM = (downMove > upMove AND downMove > 0) ? downMove : 0

  // Jurik smoothing (replaces Wilder's RMA)
  smoothPlusDM = jmaPlusDM.Update(+DM)
  smoothMinusDM = jmaMinusDM.Update(-DM)
  smoothTR = jmaTR.Update(TR)

  // Directional indicators
  if smoothTR > 0:
    DI_plus = 100 × smoothPlusDM / smoothTR
    DI_minus = 100 × smoothMinusDM / smoothTR
  else:
    DI_plus = DI_minus = 0

  DMX = DI_plus - DI_minus

  prevHigh = high
  prevLow = low
  prevClose = close
  output = DMX
```

### DMX vs DMI Comparison

| Property | DMI (Wilder) | DMX (Jurik) |
|----------|-------------|-------------|
| Smoothing | RMA ($\alpha = 1/N$) | JMA (adaptive) |
| Lag | $\approx N$ bars | $\approx N/2$ bars |
| Whipsaw rejection | Moderate | High |
| Available in TA-Lib | Yes | No |
| Overshoot | Low | Can overshoot in extreme volatility |

### Period Selection

Because JMA is more efficient than RMA, slightly longer periods (e.g., 20 instead of 14) can be used without incurring a lag penalty, producing smoother results while maintaining responsiveness.

## Performance Profile

### Operation Count (Streaming Mode)

DMX (Directional Movement Index) computes +DM and −DM only, without ADX smoothing — a lighter version of ADX.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB × 4 (TR components + DM moves) | 4 | 1 | 4 |
| ABS × 2 (absolute TR components) | 2 | 1 | 2 |
| MAX × 2 (TR max) | 2 | 1 | 2 |
| CMP × 2 (DM directional guards) | 2 | 1 | 2 |
| FMA × 2 (RMA smooth +DM, −DM) | 2 | 4 | 8 |
| FMA × 1 (RMA smooth TR) | 1 | 4 | 4 |
| DIV × 2 (+DI, −DI from smoothed values) | 2 | 15 | 30 |
| MUL × 2 (scale to 100) | 2 | 3 | 6 |
| **Total** | **19** | — | **~58 cycles** |

DMX skips the DX/ADX second smoothing phase. ~58 cycles per bar vs ~79 for full ADX.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| TR/DM computation | Yes | VSUBPD + VABSPD + VMAXPD + VCMPPD |
| RMA smoothing × 3 | **No** | Recursive IIR — sequential |
| DI scaling | Yes | VDIVPD + VMULPD after RMA pass |

Same constraint as ADX: the recursive RMA smoothing blocks cross-bar SIMD.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | FMA-precise Wilder smoothing |
| **Timeliness** | 6/10 | N-bar warmup only (vs 2N for ADX); responds faster |
| **Smoothness** | 7/10 | Single RMA layer; less smooth than full ADX |
| **Noise Rejection** | 7/10 | One smoothing pass sufficient for directional signals |

## Resources

- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- Jurik, M. — JMA adaptive smoothing methodology
- PineScript reference: `dmx.pine` in indicator directory
