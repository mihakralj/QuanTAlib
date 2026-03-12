# PSAR: Parabolic Stop And Reverse

> *The trend is your friend until the end when it bends.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `afStart` (default 0.02), `afIncrement` (default 0.02), `afMax` (default 0.20)                      |
| **Outputs**      | Single series (Psar)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `1` bars                          |
| **PineScript**   | [psar.pine](psar.pine)                       |

- The Parabolic Stop And Reverse (PSAR) is a trend-following overlay indicator created by J.
- Parameterized by `afStart` (default 0.02), `afIncrement` (default 0.02), `afMax` (default 0.20).
- Output range: Varies (see docs).
- Requires `1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Introduction

The Parabolic Stop And Reverse (PSAR) is a trend-following overlay indicator created by J. Welles Wilder Jr. in 1978. It produces a trailing stop level that accelerates toward price as the trend extends, then flips to the opposite side when price crosses the stop. The acceleration mechanism is the key differentiator: SAR starts slow and tightens progressively, creating the characteristic parabolic curve that gives the indicator its name. Default parameters (0.02 start, 0.02 increment, 0.20 maximum) produce approximately 10–30 reversals per 500 bars on typical equity data.

## Historical Context

Wilder introduced PSAR alongside RSI, ATR, and ADX in *New Concepts in Technical Trading Systems* (1978). Unlike fixed-percentage trailing stops, PSAR uses an acceleration factor (AF) that increases each time price makes a new extreme in the trend direction, creating time-dependent tightening. This was novel for 1978: most trailing stops were static. The parabolic shape emerges because SAR converges on price at an accelerating rate, mathematically similar to a particle under constant acceleration. Most implementations today follow Wilder's original specification with minor variations in initialization logic (first-bar handling).

## Architecture and Physics

### 1. State Machine

PSAR operates as a two-state machine: **Long** (uptrend) and **Short** (downtrend). Each state tracks three variables:

- **SAR**: Current stop level
- **EP** (Extreme Point): Highest high in long mode, lowest low in short mode
- **AF** (Acceleration Factor): Ramps from `afStart` to `afMax` in `afIncrement` steps

### 2. SAR Update Rule

$$\text{SAR}_{t} = \text{SAR}_{t-1} + \text{AF} \times (\text{EP} - \text{SAR}_{t-1})$$

This is an exponential chase: SAR moves toward EP at a rate proportional to the gap, with AF controlling the speed. As AF increases, SAR accelerates toward the extreme point.

### 3. SAR Clamping

In long mode, SAR is clamped to be at or below the minimum of the prior two bars' lows:

$$\text{SAR}_{t} = \min(\text{SAR}_{t}, \text{Low}_{t-1}, \text{Low}_{t-2})$$

In short mode, SAR is clamped to be at or above the maximum of the prior two bars' highs:

$$\text{SAR}_{t} = \max(\text{SAR}_{t}, \text{High}_{t-1}, \text{High}_{t-2})$$

### 4. Reversal Detection

- **Long → Short**: When $\text{Low}_t < \text{SAR}_t$, reverse. Set SAR = EP, EP = Low, AF = afStart.
- **Short → Long**: When $\text{High}_t > \text{SAR}_t$, reverse. Set SAR = EP, EP = High, AF = afStart.

### 5. EP/AF Update (No Reversal)

If no reversal occurs and price makes a new extreme:

- Long: if $\text{High}_t > \text{EP}$, then EP = High, AF = min(AF + afIncrement, afMax)
- Short: if $\text{Low}_t < \text{EP}$, then EP = Low, AF = min(AF + afIncrement, afMax)

## Mathematical Foundation

The SAR update equation is a first-order IIR filter with time-varying coefficient:

$$y_t = y_{t-1} + \alpha_t (x^* - y_{t-1})$$

where $y_t$ = SAR, $x^*$ = EP (target), and $\alpha_t$ = AF (time-varying). This is equivalent to exponential smoothing toward a moving target, where the smoothing constant increases over time.

The acceleration factor progression:

$$\text{AF}_t = \min(\text{AF}_{\text{start}} + n \times \text{AF}_{\text{increment}}, \text{AF}_{\text{max}})$$

where $n$ is the number of new extreme points observed since the last reversal. The maximum number of acceleration steps is:

$$n_{\max} = \left\lfloor \frac{\text{AF}_{\max} - \text{AF}_{\text{start}}}{\text{AF}_{\text{increment}}} \right\rfloor = \left\lfloor \frac{0.20 - 0.02}{0.02} \right\rfloor = 9$$

At AF = 0.20 (maximum), SAR covers 20% of the EP-SAR gap per bar.

### Parameter Mapping

| Parameter | Default | Effect |
|-----------|---------|--------|
| afStart | 0.02 | Initial tracking speed. Lower = slower start. |
| afIncrement | 0.02 | How fast AF ramps. Lower = slower acceleration. |
| afMax | 0.20 | Terminal tracking speed. Higher = tighter final stop. |

## Performance Profile

### Operation Count (Streaming Mode)

Parabolic SAR uses an adaptive acceleration factor with trend-reversal detection — O(1) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Trend direction check | 1 | 2 cy | ~2 cy |
| EP (extreme point) update | 1 | 2 cy | ~2 cy |
| AF increment (conditional) | 1 | 2 cy | ~2 cy |
| SAR = SAR + AF*(EP - SAR) via FMA | 1 | 1 cy | ~1 cy |
| Reversal detection + reset | 1 | 3 cy | ~3 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~12 cy** |

O(1) per bar. FMA computes SAR update in a single instruction. Reversal branching adds ~3 cy amortized. No SIMD in streaming — trend state is sequential.

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Update (streaming) | O(1) | State machine: constant work per bar |
| Batch (span) | O(n) | Sequential state machine (no SIMD possible) |
| Memory | O(1) | Fixed state: 12 doubles + 1 bool |
| Warmup | 1 bar | First bar initializes direction |

### SIMD Analysis

PSAR cannot be vectorized. The state machine has data-dependent branches (reversal detection) and sequential dependencies (SAR depends on prior SAR). The Batch API delegates to streaming for correctness.

### Quality Metrics (1–10 Scale)

| Metric | Score | Rationale |
|--------|-------|-----------|
| Trend detection | 7 | Good in strong trends; whipsaws in ranges |
| Responsiveness | 8 | Acceleration factor provides adaptive speed |
| False signals | 5 | Prone to whipsaws in sideways markets |
| Simplicity | 9 | Three intuitive parameters |
| Universality | 8 | Works on any timeframe and asset class |

## Validation

| Library | Match | Tolerance | Notes |
|---------|-------|-----------|-------|
| Skender | ✅ | 1e-8 | `GetParabolicSar(0.02, 0.02, 0.2)` |
| TA-Lib | ✅ | 1e-8 | `Core.Sar(highs, lows, 0.02, 0.2)` |
| Self | ✅ | 1e-10 | Streaming == Batch == Span |

Note: Different libraries may vary on first-bar initialization (close > open vs. first-bar direction). QuanTAlib follows Wilder's original: direction from close vs. open on bar 0.

## Common Pitfalls

1. **Whipsaw in ranges**: PSAR reverses on every price crossover. In tight ranges, this produces rapid alternation. Mitigation: combine with ADX filter (only follow PSAR when ADX > 25). Impact: 30–50% of signals may be false in ranging markets.

2. **AF sensitivity**: Setting afStart too high (e.g., 0.10) makes SAR track price so tightly that minor retracements trigger reversals. Setting afMax too low (e.g., 0.05) makes SAR lag badly in strong trends.

3. **Initialization ambiguity**: Different implementations handle bar 0 differently (some use first 5 bars to determine initial direction). QuanTAlib uses Wilder's original close > open test. This may cause initial-bar divergence from other libraries.

4. **Bar correction with state machine**: The isNew=false rollback must restore the complete state machine (isLong, SAR, EP, AF, prev bars). Missing any field corrupts the trailing stop.

5. **No SIMD path**: The sequential state machine with data-dependent branches prevents vectorization. Batch API is O(n) sequential, not O(n/vector_width).

6. **SAR clamping requires history**: The clamp to prior-2-bars' extremes means bars 1–2 have limited clamping. This is by design (Wilder's specification) but can produce slightly different values than implementations that don't clamp on early bars.

## References

- Wilder, J. W. Jr. (1978). *New Concepts in Technical Trading Systems*. Trend Research. ISBN 978-0894590276.
- Kaufman, P. J. (2013). *Trading Systems and Methods*, 5th ed. Wiley. Chapter on Parabolic Time/Price System.
- StockCharts.com. "Parabolic SAR." ChartSchool Technical Indicators.
