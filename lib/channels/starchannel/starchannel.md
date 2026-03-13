# STARCHANNEL: Stoller Average Range Channel

> *Stoller channels use ATR to build a corridor around the average — a volatility-aware boundary for range traders.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20), `multiplier` (default 2.0), `atrPeriod` (default 0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `Math.Max(period, effectiveAtrPeriod)` bars                          |
| **PineScript**   | [starchannel.pine](starchannel.pine)                       |

- Stoller Average Range Channel creates a volatility-adaptive price envelope using Average True Range (ATR) to determine band width around a simple m...
- **Similar:** [KChannel](../kchannel/kchannel.md), [ATRBands](../atrbands/atrbands.md) | **Complementary:** ADX for trend confirmation | **Trading note:** Stoller Average Range Channel; ATR-based bands around a moving average.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Stoller Average Range Channel creates a volatility-adaptive price envelope using Average True Range (ATR) to determine band width around a simple moving average centerline. The bands automatically expand during volatile periods and contract during calmer markets. The implementation uses a circular buffer for the SMA running sum and Wilder's RMA with a warmup compensator for ATR, achieving O(1) streaming updates per bar.

## Historical Context

Manning Stoller developed STARC Bands in the early 1980s as a volatility-adaptive alternative to fixed percentage envelopes. His insight was straightforward: channels should widen during high volatility and contract during low volatility, reflecting actual market conditions rather than arbitrary percentages.

The indicator combines two established building blocks: the simple moving average (for trend direction) and Average True Range (for volatility measurement). J. Welles Wilder had already introduced ATR in his 1978 book *New Concepts in Technical Trading Systems*. Stoller's contribution was recognizing that ATR-based bands would naturally adapt to each security's volatility characteristics without requiring manual adjustment across different instruments or timeframes.

STARC Bands gained popularity among futures traders in the 1980s and influenced many subsequent volatility-adaptive channel indicators. The structure is similar to Keltner Channels (EMA center + ATR width) but uses an SMA centerline, which gives equal weight to all bars in the window rather than exponentially decaying emphasis on recent prices.

## Architecture & Physics

### 1. Simple Moving Average (Middle Band)

The centerline is a standard SMA of the source price using a circular buffer with running sum:

$$
\text{Middle}_t = \frac{1}{n} \sum_{i=0}^{n-1} C_{t-i}
$$

where $C$ is the source price (typically close) and $n$ is the period.

### 2. True Range

True Range captures the full extent of price movement including gaps:

$$
TR_t = \max(H_t - L_t,\; |H_t - C_{t-1}|,\; |L_t - C_{t-1}|)
$$

### 3. Average True Range (RMA with Warmup Compensation)

ATR uses Wilder's smoothing (RMA) with a warmup compensator to eliminate cold-start bias:

$$
\text{raw\_rma}_t = \frac{\text{raw\_rma}_{t-1} \cdot (n - 1) + TR_t}{n}
$$

$$
e_t = (1 - \alpha) \cdot e_{t-1}, \quad \alpha = \frac{1}{n}
$$

$$
ATR_t = \begin{cases} \text{raw\_rma}_t \;/\; (1 - e_t) & \text{if } e_t > \epsilon \\ \text{raw\_rma}_t & \text{otherwise} \end{cases}
$$

The compensator $e_t$ converges to zero as bars accumulate, removing the initialization bias that would otherwise undercount early ATR values.

### 4. Band Construction

$$
U_t = \text{Middle}_t + k \cdot ATR_t
$$

$$
L_t = \text{Middle}_t - k \cdot ATR_t
$$

where $k$ is the multiplier (default 2.0).

### 5. Complexity

Streaming: $O(1)$ per bar. The SMA uses a running sum with circular buffer (add new, subtract oldest). The RMA is a single-pole IIR filter. Memory: one circular buffer of $n$ floats for SMA, plus scalar state for RMA/compensator.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n$ | period | 20 | $> 0$ | SMA and ATR lookback period |
| $k$ | multiplier | 2.0 | $> 0$ | ATR multiplier for band width |
| $n_{\text{atr}}$ | atr_length | 0 | $\geq 0$ | Separate ATR period (0 = same as SMA period) |

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Band width expanding | ATR rising; volatility increasing |
| Band width contracting | ATR falling; volatility decreasing |
| Price at upper band | Overextended above SMA by ATR measure |
| Price at lower band | Overextended below SMA by ATR measure |
| Middle band slope positive | SMA trending upward |

## Performance Profile

### Operation Count (Streaming Mode)

STARCHANNEL combines an SMA running sum (center), True Range, and Wilder's RMA with warmup compensation — identical cost to ATRBANDS:

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

During warmup (RMA compensator active):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (e × (1 - α)) | 1 | 3 | 3 |
| SUB (1 - e) | 1 | 1 | 1 |
| DIV (raw_rma / (1 - e)) | 1 | 15 | 15 |
| CMP (e > ε) | 1 | 1 | 1 |
| **Warmup overhead** | **4** | — | **~20 cycles** |

**Total during warmup:** ~53 cycles/bar; **Post-warmup:** ~33 cycles/bar.

### Batch Mode (SIMD Analysis)

The SMA running sum and RMA recursion are sequential. True Range computation is independent per bar:

| Optimization | Benefit |
| :--- | :--- |
| True Range (3-way max) | Vectorizable with `Vector.Max` and `Vector.Abs` |
| RMA recursion | Sequential (IIR dependency) |
| SMA running sum | Sequential |
| Band arithmetic | Vectorizable in a post-pass |

## Resources

- Stoller, M. (1980s). Development of the Stoller Average Range Channel.
- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research.
- Kaufman, P. (2013). *Trading Systems and Methods*, 5th ed. Wiley.
