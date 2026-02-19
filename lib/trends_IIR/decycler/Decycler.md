# DECYCLER: Ehlers Decycler

> "The trend is what remains when you stop looking for cycles."

The Ehlers Decycler extracts the trend component from a price series by subtracting a 2-pole Butterworth high-pass filter from the source signal. Where most moving averages blur the boundary between trend and cycle, the Decycler defines it with a frequency-domain cutoff: cycles shorter than the specified period are removed, everything longer stays. The result is an overlay that hugs price with near-zero lag during trends and rejects short-term oscillations without the smoothing artifacts of convolution-based averages.

## Historical Context

John Ehlers introduced the Decycler in "Decyclers" (*Technical Analysis of Stocks & Commodities*, September 2015), alongside its oscillator cousin DECO. The insight was characteristically Ehlers: if a high-pass filter isolates cycles, then subtracting that filter's output from price isolates the trend. One subtraction. No iterative smoothing. No window functions. No lag-vs-smoothness tradeoff negotiations.

The idea predates the 2015 article. Ehlers had been building 2-pole Butterworth high-pass filters since *Cybernetic Analysis for Stocks and Futures* (2004), primarily for cycle measurement. The Decycler simply inverts the question: instead of asking "what are the cycles?", it asks "what is everything except the cycles?"

Traditional trend followers face a fundamental tension. Moving averages introduce lag proportional to their smoothing window. Adaptive averages (KAMA, FRAMA, JMA) reduce lag during trends but add complexity and parameters. The Decycler sidesteps the problem entirely. It defines trend as a frequency band, not a smoothing operation. The cutoff period maps directly to the frequency boundary. There is exactly one parameter. The phase response of the complementary filter ($1 - H_{HP}$) preserves the phase of passed frequencies, producing minimal lag for trend components that fall below the cutoff.

Most trading platforms do not implement the Decycler natively. It does not appear in TA-Lib, Skender, Tulip, or OoplesFinance. The PineScript reference implementation in this repository serves as the canonical validation source.

## Architecture & Physics

The Decycler is a complementary filter: it computes $1 - H_{HP}(z)$, where $H_{HP}$ is a 2-pole Butterworth high-pass filter. This architecture has two components.

### 1. Butterworth 2-Pole High-Pass Filter

The HP filter uses a second-order IIR structure with coefficients derived from the cutoff period. The $0.707$ factor ($1/\sqrt{2}$) places the filter response at the $-3$ dB Butterworth design point, ensuring maximally flat passband behavior.

The frequency parameter:

$$
\omega = \frac{0.707 \times 2\pi}{P}
$$

The smoothing coefficient:

$$
\alpha = \frac{\cos(\omega) + \sin(\omega) - 1}{\cos(\omega)}
$$

The recurrence coefficients:

$$
a_1 = \left(1 - \frac{\alpha}{2}\right)^2, \quad b_1 = 2(1 - \alpha), \quad c_1 = -(1 - \alpha)^2
$$

The HP recurrence (2nd-order difference equation):

$$
\text{HP}_t = a_1(x_t - 2x_{t-1} + x_{t-2}) + b_1 \cdot \text{HP}_{t-1} + c_1 \cdot \text{HP}_{t-2}
$$

### 2. Complementary Subtraction

The Decycler output is the residual after removing the high-pass component:

$$
\text{Decycler}_t = x_t - \text{HP}_t
$$

### Z-Domain Transfer Function

The HP filter has the transfer function:

$$
H_{HP}(z) = \frac{a_1(1 - 2z^{-1} + z^{-2})}{1 - b_1 z^{-1} - c_1 z^{-2}}
$$

The Decycler's transfer function is the complementary lowpass:

$$
H_{DC}(z) = 1 - H_{HP}(z)
$$

This complementary structure guarantees that $H_{DC}(z) + H_{HP}(z) = 1$ at all frequencies. No signal energy is created or destroyed. The trend and cycle components sum exactly to the original price.

### Frequency Response Characteristics

- Frequencies **below** the cutoff period pass through with unity gain and near-zero phase shift
- Frequencies **above** the cutoff are attenuated at $-12$ dB/octave (2-pole rolloff)
- The $-3$ dB point occurs at period $P$, meaning cycles at the cutoff period are attenuated by $\approx 29\%$

## Mathematical Foundation

### Alpha Derivation

Starting from the Butterworth design frequency:

$$
\omega = \frac{0.707 \times 2\pi}{P}
$$

where $P$ is the cutoff period. The $0.707 = 1/\sqrt{2}$ factor is the Butterworth normalization that places the half-power point at the specified frequency.

The bilinear transform approximation yields:

$$
\alpha = \frac{\cos(\omega) + \sin(\omega) - 1}{\cos(\omega)}
$$

### Coefficient Derivation

From $\alpha$, three recurrence coefficients are computed once at construction:

| Coefficient | Formula | Role |
| :--- | :--- | :--- |
| $a_1$ | $(1 - \alpha/2)^2$ | Input gain (second difference) |
| $b_1$ | $2(1 - \alpha)$ | First feedback term |
| $c_1$ | $-(1 - \alpha)^2$ | Second feedback term |

### HP Filter Recurrence

$$
\text{HP}_t = a_1 \underbrace{(x_t - 2x_{t-1} + x_{t-2})}_{\text{second difference}} + b_1 \cdot \text{HP}_{t-1} + c_1 \cdot \text{HP}_{t-2}
$$

The second difference operator $(x_t - 2x_{t-1} + x_{t-2})$ acts as a discrete approximation to the second derivative, rejecting DC and linear trends.

### Decycler Output

$$
\text{Decycler}_t = x_t - \text{HP}_t
$$

### Parameter Mapping

| Parameter | Default | Range | Effect |
| :--- | :--- | :--- | :--- |
| `period` | 60 | $\geq 2$ | Cutoff period in bars. Larger values pass more cycle content (smoother). Smaller values track price more closely (less smooth). |

### Initialization

For $t < 2$ (fewer than 3 bars of history), $\text{HP}_t = 0$ and $\text{Decycler}_t = x_t$. The filter requires two prior source values and two prior HP values to engage the recurrence.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

One Decycler update requires the following operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 4 | 1 | 4 |
| MUL | 3 | 3 | 9 |
| FMA | 2 | 4 | 8 |
| CMP | 1 | 1 | 1 |
| **Total** | **10** | | **~22 cycles** |

Coefficient computation ($\cos$, $\sin$, division) occurs once at construction and is excluded from per-bar cost.

### Batch Mode (SIMD Analysis)

The HP recurrence is inherently sequential: each bar depends on $\text{HP}_{t-1}$ and $\text{HP}_{t-2}$. SIMD parallelization across bars is not possible for the recursive portion. However, the final subtraction ($x_t - \text{HP}_t$) is element-wise and could be vectorized in a two-pass approach. The implementation uses a single fused loop for cache efficiency, as the subtraction cost is negligible relative to loop overhead.

| Mode | Cycles/bar | Notes |
| :--- | :---: | :--- |
| Scalar streaming | ~22 | FMA-optimized hot path |
| Batch (fused loop) | ~22 | Same cost; recursion prevents parallelism |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact complementary filter; no approximation error |
| **Timeliness** | 9/10 | Near-zero phase lag for passed frequencies |
| **Overshoot** | 9/10 | No overshoot (lowpass complement of Butterworth) |
| **Smoothness** | 8/10 | Smooth but not as aggressive as dedicated smoothers |
| **Simplicity** | 10/10 | One parameter, one subtraction, zero ambiguity |

## Parameters

| Name | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `period` | `int` | 60 | Cutoff period for the high-pass filter. Must be $\geq 2$. |

**Period selection guidance:**

- **20-30**: Responsive trend line, tracks swings. Suitable for short-term trading.
- **40-80**: Balanced smoothing. The default of 60 works well for daily timeframes.
- **100-200**: Heavy smoothing, reveals only major trend direction. Useful for position trading or regime detection.

## Validation

No external open-source library implements the Ehlers Decycler. Validation is performed against the PineScript reference implementation.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript Reference** | Validated | Matches `decycler.pine` within floating-point tolerance |

## Common Pitfalls

1. **Confusing Decycler with Decycler Oscillator (DECO)**: The Decycler is a lowpass trend overlay ($x - \text{HP}$). DECO is a bandpass oscillator ($\text{HP}_{long} - \text{HP}_{short}$). They share the same HP filter core but answer different questions. Using Decycler where DECO is needed produces a trend line instead of a zero-crossing oscillator.

2. **Period Too Short for Timeframe**: A period of 10 on daily bars means cycles shorter than 10 days are removed. That passes nearly everything, making the Decycler almost identical to price. The filter is only useful when the cutoff period exceeds the dominant cycle length in your data. For daily bars, periods below 20 rarely provide meaningful separation.

3. **Expecting Moving Average Behavior**: The Decycler is not a moving average. It does not compute a weighted sum of past prices. It subtracts a filtered signal. During strong trends, the Decycler tracks price with less lag than an EMA of comparable smoothness. During range-bound markets, it can exhibit small oscillations that a moving average would smooth away.

4. **Ignoring the First Two Bars**: The HP filter requires $x_{t-1}$ and $x_{t-2}$. For the first two bars, HP output is zero and the Decycler returns the raw source value. Trading signals generated from these initial bars are meaningless. The `WarmupPeriod` property reflects this constraint.

5. **Floating-Point Drift in Long Series**: The IIR feedback terms ($b_1 \cdot \text{HP}_{t-1} + c_1 \cdot \text{HP}_{t-2}$) accumulate floating-point error over thousands of bars. For series exceeding ~10,000 bars, consider periodic resynchronization by replaying the last $N$ bars from scratch. In practice, drift for typical trading horizons (< 5,000 bars) stays below $10^{-10}$.

6. **Using `isNew=false` Incorrectly**: When correcting the current bar (same-timestamp update), pass `isNew: false` to restore the previous state before recomputing. Forgetting this corrupts the HP state history and produces discontinuities in the output.

7. **Large Period with Small Dataset**: A period of 200 on a 100-bar dataset means the HP filter cutoff frequency is below the Nyquist limit of the data. The filter will remove almost nothing, and the Decycler output will nearly equal the input. Ensure your dataset has at least $2 \times \text{period}$ bars for meaningful trend extraction.

## C# Usage

```csharp
// Streaming
var dec = new Decycler(period: 60);
var result = dec.Update(new TValue(DateTime.UtcNow, price));

// Batch (TSeries)
var results = Decycler.Batch(series, period: 60);

// Batch (Span)
Decycler.Batch(sourceSpan, outputSpan, period: 60);

// Chaining via publisher
var dec = new Decycler(source, period: 60);

// Static with indicator return
var (results, indicator) = Decycler.Calculate(series, period: 60);
```

## C# Implementation Considerations

### State Record Struct with Auto Layout

All IIR filter state is packed into a `record struct` with `LayoutKind.Auto` for compiler-optimized field ordering:

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State
{
    public double Hp;
    public double Hp1;
    public double Src1;
    public double Src2;
    public bool IsInitialized;
}
```

Five fields, ~40 bytes. Minimal footprint per instance.

### FusedMultiplyAdd for IIR Recurrence

The HP recurrence uses nested `Math.FusedMultiplyAdd` calls to minimize rounding error and exploit hardware FMA instructions:

```csharp
double hp = Math.FusedMultiplyAdd(_a1, src - 2.0 * _state.Src1 + _state.Src2,
             Math.FusedMultiplyAdd(_b1, _state.Hp, _c1 * _state.Hp1));
```

### Precomputed Coefficients

The trigonometric operations ($\cos$, $\sin$) execute once in the constructor. The hot path uses only the three precomputed coefficients `_a1`, `_b1`, `_c1`.

### Bar Correction via State Snapshots

The `_state` / `_p_state` pattern enables bar correction:

```csharp
if (isNew) { _p_state = _state; }
else       { _state = _p_state; }
```

### Memory Layout

- **State struct**: ~40 bytes (4 doubles + 1 bool + padding)
- **Precomputed coefficients**: 24 bytes (3 doubles)
- **Total per instance**: ~120 bytes including base class overhead

## References

- Ehlers, J. F. (2015). "Decyclers." *Technical Analysis of Stocks & Commodities*, September 2015.
- Ehlers, J. F. (2013). *Cycle Analytics for Traders*. Wiley. Chapter 4.
- Ehlers, J. F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley.
