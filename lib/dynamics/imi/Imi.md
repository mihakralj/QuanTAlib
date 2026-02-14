# IMI: Intraday Momentum Index

> "RSI measures close-to-close momentum. IMI measures open-to-close momentum. One tracks what happened between sessions; the other tracks what happened inside them."

IMI (Intraday Momentum Index), developed by Tushar Chande, combines candlestick analysis with RSI-like overbought/oversold signals. Unlike RSI, which uses close-to-close price changes, IMI measures the relationship between each bar's open and close prices. This makes it particularly effective for detecting intraday buying/selling pressure and candlestick pattern strength. The result oscillates between 0 and 100, with readings above 70 indicating overbought conditions and below 30 indicating oversold.

## Historical Context

Tushar Chande introduced the Intraday Momentum Index in "The New Technical Trader" (1994), alongside other innovations like the Chande Momentum Oscillator (CMO). Chande observed that traditional momentum indicators like RSI ignored the intraday price action captured by candlestick patterns. By using the open-close relationship instead of close-close changes, IMI bridges the gap between Japanese candlestick analysis and Western oscillator theory.

The indicator is particularly useful on daily charts where the open-close relationship has clear meaning (overnight gap vs session direction). On intraday timeframes, its interpretation shifts to measuring buying pressure within each bar. Unlike RSI, IMI does not require a previous close, making it self-contained within each bar.

The formula structure mirrors RSI: sum of gains over sum of gains plus losses, scaled to 0-100. This provides familiar overbought/oversold levels while measuring a fundamentally different quantity.

## Architecture & Physics

### 1. Gain/Loss Classification

Each bar is classified based on the open-close relationship:

$$
\text{Gain}_t = \begin{cases} \text{Close}_t - \text{Open}_t & \text{if Close} > \text{Open} \\ 0 & \text{otherwise} \end{cases}
$$

$$
\text{Loss}_t = \begin{cases} \text{Open}_t - \text{Close}_t & \text{if Close} < \text{Open} \\ 0 & \text{otherwise} \end{cases}
$$

### 2. Rolling Sum Calculation

The indicator uses O(1) rolling sums via ring buffers:

$$
\text{SumGains}_t = \sum_{i=t-n+1}^{t} \text{Gain}_i
$$

$$
\text{SumLosses}_t = \sum_{i=t-n+1}^{t} \text{Loss}_i
$$

### 3. IMI Value

$$
\text{IMI}_t = 100 \times \frac{\text{SumGains}_t}{\text{SumGains}_t + \text{SumLosses}_t}
$$

When both sums are zero (flat bars only), IMI defaults to 50.0 (neutral).

### 4. State Management

The indicator implements `ITValuePublisher` directly (not `AbstractBase`) because it requires `TBar` input (OHLC data). Rolling sums (`_gainSum`, `_lossSum`) are saved/restored for bar correction via `_savedGainSum` / `_savedLossSum`.

## Mathematical Foundation

### Core Formula

$$
\text{IMI} = 100 \times \frac{\sum_{i=1}^{n} G_i}{\sum_{i=1}^{n} G_i + \sum_{i=1}^{n} L_i}
$$

where:

- $G_i = \max(C_i - O_i, 0)$ (gain on bullish bars)
- $L_i = \max(O_i - C_i, 0)$ (loss on bearish bars)
- $n$ = lookback period (default 14)

### Key Levels

| Level | Interpretation |
|-------|---------------|
| > 70 | Overbought: strong bullish intraday pressure |
| < 30 | Oversold: strong bearish intraday pressure |
| 50 | Neutral: balanced buying/selling pressure |

### Comparison with RSI

| Property | RSI | IMI |
|----------|-----|-----|
| Input | Close-to-close change | Open-to-close change |
| Measures | Inter-session momentum | Intra-session momentum |
| Requires previous bar | Yes | No (self-contained) |
| Smoothing | Wilder's smoothing (EMA) | Simple sum (no smoothing) |
| Range | 0-100 | 0-100 |
| Default period | 14 | 14 |

### Default Parameters

| Parameter | Default | Purpose |
|-----------|---------|---------|
| period | 14 | Lookback window for gain/loss sums |

### Warmup

$$
\text{WarmupPeriod} = \text{period}
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | close - open |
| CMP | 1 | classify gain vs loss |
| ADD/SUB | 2 | rolling sum update |
| DIV | 1 | IMI ratio |
| MUL | 1 | scale to 100 |
| **Total** | **~6 ops** | O(1) per bar |

### Batch Mode

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Rolling sum, no re-scan |
| Total | O(n) | Linear scan |
| Memory | O(period) | Two ring buffers |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic, no approximation |
| **Timeliness** | 8/10 | No smoothing lag beyond window |
| **Smoothness** | 5/10 | Can be choppy in ranging markets |
| **Simplicity** | 8/10 | Straightforward gain/loss ratio |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | Matches within tolerance |
| **TA-Lib** | N/A | No IMI function |
| **Tulip** | N/A | No IMI function |
| **Ooples** | ✅ | Matches within tolerance |
| **CQG** | ✅ | Reference implementation matches |

## Common Pitfalls

1. **TBar input required**: IMI needs Open and Close prices. Passing single values (TValue) is not supported. The indicator implements `ITValuePublisher` directly, not `AbstractBase`.

2. **Doji bars**: When Open equals Close, both Gain and Loss are zero. These bars contribute nothing to either sum but still age out of the window.

3. **All-zero edge case**: If all bars in the window are Dojis, both sums are zero. The implementation returns 50.0 (neutral) to avoid division by zero.

4. **Not smoothed**: Unlike RSI, which uses Wilder's smoothing (exponential), IMI uses simple sums. This makes it more responsive but also noisier.

5. **Timeframe sensitivity**: On daily charts, open-close captures overnight gaps plus session direction. On 1-minute charts, the open-close relationship is less meaningful. Choose timeframes where the open price carries information.

6. **NaN handling**: Non-finite Open or Close values cause the bar to be skipped, preserving the last valid IMI value.

## References

- Chande, T. S., & Kroll, S. (1994). "The New Technical Trader." Wiley.
- Investopedia: "Intraday Momentum Index (IMI) Definition."
- CQG: "Intraday Momentum Index (IMI)" Technical Reference.
