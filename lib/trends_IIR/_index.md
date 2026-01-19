# Trends (IIR)

> "Recursion trades memory for computation. Single coefficient replaces entire window. But feedback loop carries risk: instability lurks in coefficient choices that FIR designers never face."

Trend indicators based on Infinite Impulse Response (IIR) filters. Recursive architecture uses previous outputs to compute current values, enabling lower lag with fewer coefficients than equivalent FIR filters.

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [DEMA](dema/Dema.md) | Double Exponential MA |  | Reduces lag by applying double exponential smoothing, enhancing responsiveness while maintaining signal quality. |
| [DSMA](dsma/Dsma.md) | Deviation-Scaled MA |  | Adaptive IIR filter that adjusts smoothing factor based on market volatility, increasing responsiveness during high-deviation periods. |
| [EMA](ema/Ema.md) | Exponential MA |  | Applies exponentially decreasing weights to price data, balancing responsiveness and stability. |
| [FRAMA](frama/Frama.md) | Fractal Adaptive MA |  | Adapts smoothing based on fractal dimension analysis, minimizing lag in trends and maximizing smoothing in consolidation. |
| [HEMA](hema/Hema.md) | Hull Exponential MA |  | EMA-domain Hull analog using half-life timing and de-lagged EMA cascade. |
| [HTIT](htit/Htit.md) | Hilbert Transform Instantaneous Trend |  | Utilizes Hilbert Transform to isolate instantaneous trend component, providing zero-lag trendline with hybrid FIR-in-IIR design. |
| [JMA](jma/Jma.md) | Jurik MA |  | Adaptive filter achieving high noise reduction and low phase delay through multi-stage volatility normalization and dynamic parameter optimization. |
| [KAMA](kama/Kama.md) | Kaufman Adaptive MA |  | Automatically adjusts sensitivity based on market volatility using Efficiency Ratio, balancing responsiveness and stability. |
| [MAMA](mama/Mama.md) | MESA Adaptive MA |  | Applies Hilbert Transform for phase-based adaptation, using dual-line system (MAMA/FAMA) for cycle-sensitive smoothing. |
| [MGDI](mgdi/Mgdi.md) | McGinley Dynamic Indicator |  | Adjusts speed based on market volatility using dynamic factor, aiming to hug prices closely. |
| [MMA](mma/Mma.md) | Modified MA |  | Combines simple and weighted components, emphasizing central values for balanced smoothing. |
| [QEMA](qema/Qema.md) | Quad Exponential MA |  | Zero-lag filter with four cascaded EMAs using geometrically ramped alphas and minimum-energy weights for DC lag elimination. |
| [REMA](rema/Rema.md) | Regularized Exponential MA |  | Applies regularization to EMA using lambda parameter, balancing smoothing and momentum-based prediction. |
| [RGMA](rgma/Rgma.md) | Recursive Gaussian MA |  | Approximates Gaussian smoothing by recursively applying EMA filters multiple times (passes), controlled by adjusted period. |
| [RMA](rma/Rma.md) | wildeR MA (SMMA, MMA) |  | Wilder's smoothing average using specific alpha (1/period), designed for indicators like RSI and ATR. |
| [T3](t3/T3.md) | Tillson T3 MA |  | Six-stage EMA cascade with optimized coefficients based on volume factor for reduced lag and superior noise reduction. |
| [TEMA](tema/Tema.md) | Triple Exponential MA |  | Triple-cascade EMA architecture with optimized coefficients (3, -3, 1) for further lag reduction compared to DEMA. |
| [VAMA](vama/Vama.md) | Volatility Adjusted MA |  | Dynamically adjusts moving average length based on ATR volatility ratio, shortening during high volatility and lengthening during low volatility. |
| [VIDYA](vidya/Vidya.md) | Variable Index Dynamic Average |  | Adjusts smoothing factor based on market volatility using Volatility Index (ratio of short-term to long-term standard deviation). |
| [YZVAMA](yzvama/Yzvama.md) | Yang-Zhang Volatility Adjusted MA |  | Adjusts MA length based on percentile rank of short-term YZV, providing context-aware volatility adaptation for gap-prone markets. |
| [ZLEMA](zlema/Zlema.md) | Zero-Lag Exponential MA |  | Reduces lag by estimating future price based on current momentum, using dynamically calculated lag period. |

## Selection Guide

**For trend-following systems:** EMA provides baseline stability. DEMA/TEMA reduce lag at cost of increased overshoot. T3 offers best lag-to-smoothness ratio for most applications.

**For adaptive response:** KAMA adjusts to efficiency ratio (trend vs noise). VIDYA responds to volatility changes. FRAMA uses fractal dimension for market state detection. JMA combines all adaptive mechanisms into unified filter.

**For zero-lag requirements:** ZLEMA applies momentum-based lag compensation. HTIT uses Hilbert Transform for instantaneous trend. QEMA achieves DC lag elimination through cascaded architecture.

**For Wilder-family indicators:** RMA (SMMA) provides standard smoothing for RSI, ATR, ADX calculations.

## IIR Characteristics Comparison

| Filter | Lag (bars) | Smoothness | Overshoot | Adaptivity | Complexity |
| :--- | :---: | :---: | :---: | :---: | :---: |
| EMA | Period/2 | Medium | Low | None | O(1) |
| DEMA | Period/3 | Medium | Medium | None | O(1) |
| TEMA | Period/4 | Low | High | None | O(1) |
| T3 | Period/5 | High | Low | None | O(1) |
| ZLEMA | ~0 | Low | High | None | O(1) |
| KAMA | Variable | Variable | Low | Efficiency | O(n) |
| VIDYA | Variable | Variable | Low | Volatility | O(n) |
| FRAMA | Variable | Variable | Medium | Fractal | O(n) |
| JMA | ~1-2 | High | Very Low | Multi-factor | O(1) |

## Adaptive Filter Categories

| Category | Filters | Adaptation Mechanism | Best Application |
| :--- | :--- | :--- | :--- |
| **Fixed Alpha** | EMA, RMA, MMA | Constant smoothing factor | Stable trending markets |
| **Cascade** | DEMA, TEMA, T3, QEMA | Multiple EMA stages | Lag reduction priority |
| **Efficiency-Based** | KAMA | Direction vs noise ratio | Choppy/trending detection |
| **Volatility-Based** | VIDYA, VAMA, DSMA, YZVAMA | Standard deviation or ATR | Regime-change adaptation |
| **Fractal-Based** | FRAMA | Hurst exponent proxy | Range/trend detection |
| **Phase-Based** | MAMA, HTIT | Hilbert Transform | Cycle-sensitive smoothing |
| **Multi-Stage Adaptive** | JMA, MGDI | Combined mechanisms | Universal application |

## IIR vs FIR Design Principles

| Aspect | IIR Filters | FIR Filters |
| :--- | :--- | :--- |
| **Memory** | O(1) state | O(period) buffer |
| **Computation** | 2-4 multiplications | period multiplications |
| **Stability** | Requires careful design | Always stable |
| **Phase Response** | Non-linear phase | Can be linear phase |
| **Lag Achievable** | Lower lag possible | Minimum lag = (period-1)/2 |
| **Adaptivity** | Natural (modify alpha) | Requires coefficient recalc |
| **SIMD Potential** | Limited (recursive) | High (parallel windows) |

## Alpha-Period Relationship

IIR filters use smoothing factor ± instead of explicit period. Conversion formulas:

| Formula | Expression | Use Case |
| :--- | :--- | :--- |
| Standard EMA | ± = 2/(period+1) | General purpose |
| Wilder (RMA) | ± = 1/period | RSI, ATR, ADX |
| Percentage | ± = percentage/100 | Direct control |

Effective period approximation: `period H 2/± - 1` for standard EMA weighting.