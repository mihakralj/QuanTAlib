# OneEuro — One Euro Filter

The **One Euro Filter** (1€ Filter) is a speed-adaptive first-order low-pass filter designed to balance jitter removal against responsiveness. It uses an adaptive cutoff frequency: at low signal speed, a low cutoff stabilizes the signal by reducing jitter; as speed increases, the cutoff rises to reduce lag.

## Algorithm

For uniform bar spacing ($T_e = 1.0$):

$$\alpha(f_c) = \frac{r}{r + 1}, \quad r = 2\pi \cdot f_c$$

**Per bar:**
1. **Raw derivative:** $dx_i = x_i - \hat{x}_{i-1}$
2. **Smooth derivative:** $\hat{\dot{x}}_i = \alpha_d \cdot dx_i + (1 - \alpha_d) \cdot \hat{\dot{x}}_{i-1}$
3. **Adaptive cutoff:** $f_c = f_{c_{min}} + \beta \cdot |\hat{\dot{x}}_i|$
4. **Adaptive alpha:** $\alpha = \alpha(f_c)$
5. **Filter output:** $\hat{x}_i = \alpha \cdot x_i + (1 - \alpha) \cdot \hat{x}_{i-1}$

Where:
- $\alpha_d$ = smoothing factor for derivative, from fixed cutoff $f_{c_d}$
- $f_{c_{min}}$ = minimum cutoff frequency (controls jitter)
- $\beta$ = speed coefficient (controls lag reduction)

## Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| minCutoff | 1.0 | > 0 | Minimum cutoff frequency. Lower = smoother at low speed. |
| beta | 0.007 | ≥ 0 | Speed coefficient. Higher = faster response to rapid moves. |
| dCutoff | 1.0 | > 0 | Cutoff frequency for the derivative estimator. |

## Tuning Guide

- **Reduce jitter:** Decrease `minCutoff`
- **Reduce lag on fast moves:** Increase `beta`
- **Smooth derivative estimate:** Decrease `dCutoff`

Start with `beta = 0`, decrease `minCutoff` until jitter is acceptable, then increase `beta` until lag on fast moves is acceptable.

## Characteristics

| Property | Value |
|----------|-------|
| Type | Low-pass (adaptive IIR) |
| Overlay | Yes (tracks price) |
| Complexity | O(1) per bar |
| Memory | O(1) — 3 state variables |
| Warmup Period | 1 bar |
| Causal | Yes |
| Zero-phase | No |
| Look-ahead | None |

## Usage

```csharp
// Streaming
var filter = new OneEuro(minCutoff: 1.0, beta: 0.007, dCutoff: 1.0);
TValue result = filter.Update(new TValue(time, price));

// Batch
TSeries results = OneEuro.Batch(series, minCutoff: 1.0, beta: 0.007, dCutoff: 1.0);

// Span
OneEuro.Batch(sourceSpan, outputSpan, minCutoff: 1.0, beta: 0.007, dCutoff: 1.0);

// Event chaining
var source = new Sma(20);
var smooth = new OneEuro(source, minCutoff: 1.0, beta: 0.007);
```

## Reference

> Casiez, G., Roussel, N., & Vogel, D. (2012). **1€ Filter: A Simple Speed-Based Low-Pass Filter for Noisy Input in Interactive Systems.** *CHI '12*, pp. 2527–2530.
> DOI: [10.1145/2207676.2208639](https://doi.org/10.1145/2207676.2208639)
