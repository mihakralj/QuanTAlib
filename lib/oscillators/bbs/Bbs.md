# BBS: Bollinger Band Squeeze

> *Volatility contraction precedes expansion. The squeeze tells you when to watch.*

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | High, Low, Close (OHLC bar) |
| Parameters   | `bbPeriod` (int, default: 20), `bbMult` (double, default: 2.0), `kcPeriod` (int, default: 20), `kcMult` (double, default: 1.5) |
| Outputs      | double (bandwidth %) + `SqueezeOn` (bool) + `SqueezeFired` (bool) |
| Output range | Bandwidth: 0 to unbounded; Squeeze: boolean |
| Warmup       | `Max(bbPeriod, kcPeriod)` bars (default: 20) |

### Key takeaways

- BBS detects when Bollinger Bands contract inside Keltner Channels, identifying low-volatility consolidation zones that typically precede breakouts.
- Primary use: the "squeeze" signal fires when BB bands fit entirely within KC bands, indicating compressed volatility; `SqueezeFired` marks the first bar after the squeeze ends.
- Unlike standalone Bollinger Bandwidth (which only measures band width), BBS adds the Keltner Channel overlay to distinguish genuine low-volatility consolidation from merely narrow bands.
- The bandwidth component is expressed as a percentage: `((Upper - Lower) / Middle) * 100`, making it comparable across different price levels.
- BBS combines two independent indicator systems (BB + KC) with different volatility measures (standard deviation vs ATR), providing a more robust volatility assessment than either alone.

## Historical Context

The Bollinger Band Squeeze concept originates from John Carter's *Mastering the Trade* (2005), building on John Bollinger's foundational work in *Bollinger on Bollinger Bands* (2001). Carter observed that when Bollinger Bands (which track standard deviation) contract inside Keltner Channels (which track average true range), the market enters a "coiled spring" state. The statistical volatility (BB) dropping below the range-based volatility (KC) signals an unusual degree of price compression.

The squeeze is not a standalone entry signal -- it identifies *when* to pay attention, not *which direction* to trade. Carter's system uses the Linear Regression Oscillator for direction; other implementations use momentum indicators or price action for the directional bias. QuanTAlib's BBS outputs the bandwidth percentage as its primary numeric value, with `SqueezeOn` and `SqueezeFired` as boolean auxiliary outputs.

No standard library implements BBS as a unified indicator. Skender provides Bollinger Bands (with `Width` property) and Keltner Channels separately. QuanTAlib rolls both calculations into a single O(1) streaming indicator with coordinated state management, avoiding the overhead and complexity of maintaining two separate indicator instances.

## What It Measures and Why It Matters

BBS measures the relationship between two different volatility metrics applied to the same price series. Bollinger Bands measure statistical volatility (standard deviation of close prices). Keltner Channels measure range-based volatility (ATR). When statistical volatility drops *below* range-based volatility -- when the BB bands fit inside the KC bands -- something unusual is happening: price is consolidating tighter than its recent trading range would suggest.

This condition matters because volatility is mean-reverting. Extremely low volatility does not persist; it resolves into a directional move. The squeeze identifies the compression phase; the "squeeze fired" moment (first bar after BB bands expand back outside KC bands) marks the beginning of the expansion phase. Traders use this transition to initiate positions in the direction of the emerging trend.

The bandwidth percentage provides continuous information even outside squeeze conditions. Rising bandwidth indicates expanding volatility; falling bandwidth indicates contracting volatility. The squeeze condition is simply the binary threshold where contraction crosses from "normal" to "compressed." Using bandwidth alongside the squeeze boolean gives both the continuous volatility reading and the discrete event signal.

## Mathematical Foundation

### Core Formula

BBS computes Bollinger Bands and Keltner Channels in parallel, then detects when BB nests inside KC.

**Bollinger Bands:**

$$
\text{BB\_Mid}_t = SMA(P, N_{bb})_t
$$

$$
\sigma_t = \sqrt{\frac{1}{N_{bb}} \sum_{i=0}^{N_{bb}-1} P_{t-i}^2 - \text{BB\_Mid}_t^2}
$$

$$
\text{BB\_Upper}_t = \text{BB\_Mid}_t + k_{bb} \cdot \sigma_t
$$

$$
\text{BB\_Lower}_t = \text{BB\_Mid}_t - k_{bb} \cdot \sigma_t
$$

**Keltner Channels:**

$$
\text{KC\_Mid}_t = SMA(P, N_{kc})_t
$$

$$
ATR_t = EMA(TR, N_{kc})_t \quad \text{(warmup-compensated)}
$$

$$
\text{KC\_Upper}_t = \text{KC\_Mid}_t + k_{kc} \cdot ATR_t
$$

$$
\text{KC\_Lower}_t = \text{KC\_Mid}_t - k_{kc} \cdot ATR_t
$$

**Squeeze Detection:**

$$
\text{SqueezeOn} = (\text{BB\_Upper} < \text{KC\_Upper}) \wedge (\text{BB\_Lower} > \text{KC\_Lower})
$$

**Bandwidth:**

$$
\text{Bandwidth}_t = \frac{\text{BB\_Upper}_t - \text{BB\_Lower}_t}{\text{BB\_Mid}_t} \times 100
$$

where:

- $P_t$ = close price at bar $t$
- $N_{bb}$, $N_{kc}$ = BB and KC lookback periods
- $k_{bb}$, $k_{kc}$ = BB standard deviation multiplier and KC ATR multiplier
- $TR$ = True Range = $\max(H - L,\; |H - C_{t-1}|,\; |L - C_{t-1}|)$

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `bbPeriod` | $N_{bb}$ | 20 | $N_{bb} > 0$ |
| `bbMult` | $k_{bb}$ | 2.0 | $k_{bb} > 0$ |
| `kcPeriod` | $N_{kc}$ | 20 | $N_{kc} > 0$ |
| `kcMult` | $k_{kc}$ | 1.5 | $k_{kc} > 0$ |

### Warmup Period

$$
\text{WarmupPeriod} = \max(N_{bb}, N_{kc})
$$

The `IsHot` flag activates after `WarmupPeriod` bars. Both the BB ring buffer and KC ring buffer need to fill before the squeeze detection is meaningful.

## Architecture & Physics

BBS manages parallel BB and KC computations in a single `record struct State`, with two ring buffers for the rolling windows.

```
Close ──→ RingBuffer(BB) ──→ Sum/SumSq ──→ SMA + StdDev ──→ BB_Upper, BB_Lower
Close ──→ RingBuffer(KC) ──→ Sum ──→ SMA ──→ KC_Mid
H,L,C ──→ True Range ──→ EMA(ATR, compensated) ──→ KC_Upper, KC_Lower
                                                              │
BB_Upper < KC_Upper AND BB_Lower > KC_Lower ──→ SqueezeOn ───┤
((BB_Upper - BB_Lower) / BB_Mid) × 100 ──→ Bandwidth ────────┘
```

### 1. Dual Rolling Window State

The state tracks `BbSum`, `BbSumSq` (for O(1) SMA + standard deviation), `KcSum` (for KC SMA), `AtrRaw`/`AtrE` (for compensated ATR EMA), `PrevClose` (for True Range), and `LastValid` values for H/L/C (for NaN substitution). All packed into a single `[StructLayout(LayoutKind.Auto)]` record struct.

### 2. FMA-Accelerated ATR

The ATR uses compensated EMA with `Math.FusedMultiplyAdd`:

```
AtrRaw = FMA(AtrRaw, atrBeta, atrAlpha × TR)
```

Warmup compensation divides by `(1 - atrE)` until the decay factor drops below `1e-10`.

### 3. Squeeze State Machine

`SqueezeOn` is computed every bar from the BB/KC overlap condition. `SqueezeFired` detects the transition from on to off (saved `_prevSqueezeOn` vs current). Both squeeze flags are included in the `_p_state`/`_state` rollback for bar correction.

### 4. Edge Cases

- **NaN/Infinity inputs**: Each of Close, High, Low is independently validated and substituted with its respective `LastValid` value.
- **Zero mean**: When `BB_Mid = 0`, bandwidth returns 0.0 to avoid division by zero.
- **Constant price**: All bands collapse; squeeze is always active (BB width = KC width = 0).
- **Bar correction**: `isNew=false` restores the full state including tick counter and previous squeeze flag.

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Squeeze active | `SqueezeOn = true` | BB inside KC; volatility compressed; breakout pending |
| Squeeze fired | `SqueezeFired = true` | First bar after squeeze ends; breakout initiating |
| Normal volatility | `SqueezeOn = false` | Bands in normal relationship; trend or range in progress |
| Narrowing bandwidth | Bandwidth decreasing | Volatility contracting; approaching squeeze territory |
| Expanding bandwidth | Bandwidth increasing | Volatility expanding; trend acceleration |

### Signal Patterns

- **Squeeze-to-breakout**: The most common pattern. Bandwidth contracts, squeeze activates, then releases. The direction of the first few bars after `SqueezeFired` typically establishes the breakout direction. Use a momentum indicator (Linear Regression, MACD, or MOM) to determine direction.
- **Multiple squeezes**: Consecutive squeeze activations without significant price movement indicate extreme compression. The eventual breakout tends to be more violent. Each failed squeeze adds energy to the "coiled spring."
- **Bandwidth divergence**: Bandwidth declining while price trends suggests the trend is maturing and may reverse. Bandwidth expanding during a pullback suggests the pullback is significant, not a minor retracement.

### Practical Notes

BBS is a timing tool, not a directional indicator. Pair it with a momentum oscillator for direction: when `SqueezeFired` triggers, enter in the direction indicated by the momentum reading. The default parameters (BB 20/2.0, KC 20/1.5) work well on daily charts. For more frequent signals, reduce `kcMult` (making it easier for BB to fit inside KC). For higher-confidence signals, increase `kcMult` or `bbMult` to tighten the squeeze threshold.

## Related Indicators

- **[BBB](../bbb/Bbb.md)**: Bollinger %B. Normalizes price position within the bands (where is price?) while BBS measures the bands themselves (how wide are they?).
- **[Stoch](../stoch/Stoch.md)**: Stochastic Oscillator. Can serve as the directional component when BBS signals a squeeze firing.
- **[TRIX](../trix/Trix.md)**: Triple EMA momentum. Smooth enough to serve as a directional filter for squeeze breakouts without generating false signals.

## Validation

Validated against external libraries in [`Bbs.Validation.Tests.cs`](Bbs.Validation.Tests.cs).

| Library | Status | Notes |
|---------|:------:|-------|
| **Skender** | ✓ | `GetBollingerBands().Width * 100` matches bandwidth, periods 5/10/20/50, tolerance 1e-4 |
| **Self-consistency** | ✓ | Streaming, batch, and span modes agree to 1e-7 |
| **Squeeze span** | ✓ | Span-based squeeze detection matches streaming squeeze states |
| **TA-Lib** | -- | Provides BB only, no squeeze detection |
| **Tulip** | -- | Provides BB only, no squeeze detection |

Additional validation covers: large-dataset stability (bandwidth non-negative, all values finite), `Calculate` returns hot indicator, and finite output guarantee across 5,000 bars.

## Performance Profile

### Key Optimizations

- **O(1) rolling variance**: Running `Sum` and `SumSq` for BB avoid O(N) recomputation per update.
- **FMA for ATR**: `Math.FusedMultiplyAdd` in the EMA recursion eliminates one intermediate rounding step.
- **Compensated ATR warmup**: Warmup bias correction matches PineScript ATR behavior.
- **Resync guard**: Every 1,000 ticks, BB and KC sums are recalculated from ring buffers to bound floating-point drift.
- **Aggressive inlining**: `Update`, `PubEvent`, `GetValidValues`, `RecalculateSums`, and `Batch(Span)` are all inlined.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| ADD/SUB   | 8     | 1             | 8        |
| MUL       | 4     | 3             | 12       |
| FMA       | 1     | 4             | 4        |
| DIV       | 3     | 15            | 45       |
| SQRT      | 1     | 15            | 15       |
| CMP       | 4     | 1             | 4        |
| MAX/ABS   | 3     | 1             | 3        |
| **Total** | **24** | --           | **~91**  |

The three DIVs (BB mean, KC mean, bandwidth normalization) and the SQRT (standard deviation) dominate.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| BB rolling sum/sumSq | No | Sequential ring buffer dependency |
| KC rolling sum | No | Sequential ring buffer dependency |
| ATR EMA | No | IIR dependency chain |
| Band computation | No | Depends on per-element rolling statistics |
| Squeeze detection | No | Depends on per-element BB/KC values |

BBS is fully sequential across all computation paths. No SIMD vectorization is possible.

## Common Pitfalls

1. **Squeeze is a timing tool, not directional**: BBS tells you *when* to trade (squeeze fired), not *which direction*. Without a directional indicator (momentum, linear regression), you have a high-probability timing signal with a coin-flip on direction.

2. **Default kcMult (1.5) makes squeezes common**: With BB mult at 2.0 and KC mult at 1.5, squeezes occur regularly. If signals are too frequent, increase `kcMult` to 2.0 or higher to tighten the threshold.

3. **Warmup requires 20 bars minimum**: Both BB and KC need their periods filled. Pre-warmup squeeze readings are unreliable because partial-window statistics underestimate both standard deviation and ATR.

4. **Bandwidth is percentage-normalized**: Bandwidth = `((Upper - Lower) / Middle) * 100`. This is a percentage, not absolute price units. A bandwidth of 10.0 means the bands span 10% of the SMA value.

5. **Floating-point drift in running sums**: The O(1) computation accumulates rounding errors. The 1,000-tick resync bounds this, but extremely long-running streams may show minor divergence from batch computation.

6. **Bar correction restores squeeze state**: Both `_prevSqueezeOn` and the tick counter are included in the rollback. Forgetting to restore these during `isNew=false` would cause `SqueezeFired` to fire at incorrect times.

## FAQ

**Q: How does BBS compare to TTM Squeeze?**
A: TTM Squeeze (by John Carter) is the same concept: BB inside KC. QuanTAlib's BBS implements the identical squeeze detection logic. The primary difference is in the momentum component: Carter uses Linear Regression Oscillator for direction, while BBS outputs only bandwidth and squeeze state, leaving the directional component to the user's choice.

**Q: Can I use BBS with TValue instead of TBar?**
A: No. BBS requires OHLC data because the Keltner Channel component needs True Range (computed from High, Low, and previous Close). The `Update` method only accepts `TBar`.

**Q: Why is ATR computed with EMA instead of SMA?**
A: The implementation uses EMA-smoothed ATR with warmup compensation to match PineScript behavior. EMA ATR is more responsive to recent volatility changes than SMA ATR, making the Keltner Channel widths more adaptive.

## References

- Carter, J. (2005). *Mastering the Trade*. McGraw-Hill. Chapter on TTM Squeeze.
- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill. Bandwidth and squeeze concepts.
- [Investopedia: Bollinger BandWidth](https://www.investopedia.com/terms/b/bandwidthindicator.asp) -- bandwidth calculation and interpretation.
- [PineScript reference](bbs.pine) -- original implementation source.
