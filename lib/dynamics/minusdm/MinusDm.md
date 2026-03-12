# MINUS_DM: Minus Directional Movement

> *-DM captures the raw downward price extension, smoothed by Wilder's RMA — the building block before normalization to -DI.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series                       |
| **Output range** | ≥ 0 (price units)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [minusdm.pine](minusdm.pine)                       |

- Minus Directional Movement outputs the Wilder-smoothed downward directional movement in price units.
- Parameterized by `period` (default 14).
- Output range: ≥ 0 (price units, scales with instrument).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib and Dx equivalence.

Minus Directional Movement (-DM) measures the magnitude of downward price movement, smoothed using Wilder's method. Unlike -DI which normalizes by true range to produce a percentage, -DM outputs raw smoothed values in price units. -DM captures when the previous bar's low exceeds the current bar's low by more than the current bar's high exceeds the previous bar's high. It is the raw building block of the Directional Movement System — the unnormalized signal before division by True Range converts it to -DI.

## Historical Context

J. Welles Wilder Jr. designed the Directional Movement System as a pipeline in *New Concepts in Technical Trading Systems* (1978). -DM is the first computational stage for downward movement: a binary event detector that fires when downward range expansion dominates. Wilder's insight was that direction should be measured by range *extension*, not by close-to-close returns. A bar that pushes to a new low by more than it pushes to a new high registers as negative directional movement. The smoothed -DM series shows how much downward thrust is being sustained over the lookback period, in absolute price units.

## Architecture & Physics

### 1. Minus Directional Movement (Raw)

$$\text{UpMove} = H_t - H_{t-1}, \quad \text{DownMove} = L_{t-1} - L_t$$

$$-DM = \begin{cases} \text{DownMove} & \text{if DownMove} > \text{UpMove and DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}$$

Only one of +DM or -DM can be non-zero per bar — the dominant direction wins.

### 2. Wilder Smoothing (RMA)

$$-DM_{\text{smooth}} = \text{RMA}(-DM, N), \quad \alpha = 1/N$$

### 3. Complexity

- **Time:** $O(1)$ per bar — recursive RMA update
- **Space:** $O(1)$ — scalar state only (delegates to Dx)
- **Warmup:** $N$ bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

### Interpretation

| -DM Behavior | Signal |
|--------------|--------|
| Rising -DM | Increasing downward price extension |
| -DM > +DM | Downward movement exceeds upward movement |
| Zero -DM | No downward directional movement on the bar |
| Values scale with instrument | Compare within same instrument only |

-DM values are in price units and scale with the instrument. They cannot be compared across different instruments without normalization (which is what -DI provides).

## Performance Profile

### Operation Count (Streaming Mode)

-DM is a thin wrapper around Dx. The per-bar cost is identical to Dx (one property extraction after Dx completes its update).

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
- PineScript reference: `minusdm.pine` in indicator directory
