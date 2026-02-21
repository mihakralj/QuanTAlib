# HT_TRENDMODE: Hilbert Transform Trend vs Cycle Mode

The Hilbert Transform Trend Mode indicator is a binary regime classifier that determines whether price action is dominated by trending behavior (output = 1) or cyclical/mean-reverting behavior (output = 0). It uses the full Ehlers Hilbert Transform pipeline — 4-bar WMA smoothing, Hilbert FIR filters, homodyne discriminator for period estimation, DC phase extraction, and SineWave indicators — then applies four decision criteria to classify the current regime. The implementation follows TA-Lib's Ehlers-faithful algorithm from the February 2002 publication. Output is discrete {0, 1}, making it a direct strategy selector: deploy trend-following logic when mode = 1, and mean-reversion logic when mode = 0.

## Historical Context

John Ehlers developed the Trend Mode indicator as part of his cycle analysis toolkit, published in "The Instantaneous Trendline" (February 2002) and expanded in *MESA and Trading Market Cycles* (2002). Ehlers recognized that traders face two fundamentally different market regimes requiring opposite strategies. Applying a trend-following system to a cycling market produces losses, and applying a mean-reversion system to a trending market produces losses. The Hilbert Transform provides the mathematical machinery to distinguish these states by analyzing the phase behavior of the dominant cycle. When phase advances at a regular rate (consistent with a sinusoidal cycle), the market is in cycle mode. When phase rate becomes irregular or price deviates significantly from its trendline, the market is trending. The four-criteria decision logic prevents rapid mode flipping during transitional periods by requiring sustained evidence before declaring a regime change.

## Architecture & Physics

### 1. Hilbert Transform Core

The same pipeline as HT_DCPERIOD and HT_SINE:

$$\text{smooth} = \frac{4P_t + 3P_{t-1} + 2P_{t-2} + P_{t-3}}{10}$$

Hilbert FIR filters extract InPhase and Quadrature components, which feed the homodyne discriminator for period estimation:

$$Re = 0.2(I_2 \cdot I_{2,t-1} + Q_2 \cdot Q_{2,t-1}) + 0.8 \cdot Re_{t-1}$$

$$Im = 0.2(I_2 \cdot Q_{2,t-1} - Q_2 \cdot I_{2,t-1}) + 0.8 \cdot Im_{t-1}$$

$$\text{period} = \frac{360}{\arctan(Im/Re) \times \frac{180}{\pi}}$$

$$\text{smoothPeriod} = 0.33 \times \text{period} + 0.67 \times \text{smoothPeriod}_{t-1}$$

### 2. DC Phase and SineWave

DFT accumulation over the dominant cycle period extracts the DC phase:

$$\text{dcPhase} = \arctan\!\left(\frac{\sum \sin(\omega i) \cdot \text{smooth}_i}{\sum \cos(\omega i) \cdot \text{smooth}_i}\right) + 90° + \text{lagComp}$$

$$\text{sine} = \sin(\text{dcPhase}), \quad \text{leadSine} = \sin(\text{dcPhase} + 45°)$$

### 3. Trendline

An SMA over the dominant cycle period, further smoothed with a 4-bar WMA:

$$\text{sma} = \text{Average}(\text{price}, \lfloor\text{dcPeriod}\rfloor)$$

$$\text{trendline} = \frac{4 \cdot \text{sma}_0 + 3 \cdot \text{sma}_1 + 2 \cdot \text{sma}_2 + \text{sma}_3}{10}$$

### 4. Four-Criteria Decision Logic

```
trend = 1  (assume trend by default)

Criterion 1: SineWave crossing resets counter
  if sine crosses leadSine → daysInTrend = 0, trend = 0

Criterion 2: Duration threshold
  daysInTrend++
  if daysInTrend < 0.5 × smoothPeriod → trend = 0

Criterion 3: Phase rate check
  phaseChange = dcPhase - prevDcPhase
  expected = 360 / smoothPeriod
  if 0.67 × expected < phaseChange < 1.5 × expected → trend = 0

Criterion 4: Price deviation override
  if |smoothPrice - trendline| / trendline ≥ 0.015 → trend = 1
```

### 5. Complexity

- **Time:** $O(P)$ per bar for the SMA over dominant cycle period; Hilbert pipeline is $O(1)$
- **Space:** $O(P_{\max})$ — circular buffers for price history and Hilbert state ($P_{\max} = 50$)
- **Warmup:** 63 bars (TA-Lib compatible)

## Mathematical Foundation

### Parameters

No user-configurable parameters. The algorithm self-tunes based on the detected dominant cycle period (clamped to 6-50 bars).

### Pseudo-code

```
Initialize:
  circBuffer = array for Hilbert state
  smoothPrice = priceHistory = arrays
  daysInTrend = 0
  smoothPeriod = 0
  prevDcPhase = 0
  bar_count = 0

On each bar (price, isNew):
  if !isNew: restore previous state

  // Step 1: 4-bar WMA smooth
  smooth = (4×price[0] + 3×price[1] + 2×price[2] + price[3]) / 10

  // Step 2: Hilbert Transform (FIR filters)
  detrender = HilbertFIR(smooth) × adjustedBandwidth
  Q1 = HilbertFIR(detrender) × adjustedBandwidth
  I1 = detrender[3]

  // Step 3: Phasor rotation
  I2 = I1 - jQ_prev;  Q2 = Q1 + jI_prev
  I2 = 0.2×I2 + 0.8×I2_prev;  Q2 = 0.2×Q2 + 0.8×Q2_prev

  // Step 4: Homodyne discriminator → period
  Re = 0.2×(I2×I2_prev + Q2×Q2_prev) + 0.8×Re_prev
  Im = 0.2×(I2×Q2_prev - Q2×I2_prev) + 0.8×Im_prev
  period = clamp(360 / (atan(Im/Re) × RAD2DEG), 6, 50)
  smoothPeriod = 0.33×period + 0.67×smoothPeriod_prev

  // Step 5: DC Phase via DFT
  dcPeriodInt = floor(smoothPeriod + 0.5)
  realPart = Σ sin(i × 360/dcPeriodInt) × smooth[i]  for i=0..dcPeriodInt-1
  imagPart = Σ cos(i × 360/dcPeriodInt) × smooth[i]
  dcPhase = atan(realPart/imagPart)×RAD2DEG + 90 + lagCompensation

  // Step 6: SineWave indicators
  sine = sin(dcPhase × DEG2RAD)
  leadSine = sin((dcPhase + 45) × DEG2RAD)

  // Step 7: Trendline (SMA smoothed with WMA)
  sma = average(price, dcPeriodInt)
  trendline = (4×sma[0] + 3×sma[1] + 2×sma[2] + sma[3]) / 10

  // Step 8: Four-criteria trend decision
  trend = 1
  if sine crosses leadSine: daysInTrend = 0; trend = 0
  daysInTrend++
  if daysInTrend < 0.5 × smoothPeriod: trend = 0
  phaseChange = dcPhase - prevDcPhase
  expected = 360 / smoothPeriod
  if phaseChange > 0.67×expected AND phaseChange < 1.5×expected: trend = 0
  if |smooth - trendline| / trendline >= 0.015: trend = 1

  prevDcPhase = dcPhase
  output = trend  // 1 = trending, 0 = cycling
```

### Decision Criteria Summary

| Criterion | Purpose |
|-----------|---------|
| SineWave crossing | Resets trend counter — new cycle detected |
| Duration threshold | Requires sustained trending before declaration |
| Phase rate check | Normal phase advance indicates cycle mode |
| Price deviation | Large deviation from trendline forces trend mode |

### Mode Transition Patterns

| Pattern | Interpretation |
|---------|---------------|
| 0→1 after breakout | Trend confirmed; deploy momentum strategy |
| 1→0 at extremes | Cycle started; switch to mean-reversion |
| Long run of 1s | Strong, sustained trend |
| Rapid 0/1 flipping | Transitional/choppy — reduce exposure |

## Resources

- Ehlers, J.F. — "The Instantaneous Trendline" (February 2002)
- Ehlers, J.F. — *MESA and Trading Market Cycles* (John Wiley & Sons, 2002)
- Ehlers, J.F. — *Rocket Science for Traders* (John Wiley & Sons, 2001)
- PineScript reference: `ht_trendmode.pine` in indicator directory
