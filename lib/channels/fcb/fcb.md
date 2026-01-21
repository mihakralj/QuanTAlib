# FCB: Fractal Chaos Bands

> "The market speaks through fractals—moments when price definitively says 'this high matters' or 'this low counts.' Everything else is noise."

Fractal Chaos Bands (FCB) track the highest fractal high and lowest fractal low over a lookback period. Unlike Donchian Channels that use raw price extremes, FCB filters for *significant* turning points—three-bar patterns where the middle bar's high exceeds both neighbors (fractal high) or the middle bar's low undercuts both neighbors (fractal low). The result: bands that represent confirmed support and resistance levels rather than transient spikes.

## Historical Context

The concept of fractals in trading traces back to Bill Williams' work in the 1990s, published in "Trading Chaos" (1995) and "New Trading Dimensions" (1998). Williams defined fractal highs and lows as five-bar patterns, but the three-bar variant has become more common in modern implementations due to its faster response.

The three-bar fractal definition originates from chaos theory principles: a local maximum or minimum surrounded by lower or higher values represents a point where market sentiment definitively shifted. These aren't just any highs and lows—they're *confirmed* turning points where buyers or sellers demonstrated clear dominance.

Most fractal band implementations store fractals in lists and rescan for max/min on each bar. QuanTAlib uses monotonic deques that maintain running max/min of fractal values in O(1) amortized time, enabling real-time feeds without performance degradation.

## Architecture & Physics

Fractal Chaos Bands consist of three components: fractal detection, band tracking via monotonic deques, and the middle band calculation.

### 1. Fractal High Detection

A fractal high occurs when the previous bar's high exceeds both its neighbors:

$$
\text{FractalHigh}_t = \begin{cases}
H_{t-1} & \text{if } H_{t-1} > H_{t-2} \text{ and } H_{t-1} > H_t \\
\text{FractalHigh}_{t-1} & \text{otherwise}
\end{cases}
$$

where $H$ is the high price. The fractal is detected on bar $t$ but refers to the price at bar $t-1$ (the middle bar of the three-bar pattern).

### 2. Fractal Low Detection

A fractal low occurs when the previous bar's low undercuts both its neighbors:

$$
\text{FractalLow}_t = \begin{cases}
L_{t-1} & \text{if } L_{t-1} < L_{t-2} \text{ and } L_{t-1} < L_t \\
\text{FractalLow}_{t-1} & \text{otherwise}
\end{cases}
$$

where $L$ is the low price. Like fractal highs, this is confirmed one bar later.

### 3. Upper Band (Highest Fractal High)

Tracks the maximum fractal high value over the lookback window:

$$
U_t = \max_{i=0}^{n-1}(\text{FractalHigh}_{t-i})
$$

The upper band represents the highest *confirmed* resistance level within the period.

### 4. Lower Band (Lowest Fractal Low)

Tracks the minimum fractal low value over the lookback window:

$$
L_t = \min_{i=0}^{n-1}(\text{FractalLow}_{t-i})
$$

The lower band represents the lowest *confirmed* support level within the period.

### 5. Middle Band

The arithmetic mean of the upper and lower bands:

$$
M_t = \frac{U_t + L_t}{2}
$$

This represents the equilibrium between confirmed support and resistance.

## Mathematical Foundation

### Three-Bar Fractal Pattern

The three-bar fractal pattern requires strict inequality:

**Fractal High at index $i$:**
$$
H_i > H_{i-1} \quad \text{AND} \quad H_i > H_{i+1}
$$

**Fractal Low at index $i$:**
$$
L_i < L_{i-1} \quad \text{AND} \quad L_i < L_{i+1}
$$

Note: The fractal at index $i$ is only *detected* when bar $i+1$ arrives, introducing a one-bar confirmation delay.

### Monotonic Deque Algorithm

The implementation maintains two monotonic deques for fractal values (not raw prices):

**For maximum (upper band):**

1. On new fractal high: remove smaller values from deque back, add new value
2. On each bar: expire indices outside the lookback window
3. Front element is always the maximum fractal high

**For minimum (lower band):**

1. On new fractal low: remove larger values from deque back, add new value
2. On each bar: expire indices outside the lookback window
3. Front element is always the minimum fractal low

**Complexity**: O(1) amortized per bar.

### Warmup Period

FCB requires $\text{period} + 2$ bars for full warmup:

- 2 bars for fractal detection (need bars 0, 1, 2 to detect fractal at bar 1)
- Period bars for the sliding window to fill

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar cost includes fractal detection plus deque updates:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (fractal detection) | 4 | 1 | 4 |
| CMP (deque maintenance) | 4 | 1 | 4 |
| ADD | 1 | 1 | 1 |
| MUL | 1 | 3 | 3 |
| **Total** | **10** | — | **~12 cycles** |

**Complexity**: O(1) amortized per bar.

### Batch Mode (512 values, SIMD/FMA)

Fractal detection is inherently sequential (depends on neighbors). Limited SIMD benefit:

| Operation | Scalar Ops | SIMD Benefit | Notes |
| :--- | :---: | :---: | :--- |
| Fractal detection | 4 | 1× | Sequential dependency |
| Deque maintenance | 4 | 1× | Sequential dependency |
| Middle band | 2 | 2× | Parallelizable |

**Batch efficiency (512 bars):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 12 | 6,144 | — |
| Partial SIMD | ~11 | ~5,632 | **~8%** |

The algorithm is already efficient; sequential dependencies limit SIMD gains.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact fractal detection and max/min calculation |
| **Timeliness** | 5/10 | One-bar confirmation delay plus lookback lag |
| **Overshoot** | 10/10 | No overshoot—bands are actual fractal price levels |
| **Smoothness** | 6/10 | Bands move in steps as new fractals form or old ones exit |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No FCB implementation |
| **Skender** | N/A | No FCB implementation |
| **Tulip** | N/A | No FCB implementation |
| **Ooples** | N/A | No FCB implementation |
| **PineScript** | ✅ | Reference implementation match |

FCB is not widely implemented in standard libraries. Validation is performed against the reference PineScript algorithm and internal consistency checks (batch vs. streaming vs. span mode parity).

## Common Pitfalls

1. **Confirmation Delay**: Fractals are confirmed one bar *after* they form. A fractal high at bar 10 is only detected when bar 11 arrives. Don't expect the upper band to update immediately on a new high—it must first be confirmed as a fractal.

2. **No Fractal, No Update**: If price moves monotonically (no three-bar reversal pattern), no new fractals form, and bands remain static. This isn't a bug—it means there are no confirmed turning points. Extended trends can produce long periods of unchanging bands.

3. **Warmup Period**: FCB requires `period + 2` bars before `IsHot` becomes true. The extra 2 bars account for fractal detection. Using the indicator before warmup produces bands based on initial (possibly unconfirmed) values.

4. **Different from Donchian**: Donchian uses raw highs and lows; FCB uses fractal highs and lows. FCB bands are typically *inside* Donchian bands because fractals filter out transient spikes. Don't expect them to match.

5. **Five-Bar vs. Three-Bar**: Williams' original fractals used five bars; this implementation uses three. Three-bar fractals are more responsive but less filtered. If you need the original Williams definition, this isn't it.

6. **Memory Footprint**: The implementation stores separate buffers for fractal values and deque indices. For period=200, expect ~6.4 KB per instance (4 arrays × 200 elements × 8 bytes). For 5,000 symbols, budget ~32 MB.

7. **Bar Correction (isNew=false)**: When correcting the current bar, the indicator rebuilds its deques from the stored fractal buffer. Frequent corrections are supported but trigger O(period) rebuilds. Minimize correction calls when possible.

## References

- Williams, B. M. (1995). *Trading Chaos: Applying Expert Techniques to Maximize Your Profits*. Wiley.
- Williams, B. M. (1998). *New Trading Dimensions: How to Profit from Chaos in Stocks, Bonds, and Commodities*. Wiley.
- Mandelbrot, B. B. (1982). *The Fractal Geometry of Nature*. Freeman.
- TradingView. (2024). "Fractal Chaos Bands." Pine Script Reference Manual.
