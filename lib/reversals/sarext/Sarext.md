# SAREXT: Parabolic SAR Extended

> *The trend is your friend — but which way it accelerates depends on whether you're long or short.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                |
| **Parameters**   | `startValue` (0), `offsetOnReverse` (0), `afInitLong` (0.02), `afLong` (0.02), `afMaxLong` (0.20), `afInitShort` (0.02), `afShort` (0.02), `afMaxShort` (0.20) |
| **Outputs**      | Single series (sign-encoded SAR) |
| **Output range** | ±price level (positive = long, negative = short) |
| **Warmup**       | `2` bars                        |
| **PineScript**   | [sarext.pine](sarext.pine)                       |

- Extended Parabolic SAR with **asymmetric acceleration factors** for long and short positions.
- Sign-encoded output: positive = long (SAR below price), negative = short (SAR above price).
- Matches TA-Lib `TA_SAREXT` specification with 8 parameters.
- Auto-detects initial direction from Directional Movement when `startValue == 0`.
- Validated against TA-Lib reference implementation.

## Introduction

The Parabolic SAR Extended (SAREXT) is an enhanced version of Wilder's Parabolic Stop And Reverse that allows **separate acceleration factor configurations for long and short positions**. While standard PSAR uses the same AF start, increment, and maximum for both trend directions, SAREXT provides six independent AF parameters (three for long, three for short), plus a `startValue` to force initial direction and `offsetOnReverse` to add a gap buffer when the indicator reverses.

This design makes SAREXT suitable for markets where bullish and bearish trends have different characteristics — for example, equity markets where rallies tend to be gradual (lower AF) and selloffs tend to be sharp (higher AF).

## Historical Context

SAREXT originates from the TA-Lib open-source technical analysis library, where it appears as `TA_SAREXT`. It extends Wilder's original 1978 PSAR with asymmetric parameters, addressing a common criticism: that markets don't behave symmetrically in both directions. The TA-Lib implementation adds the `startValue` parameter for deterministic initialization (useful in backtesting) and `offsetOnReverse` for creating a buffer zone that reduces whipsaw on reversals.

## Architecture and Physics

### 1. State Machine

SAREXT operates as a two-state machine identical to PSAR: **Long** (uptrend) and **Short** (downtrend). Each state tracks:

- **SAR**: Current stop level
- **EP** (Extreme Point): Highest high in long mode, lowest low in short mode
- **AF** (Acceleration Factor): Uses direction-specific parameters

### 2. Initialization (Bars 0–1)

| Bar | Action |
|-----|--------|
| Bar 0 | Collect first OHLC data, no output |
| Bar 1 | Determine direction: `startValue > 0` → long, `startValue < 0` → short, `startValue == 0` → auto-detect from DM |

**Auto-detection**: Compares plusDM (High[1] - High[0]) vs minusDM (Low[0] - Low[1]). If plusDM > minusDM and plusDM > 0, start long; otherwise start short.

### 3. SAR Update Rule (Asymmetric)

**Long mode:**

$$\text{SAR}_{t} = \text{SAR}_{t-1} + \text{AF}_{\text{long}} \times (\text{EP} - \text{SAR}_{t-1})$$

**Short mode:**

$$\text{SAR}_{t} = \text{SAR}_{t-1} + \text{AF}_{\text{short}} \times (\text{EP} - \text{SAR}_{t-1})$$

Both computed using `Math.FusedMultiplyAdd` for numerical precision.

### 4. SAR Clamping

Identical to PSAR:

- Long: $\text{SAR}_{t} = \min(\text{SAR}_{t}, \text{Low}_{t-1}, \text{Low}_{t-2})$
- Short: $\text{SAR}_{t} = \max(\text{SAR}_{t}, \text{High}_{t-1}, \text{High}_{t-2})$

### 5. Reversal Detection with Offset

- **Long → Short**: When $\text{Low}_t \leq \text{SAR}_t$:
  - $\text{SAR} = \text{EP} + \text{offsetOnReverse}$
  - $\text{EP} = \text{Low}_t$, $\text{AF} = \text{afInitShort}$

- **Short → Long**: When $\text{High}_t \geq \text{SAR}_t$:
  - $\text{SAR} = \text{EP} - \text{offsetOnReverse}$
  - $\text{EP} = \text{High}_t$, $\text{AF} = \text{afInitLong}$

### 6. EP/AF Update (No Reversal)

- Long: if $\text{High}_t > \text{EP}$, then $\text{EP} = \text{High}$, $\text{AF} = \min(\text{AF} + \text{afLong}, \text{afMaxLong})$
- Short: if $\text{Low}_t < \text{EP}$, then $\text{EP} = \text{Low}$, $\text{AF} = \min(\text{AF} + \text{afShort}, \text{afMaxShort})$

### 7. Sign-Encoded Output

$$\text{output} = \begin{cases} +\text{SAR} & \text{if long (SAR below price)} \\ -\text{SAR} & \text{if short (SAR above price)} \end{cases}$$

## Mathematical Foundation

The SAR update is a first-order IIR filter with time-varying, direction-dependent coefficient:

$$y_t = y_{t-1} + \alpha_t^{(d)} (x^* - y_{t-1})$$

where $d \in \{\text{long}, \text{short}\}$ selects the parameter set. The asymmetric AF progression:

$$\text{AF}_t^{(\text{long})} = \min(\text{afInitLong} + n_{\text{long}} \times \text{afLong}, \text{afMaxLong})$$

$$\text{AF}_t^{(\text{short})} = \min(\text{afInitShort} + n_{\text{short}} \times \text{afShort}, \text{afMaxShort})$$

### Parameter Reference

| Parameter | Default | Effect |
|-----------|---------|--------|
| startValue | 0 | Initial direction: >0 long, <0 short, 0 auto-detect |
| offsetOnReverse | 0 | Gap added to SAR on reversal (reduces whipsaw) |
| afInitLong | 0.02 | Initial AF for long positions |
| afLong | 0.02 | AF increment per new high in long mode |
| afMaxLong | 0.20 | Maximum AF for long positions |
| afInitShort | 0.02 | Initial AF for short positions |
| afShort | 0.02 | AF increment per new low in short mode |
| afMaxShort | 0.20 | Maximum AF for short positions |

## Performance Profile

### Operation Count (Streaming Mode)

SAREXT is O(1) per bar — identical to PSAR with minor overhead for parameter selection.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Direction check + param select | 1 | 3 cy | ~3 cy |
| EP (extreme point) update | 1 | 2 cy | ~2 cy |
| AF increment (conditional) | 1 | 2 cy | ~2 cy |
| SAR = SAR + AF*(EP - SAR) via FMA | 1 | 1 cy | ~1 cy |
| Reversal detection + offset | 1 | 4 cy | ~4 cy |
| Sign encoding + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~14 cy** |

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Update (streaming) | O(1) | State machine: constant work per bar |
| Batch (span) | O(n) | Sequential state machine (no SIMD possible) |
| Memory | O(1) | Fixed state: 12 doubles + 1 bool |
| Warmup | 2 bars | Bar 0 collects data, bar 1 determines direction |

### SIMD Analysis

SAREXT cannot be vectorized. The state machine has data-dependent branches (reversal detection, direction-specific AF selection) and sequential dependencies. The Batch API delegates to streaming for correctness.

### Quality Metrics (1–10 Scale)

| Metric | Score | Rationale |
|--------|-------|-----------|
| Trend detection | 7 | Same as PSAR; asymmetric AF can reduce false reversals |
| Responsiveness | 9 | Independent AF tuning per direction improves adaptability |
| False signals | 6 | offsetOnReverse helps reduce whipsaw vs standard PSAR |
| Flexibility | 10 | 8 parameters allow fine-grained control |
| TA-Lib compatibility | 10 | Matches TA_SAREXT specification |

## Validation

| Library | Match | Tolerance | Notes |
|---------|-------|-----------|-------|
| TA-Lib | ✅ | 1e-8 | `Functions.SarExt(highs, lows, ...)` with all 8 parameters |
| Self | ✅ | 1e-10 | Streaming == Batch == Span |

## Common Pitfalls

1. **Sign interpretation**: Output is sign-encoded. Use `Math.Abs(output)` for the raw SAR level. Check `output > 0` for long, `output < 0` for short.

2. **Bar 0 outputs NaN**: The first bar collects data only. Valid output starts at bar 1 (sample index 2).

3. **offsetOnReverse too large**: Large offsets create SAR values far from price, delaying re-entry. Start with 0 and increase incrementally.

4. **Asymmetric AF interaction**: Setting `afMaxShort` much higher than `afMaxLong` makes short-side SAR track price tightly while long-side SAR lags. This is intentional for bearish-bias strategies but may surprise.

5. **Auto-detect sensitivity**: When `startValue == 0`, the DM comparison on bars 0–1 determines initial direction. A single bar's DM can be noisy; use `startValue` for deterministic behavior in backtests.

6. **No SIMD path**: Sequential state machine with data-dependent branches prevents vectorization. Batch API is O(n) sequential.

## References

- TA-Lib. "TA_SAREXT — SAR Extended." Open-source technical analysis library.
- Wilder, J. W. Jr. (1978). *New Concepts in Technical Trading Systems*. Trend Research. ISBN 978-0894590276.
- Kaufman, P. J. (2013). *Trading Systems and Methods*, 5th ed. Wiley.