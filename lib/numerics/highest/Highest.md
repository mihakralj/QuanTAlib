# HIGHEST: Rolling Maximum

> *What's the peak? The answer to that question defines support, resistance, and breakout levels.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Highest)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [highest.pine](highest.pine)                       |

- HIGHEST calculates the maximum value over a rolling lookback window.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HIGHEST calculates the maximum value over a rolling lookback window. This O(1) amortized streaming implementation uses a monotonic deque algorithm, enabling real-time updates without re-scanning the entire window. Validated against TA-Lib MAX and Tulip max functions.

## Historical Context

Rolling maximum is a foundational concept in technical analysis, underpinning Donchian Channels, breakout detection, and trailing stop calculations. The naive approach scans all values in the window on each update—O(n) per bar. For a 200-period window processing 10,000 bars, that's 2 million comparisons.

The monotonic deque algorithm reduces this to O(1) amortized time by maintaining a decreasing sequence of candidates. Only values that could potentially be the maximum are kept; smaller values that can never become maximum (because they'll expire before the larger values) are discarded.

QuanTAlib implements this optimal algorithm with full streaming support, SIMD batch optimization, and proper state management for bar corrections.

## Architecture & Physics

### 1. Monotonic Deque

The core data structure is a deque maintaining indices of values in monotonically decreasing order:

$$
\text{deque} = [i_1, i_2, \ldots, i_k] \quad \text{where} \quad V_{i_1} \geq V_{i_2} \geq \cdots \geq V_{i_k}
$$

The front of the deque always holds the index of the maximum value in the current window.

### 2. Update Algorithm

On each new value $V_t$:

1. **Remove expired**: Pop indices from front if `index <= t - period`
2. **Maintain monotonicity**: Pop indices from back while `V[back] <= V_t`
3. **Add new**: Push current index $t$ to back
4. **Result**: Front of deque is the maximum's index

```
Window: [3, 7, 2, 5, 4]  Period: 5
Deque:  [1]              // Index 1 holds 7 (max)
        
Add 6 at index 5:
Deque:  [1, 5]           // 7 > 6, keep both
        
Add 9 at index 6:
Deque:  [6]              // 9 > 7 > 6, 9 dominates all
```

### 3. Bar Correction via Rollback

When `isNew=false`, the indicator:
1. Restores previous state (`_state = _p_state`)
2. Replaces the last value in the buffer
3. Rebuilds the deque by scanning the buffer

This maintains correctness for real-time bar updates.

## Mathematical Foundation

### Rolling Maximum Definition

$$
\text{Highest}_t = \max(V_{t-n+1}, V_{t-n+2}, \ldots, V_t)
$$

where $n$ is the lookback period.

### Partial Window Behavior

Before the window is full:

$$
\text{Highest}_t = \max(V_0, V_1, \ldots, V_t) \quad \text{for } t < n
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
| **Accuracy** | 10/10 | Exact maximum |
| **Timeliness** | 10/10 | Zero lag for maxima |
| **Smoothness** | 2/10 | Step changes at window boundaries |
| **Computational Cost** | 9/10 | O(1) amortized |
| **Memory** | 7/10 | O(n) for buffer + deque |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib MAX** | ✅ | Exact match |
| **Tulip max** | ✅ | Exact match |
| **Known Values** | ✅ | Manual verification |

## Common Pitfalls

1. **Window Boundary Effects**: Maximum changes abruptly when the previous max expires from the window. This creates step changes in the output.

2. **Warmup Period**: `IsHot` becomes true after `period` values. Before warmup, returns maximum of available data.

3. **Memory Footprint**: O(n) memory for both the ring buffer and deque indices. For period=200: ~3.2KB (200 doubles + 200 ints).

4. **Deque Rebuild on Correction**: When `isNew=false`, the entire deque is rebuilt by scanning the buffer. Frequent corrections are O(n) each.

5. **Large Periods**: For very large periods (>1000), consider segment trees or sparse tables if corrections are rare. The deque approach optimizes for the streaming case.

6. **Using isNew Incorrectly**: Use `isNew: false` only when correcting the current bar. New bars must use `isNew: true`.

## References

- Tarjan, Robert E. (1985). "Amortized Computational Complexity." SIAM Journal on Algebraic Discrete Methods.
- Lemire, Daniel. (2006). "Streaming Maximum-Minimum Filter Using No More than Three Comparisons per Element."
- TA-Lib: MAX function documentation.
