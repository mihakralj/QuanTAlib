# ABBER: Aberration Bands

> "Standard deviation punishes outliers twice: once when they happen, once when they distort everything else."

ABBER measures price deviation from a central moving average using absolute deviation rather than standard deviation. The result: dynamic bands that adapt to volatility while remaining robust against extreme outliers. Where Bollinger Bands amplify outliers through squaring, ABBER uses raw absolute differences. Bands respond to typical price behavior, not the occasional spike that yanks everything sideways.

## Historical Context

Aberration Bands emerged as a response to the statistical assumptions baked into Bollinger Bands. Standard deviation assumes normally distributed returns. Markets laugh at that assumption daily. Fat tails, volatility clustering, flash crashes: the squared-deviation approach treats these events as if they carry information about typical behavior. They do not.

The absolute deviation approach predates Bollinger's work (mean absolute deviation appears in early 20th-century statistics), but applying it to band construction arrived later, once practitioners grew tired of watching their bands blow out on single-bar anomalies. No single inventor claims credit. The technique spread through trading floors where robustness mattered more than textbook elegance.

## Architecture & Physics

ABBER computes three outputs through running sums maintained in O(1) streaming time:

* **Middle Band**: Simple Moving Average of source price
* **Upper Band**: Middle + (Multiplier × Average Absolute Deviation)
* **Lower Band**: Middle − (Multiplier × Average Absolute Deviation)

The average absolute deviation represents typical distance price travels from the moving average. No squaring, no square roots. Just raw, intuitive dispersion.

### The Outlier Problem

Standard deviation squares each deviation before averaging, then takes the square root. A single bar 4σ from the mean contributes 16× more weight than a 1σ bar. In ABBER, that same outlier contributes only 4× more. The mathematical consequence: ABBER bands recover faster from shocks. They measure the market's normal breathing, not its occasional screams.

The physics analogy: standard deviation is a spring that stores energy quadratically. Push twice as hard, store four times the energy. ABBER is a linear damper. Push twice as hard, resist twice as hard. Different behaviors, different use cases.

## Mathematical Foundation

### 1. Middle Band

$$\text{Middle}_t = \frac{1}{n} \sum_{i=0}^{n-1} \text{Source}_{t-i}$$

### 2. Absolute Deviation

$$\text{Deviation}_t = |\text{Source}_t - \text{Middle}_{t-1}|$$

### 3. Average Absolute Deviation

$$\text{AvgDev}_t = \frac{1}{n} \sum_{i=0}^{n-1} \text{Deviation}_{t-i}$$

### 4. Band Calculation

$$\text{Upper}_t = \text{Middle}_t + (k \times \text{AvgDev}_t)$$

$$\text{Lower}_t = \text{Middle}_t - (k \times \text{AvgDev}_t)$$

Where $n$ = lookback period (default: 20), $k$ = multiplier (default: 2.0).

```csharp
// Streaming usage
var abber = new Abber(period: 20, multiplier: 2.0);
foreach (var price in priceData)
{
    abber.Update(price);
    // Middle: abber.Last.Value, Upper: abber.Upper.Value, Lower: abber.Lower.Value
}

// Batch calculation
var (middle, upper, lower) = Abber.Batch(series, period: 20, multiplier: 2.0);

// Span-based (zero allocation)
Abber.Batch(source.AsSpan(), middleOut.AsSpan(), upperOut.AsSpan(), lowerOut.AsSpan(), 20, 2.0);
```

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 6 | 1 | 6 |
| MUL | 2 | 3 | 6 |
| DIV | 2 | 15 | 30 |
| ABS | 1 | 1 | 1 |
| **Total** | **11** | — | **~43 cycles** |

**Breakdown:**
- SMA (Middle): 2 ADD + 1 DIV = 17 cycles (running sum)
- Absolute deviation: 1 SUB + 1 ABS = 2 cycles
- Average deviation: 2 ADD + 1 DIV = 17 cycles (running sum)
- Band calculation: 2 MUL + 2 ADD = 8 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Running sums with circular buffers |
| Batch | O(n) | Linear scan, n = series length |

**Memory**: ~128 bytes (two circular buffers for price and deviation).

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | Partial | Band calc vectorizable; SMA recursion blocks full SIMD |
| FMA | ✅ | `Middle ± (Multiplier × AvgDev)` |
| Batch parallelism | Partial | Deviation calculation vectorizable |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact computation, no approximations |
| **Timeliness** | 6/10 | Inherits SMA lag (period/2 bars typical) |
| **Overshoot** | 3/10 | Resistant to outlier-induced band explosions |
| **Smoothness** | 7/10 | Smoother than standard deviation under shock |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Internal** | ✅ | All API modes produce identical results |
| **Manual Calc** | ✅ | Formula verification against known values |

ABBER lacks external library equivalents for cross-validation. Validation relies on internal consistency (streaming vs batch vs span) and manual calculation verification.

### Common Pitfalls

**Parameter sensitivity**: Multiplier of 2.0 captures ~89% of data under Gaussian assumptions, but market distributions vary. Adjust based on asset volatility characteristics.

**Lag inheritance**: ABBER inherits SMA lag. For a 20-period setting, expect approximately 10 bars of delay in band response. Not suitable for high-frequency mean reversion where milliseconds matter.

**Band width interpretation**: Narrowing bands signal consolidation, but ABBER narrows more slowly than Bollinger Bands after volatility spikes. The "squeeze" pattern requires recalibration when switching from standard deviation to absolute deviation.