# TTM_WAVE: TTM Wave

> "The market speaks in waves. Most traders only hear the ripples." -- John Carter

## Introduction

TTM Wave is a multi-period MACD composite oscillator built from six histogram channels at Fibonacci EMA periods. Each channel computes a standard MACD histogram (fast EMA minus slow EMA, then subtract the signal EMA of that difference). The six channels group into three wave bands -- A (short-term), B (medium-term), C (long-term) -- giving traders a single-pane view of momentum alignment across cycle lengths. When all three bands share the same sign, momentum is unanimous. When they diverge, the market is arguing with itself.

## Historical Context

John Carter introduced TTM Wave in *Mastering the Trade* (2005, revised 2012) as part of his "Trade The Markets" (TTM) suite alongside TTM Squeeze and TTM Trend. The indicator descends from Gerald Appel's MACD (1979) but extends it by running six parallel MACD channels whose periods follow the Fibonacci sequence: 8, 34, 55, 89, 144, 233, 377.

The design philosophy is straightforward: a single MACD channel captures momentum at one timescale. Stack six of them and you get a momentum spectrum. When short-term waves (A) fire first and medium/long-term waves (B, C) follow suit, the trend has legs. When A waves reverse while C waves persist, you are looking at a pullback, not a reversal.

Carter's original implementation appeared as thinkScript studies on the thinkorswim platform. The `TTM_Wave_A`, `TTM_Wave_B`, and `TTM_Wave_C` studies each contribute two histogram plots. Our implementation unifies all six channels into a single class with named outputs matching the TOS convention.

No external TA libraries (Skender, TA-Lib, Tulip, OoplesFinance) implement TTM Wave, making this a first-principles implementation validated through self-consistency tests.

## Architecture and Physics

### 1. MACD Channel Structure

Each channel *k* computes:

$$\text{MACD}_k = \text{EMA}(\text{close}, 8) - \text{EMA}(\text{close}, S_k)$$

$$\text{Signal}_k = \text{EMA}(\text{MACD}_k, S_k)$$

$$\text{Histogram}_k = \text{MACD}_k - \text{Signal}_k$$

where the slow/signal period $S_k$ takes Fibonacci values:

| Channel | $S_k$ | Wave Group |
| :------ | :----- | :--------- |
| 1 | 34 | A (inner) |
| 2 | 55 | A (outer) |
| 3 | 89 | B (inner) |
| 4 | 144 | B (outer) |
| 5 | 233 | C (inner) |
| 6 | 377 | C (outer) |

All channels share fast period $F = 8$ (Fibonacci $F_6$).

### 2. Wave Grouping

The six histograms map to three wave bands, each containing an inner (smaller period) and outer (larger period) envelope:

- **Wave A** (short-term momentum): channels 1 and 2
- **Wave B** (medium-term momentum): channels 3 and 4
- **Wave C** (long-term momentum): channels 5 and 6

Within each group, the inner channel reacts faster, the outer channel slower. When the inner crosses zero before the outer, momentum is accelerating at that timescale.

### 3. TOS Compatibility Mapping

The thinkorswim platform labels outputs differently:

| TOS Name | QuanTAlib Property | Definition |
| :------- | :----------------- | :--------- |
| Wave1 | `Wave1` / `WaveA2` | Channel 1 histogram (8,34,34) |
| Wave2High | `Wave2High` | max(WaveC1, WaveC2) |
| Wave2Low | `Wave2Low` | min(WaveC1, WaveC2) |

### 4. Composition Architecture

`TtmWave` is implemented as a composition of six internal `Macd` instances rather than manual EMA management. This delegates bar correction (`isNew` rollback), NaN handling, and state management to the battle-tested `Macd` class. The tradeoff: six redundant fast EMA computations (all share period 8). The benefit: zero additional state synchronization bugs and trivial maintenance.

### 5. Warmup Period

The slowest channel uses periods (8, 377, 377). The MACD warmup for that channel is:

$$W = \max(8, 377) + 377 - 2 = 752$$

All channels are hot once the slowest is hot. `IsHot` is the conjunction of all six MACD `IsHot` flags.

## Mathematical Foundation

### EMA Recursion

Each EMA with period $P$ uses smoothing factor $\alpha = 2/(P+1)$:

$$\text{EMA}_t = \alpha \cdot x_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

### MACD Line

$$M_t = \text{EMA}(x, 8)_t - \text{EMA}(x, S_k)_t$$

### Signal Line

$$\text{Sig}_t = \text{EMA}(M, S_k)_t$$

### Histogram

$$H_t = M_t - \text{Sig}_t$$

The histogram is a second-order momentum measure: it tracks the rate of change of the MACD line relative to its own smoothed average. Positive histogram means MACD is above its signal (bullish acceleration); negative means below (bearish acceleration).

### Z-Domain Transfer Function

For a single channel with fast period $F$ and slow period $S$:

$$H(z) = \left[\frac{\alpha_F}{1-(1-\alpha_F)z^{-1}} - \frac{\alpha_S}{1-(1-\alpha_S)z^{-1}}\right] \cdot \left[1 - \frac{\alpha_S}{1-(1-\alpha_S)z^{-1}}\right]$$

where $\alpha_F = 2/(F+1)$ and $\alpha_S = 2/(S+1)$.

## Performance Profile

| Metric | Value |
| :----- | :---- |
| Operations per update | 6 x MACD update (18 EMA updates total) |
| Memory | 6 Macd instances with internal state |
| Streaming complexity | O(1) per bar |
| SIMD applicability | Not applicable (recursive IIR filter) |
| Warmup bars | 752 |
| Allocations in Update | Zero (struct TValue returns) |

### Quality Metrics

| Quality | Score (1-10) | Notes |
| :------ | :----------- | :---- |
| Trend detection | 8 | Multi-timeframe alignment is powerful |
| Noise rejection | 7 | Longer-period channels naturally filter |
| Responsiveness | 6 | C waves lag substantially (377 period) |
| Divergence signals | 8 | A vs C divergence is the primary signal |
| False signal rate | 5 | A waves generate frequent zero crosses |
| Computational cost | 4 | Six MACD instances is nontrivial |

## Validation

No external libraries implement TTM Wave. Validation relies on self-consistency:

| Test Category | Method | Result |
| :------------ | :----- | :----- |
| Streaming vs Batch | All values match to 1e-10 | Pass |
| Primed vs Cold | Last value matches to 1e-8 | Pass |
| Deterministic replay | Same seed produces identical output | Pass |
| Reset + replay | Matches fresh computation to 1e-15 | Pass |
| TOS property mapping | Wave1=WaveA2, Wave2High/Low correct | Pass |
| Large dataset (5000 bars) | All outputs finite, no overflow | Pass |
| Warmup period | IsHot engages at or before bar 752 | Pass |

## Common Pitfalls

1. **Confusing wave numbering with channel numbering.** WaveA1 is the *outer* A wave (channel 2, period 55), not channel 1. WaveA2 is the *inner* (channel 1, period 34). This matches the TOS naming where larger envelope gets the "1" suffix. Swapping them reverses your interpretation of momentum acceleration.

2. **Expecting C waves to react to short-term moves.** Channel 6 (period 377) needs roughly 752 bars to warm up and responds glacially to price changes. A sudden $5\%$ move barely registers on Wave C. Use Wave A for timing, Wave C for bias.

3. **Trading A wave zero-crosses in isolation.** Wave A zero-crosses fire frequently in choppy markets. Without confirming B/C wave direction, you are trading noise. The indicator's value lies in multi-wave alignment, not single-wave signals.

4. **Ignoring the warmup period.** With 752 bars needed for full warmup, daily charts require three years of history. On 1-minute charts that is 12.5 hours. Insufficient warmup produces misleading histogram values that can invert actual momentum direction.

5. **Assuming histogram magnitude implies trend strength.** Longer-period channels naturally produce larger absolute histogram values because the fast-slow EMA spread grows with period. Comparing Wave A magnitude to Wave C magnitude directly is comparing apples to watermelons. Normalize by channel period if you need cross-wave magnitude comparison.

6. **Not accounting for bar correction.** When the current bar updates (same timestamp), all six channels must roll back to their previous state. The composition architecture handles this via `isNew=false` propagation to each internal Macd, but custom implementations that skip bar correction will accumulate state errors.

7. **Over-optimizing by sharing the fast EMA.** All six channels use fast period 8, so sharing one EMA(8) instance seems logical. However, the MACD class manages internal state atomically (previous state rollback). Sharing the fast EMA across channels breaks independent bar correction. The redundant computation costs microseconds; the correctness cost of sharing would be debugging hours.

## References

- Carter, J. (2012). *Mastering the Trade: Proven Techniques for Profiting from Intraday and Swing Trading Setups* (2nd ed.). McGraw-Hill.
- Appel, G. (1979). *The Moving Average Convergence-Divergence Trading Method*. Signalert Corporation.
- thinkorswim TTM_Wave_A, TTM_Wave_B, TTM_Wave_C thinkScript studies.
- useThinkScript community analysis of TTM Wave internals.

## See Also

- [MACD: Moving Average Convergence Divergence](../../momentum/macd/Macd.md)
- [TTM_SQUEEZE: TTM Squeeze](../../dynamics/ttm_squeeze/TtmSqueeze.md)
- [TTM_TREND: TTM Trend](../../dynamics/ttm_trend/TtmTrend.md)
- [AO: Awesome Oscillator](../ao/Ao.md)
