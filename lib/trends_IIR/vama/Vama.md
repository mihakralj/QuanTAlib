# VAMA: Volatility Adjusted Moving Average

> "The market doesn't care about your moving average period. VAMA returns the favor by not caring about a fixed period either."

## The Core Insight

Most moving averages use a fixed lookback period. VAMA takes a different approach: it dynamically adjusts its effective period based on current market volatility relative to historical norms. When short-term volatility exceeds long-term volatility (high activity), VAMA shortens its period for faster response. When volatility contracts (quiet markets), it lengthens the period for smoother output.

The mechanism uses two ATRs (Average True Range) measured over different timeframes. Their ratio determines how the base period scales. Think of it as an automatic gear shift: volatile markets get responsive tracking, while calm markets get noise reduction.

## Historical Context

VAMA emerged from the observation that fixed-period averages create a fundamental mismatch: periods optimal for trending markets over-smooth during volatility spikes, while periods optimized for choppy conditions whipsaw during trends. The volatility ratio approach provides a principled way to adapt rather than choosing a compromise period that works poorly in both regimes.

The ATR-based volatility measurement (using True Range rather than close-to-close changes) captures gap activity and intrabar range that simpler volatility proxies miss. This matters for instruments that gap frequently or have significant intrabar movement.

## Architecture

VAMA consists of three interconnected subsystems:

1. **Dual ATR Engine**: Two RMA (Wilder's smoothed average) calculations track True Range over short and long periods. Both use bias compensation during warmup to avoid the typical EMA startup distortion.

2. **Period Adjustment Logic**: The ratio `long_ATR / short_ATR` scales the base period. When short-term volatility exceeds long-term (ratio < 1), the period shrinks. When short-term is subdued (ratio > 1), the period extends. Clamping prevents extreme values.

3. **Dynamic SMA Calculator**: A circular buffer holds recent values, and SMA is computed over the adjusted period by iterating backwards from the most recent entry.

### The Volatility Ratio

```
volatility_ratio = long_ATR / short_ATR
adjusted_length = base_length × volatility_ratio
adjusted_length = clamp(adjusted_length, min_length, max_length)
```

When short ATR rises relative to long ATR (current volatility spike):
- Ratio drops below 1
- Adjusted length shortens
- VAMA becomes more responsive

When short ATR falls relative to long ATR (volatility contraction):
- Ratio exceeds 1
- Adjusted length extends
- VAMA becomes smoother

### True Range Calculation

True Range captures the full bar's movement including gaps:

$$TR = \max(H - L, |H - C_{prev}|, |L - C_{prev}|)$$

This matters because:
- Gap-up followed by selloff: $|L - C_{prev}|$ captures the true range
- Gap-down followed by rally: $|H - C_{prev}|$ captures the true range
- No gap: $H - L$ applies as expected

### RMA with Bias Compensation

The ATR smoothing uses RMA (Relative Moving Average, also called Wilder's smoothing):

$$\alpha = \frac{1}{\text{period}}$$

$$RMA_t = \alpha \cdot TR_t + (1 - \alpha) \cdot RMA_{t-1}$$

Bias compensation addresses startup:

$$e_t = (1 - \alpha)^t$$

$$RMA_{compensated} = \frac{RMA_{raw}}{1 - e_t}$$

## Mathematical Foundation

### Parameter Relationships

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `baseLength` | 20 | Center point for period adjustment |
| `shortAtrPeriod` | 10 | Current volatility measurement window |
| `longAtrPeriod` | 50 | Historical volatility reference |
| `minLength` | 5 | Floor for adjusted period |
| `maxLength` | 100 | Ceiling for adjusted period |

The ratio of ATR periods determines sensitivity to volatility changes. A 10/50 ratio (5:1) means the short ATR reacts five times faster to volatility changes than the long ATR, creating meaningful but not excessive period swings.

### Effective Period Dynamics

With default parameters and typical market behavior:

| Market Condition | Typical Ratio | Adjusted Length |
|-----------------|---------------|-----------------|
| Volatility spike | 0.5 - 0.8 | 10 - 16 bars |
| Normal conditions | 0.9 - 1.1 | 18 - 22 bars |
| Volatility compression | 1.2 - 2.0 | 24 - 40 bars |

The clamping to `[minLength, maxLength]` prevents extreme values that could cause either excessive noise (too short) or excessive lag (too long).

## Implementation Notes

### Complexity Analysis

| Operation | Complexity | Notes |
|-----------|------------|-------|
| True Range | O(1) | Three comparisons |
| ATR updates | O(1) | RMA is recursive |
| Buffer insertion | O(1) | Circular buffer |
| SMA calculation | O(adjusted_length) | Sum over dynamic window |

The SMA calculation is the dominant cost. With `maxLength = 100`, worst case iterates 100 values. For typical adjusted lengths of 15-30, this remains efficient.

### Memory Layout

- Two `RmaState` structs (24 bytes each): ATR state
- Circular buffer (`double[maxLength]`): Source values
- State copy for bar correction: Additional buffer array

Total footprint scales with `maxLength` parameter.

### Bar Correction (isNew=false)

VAMA supports bar correction by maintaining previous state (`_p_state`, `_p_buffer`). When `isNew=false`, state rolls back before recalculation. This handles real-time bar updates where the current bar's OHLC changes before bar close.

## Performance Profile

| Metric | Value | Notes |
|--------|-------|-------|
| Throughput | ~15M bars/sec | TBar input, single-threaded |
| Allocations | 0 | Hot path allocation-free |
| Complexity | O(adjusted_length) | Per-bar, varies with volatility |
| Warmup | max(longAtrPeriod, maxLength) | Both ATRs and buffer must fill |

## Usage Patterns

### Basic Usage

```csharp
var vama = new Vama(baseLength: 20, shortAtrPeriod: 10, longAtrPeriod: 50);

foreach (var bar in bars)
{
    var result = vama.Update(bar, isNew: true);
    // result.Value contains the volatility-adjusted average
}
```

### With OHLC Data (Recommended)

```csharp
// TBar provides proper True Range calculation
var bar = new TBar(time, open, high, low, close, volume);
var result = vama.Update(bar, isNew: true);
```

### With Single Values (Limited)

```csharp
// Single values create synthetic bar with O=H=L=C
// This results in TR=0, so period stays at baseLength
var value = new TValue(time, close);
var result = vama.Update(value, isNew: true);
```

**Note**: For proper volatility adaptation, VAMA requires OHLC data. Single-value input forces TR=0 and disables the adaptive behavior.

### Event-Driven Chaining

```csharp
var source = new TBarSeries();
var vama = new Vama(source, baseLength: 20);

// VAMA subscribes to source.Pub events
source.Add(new TBar(...));  // Triggers VAMA update
```

## Common Pitfalls

1. **Using TValue input**: VAMA needs OHLC for True Range. Single values produce zero TR and no adaptation.

2. **Mismatched ATR periods**: Short period should be significantly less than long period (typically 5:1 ratio). Similar periods produce ratio ≈ 1 and minimal adaptation.

3. **Narrow min/max range**: If `minLength` and `maxLength` are too close, the adaptive behavior is constrained. Allow meaningful range.

4. **Forgetting warmup**: Both ATR calculations need warmup (especially the longer one). Early values before `IsHot` are approximations.

5. **Over-optimizing parameters**: The default 10/50 ATR ratio works across most instruments. Excessive parameter tuning often means overfitting to historical data.

## Comparison with Alternatives

| Indicator | Adaptation Mechanism | OHLC Required |
|-----------|---------------------|---------------|
| VAMA | ATR volatility ratio | Yes (for proper operation) |
| KAMA | Efficiency ratio (direction vs noise) | No |
| VIDYA | Standard deviation ratio | No |
| JMA | Proprietary adaptive filter | No |

VAMA's ATR-based approach specifically responds to range expansion/contraction, making it well-suited for instruments with significant intrabar movement or gaps. KAMA responds to directional efficiency, VIDYA to statistical volatility.

## References

- Wilder, J.W. (1978). "New Concepts in Technical Trading Systems" - ATR and RMA foundations
- PineScript reference implementation: `vama.pine`
