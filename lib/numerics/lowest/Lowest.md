# LOWEST: Rolling Minimum

> *Know your floor. Support levels are just historical minimums waiting to be tested.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Lowest)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [lowest.pine](lowest.pine)                       |

- LOWEST calculates the minimum value over a rolling lookback window.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

LOWEST calculates the minimum value over a rolling lookback window. This O(1) amortized streaming implementation uses a monotonic deque algorithm, enabling real-time updates without re-scanning the entire window. Validated against TA-Lib MIN and Tulip min functions.

## Historical Context

Rolling minimum is fundamental to technical analysis—support detection, drawdown calculation, and trailing stop placement all depend on tracking minimum values efficiently. The naive approach scans all values in the window on each update, requiring O(n) time per bar.

The monotonic deque algorithm, popularized by Lemire (2006), reduces this to O(1) amortized time by maintaining an increasing sequence of candidates. Values that can never become the minimum (because they're larger and will expire before smaller values) are immediately discarded.

QuanTAlib implements this optimal algorithm with full streaming support, SIMD batch optimization, and proper state management for bar corrections.

## Architecture & Physics

### 1. Monotonic Deque

The core data structure is a deque maintaining indices of values in monotonically increasing order:

$$
\text{deque} = [i_1, i_2, \ldots, i_k] \quad \text{where} \quad V_{i_1} \leq V_{i_2} \leq \cdots \leq V_{i_k}
$$

The front of the deque always holds the index of the minimum value in the current window.

### 2. Update Algorithm

On each new value $V_t$:

1. **Remove expired**: Pop indices from front if `index <= t - period`
2. **Maintain monotonicity**: Pop indices from back while `V[back] >= V_t`
3. **Add new**: Push current index $t$ to back
4. **Result**: Front of deque is the minimum's index

```
Window: [5, 2, 7, 3, 6]  Period: 5
Deque:  [1, 3]           // Index 1=2 (min), Index 3=3
        
Add 4 at index 5:
Deque:  [1, 3, 5]        // 2 < 3 < 4, keep all
        
Add 1 at index 6:
Deque:  [6]              // 1 < all others, 1 dominates
```

### 3. Bar Correction via Rollback

When `isNew=false`, the indicator:
1. Restores previous state (`_state = _p_state`)
2. Replaces the last value in the buffer
3. Rebuilds the deque by scanning the buffer

This maintains correctness for real-time bar updates.

## Mathematical Foundation

### Rolling Minimum Definition

$$
\text{Lowest}_t = \min(V_{t-n+1}, V_{t-n+2}, \ldots, V_t)
$$

where $n$ is the lookback period.

### Partial Window Behavior

Before the window is full:

$$
\text{Lowest}_t = \min(V_0, V_1, \ldots, V_t) \quad \text{for } t < n
$$

### Complexity Analysis

| Operation | Naive | Monotonic Deque |
| :--- | :---: | :---: |
| Per-update (worst) | O(n) | O(n) |
| Per-update (amortized) | O(n) | O(1) |
| Total for N updates | O(N×n) | O(N) |

Each element is pushed and popped from the deque at most once across all operations.

## Performance Profile

### Operation Count (Streaming Mode, Amortized)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (expired check) | 1 | 1 | 1 |
| CMP (monotonicity) | ~2 avg | 1 | 2 |
| Array access | 3 | 3 | 9 |
| Index arithmetic | 2 | 1 | 2 |
| **Total** | **~8** | — | **~14 cycles** |

### Batch Mode (SIMD)

For batch processing, SIMD can parallelize comparisons within segments. However, the monotonic deque's sequential nature limits full vectorization. The span-based Calculate method uses a stackalloc deque buffer for cache efficiency.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact minimum |
| **Timeliness** | 10/10 | Zero lag for minima |
| **Smoothness** | 2/10 | Step changes at window boundaries |
| **Computational Cost** | 9/10 | O(1) amortized |
| **Memory** | 7/10 | O(n) for buffer + deque |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib MIN** | ✅ | Exact match |
| **Tulip min** | ✅ | Exact match |
| **Known Values** | ✅ | Manual verification |

## Common Pitfalls

1. **Window Boundary Effects**: Minimum changes abruptly when the previous min expires from the window. This creates step changes in the output.

2. **Warmup Period**: `IsHot` becomes true after `period` values. Before warmup, returns minimum of available data.

3. **Memory Footprint**: O(n) memory for both the ring buffer and deque indices. For period=200: ~3.2KB (200 doubles + 200 ints).

4. **Deque Rebuild on Correction**: When `isNew=false`, the entire deque is rebuilt by scanning the buffer. Frequent corrections are O(n) each.

5. **Support Level Detection**: The minimum often acts as support, but LOWEST reports raw values, not significance levels. Consider combining with volume or multiple timeframes.

6. **Using isNew Incorrectly**: Use `isNew: false` only when correcting the current bar. New bars must use `isNew: true`.

## References

- Tarjan, Robert E. (1985). "Amortized Computational Complexity." SIAM Journal on Algebraic Discrete Methods.
- Lemire, Daniel. (2006). "Streaming Maximum-Minimum Filter Using No More than Three Comparisons per Element."
- TA-Lib: MIN function documentation.
