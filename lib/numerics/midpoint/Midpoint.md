# MIDPOINT: Rolling Range Midpoint

> "The center of the range is where price gravitates—equilibrium between bulls and bears."

MIDPOINT calculates the midpoint of the rolling range: (Highest + Lowest) / 2. This represents the equilibrium price level within the lookback window. Validated against TA-Lib MIDPOINT function.

## Historical Context

The range midpoint appears throughout technical analysis as a mean-reversion anchor. Donchian Channel centerlines, pivot points, and equilibrium theories all reference the arithmetic mean of high and low extremes. The concept predates computers—floor traders mentally tracked "the middle of the range" to identify fair value.

The calculation itself is trivial: average the maximum and minimum. The efficiency challenge lies in computing those extremes. QuanTAlib composes MIDPOINT from HIGHEST and LOWEST, each using O(1) amortized monotonic deque algorithms, yielding O(1) amortized midpoint updates.

## Architecture & Physics

### 1. Composition Pattern

MIDPOINT internally maintains two child indicators:

$$
\text{Midpoint}_t = \frac{\text{Highest}_t + \text{Lowest}_t}{2}
$$

Both HIGHEST and LOWEST use monotonic deques independently.

### 2. Data Flow

```
Input Value
    │
    ├──► Highest (monotonic decreasing deque) ──► max
    │
    └──► Lowest (monotonic increasing deque) ──► min
                                                  │
                            (max + min) × 0.5 ◄───┘
                                    │
                                    ▼
                               Midpoint
```

### 3. State Synchronization

When `isNew=false`:
1. Both child indicators receive the correction
2. Each rebuilds its deque independently
3. Midpoint recalculates from corrected extremes

State consistency is maintained through delegation.

## Mathematical Foundation

### Midpoint Definition

$$
\text{Midpoint}_t = \frac{\max_{i \in [t-n+1, t]} V_i + \min_{i \in [t-n+1, t]} V_i}{2}
$$

### Equivalent Formulation

$$
\text{Midpoint}_t = \text{Lowest}_t + \frac{\text{Range}_t}{2}
$$

where $\text{Range}_t = \text{Highest}_t - \text{Lowest}_t$.

### Properties

- **Bounds**: $\text{Lowest}_t \leq \text{Midpoint}_t \leq \text{Highest}_t$
- **Symmetry**: Equidistant from both extremes by definition
- **Range relationship**: $\text{Midpoint} = \text{Lowest} + 0.5 \times \text{Range}$

## Performance Profile

### Operation Count (Streaming Mode, Amortized)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Highest update | 1 | ~14 | 14 |
| Lowest update | 1 | ~14 | 14 |
| ADD | 1 | 1 | 1 |
| MUL | 1 | 3 | 3 |
| **Total** | **4** | — | **~32 cycles** |

### Batch Mode Optimization

The span-based Calculate method can:
1. Compute HIGHEST for entire series
2. Compute LOWEST for entire series
3. Vectorize `(high[i] + low[i]) * 0.5` using SIMD

Steps 1-2 are sequential (deque-based), but step 3 is embarrassingly parallel.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic |
| **Timeliness** | 8/10 | Lags turning points |
| **Smoothness** | 4/10 | Smoother than raw H/L but still stepped |
| **Computational Cost** | 9/10 | 2× deque overhead |
| **Memory** | 6/10 | 2× buffer memory |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib MIDPOINT** | ✅ | Exact match |
| **Internal Consistency** | ✅ | (Highest + Lowest) / 2 verified |
| **Known Values** | ✅ | Manual verification |

## Common Pitfalls

1. **Not a Moving Average**: MIDPOINT tracks range center, not price average. A trending series may have midpoint above or below mean price.

2. **Step Changes**: When either extreme expires from the window, midpoint jumps. This creates non-smooth transitions.

3. **Double Memory**: Maintains two ring buffers internally. For period=200: ~6.4KB total.

4. **Mean-Reversion Assumption**: Using midpoint as "fair value" assumes bounded ranges. In trending markets, price may stay above/below midpoint for extended periods.

5. **Warmup Period**: `IsHot` becomes true after `period` values. Before warmup, computes midpoint of available data.

6. **Difference from MEDPRICE**: MIDPOINT uses rolling H/L extremes. TA-Lib MEDPRICE uses single-bar (High + Low) / 2. These are distinct calculations.

## References

- Donchian, Richard D. (1960). "High Finance in Copper." Financial Analysts Journal.
- TA-Lib: MIDPOINT function documentation.
- Murphy, John J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
