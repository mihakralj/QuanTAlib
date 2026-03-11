# ALLIGATOR: Williams Alligator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `jawPeriod`, `jawOffset`, `teethPeriod`, `teethOffset`, `lipsPeriod`, `lipsOffset`                      |
| **Outputs**      | Multiple series (Jaw, Teeth, Lips)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `Math.Max(Math.Max(jawPeriod, teethPeriod), lipsPeriod)` bars                          |
| **PineScript**   | [alligator.pine](alligator.pine)                       |

- The Williams Alligator is a trend-following system that uses three Smoothed Moving Averages (SMMA/RMA) with different periods and forward display o...
- Parameterized by `jawperiod`, `jawoffset`, `teethperiod`, `teethoffset`, `lipsperiod`, `lipsoffset`.
- Output range: Varies (see docs).
- Requires `Math.Max(Math.Max(jawPeriod, teethPeriod), lipsPeriod)` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Williams Alligator is a trend-following system that uses three Smoothed Moving Averages (SMMA/RMA) with different periods and forward display offsets to visualize market phases. The Jaw (13-period, offset 8), Teeth (8-period, offset 5), and Lips (5-period, offset 3) create a layered structure where intertwined lines indicate consolidation ("sleeping") and separated, aligned lines indicate trending conditions ("eating"). The metaphor maps directly to position management: stay out when the alligator sleeps, ride when it eats. Each line uses Wilder's smoothing ($\alpha = 1/N$), which is heavier than standard EMA, providing superior noise rejection at the cost of additional lag.

## Historical Context

Bill Williams introduced the Alligator in *Trading Chaos* (1995) as part of his broader chaos theory framework for trading. The metaphor is biological: markets alternate between feeding (trending) and sleeping (ranging) states, and the three moving averages at different timescales reveal which phase is active. The Jaw represents the long-term balance line (the "blue line" on most charting platforms), the Teeth the intermediate balance (red), and the Lips the short-term momentum (green). Williams paired the Alligator with Fractals for entry timing and the Awesome Oscillator for momentum confirmation, creating a complete systematic framework. The forward offsets are display-only transformations — the underlying SMMA calculation uses the current bar's price — but they create visual separation that makes trend direction immediately apparent on charts.

## Architecture & Physics

### 1. Three-Line SMMA Structure

Each line is an independent SMMA (Wilder's RMA) with $\alpha = 1/N$:

| Line | Period ($N$) | Display Offset | Role |
|------|-------------|----------------|------|
| Jaw | 13 | 8 bars forward | Long-term trend (slowest) |
| Teeth | 8 | 5 bars forward | Intermediate trend |
| Lips | 5 | 3 bars forward | Short-term momentum (fastest) |

### 2. SMMA Recursion

$$\text{SMMA}_t = \frac{1}{N} \cdot P_t + \frac{N-1}{N} \cdot \text{SMMA}_{t-1}$$

Equivalently using FMA notation:

$$\text{SMMA}_t = \text{FMA}(\text{SMMA}_{t-1},\; \tfrac{N-1}{N},\; \tfrac{1}{N} \cdot P_t)$$

### 3. Default Input

Typical price (HLC/3):

$$\text{Source} = \frac{H + L + C}{3}$$

### 4. Forward Offset

The offsets shift plotted values forward in time for display purposes only. The calculation itself is not shifted — the current SMMA value represents the current bar's computation.

### 5. Complexity

- **Time:** $O(1)$ per bar — three parallel SMMA updates
- **Space:** $O(1)$ — three scalar states (no buffers needed)
- **Warmup:** 13 bars (Jaw period, the slowest line)

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N_j$ | jawPeriod | 13 | $N_j \geq 1$ |
| $O_j$ | jawOffset | 8 | $O_j \geq 0$ |
| $N_t$ | teethPeriod | 8 | $N_t \geq 1$ |
| $O_t$ | teethOffset | 5 | $O_t \geq 0$ |
| $N_l$ | lipsPeriod | 5 | $N_l \geq 1$ |
| $O_l$ | lipsOffset | 3 | $O_l \geq 0$ |

### Pseudo-code

```
Initialize:
  α_jaw = 1 / jawPeriod
  α_teeth = 1 / teethPeriod
  α_lips = 1 / lipsPeriod
  jaw = teeth = lips = first source value
  e_jaw = e_teeth = e_lips = 1.0   // bias compensation

On each bar (high, low, close, isNew):
  if !isNew: restore previous state

  source = (high + low + close) / 3.0

  // SMMA updates with bias compensation
  jaw = FMA(jaw, 1 - α_jaw, α_jaw × source)
  e_jaw = e_jaw × (1 - α_jaw)
  jaw_compensated = jaw / (1 - e_jaw)

  teeth = FMA(teeth, 1 - α_teeth, α_teeth × source)
  e_teeth = e_teeth × (1 - α_teeth)
  teeth_compensated = teeth / (1 - e_teeth)

  lips = FMA(lips, 1 - α_lips, α_lips × source)
  e_lips = e_lips × (1 - α_lips)
  lips_compensated = lips / (1 - e_lips)

  output:
    Jaw   = jaw_compensated   (plot at bar + jawOffset)
    Teeth = teeth_compensated (plot at bar + teethOffset)
    Lips  = lips_compensated  (plot at bar + lipsOffset)
```

### Market Phase Detection

| Phase | Line Configuration | Action |
|-------|-------------------|--------|
| Sleeping | Lines intertwined, crossing | No position; market is consolidating |
| Awakening | Lines begin separating | Prepare for entry |
| Eating (bullish) | Lips > Teeth > Jaw, all rising | Long; trend is strong |
| Eating (bearish) | Lips < Teeth < Jaw, all falling | Short; trend is strong |
| Sated | Lines converging | Take profits; trend weakening |

### Output Interpretation

- **Three values per bar:** Jaw, Teeth, Lips (each a smoothed price level)
- **Separation width:** Proportional to trend strength
- **Line ordering:** Determines trend direction
- **Intertwining:** Signals consolidation — the highest-probability losing zone for trend followers

## Performance Profile

### Operation Count (Streaming Mode)

The Alligator runs three SMMA (Wilder RMA) instances with different periods and bar shifts.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Median price (H+L)/2 | 2 | 1 | 2 |
| FMA × 3 (SMMA jaw, teeth, lips updates) | 3 | 4 | 12 |
| RingBuffer writes × 3 (shift lag storage) | 3 | 1 | 3 |
| RingBuffer reads × 3 (shifted output) | 3 | 1 | 3 |
| **Total** | **11** | — | **~20 cycles** |

Three independent SMMA streams run in parallel with look-ahead shift buffers. For default periods (13/8/5) with shifts (8/5/3): warmup is 13+8 = 21 bars. Steady state: ~20 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Median price computation | Yes | VADDPD + VMULPD (×0.5) |
| SMMA (Wilder RMA) | **No** | Recursive IIR — sequential per stream |
| Shifted output reads | Yes | Array offset reads, no dependencies |

Three independent recursive streams. No cross-stream dependencies, but each stream is itself sequential. Cannot batch-vectorize across bars, but the three streams can run on separate cores.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | FMA-precise RMA; independent streams eliminate cross-contamination |
| **Timeliness** | 4/10 | Longest jaw (21 bars warmup + 8-bar shift = 29 bars before output) |
| **Smoothness** | 9/10 | Wilder smoothing on all three lines; Williams designed for low noise |
| **Noise Rejection** | 8/10 | Triple staggered RMAs with shifts effectively filter market noise |

## Resources

- Williams, B. — *Trading Chaos* (John Wiley & Sons, 1995)
- Williams, B. — *New Trading Dimensions* (John Wiley & Sons, 1998)
- PineScript reference: `alligator.pine` in indicator directory
