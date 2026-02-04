# DSP: Detrended Synthetic Price

> "Remove the trend, reveal the cycles."

The Detrended Synthetic Price (DSP) indicator, developed by John Ehlers, is a cycle analysis tool that removes trend components to expose underlying price cycles. By differencing two exponential moving averages (fast and slow), DSP creates a zero-centered oscillator that highlights momentum shifts.

## Historical Context

John Ehlers introduced the Detrended Synthetic Price as part of his cycle analysis toolkit. The indicator builds on the MACD concept but uses EMA periods derived from cycle theory: quarter-cycle (fast) and half-cycle (slow) lengths. This mathematical relationship helps isolate cycle components while suppressing trend noise.

The "synthetic" in the name refers to how DSP synthesizes a detrended view of price by subtracting the slower-reacting EMA from the faster one. When the fast EMA exceeds the slow EMA, price momentum is bullish; when below, momentum is bearish.

Unlike traditional oscillators that bound between fixed levels, DSP oscillates around zero with amplitude proportional to price volatility and cycle strength.

## Architecture & Physics

DSP uses dual EMA smoothing with bias correction during warmup to produce accurate values from the first bar.

### Core Components

1. **Period Parameter**: Base cycle length (default 40)
2. **Fast EMA**: Smoothing with period = max(2, round(period/4)) - quarter cycle
3. **Slow EMA**: Smoothing with period = max(3, round(period/2)) - half cycle
4. **Bias Correction**: Warmup decay factors eliminate EMA initialization bias
5. **State Record**: Maintains EMA values and warmup factors for rollback support

### Period Derivation

For period = 40:
- Fast period = max(2, round(40/4)) = 10
- Slow period = max(3, round(40/2)) = 20

For period = 4 (minimum):
- Fast period = max(2, round(4/4)) = max(2, 1) = 2
- Slow period = max(3, round(4/2)) = max(3, 2) = 3

### Calculation Flow

For each update:
1. Calculate fast alpha: $\alpha_f = 2 / (p_f + 1)$
2. Calculate slow alpha: $\alpha_s = 2 / (p_s + 1)$
3. Update fast EMA with bias correction
4. Update slow EMA with bias correction
5. DSP = corrected_fast_ema - corrected_slow_ema

## Mathematical Foundation

### EMA Alpha Calculation

$$
\alpha = \frac{2}{period + 1}
$$

For fast period 10: $\alpha_f = \frac{2}{11} \approx 0.1818$

For slow period 20: $\alpha_s = \frac{2}{21} \approx 0.0952$

### EMA Update (with bias correction)

The raw EMA recursion:

$$
EMA^{raw}_t = \alpha \cdot P_t + (1 - \alpha) \cdot EMA^{raw}_{t-1}
$$

The warmup decay factor tracks bias:

$$
e_t = (1 - \alpha) \cdot e_{t-1}
$$

Starting with $e_0 = 1$, this converges to 0 as the EMA warms up.

The bias-corrected EMA:

$$
EMA_t = \frac{EMA^{raw}_t}{1 - e_t}
$$

### DSP Formula

$$
DSP_t = EMA^{fast}_t - EMA^{slow}_t
$$

where both EMAs are bias-corrected.

### Properties

- **Range**: Unbounded, oscillates around zero
- **Zero Crossing**: Indicates momentum shift
- **Positive Values**: Fast EMA > Slow EMA (bullish momentum)
- **Negative Values**: Fast EMA < Slow EMA (bearish momentum)
- **Warmup**: IsHot when $e_{slow} < 0.05$ (5% remaining bias)

### Example Calculation

For period = 40 with constant price 100:

After warmup, both EMAs converge to 100:
- Fast EMA = 100
- Slow EMA = 100
- DSP = 100 - 100 = 0

For uptrend (price rising steadily):
- Fast EMA responds quicker, stays closer to current price
- Slow EMA lags behind
- DSP > 0 (positive momentum)

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~8 ns/bar | O(1) constant time |
| **Allocations** | 0 | Zero-allocation in hot path |
| **Complexity** | O(1) | Fixed operations per update |
| **Accuracy** | 10 | Exact EMA with bias correction |

### Operation Count (per update)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD/SUB | ~6 | EMA updates and DSP calculation |
| MUL | ~6 | Alpha multiplications |
| DIV | 2 | Bias correction divisions |
| FMA | 4 | Fused multiply-add for EMA |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact EMA with bias correction |
| **Timeliness** | 8/10 | Faster than traditional MACD |
| **Overshoot** | 7/10 | EMA smoothing reduces overshoot |
| **Smoothness** | 8/10 | Dual EMA provides good smoothing |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not available in TA-Lib |
| **Skender** | N/A | Not available in Skender |
| **Tulip** | N/A | Not available in Tulip |
| **PineScript** | ✅ | Validated against original DSP implementation |

DSP is validated through mathematical properties:
- Constant price produces zero DSP
- Uptrend produces positive DSP
- Downtrend produces negative DSP
- Oscillates around zero for cyclic price patterns

## Common Pitfalls

1. **Period Selection**: The period parameter represents the dominant cycle length. Use half the detected cycle period for optimal results. Default 40 works for daily data.

2. **Comparison to MACD**: DSP differs from MACD in period derivation. MACD uses arbitrary 12/26 periods; DSP uses cycle-theory-based period/4 and period/2.

3. **Warmup Behavior**: DSP includes bias correction, so early values are usable. IsHot indicates when the slow EMA bias drops below 5%.

4. **Amplitude Interpretation**: DSP amplitude scales with price level. A $1 stock and $100 stock with identical percentage moves will have 100x different DSP amplitudes.

5. **Zero Crossings**: Not all zero crossings are tradeable. Use in conjunction with cycle analysis or additional confirmation.

6. **Trending Markets**: In strong trends, DSP stays positive or negative for extended periods. Cycle analysis is most effective in ranging markets.

## Usage

```csharp
using QuanTAlib;

// Create a 40-period DSP indicator
var dsp = new Dsp(period: 40);

// Update with new values
var result = dsp.Update(new TValue(DateTime.UtcNow, 100.0));

// Access the last calculated DSP value
Console.WriteLine($"DSP: {dsp.Last.Value}");

// Chained usage
var source = new TSeries();
var dspChained = new Dsp(source, period: 40);

// Static batch calculation
var output = Dsp.Calculate(source, period: 40);

// Span-based calculation
Span<double> outputSpan = stackalloc double[source.Count];
Dsp.Batch(source.Values, outputSpan, period: 40);
```

## Applications

### Cycle Detection

DSP zero crossings help identify cycle turning points:
- DSP crosses above zero: cycle trough (potential buy)
- DSP crosses below zero: cycle peak (potential sell)

### Trend Filtering

Use DSP sign to filter trades with trend direction:
- DSP > 0: Only take long trades
- DSP < 0: Only take short trades

### Momentum Confirmation

DSP slope confirms momentum strength:
- Rising DSP: Increasing bullish momentum
- Falling DSP: Increasing bearish momentum

### Divergence Analysis

Like other oscillators, DSP divergences signal potential reversals:
- Price higher high, DSP lower high: bearish divergence
- Price lower low, DSP higher low: bullish divergence

## Comparison to Related Indicators

### DSP vs MACD

| Feature | DSP | MACD |
| :--- | :--- | :--- |
| Period basis | Cycle theory (P/4, P/2) | Arbitrary (12, 26) |
| Signal line | None (optional) | 9-period EMA |
| Bias correction | Yes | No |
| Histogram | No | Yes (MACD - Signal) |

### DSP vs Detrended Price Oscillator (DPO)

| Feature | DSP | DPO |
| :--- | :--- | :--- |
| Calculation | Fast EMA - Slow EMA | Price - SMA shifted |
| Time alignment | Current | Shifted back period/2 + 1 |
| Leading/Lagging | Leading | Centered (neither) |

## References

- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley.
- Ehlers, J.F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley.
- TradingView PineScript: DSP implementation in cycle analysis scripts.