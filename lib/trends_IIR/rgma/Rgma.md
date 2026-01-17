# RGMA: Recursive Gaussian Moving Average

> "The statisticians wanted Gaussian smoothing. The HFT folks wanted O(1) updates. RGMA splits the difference: chain enough cheap EMAs together and the impulse response starts looking suspiciously bell-shaped. It's not real Gaussian—but the market doesn't know that."

RGMA (Recursive Gaussian Moving Average) approximates Gaussian smoothing by cascading multiple identical exponential moving averages. Each pass through an EMA filter smooths the signal further, and the mathematical magic is that cascaded low-pass filters push the impulse response toward a Gaussian-like shape. You get the desirable properties of Gaussian smoothing—smooth frequency roll-off, minimal ringing, symmetric lag—without the computational cost of a true FIR convolution.

## Historical Context

True Gaussian filtering is a gold standard in signal processing. The Gaussian kernel has the unique property of having no negative lobes in either time or frequency domain, which translates to smooth, overshoot-free filtering. But FIR Gaussian filters require keeping a window of samples and computing a weighted sum each update.

The insight behind RGMA is that you can approximate a Gaussian with cascaded first-order filters. This is related to the Central Limit Theorem: the convolution of multiple distributions tends toward a Gaussian. By running data through the same EMA multiple times (passes), each pass adds to the "bell curve-ness" of the overall response. With just 3-4 passes, you get something close enough to Gaussian that the difference is negligible for most trading applications.

## Architecture & Physics

RGMA chains `passes` identical exponential filters:

1. **Stage 0**: Apply EMA to the raw price
2. **Stage 1**: Apply EMA to Stage 0's output
3. **Stage 2**: Apply EMA to Stage 1's output
4. ... continue for all passes
5. **Output**: Final stage's value

The key innovation is the alpha calculation. To achieve equivalent smoothing to a single Gaussian filter of width N, the individual EMA alpha is adjusted:

$$\alpha = \frac{2}{\frac{N}{\sqrt{\text{passes}}} + 1}$$

The $\sqrt{\text{passes}}$ factor compensates for the fact that cascading filters increases effective smoothing. Without this adjustment, RGMA with 3 passes would be much smoother than a comparable EMA—possibly too smooth. The square root normalization keeps the effective period roughly equivalent while delivering the improved impulse response shape.

### Why It Works: The Math Behind the Magic

When you cascade identical low-pass filters, the frequency response multiplies:

$$H_{\text{total}}(f) = H_{\text{single}}(f)^{\text{passes}}$$

A single EMA has a 6 dB/octave roll-off—gentle, but with significant energy leaking through at high frequencies. Three cascaded EMAs give you 18 dB/octave—much steeper, much cleaner. The time-domain impulse response transitions from the sharp exponential decay of a single EMA toward the smooth bell curve of a Gaussian.

## Mathematical Foundation

The alpha calculation from the Pine reference:

$$ \alpha = \frac{2}{\frac{N}{\sqrt{P}} + 1} $$

Where $N$ is the period and $P$ is the number of passes.

Each filter stage applies the standard EMA formula:

$$ f_0[t] = \alpha \cdot (x_t - f_0[t-1]) + f_0[t-1] $$

$$ f_i[t] = \alpha \cdot (f_{i-1}[t] - f_i[t-1]) + f_i[t-1] \quad \text{for } i = 1 \ldots P-1 $$

The output is the final stage:

$$ \text{RGMA}_t = f_{P-1}[t] $$

Using FMA (fused multiply-add) form for each stage:

$$ f_i[t] = \text{FMA}(\alpha, f_{i-1}[t] - f_i[t-1], f_i[t-1]) $$

### Special Cases

- **passes = 1**: Degenerates to standard EMA with $\alpha = \frac{2}{N+1}$
- **passes = 2**: Equivalent to a cascaded double-EMA (but NOT the same as DEMA, which uses a different formula)
- **passes → ∞**: Approaches true Gaussian smoothing (practically, 4-5 passes is sufficient)

## Performance Profile

### Operation Count (Streaming Mode)

RGMA cascades P identical EMA stages. Each stage requires one FMA operation:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (input - prev_stage) per stage | P | 1 | P |
| FMA (α × diff + prev) per stage | P | 4 | 4P |
| **Total (hot)** | **2P** | — | **~5P cycles** |

For typical passes values:

| Passes | Operations | Total Cycles |
| :---: | :---: | :---: |
| 1 | 2 | ~5 cycles |
| 2 | 4 | ~10 cycles |
| 3 (default) | 6 | ~15 cycles |
| 4 | 8 | ~20 cycles |
| 5 | 10 | ~25 cycles |

During warmup, each EMA stage has additional compensator overhead (~20 cycles × P).

**Total during warmup:** ~25P cycles/bar; **Post-warmup:** ~5P cycles/bar.

### Batch Mode (SIMD Analysis)

RGMA is inherently recursive—each stage depends on its previous output, and each bar depends on the previous bar. SIMD parallelization across bars is not possible:

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | One FMA per stage already optimal |
| Loop unrolling | Compiler can unroll small pass counts |
| Cache locality | Filter array fits in L1 cache |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput (Batch)** | ~1.2 ms / 500K bars | ~2.4 ns/bar at passes=3 |
| **Throughput (Streaming)** | ~3-4 ns/bar | Depends on passes count |
| **Allocations (Hot Path)** | 0 bytes | Filter states in fixed arrays |
| **Complexity** | O(passes) | One FMA per pass per bar |
| **State Size** | 24 + 8×passes bytes | State struct + filter array |

### Quality Metrics

| Quality | Score (1-10) | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8 | Tracks trends well, minimal distortion |
| **Timeliness** | 6 | More lag than single EMA (expected) |
| **Smoothness** | 9 | Primary benefit—Gaussian-like smooth |
| **Overshoot** | 2 | Very low—Gaussian response minimizes ringing |

### Passes Trade-offs

| Passes | Smoothness | Lag | Use Case |
| :---: | :---: | :---: | :--- |
| 1 | Low | Low | Equivalent to EMA |
| 2 | Medium | Medium | Smoother EMA alternative |
| 3 | High | Medium-High | Default—good balance |
| 4+ | Very High | High | When maximum smoothness is priority |

## Usage Examples

```csharp
// Streaming: Process one bar at a time
var rgma = new Rgma(20, passes: 3);  // 20-period, 3 passes (default)
foreach (var bar in liveStream)
{
    var result = rgma.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"RGMA: {result.Value:F2}");
}

// Different passes for different smoothness levels
var light = new Rgma(20, passes: 2);   // Lighter smoothing, less lag
var standard = new Rgma(20, passes: 3); // Standard (default)
var heavy = new Rgma(20, passes: 5);   // Heavy smoothing, more lag

// When passes = 1, RGMA equals EMA (with same alpha formula)
var asEma = new Rgma(20, passes: 1);  // Degenerates to EMA

// Batch processing with Span (zero allocation)
double[] prices = LoadHistoricalData();
double[] rgmaValues = new double[prices.Length];
Rgma.Batch(prices.AsSpan(), rgmaValues.AsSpan(), period: 20, passes: 3);

// Batch processing with TSeries
var series = new TSeries();
// ... populate series ...
var results = Rgma.Batch(series, period: 20, passes: 3);

// Event-driven chaining
var source = new TSeries();
var rgma20 = new Rgma(source, 20, 3);  // Auto-updates when source changes
source.Add(new TValue(DateTime.UtcNow, 100.0));  // RGMA updates

// Pre-load with historical data
var rgma = new Rgma(20, 3);
rgma.Prime(historicalPrices);  // Ready to process live data immediately

// Comparing smoothing levels
var ema = new Ema(20);
var rgma3 = new Rgma(20, 3);
// RGMA will be noticeably smoother but lag slightly more
```

## Validation

Validated in `Rgma.Validation.Tests.cs`:

| Test | Status | Notes |
| :--- | :---: | :--- |
| **Passes=1 matches EMA** | ✅ | RGMA(period, 1) matches EMA(period/sqrt(1)) |
| **Mode consistency** | ✅ | Batch, Streaming, Span, Eventing all match |
| **Smoothness increases with passes** | ✅ | Variance of changes decreases |
| **Prime consistency** | ✅ | Prime() produces same results as streaming |

Run validation: `dotnet test --filter "FullyQualifiedName~RgmaValidation"`

## Common Pitfalls

1. **Confusing with DEMA/TEMA**: RGMA is NOT Double or Triple EMA. DEMA and TEMA use algebraic combinations to reduce lag. RGMA cascades EMAs to improve impulse response shape—it trades lag for smoothness, not the other way around.

2. **Expecting EMA-Equivalent Period**: Due to the $\sqrt{\text{passes}}$ normalization in alpha, RGMA(20, 3) has similar *smoothing* to EMA(20), but not identical response. The cascade changes the shape of the filter, not just its magnitude.

3. **Over-smoothing with Many Passes**: Each additional pass adds lag. Beyond 4-5 passes, you're paying lag cost for diminishing smoothness improvements. For most trading applications, 3 passes is the sweet spot.

4. **Using for Crossover Signals**: RGMA's added lag makes crossover signals slower than EMA-based ones. If speed matters more than smoothness, use EMA or consider DEMA/TEMA which reduce lag.

5. **Forgetting `isNew` for Live Data**: When processing live ticks within the same bar, use `Update(value, isNew: false)` to update without advancing state. Use `isNew: true` (default) only when a new bar opens.

6. **Comparing to "Gaussian" Filters in Other Platforms**: Different platforms implement Gaussian-like smoothing differently. Some use true FIR Gaussian kernels, others use different approximations. RGMA's cascaded EMA approach is one valid method but won't match a true Gaussian implementation.

## When to Use RGMA

RGMA is ideal when:
- You need smooth signals without the overshoot/ringing of other filters
- Clean frequency roll-off matters (reducing aliasing, harmonic artifacts)
- You're filtering for visualization or trend identification
- Gaussian-like properties are desired at IIR computational cost

RGMA is less suitable when:
- Minimum lag is critical (use EMA, DEMA, or TEMA)
- You need true Gaussian filtering for statistical applications
- Comparing against external libraries expecting specific Gaussian implementations
- Signal crossovers need to be responsive

## References

- TradingView reference implementation: `lib/trends_IIR/rgma/rgma.pine`
- Central Limit Theorem and cascaded filter theory: Smith, S.W. *The Scientist and Engineer's Guide to Digital Signal Processing*, Chapter 15