# PTA: Ehlers Precision Trend Analysis

| Property       | Value                                            |
| :------------- | :----------------------------------------------- |
| **Category**   | Dynamics                                         |
| **Author**     | John F. Ehlers                                   |
| **Source**      | TASC, September 2024                             |
| **Parameters** | longPeriod (default 250), shortPeriod (default 40)|
| **Output**     | Zero-centered trend indicator                    |
| **Range**      | Unbounded (zero-centered)                        |
| **Warmup**     | longPeriod bars                                  |

## Historical Context

Traditional trend-following indicators like moving averages are lowpass filters with unavoidable lag. Ehlers' insight is to use highpass filters instead — they have nearly zero lag. By applying two highpass filters with different cutoff periods and subtracting, PTA creates a bandpass that preserves cyclic components between the short and long periods while eliminating noise and very long-term drift.

## Architecture & Physics

### Stage 1: Dual 2-Pole Butterworth Highpass Filters

Both filters use the standard Ehlers 2-pole Butterworth HP formulation:

$$a_1 = e^{-\sqrt{2} \cdot \pi / P}$$
$$b_1 = 2 \cdot a_1 \cdot \cos(\sqrt{2} \cdot \pi / P)$$
$$c_2 = b_1, \quad c_3 = -a_1^2, \quad c_1 = \frac{1 + c_2 - c_3}{4}$$
$$HP = c_1 \cdot (src - 2 \cdot src_1 + src_2) + c_2 \cdot HP_1 + c_3 \cdot HP_2$$

HP1 uses `longPeriod` (default 250), HP2 uses `shortPeriod` (default 40).

### Stage 2: Bandpass via Subtraction

$$\text{Trend} = HP_1 - HP_2$$

HP1 passes frequencies above 1/longPeriod. HP2 passes frequencies above 1/shortPeriod. The difference preserves only the band between shortPeriod and longPeriod — the trend-relevant frequencies.

### Key Properties

- **Near-zero lag**: Highpass filters inherently have minimal lag, unlike lowpass (MA-based) trend indicators.
- **Positive = Uptrend**: When PTA > 0, price trend is up.
- **Negative = Downtrend**: When PTA < 0, price trend is down.
- **Zero crossings**: Signal trend reversals.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation          | Count |
| :----------------- | :---- |
| Subtractions       | 3     |
| Multiplications    | 4     |
| FMA                | 4     |
| IIR state updates  | 6     |
| **Total**          | **17 FLOPs** |

### Batch Mode (SIMD Analysis)

No SIMD vectorization possible — serial IIR dependency chain on HP state. The batch path uses scalar FMA loop, O(1) per bar, zero allocation.

### Quality Metrics

| Metric            | Value                |
| :---------------- | :------------------- |
| Lag               | Near zero            |
| Smoothness        | High (IIR filtering) |
| Frequency range   | shortPeriod–longPeriod |
| Allocations       | 0 (hot path)         |

## Validation

### Behavioral Test Summary

| Test                      | Description                                                  |
| :------------------------ | :----------------------------------------------------------- |
| ConstantInput → Zero      | Constant price has zero 2nd-order difference → PTA = 0       |
| Uptrend → Positive        | Steadily rising prices produce positive PTA                   |
| Downtrend → Negative      | Steadily falling prices produce negative PTA                  |
| LongPeriod > ShortPeriod  | Constructor enforces ordering constraint                     |
| Symmetry                  | Mirrored price produces mirrored PTA (negated)               |

## Common Pitfalls

1. **longPeriod must exceed shortPeriod** — otherwise the bandpass is inverted. Constructor throws.
2. **IIR Bootstrap** — First 2 bars output 0.0 while source history fills. Full convergence at ~longPeriod bars.
3. **Default 250 bars** — Requires substantial history before the long HP stabilizes. Reduce for shorter timeframes.
4. **Not a price overlay** — Output is zero-centered, plotted in separate window.

## References

- Ehlers, J. F. "Precision Trend Analysis." *Technical Analysis of Stocks & Commodities*, September 2024.
- [TradingView Implementation](https://www.tradingview.com/script/XxSVTg0v-TASC-2024-09-Precision-Trend-Analysis/)
- [Financial Hacker Analysis](https://financial-hacker.com/ehlers-precision-trend-analysis/)
