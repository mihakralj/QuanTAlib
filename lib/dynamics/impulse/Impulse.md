# IMPULSE: Elder Impulse System

> "The Impulse System identifies inflection points where a trend speeds up or slows down." -- Alexander Elder, *Come Into My Trading Room*

The Elder Impulse System combines a 13-period exponential moving average (trend inertia) with the MACD(12,26,9) histogram (momentum) to classify each price bar into one of three states: bullish, bearish, or neutral. It is the rare indicator that answers "should I be trading this direction right now?" with a single color.

## Historical Context

Alexander Elder introduced the Impulse System in his 2002 book *Come Into My Trading Room*. Elder, a psychiatrist turned trader, designed it as a censorship system: green bars permit long entries, red bars permit short entries, and blue bars prohibit new positions in either direction. The system enforces discipline by requiring both trend and momentum to align before committing capital.

The intellectual lineage runs through Gerald Appel's MACD (1979), which separated trend from momentum, and Thomas Aspray's MACD Histogram (1986), which revealed the rate of change within the MACD itself. Elder's contribution was recognizing that combining EMA slope (inertia proxy) with histogram slope (momentum proxy) produces a decision filter superior to either component alone.

Most implementations treat this as a visual overlay with colored bars. QuanTAlib exposes it as a programmatic signal (+1, 0, -1) for algorithmic consumption, while the Quantower adapter provides the traditional color-coded visualization.

## Architecture

### Component Composition

The Impulse indicator composes two child indicators internally:

1. **EMA(13)** on close price: measures trend inertia via exponential smoothing
2. **MACD(12,26,9)**: provides the histogram (MACD Line minus Signal Line) for momentum measurement

Both child indicators manage their own state, warmup compensation, and bar correction. The Impulse class tracks only the previous EMA value and previous histogram value for slope comparison.

### Signal Classification

```text
EMA_slope    = sign(EMA_current - EMA_previous)
Hist_slope   = sign(Histogram_current - Histogram_previous)

Signal = +1 (Green/Bullish):  EMA_slope > 0  AND  Hist_slope > 0
Signal = -1 (Red/Bearish):    EMA_slope < 0  AND  Hist_slope < 0
Signal =  0 (Blue/Neutral):   otherwise
```

The neutral state fires whenever the two components disagree. This is the system's core value proposition: it identifies transition zones where conviction is insufficient for new entries.

## Mathematical Foundation

### EMA Component

The 13-period EMA uses the standard recursive formulation:

$$\alpha = \frac{2}{n + 1} = \frac{2}{14} \approx 0.1429$$

$$\text{EMA}_t = \alpha \cdot \text{Close}_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

QuanTAlib's EMA implementation includes warmup bias compensation:

$$\text{EMA}_{\text{corrected}} = \frac{\text{EMA}_{\text{raw}}}{1 - (1-\alpha)^n}$$

### MACD Histogram Component

$$\text{MACD Line} = \text{EMA}(12) - \text{EMA}(26)$$

$$\text{Signal Line} = \text{EMA}(9, \text{MACD Line})$$

$$\text{Histogram} = \text{MACD Line} - \text{Signal Line}$$

The histogram is the second derivative of price (acceleration), making the Impulse System a combined first-derivative (EMA slope) and second-derivative (histogram slope) filter.

### Warmup Period

$$W = \max(\text{emaPeriod}, \text{macdSlow}) + \text{macdSignal} - 1 = \max(13, 26) + 9 - 1 = 34$$

The indicator requires 34 bars before producing valid signals. IsHot becomes true when both child indicators are warmed up and at least two comparison values exist.

## Performance Profile

| Metric | Value |
| :----- | :---- |
| Update complexity | O(1) per bar |
| Memory | 3 internal EMA states + MACD state + 4 doubles for comparison |
| Allocations in Update | Zero (delegates to child indicator Update methods) |
| SIMD potential | None (serial comparison logic) |

### Quality Metrics

| Metric | Score (1-10) |
| :----- | :----------- |
| Lag | 4 (moderate; EMA(13) + histogram smoothing introduce delay) |
| Noise rejection | 7 (requires dual confirmation) |
| Signal clarity | 9 (ternary output with no ambiguity) |
| Computational efficiency | 9 (pure O(1) composition) |
| Parameter sensitivity | 6 (Elder's defaults are widely used; customization possible but rarely needed) |

## Validation

No external libraries (TA-Lib, Skender, Tulip, OoplesFinance) implement the Elder Impulse System as a standalone indicator. Validation uses self-consistency checks:

| Test | Method |
| :--- | :----- |
| EMA identity | Impulse EMA output matches standalone EMA(13) |
| Signal correctness | Manual EMA + MACD comparison produces identical signals |
| Streaming == Batch | Streaming and batch modes produce identical EMA values |
| Determinism | Same input sequence produces identical output |
| Directional | Steady uptrend produces +1; steady downtrend produces -1 |

## Interpretation

The Impulse System operates as a **permission filter**, not a signal generator:

- **Green (+1):** Both inertia and momentum favor bulls. Long entries permitted; short entries prohibited.
- **Red (-1):** Both inertia and momentum favor bears. Short entries permitted; long entries prohibited.
- **Blue (0):** Disagreement between trend and momentum. No new entries in either direction; existing positions may be held or tightened.

### Multi-Timeframe Application

Elder recommends using the Impulse System across two timeframes (5:1 ratio):

1. Weekly chart: determines the "big picture" trend direction
2. Daily chart: identifies entry points aligned with the weekly trend

Trade only when the higher timeframe is not red (for longs) or not green (for shorts).

## Common Pitfalls

1. **Using Impulse as an entry signal instead of a filter.** The system identifies when trading is permitted, not when to trade. Combine with entry triggers (pullbacks, breakouts).

2. **Ignoring the neutral state.** Blue bars are not "do nothing" -- they indicate transitions. Watch for the sequence blue-then-green or blue-then-red for early signals.

3. **Overriding the prohibition.** Going long on a red bar or short on a green bar defeats the system's purpose. The whole point is discipline enforcement.

4. **Expecting the system to catch tops and bottoms.** The EMA and histogram lag price. By design, the system trades the middle of moves, not the extremes.

5. **Modifying the default parameters without understanding the impact.** The 13-period EMA and 12/26/9 MACD are Elder's specific design choices. Shorter periods increase noise; longer periods increase lag. The defaults balance both.

6. **Confusing EMA direction with price direction.** Price can close higher while EMA still falls (or vice versa). The EMA slope represents smoothed trend, not raw price movement.

## References

- Elder, A. (2002). *Come Into My Trading Room*. John Wiley and Sons.
- Elder, A. (1993). *Trading for a Living*. John Wiley and Sons.
- Appel, G. (1979). "The Moving Average Convergence-Divergence Method."
- Aspray, T. (1986). "MACD Histogram." *Technical Analysis of Stocks and Commodities*.
- StockCharts.com. "Elder Impulse System." ChartSchool.
