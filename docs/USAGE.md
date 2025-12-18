# Usage Guides

QuanTAlib supports four distinct operating modes to handle different architectural requirements.

## 1. Span Mode (High Performance)

**Best for:** Backtesting, batch processing, research.

Operates directly on `Span<double>` or arrays. Zero allocations, maximum speed.

```csharp
using QuanTAlib;

// 1. Prepare data
double[] prices = GetPrices(); // Your data source
double[] results = new double[prices.Length];

// 2. Calculate
// Sma.Calculate(source, destination, period)
Sma.Calculate(prices, results, 14);

// 3. Use results
Console.WriteLine($"Last SMA: {results[^1]}");
```

## 2. Streaming Mode (Real-Time)

**Best for:** Live trading, tick-by-tick analysis.

Updates one value at a time. Maintains internal state.

```csharp
using QuanTAlib;

// 1. Initialize
var sma = new Sma(period: 14);

// 2. Update loop (e.g., connected to a feed)
void OnData(double price)
{
    // Update returns a TValue struct { Time, Value, IsHot }
    TValue result = sma.Update(new TValue(DateTime.UtcNow, price));

    if (result.IsHot)
    {
        Console.WriteLine($"SMA: {result.Value}");
    }
}

// 3. Handle bar updates (correction)
// If your feed sends updates for the *same* bar multiple times:
sma.Update(new TValue(time, openPrice), isNew: true);  // New bar opens
sma.Update(new TValue(time, currentPrice), isNew: false); // Price changes within bar
```

## 3. Batch Mode (TSeries)

**Best for:** Exploratory analysis, notebooks.

Wraps calculations in `TSeries` objects that handle timestamps and alignment.

```csharp
using QuanTAlib;

// 1. Create series
TSeries prices = new TSeries();
prices.Add(DateTime.Now, 100.0);
prices.Add(DateTime.Now.AddMinutes(1), 101.0);
// ... add more data ...

// 2. Calculate
// Returns a new TSeries aligned with input
TSeries smaSeries = new Sma(prices, period: 14);

// 3. Access
Console.WriteLine($"Last Value: {smaSeries.Last.Value}");
Console.WriteLine($"Value at index 5: {smaSeries[5].Value}");
```

## 4. Event-Driven Architecture

**Best for:** Complex reactive systems.

Indicators can subscribe to other indicators or data sources.

```csharp
using QuanTAlib;

// 1. Setup chain
var source = new TSeries();
var smaFast = new Sma(source, 10);
var smaSlow = new Sma(source, 20);

// 2. Subscribe to events
smaFast.Pub += (sender, args) => {
    Console.WriteLine($"Fast SMA updated: {args.Tick.Value}");
};

// 3. Feed data
// This triggers the chain: source -> smaFast -> event handler
source.Add(DateTime.UtcNow, 105.0);
```

## Common Patterns

### Handling Warmup
Always check `IsHot` or `Count` before using values.

```csharp
var rsi = new Rsi(14);
// ... feed data ...
if (rsi.IsHot) {
    // Safe to use rsi.Value
}
```

### Combining Indicators
You can feed the output of one indicator into another.

```csharp
var ema = new Ema(period: 12);
var rsiOfEma = new Rsi(period: 14);

void OnData(double price) {
    var emaResult = ema.Update(new TValue(DateTime.UtcNow, price));
    var finalResult = rsiOfEma.Update(emaResult);
}
