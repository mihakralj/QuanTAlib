# MAMA: Ehlers MESA Adaptive Moving Average

> *John Ehlers again. This time, he built a moving average that doesn't just adapt to volatility—it adapts to the phase of the market cycle. It's like having a GPS for your trend.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `fastLimit` (default 0.5), `slowLimit` (default 0.05)                      |
| **Outputs**      | Single series (Mama)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `50` bars                          |
| **PineScript**   | [mama.pine](mama.pine)                       |
| **Signature**    | [mama_signature](mama_signature.md) |


- MAMA (MESA Adaptive Moving Average) is a unique adaptive moving average that uses the Hilbert Transform to determine the phase rate of change of th...
- **Similar:** [FRAMA](../frama/frama.md), [KAMA](../kama/kama.md) | **Complementary:** FAMA crossover | **Trading note:** MESA Adaptive MA by Ehlers; Hilbert Transform cycle-adaptive smoothing.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

MAMA (MESA Adaptive Moving Average) is a unique adaptive moving average that uses the Hilbert Transform to determine the phase rate of change of the market cycle. It produces two outputs: MAMA (the adaptive average) and FAMA (Following Adaptive Moving Average), which acts as a slower, confirming signal.

## Historical Context

Introduced by John Ehlers in *MESA and Trading Market Cycles*, MAMA was designed to solve the problem of lag in a fundamentally different way. Instead of using price volatility (like KAMA or VIDYA), it uses the *cycle period*. When the cycle is short (fast market), MAMA speeds up. When the cycle is long (slow market), MAMA slows down.

Ehlers published the original EasyLanguage code in September 2001 in *Technical Analysis of Stocks & Commodities*. TradeStation's `ArcTangent` function returns degrees, so Ehlers' formulas mixed degrees (for phase) and radians (for trigonometry). When ported to C, Python, and C#, most implementations cargo-culted the numbers without understanding the unit conversions. Result: every MAMA implementation out there has subtle mathematical errors.

## Architecture & Physics

The architecture is a direct application of the Hilbert Transform Homodyne Discriminator.

1. **Hilbert Transform**: Decomposes price into In-Phase (I) and Quadrature (Q) components.
2. **Phase Calculation**: Computes the phase angle from I and Q.
3. **Alpha Adaptation**: The smoothing alpha is derived from the rate of change of the phase.
    * Fast Phase Change = High Alpha (Fast MA).
    * Slow Phase Change = Low Alpha (Slow MA).

Ehlers' genius was recognizing that market cycles have *phase*. When phase advances steadily (trending), use slow alpha. When phase stutters or reverses (cycle breakdown), use fast alpha. This is why MAMA responds instantly to trend changes while staying smooth in established trends.

The Homodyne Discriminator is borrowed from radio engineering. It measures frequency by multiplying a signal with a delayed copy of itself. In markets, this translates to measuring how fast the cycle period is changing. Fast change means uncertainty. Uncertainty means tighten the filter.

## Mathematical Foundation

### 1. Pre-Smoothing

A 4-tap FIR filter removes high-frequency noise (Nyquist limit) to prevent aliasing before the Hilbert Transform.

$$ \text{Smooth}_t = \frac{4 P_t + 3 P_{t-1} + 2 P_{t-2} + P_{t-3}}{10} $$

### 2. Hilbert Transform & Detrending

The signal is detrended and split into In-Phase ($I$) and Quadrature ($Q$) components using a 7-tap Hilbert Transform. The coefficients are optimized for market cycles (10-40 bars) to minimize passband ripple.

The Hilbert Transform coefficients are adjusted dynamically based on the dominant cycle period. The adjustment factors $0.075$ and $0.54$ are empirical constants derived by Ehlers to tune the Hilbert Transform for the expected range of market cycles (typically 10-40 bars).

$$ \text{Adj} = 0.075 \cdot \text{Period}_{t-1} + 0.54 $$

$$ \text{Detrender}_t = \left( \frac{5}{52} S_t + \frac{15}{26} S_{t-2} - \frac{15}{26} S_{t-4} - \frac{5}{52} S_{t-6} \right) \cdot \text{Adj} $$

$$ Q_t = \left( \frac{5}{52} D_t + \frac{15}{26} D_{t-2} - \frac{15}{26} D_{t-4} - \frac{5}{52} D_{t-6} \right) \cdot \text{Adj} $$

$$ I_t = D_{t-3} $$

### 3. Phasor Advancement & Homodyne Discriminator

The I and Q components are advanced by 90 degrees using another Hilbert Transform pass. The phasor components are then smoothed and cross-multiplied to extract period information.

$$ jI_t = \left( \frac{5}{52} I_t + \frac{15}{26} I_{t-2} - \frac{15}{26} I_{t-4} - \frac{5}{52} I_{t-6} \right) \cdot \text{Adj} $$

$$ jQ_t = \left( \frac{5}{52} Q_t + \frac{15}{26} Q_{t-2} - \frac{15}{26} Q_{t-4} - \frac{5}{52} Q_{t-6} \right) \cdot \text{Adj} $$

$$ I2_t = I_t - jQ_t $$

$$ Q2_t = Q_t + jI_t $$

These are smoothed exponentially:

$$ I2_t = 0.2 \cdot I2_t + 0.8 \cdot I2_{t-1} $$

$$ Q2_t = 0.2 \cdot Q2_t + 0.8 \cdot Q2_{t-1} $$

The homodyne discriminator extracts phase rate of change:

$$ \text{Re}_t = (I2_t \cdot I2_{t-1}) + (Q2_t \cdot Q2_{t-1}) $$

$$ \text{Im}_t = (I2_t \cdot Q2_{t-1}) - (Q2_t \cdot I2_{t-1}) $$

These are also smoothed:

$$ \text{Re}_t = 0.2 \cdot \text{Re}_t + 0.8 \cdot \text{Re}_{t-1} $$

$$ \text{Im}_t = 0.2 \cdot \text{Im}_t + 0.8 \cdot \text{Im}_{t-1} $$

The instantaneous period is derived from the phase rate:

$$ \text{Period}_t = \frac{2\pi}{\arctan\left(\frac{\text{Im}_t}{\text{Re}_t}\right)} $$

Period is constrained to [6, 50] bars and rate-limited to prevent erratic jumps (±50% max change per bar), then smoothed:

$$ \text{Period}_t = 0.2 \cdot \text{Period}_t + 0.8 \cdot \text{Period}_{t-1} $$

### 4. Adaptive Alpha Calculation

The phase angle is computed from the I1 and Q1 components:

$$ \text{Phase}_t = \arctan\left(\frac{Q_t}{I_t}\right) $$

The signed phase difference drives the adaptive behavior. Ehlers designed this with an asymmetric clamp: negative deltas (phase advancing, which is theoretically impossible in a stable cycle) get clamped to a minimum. This forces MAMA to respond quickly when the cycle model breaks down.

$$ \Delta\text{Phase} = \max(\text{Phase}_{t-1} - \text{Phase}_t, \text{MinDelta}) $$

In Ehlers' original TradeStation code, `MinDelta = 1` degree. Converting to radians: `MinDelta = π/180 ≈ 0.01745`.

The smoothing factor $\alpha$ is inversely proportional to the phase delta:

$$ \alpha = \frac{\text{FastLimit}}{\Delta\text{Phase}} $$

$$ \alpha = \max(\text{SlowLimit}, \min(\text{FastLimit}, \alpha)) $$

### 5. MAMA & FAMA Calculation

MAMA is an adaptive EMA using the calculated $\alpha$. FAMA uses half the alpha for slower confirmation.

$$ \text{MAMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{MAMA}_{t-1} $$

$$ \text{FAMA}_t = 0.5\alpha \cdot \text{MAMA}_t + (1 - 0.5\alpha) \cdot \text{FAMA}_{t-1} $$

## Mathematical Precision & Implementation Philosophy

QuanTAlib's MAMA differs from every other implementation in circulation. Not because we wanted to be clever. Because we read the original paper, transcribed the EasyLanguage code by hand, and noticed that TradeStation returns arctangent *in degrees*, while C#'s `Math.Atan` returns radians.

Most libraries ported Ehlers' numbers blindly. TA-Lib hardcodes `a = 0.0962` and `b = 0.5769`. But Ehlers' EasyLanguage code shows these as `5/52` and `15/26`. The difference is about 0.04% per coefficient, which compounds over long recursive smoothing runs. QuanTAlib uses the exact fractions instead.

An earlier revision of this indicator also replaced Ehlers' `atan(Q/I)` phase discriminator with `atan2(Q, I)` plus angle-wrapping, believing that was a "more correct" quadrant-aware version. It was not: TA-Lib and Skender.Stock.Indicators independently agree with each other to ~1e-11, because both implement Ehlers' plain `atan` discriminator (including its sign-driven asymmetric clamp on the phase delta). The `atan2` variant broke that clamp and diverged from all reference libraries by up to 11%. QuanTAlib now uses the same `atan(Q/I)` formulation as Ehlers/TA-Lib/Skender.

### Precision Improvements

| Aspect                   | Other Libraries                | QuanTAlib               | Rationale                                   |
| :----------------------- | :----------------------------- | :---------------------- | :------------------------------------------ |
| **Hilbert Coefficients** | `0.0962`, `0.5769`             | `5.0/52.0`, `15.0/26.0` | Exact fractions avoid rounding accumulation |
| **Adjustment Slope**     | `0.075`                        | `3.0/40.0`              | Preserves rational arithmetic precision     |
| **Adjustment Intercept** | `0.54`                         | `27.0/50.0`             | Ditto                                       |
| **Arctangent Function**  | `atan(y/x)` + zero-check       | `atan(y/x)` + zero-check | Matches Ehlers' original discriminator (same as TA-Lib/Skender) |
| **Minimum Delta**        | `1.0` degree                   | `1.0` degree             | Matches Ehlers' asymmetric clamp on phase delta |

### The Atan Discriminator

Ehlers used `atan(Q/I)` (single-quadrant, with a zero-check on `I`) together with a **signed, unwrapped** phase delta: `DeltaPhase = Phase[t-1] - Phase[t]`, floored at 1 degree (never taking an absolute value). When the discriminator's quadrant flips, that signed delta swings sharply negative, which forces `alpha` all the way up to `FastLimit` for one bar. This asymmetric "snap to full speed" behavior on quadrant flips is a deliberate part of Ehlers' design — it's how MAMA re-acquires the cycle quickly when the phase model breaks down.

An `atan2(Q, I)` + phase-wrapping formulation (normalizing the delta back into `[-π, π]`) looks more "mathematically correct" but it removes exactly this snap behavior, since wrapping keeps the delta small and positive across quadrant flips instead of letting it spike negative. That single change was enough to make QuanTAlib disagree with TA-Lib and Skender by as much as 11%, even though both of those libraries agree with each other to ~1e-11. QuanTAlib now uses the same `atan(Q/I)` + signed-delta formulation as Ehlers/TA-Lib/Skender:

```csharp
// Phase calculation (degrees, matches TA-Lib atan(Q1/I1), not atan2)
_state.Phase = i1 != 0.0 ? Math.Atan(q1 / i1) * RadToDeg : 0.0;

// Signed delta, no wrapping: quadrant flips intentionally spike alpha to FastLimit
double deltaPhase = _p_state.Phase - _state.Phase;
if (deltaPhase < 1.0) { deltaPhase = 1.0; }
double alpha = deltaPhase > 1.0 ? Math.Max(_fastLimit / deltaPhase, _slowLimit) : _fastLimit;
```

The period discriminator follows the same rule: `Period = 360 / atan(Im/Re)` (degrees, no absolute value). A negative result is not an error — it simply falls outside `[periodFloor, periodCap]` and gets clamped back into range by the existing bounds check, exactly as in TA-Lib.

### Convergence with Other Libraries

With the atan-based discriminator, QuanTAlib tracks TA-Lib and Skender to within ~1e-2 absolute at steady state (bars 100+), on price series in the low thousands. The residual comes from warmup/priming differences — TA-Lib primes its Hilbert Transform state with a 32-bar WMA-based unstable period, while QuanTAlib uses a running average of the first 6 bars — not from a discriminator mismatch. MAMA/FAMA crossovers match essentially all of the time.

### Initialization Philosophy

Ehlers' original paper initializes MAMA and FAMA to zero. This causes massive convergence errors for the first 100-300 bars. Skender initializes to the 6-bar SMA. We initialize to the running average of the first 6 bars:

```csharp
if (_state.Index <= 6)
{
    _state.SumPr += price;
    double avg = _state.Index > 0 ? _state.SumPr / _state.Index : price;
    _state.Mama = avg;
    _state.Fama = avg;
}
```

This reduces early-period error by ~90% compared to zero-initialization while maintaining the spirit of Ehlers' design. After 250+ bars, all methods converge.

## Performance Profile

MAMA is computationally intensive. Each bar requires four Hilbert Transform passes, two exponential smoothings, three arctangent calculations, and careful state management. The payoff is cycle-adaptive behavior that no simple moving average can match.

### Operation Count (Streaming Mode, Scalar)

**Hot path (after warmup, bars > 6):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 28 | 1 | 28 |
| MUL | 24 | 3 | 72 |
| FMA | 8 | 4 | 32 |
| DIV | 1 | 15 | 15 |
| ATAN | 3 | 45 | 135 |
| CMP/CLAMP | 8 | 1 | 8 |
| **Total** | **72** | — | **~290 cycles** |

The hot path consists of:
1. Pre-smoothing (4-tap FIR): 4 MUL + 3 ADD — 15 cycles
2. Detrender Hilbert: 4 MUL + 3 ADD/SUB — 15 cycles
3. Q1 Hilbert: 4 MUL + 3 ADD/SUB — 15 cycles
4. jI/jQ Hilbert: 8 MUL + 6 ADD/SUB — 30 cycles
5. Phasor addition: 2 ADD/SUB — 2 cycles
6. I2/Q2 smoothing: 2 FMA — 8 cycles
7. Homodyne discriminator (Re/Im): 2 FMA + 2 MUL + 2 ADD/SUB — 22 cycles
8. Re/Im smoothing: 2 FMA — 8 cycles
9. Period calculation: 1 ATAN + 1 DIV + 4 CMP — 64 cycles
10. Period smoothing: 1 FMA — 4 cycles
11. Phase calculation: 1 ATAN — 45 cycles
12. Alpha calculation: 3 CMP + 1 DIV — 18 cycles
13. MAMA/FAMA update: 2 MUL + 2 ADD/SUB — 8 cycles

**Warmup path (bars ≤ 6):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD | 1 | 1 | 1 |
| DIV | 1 | 15 | 15 |
| **Total** | **2** | — | **~16 cycles** |

### Batch Mode (SIMD Analysis)

MAMA is an IIR filter with complex phase state — **not vectorizable** across bars due to:
1. Recursive smoothing dependencies (I2, Q2, Re, Im, Period)
2. ATAN calls with data-dependent branching
3. Phase delta calculation requiring previous state

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | ~16 cycles saved (8 FMA vs 16 MUL+ADD) |
| Bitwise AND masking | ~2x faster than modulo for buffer indexing |
| stackalloc buffers | Zero heap allocation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Mathematically superior to all other implementations |
| **Timeliness** | 9/10 | Extremely fast response to phase shifts |
| **Overshoot** | 6/10 | Can overshoot on sudden cycle changes |
| **Smoothness** | 6/10 | Can be stepped/jagged in transitions |

Buffer indexing uses bitwise AND masking (`(idx - n) & 7`) instead of modulo for ~2x speed. All state variables (I2, Q2, Re, Im, period, phase) are scalars on the stack. No heap allocations. No GC pressure.

The batch `Calculate` method processes entire arrays in ~180 nanoseconds per bar on a Ryzen 9950X (AVX2, Turbo enabled).

## Validation

Validated against Skender, TA-Lib and Ooples, all within an absolute tolerance of 0.01 at steady state (bars 100+).

| Library       | Status | Notes                                                                                   |
| :------------ | :----- | :--------------------------------------------------------------------------------------- |
| **QuanTAlib** | ✅      | Same `atan(Q/I)` phase discriminator as Ehlers/TA-Lib/Skender                             |
| **Skender**   | ✔️      | Matches within 0.01 absolute at steady state; residual from warmup/priming differences   |
| **Ooples**    | ✔️      | Matches within 0.01 absolute at steady state; Ooples also uses truncated 4-decimal constants |
| **TA-Lib**    | ✔️      | Matches within 0.01 absolute at steady state; residual from WMA-based 32-bar unstable period vs QuanTAlib's 6-bar average warmup |
| **Tulip**     | N/A    | Not implemented                                                                          |

QuanTAlib still uses exact fractions (`5/52`, `15/26`) instead of TA-Lib's truncated decimals (`0.0962`, `0.5769`), which avoids coefficient rounding error compounding over long runs. That is a genuine, if small, precision improvement — but it is independent of, and much smaller than, the phase-discriminator bug this indicator previously had.

## Usage Guidelines

### When to Use

MAMA excels in specific market conditions where its cycle-adaptive nature provides an edge:

- **Trending markets with regular cycles**: Equities, forex pairs, and futures that exhibit measurable cyclical behavior (10-40 bar dominant cycles)
- **Swing trading timeframes**: Daily, 4-hour, and hourly charts where cycle periods have time to develop. Intraday scalping on 1-minute charts rarely has clean cycles for MAMA to lock onto.
- **Mean-reversion strategies**: The MAMA/FAMA crossover signals work well for identifying cycle turning points
- **Adaptive position sizing**: Use the alpha value directly as a confidence metric—high alpha means uncertainty, reduce position size
- **Trend confirmation**: MAMA below FAMA confirms bearish bias; MAMA above FAMA confirms bullish bias

### Limitations

MAMA has specific weaknesses that practitioners must understand:

- **White noise markets**: When no dominant cycle exists, MAMA's phase calculations become erratic. This happens in low-volume periods, news-driven spikes, and highly efficient markets.
- **Very short timeframes**: Sub-minute charts rarely have the 10-40 bar cycles MAMA expects. The Hilbert Transform needs at least 6-7 bars of clean data to produce meaningful phase estimates.
- **Sudden regime changes**: Flash crashes, gap openings, and news events bypass MAMA's cycle model entirely. The indicator will catch up, but with lag.
- **Cryptocurrency markets**: 24/7 trading with no session structure often lacks the cyclical patterns MAMA was designed to exploit.
- **Illiquid instruments**: Low-volume stocks and exotic derivatives produce noisy price data that corrupts the Hilbert Transform.

### Recommended Complements

MAMA works best when combined with indicators that cover its blind spots:

| Complement | Purpose | Why It Helps |
| :--- | :--- | :--- |
| **ADX/DMI** | Trend strength | Filters out ranging markets where MAMA whipsaws |
| **ATR** | Volatility context | Position sizing and stop placement during high-alpha periods |
| **Dominant Cycle Period** | Cycle existence | Ehlers' DCE or similar confirms a cycle exists before trusting MAMA |
| **Volume Profile** | Market structure | Identifies support/resistance that may interrupt cycles |
| **RSI/Stochastic** | Overbought/oversold | Confirms cycle turning points at MAMA/FAMA crossovers |

**Recommended setup**: Use ADX > 20 as a trend filter. Only take MAMA/FAMA crossovers when a dominant cycle is present (DCE confidence > 0.5). Scale position size inversely with MAMA's alpha.

### Common Pitfalls

1. **Crossover Signal Misuse**: The MAMA/FAMA crossover is the primary signal. MAMA crossing above FAMA is bullish. Crossing below is bearish. This is more reliable than a single MA because FAMA acts as confirmation. However, don't trade every crossover—filter with trend strength indicators.

2. **Parameter Tuning Mistakes**: `FastLimit` (default 0.5) controls maximum responsiveness. Higher = faster but choppier. `SlowLimit` (default 0.05) sets minimum smoothing. Lower = smoother but laggier. The 10:1 ratio is Ehlers' recommendation. Don't mess with it unless you understand phase rate of change dynamics.

3. **Whipsaws in Ranging Markets**: MAMA adapts to cycle period, not cycle *existence*. In white noise (no dominant cycle), phase measurements become erratic. MAMA will chop between fast and slow, generating false signals. Use a cycle strength indicator (like Ehlers' Hilbert Transform Dominant Cycle Period SNR) to filter.

4. **Initialization Bias**: The first 50-100 bars are unreliable. MAMA needs time for the Hilbert Transform to stabilize and for period estimates to converge. Always discard or ignore the first `WarmupPeriod` (set to 50 for safety).

5. **Precision Expectations**: QuanTAlib tracks TA-Lib and Skender within ~0.01 absolute at steady state, but won't match them to the sixth decimal. The residual comes from warmup/priming differences (TA-Lib primes with a 32-bar WMA-based unstable period; QuanTAlib uses a 6-bar running average), not from the phase discriminator. If this breaks your backtests, the backtests were fragile.

6. **Ignoring the Alpha Output**: Many traders only look at MAMA and FAMA values. The adaptive alpha itself is valuable information—it tells you how confident MAMA is in its cycle estimate. High alpha (near FastLimit) means rapid phase change and uncertainty. Low alpha (near SlowLimit) means stable, established trend.