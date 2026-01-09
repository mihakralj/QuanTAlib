# GBM Class

`GBM` (Geometric Brownian Motion) is a synthetic data generator that simulates realistic financial price movements. It is useful for testing indicators, strategies, and system performance without relying on external data files.

## Key Features

* **Geometric Brownian Motion**: Uses the standard mathematical model for asset price dynamics.
* **Configurable Parameters**: Control drift (trend) and volatility (noise).
* **Stateless Design**: Minimal memory footprint; only maintains state needed for continuity.
* **Dual Modes**: Supports both streaming (bar-by-bar) and batch generation.
* **Intra-bar Updates**: Can simulate real-time price updates within a single bar.

## Mathematical Model

The price evolution follows the stochastic differential equation:

$$ dS_t = \mu S_t dt + \sigma S_t dW_t $$

Where:

* $S_t$: Asset price at time $t$
* $\mu$: Drift (expected return)
* $\sigma$: Volatility (standard deviation of returns)
* $W_t$: Wiener process (Brownian motion)

## Class Definition

```csharp
public class GBM : IFeed
{
    public GBM(double startPrice = 100.0, double mu = 0.05, double sigma = 0.2, TimeSpan? defaultTimeframe = null);

    public TBar Next(bool isNew = true);
    public TBarSeries Fetch(int count, long startTime, TimeSpan interval);
}
```

## Usage

### 1. Initialization

```csharp
// Default: Start at 100, 5% drift, 20% volatility
var gbm = new GBM();

// Custom: Start at 50, 10% drift, 50% volatility
var volatileGbm = new GBM(startPrice: 50.0, mu: 0.10, sigma: 0.50);
```

### 2. Streaming Generation

```csharp
// Generate a new bar
var bar = gbm.Next(isNew: true);

// Simulate intra-bar updates (e.g., real-time ticks)
for (int i = 0; i < 5; i++)
{
    var updatedBar = gbm.Next(isNew: false);
    Console.WriteLine($"Update: {updatedBar.Close}");
}
```

### 3. Batch Generation

```csharp
long startTime = DateTime.UtcNow.Ticks;
var interval = TimeSpan.FromMinutes(1);

// Generate 1000 bars
var history = gbm.Fetch(1000, startTime, interval);
