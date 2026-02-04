# CG: Center of Gravity

> "The market's center of mass reveals where momentum shifts before price does."

The Center of Gravity (CG) oscillator, developed by John Ehlers, identifies potential turning points in price action using the physics concept of weighted center of mass. It leads price movement, making it particularly useful for timing entries and exits before traditional indicators signal.

## Historical Context

John Ehlers introduced the Center of Gravity oscillator in his 2002 book "Cybernetic Analysis for Stocks and Futures." Ehlers, an electrical engineer turned trader, applied signal processing concepts to financial markets, creating indicators with minimal lag.

The CG oscillator draws from physics: just as the center of gravity of an object determines its balance point, the CG of price determines where momentum is concentrated. When prices cluster toward recent values (uptrend), CG is positive; when prices cluster toward older values (downtrend), CG is negative.

Unlike momentum oscillators that react to price changes, CG measures the distribution of price within the lookback window, providing leading rather than lagging signals.

## Architecture & Physics

The CG indicator uses a sliding window (RingBuffer) to maintain price history and calculates a weighted center of mass that oscillates around zero.

### Core Components

1. **RingBuffer**: Maintains the sliding window of `period` values
2. **Weighted Sum (Numerator)**: Sum of position-weighted prices
3. **Simple Sum (Denominator)**: Sum of all prices in window
4. **Center Offset**: Subtracts the midpoint to center oscillation at zero

### Calculation Flow

For each update:
1. Add new price to buffer
2. Compute weighted sum: Σ(position × price)
3. Compute simple sum: Σ(price)
4. Calculate center: weighted_sum / simple_sum
5. Subtract midpoint: result - (period + 1) / 2

## Mathematical Foundation

### Center of Gravity Formula

The CG at time $t$ is calculated as:

$$ CG_t = \frac{\sum_{i=1}^{n} i \cdot P_{t-n+i}}{\sum_{i=1}^{n} P_{t-n+i}} - \frac{n + 1}{2} $$

where:

- $n$ is the period (lookback length)
- $P_{t-n+i}$ is the price at position $i$ within the window
- $i$ ranges from 1 (oldest) to $n$ (newest)

### Numerator (Weighted Sum)

$$ Num = \sum_{i=1}^{n} i \cdot P_i = 1 \cdot P_1 + 2 \cdot P_2 + \ldots + n \cdot P_n $$

Recent prices (higher $i$) contribute more to the weighted sum.

### Denominator (Simple Sum)

$$ Den = \sum_{i=1}^{n} P_i $$

### Center with Zero Offset

$$ CG = \begin{cases}
\frac{Num}{Den} - \frac{n + 1}{2} & \text{if } Den \neq 0 \\
0 & \text{if } Den = 0
\end{cases} $$

The subtraction of $(n + 1) / 2$ centers the oscillator at zero. Without this offset, CG would oscillate around the midpoint value.

### Properties

- **Range**: Approximately $\pm \frac{n-1}{2}$ depending on price distribution
- **Zero Crossing**: Indicates potential trend reversal
- **Positive Values**: Recent prices dominate (uptrend momentum)
- **Negative Values**: Older prices dominate (downtrend momentum)
- **Zero Value**: Prices evenly distributed (neutral momentum)

### Example Calculation

For prices [10, 12, 11, 13, 15] with period 5:

$$
Num = 1 \times 10 + 2 \times 12 + 3 \times 11 + 4 \times 13 + 5 \times 15 = 194
$$

$$
Den = 10 + 12 + 11 + 13 + 15 = 61
$$

$$
CG = \frac{194}{61} - \frac{5 + 1}{2} = 3.180 - 3.0 = 0.180
$$

The positive value indicates recent prices are weighted higher (uptrend bias).

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15 ns/bar | O(1) with running sums |
| **Allocations** | 0 | Zero-allocation in hot path |
| **Complexity** | O(1) streaming | Recalculation O(N) on bar correction |
| **Accuracy** | 10 | Exact calculation, no approximations |

### Operation Count (per update)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD/SUB | ~6 | Running sum updates |
| MUL | ~2 | Position weighting |
| DIV | 2 | Center and offset calculation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact weighted average |
| **Timeliness** | 9/10 | Leads price by design |
| **Overshoot** | 6/10 | Can overshoot at extremes |
| **Smoothness** | 7/10 | Some noise; often paired with signal line |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not available in TA-Lib |
| **Skender** | N/A | Not available in Skender |
| **Tulip** | N/A | Not available in Tulip |
| **PineScript** | ✅ | Validated against original ta.cg() |

CG is validated through mathematical properties:
- Constant price produces zero CG
- Linear uptrend produces positive CG
- Linear downtrend produces negative CG
- Values bounded by approximately ±(period-1)/2

## Common Pitfalls

1. **Period Selection**: Too short periods produce noisy signals; too long periods reduce responsiveness. Ehlers recommended 10 as a starting point.

2. **Signal Line**: CG is often smoothed with a 3-period SMA signal line. Trading raw CG crossings may produce false signals.

3. **Zero Line Crossings**: Not all zero crossings are tradeable. Confirm with price action or additional filters.

4. **Trending Markets**: In strong trends, CG may stay positive/negative for extended periods. Zero crossing may not occur until trend exhaustion.

5. **Flat Markets**: During consolidation, CG oscillates around zero without clear direction, producing whipsaws.

6. **Warmup Period**: CG requires a full window (`period` values) before producing reliable signals.

## Usage

```csharp
using QuanTAlib;

// Create a 10-period CG indicator
var cg = new Cg(period: 10);

// Update with new values
var result = cg.Update(new TValue(DateTime.UtcNow, 100.0));

// Access the last calculated CG value
Console.WriteLine($"CG: {cg.Last.Value}");

// Chained usage
var source = new TSeries();
var cgChained = new Cg(source, period: 10);

// Static batch calculation
var output = Cg.Calculate(source, period: 10);

// Span-based calculation
Span<double> outputSpan = stackalloc double[source.Count];
Cg.Batch(source.Values, outputSpan, period: 10);
```

## Applications

### Trend Reversal Detection

CG zero crossings often precede price reversals:
- CG crosses above zero: potential bullish reversal
- CG crosses below zero: potential bearish reversal

### Divergence Analysis

Like other oscillators, CG divergences from price can signal weakening trends:
- Price makes higher high, CG makes lower high: bearish divergence
- Price makes lower low, CG makes higher low: bullish divergence

### Momentum Confirmation

Use CG to confirm trend strength:
- Rising CG in uptrend: momentum supporting trend
- Falling CG in uptrend: momentum weakening, potential reversal

### Cycle Analysis

CG's leading nature makes it useful for timing cycle turns in conjunction with other Ehlers indicators.

## Signal Line Strategy

A common approach pairs CG with a trigger line:

```csharp
var cg = new Cg(10);
var trigger = new Sma(3);  // 3-period smoothing of CG

// After updates:
double cgValue = cg.Last.Value;
double triggerValue = trigger.Update(cg.Last).Value;

// Buy when CG crosses above trigger
// Sell when CG crosses below trigger
```

## References

- Ehlers, J.F. (2002). *Cybernetic Analysis for Stocks and Futures*. Wiley.
- Ehlers, J.F. (2001). "The Center of Gravity Oscillator." *Technical Analysis of Stocks & Commodities*.
- TradingView PineScript Reference: ta.cg() function.