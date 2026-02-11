# TTM_TREND: TTM Trend Indicator

> John Carter's TTM Trend - A fast EMA-based trend indicator with color-coded direction.

## Historical Context

John Carter developed the TTM (Trade the Markets) Trend indicator as a clean visual tool for identifying short-term trend direction. Popularized through his book *Mastering the Trade* and the thinkorswim platform, it provides a simple but effective way to see trend changes at a glance using color-coded lines.

## Algorithm

### Core Calculation
```
alpha = 2 / (period + 1)
EMA = alpha × source + (1 - alpha) × prevEMA
```

Or equivalently:
```
EMA = alpha × (source - EMA) + EMA
```

### Trend Detection
```
trend = sign(EMA - prevEMA)
  +1 = bullish (EMA rising)
  -1 = bearish (EMA falling)
   0 = neutral (EMA unchanged)
```

### Strength Measurement
```
strength = |EMA - prevEMA| / prevEMA × 100%
```

## Default Parameters

| Parameter | Value | Description |
|:----------|:------|:------------|
| Period | 6 | EMA lookback period (very fast) |
| Source | HLC/3 | Typical price (High + Low + Close) / 3 |

## Outputs

| Output | Type | Description |
|:-------|:-----|:------------|
| Value | double | Current EMA value |
| Trend | int | Trend direction: +1, -1, or 0 |
| Strength | double | Percent change between EMA values |
| IsHot | bool | True after warming up (2 bars) |

## Color Coding

| Color | Condition | Meaning |
|:------|:----------|:--------|
| 🟢 Green | Trend > 0 | EMA rising (bullish) |
| 🔴 Red | Trend < 0 | EMA falling (bearish) |
| ⚫ Gray | Trend = 0 | EMA unchanged (neutral) |

## Performance

| Metric | Value |
|:-------|:------|
| Time complexity | O(1) per bar |
| Space complexity | O(1) |
| Warmup period | 2 bars |
| Allocations | Zero in hot path |

## Usage Examples

### Basic Usage
```csharp
var ttm = new TtmTrend(period: 6);

// Update with typical price
var result = ttm.Update(new TValue(time, typicalPrice));

// Or update with bar (uses HLC/3 automatically)
var result = ttm.Update(bar);

// Access trend direction
if (ttm.Trend > 0) { /* bullish */ }
else if (ttm.Trend < 0) { /* bearish */ }
```

### Batch Processing
```csharp
var results = TtmTrend.Batch(barSeries, period: 6);
```

### With Indicator Instance
```csharp
var (results, indicator) = TtmTrend.Calculate(barSeries, period: 6);
bool isBullish = indicator.Trend > 0;
double strength = indicator.Strength;
```

## Trading Applications

1. **Trend Following**: Trade in the direction of the EMA color
2. **Trend Confirmation**: Use with other TTM indicators (Squeeze, Wave)
3. **Entry Timing**: Enter on color change with confirmation
4. **Exit Signal**: Exit when color changes against position

## Category

**Dynamics** - Measures trend direction and momentum using fast EMA smoothing.

## See Also

- [TTM_SQUEEZE: TTM Squeeze](../ttm_squeeze/TtmSqueeze.md)
- [TTM_WAVE: TTM Wave](../../oscillators/ttm_wave/TtmWave.md)
- [TTM_LRC: TTM Linear Regression Channel](../../channels/ttm_lrc/TtmLrc.md)
- [SUPER: SuperTrend](../super/Super.md)
