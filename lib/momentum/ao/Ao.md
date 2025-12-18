# AO - Awesome Oscillator

A momentum indicator that strips away noise to reveal the market's immediate velocity compared to its broader trend. It quantifies the gap between short-term and long-term market consensus using median prices rather than closes.

## What It Does

The Awesome Oscillator (AO) measures market momentum by comparing the last 5 bars of activity against the last 34 bars. Unlike traditional oscillators that fixate on closing prices, AO uses the **Median Price** (`(High + Low) / 2`) to capture the true center of the day's trading range.

The result is a histogram that fluctuates above and below a zero line. When the histogram is positive, short-term momentum is outpacing the long-term trend (bullish). When negative, the long-term trend is dominating (bearish). It serves as a non-lagging confirmation of trend direction and a precise tool for spotting reversals.

## Historical Context

Bill Williams introduced the Awesome Oscillator in his "Chaos Theory" of trading, presumably because "Reasonably Good Oscillator" didn't have the same marketing punch. Williams argued that standard indicators using closing prices missed the volatility that happens *during* the bar. By focusing on the median price, AO attempts to reflect the market's "balance point" rather than just its finish line.

It is a core component of the Williams Trading System, often used in conjunction with the Alligator indicator to confirm trend entries.

## How It Works

The calculation is elegantly simple, relying on the difference between two Simple Moving Averages (SMA) of the Median Price.

### The Math

$$Median Price = \frac{High + Low}{2}$$

$$AO = SMA(Median Price, 5) - SMA(Median Price, 34)$$

Where:

- **Fast SMA (5)**: Represents the current market momentum.
- **Slow SMA (34)**: Represents the broader market trend.

### The Logic

1. **Median Price Calculation**: For every bar, we first determine the midpoint of the trading range.
2. **Smoothing**: We smooth these midpoints over two distinct timeframes.
3. **Differential**: We subtract the slow average from the fast average.
    - **Positive AO**: The fast average is above the slow average (Momentum is Up).
    - **Negative AO**: The fast average is below the slow average (Momentum is Down).

## Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `fastPeriod` | `int` | 5 | The lookback period for the short-term momentum. |
| `slowPeriod` | `int` | 34 | The lookback period for the long-term trend. |

*Note: While 5 and 34 are the canonical "Williams" settings, the architecture supports any positive integer values.*

## Performance Profile

The AO implementation is designed for high-frequency and zero-allocation environments.

- **Complexity**: $O(1)$ per update. The calculation relies on two internal SMA instances, which maintain running sums.
- **Memory**: Constant space. It stores only the state required for the two SMAs (circular buffers for the periods).
- **Allocations**: Zero heap allocations during the `Update` cycle.

| Operation | Time Complexity | Space Complexity |
|-----------|-----------------|------------------|
| Update    | $O(1)$          | $O(1)$           |
| Batch     | $O(N)$          | $O(N)$           |

## Interpretation

AO is primarily a histogram, and its signals come from the bars' color (direction) and position relative to zero.

### 1. Zero Line Crossover

The most basic signal.

- **Bullish Cross**: AO crosses from negative to positive. The short-term momentum is overtaking the long-term trend.
- **Bearish Cross**: AO crosses from positive to negative. The short-term momentum is collapsing below the long-term trend.

### 2. Twin Peaks

A divergence pattern.

- **Bullish Twin Peaks**: Two lows below the zero line, where the second low is higher (closer to zero) than the first, followed by a green bar.
- **Bearish Twin Peaks**: Two highs above the zero line, where the second high is lower than the first, followed by a red bar.

### 3. The Saucer

A continuation signal.

- **Bullish Saucer**: AO is above zero. The histogram creates a "dip" (Red, Red, Green). The signal is the first Green bar.
- **Bearish Saucer**: AO is below zero. The histogram creates a "rally" (Green, Green, Red). The signal is the first Red bar.

## Architecture Notes

The `Ao` class is a composite indicator. It does not implement the smoothing logic itself but rather orchestrates two `Sma` instances.

- **Input Handling**: The `Update(TBar)` method automatically extracts the `(High + Low) / 2` median price before passing it to the internal SMAs.
- **State Management**: Resetting the AO propagates the reset to both internal SMAs, ensuring complete state clearance.
- **Warmup**: The `IsHot` property is tied to the `slowPeriod` SMA. The indicator is considered valid only when the slow SMA has filled its buffer.

## References

- Williams, Bill. *Trading Chaos: Maximize Profits with Proven Technical Techniques*. Wiley, 1995.
- Investopedia: [Awesome Oscillator](https://www.investopedia.com/terms/a/awesomeoscillator.asp)

## C# Usage

```csharp
using QuanTAlib;

// 1. Standard Initialization (Williams defaults: 5, 34)
var ao = new Ao();

// 2. Custom Initialization
var customAo = new Ao(fastPeriod: 10, slowPeriod: 50);

// 3. Processing a Bar (Standard Use Case)
// AO requires High and Low prices to calculate Median Price
var bar = new TBar(DateTime.UtcNow, open: 100, high: 105, low: 95, close: 102, volume: 1000);
var result = ao.Update(bar);

Console.WriteLine($"AO: {result.Value:F2}");

// 4. Processing a Value (Advanced Use Case)
// If you pre-calculate Median Price or want to use Close price instead
double medianPrice = (bar.High + bar.Low) / 2;
var valueResult = ao.Update(new TValue(bar.Time, medianPrice));

// 5. Batch Calculation
var series = new TBarSeries(); 
// ... populate series ...
var aoSeries = Ao.Batch(series);
