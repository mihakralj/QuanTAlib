# MIDPOINT: Rolling Range Midpoint

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Midpoint)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- Single-series rolling midpoint: `(Highest(V, N) + Lowest(V, N)) * 0.5`.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The center holds, but only for the window you're watching." — Statistical folk wisdom

Single-series rolling midpoint: `(Highest(V, N) + Lowest(V, N)) * 0.5`. Returns the center of the value range within a lookback window. TA-Lib compatible (`MIDPOINT` function). Unlike MIDPRICE which operates on separate High/Low bar channels, MIDPOINT operates on a single value series.

## Historical Context

The midpoint of a rolling range is one of the simplest channel-center calculations in technical analysis. It appears in virtually every charting platform as the baseline for range-based indicators. TA-Lib implements it as `MIDPOINT` (single series) vs `MIDPRICE` (dual H/L series). The distinction matters: MIDPOINT feeds any single-valued series through a rolling window, while MIDPRICE decomposes OHLC bars into separate high/low channels.

## Architecture and Physics

### 1. RingBuffer Pattern

Uses a single `RingBuffer(period)` to store the last N values. On each update, the buffer provides `Max()` and `Min()` for the rolling window. This is self-contained with no external indicator dependencies.

### 2. Data Flow

```text
Input(value) --> NaN guard --> RingBuffer.Add(v, isNew)
                                  |
                           (Max() + Min()) * 0.5
                                  |
                               Output
```

### 3. State Synchronization

Uses the standard `_s` / `_ps` state local copy pattern for bar correction (`isNew = false`). The `RingBuffer.Add(v, isNew)` call handles rollback internally when `isNew` is false.

## Mathematical Foundation

### Midpoint Definition

$$
\text{MIDPOINT}(N) = \frac{\max(V_0, V_1, \ldots, V_{N-1}) + \min(V_0, V_1, \ldots, V_{N-1})}{2}
$$

### Equivalent Formulation

$$
\text{MIDPOINT}(N) = \min(V, N) + \frac{\text{range}(V, N)}{2}
$$

where $\text{range}(V, N) = \max(V, N) - \min(V, N)$.

### Properties

- **Bounded:** Always between the minimum and maximum of the window
- **Idempotent on constants:** If all values equal $c$, midpoint equals $c$
- **Lag:** Responds only when the max or min of the window changes

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count |
|-----------|-------|
| Comparison (Max scan) | $O(N)$ per update |
| Comparison (Min scan) | $O(N)$ per update |
| Addition | 1 |
| Multiplication | 1 |
| **Total** | $O(N)$ |

### Batch Mode

The span-based `Batch` method uses a single `RingBuffer` with linear scan for max/min. For large datasets, amortized cost is $O(N \cdot P)$ where $P$ is the period.

### Quality Metrics

| Metric | Score |
|--------|-------|
| Simplicity | 9/10 |
| Responsiveness | 5/10 |
| Smoothness | 3/10 |
| SIMD potential | Low (sequential max/min dependency) |

## Validation

| Library | Function | Match | Notes |
|---------|----------|-------|-------|
| TA-Lib | `MIDPOINT` | Exact (1e-10) | Batch + Streaming + Span validated |

## Common Pitfalls

1. **Confusing MIDPOINT with MIDPRICE:** MIDPOINT takes a single value series; MIDPRICE takes separate High/Low channels from bars.
2. **Window lag:** The midpoint only changes when the rolling max or min changes. It can remain flat for extended periods.
3. **NaN propagation:** Implementation substitutes last-valid value for NaN/Infinity inputs to prevent corruption.
4. **Period = 1:** Returns the input value unchanged (max = min = value).
5. **Warmup:** First `period - 1` values use a partial window (fewer than N values).

## References

- TA-Lib `MIDPOINT` function documentation
- Murphy, J. *Technical Analysis of the Financial Markets* (range-based indicators)
