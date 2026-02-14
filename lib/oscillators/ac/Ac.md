# AC: Acceleration Oscillator

> "Momentum tells you which way the wind is blowing. Acceleration tells you whether the wind is picking up." -- Bill Williams, paraphrased

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | High, Low (computes Median Price internally) |
| Parameters   | `fastPeriod` (int, default: 5, valid: > 0), `slowPeriod` (int, default: 34, valid: > fastPeriod), `acPeriod` (int, default: 5, valid: > 0) |
| Outputs      | double (single value) |
| Output range | Unbounded, centered at zero |
| Warmup       | `slowPeriod + acPeriod - 1` bars (default: 38) |

### Key takeaways

- AC is the second derivative of price: AO measures momentum (first derivative), AC measures the rate of change of that momentum.
- Primary use: detecting when the market's driving force is accelerating or decelerating, independent of the current trend direction.
- Unlike AO (which just shows momentum), AC reveals whether momentum itself is gaining or losing strength.
- AC lags more than AO because it applies an additional SMA smoothing layer on top of the AO calculation.
- A constant-price input causes AC to converge monotonically to zero, confirming it measures change-of-change rather than level.

## Historical Context

Bill Williams introduced the Acceleration Oscillator in *Trading Chaos* (1995) and expanded on it in *New Trading Dimensions* (1998). It is part of his "Profitunity" trading system, which includes the Awesome Oscillator (AO), Alligator, Fractals, and the Gator Oscillator. Williams positioned AC as the "early warning system" that fires before AO and before price itself changes direction.

The indicator has no direct equivalent in TA-Lib, Skender, or Tulip. Those libraries implement AO but not the AC extension. This means validation is self-consistency based: verifying that `AC = AO - SMA(AO, acPeriod)` holds to machine precision, and that batch/streaming/span modes produce identical results.

Williams' original specification uses fixed parameters (5/34/5), matching the Awesome Oscillator's 5/34 SMA pair. QuanTAlib makes all three parameters configurable while preserving Williams' defaults.

## What It Measures and Why It Matters

AC captures the acceleration of the market's driving force. Think of price as position, AO as velocity, and AC as acceleration. When AC is positive and increasing, the market is accelerating in the bullish direction. When AC is negative and decreasing, bearish pressure is building. The zero-line crossing of AC signals that the equilibrium between momentum and its moving average has shifted.

The practical value of AC lies in its ability to signal trend changes before they appear in AO or price. When price is still rising but AC turns negative, the driving force behind the uptrend is decelerating. This provides earlier exit signals than waiting for AO to cross zero. However, this sensitivity comes at a cost: AC produces more signals than AO, and a larger fraction of them are false in ranging markets.

AC is most useful as a confirmation tool within Williams' broader system. Using AC signals alone without the Alligator trend filter or Fractal breakout confirmation leads to overtrading. The indicator excels in trending markets where you need to distinguish between a pullback (AC briefly negative in an uptrend) and a genuine reversal (AC sustained negative with increasing magnitude).

## Mathematical Foundation

### Core Formula

AC is computed in three steps from High and Low prices:

**Step 1: Median Price**

$$
MP_t = \frac{H_t + L_t}{2}
$$

**Step 2: Awesome Oscillator**

$$
AO_t = SMA(MP, N_{fast})_t - SMA(MP, N_{slow})_t
$$

**Step 3: Acceleration Oscillator**

$$
AC_t = AO_t - SMA(AO, N_{ac})_t
$$

where:

- $H_t$, $L_t$ = high and low prices at bar $t$
- $N_{fast}$ = fast SMA period (default 5)
- $N_{slow}$ = slow SMA period (default 34)
- $N_{ac}$ = AC smoothing period (default 5)

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `fastPeriod` | $N_{fast}$ | 5 | $N_{fast} > 0$ |
| `slowPeriod` | $N_{slow}$ | 34 | $N_{slow} > N_{fast}$ |
| `acPeriod` | $N_{ac}$ | 5 | $N_{ac} > 0$ |

### Warmup Period

$$
\text{WarmupPeriod} = N_{slow} + N_{ac} - 1
$$

The `IsHot` flag delegates to the internal AC SMA's `IsHot`, which becomes true after `acPeriod` values have been fed to it. Since those values themselves require `slowPeriod` bars to stabilize, the effective warmup is `slowPeriod + acPeriod - 1 = 38` bars with defaults.

## Architecture & Physics

AC composes three internal SMA instances rather than managing state directly. This delegation pattern differs from most QuanTAlib indicators which use a `record struct State`.

```
High, Low ──→ Median Price ──→ SMA(fast) ──→ ┐
                              SMA(slow) ──→ ┤ SUB ──→ AO ──→ SMA(ac) ──→ ┐
                                            └──────────────────────────── ┤ SUB ──→ AC
```

### 1. Composition Over State

Instead of maintaining raw EMA accumulators, AC delegates to three `Sma` instances (`_smaFast`, `_smaSlow`, `_smaAc`). Each SMA manages its own `RingBuffer` and running sum. This simplifies the implementation but means AC's memory footprint scales with `slowPeriod` (the largest ring buffer).

### 2. Dual Update Overloads

- `Update(TBar)`: Computes median price from High/Low, then processes through the SMA pipeline.
- `Update(TValue)`: Assumes the input is already a median price. Useful for chaining from a pre-computed median series.

### 3. SIMD Batch Path

The static `Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> destination)` method uses `ArrayPool` for temporary buffers and `SimdExtensions.Subtract` for vectorized subtraction of the fast/slow SMA and AO/SMA(AO) arrays. The SMA computations themselves remain scalar (running sum), but the two subtraction passes are SIMD-accelerated.

### 4. Edge Cases

- **NaN/Infinity inputs**: Non-finite High or Low values cause the update to return `Last` unchanged (no state mutation).
- **Constant price**: AC converges monotonically to zero after warmup, as expected for a second-derivative measure.
- **Bar correction**: `isNew=false` restores `Last` from `_p_Last` and passes `isNew=false` through to all three internal SMAs, ensuring consistent rollback.

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Accelerating bullish | AC > 0 and increasing | Momentum gaining strength in uptrend |
| Decelerating bullish | AC > 0 and decreasing | Momentum losing strength despite positive reading |
| Accelerating bearish | AC < 0 and decreasing | Bearish momentum intensifying |
| Decelerating bearish | AC < 0 and increasing | Bearish momentum weakening |

### Signal Patterns

- **Zero-line crossover**: AC crossing from negative to positive signals momentum acceleration shifting bullish. Williams recommends using this only in the direction of the Alligator trend.
- **Color-based histogram**: In Williams' original system, AC bars are colored green (current > previous) or red (current < previous). Two consecutive green bars above zero = buy signal; two consecutive red bars below zero = sell signal.
- **Divergence with price**: Price making new highs while AC makes lower highs is a deceleration warning. More responsive than AO divergence but also noisier.

### Practical Notes

AC signals are most reliable when filtered by a trend indicator (Williams uses the Alligator). In a confirmed uptrend, buy on two consecutive green AC bars; in a confirmed downtrend, sell on two consecutive red AC bars. Using AC without trend filtering produces excessive whipsaws, particularly in low-volatility ranging conditions.

## Related Indicators

- **[AO](../ao/Ao.md)**: Awesome Oscillator is AC's direct input. AC = AO - SMA(AO), making AC the "oscillator of the oscillator."
- **[APO](../apo/Apo.md)**: Absolute Price Oscillator. Similar concept (fast MA - slow MA) but uses close price and EMA, not median price and SMA.
- **[MACD](../../momentum/macd/Macd.md)**: MACD histogram serves a similar acceleration role (signal line divergence from MACD), but with EMA-based smoothing.

## Validation

Validated via self-consistency in [`Ac.Validation.Tests.cs`](Ac.Validation.Tests.cs). No external library implements the Williams AC with identical SMA methodology.

| Library | Status | Notes |
|---------|:------:|-------|
| **Self-consistency** | ✓ | AC = AO - SMA(AO, 5) verified to 1e-10 |
| **Batch vs Streaming** | ✓ | All modes match to 1e-4 |
| **Span vs TBarSeries** | ✓ | Span batch matches TBarSeries batch to 1e-10 |
| **Determinism** | ✓ | Same seed produces identical results to 1e-12 |

Additional validation covers: large-dataset stability (5,000 bars, all finite), monotonic convergence on constant input, and parameter sensitivity verification across different period configurations.

## Performance Profile

### Key Optimizations

- **SIMD subtraction**: `SimdExtensions.Subtract` vectorizes the `AO = fast - slow` and `AC = AO - SMA(AO)` operations in batch mode.
- **ArrayPool**: Batch path rents a single buffer of `4 * len` doubles for median/fast/slow/AO temporaries, avoiding per-call allocation.
- **Aggressive inlining**: `Update(TBar)`, `Update(TValue)`, `Reset()`, and `Batch(Span)` are all decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization overhead.
- **Delegation to SMA**: Individual SMA updates are O(1) using running sums with ring buffers.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| ADD       | 1     | 1             | 1        |
| MUL       | 1     | 3             | 3        |
| SMA Update| 3     | ~5            | 15       |
| SUB       | 2     | 1             | 2        |
| **Total** | **7** | --            | **~21**  |

Each SMA update internally involves one ADD, one SUB (ring buffer swap), and one DIV (average). The three SMA calls dominate the cost.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| Median price computation | Yes | Independent per-element `(H+L)*0.5` |
| SMA passes | No | Running sum has sequential dependency |
| AO subtraction (fast - slow) | Yes | Independent per-element via `SimdExtensions.Subtract` |
| AC subtraction (AO - SMA(AO)) | Yes | Independent per-element via `SimdExtensions.Subtract` |

Two of the five batch steps are SIMD-accelerated. The three SMA passes remain scalar.

## Common Pitfalls

1. **Warmup is 38 bars with defaults**: The slow SMA needs 34 bars, then the AC SMA needs 4 more (its period minus one). Pre-warmup values drift toward the initial SMA seed values rather than zero.

2. **Requires TBar input for standard usage**: The `Update(TBar)` overload computes median price internally. Using `Update(TValue)` directly bypasses the median price computation, which can produce unexpected results if the input is not already `(H+L)/2`.

3. **No external library validation**: Unlike most QuanTAlib oscillators, AC has no TA-Lib, Skender, or Tulip reference implementation. Correctness relies on the mathematical identity `AC = AO - SMA(AO)` and cross-mode consistency tests.

4. **isNew=false must propagate to all three SMAs**: Bar correction rolls back `Last` to `_p_Last` and passes `isNew=false` through to all three internal SMA instances. Forgetting `isNew=false` on any SMA would cause state drift.

5. **AC is noisier than AO**: As a second derivative, AC amplifies small changes in momentum. This makes it more sensitive but also more prone to false signals in low-volatility conditions. Always use with a trend filter.

6. **Batch vs streaming tolerance is 1e-4, not 1e-9**: The SMA delegation pattern introduces minor floating-point ordering differences between batch (SIMD subtraction) and streaming (sequential SMA updates). This is an implementation artifact, not a mathematical error.

## FAQ

**Q: Why does AC use SMA instead of EMA?**
A: Bill Williams specified SMA in his original "Profitunity" system. Using EMA would make AC more responsive but would produce different values than what traders expect from Williams' system. If you want EMA-based acceleration, consider comparing MACD histogram values across bars.

**Q: Can I chain AC after another indicator?**
A: AC accepts `TValue` input via `Update(TValue)`, so you can feed it pre-computed values. However, the standard usage expects `TBar` input to compute median price. For event chaining, use the `Pub` event: `ac.Pub += handler;`.

**Q: Why are there three parameters instead of Williams' fixed 5/34/5?**
A: QuanTAlib makes all parameters configurable for research flexibility. The defaults match Williams' specification. Changing `fastPeriod`/`slowPeriod` also changes the underlying AO behavior, so the indicator is really three parameters deep.

## References

- Williams, B. (1995). *Trading Chaos*. Wiley. Chapter on Accelerator Oscillator.
- Williams, B. (1998). *New Trading Dimensions*. Wiley. Refined Profitunity system with AC signals.
- [Investopedia: Accelerator Oscillator](https://www.investopedia.com/terms/a/accelerationdeceleration-indicator.asp) -- overview of AC usage and interpretation.
- [TradingView: AC](https://www.tradingview.com/support/solutions/43000501837-accelerator-oscillator-ac/) -- interactive AC documentation.
