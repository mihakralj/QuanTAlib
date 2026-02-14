# APO: Absolute Price Oscillator

> "Percentages are for analysts. Traders pay bills in cash. APO tells you the cash value of the trend."

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | Source (close) |
| Parameters   | `fastPeriod` (int, default: 12, valid: > 0), `slowPeriod` (int, default: 26, valid: > fastPeriod) |
| Outputs      | double (single value) |
| Output range | Unbounded, centered at zero |
| Warmup       | `slowPeriod` bars (default: 26) |

### Key takeaways

- APO measures the absolute (currency-denominated) difference between a fast and slow EMA, producing a zero-centered momentum oscillator.
- Primary use: trend confirmation via zero-line crossovers, with readings expressed in the same units as price.
- Unlike PPO (which normalizes by the slow EMA), APO preserves the raw dollar magnitude of the trend spread, making it useful for comparing momentum across time on the same instrument but not across instruments with different price levels.
- APO is functionally identical to MACD with the same parameters (12/26) but without the signal line. The math is literally the same formula.
- Scale sensitivity is APO's defining limitation: an APO reading of 5.0 on a $500 stock is noise; on a $10 stock, it signals a 50% momentum spread.

## Historical Context

The Absolute Price Oscillator is a generic momentum indicator with no single inventor. It appears in standard technical analysis textbooks as the unsigned counterpart to PPO (Percentage Price Oscillator), and shares its mathematical core with MACD. The concept of subtracting a slow moving average from a fast one predates electronic charting, but APO's explicit formulation as "EMA(fast) - EMA(slow)" became standard terminology in the 1990s with the rise of charting platforms.

TA-Lib implements APO as a configurable function with selectable MA type (SMA, EMA, WMA, etc.), defaulting to SMA. QuanTAlib uses EMA exclusively, matching the behavior of `TALib.Functions.Apo` with `MAType.Ema`. Tulip implements `apo` with standard (uncompensated) EMA initialization, which causes warmup divergence from QuanTAlib's compensated EMA. Ooples implements it as `CalculateAbsolutePriceOscillator` with EMA. Skender does not implement APO directly (use MACD instead).

The relationship between APO, PPO, and MACD is worth clarifying: all three compute `FastMA - SlowMA`. MACD adds a signal line (EMA of the result), PPO normalizes by dividing by the slow MA. APO is the raw, unnormalized difference. Choose APO when you need the spread in price units; choose PPO when comparing across instruments; choose MACD when you want crossover signals with a signal line.

## What It Measures and Why It Matters

APO quantifies how far the short-term price trend has diverged from the long-term price trend, measured in the same currency as the underlying asset. When the 12-period EMA sits $3 above the 26-period EMA, APO reads +3.0. This tells you that recent price action is running hotter than the broader trend by exactly $3. When the gap closes, momentum is fading; when it widens, momentum is accelerating.

The practical value of expressing momentum in absolute terms is that it directly translates to P&L exposure. A spread trader running a mean-reversion strategy on a single instrument can use APO to size positions: the APO magnitude maps directly to the expected convergence profit in dollar terms. This is something PPO and normalized oscillators hide behind percentages.

The downside is equally direct: APO readings are not comparable across instruments, timeframes, or even across time on the same instrument if the price level has changed significantly. A stock that traded at $20 in 2010 and $200 in 2024 will produce APO values an order of magnitude apart, even if the underlying momentum dynamics are identical. For cross-asset comparison, use PPO instead.

## Mathematical Foundation

### Core Formula

$$
APO_t = EMA(P, N_{fast})_t - EMA(P, N_{slow})_t
$$

where:

- $P_t$ = close price at bar $t$
- $N_{fast}$ = fast EMA period (default 12)
- $N_{slow}$ = slow EMA period (default 26)
- $EMA$ uses the standard smoothing factor $\alpha = \frac{2}{N + 1}$

Each EMA applies warmup compensation:

$$
\hat{EMA}_t = \frac{REMA_t}{1 - (1 - \alpha)^t}
$$

This bias correction matches PineScript and TA-Lib behavior, diverging from Tulip's uncompensated initialization.

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `fastPeriod` | $N_{fast}$ | 12 | $N_{fast} > 0$ |
| `slowPeriod` | $N_{slow}$ | 26 | $N_{slow} > N_{fast}$ |

### Warmup Period

$$
\text{WarmupPeriod} = N_{slow}
$$

The `IsHot` flag delegates to the internal slow EMA's `IsHot`, which becomes true after `slowPeriod` bars have been processed.

## Architecture & Physics

APO composes two internal EMA instances. The implementation is simpler than SMA-based oscillators (AO, AC) because EMA maintains no ring buffer -- just a scalar accumulator.

```
Source (close) ──→ EMA(fast) ──→ ┐
                   EMA(slow) ──→ ┤ SUB ──→ APO
```

### 1. Dual EMA Composition

Two `Ema` instances (`_emaFast`, `_emaSlow`) are created in the constructor. Each manages its own compensated EMA state (raw accumulator, decay tracker, count). APO's memory footprint is constant regardless of period -- no ring buffers, no sliding windows.

### 2. Source Subscription

APO implements `IDisposable` and supports a chaining constructor: `new Apo(source, 12, 26)`. The source's `Pub` event is subscribed via a cached `_handler` delegate (avoiding closure allocation). `Dispose()` unsubscribes to prevent memory leaks.

### 3. SIMD Batch Path

The static `Batch(ReadOnlySpan<double>, Span<double>)` method computes both EMAs into temporary spans, then uses `SimdExtensions.Subtract` for vectorized subtraction. Buffers use `stackalloc` for sizes <= 1024 elements, falling back to heap allocation for larger datasets.

### 4. Edge Cases

- **NaN/Infinity inputs**: Handled by the internal EMA instances, which substitute last-valid values.
- **Constant price**: APO converges to zero as both EMAs converge to the same value.
- **Bar correction**: `isNew=false` is passed through to both internal EMAs, which handle their own state rollback.

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Bullish momentum | APO > 0 | Fast EMA above slow EMA; short-term trend is stronger |
| Neutral | APO ≈ 0 | Fast and slow EMAs converging; no directional bias |
| Bearish momentum | APO < 0 | Fast EMA below slow EMA; short-term trend is weaker |

### Signal Patterns

- **Zero-line crossover**: APO crossing from negative to positive signals bullish momentum. Equivalent to a MACD zero-line cross. More reliable in trending markets; produces whipsaws in ranges.
- **Divergence**: Price making new highs while APO makes lower highs suggests momentum is weakening. Because APO is in absolute price units, the divergence magnitude directly quantifies the momentum deficit in currency terms.
- **Histogram expansion/contraction**: Increasing APO magnitude means the trend is accelerating; decreasing magnitude means it is decelerating. Unlike bounded oscillators, there is no "overbought" level -- APO can grow indefinitely in a strong trend.

### Practical Notes

APO is best used on a single instrument over a consistent price range. For multi-asset dashboards or long-horizon backtests where price levels change substantially, PPO is the better choice. Pair APO with a trend filter (moving average slope, ADX) to avoid acting on zero-line crossovers during consolidation. The default 12/26 parameters mirror MACD and work well on daily charts; shorter periods (5/13) increase sensitivity for intraday use.

## Related Indicators

- **[PPO](../../momentum/ppo/Ppo.md)**: Percentage Price Oscillator. PPO = APO / SlowEMA * 100. Normalized for cross-asset comparison but loses the absolute dollar magnitude.
- **[MACD](../../momentum/macd/Macd.md)**: Functionally identical to APO(12, 26) but adds a 9-period signal line EMA and histogram. Use MACD when you want signal-line crossover triggers.
- **[AO](../ao/Ao.md)**: Awesome Oscillator. Uses SMA of median price instead of EMA of close. Different smoothing, different input, same differential concept.

## Validation

Validated against external libraries in [`Apo.Validation.Tests.cs`](Apo.Validation.Tests.cs). Tests run batch, streaming, and span modes against each reference library.

| Library | Status | Notes |
|---------|:------:|-------|
| **TA-Lib** | ✓ | `Functions.Apo` with `MAType.Ema`, tolerance 1e-9 |
| **Tulip** | ✓ | `Indicators.apo`, tolerance 1e-9 (tail convergence after warmup) |
| **Ooples** | ✓ | `CalculateAbsolutePriceOscillator(EMA)`, tolerance 1e-6 |
| **Skender** | -- | Not implemented |

Tulip uses uncompensated EMA initialization (first value as seed), while QuanTAlib uses compensated (zero-based) initialization. The two converge after sufficient bars; with 5,000-bar test datasets, the tail values match within tolerance.

## Performance Profile

### Key Optimizations

- **SIMD subtraction**: `SimdExtensions.Subtract` vectorizes the `APO = fast - slow` operation in batch mode.
- **stackalloc for small batches**: Spans <= 1024 elements use `stackalloc`, avoiding heap allocation entirely.
- **Aggressive inlining**: `Update(TValue)`, `Update(TBar)`, `Reset()`, and `Batch(Span)` are all decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization overhead.
- **O(1) streaming**: Each EMA update is a single FMA operation with no ring buffer or sliding window.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| FMA       | 2     | 4             | 8        |
| MUL       | 2     | 3             | 6        |
| DIV       | 2     | 15            | 30       |
| SUB       | 1     | 1             | 1        |
| **Total** | **7** | --            | **~45**  |

The two DIVs are warmup compensation divisions. Once compensation decays past `1e-10`, these become no-ops, dropping steady-state cost to ~15 cycles.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| EMA recursion (fast) | No | IIR dependency chain: each output depends on the previous |
| EMA recursion (slow) | No | Same IIR dependency |
| APO subtraction | Yes | Independent per-element via `SimdExtensions.Subtract` |

Only the final subtraction step is SIMD-accelerated. The two EMA passes remain scalar due to their recursive nature.

## Common Pitfalls

1. **Scale sensitivity**: APO values are denominated in the same units as price. An APO of 5.0 on a $500 stock represents 1% momentum spread; on a $10 stock, it represents 50%. For cross-asset comparison, use PPO instead.

2. **Warmup is 26 bars with defaults**: The slow EMA needs `slowPeriod` bars before its compensation bias becomes negligible. Pre-warmup values are mathematically valid but influenced by initialization bias.

3. **Tulip warmup divergence**: Tulip uses uncompensated EMA (first value as seed). QuanTAlib uses compensated (zero-based). They converge asymptotically, but early values will differ. Validation tests compare tail values only.

4. **isNew=false delegates to EMAs**: APO does not maintain its own `_p_Last` state. Bar correction is handled entirely by the two internal EMA instances. This works correctly but means APO cannot be rolled back independently of its component EMAs.

5. **Not bounded**: Unlike RSI (0-100) or Stochastic (0-100), APO has no fixed output range. There is no inherent "overbought" or "oversold" level. Threshold-based trading requires calibration to the specific instrument and timeframe.

6. **Identical to MACD without signal line**: APO(12, 26) produces the exact same values as MACD(12, 26). If you are already computing MACD, there is no reason to compute APO separately.

## FAQ

**Q: Why does APO use EMA instead of SMA?**
A: QuanTAlib's APO uses EMA to match the standard definition in TA-Lib (`MAType.Ema`). If you want SMA-based differential momentum, use AO (which uses SMA of median price) or configure TA-Lib's APO with `MAType.Sma`.

**Q: What is the difference between APO and MACD?**
A: None, mathematically. Both compute `EMA(fast) - EMA(slow)`. MACD adds a signal line (EMA of the result) and a histogram (MACD - signal). APO is the raw differential without the signal line machinery.

**Q: Can I chain APO after another indicator?**
A: Yes. Use the chaining constructor: `new Apo(source, 12, 26)`. APO subscribes to the source's `Pub` event and updates automatically. Call `Dispose()` when done to unsubscribe.

## References

- Achelis, S. (2000). *Technical Analysis from A to Z*. McGraw-Hill. APO/PPO entry.
- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance. Chapter on oscillators.
- [Investopedia: APO](https://www.investopedia.com/terms/a/apo.asp) -- overview of APO usage and interpretation.
- [StockCharts: Price Oscillators](https://school.stockcharts.com/doku.php?id=technical_indicators:price_oscillators_ppo) -- comparison of APO and PPO.
