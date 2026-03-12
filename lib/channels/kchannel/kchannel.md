# KCHANNEL: Keltner Channel

> *Keltner wraps an EMA in ATR-scaled bands — a volatility envelope that responds to both trend and range.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20), `multiplier` (default 2.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period * 2` bars                          |
| **PineScript**   | [kchannel.pine](kchannel.pine)                       |

- Keltner Channel constructs a volatility-adaptive envelope by projecting Average True Range above and below an Exponential Moving Average center line.
- Parameterized by `period` (default 20), `multiplier` (default 2.0).
- Output range: Tracks input.
- Requires `period * 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Keltner Channel constructs a volatility-adaptive envelope by projecting Average True Range above and below an Exponential Moving Average center line. The channel differs from ATR Bands solely in the center line: Keltner uses EMA (faster, more responsive) while ATR Bands use SMA (more stable, more lag). The EMA center combined with ATR width creates a channel that both tracks trend and adapts to volatility, making it one of the most widely used channel indicators for trend-following and mean-reversion strategies. The implementation uses EMA with warmup compensation for accurate early values and Wilder's smoothing (RMA) for ATR.

## Historical Context

Chester Keltner introduced the original "Ten-Day Moving Average Trading Rule" in his 1960 book *How to Make Money in Commodities*. Keltner's original channel used a 10-day SMA of the "typical price" (HLC/3) as the center, with the band width based on the 10-day SMA of the daily range (High - Low, without gap adjustment).

Linda Bradford Raschke modernized the indicator in the 1990s by replacing the SMA center with an EMA and the simple range with Average True Range. This modern version became widely known as "Keltner Channels" and is the standard implementation in most platforms. The switch to EMA reduces lag in the center line, and the switch to ATR ensures that gaps contribute to band width — critical for futures and stocks that gap regularly. The ATR component uses Wilder's smoothing ($\alpha = 1/n$), providing infinite memory that makes the channel particularly stable after sufficient warmup.

## Architecture & Physics

### 1. Center Line (EMA with Warmup Compensation)

$$\alpha = \frac{2}{n + 1}$$

$$\text{raw}_t = \alpha \cdot x_t + (1 - \alpha) \cdot \text{raw}_{t-1}$$

$$w_t = \alpha + (1 - \alpha) \cdot w_{t-1}$$

$$\text{EMA}_t = \frac{\text{raw}_t}{w_t}$$

The weight accumulator $w$ compensates for EMA initialization bias, producing accurate values from bar 1.

### 2. True Range

$$TR_t = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

### 3. Average True Range (Wilder's Smoothing / RMA)

$$\alpha_{\text{atr}} = \frac{1}{n}$$

$$\text{raw\_rma}_t = \frac{\text{raw\_rma}_{t-1} \cdot (n-1) + TR_t}{n}$$

$$e_t = (1 - \alpha_{\text{atr}}) \cdot e_{t-1}$$

$$ATR_t = \frac{\text{raw\_rma}_t}{1 - e_t} \text{ (during warmup)}$$

### 4. Band Construction

$$\text{Upper}_t = \text{EMA}_t + k \cdot ATR_t$$

$$\text{Lower}_t = \text{EMA}_t - k \cdot ATR_t$$

### 5. Complexity

$O(1)$ per bar: one EMA update, one True Range computation, one RMA update, and two band calculations. No buffers required.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback for EMA and ATR smoothing ($n$) | 20 | $> 0$ |
| `multiplier` | ATR scale factor ($k$) | 2.0 | $> 0$ |
| `source` | Input series for EMA center | close | |

### Keltner vs. ATR Bands vs. Bollinger

| Feature | Keltner | ATR Bands | Bollinger |
|---------|---------|-----------|-----------|
| Center | EMA | SMA | SMA |
| Width | ATR | ATR | StdDev |
| Gap sensitivity | Yes (via TR) | Yes (via TR) | No |
| Distribution assumption | None | None | Gaussian |

### Output Interpretation

| Output | Description |
|--------|-------------|
| `middle` | EMA center line (trend direction) |
| `upper` | EMA + scaled ATR (dynamic resistance) |
| `lower` | EMA - scaled ATR (dynamic support) |

## Performance Profile

### Operation Count (Streaming Mode)

KCHANNEL combines an EMA with warmup compensation (center), True Range computation, and Wilder's RMA with warmup compensation (ATR):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA (EMA: α×source + (1-α)×prev) | 1 | 4 | 4 |
| FMA (weight accumulator update) | 1 | 4 | 4 |
| DIV (raw / weight for EMA) | 1 | 15 | 15 |
| SUB (H - L) | 1 | 1 | 1 |
| SUB + ABS (H - prevC, L - prevC) | 2 | 2 | 4 |
| CMP (max of 3 for TR) | 2 | 1 | 2 |
| FMA (RMA: prev×(n-1)/n + TR/n) | 1 | 4 | 4 |
| MUL (multiplier × ATR) | 1 | 3 | 3 |
| ADD/SUB (EMA ± width) | 2 | 1 | 2 |
| **Total (hot)** | **12** | — | **~39 cycles** |

During warmup (RMA compensator active):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (e × (1 - α)) | 1 | 3 | 3 |
| SUB (1 - e) | 1 | 1 | 1 |
| DIV (raw_rma / (1 - e)) | 1 | 15 | 15 |
| CMP (e > ε) | 1 | 1 | 1 |
| **Warmup overhead** | **4** | — | **~20 cycles** |

**Total during warmup:** ~59 cycles/bar; **Post-warmup:** ~39 cycles/bar.

### Batch Mode (SIMD Analysis)

All IIR recursions (EMA, RMA) are state-dependent, preventing SIMD parallelization across bars:

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | 3 hardware FMAs per bar |
| True Range computation | Vectorizable in a batch pre-pass |
| Band arithmetic | Vectorizable in a post-pass |
| No buffers | Zero allocation; all state fits in registers |

## Resources

- **Keltner, C.** *How to Make Money in Commodities*. 1960. (Original channel concept)
- **Raschke, L.B. & Connors, L.** *Street Smarts*. M. Gordon Publishing, 1995. (Modern EMA + ATR version)
- **Wilder, J.W.** *New Concepts in Technical Trading Systems*. Trend Research, 1978. (ATR and Wilder's Smoothing)
