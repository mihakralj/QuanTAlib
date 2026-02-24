# HA: Heikin-Ashi

> "The trend is your friend — but only if the noise doesn't make you abandon it at the first bump." — Every trader, eventually

HA transforms standard OHLC bars into smoothed Heikin-Ashi candles by averaging each component with its predecessor. The Close is the bar's four-price mean $(O+H+L+C)/4$, the Open is a recursive midpoint of the prior HA Open and HA Close, and High/Low are clamped extremes that guarantee the HA body always fits inside the HA wick. Unlike most indicators that reduce a bar to a single scalar, HA outputs a complete `TBar` — four smoothed prices per bar — making it a bar-to-bar transform rather than a bar-to-value reduction. The recursive Open gives HA an IIR character: each bar carries a decaying memory of the entire price history, which is what flattens trend noise but also why HA prices do not match any actual traded price.

## Historical Context

Heikin-Ashi (平均足, literally "average bar") is a Japanese charting technique that predates modern computing. The method gained widespread adoption in Western markets after Steve Nison introduced Japanese candlestick charting in the early 1990s, though Heikin-Ashi itself was popularized separately by Dan Valcu in a 2004 *Technical Analysis of Stocks & Commodities* article. The technique did not originate in academic quantitative finance; it emerged from the practitioner tradition of visually simplifying price action to identify trends.

The transformation is sometimes confused with a moving average, but the mechanics differ. A moving average produces a single smoothed value from a rolling window of N bars. Heikin-Ashi produces four smoothed values (O, H, L, C) using no window — the smoothing comes entirely from the recursive Open, which is a first-order IIR filter with $\alpha = 0.5$. This makes HA closer to an EMA(2) applied to the Open channel than to any FIR filter. The Close channel ($\text{OHLC4}$) is identical to `AVGPRICE` — it carries no memory between bars.

A persistent source of confusion across platforms: TradingView's `ticker.heikinashi()` function applies the transform at the data-feed level, meaning all built-in variables (`open`, `high`, `low`, `close`) become HA values. Indicators computed on HA data produce doubly-smoothed results that do not match the same indicator on standard data. QuanTAlib applies HA as an explicit indicator, keeping the standard data pipeline intact and the smoothing auditable.

## Architecture & Physics

### 1. HA Close (Stateless)

$$\text{HA\_Close}_t = \frac{O_t + H_t + L_t + C_t}{4}$$

This is identical to `AVGPRICE` / `OHLC4`. No inter-bar dependency. Implemented as FMA:

$$\text{HA\_Close}_t = \text{FMA}(O_t + H_t,\; 0.25,\; (L_t + C_t) \times 0.25)$$

### 2. HA Open (Recursive IIR)

$$\text{HA\_Open}_t = \frac{\text{HA\_Open}_{t-1} + \text{HA\_Close}_{t-1}}{2}$$

Seed on the first bar:

$$\text{HA\_Open}_0 = \frac{O_0 + C_0}{2}$$

This is a first-order IIR filter with $\alpha = 0.5$ and $\beta = 0.5$, giving it an effective half-life of 1 bar and exponential memory decay. The recursive structure means HA_Open carries the entire price history with geometrically decaying weights — it never fully forgets, but contributions older than ~7 bars contribute less than 1% each.

### 3. HA High (Clamped Maximum)

$$\text{HA\_High}_t = \max(H_t,\; \text{HA\_Open}_t,\; \text{HA\_Close}_t)$$

Guarantees the wick extends above the body. In strong uptrends where the actual High exceeds both HA Open and HA Close, the HA High equals the real High.

### 4. HA Low (Clamped Minimum)

$$\text{HA\_Low}_t = \min(L_t,\; \text{HA\_Open}_t,\; \text{HA\_Close}_t)$$

Guarantees the wick extends below the body. In strong downtrends where the actual Low is below both HA Open and HA Close, the HA Low equals the real Low.

### 5. Output Structure

Unlike standard indicators that output a `TValue` (timestamp + double), HA outputs a `TBar`:

```
TBar(Time, HA_Open, HA_High, HA_Low, HA_Close, Volume)
```

Volume passes through untransformed.

### 6. Complexity

$O(1)$ per bar. One FMA + one multiplication + two comparisons (max/min). State: two doubles (previous HA_Open and HA_Close). No buffers, no lookback window.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

### IIR Transfer Function

The HA Open channel is a first-order IIR filter on the midpoint of (HA_Open, HA_Close):

$$H(z) = \frac{0.5}{1 - 0.5z^{-1}}$$

This yields an exponential impulse response with decay factor $\beta = 0.5$ per bar:

$$h[n] = 0.5^{n+1}, \quad n \geq 0$$

Half-life: $t_{1/2} = \frac{-\ln 2}{\ln 0.5} = 1$ bar.

### Warmup Period

$$\text{WarmupPeriod} = 1$$

HA is "hot" from bar 1. The seed bar uses $(O_0 + C_0)/2$ for HA_Open and produces valid output immediately. The recursive filter converges rapidly due to the $\beta = 0.5$ decay — after 7 bars, the contribution of the seed value is less than 0.4%.

### Pseudo-code

```
function HA(bar, prevHaOpen, prevHaClose):
    o, h, l, c ← bar.Open, bar.High, bar.Low, bar.Close

    // Substitute last-valid for non-finite inputs
    if !finite(o): o ← lastValidOpen
    if !finite(h): h ← lastValidHigh
    if !finite(l): l ← lastValidLow
    if !finite(c): c ← lastValidClose

    haClose ← FMA(o + h, 0.25, (l + c) × 0.25)

    if firstBar:
        haOpen ← (o + c) × 0.5
    else:
        haOpen ← (prevHaOpen + prevHaClose) × 0.5

    haHigh ← max(h, haOpen, haClose)
    haLow  ← min(l, haOpen, haClose)

    return TBar(bar.Time, haOpen, haHigh, haLow, haClose, bar.Volume)
```

### Output Interpretation

| Candle Pattern | Meaning |
|----------------|---------|
| Green body, no lower wick | Strong uptrend |
| Red body, no upper wick | Strong downtrend |
| Small body, both wicks | Indecision / potential reversal |
| Increasing body size | Trend acceleration |
| Decreasing body size | Trend deceleration |

## Interpretation and Signals

### Signal Patterns

- **Wickless candles**: An HA candle with no lower wick (uptrend) or no upper wick (downtrend) signals strong directional momentum. Three or more consecutive wickless candles in one direction is a high-confidence trend signal.
- **Doji / spinning top**: Small HA bodies with wicks on both sides indicate weakening momentum and potential reversal. The smaller the body relative to the wicks, the stronger the indecision signal.
- **Color change**: A transition from red to green (or vice versa) after a series of same-colored candles signals trend reversal. Confirmation from volume or a secondary indicator reduces false signals.
- **Body size sequence**: Monotonically increasing HA body sizes indicate trend acceleration; decreasing sizes indicate exhaustion.

### Practical Notes

HA candles should never be used for precise entry/exit pricing because HA Open and HA Close are synthetic — they do not correspond to any traded price. Use HA for trend direction and standard candles for execution levels. Combining HA trend direction with a momentum oscillator (RSI, CCI) on standard data provides trend-filtered signals without the double-smoothing problem.

## Quality Metrics

| Metric | Score | Notes |
|--------|:-----:|-------|
| **Accuracy** | 7/10 | HA Close = OHLC4 (exact); HA Open drifts from real prices due to recursion |
| **Timeliness** | 8/10 | Only 1-bar effective lag from IIR Open; responds quickly to trend changes |
| **Overshoot** | 10/10 | High/Low clamping guarantees HA range ⊆ real range on High/Low channels |
| **Smoothness** | 8/10 | IIR Open provides consistent smoothing; Close is unsmoothed (bar-local) |

## Related Indicators

- **[AVGPRICE](../avgprice/Avgprice.md)**: HA_Close is identical to AVGPRICE. If you only need the average price per bar, AVGPRICE avoids the recursive state overhead.
- **[EMA](../../trends_IIR/ema/Ema.md)**: HA_Open is effectively EMA(2) on the midpoint stream. For single-value smoothing with configurable responsiveness, EMA offers more control.
- **[MEDPRICE](../medprice/Medprice.md)**: Uses (H+L)/2 — HA's seed value on bar 0 uses (O+C)/2 instead, weighting session boundaries over extremes.

## Validation

Validated against external libraries in `Ha.Validation.Tests.cs`. HA is widely implemented; cross-validation is straightforward since the formula has no ambiguity.

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | ? | ? | ? | No direct `TA_HA` function; requires manual OHLC transform |
| **Skender** | ? | ? | ? | `GetHeikinAshi()` returns OHLC results |
| **Tulip** | ? | ? | ? | No Heikin-Ashi function |
| **Ooples** | ? | ? | ? | `GetHeikinAshi()` |

## Performance Profile

### Key Optimizations

- **FMA usage**: HA_Close uses `Math.FusedMultiplyAdd(o + h, 0.25, (l + c) * 0.25)` — single instruction for the four-price average.
- **Multiplication over division**: `× 0.5` and `× 0.25` replace `/2` and `/4`.
- **No buffer**: Only two doubles of state (previous HA_Open, previous HA_Close). No `RingBuffer` or history required.
- **Aggressive inlining**: `Update` method decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| ADD (O+H) | 1 | 1 | 1 |
| ADD (L+C) | 1 | 1 | 1 |
| MUL ((L+C) × 0.25) | 1 | 3 | 3 |
| FMA (haClose) | 1 | 4 | 4 |
| ADD (prevHaOpen + prevHaClose) | 1 | 1 | 1 |
| MUL (× 0.5) | 1 | 3 | 3 |
| MAX (3-way) | 2 | 1 | 2 |
| MIN (3-way) | 2 | 1 | 2 |
| **Total (hot)** | **10** | | **~17 cycles** |

### SIMD Analysis (Batch Mode)

| Aspect | Assessment |
|--------|------------|
| HA_Close | Fully vectorizable (element-wise OHLC4) |
| HA_Open | Sequential — IIR dependency blocks vectorization |
| HA_High/Low | Vectorizable after Open/Close are computed |
| Strategy | Vectorize Close in pass 1, scalar Open in pass 2, vectorize High/Low in pass 3 |

## Common Pitfalls

1. **Synthetic prices**: HA Open and HA Close do not correspond to any actual traded price. Using HA values for order placement or stop-loss levels produces fills at non-real prices. Always use standard OHLC for execution.

2. **Double smoothing**: Applying indicators (RSI, MACD, etc.) to HA data instead of standard data produces doubly-smoothed results with increased lag and reduced sensitivity. This is the single most common misuse of Heikin-Ashi.

3. **Backtesting on HA data**: Strategies backtested on HA candles show artificially smooth equity curves because the smoothed prices overstate trend persistence. Results do not replicate on live standard-data execution.

4. **Volume passthrough**: HA transforms only prices. Volume is unchanged. Interpreting HA candle patterns without checking whether volume confirms the signal leads to false trend readings.

5. **Seed sensitivity**: The first bar's HA_Open seed $(O_0 + C_0)/2$ affects all subsequent HA_Open values. Different start dates produce different HA series for the same instrument. The impact decays as $0.5^n$ — after 10 bars the seed contributes less than 0.1%.

6. **Gap handling**: Real gaps (overnight, weekend) produce HA_Open values that split the difference between the gap ends. This is by design (smoothing), but users expecting gap preservation will be surprised. The actual High and Low still reflect the real extremes via the max/min clamping.

7. **No parameters**: Unlike most indicators, HA has no configurable period or smoothing factor. The $\alpha = 0.5$ is fixed. Users wanting adjustable smoothing should consider applying an EMA or other moving average to standard OHLC data instead.

## References

- **Valcu, D.** (2004). "Using The Heikin-Ashi Technique." *Technical Analysis of Stocks & Commodities*, Vol. 22, No. 2.
- **Nison, S.** (1991). *Japanese Candlestick Charting Techniques*. New York Institute of Finance.
- **Vervoort, S.** (2008). "Smoothing Heikin-Ashi." *Technical Analysis of Stocks & Commodities*.
- [Investopedia: Heikin-Ashi](https://www.investopedia.com/terms/h/heikinashi.asp) — accessible introduction to the technique and its trading applications.
