# AO: Awesome Oscillator

> *Awesome is a marketing term. The math is just a moving average crossover. But sometimes, simple is all you need.*

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | High, Low (computes Median Price internally) |
| Parameters   | `fastPeriod` (int, default: 5, valid: > 0), `slowPeriod` (int, default: 34, valid: > fastPeriod) |
| Outputs      | double (single value) |
| Output range | Unbounded, centered at zero |
| Warmup       | `slowPeriod` bars (default: 34) |
| PineScript   | [ao.pine](ao.pine)               |

### Key takeaways

- AO measures market momentum as the difference between a fast and slow SMA of median price, producing a zero-centered histogram.
- Primary use: confirming trend direction and detecting momentum shifts via zero-line crossovers and histogram patterns.
- Unlike MACD (which uses EMA of closing prices), AO uses SMA of median price, making it less responsive but more representative of the bar's full trading range.
- AO is a lagging indicator by construction: SMA smoothing guarantees it confirms trends rather than predicts them.
- A constant-price input causes AO to converge to zero after warmup, confirming it measures differential momentum rather than price level.

## Historical Context

Bill Williams introduced the Awesome Oscillator in *Trading Chaos* (1995) as part of his "Profitunity" trading system. Williams argued that standard indicators fixated on closing prices missed the volatility happening *during* the bar. By focusing on the median price -- the midpoint of each bar's range -- AO captures the market's "balance point" rather than the last-second noise of the close.

AO is a core component of the Williams Trading System, typically used alongside the Alligator indicator, Fractals, and the Accelerator Oscillator (AC). Williams positioned AO as the momentum confirmation layer: the Alligator determines trend direction, Fractals identify entry points, and AO confirms that momentum supports the trade.

The indicator has no TA-Lib implementation, but is widely supported elsewhere. Skender implements it as `GetAwesome()`, Tulip as `ao`, and Ooples as `CalculateAwesomeOscillator`. All use the same basic formula, making cross-library validation straightforward. QuanTAlib makes both periods configurable while preserving Williams' 5/34 defaults.

## What It Measures and Why It Matters

AO quantifies the gap between short-term and long-term market consensus. When the 5-period SMA of median price sits above the 34-period SMA, recent price action is running hotter than the broader trend -- bullish momentum. When it sits below, the short-term consensus has fallen behind -- bearish momentum. The magnitude of the difference tells you how strong that momentum divergence is.

The practical value of AO lies in its simplicity and its use of median price. Using `(High + Low) / 2` instead of `Close` filters out the noise of last-second trades, focusing on where the bar's center of gravity actually landed. This makes AO less susceptible to manipulation or anomalies at the close. The trade-off is that SMA smoothing introduces more lag than EMA-based alternatives like MACD or APO.

AO works best as a confirmation tool within a broader system. Used alone, its zero-line crossovers fire too late to capture the early portion of a move, and its histogram patterns (saucer, twin peaks) are unreliable without trend filtering. Paired with the Alligator or another trend indicator, AO becomes a reliable "is the engine still running?" check. In ranging markets, AO oscillates near zero with small amplitude, which is itself useful information: it tells you there is no trend to follow.

## Mathematical Foundation

### Core Formula

AO is computed in two steps from High and Low prices:

**Step 1: Median Price**

$$
MP_t = \frac{H_t + L_t}{2}
$$

**Step 2: Awesome Oscillator**

$$
AO_t = SMA(MP, N_{fast})_t - SMA(MP, N_{slow})_t
$$

where:

- $H_t$, $L_t$ = high and low prices at bar $t$
- $N_{fast}$ = fast SMA period (default 5)
- $N_{slow}$ = slow SMA period (default 34)

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `fastPeriod` | $N_{fast}$ | 5 | $N_{fast} > 0$ |
| `slowPeriod` | $N_{slow}$ | 34 | $N_{slow} > N_{fast}$ |

### Warmup Period

$$
\text{WarmupPeriod} = N_{slow}
$$

The `IsHot` flag delegates to the internal slow SMA's `IsHot`, which becomes true after `slowPeriod` values have filled its ring buffer. With default parameters, AO produces valid output after 34 bars.

## Architecture & Physics

AO composes two internal SMA instances rather than managing state directly. This delegation pattern trades a small amount of memory overhead for implementation clarity.

```
High, Low ──→ Median Price ──→ SMA(fast) ──→ ┐
                               SMA(slow) ──→ ┤ SUB ──→ AO
```

### 1. Composition Over State

Instead of maintaining raw running-sum accumulators, AO delegates to two `Sma` instances (`_smaFast`, `_smaSlow`). Each SMA manages its own `RingBuffer` and running sum. This simplifies the implementation but means AO's memory footprint scales with `slowPeriod` (the largest ring buffer).

### 2. Dual Update Overloads

- `Update(TBar)`: Computes median price from High/Low, then processes through the SMA pipeline.
- `Update(TValue)`: Assumes the input is already a median price. Useful for chaining from a pre-computed median series.

### 3. SIMD Batch Path

The static `Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> destination)` method uses `ArrayPool` for temporary buffers (3 x len) and `SimdExtensions.Subtract` for vectorized subtraction of the fast and slow SMA arrays. The SMA computations themselves remain scalar (running sum), but the final subtraction pass is SIMD-accelerated.

### 4. Edge Cases

- **NaN/Infinity inputs**: Non-finite values in the `Update(TValue)` path cause the update to return `Last` unchanged with no state mutation. The `Update(TBar)` path computes median price first, so NaN High/Low values propagate as NaN median and are handled by the internal SMAs.
- **Constant price**: AO converges to zero after warmup, as expected when fast and slow SMAs agree.
- **Bar correction**: `isNew=false` restores `Last` from `_p_Last` and passes `isNew=false` through to both internal SMAs, ensuring consistent rollback.

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Bullish momentum | AO > 0 | Fast SMA above slow SMA; short-term consensus is bullish |
| Neutral | AO ≈ 0 | Fast and slow SMAs converging; no directional bias |
| Bearish momentum | AO < 0 | Fast SMA below slow SMA; short-term consensus is bearish |

### Signal Patterns

- **Zero-line crossover**: AO crossing from negative to positive signals momentum shifting bullish. Williams recommends using this only in the direction of the Alligator trend. The triple smoothing of the Alligator prevents acting on crossovers during consolidation.
- **Saucer**: Three consecutive AO bars where the first is red (lower than previous), the second is also red but with smaller magnitude, and the third is green (higher than previous). A bullish saucer above zero suggests momentum is re-accelerating after a brief pullback. Bearish saucer is the mirror below zero.
- **Twin peaks**: Two peaks on the same side of zero where the second peak is closer to zero than the first. Twin peaks below zero with the second peak higher (closer to zero) form a bullish divergence. Twin peaks above zero with the second peak lower form a bearish divergence.

### Practical Notes

AO signals are most reliable when filtered by a trend indicator. In Williams' system, the Alligator provides this filter: buy signals are valid only when the Alligator jaws are opening upward, sell signals only when opening downward. Using AO histogram patterns (saucer, twin peaks) without trend confirmation leads to overtrading, particularly in low-volatility ranging conditions where AO oscillates around zero with small amplitude.

## Related Indicators

- **[AC](../ac/Ac.md)**: Accelerator Oscillator. AC = AO - SMA(AO), making it the second derivative of median price. AC signals trend changes before AO does, at the cost of more noise.
- **[APO](../apo/Apo.md)**: Absolute Price Oscillator. Similar concept (fast MA - slow MA) but uses close price and can use any MA type, not just SMA of median price.
- **[MACD](../../momentum/macd/Macd.md)**: Uses EMA instead of SMA and operates on close price. More responsive than AO but also more prone to false signals in choppy markets.

## Validation

Validated against external libraries in [`Ao.Validation.Tests.cs`](Ao.Validation.Tests.cs).

| Library | Status | Notes |
|---------|:------:|-------|
| **Skender** | ✓ | `GetAwesome(5, 34)`, tolerance 1e-9 |
| **Tulip** | ✓ | `Indicators.ao`, tolerance 1e-9, lookback 33 |
| **Ooples** | ✓ | `CalculateAwesomeOscillator(fastLength: 5, slowLength: 34)`, tolerance 1e-6 |
| **TA-Lib** | -- | Not implemented in TA-Lib |

All three external libraries produce matching results within their respective tolerances. Ooples uses a wider tolerance (1e-6) due to floating-point ordering differences in its SMA implementation. No warmup-related divergence exists because all libraries use standard (non-compensated) SMA.

## Performance Profile

### Key Optimizations

- **SIMD subtraction**: `SimdExtensions.Subtract` vectorizes the `AO = fast - slow` operation in batch mode.
- **ArrayPool**: Batch path rents a single buffer of `3 * len` doubles for median/fast/slow temporaries, avoiding per-call heap allocation.
- **Aggressive inlining**: `Update(TBar)`, `Update(TValue)`, `Reset()`, and `Batch(Span)` are all decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization overhead for all methods.
- **Delegation to SMA**: Individual SMA updates are O(1) using running sums with ring buffers.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| ADD       | 1     | 1             | 1        |
| MUL       | 1     | 3             | 3        |
| SMA Update| 2     | ~5            | 10       |
| SUB       | 1     | 1             | 1        |
| **Total** | **5** | --            | **~15**  |

Each SMA update internally involves one ADD, one SUB (ring buffer swap), and one DIV (average). The two SMA calls dominate the cost.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| Median price computation | Yes | Independent per-element `(H+L)*0.5` |
| SMA passes | No | Running sum has sequential dependency |
| AO subtraction (fast - slow) | Yes | Independent per-element via `SimdExtensions.Subtract` |

Two of the three batch steps are SIMD-accelerated. The two SMA passes remain scalar due to sequential running-sum dependency.

## Common Pitfalls

1. **Warmup is 34 bars with defaults**: The slow SMA needs `slowPeriod` bars to fill its ring buffer. Pre-warmup values reflect incomplete averaging and should not be used for signal generation.

2. **Requires TBar input for standard usage**: The `Update(TBar)` overload computes median price internally. Using `Update(TValue)` directly bypasses the median price computation. If the input is not already `(H+L)/2`, the output will not match the Williams specification.

3. **AO is a lagging indicator**: SMA smoothing guarantees that AO confirms trends rather than predicts them. By the time AO crosses zero, the underlying move is already well underway. This is a feature (fewer false signals) not a bug.

4. **isNew=false must propagate to both SMAs**: Bar correction rolls back `Last` to `_p_Last` and passes `isNew=false` through to both internal SMA instances. Forgetting `isNew=false` on either SMA would cause state drift between batch and streaming modes.

5. **Histogram patterns require trend filtering**: The saucer and twin peaks signals are frequently cited but rarely profitable in isolation. Without the Alligator or another trend filter, these patterns produce excessive whipsaws in ranging markets.

6. **No TA-Lib implementation**: TA-Lib does not include AO, so validation relies on Skender, Tulip, and Ooples. All three match within their respective tolerances.

## FAQ

**Q: Why does AO use SMA instead of EMA?**
A: Bill Williams specified SMA in his original "Profitunity" system. Using EMA would make AO more responsive but would produce different values than what traders expect from Williams' system. If you want EMA-based momentum, consider APO or MACD.

**Q: Can I chain AO after another indicator?**
A: AO accepts `TValue` input via `Update(TValue)`, so you can feed it pre-computed values. However, the standard usage expects `TBar` input to compute median price. For event chaining, use the `Pub` event: `ao.Pub += handler;`.

**Q: Why use median price instead of close?**
A: Median price `(H+L)/2` captures the center of gravity of each bar's trading range, filtering out last-second noise that determines the close. This makes AO less susceptible to manipulation or anomalous prints at the session close.

## References

- Williams, B. (1995). *Trading Chaos*. Wiley. Chapter on Awesome Oscillator.
- Williams, B. (1998). *New Trading Dimensions*. Wiley. Refined Profitunity system with AO signals.
- [Investopedia: Awesome Oscillator](https://www.investopedia.com/terms/a/awesomeoscillator.asp) -- overview of AO usage and interpretation.
- [TradingView: AO](https://www.tradingview.com/support/solutions/43000501826-awesome-oscillator-ao/) -- interactive AO documentation with chart examples.
