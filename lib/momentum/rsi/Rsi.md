# RSI: Relative Strength Index

> "Momentum is the premier anomaly." — Eugene Fama (probably, if he traded crypto)

The Relative Strength Index (RSI) is the granddaddy of momentum oscillators. Developed by J. Welles Wilder Jr. in 1978, it measures the speed and change of price movements. It oscillates between 0 and 100, identifying overbought and oversold conditions.

## Historical Context

Introduced in Wilder's seminal book *New Concepts in Technical Trading Systems*, RSI was designed to solve the problem of erratic movement in other momentum indicators. By normalizing gains and losses, Wilder created a bounded oscillator that remains relevant in every asset class from corn futures to Dogecoin.

## Architecture & Physics

RSI is built on the concept of "Average Gain" and "Average Loss". Crucially, Wilder used a smoothing method (now known as RMA or Wilder's Smoothing) rather than a simple moving average. This gives RSI a long memory—technically infinite, though the influence decays exponentially.

- **Inertia**: High (due to RMA smoothing).
- **Momentum**: Tracks price velocity.
- **Range**: Bounded [0, 100].

### The Smoothing Nuance

Many modern implementations incorrectly use SMA or EMA for the averages. QuanTAlib strictly adheres to Wilder's original RMA formula:
`NewAverage = (PreviousAverage * (Period - 1) + Current) / Period`

## Mathematical Foundation

$$ RS = \frac{\text{Average Gain}}{\text{Average Loss}} $$
$$ RSI = 100 - \frac{100}{1 + RS} $$

Where:

- **Gain**: $Close_{t} - Close_{t-1}$ (if positive, else 0)
- **Loss**: $Close_{t-1} - Close_{t}$ (if positive, else 0)
- **Average**: Smoothed using RMA (Wilder's Smoothing).

## Performance Profile

RSI requires state maintenance for the average gain and loss.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 18 ns/bar | High performance due to simple RMA calculations. |
| **Allocations** | 0 | Zero heap allocations in hot path. |
| **Complexity** | O(1) | Constant time update per bar. |
| **Accuracy** | 10/10 | Matches Wilder's definition exactly. |
| **Timeliness** | 9/10 | Very responsive to recent price changes. |
| **Overshoot** | 0/10 | Bounded [0, 100], cannot overshoot. |
| **Smoothness** | 8/10 | Smoothed via RMA, but retains volatility. |

### Zero-Allocation Design

The implementation uses a custom `Rma` logic internally to avoid creating separate indicator instances, keeping the memory footprint minimal.

## Validation

Validated against multiple external libraries to ensure correctness.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_RSI` exactly. |
| **Skender** | ✅ | Matches `GetRsi` exactly. |
| **Tulip** | ✅ | Matches `rsi` exactly. |
| **Ooples** | ✅ | Matches `CalculateRelativeStrengthIndex`. |

### Common Pitfalls

- **Initialization**: RSI requires a warmup period. The first value is typically an SMA of the initial gains/losses, followed by the RMA calculation. QuanTAlib handles this transition seamlessly.
- **Data Length**: Because of the RMA's infinite memory, RSI values can vary slightly depending on the amount of historical data provided. This is a feature, not a bug.

## Usage

```csharp
using QuanTAlib;

// 1. Streaming (Real-time)
var rsi = new Rsi(14);
TValue result = rsi.Update(new TValue(time, price));
Console.WriteLine($"RSI: {result.Value}");

// 2. Batch (Historical)
var series = new TSeries(...);
var rsiSeries = Rsi.Batch(series, 14);

// 3. Span (High-Performance)
double[] prices = ...;
double[] results = new double[prices.Length];
Rsi.Calculate(prices, results, 14);
