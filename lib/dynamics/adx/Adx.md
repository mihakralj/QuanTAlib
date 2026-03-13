# ADX: Average Directional Index

> *ADX measures trend strength without regard to direction — a compass that tells you how hard the wind blows, not where.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Multiple series (DiPlus, DiMinus)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period * 2` bars                          |
| **PineScript**   | [adx.pine](adx.pine)                       |

- The Average Directional Index is the industry-standard measure of trend strength, ignoring direction entirely to focus on the velocity of price exp...
- **Similar:** [ADXR](../adxr/Adxr.md), [DX](../dx/Dx.md) | **Complementary:** Moving averages for direction | **Trading note:** Wilder's trend strength gauge; >25 trending, <20 ranging. Does not indicate direction.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

## Performance Profile

### Operation Count (Streaming Mode)

ADX has a two-phase pipeline: first N bars accumulate TR/+DM/−DM sums, then RMA smoothing takes over.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB × 5 (TR: hl, hpc, lpc, upMove, downMove) | 5 | 1 | 5 |
| ABS × 2 (hpc, lpc) | 2 | 1 | 2 |
| MAX × 2 (TR = max(hl, max(hpc,lpc))) | 2 | 1 | 2 |
| CMP × 2 (upMove/downMove guards) | 2 | 1 | 2 |
| FMA × 3 (RMA smooth TR, +DM, −DM) | 3 | 4 | 12 |
| DIV × 2 (+DI = +DM/TR, −DI = −DM/TR) | 2 | 15 | 30 |
| MUL × 2 (scale to 100) | 2 | 3 | 6 |
| ABS + DIV (DX = abs(+DI − −DI) / (+DI + −DI)) | 2 | 16 | 16 |
| FMA × 1 (RMA smooth ADX) | 1 | 4 | 4 |
| **Total** | **21** | — | **~79 cycles** |

ADX requires a 2N warmup period (N for TR/DM smoothing initialization, N for ADX SMA seed). For default $N=14$: ~79 cycles per bar at steady state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| TR, +DM, −DM computation | Yes | Independent differences + VSUBPD, VABSPD, VMAXPD |
| RMA smoothing (TR, +DM, −DM) | **No** | Recursive IIR — each value depends on prior; sequential only |
| DI computation (+DI, −DI) | Yes | VDIVPD after RMA pass |
| DX computation | Yes | VABSPD + VDIVPD |
| ADX smoothing (RMA of DX) | **No** | Recursive IIR — sequential only |

The recursive RMA passes block SIMD across bars. The TR/DM initial computation (N×3 differences) is vectorizable as a pre-pass. Full batch acceleration requires a prefix-sum or parallel-prefix RMA approximation, which trades exact equivalence for ~4× throughput on large datasets.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | FMA-precise RMA smoothing; 2N warmup ensures fully converged output |
| **Timeliness** | 5/10 | 2N lag (28 default) before first valid ADX; responds slowly to regime shifts |
| **Smoothness** | 8/10 | Double RMA smoothing yields very smooth output; rarely whipsaws |
| **Noise Rejection** | 8/10 | Two layers of Wilder smoothing suppress bar-to-bar noise effectively |

## Resources

- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- PineScript reference: `adx.pine` in indicator directory