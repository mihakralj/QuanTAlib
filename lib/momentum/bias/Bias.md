# BIAS: Price Deviation from Moving Average (also known as Disparity Index)

> *When traders ask 'how overbought is it?', they're really asking how far price has strayed from its anchor. Bias answers that question in percentage terms, telling you whether the current price is 5% above or 10% below its moving average. It's the market's stretch marks made visible.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Bias)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [bias.pine](bias.pine)                       |

- The Bias indicator measures the percentage difference between the current price and its Simple Moving Average (SMA).
- **Similar:** [ROC](../roc/Roc.md), [CFO](../../oscillators/cfo/Cfo.md) | **Complementary:** Moving average for signal | **Trading note:** Price bias from moving average; (Price−MA)/MA × 100. Mean-reversion signal at extremes.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Bias indicator measures the percentage difference between the current price and its Simple Moving Average (SMA). A positive bias indicates price is above the average (potentially overbought), while negative bias suggests price is below average (potentially oversold). This is one of the simplest yet most effective tools for identifying mean-reversion opportunities.

## Historical Context

Bias (also known as "Rate of Change from MA" or "Price Oscillator Percentage") has been used by traders since the early days of technical analysis. The concept is straightforward: prices tend to oscillate around their moving averages, and extreme deviations often precede reversions.

In TradingView's PineScript, this indicator is known simply as "Bias" and represents the normalized distance between price and its moving average. Unlike momentum oscillators that measure price change over time, Bias measures price deviation from a smoothed reference point.

## Architecture & Physics

### The Mean-Reversion Foundation

Markets exhibit mean-reversion tendencies across multiple timeframes. When price deviates significantly from its moving average, several forces conspire to pull it back:

1. **Value Seekers**: Buyers appear when price falls too far below average
2. **Profit Taking**: Sellers emerge when price rises too far above average
3. **Statistical Gravity**: Extreme deviations are by definition unsustainable

Bias quantifies this deviation in normalized (percentage) terms, making it comparable across different price scales.

### Calculation Formula

The Bias is computed as:

$$\text{Bias} = \frac{P - \text{SMA}}{\text{SMA}} = \frac{P}{\text{SMA}} - 1$$

Where:

* $P$ = Current price
* $\text{SMA}$ = Simple Moving Average over the specified period

This formula normalizes the deviation as a ratio, expressing it as a decimal. A Bias of 0.05 means price is 5% above the SMA; a Bias of -0.10 means price is 10% below.

### O(1) Streaming Implementation

Rather than recalculating the SMA from scratch each tick, this implementation maintains a running sum with RingBuffer:

```csharp
// Efficient sliding window sum
_sum = _sum - oldValue + newValue;
double sma = _sum / Period;
double bias = (currentPrice / sma) - 1.0;
```

This achieves constant-time updates regardless of period length.

## Mathematical Foundation

### 1. Simple Moving Average

$$\text{SMA}_t = \frac{1}{n} \sum_{i=0}^{n-1} P_{t-i}$$

Where $n$ is the period and $P_t$ is the price at time $t$.

### 2. Bias Calculation

$$\text{Bias}_t = \frac{P_t - \text{SMA}_t}{\text{SMA}_t}$$

Equivalently:

$$\text{Bias}_t = \frac{P_t}{\text{SMA}_t} - 1$$

### 3. Relationship to Price

Given the Bias value, you can recover the implied SMA:

$$\text{SMA}_t = \frac{P_t}{1 + \text{Bias}_t}$$

### 4. Boundary Behavior

* When $P_t = \text{SMA}_t$: $\text{Bias} = 0$
* When $P_t > \text{SMA}_t$: $\text{Bias} > 0$
* When $P_t < \text{SMA}_t$: $\text{Bias} < 0$
* When $\text{SMA}_t = 0$: $\text{Bias} = 0$ (division guard)

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 3 | 1 | 3 |
| MUL | 0 | 3 | 0 |
| DIV | 2 | 15 | 30 |
| Buffer Access | 2 | 3 | 6 |
| **Total** | **7** | — | **~39 cycles** |

Division dominates the cost (two divisions: one for SMA, one for bias ratio).

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact formula, no approximation |
| **Timeliness** | 7/10 | Inherits SMA lag |
| **Smoothness** | 8/10 | SMA provides inherent smoothing |
| **Interpretability** | 10/10 | Direct percentage meaning |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No direct equivalent |
| **Skender** | N/A | No direct equivalent |
| **Tulip** | N/A | No direct equivalent |
| **TradingView** | ✅ | Matches PineScript Bias indicator |
| **Mathematical** | ✅ | Validated against manual calculation |

## Use Cases

### 1. Mean-Reversion Trading

* **Long Entry**: Bias < -0.05 (price 5%+ below SMA)
* **Short Entry**: Bias > 0.05 (price 5%+ above SMA)
* **Exit**: Bias returns toward zero

### 2. Trend Confirmation

Persistent positive Bias confirms uptrend; persistent negative confirms downtrend.

### 3. Overbought/Oversold Detection

Extreme Bias values (e.g., ±10%) suggest extended conditions ripe for reversal.

### 4. Multi-Timeframe Analysis

Compare Bias across different periods to identify nested mean-reversion setups.

## API Usage

### Streaming Mode

```csharp
var bias = new Bias(period: 20);
foreach (var price in prices)
{
    var result = bias.Update(new TValue(DateTime.UtcNow, price));
    Console.WriteLine($"Bias: {result.Value:P2}");  // e.g., "Bias: 3.45%"
}
```

### Batch Mode

```csharp
var series = new TSeries();
// ... populate series ...
var results = Bias.Batch(series, period: 20);
```

### Span Mode (Zero Allocation)

```csharp
double[] input = new double[1000];
double[] output = new double[1000];
// ... populate input ...
Bias.Batch(input.AsSpan(), output.AsSpan(), period: 20);
```

### Event-Driven Mode

```csharp
var source = new TSeries();
var bias = new Bias(source, period: 20);
// Bias automatically updates when source publishes
source.Add(new TValue(DateTime.UtcNow, 100.0));
```

## Common Pitfalls

1. **Interpreting Raw Values**: Bias of 0.05 means 5%, not 0.05%. Display as percentage or multiply by 100 for human consumption.

2. **Period Selection**: Shorter periods (10-20) respond faster but generate more noise. Longer periods (50-200) are smoother but lag more.

3. **Asymmetric Interpretation**: A +10% Bias doesn't necessarily have the same significance as -10% Bias due to asymmetric price distributions.

4. **Division by Zero**: When SMA is zero (rare but possible with price data starting at zero), the indicator returns 0 as a safeguard.

5. **Not a Standalone Signal**: Extreme Bias suggests potential reversal but doesn't guarantee it. Prices can stay overbought/oversold longer than expected.

6. **Warmup Period**: The indicator needs `period` bars to reach full window. Before that, it uses a growing window for calculation.

## When to Use Bias

**Use it when:**

* You want a simple, interpretable overbought/oversold measure
* Mean-reversion strategies are your focus
* You need to compare deviation across instruments with different price scales
* Quick assessment of price extension from average

**Skip it when:**

* You need trend-following signals (use moving average crossovers instead)
* Strong trending markets where mean-reversion fails
* You prefer momentum-based indicators (RSI, ROC)
* Price data starts at or crosses zero

## References

* TradingView. "Bias Indicator (PineScript)." *TradingView Documentation*.
* Kaufman, P.J. (2013). "Trading Systems and Methods." *Wiley Trading*, 5th Edition.