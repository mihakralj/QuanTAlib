# BUTTER3: Ehlers 3-Pole Butterworth Filter

> "Steeper rolloff demands a third pole."

The 3-Pole Butterworth Filter (BUTTER3) extends the classic Butterworth design to third order, providing -60 dB/decade rolloff compared to -40 dB/decade for the 2-pole variant. Developed from John Ehlers' formulation in "Cybernetic Analysis for Stocks and Futures" (2004), this implementation uses the same pole placement as the 3-pole Super Smoother (SSF3) but with binomial (1,3,3,1) feedforward weights that preserve the maximally flat passband characteristic. The steeper rolloff makes BUTTER3 more effective at rejecting high-frequency noise, at the cost of slightly more lag than BUTTER2.

## Core Concepts

* **Steeper rolloff**: -60 dB/decade vs -40 dB/decade for BUTTER2. Rejects noise more aggressively above the cutoff frequency.
* **Maximally flat passband**: Inherits the Butterworth characteristic of zero passband ripple, ensuring consistent filtering below cutoff.
* **Shared pole placement with SSF3**: Identical feedback coefficients (coef2, coef3, coef4). Only the feedforward structure differs: SSF3 uses single-sample input; BUTTER3 uses binomial-weighted 4-sample average.
* **Higher lag**: Third-order filtering introduces more group delay than second-order, a fundamental tradeoff for steeper attenuation.

## Mathematical Foundation

The 3-pole Butterworth filter uses Ehlers' exponential pole placement with binomial feedforward weights:

### Coefficient Derivation

$$a_1 = e^{-\pi/P}$$
$$b_1 = 2 a_1 \cos\!\left(\frac{\sqrt{3}\,\pi}{P}\right)$$
$$c_1 = a_1^2$$

### Filter Coefficients

$$\text{coef}_1 = \frac{(1 - b_1 + c_1)(1 - c_1)}{8}$$
$$\text{coef}_2 = b_1 + c_1$$
$$\text{coef}_3 = -(c_1 + b_1 c_1)$$
$$\text{coef}_4 = c_1^2$$

### Recurrence Relation

$$y[n] = \text{coef}_1 \cdot (x[n] + 3\,x[n\!-\!1] + 3\,x[n\!-\!2] + x[n\!-\!3]) + \text{coef}_2 \cdot y[n\!-\!1] + \text{coef}_3 \cdot y[n\!-\!2] + \text{coef}_4 \cdot y[n\!-\!3]$$

The feedforward weights (1, 3, 3, 1) are binomial coefficients for 3rd order, matching row 3 of Pascal's triangle.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 50M ops/s | O(1) complexity, 3-pole IIR implementation. |
| **Allocations** | 0 | Zero-allocation in hot path. |
| **Complexity** | O(1) | Constant time per bar. |
| **Accuracy** | 9/10 | Maximally flat passband preserves signal integrity. |
| **Timeliness** | 7/10 | More lag than BUTTER2 due to third pole. |
| **Overshoot** | 8/10 | Minimal overshoot; Butterworth characteristic. |
| **Smoothness** | 10/10 | Superior noise suppression from steeper rolloff. |

### Zero-Allocation Design

The implementation uses a fixed-size `State` record struct with 6 doubles (X1, X2, X3, Y1, Y2, Y3) and a count field. No heap allocations during the `Update` cycle. Coefficients are pre-calculated and stored as readonly fields.

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against PineScript reference implementation (butter3.pine). |
| **BUTTER2** | ✅ | Verified steeper rolloff behavior vs 2-pole variant. |
| **TA-Lib** | - | Not available. |
| **Skender** | - | Not available. |
| **Tulip** | - | Not available. |

## Common Pitfalls

1. **Period too small**: Period < 2 throws `ArgumentOutOfRangeException`. Minimum meaningful period is ~4 for 3-pole stability.
2. **Excessive lag**: 3-pole adds ~50% more group delay than 2-pole at the same period. Use BUTTER2 when lag sensitivity outweighs noise rejection.
3. **Warmup transient**: First 4 bars use pass-through (output = input). Full convergence requires ~6× period bars.
4. **Coefficient sensitivity**: Small period values create aggressive filtering with potential for numerical instability. Monitor for divergence with period < 4.
5. **Not interchangeable with BUTTER2**: Different coefficient derivation (Ehlers exponential vs standard cookbook). Cannot substitute one for the other without revalidation.

## Usage

```csharp
using QuanTAlib;

// Initialize
var butter = new Butter3(period: 20);

// Streaming update
double result = butter.Update(price).Value;

// Batch processing
var (results, indicator) = Butter3.Calculate(sourceSeries, period: 20);

// Span-based (zero allocation)
Butter3.Batch(sourceSpan, destSpan, period: 20, initialLast: double.NaN);
```

## References

* Ehlers, John F. "Cybernetic Analysis for Stocks and Futures." Wiley, 2004.
* Butterworth, Stephen. "On the Theory of Filter Amplifiers." Experimental Wireless and the Wireless Engineer, 1930.
