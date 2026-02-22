# NMA: Natural Moving Average

> "Jim Sloman looked at how volatility distributes across a window and asked: if the most volatile bars are recent, should the filter not respond faster? NMA derives its smoothing constant from the volatility profile itself, weighted by a square-root kernel that emphasizes recent action."

NMA is an adaptive IIR filter whose smoothing ratio is derived from a volatility-weighted square-root kernel analysis of log-price movements over a lookback window. When volatility concentrates in recent bars, the ratio approaches 1.0 (fast tracking). When volatility is spread uniformly, the ratio approaches $1/\sqrt{N}$ (heavy smoothing). The square-root kernel $(\sqrt{i+1} - \sqrt{i})$ gives a concave-down weighting that gently emphasizes recency, while the log-price transformation normalizes for price level, making the adaptation scale-invariant.

## Historical Context

Jim Sloman introduced the Natural Moving Average in *Ocean Theory* (pages 63-70), a book that applied chaos and complexity theory metaphors to financial markets. The NMA was designed as a "natural" filter that lets the market's own volatility structure determine the smoothing rate, rather than imposing an arbitrary period.

The core innovation is the square-root differencing kernel $\sqrt{i+1} - \sqrt{i}$ as the weighting function for volatility. This kernel has the property that its cumulative sum $\sqrt{N}$ grows sublinearly, meaning each additional bar in the lookback contributes less weight than the previous one. This creates a "diminishing returns" effect: extending the lookback adds context without drowning out recent information.

The log-price transformation ($\ln(\text{price}) \times 1000$) serves two purposes: (1) it makes the volatility measure proportional to percentage moves rather than absolute dollar moves, and (2) the scaling factor of 1000 brings typical values into a numerically convenient range for the ratio computation.

NMA belongs to the family of adaptive moving averages alongside KAMA, VIDYA, and ADXVMA, but uses a unique adaptation mechanism based on the spatial distribution of volatility rather than a single efficiency or strength metric.

## Architecture & Physics

### 1. Log-Price Buffer

A circular buffer of size $N+1$ stores $\ln(\text{price}) \times 1000$ for each bar, providing the lookback data for volatility computation.

### 2. Volatility-Weighted Square-Root Ratio

For each bar $i$ in the lookback:

$$
o_i = |\ln_i - \ln_{i+1}|
$$

$$
\text{num} = \sum_{i=0}^{N-1} o_i \cdot \left(\sqrt{i+1} - \sqrt{i}\right)
$$

$$
\text{denom} = \sum_{i=0}^{N-1} o_i
$$

$$
\text{ratio} = \frac{\text{num}}{\text{denom}}
$$

### 3. Adaptive EMA Step

$$
\text{NMA}_t = \text{NMA}_{t-1} + \text{ratio} \times (x_t - \text{NMA}_{t-1})
$$

## Mathematical Foundation

**Log-price volatility:**

$$
o_i = \left|\ln(x_{t-i}) - \ln(x_{t-i-1})\right| \times 1000
$$

**Square-root kernel weights:**

$$
\phi_i = \sqrt{i+1} - \sqrt{i} = \frac{1}{\sqrt{i+1} + \sqrt{i}}
$$

Note: $\phi_i \approx \frac{1}{2\sqrt{i}}$ for large $i$, confirming the $1/\sqrt{i}$ decay rate.

**Adaptive ratio:**

$$
r = \frac{\sum_{i=0}^{N-1} o_i \cdot \phi_i}{\sum_{i=0}^{N-1} o_i}
$$

**Ratio bounds:**

- If all volatility is at $i = 0$ (most recent): $r = \phi_0 = \sqrt{1} - \sqrt{0} = 1$
- If volatility is uniform: $r = \frac{\sum \phi_i}{N} = \frac{\sqrt{N}}{N} = \frac{1}{\sqrt{N}}$
- For $N = 40$: uniform ratio $\approx 0.158$, equivalent to EMA period $\approx 11$

**IIR update:**

$$
\text{NMA}_t = \text{NMA}_{t-1} + r_t \cdot (x_t - \text{NMA}_{t-1})
$$

**Default parameters:** `period = 40`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
// Store scaled log-price
lnBuf[head] = log(src) * 1000

// Compute volatility-weighted ratio
num = 0; denom = 0
for i = 0 to bars-1:
    oi = |lnBuf[t-i] - lnBuf[t-i-1]|
    num   += oi * (sqrt(i+1) - sqrt(i))
    denom += oi

ratio = denom != 0 ? num/denom : 0

// Adaptive EMA step
result = result + ratio * (src - result)
```

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per Update | Notes |
|-----------|-----------------|-------|
| Log | 1 | `Math.Log(price)` |
| Abs | $N$ | `|lnBuf[i] - lnBuf[i+1]|` per lookback step |
| Multiply | $N$ | $o_i \times \phi_i$ |
| Add | $2N + 1$ | Numerator sum + denominator sum + EMA step |
| Divide | 1 | `num / denom` |
| FMA | 1 | `FusedMultiplyAdd(prev, decay, ratio * price)` |
| **Total** | $\approx 4N + 4$ | $N = \text{period}$ |

For `period = 40`: approximately 164 FLOPs per streaming update.

### Batch Mode (SIMD Analysis)

The inner `ComputeRatio()` loop walks backward through the ring buffer with data-dependent indexing, which resists SIMD vectorization. The batch `Calculate(Span)` method uses the same scalar loop per bar.

SIMD opportunity exists for the sqrt-weight precomputation (done once in the constructor), but not for the per-bar ratio computation due to the sequential buffer access pattern.

| Metric | Score |
|--------|-------|
| Streaming latency | 8/10 (O(N) per bar, but small constant) |
| Batch throughput | 5/10 (O(N*M) total, no SIMD in hot loop) |
| Memory efficiency | 9/10 (single RingBuffer + precomputed weights) |
| Warmup speed | 9/10 (hot after N bars) |
| Numerical stability | 7/10 (log-scale amplifies FP drift in corrections; mitigated by CopyFrom pattern) |

### Memory Layout

| Field | Type | Size | Purpose |
|-------|------|------|---------|
| `_lnBuf` | RingBuffer | ~40B + (N+1)x8B | Circular log-price buffer |
| `_p_lnBuf` | RingBuffer | ~40B + (N+1)x8B | Backup buffer for bar correction |
| `_sqrtWeights` | double[] | Nx8B | Precomputed $\sqrt{i+1} - \sqrt{i}$ |
| `_state` | State | 32B | Current NMA, last NMA, bar count, flags |
| `_p_state` | State | 32B | Previous state for rollback |
| **Total** | | ~144B + 3Nx8B | |

For `period = 40`: approximately 144 + 984 = **1128 bytes** per instance.

### Bar Correction Pattern

NMA requires full buffer copy (`CopyFrom`) for bar correction rather than the lighter `Snapshot`/`Restore` used by simpler indicators. The reason: `ComputeRatio()` reads all buffer positions during backward traversal, so a single-value restore is insufficient.

```csharp
if (isNew) { _p_state = _state; _p_lnBuf.CopyFrom(_lnBuf); }
else       { _state = _p_state; _lnBuf.CopyFrom(_p_lnBuf); }
_ = _lnBuf.Add(lnVal); // always Add() since CopyFrom restores pre-Add state
```

## Validation

| Library | Batch | Streaming | Span | Notes |
|---------|-------|-----------|------|-------|
| Skender | N/A | N/A | N/A | Not available |
| TA-Lib | N/A | N/A | N/A | Not available |
| Tulip | N/A | N/A | N/A | Not available |
| Ooples | N/A | N/A | N/A | Not available |

NMA is a proprietary indicator from Sloman's *Ocean Theory*. No reference implementations exist in standard TA libraries. Validation relies on:

- Internal consistency: batch == streaming == span == eventing (4-mode consistency test)
- Mathematical verification: ratio bounds $[1/\sqrt{N}, 1]$ confirmed
- Edge cases: NaN/Infinity handling, bar correction precision

## Common Pitfalls

1. **Log of non-positive prices**: If `price <= 0`, `Math.Log` returns `-Infinity` or `NaN`. The implementation guards with `price > 0 ? Math.Log(price) * 1000 : 0.0`.

2. **Bar correction drift with Snapshot/Restore**: RingBuffer's `Snapshot()`/`Restore()` only saves one buffer position. NMA's `ComputeRatio()` reads ALL positions, so `CopyFrom()` is mandatory. Using Snapshot/Restore produces ~1% drift after corrections.

3. **Zero denominator in ratio**: When all adjacent log-prices are identical ($o_i = 0$ for all $i$), the denominator is zero. The implementation returns `ratio = 0`, causing NMA to hold its previous value.

4. **Period = 1 degeneracy**: With a single-bar lookback, `ComputeRatio()` has zero iterations and returns 0. NMA becomes a constant after initialization. Use `period >= 2` for meaningful adaptation.

5. **Log-scale amplification**: The $\times 1000$ scaling factor amplifies differences between log-prices. While this improves numerical resolution for the ratio computation, it also amplifies floating-point errors during buffer operations.

6. **Memory cost of CopyFrom**: Each bar correction copies the entire buffer array ($N+1$ doubles = 328 bytes for period 40). This is ~8x more expensive than Snapshot/Restore but necessary for correctness.

7. **No external validation available**: Unlike SMA, EMA, or KAMA, there are no reference implementations to validate against. All correctness assurance comes from internal consistency tests and mathematical bound verification.

## Resources

- Sloman, J. *Ocean Theory*. Pages 63-70. (Original NMA description.)
- Kaufman, P.J. (2013). *Trading Systems and Methods*, 5th ed. Wiley. Chapter 7: Adaptive Moving Averages.
- Chande, T.S. & Kroll, S. (1994). *The New Technical Trader*. Wiley. (Adaptive filter framework.)
