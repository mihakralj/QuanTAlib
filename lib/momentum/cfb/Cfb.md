# CFB - Composite Fractal Behavior

A sophisticated trend duration index that measures the "fractal efficiency" of price movements across multiple time scales. It answers the question: "How long has the market been trending efficiently?"

## What It Does

Composite Fractal Behavior (CFB) analyzes the market's geometry to determine the quality and persistence of a trend. Unlike standard indicators that rely on a single fixed period (e.g., RSI-14), CFB scans a wide spectrum of lookback lengths (e.g., from 2 to 192 bars) simultaneously.

It calculates the "fractal efficiency"—how straight the price path is—for each length. It then combines the lengths that show efficient trending behavior into a single composite index. The result is a value representing the approximate duration (in bars) of the current trend.

## Historical Context

Developed by Mark Jurik of Jurik Research, CFB addresses the "lag vs. noise" dilemma by avoiding it entirely. Instead of smoothing price data (which adds lag), it measures the structural integrity of the price action itself. It was designed to be an adaptive input for other indicators, allowing them to adjust their speed based on whether the market is trending or chopping.

## How It Works

The algorithm evaluates the "straightness" of price movement over many different timeframes and aggregates the results.

### The Math

For each lookback length $L$ in the configured set:

1. **Calculate Efficiency Ratio**:
    $$ \text{Ratio}_L = \frac{|\text{Price}_t - \text{Price}_{t-L}|}{\sum_{i=0}^{L-1} |\text{Price}_{t-i} - \text{Price}_{t-i-1}|} $$
    *Numerator*: Net distance traveled (straight line).
    *Denominator*: Total path length (volatility).

2. **Filter**:
    We discard any length where $\text{Ratio}_L < 0.25$. If the efficiency is below 25%, the movement is considered "noise" or "chop" at that timeframe.

3. **Composite Weighting**:
    We calculate a weighted average of the qualifying lengths, using the efficiency ratio itself as the weight.
    $$ \text{CFB} = \frac{\sum (L \cdot \text{Ratio}_L)}{\sum \text{Ratio}_L} $$

4. **Decay**:
    If no lengths qualify (the market is chaotic at all scales), the CFB value decays toward 1.0.

## Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `lengths` | `int[]` | `[2, 4, ..., 192]` | An array of lookback periods to analyze. The default is a dense set of even numbers from 2 to 192. |

## Performance Profile

Despite its complexity, the implementation is optimized for real-time use.

- **Complexity**: $O(K)$ per update, where $K$ is the number of lengths analyzed (default 96).
- **Optimization**: It maintains running sums of volatility for each length, ensuring that the denominator calculation is $O(1)$ rather than $O(L)$.
- **Memory**: $O(L_{max} + K)$. It requires a history buffer equal to the maximum lookback length, plus state for each length's running sum.

| Operation | Time Complexity | Space Complexity |
|-----------|-----------------|------------------|
| Update    | $O(K)$          | $O(L_{max})$     |
| Batch     | $O(N \cdot K)$  | $O(N)$           |

## Interpretation

CFB is primarily a "state" indicator rather than a directional one.

### 1. Trend Duration

The output value roughly corresponds to the number of bars the current trend has been valid.

- **High Values**: Strong, persistent trend.
- **Low Values**: Choppy, sideways market.

### 2. Trend Strength

- **Rising CFB**: The trend is becoming more efficient or extending in duration.
- **Falling CFB**: The trend is breaking down; volatility is increasing relative to net movement.

### 3. Adaptive Input

CFB is ideal for driving the parameters of other indicators. For example, you can use CFB to dynamically adjust the period of a Moving Average:

- **High CFB** $\rightarrow$ Use a longer period (capture the trend).
- **Low CFB** $\rightarrow$ Use a shorter period (react to chop).

## Architecture Notes

- **Running Sums**: The class maintains an array of running sums for volatility. When a new bar arrives, it adds the new volatility and subtracts the volatility from $L$ bars ago. This keeps the efficiency calculation fast.
- **State Management**: The `Update` method handles `isNew` logic carefully to ensure running sums are rolled back correctly during intra-bar updates.
- **Default Lengths**: If no lengths are provided, the constructor generates a dense array `[2, 4, 6, ..., 192]`.

## References

- Jurik Research: [CFB - Composite Fractal Behavior](http://jurikres.com/catalog1/ms_cfb.htm)

## C# Usage

```csharp
using QuanTAlib;

// 1. Standard Initialization (Default lengths 2..192)
var cfb = new Cfb();

// 2. Custom Initialization (Specific lengths)
var customCfb = new Cfb(new int[] { 10, 20, 50, 100 });

// 3. Process a Bar
// CFB uses Close price by default (or whatever value is passed)
var result = cfb.Update(new TValue(DateTime.UtcNow, 105.5));

Console.WriteLine($"Trend Duration: {result.Value:F1} bars");

// 4. Batch Calculation
var series = new TBarSeries();
// ... populate series ...
var cfbSeries = Cfb.Batch(series);
