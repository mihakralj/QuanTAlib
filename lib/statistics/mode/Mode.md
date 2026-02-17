# MODE: Statistical Mode (Most Frequent Value)

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
