# EBSW: Ehlers Even Better Sinewave

> "When you combine a high-pass filter with a super-smoother, you get cleaner cycles with automatic gain control."

The Even Better Sinewave (EBSW) indicator, developed by John Ehlers, is a normalized cycle oscillator that extracts the dominant cycle from price data using a cascade of high-pass and super-smoother filters with automatic gain control (AGC). The output oscillates between -1 and +1, with zero crossings indicating potential turning points.

## Historical Context

John Ehlers introduced the Even Better Sinewave as an improvement over earlier sinewave indicators. The original sinewave indicator suffered from trend contamination and noise sensitivity. EBSW addresses these issues through a multi-stage filtering approach:

1. **High-pass filter** removes the DC (trend) component
2. **Super-smoother filter** eliminates high-frequency noise
3. **Automatic gain control** normalizes the output regardless of volatility

The "Even Better" in the name reflects Ehlers' iterative refinement process—each successive sinewave indicator addressed limitations of its predecessors. EBSW represents the culmination of this evolution, providing a robust cycle indicator suitable for both trending and ranging markets.

Unlike traditional oscillators that use arbitrary overbought/oversold levels, EBSW's AGC ensures the output always spans the full [-1, +1] range, making interpretation consistent across different instruments and timeframes.

## Architecture & Physics

EBSW uses a two-stage IIR filter cascade followed by wave extraction and normalization.

### Core Components

1. **High-Pass Filter**: Single-pole IIR filter that removes trend/DC component
2. **Super-Smoother Filter**: Two-pole IIR filter (Butterworth-style) for noise reduction
3. **Wave Calculator**: Three-bar average of filtered values
4. **Power Calculator**: Three-bar RMS (root mean square) for normalization
5. **AGC Normalizer**: Divides wave by RMS, clamps to [-1, +1]

### Filter Cascade

```
Price → High-Pass → Super-Smoother → Wave/Power → AGC → Sinewave
       (detrend)    (smooth)        (3-bar avg)  (normalize)
```

### State Management

The indicator maintains:
- Two source values (current and previous)
- Two high-pass values (current and previous)
- Three filter values (current, previous, two-back)
- Last valid value for NaN handling

## Mathematical Foundation

### High-Pass Filter Coefficient

The high-pass filter uses an angular frequency based on the period:

$$
\theta_{hp} = \frac{2\pi}{HP_{length}}
$$

$$
\alpha_1 = \frac{1 - \sin(\theta_{hp})}{\cos(\theta_{hp})}
$$

This coefficient determines how much of the previous high-pass output carries forward. Larger HP length → larger $\alpha_1$ → more low-frequency rejection.

### High-Pass Filter Equation

$$
HP_t = 0.5 \cdot (1 + \alpha_1) \cdot (P_t - P_{t-1}) + \alpha_1 \cdot HP_{t-1}
$$

The first term applies a differencing operation (removes DC) weighted by $(1 + \alpha_1)/2$. The second term provides recursive smoothing.

### Super-Smoother Filter Coefficients

The super-smoother uses a critically damped two-pole design:

$$
\theta_{ssf} = \frac{\sqrt{2} \cdot \pi}{SSF_{length}}
$$

$$
\alpha_2 = e^{-\theta_{ssf}}
$$

$$
\beta = 2 \cdot \alpha_2 \cdot \cos(\theta_{ssf})
$$

$$
c_2 = \beta, \quad c_3 = -\alpha_2^2, \quad c_1 = 1 - c_2 - c_3
$$

Note: The coefficients sum to 1, ensuring DC gain of 1 for non-zero-mean signals (though the high-pass removes DC anyway).

### Super-Smoother Filter Equation

$$
Filt_t = \frac{c_1}{2} \cdot (HP_t + HP_{t-1}) + c_2 \cdot Filt_{t-1} + c_3 \cdot Filt_{t-2}
$$

The input is averaged to reduce aliasing artifacts. The two feedback terms create the smooth response.

### Wave Component (3-Bar Average)

$$
Wave_t = \frac{Filt_t + Filt_{t-1} + Filt_{t-2}}{3}
$$

### Power Component (3-Bar RMS)

$$
Pwr_t = \frac{Filt_t^2 + Filt_{t-1}^2 + Filt_{t-2}^2}{3}
$$

### AGC Normalization

$$
Sinewave_t = \text{clamp}\left(\frac{Wave_t}{\sqrt{Pwr_t}}, -1, +1\right)
$$

When $Pwr_t = 0$ (constant input), the division returns 0.

### Example Calculation

For default parameters (HP length=40, SSF length=10):

$$
\theta_{hp} = \frac{2\pi}{40} \approx 0.157
$$

$$
\alpha_1 = \frac{1 - \sin(0.157)}{\cos(0.157)} \approx 0.843
$$

$$
\theta_{ssf} = \frac{\sqrt{2} \cdot \pi}{10} \approx 0.444
$$

$$
\alpha_2 = e^{-0.444} \approx 0.641
$$

$$
c_1 \approx 0.213, \quad c_2 \approx 1.198, \quad c_3 \approx -0.411
$$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15 ns/bar | O(1) constant time |
| **Allocations** | 0 | Zero-allocation in hot path |
| **Complexity** | O(1) | Fixed operations per update |
| **Accuracy** | 10 | Matches PineScript reference |

### Operation Count (per update)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD/SUB | ~12 | Filter calculations, averaging |
| MUL | ~10 | Coefficient multiplications |
| DIV | 3 | Averaging and normalization |
| SQRT | 1 | RMS calculation |
| FMA | 2 | High-pass and smoother updates |
| CLAMP | 1 | Output bounding |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact match to reference |
| **Timeliness** | 8/10 | Some lag from smoothing |
| **Overshoot** | 9/10 | AGC prevents overshoot |
| **Smoothness** | 9/10 | Dual filtering excellent |
| **Normalization** | 10/10 | Always in [-1, +1] |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not available in TA-Lib |
| **Skender** | N/A | Not available in Skender |
| **Tulip** | N/A | Not available in Tulip |
| **PineScript** | ✅ | Validated against original EBSW implementation |

EBSW is validated through mathematical properties:

- Constant price produces zero output (no cycles)
- Output always bounded between -1 and +1
- Pure sine wave input produces clean oscillation near ±1
- Zero crossings align with cycle phase changes
- AGC adapts to different volatility levels

## Common Pitfalls

1. **HP Length Selection**: The high-pass length determines the longest cycle passed through. Set to approximately the dominant cycle period. Default 40 is suitable for daily data targeting ~8-week cycles.

2. **SSF Length Selection**: The super-smoother length controls noise filtering. Too short leaves noise; too long delays response. Typical ratio: SSF length = HP length / 4.

3. **Warmup Period**: EBSW needs `max(hpLength, ssfLength) + 3` bars to stabilize due to the three-bar wave calculation. Early values may not be reliable.

4. **Zero Crossings in Trends**: During strong trends, EBSW may oscillate around a non-zero mean. Zero crossings are most meaningful in ranging markets.

5. **AGC Saturation**: When EBSW reaches ±1, the cycle may be extended (not peaked). Look for the turn from ±1 rather than just the extreme values.

6. **Chained Indicators**: EBSW output is already normalized. Applying additional smoothing may distort the [-1, +1] property.

## Usage

```csharp
using QuanTAlib;

// Create an EBSW indicator with default parameters
var ebsw = new Ebsw(hpLength: 40, ssfLength: 10);

// Update with new values
var result = ebsw.Update(new TValue(DateTime.UtcNow, 100.0));

// Access the last calculated value
Console.WriteLine($"EBSW: {ebsw.Last.Value}");  // Always in [-1, +1]

// Chained usage
var source = new TSeries();
var ebswChained = new Ebsw(source, hpLength: 40, ssfLength: 10);

// Static batch calculation
var output = Ebsw.Calculate(source, hpLength: 40, ssfLength: 10);

// Span-based calculation
Span<double> outputSpan = stackalloc double[source.Count];
Ebsw.Batch(source.Values, outputSpan, hpLength: 40, ssfLength: 10);
```

## Applications

### Cycle Turning Points

EBSW zero crossings identify cycle inflection points:

- EBSW crosses above zero: cycle trough (potential buy signal)
- EBSW crosses below zero: cycle peak (potential sell signal)

### Entry/Exit Timing

Use EBSW extremes for timing:

- EBSW near -1 and turning up: entering bullish phase
- EBSW near +1 and turning down: entering bearish phase

### Trend Filtering

Combine with trend indicators:

- In uptrend: Enter long when EBSW crosses above zero
- In downtrend: Enter short when EBSW crosses below zero

### Divergence Detection

EBSW divergences signal potential reversals:

- Price higher high, EBSW lower high: bearish divergence
- Price lower low, EBSW higher low: bullish divergence

### Multi-Timeframe Analysis

EBSW on multiple timeframes provides confluence:

- Higher timeframe: Direction bias
- Lower timeframe: Entry timing

## Comparison to Related Indicators

### EBSW vs Traditional Sinewave

| Feature | EBSW | Traditional Sinewave |
| :--- | :--- | :--- |
| Trend removal | High-pass filter | None or basic |
| Noise handling | Super-smoother | Single EMA |
| Normalization | AGC | Fixed or none |
| Output range | Always [-1, +1] | Variable |

### EBSW vs RSI

| Feature | EBSW | RSI |
| :--- | :--- | :--- |
| Output range | [-1, +1] | [0, 100] |
| Zero line | 0 (midpoint) | 50 |
| Calculation | IIR filters + AGC | Up/down averaging |
| Cycle focus | Yes | No |
| Trend sensitivity | Low (high-pass) | High |

### EBSW vs Stochastic

| Feature | EBSW | Stochastic |
| :--- | :--- | :--- |
| Basis | Filtered cycles | Price range position |
| Normalization | AGC (dynamic) | Fixed lookback range |
| Smoothing | Two-pole IIR | Simple moving average |
| Leading nature | Yes | Yes |

## Parameter Tuning

### For Shorter-Term Cycles (Intraday)

```csharp
var ebsw = new Ebsw(hpLength: 20, ssfLength: 5);
```

### For Medium-Term Cycles (Daily)

```csharp
var ebsw = new Ebsw(hpLength: 40, ssfLength: 10);
```

### For Longer-Term Cycles (Weekly)

```csharp
var ebsw = new Ebsw(hpLength: 80, ssfLength: 20);
```

### Adaptive Approach

Use cycle measurement (e.g., autocorrelation, Homodyne Discriminator) to dynamically adjust HP length to match the detected dominant cycle.

## References

- Ehlers, J.F. (2013). *Cycle Analytics for Traders*. Wiley.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley.
- TradingView PineScript: Even Better Sinewave indicator implementation.
- Original PineScript reference: `ebsw.pine` in QuanTAlib repository.