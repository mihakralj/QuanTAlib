# TTM_WAVE: TTM Wave Indicator

> *The market speaks in waves. Most traders only hear the ripples.*

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Single value (Close) |
| **Parameters** | None (canonical Fibonacci periods) |
| **Outputs** | WaveA1, WaveA2, WaveB1, WaveB2, WaveC1, WaveC2 (`Last` = Wave1 = WaveA2) |
| **Output range** | Unbounded (MACD histograms in price units) |
| **Warmup period** | 752 bars |
| **PineScript** | [ttm_wave.pine](ttm_wave.pine) |

### Key takeaways

- Composite oscillator built from **six parallel MACD histograms** at Fibonacci EMA periods (34, 55, 89, 144, 233, 377), all sharing fast period 8.
- Groups into three wave bands: **A** (short-term), **B** (medium-term), **C** (long-term), giving a single-pane momentum spectrum across cycle lengths.
- When all three bands share the same sign, momentum is **unanimous**. When they diverge, the market is arguing with itself.
- Matches the **thinkorswim TTM_Wave_A/B/C** thinkScript studies with TOS-compatible property mapping.
- No configurable parameters: the Fibonacci period structure is the design, not a tunable choice.

## Historical Context

John Carter introduced TTM Wave in *Mastering the Trade* (2005, revised 2012) as part of his "Trade The Markets" (TTM) suite alongside TTM Squeeze and TTM Trend. The indicator descends from Gerald Appel's MACD (1979) but extends it by running six parallel MACD channels whose periods follow the Fibonacci sequence: 8, 34, 55, 89, 144, 233, 377.

The design philosophy is straightforward: a single MACD channel captures momentum at one timescale. Stack six of them and you get a momentum spectrum. When short-term waves (A) fire first and medium/long-term waves (B, C) follow suit, the trend has legs. When A waves reverse while C waves persist, you are looking at a pullback, not a reversal.

Carter's original implementation appeared as thinkScript studies on the thinkorswim platform. No external TA libraries (Skender, TA-Lib, Tulip, OoplesFinance) implement TTM Wave, making this a first-principles implementation validated through self-consistency tests.

## What It Measures and Why It Matters

TTM Wave measures momentum acceleration at six different timescales simultaneously. Each channel computes a MACD histogram (the difference between the MACD line and its signal line), which is a second-order momentum measure: it tracks the rate of change of the fast-minus-slow EMA spread relative to its own smoothed average. Positive histogram means momentum is accelerating bullishly; negative means bearishly.

The multi-timeframe structure is the indicator's core value proposition. A single MACD can only tell you about momentum at one scale. TTM Wave tells you whether short-term, medium-term, and long-term momentum agree. Agreement (all six histograms positive or all negative) identifies high-conviction trend environments. Disagreement (A waves positive while C waves negative, or vice versa) identifies pullbacks and potential reversals.

## Mathematical Foundation

### Core Formula

For each channel $k$ with slow/signal period $S_k$:

$$MACD_k = EMA(close, 8) - EMA(close, S_k)$$

$$Signal_k = EMA(MACD_k, S_k)$$

$$Histogram_k = MACD_k - Signal_k$$

where $EMA$ uses smoothing factor $\alpha = 2/(P+1)$:

$$EMA_t = \alpha \cdot x_t + (1 - \alpha) \cdot EMA_{t-1}$$

### Parameter Mapping

| Channel | Slow/Signal period ($S_k$) | Wave group | Property |
|---------|---------------------------|------------|----------|
| 1 | 34 | A (inner) | `WaveA2` |
| 2 | 55 | A (outer) | `WaveA1` |
| 3 | 89 | B (inner) | `WaveB2` |
| 4 | 144 | B (outer) | `WaveB1` |
| 5 | 233 | C (inner) | `WaveC2` |
| 6 | 377 | C (outer) | `WaveC1` |

All channels share fast period $F = 8$ (Fibonacci $F_6$).

### Warmup Period

$$W = \max(8, 377) + 377 - 2 = 752$$

The slowest channel (8, 377, 377) dictates the warmup. [`IsHot`](lib/oscillators/ttm_wave/TtmWave.cs:71) is the conjunction of all six MACD `IsHot` flags.

## Architecture & Physics

### 1. Composition of Six MACD Instances

[`TtmWave`](lib/oscillators/ttm_wave/TtmWave.cs:29) is implemented as a composition of six internal [`Macd`](lib/oscillators/ttm_wave/TtmWave.cs:43) instances rather than manual EMA management. This delegates bar correction, NaN handling, and state management to the battle-tested `Macd` class. The trade-off: six redundant fast EMA computations (all share period 8). The benefit: zero additional state synchronization bugs.

### 2. Wave Grouping

The six histograms map to three wave bands, each containing an inner (smaller period) and outer (larger period) envelope. Within each group, the inner channel reacts faster, the outer channel slower. When the inner crosses zero before the outer, momentum is accelerating at that timescale.

### 3. TOS Compatibility Mapping

| TOS Name | QuanTAlib Property | Definition |
|----------|-------------------|------------|
| Wave1 | [`Wave1`](lib/oscillators/ttm_wave/TtmWave.cs:95) / `WaveA2` | Channel 1 histogram (8,34,34) |
| Wave2High | [`Wave2High`](lib/oscillators/ttm_wave/TtmWave.cs:98) | max(WaveC1, WaveC2) |
| Wave2Low | [`Wave2Low`](lib/oscillators/ttm_wave/TtmWave.cs:101) | min(WaveC1, WaveC2) |

### 4. IDisposable Pattern

Because `TtmWave` subscribes to a source publisher's events, it implements [`IDisposable`](lib/oscillators/ttm_wave/TtmWave.cs:29) to properly unsubscribe and dispose all six internal MACD instances.

### 5. Edge Cases

- **Zero parameters**: No user-configurable parameters exist; the Fibonacci structure is fixed by design.
- **Insufficient warmup**: All six channels must be hot before `IsHot` returns true. Early values are computed but unreliable.
- **NaN/Infinity inputs**: Handled by each internal MACD instance independently.
- **Bar correction**: `isNew=false` propagates to all six channels, each of which rolls back independently.

## Interpretation and Signals

### Signal Zones

| Condition | Meaning |
|-----------|---------|
| All 6 histograms positive | Strong bullish alignment across all timeframes |
| All 6 histograms negative | Strong bearish alignment across all timeframes |
| A positive, B/C negative | Short-term bounce within a bearish trend |
| A negative, B/C positive | Short-term pullback within a bullish trend |
| A/B positive, C negative | Medium-term rally against long-term bearish bias |
| Mixed within groups | Transitional; no clear directional conviction |

### Signal Patterns

- **Full alignment**: All waves share the same sign. Highest probability trend continuation environment.
- **A-wave reversal**: A waves cross zero while B and C persist. Signals a pullback, not a trend change (unless B follows).
- **Sequential ignition**: A fires first, then B, then C. The strongest trends develop when waves "ignite" in sequence from short to long.
- **C-wave divergence**: C waves weakening while A waves strengthen in the opposite direction. Early warning of a potential macro trend change.
- **Inner/outer spread**: Within a wave band, the inner channel crossing zero before the outer confirms momentum acceleration.

### Practical Notes

- Do not trade A-wave zero-crosses in isolation. They fire frequently in choppy markets and carry no conviction without B/C confirmation.
- C waves respond glacially to price changes (period 377). A sudden 5% move barely registers. Use Wave A for timing, Wave C for directional bias.
- Histogram magnitudes are not comparable across wave bands. Longer-period channels naturally produce larger absolute values because the fast-slow EMA spread grows with period.

## Related Indicators

- [**AO**](../ao/Ao.md): Awesome Oscillator, a single-channel momentum histogram (SMA-based rather than EMA).
- [**APO**](../apo/Apo.md): Absolute Price Oscillator, single dual-EMA difference without signal line.
- [**TRIX**](../trix/Trix.md): Triple EMA rate-of-change, another approach to multi-smoothed momentum.

## Validation

No external libraries implement TTM Wave. Validation relies on self-consistency and deterministic reproducibility.

| Check | Status | Notes |
|-------|--------|-------|
| Streaming vs Batch | ✅ | All values match within 1e-10 |
| Streaming vs Batch (all bars) | ✅ | Zero mismatches post-warmup at 1e-8 |
| Primed vs Cold start | ✅ | Last value matches within 1e-8 |
| Deterministic replay | ✅ | Same seed produces identical output at 1e-15 |
| Different seeds differ | ✅ | Different GBM seeds produce distinct outputs |
| All six waves finite | ✅ | All outputs finite after 1000 bars |
| TOS property mapping | ✅ | Wave1=WaveA2, Wave2High/Low correct at 1e-15 |
| Reset + replay | ✅ | Matches fresh computation at 1e-15 |
| Large dataset (5000 bars) | ✅ | All outputs finite, no overflow |
| WarmupPeriod = 752 | ✅ | Verified |
| IsHot timing | ✅ | Engages at or before bar 752 |

## Performance Profile

### Key Optimizations

- **Composition architecture**: Six `Macd` instances handle all state management, bar correction, and NaN handling independently.
- **Zero allocations in Update**: All six histogram extractions produce `TValue` structs on the stack.
- **O(1) per bar**: Each MACD update is constant-time (EMA recursion), so total cost is 6x EMA updates = 18 EMA computations per bar.
- **CollectionsMarshal spans**: Batch output uses pre-sized lists with span access.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| EMA updates | 18 (6 channels x 3 EMAs each: fast, slow, signal) |
| Subtractions | 12 (6 MACD lines + 6 histograms) |
| TValue constructions | 6 (wave outputs) |
| Max/Min | 2 (Wave2High, Wave2Low) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| EMA recursion | Scalar (IIR filter, sequential dependency) |
| Histogram subtraction | Scalar (trivial, not worth vectorizing alone) |
| Six channels | Independent but sequential (could parallelize, not worth the overhead) |
| Vectorization potential | None — 18 IIR recursions per bar prevent SIMD |

## Common Pitfalls

1. **Confusing wave numbering with channel numbering.** WaveA1 is the *outer* A wave (channel 2, period 55), not channel 1. WaveA2 is the *inner* (channel 1, period 34). This matches the TOS naming where the larger envelope gets the "1" suffix.
2. **Expecting C waves to react to short-term moves.** Channel 6 (period 377) needs roughly 752 bars to warm up and responds glacially to price changes. Use Wave A for timing, Wave C for bias.
3. **Trading A-wave zero-crosses in isolation.** Wave A zero-crosses fire frequently in choppy markets. Without confirming B/C wave direction, you are trading noise.
4. **Ignoring the warmup period.** With 752 bars needed, daily charts require three years of history. On 1-minute charts, that is 12.5 hours. Insufficient warmup produces misleading histogram values.
5. **Comparing histogram magnitudes across wave bands.** Longer-period channels naturally produce larger absolute histograms. Normalize by channel period if you need cross-wave magnitude comparison.
6. **Over-optimizing by sharing the fast EMA.** All six channels use fast period 8, so sharing one EMA(8) seems logical. However, the MACD class manages state atomically (previous state rollback). Sharing breaks independent bar correction.

## FAQ

**Q: Why are there no configurable parameters?**
A: The Fibonacci period structure (8, 34, 55, 89, 144, 233, 377) is the design itself, not a tunable choice. Changing the periods would produce a different indicator, not a different configuration of TTM Wave. Carter designed these specific ratios to capture momentum at Fibonacci-harmonic timescales.

**Q: Why does Last return WaveA2 instead of an aggregate?**
A: TOS convention. The primary `Wave1` plot on thinkorswim is the channel 1 (8, 34, 34) histogram, which corresponds to `WaveA2`. All six wave outputs are available as separate properties for multi-wave analysis.

**Q: How do I interpret all six waves at once?**
A: Look for alignment. When all six histograms share the same sign, momentum is unanimous across timescales. When A waves flip while C waves persist, it is a pullback, not a reversal. Sequential ignition (A fires, then B, then C) confirms a developing trend.

## References

- Carter, John. *Mastering the Trade: Proven Techniques for Profiting from Intraday and Swing Trading Setups* (2nd ed.). McGraw-Hill, 2012.
- Appel, Gerald. *The Moving Average Convergence-Divergence Trading Method*. Signalert Corporation, 1979.
- thinkorswim TTM_Wave_A, TTM_Wave_B, TTM_Wave_C thinkScript studies.
