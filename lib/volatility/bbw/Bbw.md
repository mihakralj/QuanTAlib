# BBW: Bollinger Band Width

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `multiplier` (default 2.0)                      |
| **Outputs**      | Single series (Bbw)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- Bollinger Band Width measures the distance between upper and lower Bollinger Bands, normalized by the middle band.
- Parameterized by `period`, `multiplier` (default 2.0).
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Volatility breeds opportunity. The squeeze precedes the explosion."

Bollinger Band Width measures the distance between upper and lower Bollinger Bands, normalized by the middle band. When BBW is low, the bands are squeezing together, signaling compressed volatility and impending breakout. When BBW is high, the market is in an expanded volatility state. BBW transforms Bollinger Bands from a visual channel indicator into a quantifiable volatility oscillator, enabling algorithmic detection of "squeeze" conditions that often precede significant price moves.

## Historical Context

John Bollinger introduced Bollinger Bands in the 1980s as a self-adjusting volatility envelope. The bands expand and contract based on recent price volatility measured by standard deviation. While the bands themselves are useful for identifying overbought/oversold conditions, traders noticed that band contraction often preceded explosive moves.

BBW (Bollinger Band Width) was developed to quantify this contraction numerically. Rather than eyeballing chart patterns, BBW provides an objective measurement. The formula divides the band distance by the middle band (SMA), producing a percentage-based reading that allows comparison across different price levels and assets.

The "Bollinger Squeeze" became a popular trading setup: identify periods of historically low BBW, then trade the subsequent breakout. Some traders add a momentum filter (like Keltner Channels inside Bollinger Bands) to confirm the squeeze, but BBW alone captures the core volatility compression signal.

## Architecture & Physics

BBW is derived from Bollinger Bands components. It requires:

1. **SMA (Simple Moving Average)**: The middle band and normalizer
2. **StdDev (Standard Deviation)**: Measures price dispersion
3. **Multiplier**: Scales the standard deviation for band width

### Band Construction

$$
\text{Middle} = SMA(P, N)
$$

$$
\text{Upper} = \text{Middle} + k \times \sigma_N
$$

$$
\text{Lower} = \text{Middle} - k \times \sigma_N
$$

Where:
- $P$: Price series (typically close)
- $N$: Period (default 20)
- $k$: Multiplier (default 2.0)
- $\sigma_N$: Standard deviation over N periods

### BBW Calculation

$$
BBW_t = \frac{\text{Upper}_t - \text{Lower}_t}{\text{Middle}_t} = \frac{2k \times \sigma_t}{SMA_t}
$$

Since $\text{Upper} - \text{Lower} = 2k\sigma$, BBW simplifies to:

$$
BBW_t = \frac{2k \times \sigma_t}{SMA_t}
$$

This is equivalent to:

$$
BBW_t = \frac{2k \times StdDev(P, N)}{SMA(P, N)}
$$

### Interpretation

| BBW Value | Volatility State | Market Condition |
| :-------- | :--------------- | :--------------- |
| Low (< historical 20th percentile) | Compressed | Squeeze, expect breakout |
| Medium | Normal | Trending or ranging |
| High (> historical 80th percentile) | Expanded | Post-breakout, potential reversal |

## Mathematical Foundation

### Relationship to Coefficient of Variation

BBW is proportional to the Coefficient of Variation (CV):

$$
CV = \frac{\sigma}{\mu}
$$

$$
BBW = 2k \times CV
$$

With default $k=2$, BBW equals 4 times the coefficient of variation. This normalization allows BBW to be compared across assets with different price levels.

### Standard Deviation Formula

Population standard deviation over N periods:

$$
\sigma_N = \sqrt{\frac{1}{N} \sum_{i=1}^{N} (P_i - \bar{P})^2}
$$

Where $\bar{P} = SMA(P, N)$.

### Warmup Period

BBW requires N bars to compute valid SMA and StdDev. The first N-1 values are progressively calculated but may not reflect stable readings.

### Range Bounds

BBW is theoretically unbounded above but has practical constraints:
- Minimum: 0 (when StdDev = 0, all prices identical)
- Typical range: 0.01 to 0.5 (1% to 50% band width relative to SMA)
- Extreme: > 0.5 during market panics

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| SMA update | 1 | ~5 | 5 |
| StdDev update | 1 | ~20 | 20 |
| MUL (2 × k) | 1 | 3 | 3 |
| MUL (× StdDev) | 1 | 3 | 3 |
| DIV (/ SMA) | 1 | 15 | 15 |
| **Total** | **5** | — | **~46 cycles** |

StdDev dominates due to variance calculation. Division is secondary cost.

### SIMD Analysis

| Component | SIMD Potential | Notes |
| :-------- | :------------- | :---- |
| SMA calculation | Yes | Sum can vectorize |
| Variance calculation | Yes | Sum of squares vectorizes |
| Final BBW | No | Scalar division |

### Quality Metrics

| Metric | Score | Notes |
| :----- | ----: | :---- |
| **Accuracy** | 10/10 | Exact Bollinger Band formula |
| **Timeliness** | 7/10 | Lags due to SMA/StdDev windowing |
| **Overshoot** | 10/10 | Bounded measure, cannot overshoot |
| **Smoothness** | 8/10 | Smooth due to SMA averaging |

## Validation

Validated against external libraries in `Bbw.Validation.Tests.cs`.

| Library | Status | Notes |
| :------ | :----: | :---- |
| **TA-Lib** | N/A | No direct BBW function |
| **Skender** | ✅ | Matches `GetBollingerBands` width calculation |
| **Tulip** | N/A | No direct BBW function |
| **Ooples** | ✅ | Matches `CalculateBollingerBandsWidth` |

## Common Pitfalls

1. **Squeeze Detection Timing**: Low BBW signals *potential* breakout, not *immediate* breakout. Squeezes can persist for extended periods before resolution. Combine with momentum or volume confirmation.

2. **Directional Assumption**: BBW measures volatility magnitude, not direction. A squeeze can break upward or downward with equal probability from BBW alone. Use trend filters for directional bias.

3. **Period Sensitivity**: Shorter periods (10-15) produce more responsive but noisier BBW. Longer periods (25-50) are smoother but lag volatility changes. Match period to your trading timeframe.

4. **Multiplier Impact**: Changing the multiplier (k) scales BBW proportionally. BBW with k=3 will be 1.5× the value of BBW with k=2. Ensure consistent multiplier when comparing historical readings.

5. **Mean-Reverting Nature**: Unlike trending indicators, BBW tends to mean-revert. Extremely low BBW readings eventually return to average as volatility normalizes post-squeeze.

6. **Cross-Asset Comparison**: While BBW is percentage-normalized, different assets have different "normal" volatility ranges. A 0.10 BBW might be low for a volatile stock but high for a bond ETF.

7. **Zero Division Guard**: If SMA equals zero (theoretically impossible with positive prices), BBW would be undefined. Implementation guards against this edge case.

## Usage Examples

```csharp
// Streaming mode
var bbw = new Bbw(period: 20, multiplier: 2.0);
foreach (var price in priceStream)
{
    var result = bbw.Update(price);
    Console.WriteLine($"BBW: {result.Value:P2}"); // e.g., "BBW: 5.23%"
}

// Batch processing
var prices = new TSeries();
// ... populate prices ...
var bbwSeries = Bbw.Calculate(prices, period: 20, multiplier: 2.0);

// Squeeze detection
var bbw20 = new Bbw(20, 2.0);
var recentBbw = new List<double>();
foreach (var price in prices)
{
    var result = bbw20.Update(price);
    recentBbw.Add(result.Value);
    
    // Check for squeeze (BBW below 6-month low)
    if (recentBbw.Count > 126)
    {
        double sixMonthLow = recentBbw.Skip(recentBbw.Count - 126).Min();
        if (result.Value <= sixMonthLow * 1.05)
        {
            Console.WriteLine("Squeeze detected!");
        }
    }
}

// Event-driven chaining
var source = new TSeries();
var bbw = new Bbw(source, period: 20, multiplier: 2.0);
// BBW updates automatically when prices are added to source
```

## C# Implementation Considerations

### Delegation to SMA and StdDev

BBW composes two internal indicators:

```csharp
private readonly Sma _sma;
private readonly Stddev _stddev;
private readonly double _mult;
```

This reuses existing SMA and StdDev implementations with their warmup and state management.

### Core Calculation

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public TValue Update(TValue input, bool isNew = true)
{
    _sma.Update(input, isNew);
    _stddev.Update(input, isNew);
    
    double smaValue = _sma.Last.Value;
    double stdValue = _stddev.Last.Value;
    
    // Guard against division by zero
    double bbw = smaValue > 0 ? (2.0 * _mult * stdValue) / smaValue : 0.0;
    
    return new TValue(input.Time, bbw);
}
```

### Memory Layout

| Component | Size | Purpose |
| :-------- | ---: | :------ |
| `_sma` (Sma) | ~48 + N×8 bytes | SMA with circular buffer |
| `_stddev` (Stddev) | ~48 + N×8 bytes | StdDev with circular buffer |
| `_mult` | 8 bytes | Multiplier constant |
| **Total per instance** | **~104 + 2N×8 bytes** | Period-dependent |

For default N=20: approximately 424 bytes per instance.

## References

- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill. (Original Bollinger Band methodology)
- Bollinger, J. "Bollinger Band Width." BollingerBands.com. (BBW definition and squeeze strategy)
- Connors, L., & Raschke, L. (1995). *Street Smarts*. M. Gordon Publishing. (Squeeze trading strategies)
