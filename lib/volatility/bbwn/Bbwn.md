# BBWN: Bollinger Band Width Normalized

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `multiplier` (default 2.0), `lookback` (default 252)                      |
| **Outputs**      | Single series (Bbwn)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period + lookback` bars                          |
| **PineScript**   | [bbwn.pine](bbwn.pine)                       |

- Bollinger Band Width Normalized (BBWN) extends the standard BBW by normalizing it to a [0,1] range based on historical minimum and maximum values o...
- Parameterized by `period`, `multiplier` (default 2.0), `lookback` (default 252).
- Output range: $\geq 0$.
- Requires `period + lookback` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Normalization transforms volatility chaos into comparable signals."

Bollinger Band Width Normalized (BBWN) extends the standard BBW by normalizing it to a [0,1] range based on historical minimum and maximum values over a lookback period. This normalization enables better comparison across different timeframes, instruments, and market conditions, making it easier to identify relative volatility levels consistently.

## Historical Context

While Bollinger Band Width (BBW) effectively measures volatility expansion and contraction, its absolute values can vary dramatically across different assets and timeframes. A BBW of 0.05 might be low for a volatile stock but high for a stable bond. BBWN solves this problem by creating a normalized scale.

The normalization concept comes from technical analysis standardization techniques, similar to those used in oscillators like RSI or Stochastic. By tracking the historical range of BBW values and expressing the current BBW as a position within that range, BBWN provides a consistent 0-100% scale where:

- 0% = Lowest volatility in the lookback period (maximum squeeze)
- 100% = Highest volatility in the lookback period (maximum expansion)
- 50% = Mid-range volatility when no historical range exists

## Architecture & Physics

BBWN builds upon BBW calculation and adds historical normalization:

### Step 1: Standard BBW Calculation

$$
BBW_t = \frac{2k \times \sigma_t}{SMA_t}
$$

Where:

- $k$: Multiplier (default 2.0)
- $\sigma_t$: Standard deviation at time $t$
- $SMA_t$: Simple moving average at time $t$

### Step 2: Historical Min/Max Tracking

For a lookback period $L$ (default 252), track:

$$
BBW_{min} = \min(BBW_{t-L+1}, ..., BBW_t)
$$

$$
BBW_{max} = \max(BBW_{t-L+1}, ..., BBW_t)
$$

### Step 3: Normalization

$$
BBWN_t = \begin{cases}
\frac{BBW_t - BBW_{min}}{BBW_{max} - BBW_{min}} & \text{if } BBW_{max} > BBW_{min} \\
0.5 & \text{otherwise}
\end{cases}
$$

The result is clamped to $[0, 1]$ to ensure bounds.

## Implementation Features

### Performance Optimizations

1. **O(1) BBW Calculation**: Uses running variance with sum-of-squares method
2. **Circular Buffers**: Both price data and BBW history use ring buffers
3. **Incremental Min/Max**: Recalculates min/max only when necessary
4. **Resync Protection**: Periodically recalculates sums to prevent drift

### Data Integrity

- **NaN/Infinity Handling**: Invalid inputs use last valid value
- **Zero Division Protection**: Handles constant price sequences
- **Numerical Stability**: Uses epsilon checks for floating-point comparisons


## Performance Profile

### Operation Count (Streaming Mode)

BBWN chains BBW computation (SMA + StdDev of N bars) with min/max normalization over a lookback window — O(1) amortized.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Running sum_x, sum_x2 (StdDev O(1)) | 2 | 2 cy | ~4 cy |
| sqrt(variance) for StdDev | 1 | 14 cy | ~14 cy |
| BBW = 2*k*StdDev / SMA | 1 | 5 cy | ~5 cy |
| RingBuffer min update (lookback) | 1 | 4 cy | ~4 cy |
| RingBuffer max update (lookback) | 1 | 4 cy | ~4 cy |
| BBWN = (BBW - min) / (max - min) | 1 | 5 cy | ~5 cy |
| Zero-range guard (constant series) | 1 | 2 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~40 cy** |

O(1) per bar. Two chained O(1) computations: BBW (running variance) + min/max normalization (RingBuffer monotonic deque). sqrt() is the dominant latency.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Running sum_x, sum_x2 | Yes | Vector<double> accumulation |
| sqrt(variance) | Yes | Vector<double>.Sqrt() or Avx.Sqrt |
| BBW from StdDev/SMA | Yes | Vector divide |
| Min/max tracking | Partial | Sequential dependency for running extremes |
| Normalization division | Yes | Vector divide with zero-guard |

Batch path can vectorize the BBW computation phase (4 bars per AVX2 cycle). Min/max phase is partially sequential. Overall ~2-3× batch speedup over scalar.

## Usage Examples

### Basic Setup

```csharp
// Default: 20-period BBW, 2.0 multiplier, 252-day lookback
var bbwn = new Bbwn(20, 2.0, 252);

foreach (var price in prices)
{
    var result = bbwn.Update(new TValue(DateTime.Now, price));
    Console.WriteLine($"BBWN: {result.Value:F4}");
}
```

### Custom Parameters

```csharp
// Short-term squeeze detection: 10-period, 1.5 multiplier, 50-day lookback
var shortTermBbwn = new Bbwn(10, 1.5, 50);

// Long-term volatility: 50-period, 2.5 multiplier, 500-day lookback  
var longTermBbwn = new Bbwn(50, 2.5, 500);
```

### Batch Processing

```csharp
var source = new TSeries(times, prices);
var bbwnSeries = Bbwn.Calculate(source, period: 20, multiplier: 2.0, lookback: 252);

for (int i = 0; i < bbwnSeries.Count; i++)
{
    Console.WriteLine($"{bbwnSeries.Times[i]}: {bbwnSeries.Values[i]:F4}");
}
```

## Trading Applications

### Volatility Regime Detection

```csharp
if (bbwn.Last.Value < 0.2)
{
    Console.WriteLine("Low volatility regime - potential squeeze");
}
else if (bbwn.Last.Value > 0.8) 
{
    Console.WriteLine("High volatility regime - potential reversal zone");
}
```

### Breakout Confirmation

```csharp
var previousBbwn = bbwn.Last.Value;
// ... update with new price ...
var currentBbwn = bbwn.Last.Value;

if (previousBbwn < 0.3 && currentBbwn > 0.5)
{
    Console.WriteLine("Volatility expansion - potential breakout confirmed");
}
```

### Multi-Timeframe Analysis

```csharp
var dailyBbwn = new Bbwn(20, 2.0, 252);   // Daily squeeze
var hourlyBbwn = new Bbwn(20, 2.0, 252);  // Hourly expansion

// Trade when daily is squeezed but hourly is expanding
if (dailyBbwn.Last.Value < 0.2 && hourlyBbwn.Last.Value > 0.6)
{
    Console.WriteLine("Multi-timeframe breakout setup");
}
```

## Key Characteristics

### Advantages

- **Scale Independence**: Normalized values work across all instruments
- **Historical Context**: Compares current volatility to recent history
- **Consistent Signals**: 0-100% scale enables consistent thresholds
- **Regime Detection**: Clearly identifies volatility regimes

### Limitations

- **Lookback Dependency**: Normalization quality depends on lookback period
- **Lag**: Historical normalization adds slight lag to signals
- **Range Bound**: Extreme volatility may still be constrained to [0,1]
- **Parameter Sensitivity**: Multiple parameters need optimization

## Parameter Guidelines

| Parameter | Typical Range | Default | Purpose |
|-----------|---------------|---------|---------|
| Period | 5-50 | 20 | BBW calculation period |
| Multiplier | 1.0-3.0 | 2.0 | Band width scaling |
| Lookback | 50-500 | 252 | Historical normalization range |

### Period Selection

- **Short (5-15)**: Sensitive to recent volatility changes
- **Medium (16-30)**: Balanced sensitivity and stability  
- **Long (31-50)**: Smoother, longer-term volatility trends

### Lookback Selection

- **Short (50-100)**: More responsive to regime changes
- **Medium (150-300)**: Balanced historical context
- **Long (400+)**: Stable long-term perspective

## Mathematical Properties

### Range and Bounds

- **Output Range**: $[0, 1]$ by design
- **Convergence**: Values stabilize after lookback period
- **Monotonicity**: Not guaranteed due to normalization updates

### Statistical Properties

- **Distribution**: Depends on underlying price process
- **Mean Reversion**: Normalization creates artificial mean reversion
- **Serial Correlation**: Inherits from underlying BBW

## Alternative Formulations

### Percentile-Based Normalization

Instead of min/max, use percentiles for robustness:

$$
BBWN_t = \frac{BBW_t - P_{10}(BBW)}{P_{90}(BBW) - P_{10}(BBW)}
$$

### Z-Score Normalization

Standardize BBW using mean and standard deviation:

$$
BBWN_t = \frac{BBW_t - \mu_{BBW}}{\sigma_{BBW}}
$$

### Exponential Smoothing

Weight recent history more heavily:

$$
BBWN_t = \frac{BBW_t - EMA_{min}(BBW)}{EMA_{max}(BBW) - EMA_{min}(BBW)}
$$

## Implementation Notes

### Edge Cases

1. **Constant Prices**: When BBW is always zero, BBWN defaults to 0.5
2. **Single Value**: With only one BBW value, BBWN returns 0.5
3. **Numerical Precision**: Uses epsilon comparisons for floating-point safety

### Performance Considerations

- **Memory Usage**: O(period + lookback) for circular buffers
- **CPU Complexity**: O(1) per update, O(lookback) for min/max search
- **Batch Processing**: Optimized vectorized calculations available

BBWN transforms absolute volatility measurements into relative, comparable signals that work consistently across different market conditions and instruments. The normalization provides context that pure BBW cannot offer, making it particularly valuable for systematic trading strategies that need consistent volatility thresholds.
