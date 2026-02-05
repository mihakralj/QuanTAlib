# HT_SINE: Hilbert Transform - SineWave

> "The Hilbert Transform gives us the phase of the dominant cycle—knowing when to buy and sell becomes a matter of trigonometry."

HT_SINE applies the Hilbert Transform to extract the dominant market cycle and outputs the sine of the current phase angle. The indicator produces two outputs: **Sine** (current phase) and **LeadSine** (45° phase lead), enabling traders to identify cycle turning points before they occur. Crossovers between Sine and LeadSine signal potential reversals in ranging markets.

## Historical Context

John Ehlers introduced the Hilbert Transform indicator in his 2001 book *Rocket Science for Traders*, later refining it in *Cycle Analytics for Traders* (2013). The Hilbert Transform originates from signal processing, where it creates an analytic signal by generating a 90° phase-shifted version of the input. This quadrature relationship enables measurement of instantaneous phase and frequency.

The HT_SINE indicator represents Ehlers' adaptation of the Hilbert Transform for financial markets. Unlike simple oscillators that assume fixed periodicity, HT_SINE dynamically measures the dominant cycle period using homodyne discrimination—a technique borrowed from radio engineering. The 45° phase lead of LeadSine anticipates turning points by approximately 1/8 of the cycle period, providing early warning of reversals.

TA-Lib implements a version of this indicator matching Ehlers' published specifications. This implementation validates against TA-Lib's output within floating-point tolerance.

## Architecture & Physics

### 1. WMA Price Smoothing

The algorithm begins with weighted moving average smoothing:

$$
\text{SmoothPrice}_t = \frac{4 \cdot P_t + 3 \cdot P_{t-1} + 2 \cdot P_{t-2} + P_{t-3}}{10}
$$

This 4-bar WMA provides initial noise rejection without excessive lag. The weights (4, 3, 2, 1) sum to 10, centering the filter approximately 1.5 bars back.

### 2. Bandwidth Calculation

The Hilbert Transform coefficients scale with the measured cycle period:

$$
\text{Bandwidth}_t = 0.075 \cdot \text{SmoothPeriod}_{t-1} + 0.54
$$

This adaptive bandwidth widens for longer cycles and narrows for shorter ones, maintaining filter stability across varying market conditions.

### 3. Hilbert Transform Cascade

The transform applies Ehlers' specialized coefficients in a cascade:

$$
A = 0.0962, \quad B = 0.5769
$$

**Detrender:**
$$
D_t = (A \cdot \text{SP}_t + B \cdot \text{SP}_{t-2} - B \cdot \text{SP}_{t-4} - A \cdot \text{SP}_{t-6}) \cdot \text{BW}
$$

**Quadrature (Q1):**
$$
Q1_t = (A \cdot D_t + B \cdot D_{t-2} - B \cdot D_{t-4} - A \cdot D_{t-6}) \cdot \text{BW}
$$

**In-Phase (I1):**
$$
I1_t = D_{t-3}
$$

**jI (Hilbert of I1):**
$$
jI_t = (A \cdot I1_t + B \cdot I1_{t-2} - B \cdot I1_{t-4} - A \cdot I1_{t-6}) \cdot \text{BW}
$$

**jQ (Hilbert of Q1):**
$$
jQ_t = (A \cdot Q1_t + B \cdot Q1_{t-2} - B \cdot Q1_{t-4} - A \cdot Q1_{t-6}) \cdot \text{BW}
$$

### 4. Phasor Components

The in-phase and quadrature components combine:

$$
I2_t = I1_t - jQ_t
$$

$$
Q2_t = Q1_t + jI_t
$$

These are smoothed with a 0.2/0.8 EMA:

$$
I2_t \leftarrow 0.2 \cdot I2_t + 0.8 \cdot I2_{t-1}
$$

$$
Q2_t \leftarrow 0.2 \cdot Q2_t + 0.8 \cdot Q2_{t-1}
$$

### 5. Homodyne Discriminator

Period measurement uses cross-correlation of consecutive phasors:

$$
\text{Re}_t = I2_t \cdot I2_{t-1} + Q2_t \cdot Q2_{t-1}
$$

$$
\text{Im}_t = I2_t \cdot Q2_{t-1} - Q2_t \cdot I2_{t-1}
$$

Smoothed with 0.2/0.8 EMA:

$$
\text{Re}_t \leftarrow 0.2 \cdot \text{Re}_t + 0.8 \cdot \text{Re}_{t-1}
$$

$$
\text{Im}_t \leftarrow 0.2 \cdot \text{Im}_t + 0.8 \cdot \text{Im}_{t-1}
$$

The instantaneous period:

$$
\text{Period}_t = \begin{cases}
\frac{2\pi}{\arctan2(\text{Im}_t, \text{Re}_t)} & \text{if angle} \neq 0 \\
\text{Period}_{t-1} & \text{otherwise}
\end{cases}
$$

### 6. Period Clamping and Smoothing

$$
\text{Period}_t = \text{clamp}(\text{Period}_t, 6, 50)
$$

$$
\text{SmoothPeriod}_t = 0.33 \cdot \text{Period}_t + 0.67 \cdot \text{SmoothPeriod}_{t-1}
$$

### 7. Phase and Output

Phase angle from the phasor:

$$
\phi_t = \arctan2(Q2_t, I2_t)
$$

Final outputs:

$$
\text{Sine}_t = \sin(\phi_t)
$$

$$
\text{LeadSine}_t = \sin\left(\phi_t + \frac{\pi}{4}\right)
$$

## Mathematical Foundation

### Analytic Signal Theory

The Hilbert Transform $\mathcal{H}$ creates a 90° phase shift:

$$
\hat{x}(t) = \mathcal{H}[x(t)]
$$

The analytic signal combines original and transformed:

$$
z(t) = x(t) + j\hat{x}(t) = A(t)e^{j\phi(t)}
$$

where $A(t)$ is instantaneous amplitude and $\phi(t)$ is instantaneous phase.

### Discrete Approximation

Ehlers' discrete Hilbert Transform uses a specialized FIR structure with coefficients A and B that approximate the continuous transform's frequency response over the 6-50 bar period range typical of market cycles.

### LeadSine Phase Relationship

The 45° ($\pi/4$ radians) phase lead means:

$$
\text{LeadSine} = \sin(\phi + 45°) = \frac{\sqrt{2}}{2}(\sin\phi + \cos\phi)
$$

This advance equals 1/8 of a full cycle. For a 32-bar cycle, LeadSine leads by 4 bars.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 32 | 3 | 96 |
| ADD/SUB | 24 | 1 | 24 |
| Buffer access | 28 | 1 | 28 |
| ATAN2 | 2 | 50 | 100 |
| SIN | 2 | 50 | 100 |
| State EMA (×6) | 6 | 4 | 24 |
| **Total** | — | — | **~372 cycles** |

Dominant cost: trigonometric functions (ATAN2, SIN). The recursive nature of the Hilbert Transform cascade prevents SIMD vectorization in streaming mode.

### State Memory

| Component | Size |
| :--- | :---: |
| Ring buffers (4 × 8 doubles) | 256 bytes |
| State record (Period, SmoothPeriod, I2, Q2, Re, Im, PrevI2, PrevQ2, Price1-3, Count, LastValid) | 104 bytes |
| Previous state (snapshot) | 104 bytes |
| Buffer snapshots (4 × 8 doubles) | 256 bytes |
| **Total per instance** | **~720 bytes** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Matches TA-Lib output within 1e-9 tolerance |
| **Timeliness** | 7/10 | 45° lead via LeadSine; warmup requires 63 bars |
| **Overshoot** | 6/10 | Bounded to [-1, +1]; phase errors during trend transitions |
| **Smoothness** | 8/10 | Multiple EMAs in cascade provide good noise rejection |
| **Cycle Fidelity** | 8/10 | Accurate in ranging markets; degrades in strong trends |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Matches `TALib.Functions.HtSine()` for both Sine and LeadSine outputs |
| **Skender** | N/A | No HT_SINE implementation |
| **Tulip** | N/A | No HT_SINE implementation |
| **Ooples** | N/A | No HT_SINE implementation |
| **PineScript** | ✅ | Matches `ht_sine.pine` reference within floating-point tolerance |

Validation confirms:
1. Lookback period = 63 bars (matches TA-Lib)
2. Both outputs bounded to [-1, +1]
3. LeadSine consistently leads Sine by π/4 radians
4. Period measurement stable in 6-50 bar range

## Common Pitfalls

1. **Trend Mode Failure**: HT_SINE assumes cyclic behavior. In strong trends, the indicator produces unreliable signals. Combine with trend detection (e.g., `HT_TRENDMODE`) to filter signals.

2. **Warmup Period**: The 63-bar warmup is substantial. First 63 values should be ignored; `IsHot = false` during this period.

3. **Period Clamping**: Cycles outside 6-50 bars get clamped, distorting phase measurement. Markets with very long cycles (weekly/monthly) may not suit HT_SINE.

4. **Crossover Interpretation**: Sine crossing LeadSine from below suggests a cycle trough (buy); crossing from above suggests a peak (sell). However, this assumes price follows the extracted cycle.

5. **Phase Discontinuities**: Phase wraps at ±π, causing potential signal jumps. The sine function naturally handles this, but raw phase values require unwrapping for derivative calculations.

6. **Bar Correction**: When updating the same bar (`isNew = false`), all ring buffers and state must rollback. The implementation uses snapshot arrays for this; incorrect `isNew` usage corrupts 8 bars of filter memory.

7. **Memory Footprint**: At ~720 bytes per instance, HT_SINE is memory-heavy compared to simple oscillators. Monitor allocation when running many instances.

## API Usage

```csharp
// Streaming mode
var htSine = new HtSine();
foreach (var bar in bars)
{
    TValue result = htSine.Update(new TValue(bar.Time, bar.Close), isNew: true);
    if (htSine.IsHot)
    {
        double sine = result.Value;
        double leadSine = htSine.LeadSine;
        
        // Crossover detection
        if (prevSine < prevLeadSine && sine > leadSine)
        {
            // Potential sell signal (peak)
        }
    }
}

// Bar correction (same bar, updated price)
TValue corrected = htSine.Update(new TValue(bar.Time, newClose), isNew: false);

// Batch mode with dual outputs
Span<double> sine = stackalloc double[closes.Length];
Span<double> leadSine = stackalloc double[closes.Length];
HtSine.Batch(closes, sine, leadSine);

// TSeries mode
TSeries output = HtSine.Calculate(closePrices);
// Note: LeadSine only available in streaming mode

// Chaining
var source = new Ema(10);
var htSine = new HtSine(source);
// htSine automatically subscribes to source.Pub events
```

## Trading Signals

### Primary Crossover Strategy

1. **Buy Signal**: Sine crosses above LeadSine (from below)
2. **Sell Signal**: Sine crosses below LeadSine (from above)

### Confirmation Filters

- Filter signals when both lines are near zero (flat cycle)
- Avoid signals when Sine and LeadSine are nearly parallel (trend mode)
- Combine with volume or momentum confirmation

### Exit Strategy

- Exit longs when Sine peaks (approaches +1 then reverses)
- Exit shorts when Sine troughs (approaches -1 then reverses)

## References

- Ehlers, J. (2001). *Rocket Science for Traders*. Wiley.
- Ehlers, J. (2013). *Cycle Analytics for Traders*. Wiley.
- TA-Lib: `TALib.Functions.HtSine()`
- PineScript reference: `lib/cycles/ht_sine/ht_sine.pine`