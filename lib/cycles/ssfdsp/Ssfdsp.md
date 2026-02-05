# SSFDSP: Super Smooth Filter Detrended Synthetic Price

> "The Super Smoother does what its name implies—it smooths without adding the lag penalty that haunts lesser filters."

SSF-DSP applies John Ehlers' Super Smooth Filter (SSF) as a detrending mechanism, subtracting a slow SSF from a fast SSF to isolate cyclical components. Where the original DSP uses dual EMAs, SSF-DSP substitutes 2-pole Butterworth-derived filters that reject high-frequency noise more aggressively while maintaining phase fidelity. The result oscillates around zero with reduced whipsaw in choppy conditions.

## Historical Context

John Ehlers introduced the Super Smoother Filter in his 2013 book *Cycle Analytics for Traders*. The SSF represents Ehlers' effort to create a filter with the smoothness of higher-order IIR filters without excessive lag. By using a 2-pole Butterworth-style design with coefficients derived from the cutoff period, SSF achieves superior noise rejection compared to EMAs of equivalent lag.

The Detrended Synthetic Price concept—subtracting a slower smoothed series from a faster one—predates SSF. The innovation here combines the detrending approach with SSF's superior frequency response. Where EMA-based DSP suffers from high-frequency bleed-through, SSF-DSP provides cleaner cycle extraction.

## Architecture & Physics

### 1. Period Decomposition

The single `period` parameter decomposes into two cutoff frequencies:

$$
\text{fastPeriod} = \max\left(2, \left\lfloor \frac{P}{4} \right\rfloor\right)
$$

$$
\text{slowPeriod} = \max\left(3, \left\lfloor \frac{P}{2} \right\rfloor\right)
$$

The floor operation and minimum bounds ensure valid filter coefficients even for small periods. Fast period captures quarter-cycle oscillations; slow period captures half-cycle trends.

### 2. SSF Coefficient Derivation

Each SSF uses identical coefficient formulas with different periods:

$$
\omega = \frac{\sqrt{2} \cdot \pi}{P_{cutoff}}
$$

$$
c_2 = 2 \cdot e^{-\omega} \cdot \cos(\omega)
$$

$$
c_3 = -e^{-2\omega}
$$

$$
c_1 = 1 - c_2 - c_3
$$

The $\sqrt{2}$ factor originates from Butterworth filter design, ensuring maximally flat passband response. The exponential-cosine product creates the characteristic 2-pole rolloff.

### 3. IIR Recursion

Each SSF applies the standard 2-pole recursion:

$$
\text{SSF}_t = c_1 \cdot x_t + c_2 \cdot \text{SSF}_{t-1} + c_3 \cdot \text{SSF}_{t-2}
$$

where $x_t$ is the current input price. The recursion maintains two bars of history for each filter.

### 4. Detrending Operation

The final output removes trend by differencing:

$$
\text{SSFDSP}_t = \text{SSF}_{fast,t} - \text{SSF}_{slow,t}
$$

This produces a zero-centered oscillator. When price rises faster than the slow filter can track, SSFDSP goes positive. When price momentum fades, SSFDSP returns toward zero.

## Mathematical Foundation

### Transfer Function

Each SSF has the z-domain transfer function:

$$
H(z) = \frac{c_1}{1 - c_2 z^{-1} - c_3 z^{-2}}
$$

The combined system (fast minus slow) creates a bandpass-like response, attenuating both very high frequencies (rejected by both filters) and very low frequencies (canceled by the differencing operation).

### Frequency Response

The -3dB cutoff frequency for each SSF:

$$
f_{cutoff} = \frac{1}{P_{cutoff}}
$$

The bandpass center frequency falls approximately between the fast and slow cutoffs:

$$
f_{center} \approx \frac{1}{2} \left( \frac{1}{P_{fast}} + \frac{1}{P_{slow}} \right)
$$

### Warmup Period

The filter requires warmup before producing stable output. Given the 2-pole recursive structure:

$$
\text{WarmupPeriod} = P_{slow}
$$

During warmup, the filter uses available history to bootstrap state, but outputs should be considered unreliable until `IsHot = true`.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 6 | 3 | 18 |
| ADD/SUB | 5 | 1 | 5 |
| State load/store | 8 | 1 | 8 |
| FMA candidates | 4 | 4→3 | 12→9 |
| **Total** | — | — | **~28 cycles** |

Dominant cost: coefficient multiplications. FMA optimization reduces 2 MUL+ADD pairs per SSF to single FMA operations.

### State Memory

| Component | Size |
| :--- | :---: |
| Fast SSF state (2 doubles) | 16 bytes |
| Slow SSF state (2 doubles) | 16 bytes |
| Tick counter | 4 bytes |
| Last valid input | 8 bytes |
| **Total per instance** | **~48 bytes** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact SSF formula; matches PineScript reference |
| **Timeliness** | 8/10 | Lower lag than EMA-based DSP for equivalent smoothing |
| **Overshoot** | 7/10 | 2-pole design has mild overshoot on step inputs |
| **Smoothness** | 9/10 | Superior noise rejection vs EMA |
| **Cycle Fidelity** | 8/10 | Good phase preservation; minor amplitude distortion at extremes |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No SSF-DSP implementation |
| **Skender** | N/A | No SSF-DSP implementation |
| **Tulip** | N/A | No SSF-DSP implementation |
| **Ooples** | N/A | No SSF-DSP implementation |
| **PineScript** | ✅ | Matches `ssfdsp.pine` reference within floating-point tolerance |

Validation relies on mathematical property verification:
1. Zero-crossing behavior matches detrending theory
2. Coefficient formulas match Ehlers' published SSF design
3. Output bounds are symmetric around zero
4. Filter stability verified (poles inside unit circle)

## Common Pitfalls

1. **Period Too Small**: Periods below 8 produce fast/slow periods that are too close, resulting in minimal oscillator amplitude. Recommended minimum: `period >= 8`.

2. **Warmup Interpretation**: The filter produces output immediately but is unreliable until `IsHot = true`. Trading signals during warmup phase are statistically noise.

3. **Amplitude Variability**: Unlike bounded oscillators (RSI, Stochastic), SSF-DSP amplitude varies with price volatility. Normalize if consistent threshold signals are needed.

4. **Lag vs Smoothness Tradeoff**: Increasing period improves smoothness but increases lag. The fast/slow period ratio (4:2 or 1:2) is fixed by design. Adjust base period, not ratio.

5. **Bar Correction**: When updating the same bar (`isNew = false`), state rolls back to prevent cumulative drift. Failing to use `isNew` correctly corrupts filter memory.

6. **Memory Requirements**: Each SSF maintains 2 bars of state. For multi-period analysis, memory scales linearly with instance count.

## API Usage

```csharp
// Streaming mode
var ssfdsp = new Ssfdsp(period: 20);
foreach (var bar in bars)
{
    TValue result = ssfdsp.Update(new TValue(bar.Time, bar.Close), isNew: true);
    if (ssfdsp.IsHot)
    {
        // Use result.Value for signal generation
    }
}

// Bar correction (same bar, updated price)
TValue corrected = ssfdsp.Update(new TValue(bar.Time, newClose), isNew: false);

// Batch mode
TSeries output = Ssfdsp.Calculate(closePrices, period: 20);

// Chaining
var source = new Ema(10);
var ssfdsp = new Ssfdsp(source, period: 20);
// ssfdsp automatically subscribes to source.Pub events
```

## References

- Ehlers, J. (2013). *Cycle Analytics for Traders*. Wiley.
- Ehlers, J. (2001). *Rocket Science for Traders*. Wiley.
- Ehlers, J. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley.
- PineScript reference: `lib/cycles/ssfdsp/ssfdsp.pine`