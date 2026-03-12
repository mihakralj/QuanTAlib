# KDJ: Enhanced Stochastic Oscillator

> *K leads, D confirms, J exaggerates — three perspectives on momentum condensed into one indicator.*

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | High, Low, Close (TBar) |
| **Parameters** | `length` (default 9), `signal` (default 3) |
| **Outputs** | K line, D line, J line (`Last`) |
| **Output range** | K: [0, 100], D: [0, 100], J: unbounded |
| **Warmup period** | `length + signal - 1` |

### Key takeaways

- KDJ extends the classic Stochastic by adding a **J line** that amplifies K/D divergence, giving earlier reversal signals.
- Uses **Wilder's RMA** (α = 1/signal) instead of SMA for smoother K and D lines, producing less whipsaw than the standard Stochastic.
- Popular in **Asian markets** (standard indicator on Chinese exchanges) where the J line's overbought/oversold extremes drive position sizing.
- Monotonic deques deliver **O(1) amortized** highest-high/lowest-low tracking without re-scanning the lookback window.
- Exponential warmup compensators eliminate the typical initialization bias that plagues recursive filters from bar one.

## Historical Context

The KDJ indicator originated in Asian financial markets as an extension of George Lane's Stochastic Oscillator. Chinese and Japanese traders found the classic %K/%D pair insufficient for capturing the acceleration of momentum reversals, so they added a third component, the J line, defined as 3K - 2D. This amplification makes J break above 100 or below 0 well before K and D reach their own extreme zones, providing an early-warning system that the standard Stochastic lacks.

The QuanTAlib implementation departs from the traditional SMA-based smoothing found in most charting platforms. By substituting Wilder's RMA (equivalent to an EMA with α = 1/period), the K and D lines respond more quickly to recent price changes while maintaining the exponentially-weighted memory that prevents sudden jumps on window entry/exit. This choice trades some of the SMA version's visual smoothness for faster signal generation, which matters when the J line's purpose is precisely to detect reversals early.

## What It Measures and Why It Matters

KDJ measures where the current close sits within the recent high-low range, then smooths that position twice (K smooths RSV, D smooths K) and amplifies the gap between the two smoothed lines into J. The K line answers "where is price relative to its range?", the D line answers "where has that relative position been trending?", and the J line answers "is the trend accelerating or decelerating?"

The J line's unbounded nature is its defining feature. While K and D are clamped to [0, 100], J routinely exceeds 100 during strong uptrends and drops below 0 during strong downtrends. These excursions signal exhaustion before the bounded lines reach their own overbought/oversold thresholds. Traders use J > 100 as a warning that bullish momentum is overextended, and J < 0 as a warning that bearish momentum cannot sustain itself.

## Mathematical Foundation

### Core Formula

$$RSV = 100 \times \frac{Close - LL_n}{HH_n - LL_n}$$

where $HH_n$ is the highest high and $LL_n$ is the lowest low over the lookback period $n$. When the range is zero, RSV defaults to 50.0 (neutral).

$$K = \text{RMA}(RSV, \, signal) = \alpha \cdot RSV + (1 - \alpha) \cdot K_{prev}$$

$$D = \text{RMA}(K, \, signal) = \alpha \cdot K + (1 - \alpha) \cdot D_{prev}$$

$$J = 3K - 2D$$

where $\alpha = 1 / signal$.

### Parameter Mapping

| Parameter | Formula role | Default | Constraint |
|-----------|-------------|---------|------------|
| `length` | Lookback window for $HH_n$ / $LL_n$ | 9 | > 0 |
| `signal` | RMA period for K and D smoothing | 3 | > 0 |

### Warmup Period

$$W = length + signal - 1$$

Default configuration (9, 3) warms up in 11 bars.

## Architecture & Physics

### 1. Three-Output Design

KDJ produces three correlated outputs per bar. [`K`](lib/oscillators/kdj/Kdj.cs:49) and [`D`](lib/oscillators/kdj/Kdj.cs:50) are stored as separate `TValue` properties; [`Last`](lib/oscillators/kdj/Kdj.cs:48) holds the J line. The `Update(TBarSeries)` method returns a named tuple `(TSeries K, TSeries D, TSeries J)`.

### 2. Monotonic Deque Min/Max

Instead of scanning the entire lookback window on each bar, [`MonotonicDeque`](lib/oscillators/kdj/Kdj.cs:30) maintains sorted candidates so that highest-high and lowest-low queries are O(1). On correction (`isNew=false`), the deque is rebuilt from the circular buffer without heap allocation.

### 3. RMA with Warmup Compensator

The exponential warmup compensator tracks the geometric decay factor $e_K = e_K \times (1 - \alpha)$ and divides raw RMA output by $(1 - e_K)$ during the transient phase. This bias correction ensures accurate K and D values from the first bar rather than waiting for the filter to converge.

### 4. FMA Hot Path

Both RMA updates use [`Math.FusedMultiplyAdd`](lib/oscillators/kdj/Kdj.cs:131) for the `decay * prev + alpha * input` pattern, and the J computation uses FMA for `3.0 * K + (-2.0 * D)`.

### 5. Edge Cases

- **Zero range**: RSV defaults to 50.0 (midpoint).
- **NaN/Infinity inputs**: Last-valid substitution per channel (high, low, close independently).
- **All NaN**: Returns NaN for all three outputs until a finite bar arrives.
- **K/D clamping**: `Math.Clamp(value, 0.0, 100.0)` enforces bounds post-computation.

## Interpretation and Signals

### Signal Zones

| Zone | K value | J value | Meaning |
|------|---------|---------|---------|
| Overbought | > 80 | > 100 | Momentum exhaustion, potential reversal down |
| Neutral | 20 - 80 | 0 - 100 | Trend continuation likely |
| Oversold | < 20 | < 0 | Selling exhaustion, potential reversal up |

### Signal Patterns

- **K crosses above D**: Bullish signal, especially when both are below 20.
- **K crosses below D**: Bearish signal, especially when both are above 80.
- **J > 100**: Strongly overbought, reversal probability increases.
- **J < 0**: Strongly oversold, reversal probability increases.
- **J divergence from price**: J making lower highs while price makes higher highs warns of trend weakness.

### Practical Notes

- The J line generates more false signals than K/D crossovers in ranging markets. Combine with trend confirmation (ADX, moving average slope) to filter.
- Shorter `length` values (5-7) suit intraday timeframes; longer values (14-21) suit daily/weekly analysis.
- The RMA smoothing makes this variant less noisy than SMA-based KDJ but slightly slower to react to sharp reversals.

## Related Indicators

- [**Stoch**](../stoch/Stoch.md): Classic Stochastic %K/%D with SMA smoothing.
- [**Stochf**](../stochf/Stochf.md): Fast Stochastic without secondary smoothing.
- [**SMI**](../smi/Smi.md): Stochastic Momentum Index, uses distance from midpoint rather than from lowest low.
- [**Williams %R**](../willr/Willr.md): Inverted raw stochastic without smoothing.

## Validation

No direct TA-Lib, Skender, Tulip, or Ooples equivalent exists for KDJ with Wilder's RMA smoothing. Validation is performed via internal consistency checks.

| Check | Status | Notes |
|-------|--------|-------|
| Streaming vs Batch | ✅ | All three outputs match within 1e-10 |
| Span vs TBarSeries Batch | ✅ | K, D, J identical across APIs |
| J = 3K - 2D identity | ✅ | Mathematical identity holds for all bars |
| K/D bounded [0, 100] | ✅ | Verified across 500 bars with high-volatility GBM |
| Parameter sensitivity | ✅ | Different length/signal values produce distinct outputs |
| Constant price convergence | ✅ | K = D = J = 50.0 after warmup |

## Performance Profile

### Key Optimizations

- **O(1) amortized streaming**: Monotonic deques eliminate window re-scan.
- **FMA in RMA updates**: `Math.FusedMultiplyAdd` for both K and D smoothing plus J computation.
- **Precomputed constants**: `_alpha` and `_decay` calculated once in constructor.
- **Circular buffer**: Fixed-size `double[]` arrays for high/low with modulo indexing.
- **Zero-allocation correction**: Deque rebuild operates on existing buffer memory.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Comparisons | 3 (input validation) + O(1) amortized deque |
| Multiplications | 4 (2× RMA + J) |
| Additions | 4 (2× RMA + J + range) |
| FMA calls | 3 (`K`, `D`, `J`) |
| Divisions | 1 (RSV) + up to 2 (warmup compensators) |
| Clamp | 2 (K, D) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| Highest/Lowest | Delegated to `Highest.Batch` / `Lowest.Batch` (SIMD-capable) |
| RSV computation | Scalar (data-dependent division) |
| RMA smoothing | Scalar (IIR recursion, sequential dependency) |
| J computation | FMA scalar per element |
| Buffer strategy | `stackalloc` ≤ 256, `ArrayPool` above |

## Common Pitfalls

1. **Confusing J with a bounded oscillator.** J routinely exceeds [0, 100]. Applying overbought/oversold thresholds designed for K to the J line produces premature signals.
2. **Using SMA-based KDJ formulas.** Many charting platforms use SMA smoothing. This implementation uses RMA (Wilder's), so values will not match SMA-based references exactly.
3. **Ignoring warmup bias.** Without the exponential compensator, early K and D values are biased toward zero. The compensator fixes this, but comparing against implementations without it will show discrepancies in the first `signal` bars.
4. **Short lookback in ranging markets.** A 5-bar length produces rapid oscillation between 0 and 100, generating excessive crossover signals. Increase `length` or add a trend filter.
5. **Treating K/D crossovers as standalone signals.** In strong trends, K and D can remain above 80 (or below 20) for extended periods. Crossovers in the direction of the trend are continuations, not reversals.

## FAQ

**Q: Why does J go above 100 or below 0?**
A: By design. J = 3K - 2D amplifies the gap between K and D. When K leads D strongly (fast momentum), the 3:2 weighting pushes J beyond the [0, 100] range. This is the indicator's primary edge over standard Stochastic.

**Q: Why use RMA instead of SMA for smoothing?**
A: RMA gives exponentially-weighted memory, so old data fades gradually rather than dropping off a cliff when it exits the window. This produces smoother K and D lines with fewer whipsaws at the cost of slightly more lag.

**Q: How does this compare to the standard Stochastic (%K/%D)?**
A: The standard Stochastic uses SMA smoothing and lacks the J line. KDJ with RMA smoothing responds faster to price changes and provides the additional J line for early reversal detection, making it a strict superset of Stochastic functionality.

## References

- Chinese securities analysis (KDJ is a standard indicator on Chinese exchanges)
- [PineScript reference](kdj.pine)
- Lane, G. C. "Lane's Stochastics." *Technical Analysis of Stocks and Commodities*, 1984.
