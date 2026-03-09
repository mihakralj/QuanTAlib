# MININDEX: Rolling Minimum Index

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default=14, min=2)                      |
| **Outputs**      | Single series (Minindex)                       |
| **Output range** | Streaming: 0 to period-1 (bars-ago); Batch span: absolute array index |
| **Warmup**       | `period` bars                          |

### TL;DR

- MININDEX finds the position (index) of the minimum value within a rolling lookback window.
- Parameterized by `period` (minimum 2).
- Streaming mode outputs bars-ago offset (0 = current bar holds the min, period-1 = oldest bar).
- Batch span mode outputs absolute array indices (TA-Lib MININDEX compatible).
- Tie-breaking: last occurrence wins (most recent bar, `<=` comparison).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Cross-validation: `source[Minindex.Batch[i]] == Lowest.Batch[i]` for all bars after warmup.

> "Finding support isn't just about the price — it's about *when* the floor was set."

MININDEX identifies the position of the minimum value within a rolling window. While LOWEST tells you the trough *value*, MININDEX tells you *where* that trough is relative to the current bar. This is essential for support analysis, timing studies, and detecting how "stale" a low is.

## Historical Context

The MININDEX function originates from TA-Lib (TA_MININDEX), used in quantitative trading systems to identify when the lowest price in a lookback window occurred. This timing information is critical for:

- **Support freshness**: A min at position 0 means support is being tested *now*; at position period-1, the low is stale and potentially irrelevant.
- **Pattern detection**: Identifying double bottoms, inverse head-and-shoulders, and other formations requires knowing *when* troughs occurred.
- **Exhaustion analysis**: The position of the low within the window indicates whether selling pressure is current or historical.

## Architecture & Physics

### 1. Streaming Mode — Bars-Ago Offset

In streaming mode, the output represents how many bars ago the minimum occurred:

$$
\text{Minindex}_t = t - \arg\min_{t-n+1 \leq k \leq t} V_k
$$

where $n$ is the lookback period. A value of 0 means the current bar is the minimum; a value of $n-1$ means the oldest bar in the window holds the minimum.

### 2. Batch Span Mode — Absolute Index

In the `Batch(ReadOnlySpan)` method, output is the absolute array index:

$$
\text{output}[i] = \arg\min_{i-n+1 \leq k \leq i} V_k
$$

This matches TA-Lib's MININDEX convention and enables direct array lookup: `source[output[i]]` yields the minimum value.

### 3. Tie-Breaking

When multiple values in the window are equal to the minimum, the **most recent** (rightmost) occurrence wins:

$$
\text{Minindex}_t = \max \{ k : V_k = \min(\text{window}) \}
$$

This is achieved using `<=` comparison, matching TA-Lib behavior.

### 4. Monotonic Deque (Batch Mode)

The batch span method uses the same O(n) monotonic deque algorithm as Lowest, but outputs the index stored at the deque head rather than the value at that index:

```
// Lowest:   output[i] = values[deque.PeekHead()]  → the VALUE
// Minindex: output[i] = deque.PeekHead()           → the INDEX
```

### 5. Bar Correction via Rollback

When `isNew=false`, the indicator:
1. Restores previous state (`_state = _p_state`)
2. Replaces the last value in the buffer
3. Re-scans the buffer to find the new minimum position

## Mathematical Foundation

### Rolling Minimum Index Definition

$$
\text{Minindex}_t = \arg\min_{t-n+1 \leq k \leq t} V_k
$$

where $n$ is the lookback period and ties are broken in favor of the most recent occurrence.

### Partial Window Behavior

Before the window is full:

$$
\text{Minindex}_t = \arg\min_{0 \leq k \leq t} V_k \quad \text{for } t < n
$$

### Complexity Analysis

| Operation | Streaming | Batch (Deque) |
| :--- | :---: | :---: |
| Per-update (worst) | O(n) | O(n) |
| Per-update (amortized) | O(n) | O(1) |
| Total for N updates | O(N×n) | O(N) |

Streaming uses a linear scan of the RingBuffer, which is O(period) per bar — acceptable for typical periods (5–30). Batch mode uses the monotonic deque for O(1) amortized.

## Performance Profile

### Streaming Mode (Linear Scan)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (scan) | period | 1 | period |
| Array access | period | 3 | 3×period |
| Index arithmetic | 2 | 1 | 2 |
| **Total** | — | — | **~4×period cycles** |

### Batch Mode (Monotonic Deque)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (expired check) | 1 | 1 | 1 |
| CMP (monotonicity) | ~2 avg | 1 | 2 |
| Array access | 3 | 3 | 9 |
| Index arithmetic | 2 | 1 | 2 |
| **Total** | **~8** | — | **~14 cycles** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact index of minimum |
| **Timeliness** | 10/10 | Zero lag for index detection |
| **Smoothness** | 2/10 | Discrete jumps as window slides |
| **Computational Cost** | 8/10 | O(period) streaming, O(1) batch |
| **Memory** | 7/10 | O(n) for buffer + deque |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib MININDEX** | ✅ | Batch span output matches absolute indices |
| **Cross-validation** | ✅ | `source[Minindex[i]] == Lowest[i]` for all valid bars |
| **Known Values** | ✅ | Manual verification |

## Common Pitfalls

1. **Two Output Modes**: Streaming returns bars-ago offset; Batch(ReadOnlySpan) returns absolute array index. Do not mix them up.

2. **Period Minimum is 2**: Unlike Lowest (which accepts period=1), Minindex requires period >= 2, since the index of a single element is trivially 0.

3. **Tie-Breaking**: Uses `<=` so the most recent (rightmost) occurrence wins ties. This matches TA-Lib convention.

4. **Window Boundary Effects**: When the previous min expires from the window, the index can jump abruptly. This is expected behavior.

5. **Warmup Period**: `IsHot` becomes true after `period` values. Before warmup, returns index within available data.

6. **Using isNew Incorrectly**: Use `isNew: false` only when correcting the current bar. New bars must use `isNew: true`.

## References

- TA-Lib: MININDEX function documentation.
- Lemire, Daniel. (2006). "Streaming Maximum-Minimum Filter Using No More than Three Comparisons per Element."
- Tarjan, Robert E. (1985). "Amortized Computational Complexity." SIAM Journal on Algebraic Discrete Methods.
