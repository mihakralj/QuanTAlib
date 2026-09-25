# API Reference

QuanTAlib has a split personality. On purpose.

Backtesting wants to chew through 500,000 bars in microseconds. Live trading wants sub-microsecond updates on every tick. These are fundamentally different computational models: one is stateless bulk math, the other is stateful incremental math. Most libraries pick one and apologize for the other.

We picked both. Then made them share a consistent API surface so you do not have to remember two different libraries depending on whether it is Saturday backtesting or Monday morning.

All indicators inherit from `AbstractBase` and implement `ITValuePublisher`. Every indicator, all 447 of them, exposes the same interface. No special cases. No "oh, this oscillator works differently." Consistency is not optional when you have 447 things to keep straight.

## The Contract

Every indicator exposes these properties and methods. No exceptions.

### Properties

| Property | Type | What It Does |
| :------- | :--- | :----------- |
| `Name` | `string` | Human-readable identifier. `"Sma(14)"`, not `"indicator_7"`. |
| `Last` | `TValue` | Most recent calculated value: timestamp plus result. |
| `IsHot` | `bool` | `true` when enough data has accumulated for mathematically valid output. Before this, you are reading warmup noise. |
| `WarmupPeriod` | `int` | How many samples the indicator needs before `IsHot` flips. |
| `Pub` | `event` | Fires when new value is calculated. This is how reactive chaining works. |

### Methods

| Method | Signature | Purpose |
| :----- | :-------- | :------ |
| `Update` | `TValue Update(TValue input, bool isNew = true)` | Process single value. Streaming mode. |
| `Update` | `TSeries Update(TSeries source)` | Process entire series through stateful instance. |
| `Batch` | `static void Batch(ReadOnlySpan<double>, Span<double>, int)` | Stateless bulk calculation. SIMD-accelerated. |
| `Prime` | `void Prime(ReadOnlySpan<double> source, TimeSpan? step)` | Hydrate internal state from historical tail. |
| `Reset` | `void Reset()` | Wipe state. Return to factory condition. |

## Batch Mode

**When:** Backtesting. Historical analysis. Optimization sweeps where you are running 10,000 parameter combinations and need results before your coffee gets cold.

Batch mode is stateless. No object state. No circular buffers. Just contiguous memory and SIMD instructions grinding through doubles.

### Span-Based (The Fast Path)

This is the path where the garbage collector sleeps. Operates directly on memory spans. AVX-512 on hardware that supports it, AVX2 where it does not, NEON on ARM. The runtime picks the widest vector width your CPU can handle.

```csharp
double[] prices = LoadHistoricalData();  // 500,000 bars
double[] results = new double[prices.Length];

Sma.Batch(prices.AsSpan(), results.AsSpan(), period: 14);
```

500,000 bars of SMA in 328 microseconds. That is 0.66 nanoseconds per value. Faster than a single L1 cache miss on most architectures.

Zero allocations. The `Span<double>` points directly at the arrays you already own. No intermediate collections. No LINQ. No temporary lists that make the GC twitch.

### TSeries-Based (The Convenient Path)

When you need timestamps aligned with your results. Slightly slower because metadata travels with the data, but still fast enough that you will not notice unless you are benchmarking.

```csharp
TSeries history = LoadTimeSeriesData();
TSeries smaValues = Sma.Batch(history, period: 14);

Console.WriteLine($"SMA at {smaValues[100].Time}: {smaValues[100].Value}");
```

Timestamps survive the calculation. Time alignment is preserved. You can correlate results back to the original bars without maintaining a separate index.

## Streaming Mode

**When:** Live trading. Real-time charting. Anything where data arrives one value at a time and you need answers before the next value shows up.

Streaming mode maintains internal state: circular buffers, running sums, previous values. Each `Update` call is O(1). Not amortized O(1). Actual O(1). The indicator does constant work regardless of period length or history depth.

### Standard Update

```csharp
var sma = new Sma(period: 14);

foreach (var bar in liveDataFeed)
{
    TValue result = sma.Update(new TValue(bar.Time, bar.Close));

    if (result.IsHot)
    {
        // Valid output. Safe to make trading decisions.
        // Before IsHot: warmup noise. Do not trade on warmup noise.
        ProcessSignal(result.Value);
    }
}
```

### Bar Correction

Markets send corrections. This is not a design flaw in the market. This is reality.

A bar opens. Ticks update it. The close price changes six times before the bar finalizes. Naive implementations accumulate drift: each "correction" adds another data point instead of replacing the current one. After enough corrections, your 14-period SMA quietly becomes a 14-plus-however-many-corrections-period SMA.

The `isNew` parameter solves this.

```csharp
var indicator = new Sma(14);

// Bar opens at 09:30:00
indicator.Update(new TValue(time_0930, 100.0), isNew: true);

// Ticks arrive. Same bar. Price moves.
indicator.Update(new TValue(time_0930, 100.5), isNew: false);  // Replaced
indicator.Update(new TValue(time_0930, 101.2), isNew: false);  // Replaced
indicator.Update(new TValue(time_0930, 100.8), isNew: false);  // Replaced

// New bar opens at 09:31:00
indicator.Update(new TValue(time_0931, 101.0), isNew: true);
```

When `isNew` is `false`, the indicator rolls back to state before the current bar, then applies the new value. No drift. No accumulated phantom data points. The SMA of 14 periods remains an SMA of 14 periods, no matter how many intrabar corrections arrive.

I once spent an entire weekend debugging a live trading system where the EMA was drifting by 0.3% over the course of a trading day. The cause: bar corrections being treated as new bars. That is thirty basis points of compounding error, invisible on any single bar, devastating over 390 bars. The `isNew` flag exists because of weekends like that.

### Reactive Chaining

Indicators implement `ITValuePublisher`. They publish. Other indicators subscribe. Updates cascade automatically.

```csharp
var source = new TSeries();
var sma = new Sma(source, period: 14);   // Subscribes to source
var rsi = new Rsi(sma, period: 9);       // Subscribes to sma
var ema = new Ema(rsi, period: 5);       // Subscribes to rsi

// One add. Three calculations. Zero manual orchestration.
source.Add(new TValue(DateTime.UtcNow, 100.0));
```

The chain fires in subscription order. Each indicator receives the output of its upstream dependency, computes, publishes. By the time `source.Add` returns, all three indicators have updated values.

This replaces the typical pattern of "calculate SMA, then pass result to RSI, then pass result to EMA, and do not forget to check IsHot on each one, and do not forget the isNew flag, and make sure the timestamps align." That pattern is a bug factory. Event-driven chaining eliminates the manual wiring.

## Priming

**When:** You have historical data and want to transition to live streaming without starting cold.

A common mistake: run batch mode to get historical results, create a fresh streaming instance for live data, and start with a cold indicator that needs another `WarmupPeriod` bars before producing valid output. During those warmup bars, your live trading system either produces garbage signals or sits idle. Neither is acceptable.

Priming solves this. It hydrates a streaming indicator using the minimal tail of historical data.

```csharp
var indicator = new Sma(period: 14);
double[] history = LoadHistoricalData();  // 100,000 bars

// Processes only the tail needed to fill internal buffers
// O(warmup) work, not O(history)
indicator.Prime(history.AsSpan());

Console.WriteLine(indicator.IsHot);  // true, immediately
Console.WriteLine(indicator.Last);   // Valid value, ready to use

// Next live tick feeds directly into warm indicator
indicator.Update(nextLiveTick);
```

`Prime` does not process all 100,000 bars. It takes the last `WarmupPeriod` values (or however many the specific indicator needs for convergence), processes them through the streaming path, and leaves the indicator in the exact state it would be in if it had processed that history bar by bar.

## Calculate: The Hybrid

Combines batch processing with priming in a single call. Returns both the complete historical results and a warmed-up instance ready for streaming.

```csharp
TSeries history = LoadTimeSeriesData();

// Batch-process history AND get a primed instance. One call.
var (results, indicator) = Sma.Calculate(history, period: 14);

// Plot the history
PlotChart(results);

// Immediately ready for live data
indicator.Update(nextLiveTick);
```

This eliminates the "two instances" problem entirely. The `indicator` returned from `Calculate` has full internal state. It does not need warmup. It does not need priming. It is ready.

## Warmup and Validity

### When IsHot Matters

`IsHot` is the indicator telling you: "I have enough data. My output is mathematically valid. Before this point, I was guessing."

Different indicator types need different amounts of warmup:

| Indicator Type | Warmup Logic | Example |
| :------------- | :----------- | :------ |
| Fixed-window (SMA, WMA) | Exactly `period` bars | SMA(14): hot after 14 bars |
| Recursive (EMA, DEMA, T3) | ~3 * period for convergence | EMA(14): hot after ~42 bars |
| Multi-component (Bollinger) | Longest component's warmup | Bollinger(20): hot after 20 bars |
| Composite chains | Sum of all component warmups | EMA(SMA(14), 5): hot after 14 + 15 bars |

For recursive indicators, "convergence" means the output has settled within 1% of the value it would produce with infinite history. A 14-period EMA technically never fully converges (the impulse response is infinite, hence "IIR"), but after 42 bars the residual error is smaller than floating-point noise. Good enough for trading. Good enough for math.

### Streaming Context

```csharp
var sma = new Sma(10);

for (int i = 0; i < 15; i++)
{
    var result = sma.Update(new TValue(DateTime.UtcNow, prices[i]));
    Console.WriteLine($"Bar {i}: IsHot = {result.IsHot}");
}
// Bars 0-8:  IsHot = false  (warmup, do not trade on these)
// Bars 9-14: IsHot = true   (valid output)
```

### Batch Context

Batch output contains cold values at the beginning. The count of cold values equals `WarmupPeriod`. This is not a bug. This is math.

```csharp
TSeries results = Sma.Batch(history, period: 14);

// Skip the warmup region
for (int i = 13; i < results.Count; i++)
{
    ProcessValidValue(results[i]);
}
```

## Error Handling

### Invalid Parameters

Constructors validate eagerly. Bad parameters throw `ArgumentException` with the parameter name. You find out at construction time, not 50,000 bars later when the division by zero surfaces.

```csharp
try
{
    var sma = new Sma(period: 0);  // No.
}
catch (ArgumentException ex)
{
    // ex.ParamName == "period"
    // "must be greater than 0"
    // Clear. Immediate. No mysteries.
}
```

### NaN Propagation

Financial data contains holes. Feeds drop. Connections hiccup. The resulting `NaN` values, if allowed to propagate through an indicator chain, will poison every downstream calculation. NaN is infectious: `NaN + anything = NaN`. One bad tick and your entire indicator pipeline produces `NaN` for the rest of the session.

QuanTAlib substitutes the last valid value when it encounters non-finite input.

```csharp
var sma = new Sma(14);
sma.Update(new TValue(t1, 100.0));       // Valid
sma.Update(new TValue(t2, double.NaN));  // Substituted with 100.0 internally
sma.Update(new TValue(t3, 101.0));       // Valid, chain continues clean
```

The output never contains NaN unless all inputs were NaN. At that point, there is nothing to substitute, and NaN is the honest answer.

## Thread Safety

Indicators are not thread-safe. Each instance maintains mutable internal state: circular buffers, running sums, previous values. Concurrent access will corrupt that state in ways that produce subtly wrong numbers rather than obvious crashes. The worst kind of bug.

For multi-threaded scenarios: one indicator instance per thread. Or external synchronization. But if you are synchronizing access to a sub-microsecond indicator, the lock contention will cost more than the calculation. Just create separate instances.

## IDisposable

All indicators implement `IDisposable`. In practice, there is nothing unmanaged to release. But the interface is there because indicators subscribe to upstream events via `Pub`, and proper disposal ensures those subscriptions are cleaned up. Use `using` statements or explicit `Dispose` calls in long-running applications where indicator instances are created and destroyed dynamically.

```csharp
using var sma = new Sma(source, period: 14);
// sma unsubscribes from source when disposed
```

Full indicator catalog: [447 indicators](../lib/_index.md)