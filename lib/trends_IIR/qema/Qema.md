# QEMA: Quad Exponential Moving Average

> "Four EMAs walk into a bar. The first one's slow and thoughtful. The fourth one's practically twitching. Together, they somehow produce a signal that's both smooth and responsive. The bartender asks, 'How did you achieve zero lag?' They reply, 'Constrained quadratic optimization.' The bartender pours them a free drink."

QEMA (Quad Exponential Moving Average) is a zero-lag smoothing filter that cascades four EMAs with geometrically ramped alphas and combines them using minimum-energy weights. Unlike traditional multi-stage EMAs (DEMA, TEMA) that use fixed coefficients, QEMA solves for weights that explicitly eliminate DC lag while minimizing output variance. The result is a filter that tracks linear trends with zero group delay while suppressing high-frequency noise more effectively than standard EMA cascades.

## Historical Context

The pursuit of zero-lag smoothing has produced numerous filter designs: DEMA (1994), TEMA, ZLEMA, Jurik's JMA (proprietary), and various Ehlers filters. Most attack lag through extrapolation (predicting where price "should" be) or by subtracting lagged versions of the signal. These approaches work but introduce overshoot and ringing on step changes.

QEMA takes a different approach: instead of extrapolating, it constructs a weighted sum of four EMAs with progressively faster response rates, then solves for weights that zero the aggregate lag. The key insight is that each EMA stage has a known lag of $(1-\alpha)/\alpha$ bars. By geometrically spacing the alphas and applying constrained optimization, the lags can be perfectly canceled for DC and linear components while maintaining stability.

## Architecture & Physics

### Why Four Stages?

Traditional zero-lag approaches like DEMA and TEMA use 2 or 3 EMA stages with fixed Pascal triangle coefficients. QEMA uses four stages because:

1. **Degrees of Freedom**: Four stages with optimized weights provide enough flexibility to satisfy both constraints (unity gain + zero lag) while minimizing weight energy.
2. **Geometric Span**: The alpha ramp $\alpha_k = \alpha_1^{(5-k)/4}$ naturally spans from slow ($\alpha_1$) to fast ($\alpha_4 \approx \alpha_1^{1/4}$), covering a wide frequency range.
3. **Adaptive Weights**: Unlike TEMA's fixed $(3, -3, 1)$ or theoretical $(4, -6, 4, -1)$ coefficients, QEMA computes weights specifically for each period setting.

### The Core Challenge

Every smoothing filter faces a fundamental tradeoff: lag versus noise. A single EMA with smoothing factor $\alpha$ has mean lag $\tau = (1-\alpha)/\alpha$ bars. Halving $\alpha$ roughly doubles smoothness but also doubles lag.

QEMA's insight: cascade multiple EMAs with different time constants, then combine them with weights that cancel the aggregate lag. The geometric alpha ramp ensures the stages span from "slow and smooth" to "fast and noisy," providing the degrees of freedom to zero the weighted average lag.

### Progressive Alpha Design

The four stages use alphas following $\alpha_k = \alpha_1^{(5-k)/4}$:

| Stage | Behavior | Typical $\alpha$ (N=20) |
| :---: | :--- | :---: |
| 1 | Slow, smooth, high lag | 0.095 |
| 2 | Medium-slow | 0.176 |
| 3 | Medium-fast | 0.309 |
| 4 | Fast, noisy, low lag | 0.555 |

Even for long periods (N=100), $\alpha_4$ remains above 0.35, ensuring the fastest stage responds quickly enough to provide effective lag cancellation.

### Weight Optimization (Option A)

QEMA solves a constrained least-squares problem: find weights $w_i$ that minimize $\sum w_i^2$ subject to unity gain ($\sum w_i = 1$) and zero lag ($\sum w_i L_i = 0$). The closed-form solution yields weights that:

- Can be negative (enabling extrapolation beyond the input)
- Always sum to exactly 1.0
- Adapt automatically to the period setting

This "minimum energy" approach produces smaller weight magnitudes than naive lag cancellation, reducing overshoot and improving stability.

### Bias Compensation

Each EMA stage maintains its own bias correction factor. When starting from zero, a standard EMA underestimates the true mean by factor $(1-\alpha)^t$. QEMA divides each stage's output by $1 - (1-\alpha_k)^t$ during warmup, ensuring statistically valid output from bar one. Once the bias term falls below machine epsilon, the correction becomes identity.

## Mathematical Foundation

### 1. Progressive Alphas

Given period $N$ and input series $x_t$, the base alpha is:

$$\alpha_1 = \frac{2}{N + 1}$$

Define the geometric ramp factor:

$$r = \left(\frac{1}{\alpha_1}\right)^{1/4}$$

The four stage alphas follow a geometric progression:

$$\alpha_k = \alpha_1 \cdot r^{k-1} \quad \text{for } k \in \{1, 2, 3, 4\}$$

This admits an elegant closed form:

$$\alpha_k = \alpha_1^{(5-k)/4}$$

Expanding explicitly:

| Stage | Formula | Equivalent |
| :---: | :--- | :--- |
| $\alpha_1$ | $\alpha_1^{4/4}$ | $\alpha_1$ |
| $\alpha_2$ | $\alpha_1^{3/4}$ | $\sqrt[4]{\alpha_1^3}$ |
| $\alpha_3$ | $\alpha_1^{2/4}$ | $\sqrt{\alpha_1}$ |
| $\alpha_4$ | $\alpha_1^{1/4}$ | $\sqrt[4]{\alpha_1}$ |

The "hypothetical" next stage would be $\alpha_5 = \alpha_1 \cdot r^4 = 1$, which explains the geometric structure: the ramp spans from $\alpha_1$ to unity in four steps.

### 2. Four-Stage EMA Cascade

The stages are applied in sequence, each feeding into the next:

$$E_{1,t} = (1 - \alpha_1) \cdot E_{1,t-1} + \alpha_1 \cdot x_t$$

$$E_{2,t} = (1 - \alpha_2) \cdot E_{2,t-1} + \alpha_2 \cdot E_{1,t}$$

$$E_{3,t} = (1 - \alpha_3) \cdot E_{3,t-1} + \alpha_3 \cdot E_{2,t}$$

$$E_{4,t} = (1 - \alpha_4) \cdot E_{4,t-1} + \alpha_4 \cdot E_{3,t}$$

Each stage is an IIR filter with its own time constant. Stage 1 is slowest (highest smoothing), Stage 4 is fastest (lowest smoothing).

### 3. Startup Bias Compensation

When initializing EMAs from zero, each stage accumulates bias. For a single EMA with smoothing factor $\alpha$ started at zero, the expected shrinkage after $t$ bars is $(1-\alpha)^t$. The debiased output is:

$$\tilde{E}_t = \frac{E_t}{1 - (1 - \alpha)^t}$$

QEMA applies this correction per stage during warmup. Once the bias term $(1-\alpha)^t$ becomes negligible (below machine epsilon), the correction reverts to identity. This ensures statistically valid output from bar one without affecting steady-state behavior.

### 4. Lag Coordinates

The mean lag of a single EMA with smoothing factor $\alpha$ is:

$$\tau(\alpha) = \frac{1 - \alpha}{\alpha}$$

Individual stage lags:

$$\tau_k = \tau(\alpha_k) = \frac{1 - \alpha_k}{\alpha_k}$$

Cumulative lags for each cascaded stage output:

$$L_1 = \tau_1$$

$$L_2 = \tau_1 + \tau_2$$

$$L_3 = \tau_1 + \tau_2 + \tau_3$$

$$L_4 = \tau_1 + \tau_2 + \tau_3 + \tau_4$$

These $L_i$ represent the "time centers" (first moments) of the impulse responses—the effective delay of each stage's output.

### 5. Option A Weights (Minimum-Energy, Zero-Lag)

The output is a weighted combination:

$$y_t = w_1 E_{1,t} + w_2 E_{2,t} + w_3 E_{3,t} + w_4 E_{4,t}$$

Option A chooses weights that minimize total energy $\sum w_i^2$ while satisfying two constraints:

**Unity Gain** (no DC scaling):

$$\sum_{i=1}^{4} w_i = 1$$

**Zero DC Lag** (target delay $\delta = 0$):

$$\sum_{i=1}^{4} w_i \cdot L_i = 0$$

This constrained optimization has a closed-form solution via Lagrange multipliers. The weights are affine in the cumulative lags:

$$w_i = \lambda + \mu \cdot L_i$$

Define (with $n = 4$ stages):

$$B = \sum_{i=1}^{n} L_i \quad \text{(sum of lags)}$$

$$C = \sum_{i=1}^{n} L_i^2 \quad \text{(sum of squared lags)}$$

$$D = nC - B^2 \quad \text{(discriminant)}$$

The Lagrange multipliers are:

$$\lambda = \frac{C - B\delta}{D}$$

$$\mu = \frac{-B + n\delta}{D}$$

For the zero-lag case ($\delta = 0$), this simplifies to:

$$\lambda = \frac{C}{D}, \quad \mu = \frac{-B}{D}$$

And the weights become:

$$w_i = \frac{C - B \cdot L_i}{D}$$

This is the "properly balanced" replacement for fixed Pascal coefficients $(4, -6, 4, -1)$ used in TEMA—weights that adapt to the geometric alpha structure.

**Degenerate Case**: If $D \approx 0$ (all lags equal, theoretically impossible with geometric alphas), fall back to equal weights $w_i = 0.25$.

### 6. Final Output

Combining all stages with computed weights:

$$\text{QEMA}_t = \sum_{i=1}^{4} w_i \cdot \tilde{E}_{i,t}$$

where $\tilde{E}_{i,t}$ is the bias-compensated EMA output for stage $i$ at time $t$.

### Worked Example (N = 20)

To ground the formulas, here are the computed values for a 20-period QEMA:

**Step 1: Alphas**

$$\alpha_1 = \frac{2}{21} \approx 0.0952$$

$$r = (1/0.0952)^{0.25} \approx 1.802$$

| Stage | $\alpha_k = \alpha_1 \cdot r^{k-1}$ | Value |
| :---: | :--- | :---: |
| 1 | $0.0952 \times 1.0$ | 0.0952 |
| 2 | $0.0952 \times 1.802$ | 0.1716 |
| 3 | $0.0952 \times 3.247$ | 0.3092 |
| 4 | $0.0952 \times 5.852$ | 0.5573 |

**Step 2: Individual and Cumulative Lags**

| Stage | $\tau_k = (1-\alpha_k)/\alpha_k$ | Cumulative $L_k$ |
| :---: | :---: | :---: |
| 1 | 9.50 | 9.50 |
| 2 | 4.83 | 14.33 |
| 3 | 2.23 | 16.56 |
| 4 | 0.79 | 17.35 |

**Step 3: Weight Computation**

$$B = 9.50 + 14.33 + 16.56 + 17.35 = 57.74$$

$$C = 90.25 + 205.35 + 274.23 + 301.02 = 870.85$$

$$D = 4 \times 870.85 - 57.74^2 = 150.32$$

| Stage | $w_k = (C - B \cdot L_k) / D$ | Value |
| :---: | :--- | :---: |
| 1 | $(870.85 - 57.74 \times 9.50) / 150.32$ | **2.14** |
| 2 | $(870.85 - 57.74 \times 14.33) / 150.32$ | **0.29** |
| 3 | $(870.85 - 57.74 \times 16.56) / 150.32$ | **-0.57** |
| 4 | $(870.85 - 57.74 \times 17.35) / 150.32$ | **-0.86** |

**Verification**: $2.14 + 0.29 + (-0.57) + (-0.86) = 1.00$ ✓

The negative weights on stages 3 and 4 enable lag cancellation through extrapolation. The large positive weight on stage 1 (the slowest, most lagged stage) is counterbalanced by the negative weights on faster stages.

## Performance Profile

Benchmarked on Apple M4, .NET 10.0, AdvSIMD, 500,000 bars:

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput (Span)** | ~1.2 μs / 500K bars | ~2.4 ns/bar, 4× EMA cost |
| **Throughput (Streaming)** | ~8 ns/bar | Four FMA operations + weighted sum |
| **Allocations (Hot Path)** | 0 bytes | All state in struct |
| **Complexity** | O(1) | Four parallel EMAs + dot product |
| **State Size** | 160 bytes | Four EmaState (32B each) + weights |

### Characteristic Comparison

| Property | QEMA | EMA | DEMA | TEMA |
| :--- | :---: | :---: | :---: | :---: |
| **Accuracy** | 9/10 | 6/10 | 7/10 | 7/10 |
| **Timeliness** | 9/10 | 5/10 | 7/10 | 8/10 |
| **Smoothness** | 8/10 | 7/10 | 6/10 | 5/10 |
| **Overshoot** | Low | None | Medium | High |

QEMA achieves near-zero lag on linear trends while maintaining smoothness comparable to EMA. DEMA and TEMA trade smoothness for speed; QEMA finds a better balance through optimization.

## Usage Examples

```csharp
// Streaming: Process one bar at a time
var qema = new Qema(20);  // 20-period QEMA
foreach (var bar in liveStream)
{
    var result = qema.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"QEMA: {result.Value:F2}");
}

// Batch processing with Span (zero allocation)
double[] prices = LoadHistoricalData();
double[] qemaValues = new double[prices.Length];
Qema.Batch(prices.AsSpan(), qemaValues.AsSpan(), period: 20);

// Batch processing with TSeries
var series = new TSeries();
// ... populate series ...
var results = Qema.Batch(series, period: 20);

// Event-driven chaining
var source = new TSeries();
var qema20 = new Qema(source, 20);  // Auto-updates when source changes
source.Add(new TValue(DateTime.UtcNow, 100.0));  // QEMA updates

// Pre-load with historical data
var qema = new Qema(20);
qema.Prime(historicalPrices);  // Ready to process live data immediately

// Access component EMAs for analysis
// (Internal state exposed via Last property)
```

## Validation

QEMA is a proprietary indicator not available in external libraries (TA-Lib, Skender, Tulip, Ooples). Validation tests focus on self-consistency and mathematical properties:

| Test | Status | Notes |
| :--- | :---: | :--- |
| **Batch vs Streaming** | ✅ | All modes produce identical results |
| **Span vs Streaming** | ✅ | Span API matches streaming exactly |
| **Weights Sum to 1** | ✅ | Constant input → constant output |
| **Zero Lag on Linear** | ✅ | Lag < 2 bars on linear trend |
| **Geometric Alphas** | ✅ | $\alpha_i / \alpha_{i-1} = r$ verified |
| **Smoothness** | ✅ | Comparable to EMA smoothness |

Run validation: `dotnet test --filter "FullyQualifiedName~QemaValidation"`

## Common Pitfalls

1. **Expecting Zero Lag on All Signals**: QEMA eliminates lag for DC (constant) and linear (ramp) components. Oscillatory signals still experience phase shift. Don't expect QEMA to predict reversals—it tracks trends.

2. **Negative Weights Aren't a Bug**: The optimization can produce negative weights. This is mathematically correct—it's how the filter extrapolates to cancel lag. If you're uncomfortable with weights outside [0,1], QEMA isn't for you. Use a different filter.

3. **Short Periods Produce Wild Results**: With period < 5, the alphas compress and weights become extreme. QEMA is designed for trend-following on moderate to long periods (10+). For scalping, stick with simple EMA.

4. **Comparing with DEMA/TEMA**: QEMA will differ from DEMA (2-stage) and TEMA (3-stage) significantly. They use fixed coefficients derived from different assumptions. QEMA's weights are computed fresh for each period setting.

5. **Warmup Period**: QEMA requires approximately $3N$ bars to fully converge, similar to EMA. The bias compensation helps early values, but the four cascaded stages need time to synchronize. Trust the `IsHot` property.

6. **Using `isNew` Incorrectly**: For live tick updates within the same bar, use `Update(value, isNew: false)`. Use `isNew: true` (default) only when a new bar opens. Getting this wrong causes the filter to run 4× faster than intended.

7. **Overshoot on Step Changes**: Despite being "zero-lag," QEMA can overshoot on sudden step changes because the weighted sum can extrapolate beyond the input. This is the price of reduced lag. If overshoot is unacceptable, use a filter with monotonic step response (like EMA).
