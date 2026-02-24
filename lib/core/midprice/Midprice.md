# MIDPRICE: Midpoint Price over Period

MIDPRICE computes the center of a rolling price channel by averaging the highest High and lowest Low over the past $N$ bars: $(\text{Highest}(H, N) + \text{Lowest}(L, N)) \times 0.5$. Unlike the stateless price transforms (AVGPRICE, MEDPRICE, TYPPRICE, WCLPRICE) that operate on a single bar, MIDPRICE maintains a lookback window and produces a rolling estimate of the price range's midpoint. This makes it a simplified channel center line, equivalent to the midpoint of a Donchian Channel. The calculation uses two internal RingBuffers for $O(N)$ max/min computation per bar. TA-Lib compatible via `TA_MIDPRICE`.

## Historical Context

MIDPRICE is the simplest possible channel-based price reference, conceptually dating back to Richard Donchian's channel breakout work in the 1960s. Where Donchian Channels plot the full upper/lower envelope, MIDPRICE extracts only the midline. The TA-Lib function `TA_MIDPRICE` takes separate High and Low arrays and a period parameter, which distinguishes it from `TA_MIDPOINT` (which operates on a single series).

The distinction between MIDPRICE and MIDPOINT matters:

- **MIDPOINT**: $(\text{Highest}(V, N) + \text{Lowest}(V, N)) \times 0.5$ on a single value series
- **MIDPRICE**: $(\text{Highest}(H, N) + \text{Lowest}(L, N)) \times 0.5$ on separate High/Low channels

MIDPRICE always produces a wider (or equal) range because the highest High is at least as large as the highest Close, and the lowest Low is at most as small as the lowest Close. This makes MIDPRICE a more conservative channel center, reflecting the full extent of price exploration rather than just settlement levels.

## Architecture & Physics

### 1. Core Formula

$$\text{MidPrice}_t = \left(\max_{i=0}^{N-1} H_{t-i} + \min_{i=0}^{N-1} L_{t-i}\right) \times 0.5$$

### 2. Rolling Window Implementation

Two independent `RingBuffer` instances maintain the last $N$ High and Low values:

- `_highBuffer`: Stores High values; `Max()` returns the rolling maximum
- `_lowBuffer`: Stores Low values; `Min()` returns the rolling minimum

The `RingBuffer.Max()` and `RingBuffer.Min()` operations scan the buffer linearly, making each `Update` call $O(N)$. This was a deliberate design choice to avoid the cross-project dependency that composing `Highest`/`Lowest` indicator instances from `lib/numerics/` would introduce. The core library must remain self-contained for Quantower builds.

### 3. State Management

- **RingBuffer snapshots**: `isNew=true` captures buffer state via `Snapshot()`; `isNew=false` restores via `Restore()` for bar correction.
- **Last-valid substitution**: Non-finite High or Low values are replaced with the last known finite value.
- **Warmup**: `IsHot` becomes true when the buffer reaches `period` elements.

### 4. Complexity

$O(N)$ per bar where $N$ is the period, due to linear scan for max/min. For typical periods (5-20), this is negligible. Always-hot after $N$ bars.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window for rolling max/min ($N$) | (required) | $\geq 1$ |

### MIDPRICE vs Related Indicators

| Indicator | Formula | Input | State |
|-----------|---------|-------|-------|
| MIDPRICE | $(\max(H,N) + \min(L,N)) \times 0.5$ | TBar (H/L channels) | Rolling window |
| MIDPOINT | $(\max(V,N) + \min(V,N)) \times 0.5$ | Single series | Rolling window |
| MEDPRICE | $(H + L) \times 0.5$ | TBar (single bar) | Stateless |
| Donchian Mid | Same as MIDPRICE | TBar (H/L channels) | Rolling window |

### Pseudo-code

```
function MIDPRICE(bar, period):
    validate: period ≥ 1

    h, l ← bar.High, bar.Low

    // Substitute last-valid for non-finite inputs
    if !finite(h): h ← lastValidHigh
    if !finite(l): l ← lastValidLow

    highBuffer.Add(h)
    lowBuffer.Add(l)

    result ← (highBuffer.Max() + lowBuffer.Min()) × 0.5
    return result
```

### Output Interpretation

| Context | Meaning |
|---------|---------|
| Price > MIDPRICE | Trading in the upper half of the $N$-bar channel |
| Price < MIDPRICE | Trading in the lower half of the $N$-bar channel |
| MIDPRICE rising | Channel shifting upward (uptrend) |
| MIDPRICE flat | Range-bound market; channel stable |
| MIDPRICE converging with price | Trend exhaustion; approaching channel center |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|:-----:|:-------------:|:--------:|
| RingBuffer.Add (high) | 1 | ~3 | 3 |
| RingBuffer.Add (low) | 1 | ~3 | 3 |
| RingBuffer.Max() scan | $N$ | ~$N$ | $N$ |
| RingBuffer.Min() scan | $N$ | ~$N$ | $N$ |
| ADD (max+min) | 1 | 1 | 1 |
| MUL (× 0.5) | 1 | 3 | 3 |
| **Total (hot)** | **$2N+4$** | | **~$2N + 10$ cycles** |

For period=14: approximately 38 cycles per bar.

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Partial: max/min scans are sequential per window; final midpoint is vectorizable |
| Optimal strategy | Monotonic deque for $O(1)$ amortized max/min (not yet implemented) |
| Memory | $O(N)$: two RingBuffers of size $N$ |
| Throughput | Dominated by max/min scans; ~5x slower than stateless transforms at period=14 |

### Potential Optimization

A monotonic deque (sliding window max/min) would reduce per-bar cost from $O(N)$ to $O(1)$ amortized. This is a known optimization path stored for future implementation when profiling shows MIDPRICE as a bottleneck in production pipelines.

## Resources

- **TA-Lib** `TA_MIDPRICE` function reference.
- **Donchian, R.** "High Finance in Copper." *Financial Analysts Journal*, 1960. (Origin of channel-based price analysis)
- **Achelis, S.B.** *Technical Analysis from A to Z*. McGraw-Hill, 2000.
