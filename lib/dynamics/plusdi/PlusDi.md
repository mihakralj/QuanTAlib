# PLUS_DI: Plus Directional Indicator

> *+DI isolates upward directional thrust as a fraction of true range — the bullish arm of Wilder's directional system.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series                       |
| **Output range** | 0 to 100                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [plusdi.pine](plusdi.pine)                       |

- The Plus Directional Indicator measures the strength of upward price movement relative to true range.
- Parameterized by `period` (default 14).
- Output range: 0 to 100.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Dx equivalence.

The Plus Directional Indicator (+DI) is one component of J. Welles Wilder Jr.'s Directional Movement System. It quantifies the fraction of recent true range attributable to upward price extension. The computation smooths both +DM (plus directional movement) and TR (true range) with Wilder's RMA ($\alpha = 1/N$), then divides: $+DI = 100 \times \text{Smooth}(+DM) / \text{Smooth}(TR)$. When +DI rises, upward price pressure is increasing. When +DI crosses above -DI, it signals a potential bullish trend. The +DI line is commonly plotted alongside -DI to visualize directional balance.

## Historical Context

J. Welles Wilder Jr. introduced the Directional Movement System in *New Concepts in Technical Trading Systems* (1978). The system decomposes price range into directional components. +DI and -DI are the normalized indicators from which DX and ADX are derived. While most traders focus on ADX for trend strength, +DI and -DI remain essential for determining trend *direction* — a bullish signal occurs when +DI crosses above -DI, bearish when -DI crosses above +DI.

## Architecture & Physics

### 1. Plus Directional Movement

$$\text{UpMove} = H_t - H_{t-1}, \quad \text{DownMove} = L_{t-1} - L_t$$

$$+DM = \begin{cases} \text{UpMove} & \text{if UpMove} > \text{DownMove and UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

### 2. True Range

$$TR = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

### 3. Wilder Smoothing (RMA)

$$+DM_{\text{smooth}} = \text{RMA}(+DM, N), \quad TR_{\text{smooth}} = \text{RMA}(TR, N)$$

### 4. Plus Directional Indicator

$$+DI = 100 \times \frac{+DM_{\text{smooth}}}{TR_{\text{smooth}}}$$

When $TR_{\text{smooth}} = 0$ (no price movement), +DI = 0.

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

| +DI Value | Signal |
|-----------|--------|
| Rising +DI | Strengthening upward movement |
| +DI > -DI | Bulls dominate; potential uptrend |
| +DI crossover above -DI | Bullish signal |
| High +DI (>40) | Strong upward momentum |

+DI measures directional *strength*, not absolute direction. Compare +DI vs -DI for directional bias: if $+DI > -DI$, the trend is up.

## Performance Profile

### Operation Count (Streaming Mode)

+DI is a thin wrapper around Dx. The per-bar cost is identical to Dx (one property extraction after Dx completes its update).

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
- PineScript reference: `plusdi.pine` in indicator directory
