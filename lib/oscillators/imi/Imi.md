# IMI: Intraday Momentum Index

> *Intraday momentum index applies RSI logic to candle bodies — bullish closes accumulate strength, bearish closes accumulate weakness.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (IMI)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [imi.pine](imi.pine)                       |

- The Intraday Momentum Index measures buying and selling pressure using the open-to-close relationship within each bar, rather than the close-to-clo...
- **Similar:** [RSI](../../momentum/rsi/Rsi.md), [MFI](../../volume/mfi/Mfi.md) | **Complementary:** Volume | **Trading note:** Intraday Momentum Index; RSI variant using open-close relationship. Measures intrabar conviction.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Intraday Momentum Index measures buying and selling pressure using the open-to-close relationship within each bar, rather than the close-to-close changes used by RSI. Each bar is classified as a gain (close > open) or loss (close < open), with the magnitude being the absolute open-close difference. Rolling sums of gains and losses over the lookback period produce an RSI-like ratio scaled to 0-100. This bridges Japanese candlestick analysis with Western oscillator theory: bullish candles contribute to the gain sum, bearish candles contribute to the loss sum. Unlike RSI, IMI does not require a previous close and uses simple rolling sums rather than exponential smoothing, making it more responsive but noisier. Output is bounded 0-100 with conventional overbought (>70) and oversold (<30) zones.

## Historical Context

Tushar Chande introduced the Intraday Momentum Index in *The New Technical Trader* (1994), alongside innovations like the Chande Momentum Oscillator. Chande observed that traditional momentum indicators like RSI ignored the intraday price action captured by candlestick patterns. By using the open-close relationship instead of close-close changes, IMI measures a fundamentally different quantity: the directional conviction *within* each bar rather than the change *between* bars. On daily charts, the open-close relationship has clear meaning — it captures overnight positioning gaps plus session direction. The indicator is self-contained within each bar, requiring no previous bar's close, which makes it particularly clean for session-based analysis. The formula structure deliberately mirrors RSI (sum of gains over total) to provide familiar overbought/oversold levels while measuring intra-session momentum.

## Architecture & Physics

### 1. Gain/Loss Classification

Each bar is classified based on the open-close relationship:

$$G_t = \begin{cases} C_t - O_t & \text{if } C_t > O_t \\ 0 & \text{otherwise} \end{cases}$$

$$L_t = \begin{cases} O_t - C_t & \text{if } C_t < O_t \\ 0 & \text{otherwise} \end{cases}$$

Doji bars ($C = O$) contribute zero to both sums.

### 2. Rolling Sums

Simple rolling sums over the lookback window (no exponential smoothing):

$$\text{SumGains}_t = \sum_{i=t-N+1}^{t} G_i$$

$$\text{SumLosses}_t = \sum_{i=t-N+1}^{t} L_i$$

Implemented with ring buffers and incremental add/subtract for $O(1)$ per bar.

### 3. IMI Value

$$\text{IMI}_t = 100 \times \frac{\text{SumGains}_t}{\text{SumGains}_t + \text{SumLosses}_t}$$

When both sums are zero (all doji bars in window), IMI defaults to 50.0 (neutral).

### 4. Complexity

- **Time:** $O(1)$ per bar — rolling sum add/subtract
- **Space:** $O(N)$ — two ring buffers for gain and loss history
- **Warmup:** $N$ bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 1$ |

### IMI vs RSI Comparison

| Property | RSI | IMI |
|----------|-----|-----|
| Input | Close-to-close change | Open-to-close change |
| Measures | Inter-session momentum | Intra-session momentum |
| Smoothing | Wilder's RMA (exponential) | Simple rolling sum |
| Previous bar | Required ($C_{t-1}$) | Not required (self-contained) |
| Response | Smoother, more lag | More responsive, noisier |
| Range | 0-100 | 0-100 |

### Interpretation

| IMI Value | Meaning |
|-----------|---------|
| > 70 | Overbought — strong bullish intra-session pressure |
| < 30 | Oversold — strong bearish intra-session pressure |
| 50 | Neutral — balanced buying/selling within bars |
| Rising toward 70 | Increasing proportion of bullish candles |
| Falling toward 30 | Increasing proportion of bearish candles |

### Timeframe Sensitivity

On daily charts, the open-close relationship captures overnight gaps plus session direction — the most informative timeframe for IMI. On very short intraday charts (1-minute), the open-close relationship carries less structural information since the open price has minimal gap significance. Choose timeframes where the opening price carries genuine information about session sentiment.

### OHLC Requirement

IMI requires both Open and Close prices per bar. It implements `ITValuePublisher` directly rather than `AbstractBase` since it operates on `TBar` (OHLC) input, not single `TValue` input.

## Performance Profile

### Operation Count (Streaming Mode)

IMI (Intraday Momentum Index) tracks rolling sums of up-body and total-body candles over N bars.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (Close − Open = body) | 1 | 1 | 1 |
| CMP (up body vs down body) | 1 | 1 | 1 |
| RingBuffer add + oldest sub × 2 (ΣUp, ΣTotal) | 4 | 1 | 4 |
| DIV (ΣUp / ΣTotal) | 1 | 15 | 15 |
| MUL × 100 | 1 | 3 | 3 |
| CMP (guard div-by-zero) | 1 | 1 | 1 |
| **Total** | **9** | — | **~25 cycles** |

~25 cycles per bar. Fast O(1) running sums.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Body computation | Yes | VSUBPD — independent per bar |
| Up/total conditional accumulation | Partial | VCMPPD mask + VADDPD (masked add) |
| Prefix-sum sliding window | Partial | Sum scan with subtract-lag |
| Division + scale | Yes | VDIVPD + VMULPD |

The conditional accumulation (masked add for up bodies) is SIMD-friendly with AVX2 blend/mask operations.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact running sum arithmetic; integer-like body logic |
| **Timeliness** | 7/10 | N-bar window; reacts immediately to intraday momentum shifts |
| **Smoothness** | 5/10 | Raw ratio can swing sharply with candle character changes |
| **Noise Rejection** | 6/10 | Window averaging provides moderate smoothing |

## Resources

- Chande, T.S. & Kroll, S. — *The New Technical Trader* (John Wiley & Sons, 1994)
- PineScript reference: `imi.pine` in indicator directory