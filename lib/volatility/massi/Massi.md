# MASSI: Mass Index

> "The Mass Index doesn't predict direction—it predicts the moment of maximum uncertainty before clarity emerges."

The Mass Index, developed by Donald Dorsey and introduced in the June 1992 issue of *Technical Analysis of Stocks & Commodities*, identifies potential trend reversals by measuring the narrowing and widening of the range between high and low prices. Unlike directional indicators, MASSI focuses on the *pattern* of range expansion and contraction, particularly the characteristic "reversal bulge" that often precedes significant market turns.

## Historical Context

Donald Dorsey designed the Mass Index to detect trend reversals without predicting direction. His key insight was that range patterns—specifically, a sequence of widening followed by narrowing—often precede major trend changes. The classic signal occurs when MASSI rises above 27 (indicating expanding volatility) and then drops below 26.5 (indicating consolidation), forming what Dorsey called a "reversal bulge."

The indicator gained popularity because it provides advance warning of potential reversals regardless of whether the subsequent move is up or down. This makes it valuable for traders who want to tighten stops or prepare for volatility shifts.

## Architecture & Physics

### 1. Range Input

The Mass Index uses the High-Low range as its primary input:

$$
\text{Range}_t = \text{High}_t - \text{Low}_t
$$

This measures the bar's trading range—the battlefield between buyers and sellers.

### 2. Double EMA Smoothing

The range undergoes two levels of exponential smoothing:

$$
\text{EMA1}_t = \alpha \cdot \text{Range}_t + (1 - \alpha) \cdot \text{EMA1}_{t-1}
$$

$$
\text{EMA2}_t = \alpha \cdot \text{EMA1}_t + (1 - \alpha) \cdot \text{EMA2}_{t-1}
$$

where $\alpha = \frac{2}{\text{emaLength} + 1}$ (default emaLength = 9).

The double smoothing creates a lagged reference. EMA2 always lags EMA1, so their ratio reveals whether range is currently expanding or contracting relative to its recent average.

### 3. Warmup Compensation

This implementation uses proper warmup compensation to eliminate initialization bias:

$$
e_t = (1 - \alpha) \cdot e_{t-1}, \quad e_0 = 1
$$

When $e_t > 10^{-10}$, apply compensation factor $c = \frac{1}{1 - e_t}$ to both EMAs. This ensures accurate values from the first bar rather than gradual convergence.

### 4. EMA Ratio

The ratio captures the relationship between current and smoothed range:

$$
\text{Ratio}_t = \frac{\text{EMA1}_t}{\text{EMA2}_t}
$$

- Ratio > 1.0: Range is expanding (EMA1 leads EMA2 upward)
- Ratio < 1.0: Range is contracting (EMA1 leads EMA2 downward)
- Ratio ≈ 1.0: Range is stable

### 5. Rolling Sum

The final Mass Index sums the ratios over `sumLength` periods (default 25):

$$
\text{MASSI}_t = \sum_{i=0}^{\text{sumLength}-1} \text{Ratio}_{t-i}
$$

With stable ranges, ratios hover near 1.0, so MASSI hovers near sumLength (25). Deviations indicate systematic expansion or contraction patterns.

## Mathematical Foundation

### EMA Smoothing Coefficient

For emaLength = 9:

$$
\alpha = \frac{2}{9 + 1} = 0.2
$$

$$
\text{decay} = 1 - \alpha = 0.8
$$

### Reversal Bulge Threshold

The classic signal uses fixed thresholds:

- **Setup**: MASSI rises above 27.0
- **Trigger**: MASSI falls below 26.5

With sumLength = 25, this means:
- Above 27: Average ratio > 1.08 (sustained range expansion)
- Below 26.5: Average ratio < 1.06 (range contraction beginning)

### State Variables

The implementation maintains:
- `Ema1Raw`: Uncompensated EMA of range
- `Ema2Raw`: Uncompensated EMA of EMA1
- `E`: Compensation factor (decays toward 0)
- `IsCompensated`: Flag for when E <= 1e-10
- `LastRange`: Last valid range (for NaN handling)
- `Bars`: Bar counter for warmup tracking

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| FMA (EMA1 update) | 1 | `decay * ema1Raw + alpha * range` |
| FMA (EMA2 update) | 1 | `decay * ema2Raw + alpha * ema1Raw` |
| MUL (compensation) | 2 | Only during warmup |
| DIV (ratio) | 1 | EMA1 / EMA2 |
| ADD (rolling sum) | 1 | Buffer manages incremental sum |
| **Total** | **~6** | Per bar after warmup |

### Memory Footprint

- State struct: ~48 bytes
- RingBuffer: sumLength × 8 bytes (200 bytes for default 25)
- **Total per instance**: ~250 bytes

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact algorithm with warmup compensation |
| **Timeliness** | 6/10 | Inherent lag from double EMA + sum window |
| **False Signals** | 7/10 | Reversal bulge is specific but not infallible |
| **Simplicity** | 8/10 | Conceptually straightforward |
| **Actionability** | 7/10 | Clear threshold-based signals |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | `MASS` function, matches with proper warmup |
| **Skender** | ✅ | `GetMassIndex`, matches within tolerance |
| **Tulip** | ✅ | `mass` function available |
| **Pine Script** | ✅ | Reference implementation in massi.pine |

## Common Pitfalls

1. **Threshold Rigidity**: The 27/26.5 thresholds were calibrated for Dorsey's original markets. Modern markets may require adjustment. Some practitioners use 26.5/25 or 27.5/27.

2. **No Direction Signal**: MASSI only signals that a reversal may occur, not which direction. Always combine with trend analysis or other directional indicators.

3. **Warmup Period**: Need emaLength + sumLength bars (default: 34) for stable readings. The implementation tracks `IsHot` status.

4. **False Bulges**: Not every bulge above 27 leads to a reversal. The signal works best in conjunction with support/resistance levels or other confirmation.

5. **Range-Only Focus**: MASSI ignores price direction entirely. A stock trending strongly upward with consistent ranges will show stable MASSI readings despite significant price movement.

6. **Parameter Sensitivity**: Shorter emaLength makes the indicator more responsive but noisier. Longer sumLength smooths the output but delays signals.

## Usage Patterns

### Classic Reversal Bulge

```csharp
var massi = new Massi(9, 25);
bool setupTriggered = false;

foreach (var bar in bars)
{
    var result = massi.Update(bar);
    
    if (result.Value > 27.0)
        setupTriggered = true;
    
    if (setupTriggered && result.Value < 26.5)
    {
        // Reversal bulge complete - prepare for trend change
        setupTriggered = false;
    }
}
```

### Batch Processing

```csharp
// From TBarSeries
var massiSeries = Massi.Batch(barSeries, emaLength: 9, sumLength: 25);

// From pre-calculated ranges
var rangeSeries = /* High - Low values */;
var massiSeries = Massi.Batch(rangeSeries);
```

### Span-Based Calculation

```csharp
Span<double> ranges = stackalloc double[500];
Span<double> output = stackalloc double[500];

// Fill ranges with High - Low values
Massi.Calculate(ranges, output, emaLength: 9, sumLength: 25);
```

## References

- Dorsey, Donald. (1992). "The Mass Index." *Technical Analysis of Stocks & Commodities*, June 1992.
- Achelis, Steven B. (2000). *Technical Analysis from A to Z*. McGraw-Hill.
- Pring, Martin J. (2002). *Technical Analysis Explained*. McGraw-Hill.