# AMFM: Ehlers AM Detector / FM Demodulator

> *Treat price like a radio wave — demodulate amplitude for volatility, demodulate frequency for timing.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                            |
| **Inputs**       | TBar (Open, Close)               |
| **Parameters**   | `period` (default 30)            |
| **Outputs**      | Dual: AM (≥ 0) + FM (≈ [-1,+1]) |
| **Output range** | AM: ≥ 0; FM: bounded ≈ [-1,+1]  |
| **Warmup**       | `max(12, period)` bars           |
| **PineScript**   | [amfm.pine](amfm.pine)          |

- AMFM decomposes price movement into amplitude (AM) and frequency (FM) components using DSP techniques from radio engineering — AM measures volatility, FM tracks timing of price variations.
- **Similar:** [EEO](../../oscillators/eeo/Eeo.md), [DSO](../../oscillators/dso/Dso.md) | **Complementary:** Moving averages for trend confirmation | **Trading note:** AM provides volatility context; FM zero crossings signal direction changes. FM is more robust for strategy optimization (smoother parameter surface).
- No external validation libraries implement AMFM. Validated through self-consistency and behavioral testing.

Ehlers applies radio engineering signal processing to financial data, treating the whitened price derivative (Close − Open) as a modulated carrier. The AM detector extracts the amplitude envelope (volatility) using peak detection and smoothing. The FM demodulator strips amplitude information via a hard limiter (10× gain clamped to ±1), then integrates the result through a Super Smoother filter to recover the frequency/timing component. The FM demodulator produces more robust trading strategies because removing amplitude variation creates a smoother optimization parameter surface.

## Historical Context

John F. Ehlers published "A Technical Description of Market Data for Traders" in the May 2021 issue of *Technical Analysis of Stocks & Commodities*. The article applies classical radio engineering concepts — amplitude modulation (AM) and frequency modulation (FM) — to financial time series analysis. In the June 2021 follow-up, "Creating More Robust Trading Strategies With The FM Demodulator," Ehlers demonstrated that incorporating the FM demodulator into a simple momentum strategy produced significantly smoother parameter optimization surfaces, leading to more robust strategy configurations.

## Architecture & Physics

### Stage 1: Whitening (Common to Both)

$$\text{Deriv} = \text{Close} - \text{Open}$$

Using Close − Open instead of Close − Close[1] removes intraday gap effects, producing a zero-mean whitened derivative.

### Stage 2a: AM Detector (Amplitude Envelope)

$$\text{Envel} = \max(|\text{Deriv}|, 4\text{ bars})$$

$$\text{AM} = \text{SMA}(\text{Envel}, 8)$$

The 4-bar rolling maximum captures the amplitude envelope, and the 8-bar SMA smooths it into a volatility measure.

### Stage 2b: FM Demodulator (Frequency/Timing)

$$\text{HL} = \text{clamp}(10 \cdot \text{Deriv}, -1, +1)$$

The hard limiter applies 10× gain then clips to ±1, stripping all amplitude information and preserving only the sign/timing.

$$a_1 = e^{-1.414\pi / \text{Period}}, \quad b_1 = 2a_1\cos(1.414\pi / \text{Period})$$

$$c_2 = b_1, \quad c_3 = -a_1^2, \quad c_1 = 1 - c_2 - c_3$$

$$\text{FM} = \frac{c_1}{2}(\text{HL} + \text{HL}[1]) + c_2 \cdot \text{FM}[1] + c_3 \cdot \text{FM}[2]$$

The Super Smoother integrates the hard-limited signal, recovering the frequency modulation component.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation               | Count | Notes                          |
|:----------------------- |:----- |:------------------------------ |
| Subtraction (Deriv)     | 1     | Close − Open                   |
| Abs + compare (envelope)| 5     | |Deriv| + max of 4 elements     |
| SMA update              | 2     | Running sum add/remove         |
| Division (SMA)          | 1     | sum / 8                        |
| Multiply + clamp (HL)   | 3     | 10×Deriv + 2 comparisons       |
| FMA × 2 (SSF)           | 2     | 2-pole recursive filter        |
| **Total per bar**       | **~14** | Constant O(1)                |

### Batch Mode (SIMD Analysis)

The IIR Super Smoother stage prevents full vectorization. Batch mode uses `stackalloc` circular buffers to avoid heap allocation.

## Validation

Validated through self-consistency tests (streaming ≡ batch) and behavioral tests.

### Behavioral Test Summary

| Test                    | Expected Result           |
|:----------------------- |:------------------------- |
| Constant OHLC           | AM → 0, FM → 0           |
| Strong uptrend (C > O)  | AM > 0, FM > 0           |
| Strong downtrend (C < O)| AM > 0, FM < 0           |
| NaN/Inf input           | Finite output (fallback)  |
| Bar correction (isNew)  | State restored correctly  |

## Common Pitfalls

1. **AM vs FM semantics**: AM measures *how much* (volatility), FM measures *when* (timing). They are complementary, not redundant.

2. **Hard limiter gain**: The 10× multiplier before clamping is hardcoded per Ehlers. Most price derivatives are small enough that 10× pushes them to the ±1 rails, effectively creating a sign function. Do not tune this.

3. **FM period**: The `period` parameter only affects the FM Super Smoother cutoff. The AM detector uses fixed 4-bar envelope + 8-bar SMA (per Ehlers' specification).

4. **Input requirement**: Needs Open and Close prices (TBar input). Close-only data will produce Deriv = 0 if Open defaults to Close.

## References

- Ehlers, J. F. (2021). "A Technical Description of Market Data for Traders." *TASC*, May 2021.
- Ehlers, J. F. (2021). "Creating More Robust Trading Strategies With The FM Demodulator." *TASC*, June 2021.
- Ehlers, J. F. (2013). *Cycle Analytics for Traders*. John Wiley & Sons. (Super Smoother definition)
- [MESA Software Paper](https://www.mesasoftware.com/papers/AMFM.pdf)
