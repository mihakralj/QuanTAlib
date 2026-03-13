# MINUS_DI: Minus Directional Indicator

> *-DI isolates downward directional thrust as a fraction of true range — the bearish arm of Wilder's directional system.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series                       |
| **Output range** | 0 to 100                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [minusdi.pine](minusdi.pine)                       |

- The Minus Directional Indicator measures the strength of downward price movement relative to true range.
- **Similar:** [PlusDi](../plusdi/PlusDi.md), [ADX](../adx/Adx.md) | **Complementary:** ADX for trend strength | **Trading note:** Minus Directional Indicator; measures downward movement strength. Part of Wilder's DM system.
- Validated against TA-Lib, Skender, and Dx equivalence.

The Minus Directional Indicator (-DI) is one component of J. Welles Wilder Jr.'s Directional Movement System. It quantifies the fraction of recent true range attributable to downward price extension. The computation smooths both -DM (minus directional movement) and TR (true range) with Wilder's RMA ($\alpha = 1/N$), then divides: $-DI = 100 \times \text{Smooth}(-DM) / \text{Smooth}(TR)$. When -DI rises, downward price pressure is increasing. When -DI crosses above +DI, it signals a potential bearish trend. The -DI line is commonly plotted alongside +DI to visualize directional balance.

## Historical Context

J. Welles Wilder Jr. introduced the Directional Movement System in *New Concepts in Technical Trading Systems* (1978). The system decomposes price range into directional components. +DI and -DI are the normalized indicators from which DX and ADX are derived. While most traders focus on ADX for trend strength, +DI and -DI remain essential for determining trend *direction* — a bearish signal occurs when -DI crosses above +DI, bullish when +DI crosses above -DI.

## Architecture & Physics

### 1. Minus Directional Movement

$$\text{UpMove} = H_t - H_{t-1}, \quad \text{DownMove} = L_{t-1} - L_t$$

$$-DM = \begin{cases} \text{DownMove} & \text{if DownMove} > \text{UpMove and DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

### 2. True Range

$$TR = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

### 3. Wilder Smoothing (RMA)

$$-DM_{\text{smooth}} = \text{RMA}(-DM, N), \quad TR_{\text{smooth}} = \text{RMA}(TR, N)$$

### 4. Minus Directional Indicator

$$-DI = 100 \times \frac{-DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

When $TR_{\text{smooth}} = 0$ (no price movement), -DI = 0.

### 5. Complexity

- **Time:** $O(1)$ per bar — all RMA updates are recursive
- **Space:** $O(1)$ — scalar state only (delegates to Dx)
- **Warmup:** $N$ bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

### Interpretation

| -DI Value | Signal |
|-----------|--------|
| Rising -DI | Strengthening downward movement |
| -DI > +DI | Bears dominate; potential downtrend |
| -DI crossover above +DI | Bearish signal |
| High -DI (>40) | Strong downward momentum |

-DI measures directional *strength*, not absolute direction. Compare +DI vs -DI for directional bias: if $-DI > +DI$, the trend is down.

## Performance Profile

### Operation Count (Streaming Mode)

-DI is a thin wrapper around Dx. The per-bar cost is identical to Dx (one property extraction after Dx completes its update).

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Dx.Update (full pipeline) | 1 | 75 | 75 |
| Property extraction | 1 | 1 | 1 |
| **Total** | **2** | — | **~76 cycles** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact Dx delegation; FMA-precise RMA smoothing |
| **Timeliness** | 7/10 | N-bar warmup; responds to bar-level changes |
| **Smoothness** | 7/10 | Single RMA layer; moderate noise suppression |
| **Noise Rejection** | 7/10 | Wilder smoothing filters transient spikes |

## Resources

- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- PineScript reference: `minusdi.pine` in indicator directory