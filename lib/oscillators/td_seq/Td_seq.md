# TD_SEQ: TD Sequential

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (TdSeq)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `comparePeriod + 1` bars                          |

### TL;DR

- TD Sequential is Tom DeMark's exhaustion counting system that identifies potential trend reversals through two phases: a 9-count Setup phase that d...
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `comparePeriod + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

TD Sequential is Tom DeMark's exhaustion counting system that identifies potential trend reversals through two phases: a 9-count Setup phase that detects overextended trends, and a 13-count Countdown phase that pinpoints probable reversal timing. Unlike oscillators that measure momentum magnitude, TD Sequential counts consecutive qualifying bars, producing integer outputs (Setup: $\pm 1$ to $\pm 9$; Countdown: $\pm 1$ to $\pm 13$) that represent the progression toward exhaustion. A completed 9-count Setup followed by a completed 13-count Countdown signals high-probability trend exhaustion. All state is maintained in O(1) scalar variables with no buffers required.

## Historical Context

Thomas DeMark developed TD Sequential during the 1970s-1990s as part of his comprehensive market timing framework, published in *The New Science of Technical Analysis* (1994) and *New Market Timing Techniques* (1997). The indicator was conceived as a structural alternative to momentum oscillators: rather than measuring how overbought or oversold a market is, it counts how long a directional condition has persisted and identifies specific exhaustion points. DeMark's key insight was that trends exhaust at predictable counting thresholds (9 for Setup, 13 for Countdown), a pattern he validated across equity, fixed-income, commodity, and currency markets. The indicator found significant institutional adoption, with Bloomberg terminals providing native DeMark indicators and firms like Tudor Investment Corporation licensing the methodology. The compare period (typically 4 bars) determines the lookback for the close comparison: each Setup bar requires close above/below close[4], creating a structural requirement that the trend has been sustained for at least 4 additional bars beyond the count itself. The Countdown phase adds a higher bar: the close must exceed the high or low of 2 bars ago, a condition that doesn't occur on every bar, making the Countdown non-consecutive.

## Architecture & Physics

### Two-Phase State Machine

**Phase 1: Setup ($\pm 1$ to $\pm 9$)**

The Setup counter compares the current close to the close `comparePeriod` bars ago. If close > close[comparePeriod], the sell setup count increments (positive); if close < close[comparePeriod], the buy setup count decrements (negative). The count resets to zero when the condition breaks or reverses direction. Counts are clamped to $\pm 9$.

When the count reaches exactly $\pm 9$ for the first time (without having been reset), the setup is "complete" and Phase 2 begins. The setupComplete flag prevents re-triggering until a reset occurs.

**Phase 2: Countdown ($\pm 1$ to $\pm 13$)**

After a completed 9-count Setup, the Countdown phase begins. Unlike Setup, Countdown is non-consecutive: a sell countdown bar requires close > high[2]; a buy countdown bar requires close < low[2]. Only qualifying bars increment the countdown. The count progresses toward $\pm 13$, at which point the countdown completes and the directional signal resets.

An opposite 9-count Setup during an active Countdown resets and restarts the Countdown in the new direction.

### Zero-Buffer Design

The entire indicator state consists of four scalar variables: `setupCount`, `countdownCount`, `countdownDir`, and `setupComplete`. No circular buffers, arrays, or sliding windows are needed. The only historical lookback dependency is PineScript's `close[comparePeriod]`, `low[2]`, and `high[2]`.

## Mathematical Foundation

**Setup counting** (comparePeriod = $p$):

$$S_t = \begin{cases} S_{t-1} - 1 & \text{if } C_t < C_{t-p} \text{ and } S_{t-1} \leq 0 \\ -1 & \text{if } C_t < C_{t-p} \text{ and } S_{t-1} > 0 \\ S_{t-1} + 1 & \text{if } C_t > C_{t-p} \text{ and } S_{t-1} \geq 0 \\ +1 & \text{if } C_t > C_{t-p} \text{ and } S_{t-1} < 0 \\ 0 & \text{if } C_t = C_{t-p} \end{cases}$$

$$S_t = \text{clamp}(S_t, -9, +9)$$

**Setup completion trigger:**

$$\text{if } |S_t| = 9 \text{ and not previously complete} \Rightarrow \text{begin Countdown, dir} = \text{sign}(S_t)$$

**Countdown** (non-consecutive):

$$CD_t = \begin{cases} CD_{t-1} - 1 & \text{if dir} = -1 \text{ and } C_t < L_{t-2} \\ CD_{t-1} + 1 & \text{if dir} = +1 \text{ and } C_t > H_{t-2} \\ CD_{t-1} & \text{otherwise (no qualifying bar)} \end{cases}$$

**Countdown completion:**

$$\text{if } |CD_t| \geq 13 \Rightarrow CD_t = \text{sign}(dir) \times 13, \text{ reset dir}$$

**Countdown reset on opposite Setup:**

$$\text{if dir} = +1 \text{ and } S_t = -9, \text{ or dir} = -1 \text{ and } S_t = +9 \Rightarrow \text{reset CD, new dir}$$

**Default parameters:** comparePeriod = 4.

## Performance Profile

### Operation Count (Streaming Mode)

TD Sequential counts sequential close comparisons (Setup: 9 bars; Countdown: 13 bars). Pure comparison arithmetic, no floating-point math.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (close[0] > close[4]) setup count | 1 | 1 | 1 |
| CMP (close[2] ≤ close[0]) countdown | 1 | 1 | 1 |
| Counter increment/reset | 2 | 1 | 2 |
| RingBuffer reads × 2 (lag 2 and lag 4) | 2 | 1 | 2 |
| State encode (setup bar, countdown bar) | 2 | 1 | 2 |
| **Total** | **8** | — | **~8 cycles** |

The cheapest oscillator in the library: purely integer comparisons and counters. ~8 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Lag-4 comparison (Setup) | Yes | VCMPPD on offset arrays |
| Lag-2 comparison (Countdown) | Yes | VCMPPD on offset arrays |
| Sequential counter | **No** | State-dependent — each bar depends on prior count |

The counter state is inherently sequential. The individual comparisons are vectorizable in a pre-pass, but the sequential counting dependency prevents full SIMD acceleration.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact binary comparisons; no floating-point |
| **Timeliness** | 9/10 | 9-bar setup window is short; immediate signal |
| **Smoothness** | 3/10 | Discrete count output jumps at signal events |
| **Noise Rejection** | 5/10 | Sequential counting requires exact pattern; no noise tolerance |

## Resources

- DeMark, T.R. (1994). *The New Science of Technical Analysis*. Wiley
- DeMark, T.R. (1997). *New Market Timing Techniques*. Wiley
- Bloomberg Terminal: DeMark Indicators (DMRK) implementation reference
- PineScript reference: [`td_seq.pine`](td_seq.pine)
