# EMA: Exponential Moving Average

> "The EMA exists because traders in the 1960s were tired of their SMA jumping like a caffeinated squirrel every time a price from 20 days ago dropped out of the window. So they invented a filter that remembers everything but cares about nothing old. Brilliant."

EMA (Exponential Moving Average) is the standard by which all other averages are judged. Unlike the SMA, which treats data from 10 days ago with the same reverence as data from 10 seconds ago, the EMA understands that in markets, recency is relevance. It applies an exponentially decaying weight to older prices, reacting faster to new information while never completely forgetting the past. The AK-47 of technical indicators: been around forever, everyone uses it, not fancy, but it works.

## Historical Context

The EMA was brought to the financial world to solve the "drop-off effect" of the SMA. Picture this: your 20-day SMA is cruising along, and suddenly an outlier price from exactly 20 days ago drops out of the window. Your average jumps. Your signal fires. Your algorithm buys. The market laughs. By using a recursive formula, the EMA includes *all* past data in its calculation, with weights diminishing exponentially toward zero. No drop-off, no surprises, no caffeinated squirrel behavior. It is the infinite impulse response (IIR) filter of the trading world.

## Architecture & Physics

The EMA is defined by its smoothing factor, $\alpha$:

- **High $\alpha$ (close to 1)**: Fast decay, responsive, noisy. Every tick matters. Your signal will fire at shadows.
- **Low $\alpha$ (close to 0)**: Slow decay, smooth, laggy. You'll catch the trend, but you'll also be late to every party.

The relationship between period $N$ and $\alpha$ is: $\alpha = \frac{2}{N + 1}$. A 10-period EMA has $\alpha \approx 0.18$. A 100-period EMA has $\alpha \approx 0.02$. The period is just a human-friendly way to express exponential decay.

### The Compensator (Warmup Correction)

Here's where QuanTAlib diverges from the crowd. A standard EMA starts at zero (or seeds with the first price) and takes $3N$ bars to converge within 5% of its true value. During warmup, you're trading on lies.

QuanTAlib implements a **mathematical compensator** that corrects for initialization bias. The EMA is statistically valid from bar one. Not approximately valid. Actually valid. This means the first 14 bars of a 10-period EMA will differ from TA-Lib. TA-Lib is wrong. QuanTAlib is correct. File your complaints with the laws of mathematics.

## Mathematical Foundation

The standard recursive formula:

$$ \alpha = \frac{2}{N + 1} $$

$$ \text{EMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{t-1} $$

This can be rewritten using fused multiply-add for precision:

$$ \text{EMA}_t = \text{FMA}(\text{EMA}_{t-1}, (1 - \alpha), \alpha \cdot P_t) $$

### Bias Compensation

To handle the initialization bias (where $\text{EMA}_0$ is unknown), the sum of weights is tracked:

$$ E_t = (1 - \alpha)^t $$

$$ \text{Corrected EMA}_t = \frac{\text{Uncorrected EMA}_t}{1 - E_t} $$

This ensures the EMA doesn't lie to you for the first $N$ bars like every other library.

## Performance Profile

Benchmarked on Apple M4, .NET 10.0, AdvSIMD, 500,000 bars:

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput (Span)** | 381 μs / 500K bars | 0.76 ns/bar, ~1.3B bars/sec |
| **Throughput (Streaming)** | ~2 ns/bar | Single Update() call |
| **Allocations (Hot Path)** | 0 bytes | Verified via BenchmarkDotNet |
| **Complexity** | O(1) | Single FMA operation |
| **State Size** | 32 bytes | Two doubles (EMA, compensator) |

### Comparative Performance

| Library | Time (500K bars) | Allocated | Relative Speed |
| :--- | ---: | ---: | :--- |
| **QuanTAlib (Span)** | 381 μs | 0 B | 1.0× (baseline) |
| **Tulip** | 353 μs | 0 B | 0.93× |
| **TA-Lib** | 357 μs | 34 B | 0.94× |
| **Skender** | 10,635 μs | 23.6 MB | 27.9× slower |

QuanTAlib is competitive with C-based libraries (Tulip, TA-Lib) while providing bias-corrected results and zero allocations.

## Usage Examples

```csharp
// Streaming: Process one bar at a time
var ema = new Ema(20);  // 20-period EMA
foreach (var bar in liveStream)
{
    var result = ema.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"EMA: {result.Value:F2}");
}

// Using alpha directly (signal processing style)
var fastEma = new Ema(0.2);  // α=0.2, roughly equivalent to period 9

// Batch processing with Span (zero allocation)
double[] prices = LoadHistoricalData();
double[] emaValues = new double[prices.Length];
Ema.Batch(prices.AsSpan(), emaValues.AsSpan(), period: 20);

// Batch processing with TSeries
var series = new TSeries();
// ... populate series ...
var results = Ema.Batch(series, period: 20);

// Event-driven chaining
var source = new TSeries();
var ema20 = new Ema(source, 20);  // Auto-updates when source changes
var ema50 = new Ema(source, 50);  // Multiple indicators on same source
source.Add(new TValue(DateTime.UtcNow, 100.0));  // Both EMAs update

// Pre-load with historical data
var ema = new Ema(20);
ema.Prime(historicalPrices);  // Ready to process live data immediately
```

## Validation

Validated against external libraries in `Ema.Validation.Tests.cs`. Tests run against 5,000 bars with tolerance of 1e-9:

| Library | Batch | Streaming | Span | Notes |
| :--- | :---: | :---: | :---: | :--- |
| **TA-Lib** | ✅ | ✅ | ✅ | Matches after warmup period (TA-Lib lacks compensator) |
| **Skender** | ✅ | ✅ | ✅ | Matches `GetEma()` |
| **Tulip** | ✅ | ✅ | ✅ | Matches `ema` indicator |
| **Ooples** | ✅ | — | — | Matches `CalculateExponentialMovingAverage()` |

Run validation: `dotnet test --filter "FullyQualifiedName~EmaValidation"`

## Common Pitfalls

1. **The "First Value" Problem**: Most libraries seed the EMA with the first price or an SMA of the first $N$ prices. Results during warmup are approximations. QuanTAlib uses a mathematical compensator, so early values will differ from TA-Lib. This is not a bug. QuanTAlib is correct; TA-Lib is approximating.

2. **Alpha vs. Period Confusion**: $N=10$ gives $\alpha \approx 0.18$. But $\alpha=0.1$ gives $N \approx 19$. Don't confuse "EMA(10)" (fast, period-based) with "EMA(0.1)" (slow, alpha-based). The constructors accept both, and they are *not* equivalent.

3. **EMA Still Lags**: The EMA is faster than SMA, but it's not magic. It still lags. A 20-period EMA lags roughly 10 bars behind price. If you want zero lag, you want a Jurik Moving Average (JMA) or Ehlers filters. But those have their own problems.

4. **Using EMA(5) on Hourly Data**: An EMA(5) on hourly bars has a half-life of about 2.5 hours. Every minor wiggle becomes a signal. Your trading bot will panic-trade its way to bankruptcy. Use longer periods on longer timeframes.

5. **Expecting Identical Results Across Libraries**: During the first $N$ bars, QuanTAlib will differ from TA-Lib, Tulip, and Skender due to bias compensation. After warmup, all libraries converge. If you're comparing results, skip the first $3N$ bars.

6. **Forgetting `isNew` for Live Data**: When processing live ticks within the same bar, use `Update(value, isNew: false)` to update without advancing state. Use `isNew: true` (default) only when a new bar opens. Getting this wrong causes your EMA to run $N$ times faster than intended.
