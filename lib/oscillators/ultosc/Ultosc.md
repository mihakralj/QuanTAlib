# ULTOSC: Ultimate Oscillator

> "Why use one timeframe when three can save you from yourself?"

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | High, Low, Close (TBar) |
| **Parameters** | `period1` (default 7), `period2` (default 14), `period3` (default 28) |
| **Outputs** | UltOsc value (`Last`) |
| **Output range** | [0, 100] |
| **Warmup period** | `period3` |

### Key takeaways

- Combines **buying pressure ratios across three timeframes** with 4:2:1 weighting, reducing the false signals inherent in single-period oscillators.
- The buying pressure concept (Close - True Low) measures **demand efficiency** — what fraction of each bar's total range was captured by buyers.
- Output is **bounded [0, 100]**, with readings above 70 suggesting overbought and below 30 suggesting oversold conditions.
- Designed primarily for **divergence detection**: when price makes a new extreme but UltOsc does not, the trend is exhausted.
- Extensively validated against **TA-Lib, Skender, Tulip, and Ooples** — one of the best-supported indicators in the oscillator suite.

## Historical Context

Larry Williams introduced the Ultimate Oscillator in his 1985 article for *Technical Analysis of Stocks & Commodities* magazine. Williams, a legendary trader who famously turned $10,000 into over $1 million in a single year, designed UltOsc to solve a specific problem: single-period oscillators like RSI suffer from two fatal flaws. First, they generate false signals during trends (RSI can stay overbought for weeks in a strong uptrend). Second, they are period-sensitive (a 7-period RSI behaves differently from a 14-period RSI, and neither is objectively "right").

Williams' solution was to use three periods (7, 14, 28) and weight them 4:2:1, giving the most influence to the shortest period for responsiveness while still respecting the broader context. The doubling ratio between periods (7→14→28) ensures each timeframe captures a distinct frequency band. The buying pressure concept — measuring how much of each bar's range was "bought" — provides a more nuanced momentum reading than simple price-change-based oscillators.

## What It Measures and Why It Matters

The Ultimate Oscillator measures the ratio of accumulated buying pressure to accumulated true range across three lookback windows, then combines those ratios with fixed weights. Buying pressure is the distance from the True Low (the lower of today's Low or yesterday's Close) to today's Close. This captures how aggressively buyers pushed price up from the worst-case starting point, including overnight gaps.

The multi-timeframe fusion is the indicator's core innovation. A single-period buying pressure ratio can whipsaw during choppy markets. By combining short (7), medium (14), and long (28) period ratios, UltOsc filters out the noise that traps traders relying on RSI or Stochastics alone. The 4:2:1 weighting ensures the short period dominates for responsiveness while the longer periods provide context and confirmation.

## Mathematical Foundation

### Core Formula

True Low and True High:

$$TrueLow_t = \min(Low_t, \, Close_{t-1})$$

$$TrueHigh_t = \max(High_t, \, Close_{t-1})$$

Buying Pressure and True Range:

$$BP_t = Close_t - TrueLow_t$$

$$TR_t = TrueHigh_t - TrueLow_t$$

Period averages (ratio of accumulated buying pressure to accumulated true range):

$$Avg_n = \frac{\sum_{i=t-n+1}^{t} BP_i}{\sum_{i=t-n+1}^{t} TR_i}$$

Ultimate Oscillator:

$$UltOsc = 100 \times \frac{4 \cdot Avg_7 + 2 \cdot Avg_{14} + 1 \cdot Avg_{28}}{7}$$

When True Range sum is zero (constant price), the average defaults to 0.5 (neutral).

### Parameter Mapping

| Parameter | Formula role | Default | Constraint |
|-----------|-------------|---------|------------|
| `period1` | Short-term buying pressure window | 7 | > 0, < period2 |
| `period2` | Medium-term buying pressure window | 14 | > period1, < period3 |
| `period3` | Long-term buying pressure window | 28 | > period2 |

### Warmup Period

$$W = period3$$

Default configuration (7, 14, 28) warms up in 28 bars.

## Architecture & Physics

### 1. Six Ring Buffers

The implementation maintains six [`RingBuffer`](lib/oscillators/ultosc/Ultosc.cs:35) instances: three for buying pressure (one per period) and three for true range. Each buffer tracks its own running sum, providing O(period) `Sum()` calls per update.

### 2. Buying Pressure Concept

Buying pressure measures how much of the True Range was captured by buyers. If Close equals True High, BP equals TR (maximum buying). If Close equals True Low, BP equals zero (no buying). The ratio BP/TR represents buying "efficiency" — the percentage of total range movement attributable to demand.

### 3. FMA in Weighted Average

The final weighted combination uses nested [`Math.FusedMultiplyAdd`](lib/oscillators/ultosc/Ultosc.cs:183) calls: `FMA(4, avg1, FMA(2, avg2, 1 * avg3))`, combining the three period averages in a single expression with improved numerical precision.

### 4. OHLC Requirement

Unlike most oscillators that accept single values, UltOsc requires full OHLC data for True Range and Buying Pressure computation. The [`Update(TValue)`](lib/oscillators/ultosc/Ultosc.cs:192) overload returns a fixed 50.0 (neutral) as a safety fallback — it cannot compute meaningful results without High/Low context.

### 5. Edge Cases

- **Zero True Range sum**: When all bars in a period window have identical OHLC, the sum of TR is zero. The average defaults to 0.5, producing UltOsc = 50.0.
- **NaN/Infinity inputs**: Invalid inputs return the previous `Last` value unchanged.
- **First bar**: Uses High - Low for TR and Close - Low for BP (no previous close available).
- **Period ordering**: Constructor enforces `period1 < period2 < period3` to prevent nonsensical configurations.

## Interpretation and Signals

### Signal Zones

| Zone | UltOsc value | Meaning |
|------|-------------|---------|
| Overbought | > 70 | Buying pressure dominant across all timeframes |
| Neutral | 30 - 70 | No extreme buying or selling pressure |
| Oversold | < 30 | Selling pressure dominant across all timeframes |

### Signal Patterns

- **Bullish divergence**: Price makes a lower low while UltOsc makes a higher low (with UltOsc < 30). Classic Williams buy setup.
- **Bearish divergence**: Price makes a higher high while UltOsc makes a lower high (with UltOsc > 70). Classic Williams sell setup.
- **Breakout confirmation**: After bullish divergence, UltOsc breaks above the divergence high to confirm the signal.
- **Zero-line context**: UltOsc at 50 means buying pressure exactly equals half of true range — neutral equilibrium.
- **Exit rule**: Williams' original rules specify exiting when UltOsc reaches 70 or when price hits the target.

### Practical Notes

- UltOsc is designed for **divergence trading**, not simple overbought/oversold fading. Using it as a threshold-only indicator misses its primary purpose.
- The default 7/14/28 periods work for daily charts. For intraday, consider scaling down proportionally (e.g., 3/7/14).
- Like all oscillators, UltOsc struggles in strong trends. Combine with trend filters (ADX, moving averages) to avoid counter-trend entries.

## Related Indicators

- [**Stochastic**](../stoch/Stoch.md): Single-period position within range, bounded [0, 100].
- [**Williams %R**](../willr/Willr.md): Inverted raw stochastic, also based on range position.
- [**PGO**](../pgo/Pgo.md): Price displacement normalized by ATR rather than buying pressure.

## Validation

UltOsc has the broadest external validation coverage of any oscillator in the library.

| Library | Status | Notes |
|---------|--------|-------|
| TA-Lib | ✅ | Matches `TA_ULTOSC` (batch and streaming) |
| Skender | ✅ | Matches `GetUltimate` (batch and streaming) |
| Tulip | ✅ | Matches `ultosc` (batch and streaming) |
| Ooples | ✅ | Matches `CalculateUltimateOscillator` |
| Streaming vs Batch | ✅ | Agreement within 1e-6 |
| Span vs TBarSeries | ✅ | Agreement within 1e-10 |
| Constant bars | ✅ | UltOsc = 50.0 |
| Rising prices | ✅ | UltOsc > 50 |
| Falling prices | ✅ | UltOsc < 50 |
| Range bounded [0, 100] | ✅ | Verified for all bars post-warmup |
| Determinism | ✅ | Identical results across runs |

## Performance Profile

### Key Optimizations

- **RingBuffer running sums**: Six buffers maintain BP and TR for each period with O(1) add/remove.
- **Nested FMA**: `Math.FusedMultiplyAdd` for the weighted average eliminates intermediate rounding.
- **Precomputed constants**: Weight values (4.0, 2.0, 1.0, 7.0) are compile-time constants.
- **ArrayPool in batch**: Temporary BP and TR arrays use `ArrayPool<double>.Shared` for zero-allocation batch processing.
- **State copy pattern**: `_prevClose`/`_p_prevClose` and `_index`/`_p_index` for bar correction.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Min/Max | 2 (True Low, True High) |
| Subtractions | 3 (BP, TR, plus oldest removal per buffer) |
| Additions | 6 (BP and TR to 3 buffers each) |
| Sum calls | 6 (BP and TR sums for 3 periods) |
| Divisions | 3 (period averages) + 1 (weighted sum / 7) |
| FMA calls | 2 (nested weighted average) |
| Abs | 0 (True Low/High use Min/Max, not Abs) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| BP/TR precomputation | Scalar loop (data-dependent Min/Max) |
| Running sum accumulation | Scalar (sequential dependency) |
| Period average divisions | Scalar (3 per bar) |
| Weighted average | FMA scalar |
| ArrayPool strategy | `ArrayPool<double>.Shared.Rent/Return` for BP and TR arrays |
| Vectorization potential | Low — running sums and per-bar divisions prevent SIMD |

## Common Pitfalls

1. **Using UltOsc as a simple threshold indicator.** It was designed for divergence detection. Selling at 70 in a strong uptrend will produce consistent losses. The real signal is when price makes a new high but UltOsc does not.
2. **Ignoring the OHLC requirement.** UltOsc needs High, Low, and Close for True Range and Buying Pressure. The `Update(TValue)` fallback returns a constant 50.0, which is useless. Always provide `TBar` data.
3. **Wrong timeframe scaling.** The 7/14/28 defaults assume daily bars. On 5-minute bars, these periods capture very short timeframes. Scale proportionally to your bar frequency.
4. **Violating period ordering.** The periods must satisfy `period1 < period2 < period3`. The constructor throws if this constraint is violated. This is by design: identical or reversed periods produce degenerate behavior.
5. **Division by zero with flat prices.** When True Range is zero for an entire period window, the BP/TR ratio is undefined. The implementation returns 0.5 (neutral) in this case, not an error.
6. **Comparing across different period sets.** UltOsc(5, 10, 20) and UltOsc(7, 14, 28) produce different readings for the same data. Ensure consistent parameters when comparing signals across systems.

## FAQ

**Q: Why is UltOsc bounded to [0, 100] while some oscillators are unbounded?**
A: Because Buying Pressure is always between 0 and True Range (Close is always between True Low and True High), each period average is in [0, 1]. The weighted sum of values in [0, 1] is also in [0, 1], and multiplying by 100 gives [0, 100].

**Q: Why the 4:2:1 weighting?**
A: Williams chose these weights to give the shortest period the most influence (4x the long period's weight), providing responsiveness to recent price action while the longer periods act as context and confirmation. The specific 4:2:1 ratio is a design choice, not mathematically derived.

**Q: How does Buying Pressure differ from simple close-to-close change?**
A: Buying Pressure measures `Close - True Low`, which captures how much of the total available range was "bought." A bar can close unchanged but still show positive buying pressure if it gapped down and then recovered. This makes UltOsc more sensitive to intrabar demand dynamics than simple change-based momentum.

## References

- Williams, Larry. "The Ultimate Oscillator." *Technical Analysis of Stocks & Commodities*, 1985.
- Wilder, J. Welles. *New Concepts in Technical Trading Systems*. Trend Research, 1978. (True Range foundation)
- [PineScript reference](ultosc.pine)
