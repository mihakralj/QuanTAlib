# APO - Absolute Price Oscillator

The Absolute Price Oscillator (APO) measures the raw cash difference between two exponential moving averages. Unlike its percentage-based cousin PPO, APO speaks in dollars and cents, making it the preferred tool for spread traders, arbitrageurs, and anyone whose P&L is denominated in currency rather than basis points.

## 1. Context & Requirements

**The Problem:** Traders need to quantify momentum in absolute terms. A \$5 move on a \$100 stock (5%) feels different than a \$5 move on a \$20 stock (25%), but to a spread trader balancing a hedge, \$5 is \$5. Percentage oscillators distort this reality.

**The Solution:** APO strips away the percentage normalization. It simply asks: "How far is the fast trend from the slow trend in absolute terms?" This provides a direct read on the cash momentum of the asset.

**Key Metrics:**

- **Trend Direction:** Positive values = Bullish (Fast > Slow).
- **Trend Strength:** Distance from zero indicates momentum intensity.
- **Zero Line:** Crossovers signal trend reversals.

## 2. Architecture & Design

APO is built on the foundation of our high-performance `Ema` kernel. It inherits the O(1) computational complexity and zero-allocation characteristics of the underlying moving averages.

### Mathematical Foundation

$$
APO = EMA_{fast} - EMA_{slow}
$$

Where:

- $EMA_{fast}$ is the recursive Exponential Moving Average (default 12).
- $EMA_{slow}$ is the recursive Exponential Moving Average (default 26).

### Computational Efficiency

We don't recalculate the EMAs from scratch. We maintain the state of both the fast and slow EMAs, allowing us to compute the APO update in constant time, regardless of the lookback period.

- **Time Complexity:** $O(1)$ per update.
- **Space Complexity:** $O(1)$ (two EMA state structs).
- **Allocations:** 0 bytes on the hot path.

## 3. Usage & API

### C# code

```csharp
using QuanTAlib;

// Standard setup (12, 26)
var apo = new Apo();

// Custom periods for high-frequency analysis
var fastApo = new Apo(fastPeriod: 5, slowPeriod: 13);

// Update loop
foreach (var bar in bars)
{
    var result = apo.Update(bar);
    // result.Value contains the absolute difference
}
```

### Streaming vs. Batch

We provide dual implementations to support both real-time event processing and historical backtesting.

```csharp
// Batch: Process 1M bars in ~50ms
var series = Apo.Batch(history, 12, 26);

// Streaming: Process live ticks with zero GC pressure
var apo = new Apo(12, 26);
apo.Update(newBar);
```

## 4. Performance & Benchmarks

APO performance is effectively the sum of two EMA calculations. Since our EMA is highly optimized, APO remains extremely lightweight.

| Operation | Time (ns) | Allocations |
|-----------|-----------|-------------|
| Update    | ~15       | 0 bytes     |
| Batch (1k)| ~5 μs     | 0 bytes*    |

*Excluding output array allocation.

## 5. Validation

We validate our implementation against industry standards to ensure correctness.

- **TA-Lib:** Matches `APO` with `MAType.Ema` (Precision: 1e-9).
- **Tulip:** Note that Tulip's default `apo` may use SMA or different defaults; we strictly adhere to the EMA-based definition used by TA-Lib and major trading platforms.

## 6. Practical Considerations

- **Lag:** As a derivative of moving averages, APO lags price. The lag is a function of the slow period.
- **Scale Sensitivity:** APO values are not normalized. An APO of 10.0 on Bitcoin is noise; on EUR/USD, it's a catastrophe. Use PPO for cross-asset comparisons.
- **Initialization:** The indicator warms up when the slow EMA warms up. We handle `NaN` propagation gracefully during this period.
