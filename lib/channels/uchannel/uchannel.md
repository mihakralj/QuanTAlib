# UCHANNEL: Ehlers Ultimate Channel

> *The ultimate channel uses Ehlers' signal processing to carve boundaries that track the market's hidden periodicity.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `strPeriod` (default DefaultStrPeriod), `centerPeriod` (default DefaultCenterPeriod), `multiplier` (default DefaultMultiplier)                      |
| **Outputs**      | Multiple series (Upper, Middle, Lower, STR)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `Math.Max(strPeriod, centerPeriod)` bars                          |
| **PineScript**   | [uchannel.pine](uchannel.pine)                       |

- Ehlers Ultimate Channel applies the Ultrasmooth Filter (USF) twice: once to the close price for the centerline and once to True Range for band widt...
- Parameterized by `strperiod` (default defaultstrperiod), `centerperiod` (default defaultcenterperiod), `multiplier` (default defaultmultiplier).
- Output range: Tracks input.
- Requires `Math.Max(strPeriod, centerPeriod)` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Ehlers Ultimate Channel applies the Ultrasmooth Filter (USF) twice: once to the close price for the centerline and once to True Range for band width, creating a channel where both the trend estimate and the volatility measure share the same low-lag, zero-overshoot filter characteristics. Unlike UBANDS which uses RMS of price residuals, UCHANNEL uses Smoothed True Range (STR) for band width, making it responsive to gap-inclusive volatility. Separate period parameters allow independent tuning of centerline smoothness and band-width responsiveness.

## Historical Context

John F. Ehlers introduced the Ultimate Channel in 2024 as the natural companion to his Ultimate Bands (UBANDS) indicator. The two share the same USF foundation but differ in how they determine band width:

- **UBANDS**: measures RMS of price deviations from the USF centerline (statistical dispersion)
- **UCHANNEL**: smooths True Range with the USF (range-based volatility)

The True Range approach captures overnight gaps, making UCHANNEL more appropriate for markets with significant gap activity (equities, futures) where the high-low range alone would underestimate actual price risk.

The design philosophy reflects Ehlers' preference for using the same high-quality filter throughout an indicator system. By applying the USF to both the centerline and the True Range, all components share consistent lag characteristics and zero-overshoot behavior.

## Architecture & Physics

### 1. True Range

True Range extends the high-low range to capture gaps:

$$
TR_t = \max(H_t,\, C_{t-1}) - \min(L_t,\, C_{t-1})
$$

This formulation ensures gap-ups (today's low above yesterday's close) and gap-downs (today's high below yesterday's close) are fully captured.

### 2. Ultrasmooth Filter Coefficients

Each USF instance derives its coefficients from a period parameter $n$:

$$
\text{arg} = \frac{\sqrt{2}\,\pi}{n}
$$

$$
c_2 = 2\,e^{-\text{arg}} \cos(\text{arg}), \qquad c_3 = -e^{-2\,\text{arg}}, \qquad c_1 = \frac{1 + c_2 - c_3}{4}
$$

### 3. USF Recursion

The 2-pole IIR recursion applied to input series $X$:

$$
\text{USF}_t = (1 - c_1)\,X_t + (2c_1 - c_2)\,X_{t-1} - (c_1 + c_3)\,X_{t-2} + c_2\,\text{USF}_{t-1} + c_3\,\text{USF}_{t-2}
$$

This recursion is applied twice with potentially different periods:

- To close prices → **centerline** (Middle band)
- To True Range → **Smoothed True Range** (STR)

### 4. Channel Construction

$$
\text{Middle}_t = \text{USF}(C_t,\; n_{\text{center}})
$$

$$
\text{STR}_t = \text{USF}(TR_t,\; n_{\text{str}})
$$

$$
U_t = \text{Middle}_t + k \cdot \text{STR}_t
$$

$$
L_t = \text{Middle}_t - k \cdot \text{STR}_t
$$

where $k$ is the multiplier (default 1.0).

### 5. Complexity

Streaming: $O(1)$ per bar. Both USF instances are IIR recursions requiring only four multiply-adds each plus scalar state. No buffers or window scans needed. Memory: approximately 200 bytes for the two USF states plus metadata.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n_{\text{str}}$ | strPeriod | 20 | $\geq 1$ | USF period for smoothing True Range |
| $n_{\text{center}}$ | centerPeriod | 20 | $\geq 1$ | USF period for smoothing the centerline |
| $k$ | multiplier | 1.0 | $> 0$ | STR multiplier for band width |

### USF Transfer Function

$$
H(z) = \frac{(1 - c_1) + (2c_1 - c_2)\,z^{-1} - (c_1 + c_3)\,z^{-2}}{1 - c_2\,z^{-1} - c_3\,z^{-2}}
$$

Frequency response: cutoff at approximately $f_c \approx 1/(2\pi n)$ cycles per bar; 12 dB/octave rolloff.

### UCHANNEL vs UBANDS

| Aspect | UBANDS | UCHANNEL |
|--------|--------|----------|
| Centerline | USF of close | USF of close |
| Band width | RMS of residuals ($O(n)$) | USF of True Range ($O(1)$) |
| Gap sensitivity | Indirect (via residuals) | Direct (True Range includes gaps) |
| Parameters | 1 period | 2 periods (STR, center) |
| Per-bar cost | $O(n)$ | $O(1)$ |

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Centerline rising | USF-filtered uptrend |
| STR increasing | Smoothed True Range expanding; volatility rising |
| Band width contracting | Volatility compression |
| Price beyond upper | Extreme positive deviation from USF trend |

## Performance Profile

### Operation Count (Streaming Mode)

UCHANNEL runs two independent USF IIR recursions (one for close, one for True Range) plus True Range and band arithmetic — all $O(1)$:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (max(H, prevC) for TR) | 1 | 1 | 1 |
| CMP (min(L, prevC) for TR) | 1 | 1 | 1 |
| SUB (TH - TL for TR) | 1 | 1 | 1 |
| MUL + ADD (USF center, 4 terms) | 4 | 4 | 16 |
| ADD (USF center feedback, 5 terms) | 5 | 1 | 5 |
| MUL + ADD (USF STR, 4 terms) | 4 | 4 | 16 |
| ADD (USF STR feedback, 5 terms) | 5 | 1 | 5 |
| MUL (k × STR) | 1 | 3 | 3 |
| ADD/SUB (center ± width) | 2 | 1 | 2 |
| **Total (hot)** | **24** | — | **~50 cycles** |

No buffers, no window scans. All state fits in ~200 bytes (two USF 2-element histories + metadata). This is the fastest ATR-class channel indicator.

### Batch Mode (SIMD Analysis)

Both USF recursions are IIR-dependent, preventing SIMD parallelization across bars:

| Optimization | Benefit |
| :--- | :--- |
| USF IIR (2 instances) | Sequential; ~21 cycles each per bar |
| True Range computation | Vectorizable in a batch pre-pass |
| Band arithmetic | Vectorizable in a post-pass |
| No allocations | Zero heap allocation; all state in registers/stack |

## Resources

- Ehlers, J. F. (2024). "Ultimate Channel." *Technical Analysis of Stocks & Commodities*.
- Ehlers, J. F. (2013). *Cycle Analytics for Traders*. Wiley.
- Ehlers, J. F. (2001). *Rocket Science for Traders*. Wiley.
