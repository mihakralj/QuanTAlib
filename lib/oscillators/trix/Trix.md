# TRIX: Triple Exponential Average Oscillator

> *Smooth it once, smooth it twice, smooth it thrice, then ask: is it still moving?*

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | Source (close) |
| Parameters   | `period` (int, default: 14, valid: > 0) |
| Outputs      | double (single value) |
| Output range | Unbounded, centered at zero |
| Warmup       | `period * 3` bars |

### Key takeaways

- TRIX measures the percentage rate of change of a triple-smoothed EMA, converting a trend filter into a zero-centered momentum oscillator.
- Primary use: identifying trend direction and momentum via zero-line crossovers.
- Unlike MACD (which uses two EMAs), TRIX applies three layers of smoothing to a single period, producing far fewer false signals in choppy markets.
- The triple smoothing introduces significant lag: roughly 1.5x the period before meaningful signals emerge.
- A flat price series produces TRIX = 0 exactly (after warmup), regardless of price level. The indicator is scale-independent.

## Historical Context

Jack Hutson introduced TRIX in a 1983 article for *Technical Analysis of Stocks & Commodities* magazine. The indicator emerged during a period when traders were drowning in noise from shorter-term oscillators (RSI, Stochastic) and needed something that could ignore day-to-day chatter while still detecting genuine trend shifts. Hutson's insight was straightforward: apply EMA three times to remove virtually all short-term oscillation, then take the percentage rate of change to convert the smoothed trend line into an oscillator.

The name "TRIX" derives from "triple exponential," though this creates frequent confusion with the triple exponential smoothing method (Holt-Winters). They share nothing beyond the word "triple." TRIX is simply `ROC(EMA(EMA(EMA(source))))` -- three nested EMAs followed by a one-period percentage change.

Implementation differences across platforms center on EMA warmup handling. QuanTAlib uses compensated EMA (dividing by `1 - decay^n` during warmup) to match PineScript and Skender behavior. Libraries that use uncompensated EMA (Tulip, for instance) will diverge during the warmup window, and because TRIX multiplies by 100, even small EMA differences of ~1e-6 become ~1e-4 in the final output.

## What It Measures and Why It Matters

TRIX captures the *acceleration* of trend momentum. Where a single EMA tells you "price is above/below average" and a double EMA reduces noise further, the triple EMA is smooth enough that its rate of change reflects genuine directional commitment rather than random fluctuation. When TRIX crosses above zero, the triple-smoothed trend is accelerating upward. When it crosses below, the trend is decelerating or reversing.

This makes TRIX most useful in trending markets where you want to stay on the right side of a move without being whipsawed. In ranging markets, TRIX oscillates near zero with small amplitude -- which is itself useful information (telling you there is no trend to follow). The indicator works best on daily and weekly timeframes; on intraday data, the triple smoothing can delay signals long enough that the move is half-finished before TRIX confirms it.

The zero-line crossover is the primary signal. Divergence between price and TRIX (price making new highs while TRIX fails to) can precede reversals, though this pattern is less reliable than similar divergences in RSI or MACD because the extra smoothing layer absorbs small momentum shifts that might otherwise produce early warning.

## Mathematical Foundation

### Core Formula

TRIX is computed in four steps: three cascaded EMA passes followed by a percentage rate of change.

**Step 1: First EMA**

$$
\text{EMA1}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA1}_{t-1}
$$

**Step 2: Second EMA (smooths EMA1)**

$$
\text{EMA2}_t = \alpha \cdot \text{EMA1}_t + (1 - \alpha) \cdot \text{EMA2}_{t-1}
$$

**Step 3: Third EMA (smooths EMA2)**

$$
\text{EMA3}_t = \alpha \cdot \text{EMA2}_t + (1 - \alpha) \cdot \text{EMA3}_{t-1}
$$

**Step 4: Percentage rate of change**

$$
\text{TRIX}_t = 100 \times \frac{\text{EMA3}_t - \text{EMA3}_{t-1}}{\text{EMA3}_{t-1}}
$$

where:

- $P_t$ = source price at bar $t$
- $\alpha = \frac{2}{N + 1}$ = EMA smoothing factor
- $N$ = lookback period (default 14)

### Warmup Compensation

Each EMA layer applies bias correction during warmup to match PineScript behavior:

$$
\hat{\text{EMA}}_t = \frac{\text{REMA}_t}{1 - (1 - \alpha)^t}
$$

where $\text{REMA}_t$ is the raw (uncorrected) recursive EMA. The correction factor $(1 - \alpha)^t$ decays toward zero exponentially; once it falls below $10^{-10}$, compensation is bypassed and the raw value is used directly.

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period`  | $N$    | 14      | $N > 0$    |

### Warmup Period

$$
\text{WarmupPeriod} = 3N
$$

The `IsHot` flag activates when `Count > period` (after the first EMA layer has processed at least $N+1$ bars). The `WarmupPeriod` property reports $3N$ to account for full convergence of all three cascaded EMA passes.

## Architecture & Physics

The implementation uses a three-stage compensated EMA pipeline with state managed in a single `record struct`.

```
Source ──→ [Compensated EMA₁] ──→ [Compensated EMA₂] ──→ [Compensated EMA₃] ──→ ROC% ──→ TRIX
              α, decay                α, decay                α, decay
```

### 1. State Management

All scalar state lives in a single `[StructLayout(LayoutKind.Auto)]` record struct containing:

- `Rema1`, `Rema2`, `Rema3`: raw (uncorrected) EMA accumulators for each stage
- `E1`, `E2`, `E3`: compensation decay trackers (initialized to 1.0, multiplied by `decay` each bar)
- `PrevEma3`: previous corrected EMA3 value for ROC calculation
- `Count`: bar counter for warmup tracking
- `LastValid`: last finite input for NaN/Infinity substitution

The local-copy pattern (`var s = _s; ... _s = s;`) enables JIT struct promotion to registers, measured at 15-25% speedup in tight update loops.

### 2. FMA Optimization

Each EMA recursion uses `Math.FusedMultiplyAdd` for the IIR update:

```
Rema = FMA(Rema, decay, α × input)
```

This computes `Rema × decay + α × input` in a single fused operation, eliminating one intermediate rounding step and providing ~5% throughput improvement on hardware with FMA support.

### 3. Edge Cases

- **NaN/Infinity inputs**: Substituted with `LastValid` (or 0.0 if no valid input has been seen). Output remains finite.
- **Division by zero**: When `|PrevEma3| < 1e-10`, TRIX outputs 0.0 instead of computing the percentage change.
- **First bar**: All three EMA stages initialize to the input value; TRIX outputs 0.0 (no prior EMA3 to compare against).
- **Bar correction**: `isNew=false` rolls back to `_ps` (previous state snapshot), enabling same-bar overwrites without accumulating drift.

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Bullish | TRIX > 0 | Triple-smoothed trend is accelerating upward |
| Neutral | TRIX ≈ 0 | No sustained directional momentum |
| Bearish | TRIX < 0 | Triple-smoothed trend is accelerating downward |

### Signal Patterns

- **Zero-line crossover**: TRIX crossing from negative to positive signals a potential bullish trend. The triple smoothing means this signal fires less often than single-EMA crossovers, but with higher reliability. Best confirmed with volume or price action.
- **Divergence**: Price making new highs while TRIX makes lower highs suggests weakening momentum. More sluggish to detect than RSI divergence due to the extra smoothing, but when TRIX diverges, the signal carries weight precisely because it takes significant momentum change to move the triple-smoothed output.
- **Signal line crossover**: A 9-period EMA of TRIX (computed externally) can serve as a signal line, similar to the MACD signal line. Buy when TRIX crosses above its signal line; sell on the reverse.

### Practical Notes

TRIX works best on daily charts or higher timeframes where the triple smoothing lag is acceptable. On 5-minute charts with a 14-period TRIX, you are looking at ~42 bars of warmup (3.5 hours) before signals become meaningful. Pair TRIX with a faster oscillator (RSI, Stochastic) for entry timing while using TRIX for directional bias. In strongly trending markets, TRIX staying above/below zero for extended periods is itself confirmatory.

## Related Indicators

- **[EMA](../../trends_IIR/ema/Ema.md)**: The building block. TRIX is the percentage ROC of three cascaded EMAs; understanding EMA bias correction is essential to understanding TRIX warmup behavior.
- **[DEMA](../../trends_IIR/dema/Dema.md)**: Double EMA smoothing -- one layer fewer than TRIX. Faster to react, more prone to noise.
- **[PPO](../../momentum/ppo/Ppo.md)**: Percentage Price Oscillator. Both express momentum as percentages, but PPO uses the spread between two different-period EMAs rather than the ROC of a single triple-smoothed EMA.
- **[MOM](../../momentum/mom/Mom.md)**: Raw momentum (price difference). TRIX can be thought of as a heavily smoothed, percentage-normalized version of momentum.

## Validation

Validated against external libraries in [`Trix.Validation.Tests.cs`](Trix.Validation.Tests.cs). Tests run across multiple periods (5, 9, 10, 14, 20, 25, 50, 100) with self-consistency checks on 10,000-bar datasets.

| Library | Status | Notes |
|---------|:------:|-------|
| **Skender** | ✓ | `GetTrix(period)`, tolerance 1e-9 |
| **TA-Lib** | ✓ | `Functions.Trix`, tolerance 1e-9 |
| **Tulip** | ✓ | `Indicators.trix`, tolerance 5e-4 to 1e-3 (uncompensated EMA) |

Tulip uses uncompensated EMA, which diverges from the compensated approach used by QuanTAlib, Skender, and TA-Lib. The 100x multiplication in the ROC step amplifies small EMA differences: a ~1e-6 EMA divergence becomes ~1e-4 in the TRIX output. This is a methodology difference, not an error.

Additional validation tests cover: flat-line behavior (TRIX converges to 0), zero-crossing detection (uptrend/downtrend transitions), NaN/Infinity robustness, large-dataset precision (batch vs streaming match to 1e-9), and period sensitivity verification.

## Performance Profile

### Key Optimizations

- **FMA usage**: All three EMA recursions use `Math.FusedMultiplyAdd(rema, decay, alpha * input)`, eliminating intermediate rounding in the IIR accumulation.
- **Precomputed constants**: `_alpha` and `_decay` are computed once in the constructor and stored as `readonly` fields.
- **Aggressive inlining**: `Update(TValue)`, `Handle`, and `Batch(Span)` are decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **State local copy**: The `var s = _s` pattern enables JIT register promotion for the entire state struct during `Update`.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization of locals in all methods.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| FMA       | 3     | 4             | 12       |
| MUL       | 3     | 3             | 9        |
| DIV       | 4     | 15            | 60       |
| CMP       | 4     | 1             | 4        |
| ADD/SUB   | 2     | 1             | 2        |
| **Total** | **16** | --           | **~87**  |

The three DIVs are warmup compensation divisions (`rema / (1 - e)`); once compensation decays below `1e-10`, these become simple assignments, dropping the steady-state cost to ~27 cycles.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| EMA recursion | No | Each EMA output depends on the previous bar's output (IIR dependency chain) |
| ROC percentage | No | Requires sequential `PrevEma3` from the EMA stage |
| NaN substitution | No | Conditional branching on per-element validity |

TRIX is fully recursive across all three EMA stages plus the ROC step. No SIMD vectorization is possible for the core algorithm. The `Batch(Span)` method uses scalar iteration with FMA acceleration.

## Common Pitfalls

1. **Warmup period is 3x the parameter**: With `period=14`, you need 42 bars before the output is fully converged. Pre-warmup values are mathematically valid but reflect initialization bias, not market momentum.

2. **Tulip validation tolerance**: Tulip uses uncompensated EMA. Do not expect bit-exact matches. The 100x amplification from the ROC step means tolerance must be 5e-4 or wider, not the usual 1e-9.

3. **isNew=false overwrites state**: Bar correction replays the current bar without advancing the counter. Failing to pass `isNew=false` for intra-bar updates causes count drift and incorrect warmup detection.

4. **Zero-line crossovers lag real turns**: By the time TRIX crosses zero, the underlying trend change is already well underway. This is a feature (fewer false signals) but means TRIX is not suitable as a standalone entry trigger.

5. **Near-zero denominator**: When `PrevEma3` is near zero (prices close to zero or after extended NaN substitution), the percentage ROC can produce extreme spikes. The implementation guards this at `|PrevEma3| < 1e-10`, but assets trading near zero may still produce outsized readings.

6. **Not scale-bounded**: Unlike RSI (0-100) or Stochastic (0-100), TRIX has no fixed output range. Overbought/oversold thresholds must be calibrated to the specific instrument and timeframe using historical data.

## FAQ

**Q: Why does my TRIX differ from Tulip by ~0.05%?**
A: QuanTAlib uses warmup-compensated EMA (matching PineScript, Skender, and TA-Lib), while Tulip uses standard uncompensated EMA. The triple-EMA cascade amplifies this small difference, and the 100x ROC multiplier magnifies it further. Both are correct for their respective EMA definitions.

**Q: Can I use TRIX as a signal line for another indicator?**
A: Yes. Subscribe via `indicator.Pub += handler;` to chain TRIX after any `ITValuePublisher`. Common patterns include TRIX of RSI (smoothed momentum of momentum) or TRIX with an external EMA signal line.

**Q: What period should I use?**
A: Default 14 works for daily charts. Shorter periods (5-9) increase sensitivity but reintroduce noise that triple smoothing is designed to eliminate. Longer periods (20-50) are useful for weekly charts or for filtering out everything except major trend shifts.

## References

- Hutson, J. (1983). "Good TRIX." *Technical Analysis of Stocks & Commodities*, Vol. 1.
- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance. Chapter on oscillators.
- Achelis, S. (2000). *Technical Analysis from A to Z*. McGraw-Hill. TRIX entry.
- [Investopedia: TRIX](https://www.investopedia.com/terms/t/trix.asp) -- accessible overview of TRIX usage and interpretation.
