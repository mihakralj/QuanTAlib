# BWMA: Bessel-Weighted Moving Average

> *The Bessel function appears in problems involving cylindrical symmetry—heat flow in pipes, vibration of drumheads, and apparently, the smoothing of financial time series. Mathematics doesn't care about your asset class.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `order` (default 0)                      |
| **Outputs**      | Single series (Bwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [bwma.pine](bwma.pine)                       |
| **Signature**    | [bwma_signature](bwma_signature.md) |

- BWMA is a Finite Impulse Response (FIR) filter that applies a Bessel-derived window function to weight price data.
- Parameterized by `period`, `order` (default 0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

BWMA is a Finite Impulse Response (FIR) filter that applies a Bessel-derived window function to weight price data. The weighting follows a parabolic (or higher-order polynomial) profile that emphasizes the center of the lookback window while smoothly tapering to zero at the edges. Unlike rectangular (SMA) or exponential (EMA) weighting, BWMA provides a mathematically smooth transition that reduces spectral leakage and Gibbs phenomenon artifacts.

## Historical Context

The Bessel window function derives from the modified Bessel function of the first kind, $I_0$, which Friedrich Bessel studied in the early 19th century while analyzing planetary motion perturbations. The simplified polynomial approximation used in BWMA—$(1 - x^2)^{\text{power}}$—captures the essential shape without requiring the full Bessel function computation.

In signal processing, Bessel-derived windows are prized for their smooth rolloff characteristics. The Kaiser window (a close relative) is standard in FIR filter design for its ability to trade off between main lobe width and side lobe attenuation. BWMA brings this engineering discipline to technical analysis.

## Architecture & Physics

BWMA maps each position in the lookback window to a normalized coordinate $x \in [-1, 1]$, then applies the weighting function:

$$w_i = (1 - x_i^2)^{\text{power}}$$

The window is inherently symmetric around the center, creating a bell-shaped weight distribution. Higher order values sharpen the bell, concentrating weight more tightly around the center bar.

### Physical Interpretation

Think of BWMA as a mass-spring system where:

* **Order 0** (parabolic): The weight distribution follows a simple parabola—gentle tapering, broad response
* **Order 1**: The curve steepens, emphasizing center values more strongly
* **Order 2+**: Increasingly focused on the center, approaching a "soft" impulse response

The key advantage over rectangular windows (SMA) is the elimination of the "boxcar" effect—the abrupt inclusion/exclusion of data points that causes artificial oscillations in the frequency response.

### The Compute Challenge

Like other FIR filters, BWMA precomputes weights at initialization. Runtime becomes a weighted dot product:

$$\text{BWMA}_t = \frac{\sum_{i=0}^{L-1} P_{t-i} \cdot w_i}{\sum_{i=0}^{L-1} w_i}$$

QuanTAlib stores both the weight vector and the precomputed inverse of the weight sum, reducing division to multiplication in the hot path.

## Mathematical Foundation

### 1. Coordinate Mapping

For a window of length $L$, each index $i \in [0, L-1]$ maps to:

$$x_i = \frac{2i}{L-1} - 1$$

This places $x_0 = -1$ (oldest), $x_{(L-1)/2} = 0$ (center), and $x_{L-1} = 1$ (newest).

### 2. Power Calculation

The exponent depends on the order parameter:

$$\text{power} = \frac{\text{order}}{2} + 0.5$$

| Order | Power | Window Shape |
| :--- | :--- | :--- |
| 0 | 0.5 | Square root parabola: $(1-x^2)^{0.5}$ |
| 1 | 1.0 | Linear parabola: $(1-x^2)$ |
| 2 | 1.5 | Steeper: $(1-x^2)^{1.5}$ |
| 3 | 2.0 | Even sharper: $(1-x^2)^2$ |

*Note: The reference PineScript uses `order/2 + 0.5` which differs slightly from some textbook definitions.*

### 3. Weight Generation

For each index:

$$w_i = \begin{cases}
(1 - x_i^2)^{\text{power}} & \text{if } |x_i| < 1 \\
0 & \text{otherwise}
\end{cases}$$

The edge case handling ensures weights at exactly $x = \pm 1$ are zero, providing smooth cutoff.

### 4. Normalization

The final BWMA value:

$$\text{BWMA}_t = \frac{\sum_{i=0}^{L-1} P_{t-i} \cdot w_{L-1-i}}{W_{\text{sum}}}$$

Where $W_{\text{sum}} = \sum w_i$.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Constructor (one-time weight precomputation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 2L | 3 | 6L |
| ADD/SUB | 2L | 1 | 2L |
| POW | L | 80 | 80L |
| **Total (init)** | — | — | **~88L cycles** |

For period=20: ~1,760 cycles (one-time).

**Hot path (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | L + 1 | 3 | 3L + 3 |
| ADD | L | 1 | L |
| **Total** | **2L + 1** | — | **~4L + 3 cycles** |

For period=20: ~83 cycles per bar.

**Hot path breakdown:**
- Dot product: `buffer.DotProduct(weights)` → L MUL + L ADD
- Normalization: `result × invWeightSum` → 1 MUL (precomputed inverse avoids DIV)

### Batch Mode (SIMD)

The dot product is highly vectorizable:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Weighted products | L | L/8 | 8× |
| Horizontal sum | L | log₂(8) | ~L/3× |

**Batch efficiency (512 bars, period=20):**

| Mode | Cycles/bar | Total | Notes |
| :--- | :---: | :---: | :--- |
| Scalar streaming | ~83 | ~42,496 | O(L) per bar |
| SIMD batch | ~22 | ~11,264 | Vectorized dot product |
| **Improvement** | **~4×** | **~31K saved** | — |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches mathematical definition |
| **Timeliness** | 7/10 | Symmetric window introduces inherent lag |
| **Overshoot** | 9/10 | Smooth window prevents ringing |
| **Smoothness** | 9/10 | Excellent noise rejection |

### Implementation Highlights

```csharp
// Weight computation (constructor)
double scale = period > 1 ? 2.0 / (period - 1) : 0.0;
double power = order * 0.5 + 0.5;

for (int i = 0; i < period; i++)
{
    double x = period > 1 ? i * scale - 1.0 : 0.0;
    double arg = 1.0 - x * x;
    weights[i] = arg > 0 ? Math.Pow(arg, power) : 0.0;
    sum += weights[i];
}

// Runtime (Update) - SIMD-friendly dot product
double result = buffer.DotProduct(weights) * invWeightSum;
```

## Validation

BWMA is a custom indicator not found in standard technical analysis libraries. Validation relies on self-consistency tests.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Reference implementation |
| **TradingView** | ✅ | Matches PineScript `bwma.pine` |
| **TA-Lib** | ❌ | Not included |
| **Skender** | ❌ | Not included |
| **Tulip** | ❌ | Not included |
| **Ooples** | ❌ | Not included |

Self-consistency validation ensures:

* Streaming, batch, and span APIs produce identical results
* Bar correction (isNew=false) restores previous state correctly
* NaN handling substitutes last valid value
* Reset produces identical results on replay

## Common Pitfalls

1. **Order Selection Paralysis**: Start with order 0 (parabolic). It's the most balanced choice. Higher orders provide sharper filtering but may over-smooth trend transitions.

2. **Period 2 Degeneracy**: At period 2, the window points land exactly at $x = \pm 1$, where weights become zero. QuanTAlib handles this gracefully, but the output is mathematically degenerate. Use period ≥ 3.

3. **Symmetric Lag**: Unlike offset-adjustable windows (ALMA), BWMA's symmetry means the center of gravity is always at the middle of the window. Expect lag of approximately $L/2$ bars.

4. **Confusion with Kaiser-Bessel**: The full Kaiser-Bessel window uses $I_0(\beta \sqrt{1-x^2}) / I_0(\beta)$ with a shape parameter $\beta$. BWMA uses the polynomial approximation $(1-x^2)^p$, which is simpler but different. Don't mix the two in discussions.

5. **Edge Effects During Warmup**: The first $L-1$ values are computed with partial windows. Trust results only after `IsHot` becomes true.

## Parameter Guidelines

| Use Case | Period | Order | Rationale |
| :--- | :--- | :--- | :--- |
| Scalping (1-5 min) | 8-12 | 0 | Quick response, mild smoothing |
| Swing trading | 14-21 | 1 | Balanced filtering |
| Position trading | 50-100 | 2 | Heavy smoothing, trend focus |
| Noise floor analysis | 20-30 | 3 | Maximum smoothing |

## See Also

* [ALMA](../alma/Alma.md) - Gaussian window with adjustable offset
* [WMA](../wma/Wma.md) - Linear weighting (triangular window)
* [SINEMA](../sinema/Sinema.md) - Sine-weighted moving average
