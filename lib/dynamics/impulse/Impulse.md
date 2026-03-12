# IMPULSE: Elder Impulse System

> *The Impulse System identifies inflection points where a trend speeds up or slows down.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `emaPeriod` (default 13), `macdFast` (default 12), `macdSlow` (default 26), `macdSignal` (default 9)                      |
| **Outputs**      | Single series (Impulse)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `Math.Max(emaPeriod, macdSlow) + macdSignal - 1` bars (default 34)                          |

- The Elder Impulse System combines a 13-period EMA (trend inertia) with the MACD(12,26,9) histogram (momentum acceleration) to classify each bar as ...
- Parameterized by `emaperiod` (default 13), `macdfast` (default 12), `macdslow` (default 26), `macdsignal` (default 9).
- Output range: Varies (see docs).
- Requires `Math.Max(emaPeriod, macdSlow) + macdSignal - 1` bars (default 34) of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Elder Impulse System combines a 13-period EMA (trend inertia) with the MACD(12,26,9) histogram (momentum acceleration) to classify each bar as bullish (+1), bearish (-1), or neutral (0). Both EMA slope and histogram slope must agree for a directional signal; disagreement forces neutral. The system functions as a permission filter rather than a signal generator, requiring 34 bars warmup and running at O(1) per bar through composition of two child indicators.

## Historical Context

Alexander Elder introduced the Impulse System in *Come Into My Trading Room* (2002). A psychiatrist turned trader, Elder designed it as a discipline enforcement mechanism: green bars permit long entries, red bars permit short entries, blue bars prohibit new positions in either direction. The intellectual lineage runs through Gerald Appel's MACD (1979) and Thomas Aspray's MACD Histogram (1986). Elder's contribution was combining first-derivative (EMA slope) and second-derivative (histogram slope) filters into a single ternary decision gate. Most implementations treat this as a visual color overlay; QuanTAlib exposes it as a programmatic discrete signal for algorithmic consumption.

## Architecture & Physics

### 1. EMA Component (Trend Inertia)

The 13-period EMA tracks trend direction via exponential smoothing:

$$\alpha = \frac{2}{n + 1} = \frac{2}{14} \approx 0.1429$$

$$\text{EMA}_t = \alpha \cdot \text{Close}_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

With warmup bias compensation:

$$\text{EMA}_{\text{corrected}} = \frac{\text{EMA}_{\text{raw}}}{1 - (1 - \alpha)^t}$$

The EMA slope $\Delta_{\text{EMA}} = \text{sign}(\text{EMA}_t - \text{EMA}_{t-1})$ represents smoothed trend direction (first derivative of price).

### 2. MACD Histogram Component (Momentum Acceleration)

$$\text{MACD Line} = \text{EMA}(12) - \text{EMA}(26)$$

$$\text{Signal} = \text{EMA}(9,\ \text{MACD Line})$$

$$\text{Histogram} = \text{MACD Line} - \text{Signal}$$

The histogram is the second derivative of price (acceleration). Its slope $\Delta_{\text{Hist}} = \text{sign}(\text{Hist}_t - \text{Hist}_{t-1})$ indicates whether momentum is building or fading.

### 3. Signal Classification

$$\text{Impulse} = \begin{cases} +1 & \text{if } \Delta_{\text{EMA}} > 0 \text{ and } \Delta_{\text{Hist}} > 0 \\ -1 & \text{if } \Delta_{\text{EMA}} < 0 \text{ and } \Delta_{\text{Hist}} < 0 \\ 0 & \text{otherwise} \end{cases}$$

The neutral state fires whenever the two components disagree, identifying transition zones where conviction is insufficient for new entries.

### 4. Warmup

$$W = \max(13, 26) + 9 - 1 = 34 \text{ bars}$$

Both child indicators must be warmed up and at least two comparison values must exist before valid signals emerge.

### 5. Complexity

| Metric | Value |
|:-------|:------|
| Time | O(1) per bar (delegates to child EMA/MACD updates) |
| Space | O(1) (3 internal EMA states + 4 doubles for slope comparison) |
| Allocations | Zero in hot path |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| emaPeriod | int | 13 | > 0 | EMA period for trend inertia |
| macdFast | int | 12 | > 0 | MACD fast EMA period |
| macdSlow | int | 26 | > macdFast | MACD slow EMA period |
| macdSignal | int | 9 | > 0 | MACD signal smoothing period |

### Derivative Interpretation

The system combines two derivatives:

- **First derivative** (EMA slope): Is the smoothed trend rising or falling?
- **Second derivative** (histogram slope): Is the rate of MACD convergence/divergence accelerating or decelerating?

Both must confirm for a directional signal. This dual-confirmation suppresses false signals during transitions but introduces lag at inflection points.

## Performance Profile

### Operation Count (Streaming Mode)

Impulse System combines an EMA (or JMA) of close with a MACD histogram to produce a ternary directional signal.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA × 1 (EMA close update) | 1 | 4 | 4 |
| MACD pipeline (2 EMA + signal EMA) | 3 | 4 | 12 |
| SUB (MACD histogram = MACD − signal) | 1 | 1 | 1 |
| CMP × 2 (EMA up/down, histogram up/down) | 2 | 1 | 2 |
| Ternary encoding (+1/0/−1) | 1 | 1 | 1 |
| **Total** | **8** | — | **~20 cycles** |

Four independent EMA streams. ~20 cycles per bar at steady state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| All EMA passes × 4 | **No** | Recursive IIR — sequential |
| Histogram subtraction | Yes | VSUBPD after EMA arrays complete |
| Signal comparison | Yes | VCMPPD |

Same EMA constraint across all impulse components.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Three independent EMA streams; precise FMA arithmetic |
| **Timeliness** | 6/10 | MACD slow-MA period dominates warmup lag |
| **Smoothness** | 10/10 | Ternary output eliminates all intermediate noise |
| **Noise Rejection** | 8/10 | Dual confirmation (trend + momentum) reduces false signals |

## Resources

- Elder, A. (2002). *Come Into My Trading Room*. John Wiley and Sons.
- Appel, G. (1979). "The Moving Average Convergence-Divergence Method."
- Aspray, T. (1986). "MACD Histogram." *Technical Analysis of Stocks and Commodities*.
