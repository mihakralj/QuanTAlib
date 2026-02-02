# JVOLTYN: Normalized Jurik Volatility

> "When you need to compare apples to apples, normalize your volatility—0 is calm, 100 is chaos."

Normalized Jurik Volatility (JVOLTYN) maps the raw JVOLTY dynamic exponent to a 0-100 scale. While JVOLTY outputs values in the range [1, logParam] (where logParam is period-dependent), JVOLTYN transforms this to a universal scale where 0 represents minimum volatility and 100 represents maximum volatility. This normalization enables direct comparison across different periods and instruments.

## Historical Context

JVOLTY extracts the adaptive volatility component from Mark Jurik's JMA algorithm. The raw output—a dynamic exponent clamped between 1.0 and logParam—is meaningful within the JMA context but awkward for standalone analysis. A period-7 JVOLTY might reach 3.5 at maximum while a period-50 JVOLTY peaks at 4.8. Comparing these raw values across instruments or timeframes requires mental gymnastics.

JVOLTYN applies a simple linear normalization that maps the entire valid range to [0, 100]. Now a reading of 50 means "halfway between minimum and maximum volatility" regardless of the underlying period. This makes JVOLTYN suitable for:

- Cross-instrument volatility comparisons
- Threshold-based strategy rules (e.g., "if volatility > 60, reduce position size")
- Regime classification (low: 0-30, medium: 30-70, high: 70-100)
- Heatmap visualization across a portfolio

The normalization is mathematically trivial but practically essential for systematic trading applications.

## Architecture & Physics

JVOLTYN wraps the complete JVOLTY algorithm and applies a final normalization step.

### 1. Core JVOLTY Computation

The full JVOLTY pipeline executes:

1. **Adaptive Envelope**: Tracks price extremes with volatility-adjusted decay
2. **Local Deviation**: Measures distance from adaptive bands
3. **Short Volatility**: 10-bar SMA of local deviation
4. **Distribution Buffer**: 128-sample circular buffer with trimmed mean
5. **Dynamic Exponent**: Ratio of current to reference volatility, raised to adaptive power

See [JVOLTY documentation](../jvolty/Jvolty.md) for complete algorithmic details.

### 2. Normalization Transform

The raw JVOLTY output $d_t \in [1, \text{logParam}]$ is normalized:

$$
\text{JVOLTYN}_t = \frac{(d_t - 1)}{\text{logParam} - 1} \times 100
$$

where:
- $\text{logParam} = \max(\log_2(\sqrt{L}) + 2, 0)$
- $L = (N - 1) / 2$
- $N$ is the period

**Boundary conditions:**
- $d_t = 1$ → JVOLTYN = 0 (minimum volatility)
- $d_t = \text{logParam}$ → JVOLTYN = 100 (maximum volatility)

### 3. Precomputed Normalization Factor

For efficiency, the normalization factor is computed once in the constructor:

$$
\text{normFactor} = \frac{100}{\text{logParam} - 1}
$$

Then each update simply computes:

$$
\text{JVOLTYN}_t = (d_t - 1) \times \text{normFactor}
$$

This avoids repeated division in the hot path.

## Mathematical Foundation

### Period-Dependent Scaling

The logParam value determines the raw JVOLTY range:

| Period | L | logParam | Max Raw JVOLTY | Norm Factor |
| :---: | :---: | :---: | :---: | :---: |
| 5 | 2.0 | 3.00 | 3.00 | 50.00 |
| 7 | 3.0 | 3.29 | 3.29 | 43.64 |
| 10 | 4.5 | 3.58 | 3.58 | 38.76 |
| 14 | 6.5 | 3.85 | 3.85 | 35.09 |
| 20 | 9.5 | 4.12 | 4.12 | 32.05 |
| 50 | 24.5 | 4.78 | 4.78 | 26.46 |
| 100 | 49.5 | 5.28 | 5.28 | 23.36 |

Longer periods have larger logParam values, meaning the raw JVOLTY has more "headroom" before hitting maximum. The normalization factor compensates for this, ensuring that 100 always represents maximum possible volatility for the given period.

### Edge Case: Very Short Periods

For period = 2 or 3, logParam approaches small values:
- Period 2: L = 0.5, logParam = max(log₂(0.707) + 2, 0) ≈ 1.5
- Period 3: L = 1.0, logParam = max(log₂(1.0) + 2, 0) = 2.0

The normalization handles these correctly, though such short periods provide limited statistical significance.

### RawVolatility Property

JVOLTYN exposes the underlying raw JVOLTY value via the `RawVolatility` property. This allows users to access both:
- `Last.Value` → Normalized [0, 100] output
- `RawVolatility` → Raw [1, logParam] value

Useful when the normalized value is needed for display but the raw value is needed for JMA adaptation or other calculations.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

JVOLTYN adds minimal overhead to JVOLTY:

| Operation | JVOLTY | JVOLTYN Addition | Total |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 45 | 1 | 46 |
| MUL | 5 | 1 | 6 |
| All other ops | ~1,150 | 0 | ~1,150 |
| **Total** | **~1,207** | **~2** | **~1,209 cycles** |

The normalization adds ~2 cycles per bar (<0.2% overhead).

### Memory Footprint

Identical to JVOLTY plus one double for the normalization factor:
- State record struct: ~100 bytes
- Two ring buffers (10 + 128 elements): ~1.1 KB
- Normalization factor: 8 bytes
- **Total per instance**: ~1.5 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Comparability** | 10/10 | Universal 0-100 scale |
| **Spike Rejection** | 9/10 | Inherited from JVOLTY |
| **Regime Detection** | 8/10 | Inherited from JVOLTY |
| **Stability** | 9/10 | Inherited from JVOLTY |
| **Interpretability** | 9/10 | Intuitive percentage scale |

## Validation

JVOLTYN is validated against JVOLTY with manual normalization verification.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **JVOLTY Reference** | ✅ | Output = (JVOLTY - 1) / (logParam - 1) × 100 |

## Common Pitfalls

1. **Not the Same as Percentile Rank**: JVOLTYN is a linear transformation of the raw exponent, not a percentile rank of historical values. A reading of 50 means "halfway between min and max possible" for the current period, not "50th percentile of historical readings."

2. **Period Affects Behavior, Not Scale**: While the 0-100 scale is consistent, the underlying volatility dynamics still depend on period. A period-5 JVOLTYN responds faster than period-50. The normalization doesn't change the algorithm's temporal characteristics.

3. **First Bar Returns 0**: On initialization, JVOLTYN returns 0 (corresponding to raw JVOLTY = 1). This is mathematically correct but may not reflect actual market volatility until the indicator warms up.

4. **Warmup Period Inherited**: JVOLTYN requires the same ~220 bars (for period=14) plus 128 bars for distribution buffer stability. The `IsHot` property indicates warmup completion.

5. **Using RawVolatility for JMA**: If feeding JVOLTYN output to JMA or other algorithms expecting raw JVOLTY values, use `RawVolatility` property, not `Last.Value`.

6. **Threshold Interpretation**: A threshold like "JVOLTYN > 70" means different absolute volatility levels for different periods. Period-14 at JVOLTYN=70 implies raw d ≈ 3.0, while period-50 at JVOLTYN=70 implies raw d ≈ 3.6. For cross-period consistency, this is correct—both represent "70% of maximum possible adaptation."

## Use Cases

1. **Universal Volatility Threshold**: Set position sizing rules using fixed thresholds:
   - JVOLTYN < 30: Full position size
   - JVOLTYN 30-60: 75% position size
   - JVOLTYN > 60: 50% position size

2. **Portfolio Heatmap**: Display JVOLTYN across multiple instruments on a 0-100 color scale. Red indicates high volatility, green indicates low volatility—no per-instrument calibration needed.

3. **Regime Classification**: Classify market regimes using consistent thresholds:
   ```
   Low volatility:    JVOLTYN < 25
   Normal volatility: 25 ≤ JVOLTYN < 60
   High volatility:   60 ≤ JVOLTYN < 85
   Extreme volatility: JVOLTYN ≥ 85
   ```

4. **Strategy Switching**: Toggle between trend-following (JVOLTYN < 40) and mean-reversion (JVOLTYN > 60) strategies based on normalized volatility regime.

5. **Alert Generation**: Trigger alerts when JVOLTYN crosses specific levels (e.g., rises above 75 or falls below 20) without needing to know the underlying period or raw scale.

## API Reference

### Constructor

```csharp
public Jvoltyn(int period = 14)
```

**Parameters:**
- `period`: Lookback period (default: 14, minimum: 2)

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `Last` | `TValue` | Most recent normalized output (0-100) |
| `RawVolatility` | `double` | Raw JVOLTY value [1, logParam] |
| `IsHot` | `bool` | True when indicator has sufficient warmup |
| `WarmupPeriod` | `int` | Bars required for stable output |
| `Name` | `string` | Indicator name with parameters |

### Methods

| Method | Description |
| :--- | :--- |
| `Update(TValue input, bool isNew = true)` | Process new price value |
| `Update(TSeries source)` | Process entire series |
| `Reset()` | Clear all state |

## References

- Jurik Research. (1998-2005). "JMA White Papers." *jurikres.com* (archived).
- QuanTAlib. "JVOLTY: Jurik Volatility." [Documentation](../jvolty/Jvolty.md).