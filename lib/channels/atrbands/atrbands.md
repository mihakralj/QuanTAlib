# ATRBANDS: Average True Range Bands

> *True range bands let volatility itself draw the envelope — wider when uncertain, tighter when resolved.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`, `multiplier` (default 2.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [atrbands.pine](atrbands.pine)                       |

- ATR Bands create a volatility-adaptive envelope by projecting Wilder's Average True Range above and below a central Simple Moving Average.
- **Similar:** [KC](../kc/kc.md), [STBands](../stbands/stbands.md) | **Complementary:** ADX to distinguish trend vs range | **Trading note:** Volatility-normalized symmetric bands using ATR; adapts to true volatility including gaps.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

ATR Bands create a volatility-adaptive envelope by projecting Wilder's Average True Range above and below a central Simple Moving Average. Unlike fixed-percentage envelopes or standard-deviation bands, ATR Bands use True Range to measure volatility, making them robust for assets with gaps, pre-market moves, and 24/7 trading where the "hidden" volatility between bars is significant. The True Range captures the maximum of intra-bar range, gap-up distance, and gap-down distance, ensuring that overnight gaps contribute fully to band width even when the current bar's open-to-close range is narrow.

## Historical Context

J. Welles Wilder introduced Average True Range in *New Concepts in Technical Trading Systems* (1978), primarily as a trailing stop mechanism (the "Volatility Stop") and as a component of the Average Directional Index (ADX). Wilder used his own smoothing method, now known as RMA or Wilder's Smoothing, which is equivalent to an EMA with $\alpha = 1/n$. Futures traders in the 1980s quickly realized that projecting ATR above and below a trend-following moving average created a practical channel answering the question: "How far can price move from the average before it is statistically abnormal?"

ATR Bands differ from Keltner Channels only in the center line: ATR Bands use SMA, Keltner uses EMA. Some implementations use SMA-based ATR averaging instead of Wilder's smoothing. The QuanTAlib implementation uses Wilder's smoothing (RMA) for ATR with a warmup compensator for accurate early values, and SMA for the center line.

## Architecture & Physics

### 1. True Range

True Range captures the maximum extent of price movement, including gaps:

$$TR_t = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)$$

### 2. Average True Range (Wilder's Smoothing / RMA)

$$ATR_t = \frac{ATR_{t-1} \times (n - 1) + TR_t}{n}$$

This is equivalent to EMA with $\alpha = 1/n$. The warmup compensator corrects for initialization bias:

$$e_t = (1 - \alpha) \cdot e_{t-1}, \quad ATR_t^* = \frac{ATR_t}{1 - e_t} \text{ while } e > \epsilon$$

### 3. Center Line (SMA)

$$\text{Middle}_t = \frac{1}{n} \sum_{i=0}^{n-1} x_{t-i}$$

### 4. Band Construction

$$\text{Upper}_t = \text{Middle}_t + k \cdot ATR_t$$

$$\text{Lower}_t = \text{Middle}_t - k \cdot ATR_t$$

### 5. Complexity

The SMA uses a circular buffer for $O(1)$ running sums. The ATR uses recursive IIR smoothing, also $O(1)$. True Range computation requires retaining the previous close. Total: $O(1)$ per bar with one buffer of size $n$ for the SMA.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback for SMA and ATR smoothing ($n$) | 20 | $> 0$ |
| `multiplier` | Band width scale factor ($k$) | 2.0 | $> 0$ |
| `source` | Input series for center line | close | |

### True Range Components

| Component | Formula | Captures |
|-----------|---------|----------|
| Intra-bar | $H_t - L_t$ | Current bar's range |
| Gap-up | $\|H_t - C_{t-1}\|$ | Upward gap distance |
| Gap-down | $\|L_t - C_{t-1}\|$ | Downward gap distance |

### Output Interpretation

| Output | Description |
|--------|-------------|
| `middle` | SMA of source (center line) |
| `upper` | Middle + scaled ATR (volatility-adjusted resistance) |
| `lower` | Middle - scaled ATR (volatility-adjusted support) |

## Performance Profile

### Operation Count (Streaming Mode)

ATRBANDS combines an SMA running sum (center line), True Range computation, and Wilder's RMA with warmup compensation:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (oldest from SMA sum) | 1 | 1 | 1 |
| ADD (new to SMA sum) | 1 | 1 | 1 |
| DIV (SMA = sum / count) | 1 | 15 | 15 |
| SUB (H - L) | 1 | 1 | 1 |
| SUB + ABS (H - prevC, L - prevC) | 2 | 2 | 4 |
| CMP (max of 3 for TR) | 2 | 1 | 2 |
| FMA (RMA: prev×(n-1)/n + TR/n) | 1 | 4 | 4 |
| MUL (multiplier × ATR) | 1 | 3 | 3 |
| ADD/SUB (middle ± width) | 2 | 1 | 2 |
| **Total (hot)** | **12** | — | **~33 cycles** |

During warmup (compensator active):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (e × (1 - α)) | 1 | 3 | 3 |
| SUB (1 - e) | 1 | 1 | 1 |
| DIV (raw_rma / (1 - e)) | 1 | 15 | 15 |
| CMP (e > ε) | 1 | 1 | 1 |
| **Warmup overhead** | **4** | — | **~20 cycles** |

**Total during warmup:** ~53 cycles/bar; **Post-warmup:** ~33 cycles/bar.

### Batch Mode (SIMD Analysis)

The SMA running sum and RMA recursion are both sequential. True Range computation is independent per bar and vectorizable:

| Optimization | Benefit |
| :--- | :--- |
| True Range (3-way max) | Vectorizable with `Vector.Max` and `Vector.Abs` |
| RMA recursion | Sequential (IIR dependency) |
| SMA running sum | Sequential |
| Band arithmetic | Vectorizable in a post-pass |

## Resources

- **Wilder, J.W.** *New Concepts in Technical Trading Systems*. Trend Research, 1978. (Original ATR and Wilder's Smoothing)
- **Keltner, C.** "How to Use the 10-Day Moving Average Rule." *Commodities*, 1960. (EMA-centered ATR channel variant)
- **Bollinger, J.** *Bollinger on Bollinger Bands*. McGraw-Hill, 2001. (Standard deviation band alternative for comparison)
