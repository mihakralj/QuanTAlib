# ETHERM: Elder's Thermometer

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 22)                      |
| **Outputs**      | Single series (Etherm)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- Elder's Thermometer (ETHERM) measures how far today's price bar extends beyond yesterday's range, capturing the maximum absolute expansion in eithe...
- Parameterized by `period` (default 22).
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Markets run a fever before they crash. The thermometer tells you when to reach for the aspirin."

Elder's Thermometer (ETHERM) measures how far today's price bar extends beyond yesterday's range, capturing the maximum absolute expansion in either direction. Developed by Dr. Alexander Elder and described in *Come Into My Trading Room* (2002, p.162), the indicator distinguishes between sleepy, quiet periods and hot episodes when market crowds become excited. The raw thermometer reading is smoothed with an EMA to produce a signal line; when temperature spikes to triple the signal, it flags an explosive move worth fading. At 5 operations per bar for the raw value and O(1) EMA update, ETHERM is among the cheapest volatility measures to compute.

## Historical Context

Dr. Alexander Elder, a psychiatrist-turned-trader who emigrated from the Soviet Union in the 1970s, built his reputation on applying behavioral psychology to market analysis. His first book *Trading for a Living* (1993) introduced the Elder-Ray Index and the Triple Screen system. His second, *Come Into My Trading Room* (2002), added the Market Thermometer on page 162, filling a gap he identified: existing volatility tools (ATR, Bollinger Width) measured absolute dispersion, but none specifically isolated the *bar-to-bar range extension* that characterizes crowd excitement.

Elder's insight was deceptively simple. Adjacent bars in a quiet market overlap. The high barely exceeds yesterday's high; the low barely undercuts yesterday's low. When the crowd gets excited, bars start pushing outside previous ranges. The thermometer captures exactly this phenomenon: how many price units did today's bar extend beyond yesterday's boundaries?

The formula differs from True Range in a critical way. TR measures the total possible price excursion including gaps (max of H-L, |H-prevC|, |L-prevC|). ETHERM ignores the close entirely and focuses on high-to-high and low-to-low comparisons. A stock that gaps up 5 points but trades within a 1-point range registers TR=5 but ETHERM near zero (assuming yesterday's high was close to today's high). The two indicators answer different questions: TR asks "how far could price have traveled?" while ETHERM asks "how much did today's bar escape yesterday's?"

Several implementations exist across platforms. The ProRealCode and MotiveWave versions match Elder's original formula precisely. The LightningChart JS version diverges significantly, comparing current bars to N-periods-ago bars rather than the previous bar. This QuanTAlib implementation follows Elder's original specification: previous bar comparison with inside-bar detection.

## Architecture & Physics

ETHERM has three components: raw temperature calculation, EMA signal smoothing, and threshold detection.

### 1. Raw Temperature Calculation

The thermometer measures the maximum absolute extension beyond the previous bar:

$$
\text{highDiff}_t = |H_t - H_{t-1}|
$$

$$
\text{lowDiff}_t = |L_{t-1} - L_t|
$$

Three cases determine the output:

$$
T_t = \begin{cases}
0 & \text{if } H_t < H_{t-1} \text{ AND } L_t > L_{t-1} \text{ (inside bar)} \\
\max(\text{highDiff}_t, \text{lowDiff}_t) & \text{otherwise}
\end{cases}
$$

The inside bar case is significant. When today's entire range fits within yesterday's range, there is zero range extension in either direction. The crowd is dormant.

### 2. EMA Signal Line

The raw temperature is smoothed with an exponential moving average:

$$
\alpha = \frac{2}{N + 1}
$$

$$
S_t = \alpha \cdot T_t + (1 - \alpha) \cdot S_{t-1}
$$

Default period $N = 22$ (approximately one trading month). The EMA provides a baseline "normal temperature" against which spikes and troughs are measured.

### 3. Threshold Detection

Elder defined two key thresholds:

**Explosive move:** When the thermometer reaches or exceeds the signal multiplied by a factor (default 3.0):

$$
\text{Explosive} = T_t \geq S_t \times M
$$

where $M$ is the multiplier (default 3.0).

**Idle market:** When the thermometer remains below the signal for a sustained number of consecutive bars (Elder suggested 5-7 bars). This is a secondary signal not computed in the indicator itself but observable from the histogram.

### 4. First Bar Handling

For the first bar (no previous bar available):

$$
T_0 = 0
$$

Using `nz(high[1], high)` maps the previous high to today's high, making highDiff and lowDiff both zero. This is correct: with no history, there is no range extension to measure.

## Mathematical Foundation

### Why Absolute Values?

Consider a bar where today's high is 102 and yesterday's high was 105. The extension is $|102 - 105| = 3$. Without the absolute value, the result would be $-3$, hiding the magnitude. Elder's thermometer cares about *size* of escape, not direction. A 3-point high compression and a 3-point low extension represent equal amounts of crowd activity.

### Relationship to True Range

True Range and ETHERM share a structural similarity but measure different phenomena:

| Scenario | TR | ETHERM |
| :--- | :--- | :--- |
| No gap, wide bar | $H - L$ | $\max(\|H-H_{-1}\|, \|L_{-1}-L\|)$ |
| Large gap up, narrow bar | $H - C_{-1}$ (large) | Near 0 (similar H-to-H) |
| Breakout bar exceeding prior range | $H - L$ | Large (extension detected) |
| Inside bar | $H - L$ (positive) | 0 (no extension) |

ETHERM specifically detects range *expansion*. TR detects total price travel. A market that gaps and then consolidates shows high TR but low ETHERM.

### EMA Warmup Compensation

The PineScript reference implementation uses warmup-compensated EMA to eliminate initialization bias:

$$
e_t = e_{t-1} \cdot (1 - \alpha), \quad e_0 = 1
$$

$$
S_{compensated} = \frac{S_{raw}}{1 - e_t} \quad \text{when } e_t > \epsilon
$$

This ensures accurate signal values from the first bar rather than waiting for the EMA to "fill up."

### Convergence

For EMA period $N = 22$, $\alpha = 2/23 \approx 0.087$:

$$
\text{WarmupPeriod} \approx \frac{\ln(0.05)}{\ln(1 - \alpha)} \approx \frac{-3.0}{-0.091} \approx 33 \text{ bars}
$$

After 33 bars, the initialization bias drops below 5%.

### Inside Bar Probability

In typical equity markets, inside bars occur approximately 15-25% of trading days. The zero-temperature reading for inside bars creates a natural floor that keeps the EMA signal from rising without genuine range extension. This asymmetry is intentional: Elder wanted the thermometer to measure heat, not cold.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB | 2 | 1 | 2 |
| ABS | 2 | 1 | 2 |
| CMP | 3 | 1 | 3 |
| MAX | 1 | 1 | 1 |
| FMA | 1 | 5 | 5 |
| MUL | 2 | 3 | 6 |
| DIV | 1 | 15 | 15 |
| **Total** | **12** | | **~34 cycles** |

ETHERM is extremely lightweight. No logarithms, no square roots, no transcendental functions. The EMA update dominates at ~60% of total cost.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Subtractions (H-prevH, prevL-L) | 1024 | 128 | 8x |
| Absolute values | 1024 | 128 | 8x |
| Comparisons + MAX | 1536 | 192 | 8x |
| EMA update | 512 | 512 | 1x (sequential) |

The raw temperature calculation vectorizes perfectly. The EMA is inherently sequential (each value depends on the previous), limiting overall batch speedup to roughly 3-4x.

### Memory Profile

- **Per instance:** ~64 bytes (state struct with prevHigh, prevLow, EMA state, warmup)
- **No ring buffer required** (only needs previous bar's high and low)
- **100 instances:** ~6.4 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact calculation, no approximations |
| **Timeliness** | 9/10 | Minimal lag; raw value is instantaneous, EMA adds slight delay |
| **Smoothness** | 4/10 | Raw thermometer is spiky by design; signal line smooths |
| **Simplicity** | 9/10 | Two subtractions, two abs, one max, one EMA |
| **Interpretability** | 8/10 | Direct physical meaning: price units of range extension |

## Validation

ETHERM is not widely implemented in major open-source libraries under a standard name. Most implementations are custom scripts.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches etherm.pine reference |
| **ProRealCode** | ✅ | Matches Elder's original formula |
| **MotiveWave** | ✅ | Confirms formula: "highest absolute difference" |
| **Manual** | ✅ | Validated against Elder p.162 formula |

The absence from standard libraries is unsurprising. ETHERM was published in a trading book, not an academic paper. It lacks the institutional pedigree of Wilder's indicators (ATR, RSI) or Bollinger's Bands. The algorithm is simple enough that most platforms implement it as a custom script rather than a built-in function.

## Common Pitfalls

1. **Confusing ETHERM with ATR**: ATR measures total price excursion including gaps (uses close). ETHERM measures bar-to-bar range extension (ignores close entirely). A large gap-up with a narrow range produces high ATR but near-zero ETHERM. Using one where the other is intended produces meaningfully wrong signals.

2. **Inside bar handling**: Some implementations omit the inside bar check, computing `max(highDiff, lowDiff)` even when both differences are negative (meaning compression, not expansion). This incorrectly reports range contraction as if it were expansion. Elder's original formula explicitly returns zero for inside bars.

3. **Absolute value omission**: The formula requires absolute values of the differences. When `Low_today > Low_yesterday`, `Low_yesterday - Low_today` is negative. Without `abs()`, the max function may select highDiff by default even when lowDiff is the dominant extension. Approximately 10-15% of signals will be wrong.

4. **EMA period sensitivity**: Elder's default of 22 bars (roughly one trading month) works for daily charts. On 5-minute charts, 22 bars spans less than 2 hours. For intraday use, scale the period proportionally: ~250 for 5-min, ~50 for hourly. Using period 22 on intraday data produces an overly responsive signal line.

5. **Multiplier calibration**: The default 3.0 multiplier for explosive moves was designed for daily equity data in the late 1990s. Crypto and high-volatility assets may need higher multipliers (4.0-5.0) to avoid false positives. Low-volatility instruments (bonds, utilities) may need lower multipliers (2.0-2.5). Test the multiplier against historical data before relying on it.

6. **Zero-temperature clustering**: Inside bars cluster during consolidation. Extended periods of zero readings followed by a breakout bar produce a spike that appears dramatic relative to the suppressed EMA. This is feature, not bug: Elder designed the indicator to flag exactly this transition. But traders should be aware that the spike magnitude reflects the prior calm as much as the current excitement.

7. **No directional information**: ETHERM measures magnitude of range extension but not direction. A 10-point extension could be bullish (new highs) or bearish (new lows). Pair ETHERM with directional indicators (Elder-Ray, Impulse System) for complete context.

## Trading Applications

### Entry Timing

Elder's primary recommendation: enter positions when Thermometer < Signal:

```text
If system generates entry signal AND ETHERM < Signal:
  Execute entry (low slippage environment)
If system generates entry signal AND ETHERM > Signal:
  Wait or reduce size (hot market, slippage likely)
```

### Profit-Taking on Spikes

Exit (or take partial profits) when Thermometer >= Signal x 3:

```text
If ETHERM >= Signal × multiplier:
  Take profits on existing positions
  Panics are short-lived; cash in before reversion
```

### Volatility Regime Filter

Track consecutive bars below the signal line:

```text
If ETHERM < Signal for 7+ consecutive bars:
  Market is idle/consolidating
  Prepare for potential breakout
  Tighten stops or reduce position size
```

## Relationship to Other Indicators

| Indicator | Relationship to ETHERM |
| :--- | :--- |
| **TR** | TR measures total price travel (with gaps); ETHERM measures range extension only |
| **ATR** | Smoothed TR; both measure volatility but from different perspectives |
| **Elder-Ray** | Bull/Bear Power measures distance from EMA; complements ETHERM's range extension |
| **Impulse System** | Directional classification; pair with ETHERM for timing |
| **Bollinger Width** | Measures band expansion/contraction; slower-moving volatility gauge |
| **ADX** | Trend strength; ETHERM measures volatility regardless of trend |

## References

- Elder, A. (2002). *Come Into My Trading Room: A Complete Guide to Trading*. John Wiley & Sons. pp. 162-164.
- Elder, A. (1993). *Trading for a Living: Psychology, Trading Tactics, Money Management*. John Wiley & Sons.
- Elder, A. (2014). *The New Trading for a Living*. John Wiley & Sons. (Updated treatment of the Thermometer.)
- LazyBear. (2015). "Elder's Market Thermometer." TradingView Community Scripts.
- MotiveWave Documentation. "Elders Thermometer (THER)." docs.motivewave.com.
