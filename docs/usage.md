# Usage Guides

> "Make it work, make it right, make it fast."  Kent Beck (QuanTAlib skips straight to fast, assuming you already made it right)

QuanTAlib provides four operating modes. Each mode optimizes for different constraints. Choosing the wrong mode costs either performance or developer sanity. Sometimes both.

## Choosing Your Mode

| Mode | Best For | Allocations | Throughput | Complexity |
| :--- | :------- | :---------: | :--------: | :--------: |
| Span | Backtesting, batch processing | Zero | Highest | Low |
| Streaming | Live trading, tick-by-tick | Minimal | High | Medium |
| Batch (TSeries) | Exploration, notebooks | Some | Medium | Low |
| Event-Driven | Reactive systems | Varies | Medium | High |

**Rule of thumb:** Start with Span mode for research. Move to Streaming for production. Use Event-Driven only when the architecture demands it.

## Mode 1: Span (High Performance)

The fastest path. Operates directly on memory spans. Zero heap allocations. Maximum cache efficiency.

**When to use:**

- Processing historical data in bulk
- Backtesting where every microsecond matters
- SIMD-optimized batch calculations
- Memory-constrained environments

```csharp
using QuanTAlib;

// Prepare buffers (allocate once, reuse many times)
double[] prices = GetPriceHistory();  // Your data source
double[] smaResults = new double[prices.Length];
double[] emaResults = new double[prices.Length];

// Calculate (zero allocations inside these calls)
Sma.Calculate(prices.AsSpan(), smaResults.AsSpan(), period: 14);
Ema.Calculate(prices.AsSpan(), emaResults.AsSpan(), period: 14);

// Access results
Console.WriteLine($"Last SMA: {smaResults[^1]:F4}");
Console.WriteLine($"Last EMA: {emaResults[^1]:F4}");
```

**Performance note:** Span mode processes 500,000 bars in approximately 300 ¼s (0.6 ns/bar) for simple indicators like SMA. Complex indicators like JMA take longer but still measure in microseconds.

**Gotcha:** The destination span must be at least as long as the source span. Passing mismatched lengths throws `ArgumentException`.

## Mode 2: Streaming (Real-Time)

Updates one value at a time. Maintains internal state between calls. Handles bar corrections via the `isNew` parameter.

**When to use:**

- Live trading with real-time feeds
- Tick-by-tick processing
- Systems that update mid-bar
- Memory-sensitive applications (no full history needed)

```csharp
using QuanTAlib;

// Initialize once
var sma = new Sma(period: 14);
var ema = new Ema(period: 14);

// Update loop (called from your data feed)
void OnTick(DateTime timestamp, double price)
{
    // Update returns TValue struct: { Time, Value, IsHot }
    TValue smaResult = sma.Update(new TValue(timestamp, price));
    TValue emaResult = ema.Update(new TValue(timestamp, price));
    
    // IsHot indicates warmup period is complete
    if (smaResult.IsHot && emaResult.IsHot)
    {
        double spread = emaResult.Value - smaResult.Value;
        ProcessSignal(spread);
    }
}
```

### Bar Correction Pattern

Real-time feeds often send multiple updates for the same bar (as price changes within the bar). The `isNew` parameter handles this:

```csharp
// New bar arrives (isNew = true, the default)
indicator.Update(new TValue(barTime, openPrice), isNew: true);

// Price updates within same bar (isNew = false)
indicator.Update(new TValue(barTime, currentPrice), isNew: false);
indicator.Update(new TValue(barTime, currentPrice), isNew: false);
// ... more updates as price changes ...

// Next bar arrives (isNew = true again)
indicator.Update(new TValue(nextBarTime, newOpenPrice), isNew: true);
```

**What happens internally:** When `isNew = false`, the indicator rolls back its state to before the previous update, then applies the new value. This ensures mid-bar corrections produce the same final result as if only the last value had been seen.

**Gotcha:** Forgetting to set `isNew = false` for corrections causes the indicator to treat each correction as a new bar. The output will be wrong and debugging will be painful.

## Mode 3: Batch (TSeries)

Wraps calculations in `TSeries` objects that manage timestamps and alignment. More convenient than Span mode, slightly less performant.

**When to use:**

- Jupyter-style exploration
- Prototyping trading strategies
- Situations where convenience beats raw speed
- Multi-indicator analysis with automatic alignment

```csharp
using QuanTAlib;

// Create time series
var prices = new TSeries();
prices.Add(DateTime.UtcNow, 100.0);
prices.Add(DateTime.UtcNow.AddMinutes(1), 101.5);
prices.Add(DateTime.UtcNow.AddMinutes(2), 99.8);
// ... add historical data ...

// Calculate (returns new TSeries aligned with input)
var sma = new Sma(prices, period: 14);
var ema = new Ema(prices, period: 14);

// Access individual values
Console.WriteLine($"Latest SMA: {sma.Last.Value:F4}");
Console.WriteLine($"SMA at index 5: {sma[5].Value:F4}");

// Access as span for downstream processing
ReadOnlySpan<double> values = sma.Values;
```

**Gotcha:** Creating a new indicator with a TSeries computes all historical values immediately. For large datasets, this blocks until complete. For streaming scenarios, use Mode 2 instead.

## Mode 4: Event-Driven (Reactive)

Indicators can subscribe to other indicators or data sources. Changes propagate automatically through the dependency graph.

**When to use:**

- Complex indicator chains
- Reactive architectures
- Systems where manual update ordering is error-prone
- UI binding scenarios

```csharp
using QuanTAlib;

// Build the dependency graph
var source = new TSeries();
var smaFast = new Sma(source, period: 10);
var smaSlow = new Sma(source, period: 20);
var crossover = new Crossover(smaFast, smaSlow);

// Subscribe to events
crossover.Pub += (sender, args) => 
{
    if (args.Tick.Value > 0)
        Console.WriteLine("Bullish crossover detected");
    else if (args.Tick.Value < 0)
        Console.WriteLine("Bearish crossover detected");
};

// Feed data (propagates automatically: source ’ SMAs ’ crossover ’ event)
source.Add(DateTime.UtcNow, 105.0);
```

**Gotcha:** Event subscriptions create strong references. Forgetting to unsubscribe causes memory leaks. In long-running applications, use weak event patterns or explicitly call `Dispose()` on indicators when done.

## Common Patterns

### Warmup Handling

Every indicator has a warmup period before output becomes meaningful. Check `IsHot` before using values:

```csharp
var rsi = new Rsi(period: 14);

// Feed some data...
foreach (var price in prices)
{
    var result = rsi.Update(new TValue(DateTime.UtcNow, price));
    
    if (!result.IsHot)
        continue;  // Skip warmup period
        
    // Safe to use result.Value
    if (result.Value > 70)
        Console.WriteLine("Overbought");
}
```

**WarmupPeriod property:** Each indicator exposes `WarmupPeriod` indicating how many bars until `IsHot` becomes true.

### Indicator Chaining

Feed the output of one indicator into another:

```csharp
var ema = new Ema(period: 12);
var rsiOfEma = new Rsi(period: 14);

void OnTick(double price)
{
    // EMA smooths price
    var smoothed = ema.Update(new TValue(DateTime.UtcNow, price));
    
    // RSI of the smoothed values (reduces noise)
    var rsiResult = rsiOfEma.Update(smoothed);
    
    if (rsiResult.IsHot)
        ProcessSignal(rsiResult.Value);
}
```

### Reset and Reuse

Indicators can be reset and reused without reallocation:

```csharp
var sma = new Sma(period: 14);

// Process first symbol
foreach (var tick in symbol1Ticks)
    sma.Update(tick);
var symbol1Result = sma.Last.Value;

// Reset for next symbol (clears state, keeps parameters)
sma.Reset();

// Process second symbol
foreach (var tick in symbol2Ticks)
    sma.Update(tick);
var symbol2Result = sma.Last.Value;
```

### Multi-Indicator Analysis

```csharp
// Using Span mode for maximum performance
double[] prices = GetPrices();
int length = prices.Length;

double[] sma = new double[length];
double[] ema = new double[length];
double[] rsi = new double[length];
double[] atr = new double[length];

Sma.Calculate(prices, sma, 20);
Ema.Calculate(prices, ema, 20);
Rsi.Calculate(prices, rsi, 14);
Atr.Calculate(highs, lows, closes, atr, 14);

// Analyze correlations, generate signals, etc.
for (int i = Math.Max(20, 14); i < length; i++)
{
    bool bullish = ema[i] > sma[i] && rsi[i] < 70;
    bool lowVolatility = atr[i] < atr[i - 1];
    // ... strategy logic ...
}
```

## Performance Comparison

Measured on Intel i7-12700K, .NET 8.0, 500,000 bars:

| Mode | SMA(14) Time | Allocations |
| :--- | -----------: | ----------: |
| Span | 298 ¼s | 0 bytes |
| Streaming | 12.4 ms | 0 bytes |
| Batch (TSeries) | 15.1 ms | 8 MB |
| Event-Driven | 18.7 ms | 12 MB |

Span mode runs 40x faster than streaming for batch operations. Streaming mode remains allocation-free but incurs per-call overhead. Choose based on the actual requirements, not assumptions.

## References

- [API Reference](api.md): Complete method signatures and parameters
- [Architecture](architecture.md): Internal design and memory model
- [Benchmarks](benchmarks.md): Detailed performance measurements