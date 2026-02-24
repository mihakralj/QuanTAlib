# IMI: Intraday Momentum Index

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

### Pseudo-code

```
Initialize:
  gainBuf = RingBuffer(period)
  lossBuf = RingBuffer(period)
  gainSum = lossSum = 0
  bar_count = 0

On each bar (open, close, isNew):
  if !isNew: restore previous state

  // Classify bar
  diff = close - open
  gain = diff > 0 ? diff : 0
  loss = diff < 0 ? -diff : 0

  // Update rolling sums
  if gainBuf is full:
    gainSum -= gainBuf.Oldest
    lossSum -= lossBuf.Oldest
  gainBuf.Add(gain)
  lossBuf.Add(loss)
  gainSum += gain
  lossSum += loss

  // IMI calculation
  total = gainSum + lossSum
  IMI = total > 0 ? 100 × gainSum / total : 50.0

  output = IMI
```

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

## Resources

- Chande, T.S. & Kroll, S. — *The New Technical Trader* (John Wiley & Sons, 1994)
- PineScript reference: `imi.pine` in indicator directory
