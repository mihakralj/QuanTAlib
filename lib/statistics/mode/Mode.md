# MODE: Statistical Mode (Most Frequent Value)

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Mode)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- The **Mode** is a rolling statistical indicator that identifies the most frequently occurring value within a sliding window of recent observations.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The mode is the value that appears most frequently in a data set — the only measure of central tendency that tells you what's actually popular, not what's average."

## Introduction

The **Mode** is a rolling statistical indicator that identifies the most frequently occurring value within
a sliding window of recent observations. Unlike the mean and median, which find the center of a
distribution through arithmetic, the mode finds it through frequency counting. For financial data
this means identifying price levels where the market has spent the most time — a concept with direct
implications for support/resistance identification.

## Historical Context

The mode predates formal statistics. Early astronomers used it to identify the "true" value among
repeated measurements. In modern finance, the concept maps directly to volume profile analysis
(price-at-time histograms) and Point of Control (POC) calculations, though those typically bin
continuous data while this implementation uses exact value comparison matching the PineScript reference.

## Mathematical Foundation

Given a window of $n$ values $\{x_1, x_2, \ldots, x_n\}$:

$$\text{Mode} = \arg\max_{v} \sum_{i=1}^{n} \mathbf{1}(x_i = v)$$

Where $\mathbf{1}(x_i = v)$ is the indicator function returning 1 when $x_i = v$.

**Special cases:**

- Single value in window: returns that value
- All values distinct ($n > 1$): returns `NaN` (no mode exists)
- Multimodal (tie): returns the smallest mode (first in sorted order)

## Architecture

### Sorted Window Approach

The implementation maintains a sorted buffer using `BinarySearch` + `Array.Copy` for O(N) insert/remove.
After each update, a single linear scan of the sorted buffer identifies the longest consecutive run
of equal values. This is more efficient than a dictionary approach for small-to-medium periods because
it avoids hashing overhead and GC pressure from dictionary internals.

### State Management

| Component | Purpose |
|-----------|---------|
| `RingBuffer _buffer` | Circular buffer tracking insertion order (for sliding window eviction) |
| `double[] _sortedBuffer` | Values maintained in sorted order for O(N) mode finding |
| `double[] _p_sortedBuffer` | Snapshot for `isNew=false` bar correction rollback |

### Complexity

| Operation | Time | Space |
|-----------|------|-------|
| `Update` (streaming) | O(N) | O(N) |
| `Batch` (span) | O(M·N) | O(N) |

Where N = period, M = total data points.

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `period` | `int` | — | Rolling window size (must be > 0) |

## Usage

```csharp
// Streaming mode
var mode = new Mode(14);
TValue result = mode.Update(new TValue(DateTime.UtcNow, price));

// Batch mode
TSeries results = Mode.Batch(series, 14);

// Span mode (zero-allocation output)
Mode.Batch(sourceSpan, outputSpan, 14);
```

## Interpretation

| Condition | Meaning |
|-----------|---------|
| Mode = specific value | Market spent most time at this price level |
| Mode = NaN | All values unique — no dominant price level |
| Mode stable across windows | Strong support/resistance at that level |
| Mode shifting | Distribution center is moving |

## Common Pitfalls

1. **Continuous data produces NaN**: Floating-point prices with many decimals rarely repeat exactly. Mode is most useful for rounded/discretized data (e.g., tick prices, integer values).
2. **Bimodal ties**: When multiple values share the highest frequency, the smallest value wins (first in sorted order). This is deterministic but may not match all statistical software.
3. **Period = 1**: Always returns the input value (trivially the mode).
4. **NaN inputs**: NaN values are stored in the buffer. If a window contains NaN duplicates, NaN could become the mode — this matches the PineScript behavior.
5. **Performance**: O(N) per update due to sorted buffer maintenance. For very large periods (>1000), consider if mode is the right tool.


## Performance Profile

### Operation Count (Streaming Mode)

Mode uses a sorted insertion buffer; each bar requires a binary search plus array shift to maintain sort order, then a linear scan to find the longest run.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer evict oldest | 1 | 3 cy | ~3 cy |
| Binary search insert position | log2(N) | 2 cy | ~14 cy (N=128) |
| Array.Copy shift | N/2 avg | 1 cy | ~64 cy (N=128) |
| Linear scan for mode | N | 2 cy | ~256 cy (N=128) |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(N)** | — | **~340 cy (N=128)** |

O(N) per update due to sorted buffer maintenance. Not suitable for periods >1000 in tick-streamed hot paths; use a hash-counted alternative for large windows.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Sort per window | No | Comparison sort; not SIMD-friendly |
| Linear run scan | Partial | Branchless equality check possible |
| Outer loop over M bars | No | Each bar re-sorts; sequential dependency |

No SIMD benefit — sorting and frequency counting are inherently sequential for exact-match mode. Batch complexity O(M·N log N) where M = data length, N = period.

## Validation

Self-consistency validation only — no external library provides rolling mode.
Verified against Wolfram Alpha for static datasets.

| Test | Status |
|------|--------|
| Wolfram Alpha {1,2,2,3,3,3,4} | ✔️ mode = 3 |
| Batch == Streaming == Span | ✔️ |
| Bar correction (isNew=false) | ✔️ |

## References

- PineScript reference: `mode.pine` (exact value comparison, map-based counting)
- Wolfram MathWorld: [Statistical Mode](https://mathworld.wolfram.com/Mode.html)
