# HWMA: Holt-Winters Moving Average

> *Triple exponential smoothing: because sometimes tracking level, velocity, and acceleration is exactly what a price series needs—and sometimes it's overkill. Holt and Winters figured this out for inventory forecasting in the 1950s. Traders rediscovered it decades later.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 10)                      |
| **Outputs**      | Single series (Hwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [hwma.pine](hwma.pine)                       |
| **Signature**    | [hwma_signature](hwma_signature.md) |

- HWMA is an Infinite Impulse Response (IIR) filter that applies triple exponential smoothing with level (F), velocity (V), and acceleration (A) comp...
- **Similar:** [HOLT](../holt/holt.md) | **Complementary:** Seasonal analysis | **Trading note:** Holt-Winters MA; triple exponential smoothing with seasonal component.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HWMA is an Infinite Impulse Response (IIR) filter that applies triple exponential smoothing with level (F), velocity (V), and acceleration (A) components. Unlike simple exponential smoothing which only tracks the current level, HWMA anticipates future values by extrapolating trend and trend changes.

## Historical Context

Charles C. Holt developed double exponential smoothing in 1957 at the Carnegie Institute of Technology to address the limitations of single exponential smoothing when dealing with trending data. Peter R. Winters extended Holt's method in 1960 to include seasonality components.

The "Holt-Winters" name typically refers to the full seasonal model, but the triple exponential smoothing variant used here focuses on the non-seasonal components: level, trend (velocity), and trend acceleration. This makes it suitable for financial time series where seasonal patterns are less relevant than trend dynamics.

In trading applications, HWMA's ability to track acceleration makes it particularly responsive to trend changes. When a price series begins accelerating in a direction, HWMA detects this faster than single or double exponential smoothing.

## Architecture & Physics

HWMA maintains three state components updated recursively:

* **Level (F)**: The smoothed estimate of the current value
* **Velocity (V)**: The smoothed estimate of the trend/slope
* **Acceleration (A)**: The smoothed estimate of the change in trend

Each component uses its own smoothing factor:

* **α (alpha)**: Level smoothing factor, derived as $\frac{2}{\text{period}+1}$
* **β (beta)**: Velocity smoothing factor, derived as $\frac{1}{\text{period}}$
* **γ (gamma)**: Acceleration smoothing factor, derived as $\frac{1}{\text{period}}$

The physics of HWMA reveal several key properties:

* **O(1) complexity**: Only three state variables, no buffer required
* **Infinite memory**: Past values influence output indefinitely (IIR characteristic)
* **Adaptive response**: Tracks not just where price is, but where it's going
* **Forecast capability**: Output includes extrapolation of velocity and half the acceleration

### The Compute Challenge

HWMA is computationally lightweight. Each update requires only a handful of multiplications and additions—no buffer management, no weight precomputation. The recursive nature means constant time regardless of the conceptual "period."

$$ \text{Runtime Cost} = O(1) \text{ per bar} $$

FMA (Fused Multiply-Add) instructions optimize the core calculations, combining multiplication and addition into single operations without intermediate rounding.

## Mathematical Foundation

The HWMA calculation proceeds in three stages per bar:

### 1. Level Update (F)

$$ F_t = \alpha \cdot P_t + (1-\alpha) \cdot (F_{t-1} + V_{t-1} + 0.5 \cdot A_{t-1}) $$

The level blends the current price with a forecast derived from the previous level, velocity, and half the acceleration.

### 2. Velocity Update (V)

$$ V_t = \beta \cdot (F_t - F_{t-1}) + (1-\beta) \cdot (V_{t-1} + A_{t-1}) $$

The velocity blends the observed change in level with the previous velocity extrapolated by acceleration.

### 3. Acceleration Update (A)

$$ A_t = \gamma \cdot (V_t - V_{t-1}) + (1-\gamma) \cdot A_{t-1} $$

The acceleration blends the observed change in velocity with the previous acceleration.

### 4. Output Calculation

$$ \text{HWMA}_t = F_t + V_t + 0.5 \cdot A_t $$

The output extrapolates the level by adding velocity and half the acceleration—a one-step forecast.

### Example Calculation

For period=10 (α≈0.182, β=0.1, γ=0.1):

Given $F_{t-1}=100$, $V_{t-1}=2$, $A_{t-1}=0.5$, and new price $P_t=105$:

1. **Forecast**: $F_{t-1} + V_{t-1} + 0.5 \cdot A_{t-1} = 100 + 2 + 0.25 = 102.25$
2. **New F**: $0.182 \times 105 + 0.818 \times 102.25 = 19.11 + 83.64 = 102.75$
3. **New V**: $0.1 \times (102.75 - 100) + 0.9 \times (2 + 0.5) = 0.275 + 2.25 = 2.525$
4. **New A**: $0.1 \times (2.525 - 2) + 0.9 \times 0.5 = 0.0525 + 0.45 = 0.5025$
5. **Output**: $102.75 + 2.525 + 0.5 \times 0.5025 = 105.53$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

HWMA is extremely lightweight—O(1) with minimal operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 3 | 4 | 12 |
| MUL | 3 | 3 | 9 |
| ADD/SUB | 4 | 1 | 4 |
| **Total** | **10** | — | **~25 cycles** |

**Hot path breakdown:**
- Level update: `FMA(prevF + prevV + 0.5×prevA, decayAlpha, alpha×val)` → 1 FMA + 2 MUL + 2 ADD
- Velocity update: `FMA(prevV + prevA, decayBeta, beta×(newF - prevF))` → 1 FMA + 1 MUL + 1 SUB
- Acceleration update: `FMA(prevA, decayGamma, gamma×(newV - prevV))` → 1 FMA + 1 MUL + 1 SUB
- Output: `newF + newV + 0.5×newA` → 2 ADD + 1 MUL

### Batch Mode (SIMD)

HWMA is recursive (IIR)—SIMD parallelization across bars is not possible. Each output depends on the previous state.

| Mode | Cycles/bar | Notes |
| :--- | :---: | :--- |
| Streaming (scalar) | ~25 | FMA-optimized |
| Batch (scalar) | ~25 | No SIMD benefit for IIR |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches definition to `double` precision |
| **Timeliness** | 9/10 | Acceleration tracking reduces effective lag |
| **Overshoot** | 6/10 | May overshoot during trend reversals |
| **Smoothness** | 7/10 | Good for trends; noisier during consolidation |

### Implementation Details

```csharp
// Initialization
double alpha = 2.0 / (period + 1.0);
double beta = 1.0 / period;
double gamma = 1.0 / period;
double decayAlpha = 1.0 - alpha;
double decayBeta = 1.0 - beta;
double decayGamma = 1.0 - gamma;

// Runtime (Update) with FMA
double newF = Math.FusedMultiplyAdd(prevF + prevV + 0.5 * prevA, decayAlpha, alpha * val);
double newV = Math.FusedMultiplyAdd(prevV + prevA, decayBeta, beta * (newF - prevF));
double newA = Math.FusedMultiplyAdd(prevA, decayGamma, gamma * (newV - prevV));
return newF + newV + 0.5 * newA;
```

## Comparison: Exponential Smoothing Variants

| Method | Components | Trend Handling | Overshoot Risk | Use Case |
| :--- | :--- | :--- | :--- | :--- |
| EMA | Level only | None | Low | Noise filtering |
| DEMA | Level + acceleration term | Implicit | Medium | Trend following |
| **HWMA** | **Level + Velocity + Acceleration** | **Explicit triple** | **Higher** | **Trend anticipation** |
| TEMA | Triple smoothing | Implicit | High | Responsive filtering |

Choose HWMA when you need explicit tracking of trend dynamics. The three-component model provides interpretable state (where it is, where it's going, how that's changing) but introduces overshoot risk during sharp reversals.

## Validation

QuanTAlib validates HWMA against its PineScript reference implementation.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Matches PineScript reference exactly. |
| **PineScript** | ✅ | Reference implementation. |
| **TA-Lib** | ❌ | Not included in standard distribution. |
| **Skender** | ❌ | Not included. |
| **Tulip** | ❌ | Not included. |
| **Ooples** | ❌ | Not included. |

## Common Pitfalls

1. **Overshoot During Reversals**: HWMA's acceleration component can cause overshoot when trends reverse sharply. The filter "expects" the trend to continue and takes time to adapt. Consider lower β/γ values for less aggressive acceleration tracking.

2. **Cold Start**: The first value initializes F to the input with V and A at zero. The filter needs several bars to establish meaningful velocity and acceleration estimates.

3. **Period vs. Smoothing Factors**: The period parameter sets all three smoothing factors. For fine-tuned control, use the explicit (α, β, γ) constructor. Higher values mean more responsiveness but also more noise.

4. **Interpretation**: HWMA output is a one-step forecast, not a smoothed current value. The extrapolation can lead the actual price in trending markets but lag during consolidation.

5. **Seasonal Confusion**: "Holt-Winters" often implies seasonal decomposition. This implementation is the non-seasonal variant focusing on level-trend-acceleration only.

6. **Parameter Sensitivity**: Small changes in β and γ significantly affect behavior. Start with the default period-based derivation before experimenting with custom values.