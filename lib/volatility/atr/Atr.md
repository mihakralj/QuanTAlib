# ATR: Average True Range

> *Volatility is the price of admission. The question is whether the ride is worth it.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Atr)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `rma.WarmupPeriod` bars                          |
| **PineScript**   | [atr.pine](atr.pine)                       |

- The Average True Range measures market "heat" with complete disregard for direction.
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires `rma.WarmupPeriod` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Average True Range measures market "heat" with complete disregard for direction. It ignores whether the market is screaming upward or crashing downward. ATR cares only about magnitude. When ATR is high, expect wide swings. When ATR is low, expect narrow consolidation. Most traders mistakenly use ATR to find entries. Its true power lies in exits and position sizing. ATR answers the critical question: "How far can this asset move against me in a single day?"

## Historical Context

J. Welles Wilder Jr. introduced ATR in his 1978 *New Concepts in Technical Trading Systems*. This is the same book that gave us RSI, ADX, and the Parabolic SAR. Wilder was a mechanical engineer turned real estate developer turned trader. He approached markets with an engineer's obsession for robust systems.

The insight behind ATR: simple High-Low range misses overnight gaps. If a stock closes at $100 and opens at $110 the next day, the High-Low range might be small, but the *true* volatility from the previous close was substantial. ATR captures this "invisible" volatility through the True Range formula.

Wilder chose RMA (his smoothing method) rather than SMA because RMA produces smoother, less reactive output. ATR should reflect the underlying volatility regime, not every single spike. The infinite memory of RMA gives ATR its characteristic inertia: it rises fast on volatility shocks but decays slowly back to normal.

## Architecture & Physics

ATR is a two-stage indicator: True Range calculation followed by RMA smoothing.

### 1. True Range (TR)

True Range captures the maximum possible price movement from the previous close:

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

Where:
- $H_t$: Current bar high
- $L_t$: Current bar low
- $C_{t-1}$: Previous bar close

**For the first bar** (no previous close available): $TR_0 = H_0 - L_0$

The three components capture different gap scenarios:
- $H - L$: Normal intraday range (no gap)
- $|H - C_{prev}|$: Gap up followed by intraday high
- $|L - C_{prev}|$: Gap down followed by intraday low

### 2. RMA Smoothing (Wilder's Method)

True Range is smoothed using RMA:

$$
ATR_t = \frac{ATR_{t-1} \times (N-1) + TR_t}{N}
$$

Equivalent to EMA with $\alpha = 1/N$. This produces slower decay than standard EMA ($\alpha = 2/(N+1)$).

### The Gap Problem Illustrated

| Scenario | Close | Open | High | Low | H-L | True Range |
| :------- | ----: | ---: | ---: | --: | --: | ---------: |
| Normal bar | 100 | 101 | 104 | 99 | 5 | 5 |
| Gap up | 100 | 108 | 112 | 107 | 5 | **12** |
| Gap down | 100 | 93 | 95 | 90 | 5 | **10** |

Standard range (H-L) shows 5 for all three scenarios. True Range correctly identifies the gap scenarios as higher volatility.

## Mathematical Foundation

### Transfer Function

ATR applies RMA to True Range. The RMA transfer function:

$$
H_{RMA}(z) = \frac{\alpha}{1 - (1-\alpha)z^{-1}}
$$

where $\alpha = 1/N$.

### Half-Life Analysis

For RMA with $\alpha = 1/N$:

$$
t_{1/2} = \frac{\ln(2)}{\ln(1/(1-\alpha))} \approx 0.693 \times (N-1)
$$

A 14-period ATR has half-life of approximately 9 bars. A volatility spike from 50 bars ago still contributes ~2% to the current reading.

### Warmup Period

ATR requires $N$ bars for RMA initialization. The first $N$ values are progressively weighted and may differ from steady-state behavior. Full convergence (within 1% of stable reading) requires approximately $4.6N$ bars.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| SUB (H - L) | 1 | 1 | 1 |
| SUB (H - prevC) | 1 | 1 | 1 |
| ABS | 2 | 1 | 2 |
| SUB (L - prevC) | 1 | 1 | 1 |
| MAX (three-way) | 2 | 1 | 2 |
| MUL (ATR × (N-1)) | 1 | 3 | 3 |
| ADD (+ TR) | 1 | 1 | 1 |
| DIV (/ N) | 1 | 15 | 15 |
| **Total** | **10** | — | **~26 cycles** |

The division dominates (~58% of cycles). The three-way max is typically implemented as two comparisons.

### SIMD Analysis

ATR's True Range calculation involves data-dependent max operations and absolute values. The RMA smoothing is recursive and cannot be parallelized across bars.

| Component | SIMD Potential | Notes |
| :-------- | :------------- | :---- |
| TR calculation | Limited | Max/Abs can vectorize but requires gather for prevClose |
| RMA smoothing | None | Recursive dependency |
| Batch TR | 4× speedup | Can vectorize when processing multiple bars |

### Benchmark Results

Test environment: Intel i7-12700K, .NET 10.0, AVX2, 500,000 bars.

| Metric | Value | Notes |
| :----- | ----: | :---- |
| **Streaming throughput** | ~8 ns/bar | Single `Update(TBar)` call |
| **Batch throughput** | ~5 ns/bar | TBarSeries input |
| **Allocations (hot path)** | 0 bytes | State in struct |
| **Complexity** | O(1) | Per bar |
| **State size** | ~56 bytes | RMA state + prevBar |

### Comparative Performance

| Library | Time (500K bars) | Allocated | Relative |
| :------ | ---------------: | --------: | :------- |
| **QuanTAlib** | ~4 ms | 0 B | baseline |
| TA-Lib | ~3.5 ms | 32 B | 0.88× |
| Tulip | ~3.5 ms | 0 B | 0.88× |
| Skender | ~45 ms | 24 MB | 11× slower |

### Quality Metrics

| Metric | Score | Notes |
| :----- | ----: | :---- |
| **Accuracy** | 10/10 | Matches Wilder's definition exactly |
| **Timeliness** | 6/10 | Lags due to RMA smoothing; reflects past volatility |
| **Overshoot** | 10/10 | Absolute measure; cannot overshoot |
| **Smoothness** | 8/10 | Smooth decay due to RMA inertia |

## Validation

Validated against external libraries in `Atr.Validation.Tests.cs`. Tests run against 5,000 bars with tolerance of 1e-9.

| Library | Batch | Streaming | Span | Notes |
| :------ | :---: | :-------: | :--: | :---- |
| **TA-Lib** | ✅ | ✅ | ✅ | Matches `TA_ATR` exactly |
| **Skender** | ✅ | ✅ | ✅ | Matches `GetAtr` |
| **Tulip** | ✅ | ✅ | ✅ | Matches `atr` |
| **Ooples** | ✅ | — | — | Matches `CalculateAverageTrueRange` |

## Common Pitfalls

1. **Directionality Assumption**: ATR is non-directional. A crashing market has high ATR. A rallying market has high ATR. Do not use ATR to predict direction. Use it to measure potential magnitude of moves.

2. **Scale Dependence**: ATR is absolute, not percentage-based. An ATR of 5.0 on a $100 stock (5% daily range) differs from ATR of 5.0 on a $10 stock (50% daily range). Use NATR (Normalized ATR, also known as ATRP) for cross-asset comparisons.

3. **Lag Characteristics**: Because RMA decays slowly, ATR lags actual volatility changes. It tells what *has* happened, not what *will* happen. A volatility spike appears immediately; the subsequent decay takes many bars.

4. **First Bar Handling**: The first TR uses High-Low only (no previous close exists). Some implementations skip the first bar or use a different initialization. QuanTAlib follows Wilder's specification.

5. **TValue vs TBar Input**: ATR is designed for OHLC data (TBar). If fed a TValue, QuanTAlib assumes the value *is* the pre-calculated True Range. This can produce unexpected results if passing close prices directly.

6. **Period Selection**: Wilder recommended 14 periods. For intraday scalping, consider 10 periods. For position trading, consider 20 or 21 periods. Match the period to your holding horizon.

7. **Bar Correction**: When using `isNew=false` for bar corrections, ATR correctly preserves the previous bar's close for TR calculation. The internal RMA also handles state rollback.

## Usage Examples

```csharp
// Streaming with TBar input (recommended)
var atr = new Atr(14);
foreach (var bar in liveBarStream)
{
    var result = atr.Update(bar);
    Console.WriteLine($"ATR: {result.Value:F4}");
}

// Batch processing with TBarSeries
var bars = new TBarSeries();
// ... populate bars ...
var atrSeries = Atr.Batch(bars, period: 14);

// Position sizing with ATR
double accountRisk = 1000.0;  // Risk $1000 per trade
double atrValue = atr.Last.Value;
double stopDistance = 2.0 * atrValue;  // 2 ATR stop
int positionSize = (int)(accountRisk / stopDistance);

// Trailing stop calculation
double entryPrice = 100.0;
double atrStop = entryPrice - (1.5 * atrValue);  // 1.5 ATR trailing stop

// Event-driven chaining
var source = new TBarSeries();
var atr14 = new Atr(source, 14);
// ATR updates automatically when bars are added to source
```

## C# Implementation Considerations

### Delegation to RMA

ATR delegates smoothing to an internal RMA instance:

```csharp
private readonly Rma _rma;
```

This reuses RMA's warmup compensation and state management logic.

### State Management

```csharp
private TBar _prevBar;          // Previous bar for TR calculation
private bool _isInitialized;    // First bar flag
```

The implementation tracks the previous bar to compute True Range gaps. The `_isInitialized` flag handles the first-bar edge case where no previous close exists.

### True Range Calculation

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public TValue Update(TBar input, bool isNew = true)
{
    double tr;
    if (!_isInitialized)
    {
        tr = input.High - input.Low;  // First bar: H-L only
    }
    else
    {
        double hl = input.High - input.Low;
        double hpc = Math.Abs(input.High - _prevBar.Close);
        double lpc = Math.Abs(input.Low - _prevBar.Close);
        tr = Math.Max(hl, Math.Max(hpc, lpc));
    }
    // ... RMA smoothing ...
}
```

### Batch True Range Calculation

For TBarSeries input, TR is calculated for all bars first, then passed to RMA:

```csharp
private static TSeries CalculateTrueRange(TBarSeries source)
{
    // First bar: H - L
    v.Add(source[0].High - source[0].Low);
    
    // Subsequent bars: max of three components
    for (int i = 1; i < source.Count; i++)
    {
        double hl = bar.High - bar.Low;
        double hpc = Math.Abs(bar.High - prevBar.Close);
        double lpc = Math.Abs(bar.Low - prevBar.Close);
        v.Add(Math.Max(hl, Math.Max(hpc, lpc)));
    }
}
```

### Memory Layout

| Component | Size | Purpose |
| :-------- | ---: | :------ |
| `_rma` (Rma) | ~40 bytes | RMA smoothing state |
| `_prevBar` (TBar) | 48 bytes | Previous bar for gap calculation |
| `_isInitialized` | 1 byte | First bar flag |
| **Total per instance** | **~90 bytes** | No period-dependent allocations |

## References

- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research. Chapter: Average True Range.
- Kaufman, P. (2013). *Trading Systems and Methods*. Wiley. (ATR-based position sizing)
- Kase, C. (1996). "Trading with the True Range." *Technical Analysis of Stocks & Commodities*. (TR variations)
