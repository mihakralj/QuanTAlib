# DPO: Detrended Price Oscillator

> "Strip the trend and what remains is the cycle." — William Blau

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Single series (close) |
| **Parameters** | `period` (default 20) |
| **Outputs** | Single series (DPO line) |
| **Output range** | Unbounded (centered on zero) |
| **Warmup** | `period + floor(period / 2) + 1` bars |

### Key takeaways

- Removes the long-term trend by subtracting a displaced SMA, isolating short-term cycles.
- The displacement is `floor(period / 2) + 1` bars into the past, making DPO intentionally non-current.
- Zero-line crossovers indicate whether price sits above or below its detrended average.
- Useful for estimating dominant cycle length by measuring peak-to-peak distance.
- Not a timing indicator. DPO reveals rhythm, not direction.

## Historical Context

William Blau formalized the Detrended Price Oscillator in *Momentum, Direction, and Divergence* (1995), though the concept of removing trend via displaced moving averages predates his work by decades. The core insight is straightforward: if you shift an SMA backward by half its period, the average sits at the center of the window rather than its trailing edge. Subtracting that centered value from price strips the trend component and exposes the underlying cycle.

Most oscillators try to be current. DPO deliberately looks backward. That design choice makes it useless for real-time trend-following but surprisingly effective for cycle analysis. Traders who need to estimate the dominant period of a market's oscillation find DPO more honest than indicators that blend trend and cycle information into a single, ambiguous line.

The PineScript (non-centered) variant used here computes `close - SMA[displacement]`, referencing the SMA from `displacement` bars ago rather than centering the SMA around the current bar. This differs from the Tulip/TA-Lib centered variant (`close[displacement] - SMA`), which anchors the price to the past instead. Both isolate cycles, but they produce different numeric results and are not cross-comparable.

## What It Measures and Why It Matters

DPO measures the deviation of current price from a displaced simple moving average. By pushing the SMA backward in time, the trend component cancels out, leaving only the cyclical oscillation around zero.

The result is an unbounded oscillator that crosses zero when price equals its detrended average. Peaks and troughs in the DPO correspond to short-term cycle extremes. The distance between consecutive peaks (or troughs) estimates the dominant cycle period. This makes DPO a cycle-measurement tool rather than a trading signal generator.

Because DPO is not aligned to the latest bar, it should not be used for real-time entry/exit decisions. Its value lies in structural analysis: identifying how frequently a market oscillates and whether the current cycle amplitude is expanding or contracting.

## Mathematical Foundation

### Core Formula

$$
\text{displacement} = \left\lfloor \frac{P}{2} \right\rfloor + 1
$$

$$
\text{SMA}_t = \frac{1}{P} \sum_{i=0}^{P-1} x_{t-i}
$$

$$
\text{DPO}_t = x_t - \text{SMA}_{t - \text{displacement}}
$$

where $P$ is the lookback period and $x_t$ is the input value at bar $t$.

### Parameter Mapping

| Parameter | Code | Default | Constraints |
|-----------|------|---------|-------------|
| Period | `period` | 20 | `> 0` |
| Displacement | computed | `(period / 2) + 1` | derived |

### Warmup Period

$$
W = P + \left\lfloor \frac{P}{2} \right\rfloor + 1
$$

The SMA requires $P$ bars to fill. The displacement buffer then requires $\lfloor P/2 \rfloor + 1$ additional bars before the oldest SMA value is available.

## Architecture & Physics

### 1. Dual RingBuffer Design

Two `RingBuffer` instances partition the computation:

- `_smaBuffer(period)`: maintains a running sum for O(1) SMA calculation via `Sum / period`.
- `_smaHistory(displacement + 1)`: stores computed SMA values; `.Oldest` retrieves the displaced SMA.

### 2. Source Subscription

The chaining constructor subscribes to `src.Pub += Handle`, forwarding `TValueEventArgs` directly into `Update` without storing the source reference.

### 3. Batch Path

`Batch(ReadOnlySpan<double>, Span<double>, int)` mirrors the streaming logic with local `RingBuffer` instances. No SIMD acceleration is applied because the displacement dependency creates a sequential data-flow constraint.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period <= 0` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Substitutes last valid value |
| Before warmup complete | Returns `0.0` |
| `isNew = false` | `Restore()` on both RingBuffers; recomputes with corrected value |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Above zero | `DPO > 0` | Price above detrended average |
| Below zero | `DPO < 0` | Price below detrended average |
| Peak | Local maximum | Short-term cycle top |
| Trough | Local minimum | Short-term cycle bottom |

### Signal Patterns

- **Zero-line crossover**: DPO crossing above zero suggests price has moved above its displaced average; crossing below suggests the opposite.
- **Peak-to-peak measurement**: Distance between consecutive DPO peaks estimates the dominant cycle length.
- **Amplitude expansion**: Growing peak/trough magnitudes indicate increasing cyclical volatility.
- **Divergence**: Price making new highs while DPO peaks decline warns of potential cycle exhaustion.

### Practical Notes

- DPO is backward-looking by design. Do not use it for real-time trade timing.
- Choose `period` to approximate the expected cycle length. A 20-bar DPO isolates roughly 20-bar cycles.
- Works best in range-bound or cyclical markets. Strong trends produce a persistent DPO bias that obscures the cycle signal.

## Related Indicators

- [**SMA**](../../trends_FIR/sma/Sma.md): The underlying moving average that DPO detrends against.
- [**CFO**](../cfo/Cfo.md): Another detrending oscillator using linear regression instead of displaced SMA.
- [**APO**](../apo/Apo.md): Measures momentum via EMA difference rather than cycle isolation.

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Manual SMA | ✅ | Exact match against independent displaced-SMA computation (`< 1e-9`) |
| Tulip | ❌ | Not comparable. Tulip uses centered formula (`close[back] - SMA`); QuanTAlib uses PineScript non-centered formula (`close - SMA[back]`) |
| Skender | ❌ | No DPO implementation available |
| TA-Lib | ❌ | Uses centered variant; different algorithm |

## Performance Profile

### Key Optimizations

- **O(1) streaming**: `RingBuffer.Sum` provides constant-time SMA; `.Oldest` provides constant-time displacement lookback.
- **Zero allocation**: Both `Update` paths use pre-allocated `RingBuffer` instances and a `record struct State`.
- **Snapshot/Restore**: Bar correction via `RingBuffer.Snapshot()` / `Restore()` avoids buffer cloning.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| Additions | 1 (running sum maintained by RingBuffer) |
| Divisions | 1 (`Sum / period`) |
| Subtractions | 1 (`value - displacedSma`) |
| Comparisons | 2 (`IsFinite` check, `IsFull` checks) |
| **Total** | **~5 ops** |

### SIMD Analysis (Batch Mode)

| Property | Value |
|----------|-------|
| Vectorizable | No |
| Reason | Displacement creates sequential dependency between SMA output and DPO computation |
| Fallback | Scalar loop with local `RingBuffer` instances |

## Common Pitfalls

1. **Confusing DPO variants**: The PineScript (non-centered) and Tulip/TA-Lib (centered) formulas produce different results. Cross-library validation requires matching the same variant.
2. **Using DPO for real-time signals**: DPO is displaced by design. It does not reflect the latest bar's relationship to the current trend.
3. **Wrong period selection**: If the DPO period does not approximate the dominant cycle, the oscillator produces noise rather than signal. Measure peak-to-peak distance to calibrate.
4. **Ignoring warmup**: The first `period + displacement` bars output `0.0`. Trading on these values guarantees false signals.
5. **Trend-dominated markets**: In strong trends, DPO stays persistently positive or negative, defeating its cycle-isolation purpose.

## FAQ

**Q: Why does QuanTAlib's DPO not match Tulip or TA-Lib?**
A: Different formula variant. QuanTAlib uses the PineScript non-centered formula (`close - SMA[displacement]`), while Tulip and TA-Lib use the centered variant (`close[displacement] - SMA`). Both are valid but not numerically equivalent.

**Q: How do I pick the right period?**
A: Start with a period that matches your expected cycle length (e.g., 20 for daily data targeting a roughly monthly cycle). Then measure peak-to-peak distance in the DPO output. If the measured distance consistently differs from your period, adjust accordingly.

**Q: Can DPO be used as a standalone trading signal?**
A: Not recommended. DPO measures cycle structure, not timing. Pair it with a trend-following indicator (SMA, EMA) for entries and use DPO to confirm whether the market is in a cyclical phase worth trading.

## References

- Blau, William. *Momentum, Direction, and Divergence*. Wiley, 1995.
- Dorsey, Thomas. *Point and Figure Charting*. Wiley, 2007.
- PineScript reference: `dpo.pine`
