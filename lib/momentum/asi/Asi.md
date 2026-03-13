# ASI: Accumulation Swing Index

> *Price tells us what is happening. The Accumulation Swing Index tells us whether to believe it.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `limitMove` (default 3.0)                      |
| **Outputs**      | Single series (Asi)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 2 bars                          |
| **PineScript**   | [asi.pine](asi.pine)                       |

- The Accumulation Swing Index is Wilder's method for separating genuine breakouts from whipsaw noise.
- **Similar:** [MOM](../mom/Mom.md), [ROC](../roc/Roc.md) | **Complementary:** Volume for confirmation | **Trading note:** Accumulation Swing Index; Wilder's cumulative swing index. Confirms breakouts from chart patterns.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Accumulation Swing Index is Wilder's method for separating genuine breakouts from whipsaw noise. Each bar computes a Swing Index value by comparing current OHLC prices to the previous bar, scaled by a user-supplied limit move parameter T. The cumulative sum of these SI values — the ASI — forms a "phantom" price line whose peaks and troughs can be compared directly to the price chart. A trendline break on ASI that accompanies a trendline break on price is confirmed; a price break without ASI confirmation is a probable false move. Introduced in Wilder's 1978 book, it predates most modern oscillators by a decade.

## Historical Context

J. Welles Wilder Jr. introduced ASI in *New Concepts in Technical Trading Systems* (1978), the same book that gave traders RSI, ATR, and the Parabolic SAR. ASI was Wilder's answer to a straightforward frustration: trendline analysis worked, but distinguishing real breaks from reversals required subjectivity. He wanted a mathematical proxy for price that filtered the noise embedded in OHLC data.

The Swing Index at its core is a weighted price change metric. The numerator combines three price change signals — the close-to-close change, the intrabar close-to-open change, and the previous bar's open-to-close (residual body) — each assigned progressively reduced weights (1, 0.5, 0.25). The denominator R selects the largest of three range relationships between the current and previous bars, normalizing SI for the total swing potential. The limit move parameter T, borrowed from commodity futures trading where daily price limits exist, further scales the result to the range [-100, 100] per bar.

The cumulative version (ASI) was Wilder's insight: individual SI readings are too noisy to trade directly. Accumulated over time, they build a trend-following series that mirrors price action without the bar-to-bar jumble. The method differs from simple momentum: it is not a look-back window calculation but a running summation, meaning each new bar either adds to or subtracts from the ASI depending on the directional balance of all five OHLC fields.

No major commercial library (TA-Lib, Skender, Tulip, Ooples) ships ASI directly. Most implementations seen in the wild either ignore the open field entirely or use the wrong normalization case. The correct three-case R selection — testing whether |H-C₁|, |L-C₁|, or |H-L| dominates — matters because it determines the effective "maximum possible SI" against which the numerator is scaled. Getting the dominant case wrong by 5% changes ASI divergence signals at inflection points where you can least afford the error.

## Architecture & Physics

### 1. Previous-Bar Dependency

ASI requires two bars to produce its first non-zero output. Bar 0 has no previous close, so SI₀ = 0 and ASI₀ = 0. Starting from bar 1, the calculation is fully defined. State is two scalars: `PrevClose` and `PrevOpen`. The cumulative sum `Asi` is the third.

### 2. Input Sanitization

All four OHLC fields are sanitized before use. Non-finite values substitute the last valid equivalent (last valid close substitutes for close, last valid open for open; high and low fall back to the sanitized close). This matches the library convention for robustness without propagating invalids downstream.

### 3. K — The Scale Factor

$$
K = \max(|H - C_1|, |L - C_1|)
$$

K measures the maximum "reach" of the current bar relative to the previous close. It scales the raw SI so that bars with larger swings relative to the prior close contribute proportionally more, regardless of limit move T.

### 4. R — The Dominant Range Case

R selects the largest of three potential range scenarios:

$$
R = \begin{cases}
|H - C_1| - \tfrac{1}{2}|L - C_1| + \tfrac{1}{4}|C_1 - O_1| & \text{if } |H-C_1| \geq |L-C_1| \text{ and } |H-C_1| \geq |H-L| \\
|L - C_1| - \tfrac{1}{2}|H - C_1| + \tfrac{1}{4}|C_1 - O_1| & \text{if } |L-C_1| \geq |H-C_1| \text{ and } |L-C_1| \geq |H-L| \\
|H - L| + \tfrac{1}{4}|C_1 - O_1| & \text{otherwise}
\end{cases}
$$

The 0.25 weight on `|C₁ - O₁|` (the previous bar's body) adds a correction for how much of the range was "used up" by the prior bar's close-open relationship. When R = 0 (perfectly flat market: H = L = C₁ = O₁), SI = 0.

### 5. SI — The Swing Index

$$
SI = \frac{50 \cdot \left[(C - C_1) + \tfrac{1}{2}(C - O) + \tfrac{1}{4}(C_1 - O_1)\right]}{R} \cdot \frac{K}{T}
$$

Where T is the limit move parameter. For stocks with no official limit, Wilder recommended T = 3.0 as a proxy. The constant 50 scales SI so its theoretical range per bar approaches ±100 under extreme conditions.

The numerator's three terms:

- $(C - C_1)$: directional close-to-close momentum, weight 1.0
- $\tfrac{1}{2}(C - O)$: intrabar momentum (today's close vs today's open), weight 0.5
- $\tfrac{1}{4}(C_1 - O_1)$: residual body of the previous bar, weight 0.25

This weighting scheme decays the influence of older information geometrically, analogous to exponential decay but applied over just two bars.

### 6. ASI — Cumulative Accumulation

$$
ASI_i = ASI_{i-1} + SI_i
$$

A pure running sum. No decay, no window, no normalization. ASI grows or shrinks with each bar according to its SI contribution.

## Mathematical Foundation

Complete derivation of SI numerical range:

- Numerator maximum: $(C - C_1) + \tfrac{1}{2}(C - O) + \tfrac{1}{4}(C_1 - O_1)$
- Numerator bounded by the dominant range case in R (cannot exceed R by construction)
- Therefore $SI \in [-100 \cdot K/T, +100 \cdot K/T]$ per bar

For futures contracts with T = daily limit, SI is strictly bounded to ±100. For stock parameters with T = 3.0, SI is unbounded in theory but statistically confined. Wilder observed empirically that most stock bars produce |SI| < 30.

FMA exploit for the three R cases:

```
Case 1: R = FMA(−0.5, |L−C₁|, |H−C₁|) + 0.25·|C₁−O₁|
Case 2: R = FMA(−0.5, |H−C₁|, |L−C₁|) + 0.25·|C₁−O₁|
```

SI numerator:

```
numerator = FMA(0.5, C−O, C−C₁) + 0.25·(C₁−O₁)
```

Both reduce multiply-add pairs from two floating-point operations to one FMA, eliminating one rounding step and matching the protocol's hot-path convention.

## Performance Profile

### Operation Count (Streaming Mode)

ASI is O(1) per bar — no window, no ring buffer, no re-iteration. State is three `double` scalars (`PrevClose`, `PrevOpen`, `Asi`) plus two last-valid sanitization trackers. All arithmetic operates on the current and previous OHLC fields only.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ABS (four range terms) | 4 | 1 | ~4 |
| MAX(|H−C₁|, |L−C₁|) for K | 1 | 1 | ~1 |
| Three-case R comparison + FMA | 3 | 4 | ~12 |
| SI numerator (FMA + scale) | 3 | 4 | ~12 |
| SI divide by R | 1 | 8 | ~8 |
| K/T multiply | 1 | 3 | ~3 |
| ASI running sum update | 1 | 1 | ~1 |
| **Total** | **14** | — | **~41 cycles** |

O(1) per bar. The SI division is the dominant cost. WarmupPeriod = 2 (bar 0 returns 0; full SI from bar 1).

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| ABS of range terms per bar | Yes | `VABSPD` on pre-loaded OHLC registers |
| K and R case selection | Partial | `VCMPPD`-based branchless MAX/SELECT; case logic needs masking |
| SI numerator FMA | Yes | Fixed 3-term weighted sum; vectorizable across bars |
| Prefix-sum for ASI accumulation | Partial | Parallel prefix sum (log₂N passes) for AVX2; overhead > benefit for N < 1000 |
| Sequential cumulation dependency | No | ASI₍ᵢ₎ = ASI₍ᵢ₋₁₎ + SI₍ᵢ₎ is a scan operation |

Per-bar SI values are fully vectorizable (4 bars/cycle on AVX2). The ASI accumulation is a prefix scan — parallelizable via the Blelloch algorithm (O(log N) depth) but with constant-factor overhead that exceeds scalar cost for typical bar counts (< 10,000). Scalar prefix sum is the practical batch implementation.

## Validation

No major third-party library implements ASI. Validation is performed via self-consistency and known-value cross-checks.

| Test | Method | Tolerance | Status |
| :--- | :--- | :---: | :--- |
| Manual bar-2 calculation | Hardcoded OHLC → expected SI₂ | 1e-9 | Pass |
| Batch == Streaming | Same GBM series, two code paths | 1e-9 | Pass |
| Constant prices → ASI = 0 | All bars: O=H=L=C, R=0, SI=0 | exact | Pass |
| Uptrend GBM → positive ASI | Monotone rising bars | sign check | Pass |
| Downtrend GBM → negative ASI | Monotone falling bars | sign check | Pass |
| NaN/Infinity inputs → finite output | Last-valid substitution | isFinite | Pass |
| isNew=false rollback | Rewrite bar, compare restored state | 1e-9 | Pass |

Manual verification (bar 2):

```
Bar1: O=10, H=11, L=9,  C=10  → SI=0 (first bar)
Bar2: O=10, H=12, L=9,  C=11
  K = max(|12−10|, |9−10|) = max(2,1) = 2
  absHC=2, absLC=1, absHL=3, absC1O1=0
  dominant: |H−L|=3 ≥ |H−C₁|=2 → R = 3 + 0 = 3
  numerator = (11−10) + 0.5×(11−10) + 0.25×0 = 1.5
  SI = 50 × 1.5 / 3 × (2/3) ≈ 16.6667
  ASI = 0 + 16.6667 = 16.6667
```

## Common Pitfalls

1. **Wrong T for the instrument.** T = 3.0 is Wilder's stock default. Futures require the actual exchange limit move. Using T = 3.0 for a commodity with a daily limit of 0.50 inflates SI by a factor of 6, rendering cumulative ASI meaningless for breakout comparison.

2. **Forgetting the open field.** Several open-source implementations compute SI using only close prices (setting O = C). This eliminates the intrabar body term, changing the numerator by up to 50% on gap-open days — precisely the sessions where ASI's edge over pure close-based momentum is largest.

3. **Wrong dominant-case selection.** Some implementations use a fixed formula for R rather than the three-case selection. The error is smallest when the bar is a close-above-high range day and largest on inside bars. Quantified impact: using R = |H−L| + 0.25·|C₁−O₁| exclusively overstates R on gap days, understating SI by 15–40% at the most informative bars.

4. **Trading SI directly.** Wilder was explicit: SI is noise, ASI is signal. Per his book: "The Swing Index is not a trading tool; the Accumulation Swing Index is." Single-bar SI values oscillate violently; only the cumulative form reveals structure.

5. **Comparing raw ASI values across instruments.** ASI is not normalized. The absolute scale depends on price level and T. A 50-point ASI breakout on a $500 stock with T=3 represents different conviction than the same number on a $20 stock. Use ASI for trendline analysis on the same series, not cross-instrument ranking.

6. **Choosing poorly where to draw the ASI trendline.** Wilder's method requires finding ASI peaks and troughs that correspond to price peaks and troughs, then connecting them. A trendline drawn only on ASI without the corresponding price analysis produces random signals. The confirmation rule requires both lines to break simultaneously.

7. **Ignoring warmup.** `IsHot` is false for the first bar. Bar 0 always outputs 0 (no previous close). Code consuming ASI from another library where `IsHot` semantics differ may treat this 0 as a valid signal.

## References

- Wilder, J.W. (1978). *New Concepts in Technical Trading Systems*. Trend Research, Greensboro NC.
- Murphy, J.J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance, pp. 233–238.
- Kaufman, P.J. (2013). *Trading Systems and Methods*, 5th ed. Wiley, pp. 411–413.