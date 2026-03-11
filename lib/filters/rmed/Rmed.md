# RMED: Ehlers Recursive Median Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 12)                      |
| **Outputs**      | Single series (Rmed)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | 5 bars (MedianWindow)                          |
| **PineScript**   | [rmed.pine](rmed.pine)                       |
| **Signature**    | [rmed_signature](rmed_signature.md) |

- RMED applies exponential smoothing to a 5-bar running median, creating a nonlinear IIR filter that rejects impulsive spike noise while providing sm...
- Parameterized by `period` (default 12).
- Output range: Tracks input.
- Requires **5 bars** of warmup (MedianWindow) before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "John Ehlers combined two tools that rarely meet: the median (nonlinear, spike-resistant) and the EMA (smooth, recursive). The median kills the spikes, the EMA smooths the survivors. Together they produce a filter that is both resistant and smooth."

RMED applies exponential smoothing to a 5-bar running median, creating a nonlinear IIR filter that rejects impulsive spike noise while providing smooth recursive tracking. The median component eliminates outliers that would corrupt any linear filter, while the EMA provides the recursive continuity that a pure median lacks. The EMA constant $\alpha$ is derived from Ehlers' cycle-period formula, connecting the smoothing rate to the dominant cycle length of the data.

## Historical Context

John F. Ehlers published "Recursive Median Filters" in *Technical Analysis of Stocks & Commodities* (March 2018). The article addressed a fundamental limitation of linear filters: no matter how sophisticated an EMA, DEMA, or Butterworth design is, a single bad tick or flash-crash spike will corrupt the output for its entire impulse response duration.

Median filters solve this problem completely for impulse noise, but traditional median filters are non-recursive (pure FIR), which means they have no "memory" between bars — each output depends only on the current window, creating a choppy, step-like output. Ehlers' innovation was to follow the median with an exponential average, combining the spike rejection of the median with the smooth continuity of the EMA.

The 5-bar median window is a design choice: it can reject up to 2 simultaneous bad ticks in a row (the breakdown point is $\lfloor 5/2 \rfloor = 2$), while having minimal lag (centered at bar 2 of 5). Wider median windows would reject more spikes but add more lag.

The EMA constant $\alpha = (\cos\theta + \sin\theta - 1)/\cos\theta$ where $\theta = 2\pi/P$ is Ehlers' standard cycle-period-to-smoothing mapping, which produces a critically damped response at the specified period.

## Architecture & Physics

### 1. Five-Bar Median

A circular buffer of 5 values is sorted each bar; the middle element is extracted as the median.

### 2. Ehlers EMA Constant

$$
\alpha = \frac{\cos(2\pi/P) + \sin(2\pi/P) - 1}{\cos(2\pi/P)}
$$

Clamped to $[0, 1]$ for numerical safety.

### 3. Recursive Smoothing

$$
\text{RMED}_t = \alpha \cdot \text{Median5}_t + (1 - \alpha) \cdot \text{RMED}_{t-1}
$$

## Mathematical Foundation

**Five-bar median:**

$$
\text{Med5}_t = \text{median}(x_t, x_{t-1}, x_{t-2}, x_{t-3}, x_{t-4})
$$

**Ehlers smoothing constant (from cycle period $P$):**

$$
\theta = \frac{2\pi}{P}, \quad \alpha = \frac{\cos\theta + \sin\theta - 1}{\cos\theta}
$$

For common periods:

| $P$ | $\alpha$ | Equivalent EMA period |
| :---: | :---: | :---: |
| 5 | 0.72 | ~3.6 |
| 10 | 0.38 | ~4.3 |
| 20 | 0.20 | ~9.0 |
| 40 | 0.10 | ~19 |

**Recursive filter:**

$$
\text{RMED}_t = \alpha \cdot \text{Med5}_t + (1-\alpha) \cdot \text{RMED}_{t-1}
$$

**Spike rejection:** A single outlier in the 5-bar window is always rejected by the median (it cannot be the middle value). Two consecutive outliers are also rejected. Three or more consecutive outliers breach the breakdown point.

**Default parameters:** `period = 12`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
// Ehlers alpha from cycle period
angle = 2π / period
alpha = (cos(angle) + sin(angle) - 1) / cos(angle)
alpha = clamp(alpha, 0, 1)

// 5-bar median
buf[head] = price
head = (head + 1) % 5
sorted = sort(buf)
med5 = sorted[2]

// Recursive EMA of median
rm = alpha * med5 + (1-alpha) * rm
```


## Performance Profile

### Operation Count (Streaming Mode)

RMED computes the median of 5 stored values (optimal 5-element sorting network: 9 compare-and-swap) then applies one EMA. O(1) per bar with fixed small constant.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer update (shift 5-bar window) | 1 | ~3 cy | ~3 cy |
| 5-element median (9 compare-swap ops) | 9 | ~3 cy | ~27 cy |
| Alpha derivation (cos/sin of 2*pi/P) | 2 | ~10 cy | ~20 cy |
| EMA FMA (alpha * median + (1-alpha) * prev) | 1 | ~4 cy | ~4 cy |
| **Total** | **~13** | — | **~54 cycles** |

O(1) per bar. The alpha is period-dependent and precomputed at construction; per-bar cost is the 9-comparison sorting network (~27 cy) plus EMA (~4 cy). ~54 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| 5-element median (sorting network) | No | Branchy compare-swap; data-dependent ordering |
| EMA recursion | No | Sequential IIR dependency |
| History window management | Partial | ShiftRight of 5 doubles is vectorizable but trivial |

Nonlinear median + recursive EMA blocks all meaningful SIMD. Batch throughput: ~54 cy/bar scalar.

## Resources

- Ehlers, J.F. (2018). "Recursive Median Filters." *Technical Analysis of Stocks & Commodities*, March 2018.
- Tukey, J.W. (1977). *Exploratory Data Analysis*. Addison-Wesley. Chapter 7: Resistant Smoothing.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 3: Smoothing Constants from Cycle Period.
