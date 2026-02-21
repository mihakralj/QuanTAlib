# SSF3: Ehlers 3-Pole Super Smoother Filter

> "Three poles, one sample. Maximum smoothing, minimum ceremony."

The 3-Pole Super Smoother Filter (SSF3) extends Ehlers' Super Smoother concept to third order, providing -60 dB/decade rolloff compared to -40 dB/decade for the 2-pole variant (SSF2). It shares identical pole placement with BUTTER3 but uses a single-sample feedforward (`coef1 * x`) instead of the binomial-weighted 4-sample average (`coef1 * (x + 3x1 + 3x2 + x3)`). This makes SSF3 more responsive to recent price changes while still delivering aggressive high-frequency noise suppression.

## Core Concepts

* **Steeper rolloff**: -60 dB/decade vs -40 dB/decade for SSF2. Rejects noise more aggressively above the cutoff frequency.
* **Single-sample feedforward**: Unlike BUTTER3's 4-tap binomial average, SSF3 uses only the current sample. This reduces lag at the cost of slightly less passband flatness.
* **Shared pole placement with BUTTER3**: Identical feedback coefficients (coef2, coef3, coef4). Only the feedforward structure differs.
* **Higher smoothing than SSF2**: Third-order filtering provides more aggressive noise suppression, with the tradeoff of additional group delay.

## Mathematical Foundation

The 3-pole Super Smoother uses Ehlers' exponential pole placement with single-sample feedforward:

### Coefficient Derivation

$$a_1 = e^{-\pi/P}$$
$$b_1 = 2 a_1 \cos\!\left(\frac{\sqrt{3}\,\pi}{P}\right)$$
$$c_1 = a_1^2$$

### Filter Coefficients

$$\text{coef}_2 = b_1 + c_1$$
$$\text{coef}_3 = -(c_1 + b_1 c_1)$$
$$\text{coef}_4 = c_1^2$$
$$\text{coef}_1 = 1 - \text{coef}_2 - \text{coef}_3 - \text{coef}_4$$

### Recurrence Relation

$$y[n] = \text{coef}_1 \cdot x[n] + \text{coef}_2 \cdot y[n\!-\!1] + \text{coef}_3 \cdot y[n\!-\!2] + \text{coef}_4 \cdot y[n\!-\!3]$$

The key difference from BUTTER3: the feedforward is `coef1 * x[n]` (single sample) rather than `coef1 * (x[n] + 3*x[n-1] + 3*x[n-2] + x[n-3])` (binomial weighted). This means `coef1 = 1 - coef2 - coef3 - coef4` ensures unity DC gain.

## SSF3 vs BUTTER3

| Property | SSF3 | BUTTER3 |
| :--- | :--- | :--- |
| **Feedforward** | `coef1 * x` | `coef1 * (x + 3x1 + 3x2 + x3)` |
| **Feedback** | Identical | Identical |
| **DC gain** | Unity | Unity |
| **Passband flatness** | Good | Maximally flat (Butterworth) |
| **Responsiveness** | Higher | Lower |
| **State variables** | 3 (Y1, Y2, Y3) | 6 (X1, X2, X3, Y1, Y2, Y3) |

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 50M ops/s | O(1) complexity, 3-pole IIR implementation. |
| **Allocations** | 0 | Zero-allocation in hot path. |
| **Complexity** | O(1) | Constant time per bar. |
| **Accuracy** | 9/10 | Excellent noise suppression with unity DC gain. |
| **Timeliness** | 8/10 | More responsive than BUTTER3 (single-sample feedforward). |
| **Overshoot** | 7/10 | Slightly more overshoot than BUTTER3 due to less passband flatness. |
| **Smoothness** | 10/10 | Superior noise suppression from steeper rolloff. |

### Zero-Allocation Design

The implementation uses a fixed-size `State` record struct with 3 doubles (Y1, Y2, Y3) and a count field. No heap allocations during the `Update` cycle. Coefficients are pre-calculated and stored as readonly fields.

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against PineScript reference implementation (ssf3.pine). |
| **SSF2** | ✅ | Verified convergence behavior: both converge to same value on constant input. |
| **BUTTER3** | ✅ | Shared pole placement verified; feedforward difference confirmed. |
| **TA-Lib** | - | Not available. |
| **Skender** | - | Not available. |
| **Tulip** | - | Not available. |

## Common Pitfalls

1. **Period too small**: Period < 2 throws `ArgumentOutOfRangeException`. Minimum meaningful period is ~4 for 3-pole stability.
2. **More responsive than BUTTER3**: SSF3's single-sample feedforward makes it faster-reacting but with slightly more overshoot. Use BUTTER3 when maximum passband flatness matters.
3. **Warmup transient**: First 4 bars use pass-through (output = input). Full convergence requires ~6x period bars.
4. **Coefficient sensitivity**: Small period values create aggressive filtering with potential for numerical instability. Monitor for divergence with period < 4.
5. **Not interchangeable with SSF2**: Different order (3-pole vs 2-pole). Cannot substitute one for the other without revalidation.
6. **Feedforward difference from BUTTER3**: Despite sharing feedback coefficients, SSF3 and BUTTER3 produce different outputs. SSF3 has less lag but less passband flatness.

## Usage

```csharp
using QuanTAlib;

// Initialize
var ssf = new Ssf3(period: 20);

// Streaming update
double result = ssf.Update(price).Value;

// Batch processing
var (results, indicator) = Ssf3.Calculate(sourceSeries, period: 20);

// Span-based (zero allocation)
Ssf3.Batch(sourceSpan, destSpan, period: 20, initialLast: double.NaN);
```

## References

* Ehlers, John F. "Cybernetic Analysis for Stocks and Futures." Wiley, 2004.
* Ehlers, John F. "Rocket Science for Traders." Wiley, 2001.
