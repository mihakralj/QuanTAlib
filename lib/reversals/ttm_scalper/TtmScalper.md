# TTM_SCALPER: TTM Scalper Alert

> **Pending Implementation** - Placeholder for John Carter's TTM Scalper Alert indicator

## Historical Context

John Carter designed TTM Scalper Alert for quick identification of potential reversal points using a simple three-bar pattern recognition. The indicator marks pivot highs and lows that can serve as entry triggers for scalping strategies, particularly when combined with other TTM suite indicators.

## Algorithm

### Pivot High Detection
```
pivotHigh = high[1] > high[2] AND high[1] > high[0]
// Three consecutive bars where middle bar has highest high
```

### Pivot Low Detection
```
pivotLow = low[1] < low[2] AND low[1] < low[0]
// Three consecutive bars where middle bar has lowest low
```

### Alternative: Close-Based Version
```
pivotHigh = close[1] > close[2] AND close[1] > close[0]
pivotLow = close[1] < close[2] AND close[1] < close[0]
```

## Default Parameters

| Parameter | Value | Description |
|:----------|:------|:------------|
| UseCloses | false | Use close prices instead of high/low |
| ShowPaintBars | true | Color bars at pivot points |

## Outputs

| Output | Type | Description |
|:-------|:-----|:------------|
| PivotHigh | bool | True on confirmed pivot high (painted on bar-1) |
| PivotLow | bool | True on confirmed pivot low (painted on bar-1) |
| PivotHighPrice | double | Price level of pivot high |
| PivotLowPrice | double | Price level of pivot low |

## Visual Markers

- **▼ (Down Triangle):** Pivot high - potential short entry
- **▲ (Up Triangle):** Pivot low - potential long entry
- Markers appear one bar after confirmation (on the middle bar of the 3-bar pattern)

## Trading Strategy

1. **Long Entry:** Pivot low signal + TTM Squeeze firing bullish + TTM Trend bullish
2. **Short Entry:** Pivot high signal + TTM Squeeze firing bearish + TTM Trend bearish
3. **Stop Placement:** Beyond the pivot point high/low

## Category

**Reversals** - Detects potential trend reversal points using simple 3-bar pivot pattern.

## See Also

- [SWINGS: Swing High/Low Detection](../swings/Swings.md)
- [FRACTALS: Williams Fractals](../fractals/Fractals.md)
- [TTM_SQUEEZE: TTM Squeeze](../../dynamics/ttm_squeeze/TtmSqueeze.md)


## Performance Profile

### Operation Count (Streaming Mode)

TTM Scalper Alert uses a 3-bar high/low range comparison with price action pattern matching — O(1) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Update 3-bar high ring buffer | 1 | 3 cy | ~3 cy |
| Update 3-bar low ring buffer | 1 | 3 cy | ~3 cy |
| Compute 3-bar highest high | 1 | 4 cy | ~4 cy |
| Compute 3-bar lowest low | 1 | 4 cy | ~4 cy |
| Bar pattern comparison (buy/sell signal) | 1 | 3 cy | ~3 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~19 cy** |

O(1) per bar. All state fits in two RingBuffers of size 3. Signal logic is a branchless comparison between current price and the 3-bar extremes.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| 3-bar rolling max (high) | Partial | Small window; scalar faster than SIMD setup |
| 3-bar rolling min (low) | Partial | Same — short window overhead |
| Signal comparison | Yes | Vector conditional-select for buy/sell |
| Output array fill | Yes | Branchless signal assignment |

Limited SIMD benefit due to 3-bar window size — setup cost exceeds savings. The comparison/signal phase is SIMD-friendly for batch output.