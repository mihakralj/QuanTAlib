# VA: Volume Accumulation

> "Volume tells you who's winning the argument between bulls and bears—VA keeps a running tally of the score." — Anonymous Trader

Volume Accumulation (VA) measures the cumulative flow of volume weighted by where price closes relative to the bar's midpoint. When price closes above the midpoint, volume is considered buying pressure; when below, selling pressure. The cumulative sum reveals the net directional conviction of market participants over time.

Unlike the Accumulation/Distribution Line (ADL) which uses the full bar range, VA simplifies to the midpoint—a cleaner measure that's less sensitive to extreme wicks. This makes VA particularly useful in markets prone to liquidity spikes that create artificial range extensions.

## Historical Context

Volume Accumulation emerged from the Williams Accumulation/Distribution line developed by Larry Williams in the 1970s. While Williams' original formula used the relationship between close and true range, VA simplifies this to the midpoint relationship:

- **ADL approach**: Uses (Close - Low) / (High - Low) as the multiplier
- **VA approach**: Uses (Close - Midpoint) where Midpoint = (High + Low) / 2

The midpoint simplification offers several advantages:

1. **Symmetric treatment**: Above and below midpoint are treated equally
2. **Reduced sensitivity**: Extreme wicks have less impact than in ADL
3. **Computational simplicity**: One subtraction instead of division
4. **No divide-by-zero**: ADL can produce NaN when High = Low; VA cannot

VA gained popularity in technical analysis software during the 1990s as a cleaner alternative to the more complex ADL formula. It appears in various trading platforms under names like "Volume Accumulation Oscillator" or simply "VA."

## Architecture & Physics

VA operates as a simple cumulative indicator with no lookback period or decay. Each bar contributes a signed volume amount based on price position relative to midpoint.

### Component Breakdown

1. **Midpoint Calculation**: Average of high and low prices
2. **Volume Attribution**: Multiply volume by (close - midpoint)
3. **Cumulation**: Running sum of attributed volume

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| VaValue | double | Cumulative volume accumulation |
| LastValidHigh | double | Fallback for NaN handling |
| LastValidLow | double | Fallback for NaN handling |
| LastValidClose | double | Fallback for NaN handling |
| LastValidVolume | double | Fallback for NaN handling |
| Index | int | Bar counter for warmup |

### Volume Attribution Logic

$$
VA_{contribution} = Volume \times (Close - Midpoint)
$$

- **Close > Midpoint**: Positive contribution (buying pressure)
- **Close < Midpoint**: Negative contribution (selling pressure)
- **Close = Midpoint**: Zero contribution (neutral)

The magnitude scales with volume—high volume bars contribute more to the cumulative total, reflecting the intensity of conviction.

## Mathematical Foundation

### Core Formula

$$
Midpoint_t = \frac{High_t + Low_t}{2}
$$

$$
VA\_Period_t = Volume_t \times (Close_t - Midpoint_t)
$$

$$
VA_t = VA_{t-1} + VA\_Period_t
$$

### Expanded Form

$$
VA_t = \sum_{i=1}^{t} Volume_i \times \left( Close_i - \frac{High_i + Low_i}{2} \right)
$$

### Boundary Cases

| Condition | Midpoint | VA Contribution |
| :--- | :--- | :--- |
| Close = High | (H + L) / 2 | Vol × (H - (H+L)/2) = Vol × (H-L)/2 > 0 |
| Close = Low | (H + L) / 2 | Vol × (L - (H+L)/2) = -Vol × (H-L)/2 < 0 |
| Close = Midpoint | (H + L) / 2 | Vol × 0 = 0 |
| High = Low = Close | Close | Vol × 0 = 0 (doji) |

### Comparison with ADL

| Indicator | Formula | Range |
| :--- | :--- | :--- |
| VA | Vol × (C - (H+L)/2) | Unbounded |
| ADL | Vol × ((C-L) - (H-C)) / (H-L) | ±Volume |

VA produces values in volume units (shares, contracts), while ADL's multiplier is bounded to [-1, +1].

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD | 3 | H+L, cumulative sum, midpoint sub |
| MUL | 1 | Volume × price difference |
| DIV | 1 | Midpoint calculation |
| **Total** | 5 | Per bar, O(1) |

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| Midpoint calculation | ✅ | Fully parallel: (H + L) / 2 |
| Volume attribution | ✅ | Fully parallel: Vol × diff |
| Cumulative sum | ❌ | Sequential prefix sum |

The cumulative sum can be parallelized using prefix scan algorithms, but the benefit is marginal for typical series lengths (< 10K bars). Sequential implementation is preferred for simplicity.

### Memory Footprint

| Scope | Size |
| :--- | :--- |
| Per instance | ~104 bytes (State record struct × 2) |
| Buffer requirements | None (O(1) state) |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic computation |
| **Timeliness** | 10/10 | First bar valid; no warmup |
| **Trend Detection** | 7/10 | Good for sustained moves |
| **Noise Filtering** | 4/10 | None; responds to every bar |
| **Memory** | 10/10 | O(1) constant |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Uses different AD formula |
| **Skender** | N/A | Uses Chaikin ADL |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation (va.pine) |

VA validation focuses on internal consistency between streaming, batch, and span modes (verified with 1e-10 tolerance) and formula correctness against manual calculations.

## Common Pitfalls

1. **Unbounded Values**: VA accumulates indefinitely with no reset mechanism. After thousands of bars, values can become extremely large (millions in volume units). Consider normalizing or using VA change rather than absolute level.

2. **No Mean Reversion**: Unlike oscillators, VA has no center point. The indicator trends; it doesn't oscillate. Divergence analysis works, but overbought/oversold levels don't apply.

3. **Volume Scale Dependency**: VA values depend entirely on volume magnitude. A 100M share day in a liquid stock produces larger contributions than a 10K share day. Cross-instrument comparison requires normalization.

4. **Zero Volume Bars**: Bars with zero volume contribute nothing to VA regardless of price position. This is mathematically correct but can cause visual gaps in the indicator for illiquid instruments.

5. **Range Compression**: Very small bars (High ≈ Low) produce near-zero VA contributions even with significant volume. This differs from ADL which can produce large values from small ranges.

6. **Cumulative Drift**: Any floating-point error accumulates over time. While individual errors are minuscule (~1e-15), millions of bars can accumulate measurable drift. The implementation maintains last-valid tracking for NaN recovery.

7. **Session Considerations**: VA does not reset across sessions. For intraday analysis, consider comparing VA change within a session rather than absolute levels that include prior day's accumulation.

8. **isNew Parameter**: Bar correction (isNew = false) properly restores the previous VA state. Incorrect usage causes cumulative errors that propagate forward indefinitely.

## Interpretation Guide

### Trend Confirmation

| VA Behavior | Price Behavior | Interpretation |
| :--- | :--- | :--- |
| Rising VA | Rising price | Confirmed uptrend (accumulation) |
| Falling VA | Falling price | Confirmed downtrend (distribution) |
| Rising VA | Falling price | Bullish divergence (accumulation despite price drop) |
| Falling VA | Rising price | Bearish divergence (distribution despite price rise) |

### Volume-Weighted Pressure

Since VA weights by volume, large volume days dominate the calculation:

- **Big green bar**: Large positive VA contribution
- **Big red bar**: Large negative VA contribution
- **Low volume day**: Minimal impact on VA regardless of price action

This makes VA particularly useful for identifying whether institutional players (high volume) support the price move.

### Divergence Trading

VA divergences often precede trend reversals:

1. **Bullish divergence**: Price makes lower lows, VA makes higher lows
2. **Bearish divergence**: Price makes higher highs, VA makes lower highs

The divergence signals that volume conviction doesn't support the price extreme—a potential reversal setup.

### Rate of Change Analysis

Rather than absolute VA level, consider VA change:

$$
VA\_ROC_n = VA_t - VA_{t-n}
$$

This removes the unbounded accumulation issue and focuses on recent volume pressure.

## Parameter Selection Guide

VA has no parameters—it's a pure cumulative indicator. Usage variations include:

| Technique | Description |
| :--- | :--- |
| Raw VA | Cumulative value (unbounded) |
| VA change | Difference over N periods |
| VA rate | Percentage change of VA |
| Smoothed VA | EMA/SMA of VA for noise reduction |
| VA divergence | Compare VA slope vs price slope |

### Suggested Smoothing

For noisy instruments, apply a short moving average:

```csharp
var va = new Va();
var smoothedVa = new Ema(5); // 5-period smoothing
// Chain: va.Pub += (_, args) => smoothedVa.Update(args.Value);
```

## References

- Williams, L. (1979). "How I Made One Million Dollars Last Year Trading Commodities." Windsor Books.
- Granville, J. (1976). "Granville's New Strategy of Daily Stock Market Timing." Prentice-Hall.
- Achelis, S. (2000). "Technical Analysis from A to Z." McGraw-Hill.
- TradingView. "PineScript Volume Accumulation." Community Reference.