# AMAT: Archer Moving Averages Trends

> "Markets trend about 30% of the time. The trick isn't just finding trends—it's confirming them before your stops get hit."

AMAT (Archer Moving Averages Trends) is a trend identification system that uses dual EMAs to provide clear directional signals. Unlike simple moving average crossovers that generate signals on any intersection, AMAT requires **alignment** of both fast and slow averages moving in the same direction—reducing false signals during choppy, sideways markets.

## Historical Context

AMAT emerged from concepts developed by Mark Whistler (known as "Archer" in trading circles) and was formalized by Tom Joseph in 2009. The indicator addresses a fundamental problem with traditional crossover systems: they generate excessive whipsaws in ranging markets because a crossover only measures relative position, not directional agreement.

The innovation lies in requiring **three conditions** for a trend signal:

1. Relative position (fast above/below slow)
2. Fast EMA direction (rising/falling)
3. Slow EMA direction (rising/falling)

This triple-confirmation approach filters out the noise inherent in single-condition systems.

## Architecture & Physics

AMAT operates on dual EMA calculations with directional analysis. The computational flow:

```
Input Price
    │
    ├──► Fast EMA ───► Direction (rising/falling)
    │                         │
    │                         ▼
    └──► Slow EMA ───► Direction (rising/falling)
                              │
                              ▼
                    Trend Logic (+1, -1, 0)
                              │
                              ▼
                    Strength = |Fast - Slow| / Slow × 100
```

### Trend State Machine

| State | Fast vs Slow | Fast Direction | Slow Direction |
|:------|:------------|:---------------|:---------------|
| **Bullish (+1)** | Fast > Slow | Rising | Rising |
| **Bearish (-1)** | Fast < Slow | Falling | Falling |
| **Neutral (0)** | Any | Mixed | Mixed |

The neutral state captures market indecision: when EMAs disagree on direction or their relative position contradicts their momentum, AMAT stays flat. This is a feature, not a limitation.

### EMA Bias Compensation

QuanTAlib's implementation uses bias-compensated EMAs during the warmup phase. Traditional EMA initialization assumes the first price equals the true average—a convenient fiction. The compensator factor `e` decays exponentially:

$$e_{t} = e_{t-1} \times (1 - \alpha)$$

Until convergence, the EMA is divided by $(1 - e)$ to remove initialization bias.

## Mathematical Foundation

### 1. EMA Calculation

$$\text{EMA}_t = \alpha \times P_t + (1 - \alpha) \times \text{EMA}_{t-1}$$

Where $\alpha = \frac{2}{n + 1}$ and $n$ is the period.

### 2. Direction Detection

$$\text{Direction}_t = \begin{cases} \text{rising} & \text{if } \text{EMA}_t > \text{EMA}_{t-1} \\ \text{falling} & \text{if } \text{EMA}_t < \text{EMA}_{t-1} \\ \text{flat} & \text{otherwise} \end{cases}$$

### 3. Trend Signal

$$\text{Trend}_t = \begin{cases} +1 & \text{if } \text{FastEMA}_t > \text{SlowEMA}_t \land \text{FastRising} \land \text{SlowRising} \\ -1 & \text{if } \text{FastEMA}_t < \text{SlowEMA}_t \land \text{FastFalling} \land \text{SlowFalling} \\ 0 & \text{otherwise} \end{cases}$$

### 4. Trend Strength

$$\text{Strength}_t = \frac{|\text{FastEMA}_t - \text{SlowEMA}_t|}{\text{SlowEMA}_t} \times 100$$

Strength quantifies the separation between EMAs as a percentage of the slow EMA—useful for gauging trend conviction or filtering weak signals.

## Usage

```csharp
// Standard instantiation
var amat = new Amat(fastPeriod: 10, slowPeriod: 50);

// Process streaming data
foreach (var price in prices)
{
    amat.Update(new TValue(DateTime.UtcNow, price));

    if (amat.Last.Value == 1.0)
        Console.WriteLine($"Bullish - Strength: {amat.Strength.Value:F2}%");
    else if (amat.Last.Value == -1.0)
        Console.WriteLine($"Bearish - Strength: {amat.Strength.Value:F2}%");
    else
        Console.WriteLine("Neutral");
}

// Access individual EMAs
double fastEma = amat.FastEma.Value;
double slowEma = amat.SlowEma.Value;

// Batch processing
var results = Amat.Batch(priceSeries, fastPeriod: 10, slowPeriod: 50);

// Span-based high-performance
double[] trend = new double[prices.Length];
double[] strength = new double[prices.Length];
Amat.Calculate(prices.AsSpan(), trend, strength, fastPeriod: 10, slowPeriod: 50);
```

### Event-Driven (Chained)

```csharp
var source = new TSeries();
var amat = new Amat(source, fastPeriod: 10, slowPeriod: 50);

// AMAT automatically updates when source publishes
source.Add(new TValue(DateTime.UtcNow, 100.0));
Console.WriteLine($"Trend: {amat.Last.Value}");
```

## Parameters

| Parameter | Type | Default | Description |
|:----------|:-----|:--------|:------------|
| `fastPeriod` | int | 10 | Fast EMA period (must be > 0) |
| `slowPeriod` | int | 50 | Slow EMA period (must be > fastPeriod) |

### Common Period Combinations

| Use Case | Fast | Slow | Notes |
|:---------|:-----|:-----|:------|
| **Scalping** | 5 | 13 | High responsiveness, more signals |
| **Swing** | 10 | 50 | Balanced, classic configuration |
| **Position** | 20 | 100 | Filtered for major trends |
| **Investment** | 50 | 200 | Long-term directional bias |

## Output Properties

| Property | Type | Description |
|:---------|:-----|:------------|
| `Last` | TValue | Trend direction: +1 (bullish), -1 (bearish), 0 (neutral) |
| `Strength` | TValue | Trend strength as percentage |
| `FastEma` | TValue | Current fast EMA value |
| `SlowEma` | TValue | Current slow EMA value |
| `IsHot` | bool | True when both EMAs are fully warmed |
| `WarmupPeriod` | int | Equal to slowPeriod |

## Performance Profile

| Metric | Score | Notes |
|:-------|:------|:------|
| **Throughput** | ~15 ns/bar | Dual EMA + direction check |
| **Allocations** | 0 | Streaming mode is allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 9/10 | Bias-compensated EMAs match external libs |
| **Timeliness** | 7/10 | Triple-confirmation adds slight lag |
| **Overshoot** | 8/10 | No overshoot; discrete {-1, 0, +1} output |
| **Smoothness** | 6/10 | State transitions can be abrupt |

## Validation

AMAT is a custom indicator not present in standard TA libraries. Validation confirms:

| Component | Library | Status | Notes |
|:----------|:--------|:-------|:------|
| **Fast EMA** | TA-Lib | ✅ | Matches `TA_EMA` |
| **Fast EMA** | Skender | ✅ | Matches `GetEma` |
| **Slow EMA** | TA-Lib | ✅ | Matches `TA_EMA` |
| **Slow EMA** | Skender | ✅ | Matches `GetEma` |
| **Trend Logic** | Manual | ✅ | Verified against known patterns |
| **Strength** | Manual | ✅ | Formula verification |

## Common Pitfalls

### 1. Expecting Continuous Signals

AMAT returns 0 (neutral) frequently. This is intentional—choppy markets produce neutral signals. Trading systems should respect neutral states rather than forcing a directional bias.

### 2. Period Selection

Fast periods that are too close to slow periods produce excessive neutral readings. A ratio of 1:5 (e.g., 10/50) provides reasonable separation.

### 3. Strength Interpretation

High strength doesn't guarantee trend continuation. It measures current separation, not momentum. A declining strength during a +1 trend may indicate weakening conviction.

### 4. Initialization Phase

Until `IsHot` returns true, trend signals may be unreliable. The indicator needs `slowPeriod` bars to stabilize both EMAs.

## See Also

- [EMA](../trends/ema/Ema.md) - Exponential Moving Average (AMAT's building block)
- [MACD](../momentum/macd/Macd.md) - Another dual-EMA system with different logic
- [ADX](../momentum/adx/Adx.md) - Trend strength without directional bias
