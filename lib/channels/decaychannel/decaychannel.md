# DECAYCHANNEL: Decay Min-Max Channel

> "Yesterday's high matters less today. Tomorrow, it matters even less. Decay channels know this."

Decay Min-Max Channel (DECAYCHANNEL) tracks the highest high and lowest low like Donchian, then applies exponential decay toward the midpoint. Fresh extremes snap the bands outward; time compresses them inward. The result: channels that respect recent price action while gradually forgetting stale levels. This implementation uses true half-life mathematics—50% convergence over the period length—ensuring predictable decay behavior across all timeframes.

## Historical Context

Traditional Donchian Channels treat all extremes within the lookback window equally. A high from 19 bars ago has the same influence as a high from 1 bar ago. This works for breakout detection but creates artificial support/resistance levels that persist until they mechanically exit the window.

Traders noticed this rigidity. A 20-day high from exactly 20 days ago shouldn't matter as much as one from 5 days ago. Various "adaptive channel" approaches emerged in the 1990s-2000s, but most used arbitrary decay rates or complex volatility weighting.

DECAYCHANNEL takes a simpler approach: pure exponential decay with mathematically defined half-life. The decay constant $\lambda = \ln(2) / \text{period}$ guarantees that bands converge 50% toward the midpoint over exactly one period. After two periods: 75%. After three: 87.5%. No tuning parameters, no volatility lookups—just consistent, predictable decay.

## Architecture & Physics

DECAYCHANNEL consists of four interconnected components that balance extreme tracking with temporal decay.

### 1. Extreme Tracking (Highest/Lowest)

Internal Highest and Lowest indicators maintain the actual max/min over the period:

$$
H_t^{raw} = \max_{i=0}^{n-1}(High_{t-i})
$$

$$
L_t^{raw} = \min_{i=0}^{n-1}(Low_{t-i})
$$

These raw values constrain the decayed bands—the upper band can never exceed the actual highest high, and the lower band can never go below the actual lowest low.

### 2. Decay Timers

Separate counters track how long since each band was reset by a new extreme:

$$
\tau_U = \text{bars since } High_t = H_t^{raw}
$$

$$
\tau_L = \text{bars since } Low_t = L_t^{raw}
$$

When price makes a new extreme, the corresponding timer resets to zero. Otherwise, it increments each bar.

### 3. Exponential Decay Engine

The decay rate uses the half-life formula:

$$
\lambda = \frac{\ln(2)}{\text{period}}
$$

For each bar, compute the decay factor based on elapsed time:

$$
d_U = 1 - e^{-\lambda \cdot \tau_U}
$$

$$
d_L = 1 - e^{-\lambda \cdot \tau_L}
$$

At $\tau = 0$ (new extreme), $d = 0$ (no decay). At $\tau = \text{period}$, $d = 0.5$ (half decayed).

### 4. Midpoint Convergence

Bands decay toward the current midpoint, not toward price:

$$
M_t = \frac{U_{t-1} + L_{t-1}}{2}
$$

$$
U_t = U_{t-1} - d_U \cdot (U_{t-1} - M_t)
$$

$$
L_t = L_{t-1} + d_L \cdot (M_t - L_{t-1})
$$

Finally, constrain to actual extremes:

$$
U_t = \max(U_t, H_t^{raw})
$$

$$
L_t = \min(L_t, L_t^{raw})
$$

## Mathematical Foundation

### Half-Life Derivation

Exponential decay follows:

$$
V(t) = V_0 \cdot e^{-\lambda t}
$$

For half-life $t_{1/2}$ where $V(t_{1/2}) = \frac{V_0}{2}$:

$$
\frac{V_0}{2} = V_0 \cdot e^{-\lambda t_{1/2}}
$$

$$
\lambda = \frac{\ln(2)}{t_{1/2}}
$$

Setting $t_{1/2} = \text{period}$ gives the implementation's decay constant.

### Convergence Schedule

| Elapsed Time | Decay Factor | Remaining Distance |
| :--- | :---: | :---: |
| 0 bars | 0% | 100% |
| period/2 bars | 29.3% | 70.7% |
| period bars | 50% | 50% |
| 2×period bars | 75% | 25% |
| 3×period bars | 87.5% | 12.5% |

### Middle Band Calculation

The output middle band is the average of the decayed upper and lower bands:

$$
Middle_t = \frac{U_t + L_t}{2}
$$

This differs from the convergence midpoint (which uses previous bar's values) to avoid feedback loops.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar cost including internal Highest/Lowest updates:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 8 | 1 | 8 |
| MUL | 4 | 3 | 12 |
| DIV | 1 | 15 | 15 |
| EXP | 2 | 50 | 100 |
| CMP/MAX/MIN | 6 | 1 | 6 |
| **Total** | **21** | — | **~141 cycles** |

**Breakdown:**

- Lambda: precomputed at construction (0 cycles per bar)
- Midpoint: 1 ADD + 1 DIV = 16 cycles
- Decay factors (×2): 2 MUL + 2 EXP + 2 SUB = 106 cycles
- Band updates: 2 MUL + 2 SUB = 8 cycles
- Constraint checks: 4 CMP = 4 cycles
- Internal Highest/Lowest: ~8 cycles (amortized O(1))

**Dominant cost:** EXP operations at 71% of total cycles.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Benefit | Notes |
| :--- | :---: | :---: | :--- |
| Decay calculation | 2 | Limited | Sequential dependency on timers |
| Band update | 4 | 2× via FMA | `band - decay × (band - mid)` |
| Max/Min constraint | 4 | 1× | Comparison-based |

**Batch efficiency (512 bars):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 141 | 72,192 | — |
| FMA-optimized | ~135 | ~69,120 | **~4%** |

Limited improvement due to:

1. **EXP dominates**: 100 of 141 cycles are exponential operations (not SIMD-friendly in scalar mode)
2. **Timer dependency**: Each bar's decay factor depends on its timer value
3. **State coupling**: Upper/lower bands depend on previous bar's midpoint

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Mathematically exact exponential decay |
| **Timeliness** | 8/10 | Immediate response to new extremes |
| **Overshoot** | 6/10 | New extremes reset decay, can spike bands |
| **Smoothness** | 7/10 | Exponential decay provides smooth convergence between resets |
| **Adaptivity** | 8/10 | Channels naturally tighten during consolidation |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Internal** | ✅ | Four-mode consistency verified (streaming, batch, span, event) |

DECAYCHANNEL is a QuanTAlib-specific indicator with no external reference implementations.

## Common Pitfalls

1. **Decay Rate Confusion**: The period parameter controls half-life, not full decay. At period=100, bands are 50% decayed after 100 bars, not fully converged. For near-complete convergence (>95%), allow 4-5× the period.

2. **Constraint Snap-Back**: When the actual highest high drops (because an old extreme exits the Highest window), the upper band can snap downward even mid-decay. This is intentional—decayed bands never exceed actual extremes.

3. **Initialization Period**: DECAYCHANNEL needs `period` bars to establish meaningful extremes before decay becomes relevant. IsHot reflects this warmup requirement.

4. **Timer State Management**: Using `isNew=false` for bar correction requires restoring both the band values and the decay timers. The implementation handles this via state snapshots, but improper use corrupts both.

5. **Midpoint Targeting**: Bands decay toward the channel midpoint, not toward current price. In strong trends, this means the trailing band decays toward a point that may be far from price, creating asymmetric behavior.

6. **Memory Overhead**: Each instance maintains two Highest/Lowest indicators plus decay state. For period=100, budget ~1.6 KB per instance for the internal monotonic deques plus ~64 bytes for state.

7. **Exponential Sensitivity**: Small period values create aggressive decay. At period=10, bands are 50% converged after just 10 bars. For most applications, period≥50 provides more stable channels.

## References

- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Kaufman, P. J. (2013). *Trading Systems and Methods* (5th ed.). John Wiley & Sons.
- Press, W. H., et al. (2007). *Numerical Recipes: The Art of Scientific Computing* (3rd ed.). Cambridge University Press. [Exponential decay mathematics]
