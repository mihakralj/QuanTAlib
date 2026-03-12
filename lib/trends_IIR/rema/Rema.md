# REMA: Regularized Exponential Moving Average

> *Someone looked at the EMA and thought: 'What if we punished it for changing its mind?' The result is REMA—an EMA with a conscience that remembers where it was going and resists the temptation to chase every price wiggle.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `lambda` (default 0.5)                      |
| **Outputs**      | Single series (Rema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [rema.pine](rema.pine)                       |
| **Signature**    | [rema_signature](rema_signature.md) |

- REMA (Regularized Exponential Moving Average) combines exponential smoothing with a regularization term that penalizes deviations from the previous...
- Parameterized by `period`, `lambda` (default 0.5).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

REMA (Regularized Exponential Moving Average) combines exponential smoothing with a regularization term that penalizes deviations from the previous trend direction. The result is a filter that responds to genuine price movements while suppressing noise-induced oscillations. Think of it as an EMA with momentum awareness: it knows where it was heading and applies a penalty for sudden course corrections.

## Historical Context

The concept of regularization comes from machine learning and signal processing, where it's used to prevent overfitting by penalizing model complexity. REMA applies this principle to moving averages: the "complexity" being penalized is deviation from the established trend. When price noise tries to yank the average in a new direction, the regularization term pushes back, saying "prove it." The lambda parameter controls how much proof is required—at lambda=1, REMA believes everything (standard EMA); at lambda=0, it's pure momentum that ignores new information entirely.

## Architecture & Physics

REMA introduces a two-component calculation:

1. **EMA Component**: Standard exponential smoothing that responds to new prices
2. **Regularization Component**: Momentum continuation that extrapolates the previous trend

The lambda parameter blends these components:

* **lambda = 1**: Pure EMA behavior. Every price gets full consideration.
* **lambda = 0.5**: Balanced. New prices compete with trend momentum.
* **lambda = 0**: Pure momentum extrapolation. New prices are ignored entirely (not recommended).

The regularization component calculates where the average *would be* if the current trend continued unchanged. The final REMA value is a weighted blend between where EMA wants to go (following price) and where momentum wants to go (continuing trend).

### The Compensator (Warmup Correction)

Like QuanTAlib's EMA implementation, REMA includes a mathematical compensator that corrects for initialization bias. The first N bars aren't approximations—they're mathematically valid from bar one. This means REMA(lambda=1) will match QuanTAlib's EMA implementation exactly, including the bias-corrected warmup period.

## Mathematical Foundation

The standard EMA alpha calculation:

$$ \alpha = \frac{2}{N + 1} $$

The EMA component (standard exponential smoothing):

$$ \text{EMA}_t = \alpha \cdot (P_t - \text{REMA}_{t-1}) + \text{REMA}_{t-1} $$

The regularization component (momentum continuation):

$$ \text{REG}_t = \text{REMA}_{t-1} + (\text{REMA}_{t-1} - \text{REMA}_{t-2}) $$

The final REMA calculation:

$$ \text{REMA}_t = \lambda \cdot (\text{EMA}_t - \text{REG}_t) + \text{REG}_t $$

This can be expanded:

$$ \text{REMA}_t = \lambda \cdot \text{EMA}_t + (1 - \lambda) \cdot \text{REG}_t $$

When $\lambda = 1$: $\text{REMA}_t = \text{EMA}_t$ (standard EMA)

When $\lambda = 0$: $\text{REMA}_t = \text{REG}_t$ (pure momentum extrapolation)

### Bias Compensation

To handle initialization bias, the compensator tracks the sum of weights:

$$ E_t = (1 - \alpha)^t $$

$$ \text{Corrected REMA}_t = \frac{\text{Uncorrected REMA}_t}{1 - E_t} $$

## Performance Profile

### Operation Count (Streaming Mode)

REMA combines EMA with a regularization term that extrapolates trend:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (Pt - REMAt-1) | 1 | 1 | 1 |
| FMA (EMA update) | 1 | 4 | 4 |
| SUB (momentum: REMAt-1 - REMAt-2) | 1 | 1 | 1 |
| ADD (REG: prev + momentum) | 1 | 1 | 1 |
| SUB (EMA - REG) | 1 | 1 | 1 |
| FMA (λ × diff + REG) | 1 | 4 | 4 |
| **Total (hot)** | **6** | — | **~12 cycles** |

During warmup (bias compensation active):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (E × decay) | 1 | 3 | 3 |
| SUB (1 - E) | 1 | 1 | 1 |
| DIV (correction) | 1 | 15 | 15 |
| CMP (warmup check) | 1 | 1 | 1 |
| **Warmup overhead** | **4** | — | **~20 cycles** |

**Total during warmup:** ~32 cycles/bar; **Post-warmup:** ~12 cycles/bar.

### Batch Mode (SIMD Analysis)

REMA is inherently recursive due to state dependency on previous two values. SIMD parallelization across bars is not possible:

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | Already using 2 FMAs per bar |
| State locality | REMA + PrevRema fit in registers |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput (Batch)** | ~400 μs / 500K bars | ~0.8 ns/bar |
| **Throughput (Streaming)** | ~2 ns/bar | Single Update() call |
| **Allocations (Hot Path)** | 0 bytes | Verified via BenchmarkDotNet |
| **Complexity** | O(1) | Two FMA operations per bar |
| **State Size** | 48 bytes | REMA, PrevRema, E, flags, counter |

### Quality Metrics

| Quality | Score (1-10) | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8 | Tracks price well when lambda > 0.5 |
| **Timeliness** | 7 | Regularization adds slight lag vs pure EMA |
| **Smoothness** | 9 | Primary benefit—significantly smoother than EMA |
| **Overshoot** | 3 | Low overshoot due to momentum awareness |

## Usage Examples

```csharp
// Streaming: Process one bar at a time
var rema = new Rema(20, lambda: 0.5);  // 20-period, balanced regularization
foreach (var bar in liveStream)
{
    var result = rema.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"REMA: {result.Value:F2}");
}

// Different lambda values for different behaviors
var smooth = new Rema(20, lambda: 0.3);   // Strong regularization, very smooth
var balanced = new Rema(20, lambda: 0.5); // Balanced (default)
var responsive = new Rema(20, lambda: 0.8); // Weak regularization, more responsive

// When lambda = 1, REMA equals EMA
var asEma = new Rema(20, lambda: 1.0);  // Equivalent to Ema(20)

// Batch processing with Span (zero allocation)
double[] prices = LoadHistoricalData();
double[] remaValues = new double[prices.Length];
Rema.Batch(prices.AsSpan(), remaValues.AsSpan(), period: 20, lambda: 0.5);

// Batch processing with TSeries
var series = new TSeries();
// ... populate series ...
var results = Rema.Batch(series, period: 20, lambda: 0.5);

// Event-driven chaining
var source = new TSeries();
var rema20 = new Rema(source, 20, 0.5);  // Auto-updates when source changes
source.Add(new TValue(DateTime.UtcNow, 100.0));  // REMA updates

// Pre-load with historical data
var rema = new Rema(20, 0.5);
rema.Prime(historicalPrices);  // Ready to process live data immediately
```

## Validation

Validated in `Rema.Validation.Tests.cs`:

| Test | Status | Notes |
| :--- | :---: | :--- |
| **Lambda=1 matches EMA** | ✅ | REMA(period, 1.0) equals EMA(period) |
| **Mode consistency** | ✅ | Batch, Streaming, Span, Eventing all match |
| **Smoothing behavior** | ✅ | Lower lambda produces smoother output |
| **Prime consistency** | ✅ | Prime() produces same results as streaming |

Run validation: `dotnet test --filter "FullyQualifiedName~RemaValidation"`

## Common Pitfalls

1. **Lambda Confusion**: lambda=1 is standard EMA (no regularization), lambda=0 is pure momentum (ignores new prices). Most use cases want something in between. Start with 0.5 and adjust based on your tolerance for lag vs smoothness.

2. **Not a Prediction Tool**: The regularization component extrapolates trend, but REMA is not a forecasting indicator. It's a filter that resists noise. Don't interpret the momentum component as a price prediction.

3. **Comparing to Other Implementations**: REMA isn't standardized across platforms. The formula here matches the PineScript reference implementation. Other platforms may implement "regularized" averages differently.

4. **Over-regularization**: Setting lambda too low (below 0.3) makes REMA extremely laggy and unresponsive. It will miss genuine trend changes. Use lower lambda values only for visualization or as a baseline reference, not for signal generation.

5. **Using REMA(20, 0.5) Like EMA(20)**: Due to regularization, REMA with lambda < 1 will lag behind EMA. If you're replacing an EMA-based strategy, you may need to reduce the period to compensate, or use higher lambda values.

6. **Forgetting `isNew` for Live Data**: When processing live ticks within the same bar, use `Update(value, isNew: false)` to update without advancing state. Use `isNew: true` (default) only when a new bar opens.

## When to Use REMA

REMA is ideal when:
- You need smoother signals than EMA provides
- Noise-induced whipsaws are causing false signals
- You want to maintain trend-following behavior with reduced sensitivity to outliers
- Your strategy benefits from a filter that "commits" to trends

REMA is less suitable when:
- You need maximum responsiveness (use EMA instead)
- You're comparing against external libraries that don't implement REMA
- You need predictable, standardized behavior across platforms
