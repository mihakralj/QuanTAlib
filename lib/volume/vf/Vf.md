# VF: Volume Force

> *Price without volume is like a punch without body weight behind it—VF measures the momentum of conviction.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Vf)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period` bars                          |
| **PineScript**   | [vf.pine](vf.pine)                       |

- Volume Force (VF) quantifies the strength of volume behind price movements by multiplying price change by volume and applying EMA smoothing with wa...
- **Similar:** [MFI](../mfi/Mfi.md), [CMF](../cmf/Cmf.md) | **Complementary:** RSI | **Trading note:** Volume Force; measures directional volume pressure. Positive = buyers dominant.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Volume Force (VF) quantifies the strength of volume behind price movements by multiplying price change by volume and applying EMA smoothing with warmup compensation. The result is a momentum-style oscillator that distinguishes between genuine volume-backed moves and hollow price action.

Unlike simple volume indicators that ignore direction, VF combines directional price change with volume intensity. Large volumes during significant price moves produce high VF readings; large volumes during flat price action contribute nothing. This selectivity makes VF particularly effective at filtering noise from signal.

## Historical Context

Volume Force derives from the concept of "Force Index" popularized by Alexander Elder in his 1993 book "Trading for a Living." Elder's original Force Index multiplied price change by volume without smoothing:

$$
Force_t = (Close_t - Close_{t-1}) \times Volume_t
$$

VF enhances this concept with EMA smoothing and warmup compensation, addressing two limitations of the raw Force Index:

1. **Noise sensitivity**: Raw Force Index is extremely volatile
2. **Initial bias**: Standard EMA starts with zero, creating warmup distortion

The warmup compensation technique ensures that early VF values aren't biased toward zero, providing accurate readings from the second bar onward. This makes VF suitable for both long-term trending analysis and short-term momentum assessment.

## Architecture & Physics

VF combines three components: price change calculation, volume weighting, and EMA smoothing with compensation.

### Component Breakdown

1. **Price Change**: Difference between current and previous close
2. **Raw VF**: Price change multiplied by volume (Force Index)
3. **EMA Smoothing**: Exponential moving average of raw VF
4. **Warmup Compensation**: Bias correction during initial period

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| EmaValue | double | Smoothed VF value |
| E | double | Warmup decay factor (starts at 1) |
| PrevClose | double | Previous bar's close price |
| LastValidClose | double | Fallback for NaN handling |
| LastValidVolume | double | Fallback for NaN handling |
| Warmup | bool | Whether compensation is active |
| Index | int | Bar counter for IsHot |

### Warmup Compensation Mechanism

Standard EMA initialization biases early values toward zero:

$$
EMA_1 = \alpha \times Value_1 + (1 - \alpha) \times 0 = \alpha \times Value_1
$$

This underestimates the true average. VF compensates by tracking the decay factor:

$$
e_t = e_{t-1} \times (1 - \alpha)
$$

$$
VF_t = \frac{EMA_t}{1 - e_t}
$$

As $e \rightarrow 0$, the compensator $\frac{1}{1 - e} \rightarrow 1$, and VF converges to the raw EMA.

## Mathematical Foundation

### Core Formula

$$
PriceChange_t = Close_t - Close_{t-1}
$$

$$
RawVF_t = PriceChange_t \times Volume_t
$$

$$
EMA_t = \alpha \times RawVF_t + (1 - \alpha) \times EMA_{t-1}
$$

where $\alpha = \frac{2}{period + 1}$

### With Warmup Compensation

$$
e_t = e_{t-1} \times (1 - \alpha), \quad e_0 = 1
$$

$$
VF_t = \begin{cases}
\frac{EMA_t}{1 - e_t} & \text{if } e_t > 10^{-10} \\
EMA_t & \text{otherwise}
\end{cases}
$$

### First Bar Handling

The first bar has no previous close, so:

$$
VF_0 = 0
$$

This is mathematically correct—there's no price change to measure.

### FMA Optimization

The EMA update uses fused multiply-add for numerical precision:

```csharp
emaValue = Math.FusedMultiplyAdd(alpha, rawVf - emaValue, emaValue);
// Equivalent to: emaValue = alpha * (rawVf - emaValue) + emaValue
// Which equals: emaValue = alpha * rawVf + (1 - alpha) * emaValue
```

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 2 | Price change, EMA diff |
| MUL | 3 | Raw VF, EMA decay, compensation |
| ADD | 1 | FMA operation |
| DIV | 1 | Compensation factor |
| CMP | 1 | Warmup check |
| **Total** | 8 | Per bar, O(1) |

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| Price differences | ✅ | Parallel subtraction |
| Volume multiplication | ✅ | Parallel multiply |
| EMA recursion | ❌ | Sequential dependency |
| Compensation | ❌ | Depends on EMA state |

The EMA recursion prevents full SIMD optimization. However, the price × volume multiplication can be vectorized before the sequential EMA pass.

### Memory Footprint

| Scope | Size |
| :--- | :--- |
| Per instance | ~112 bytes (State record struct × 2) |
| Buffer requirements | None (O(1) state) |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | FMA-precise computation |
| **Timeliness** | 9/10 | Second bar valid; warmup compensated |
| **Smoothness** | 8/10 | EMA provides controlled smoothing |
| **Noise Filtering** | 7/10 | Period-dependent noise reduction |
| **Memory** | 10/10 | O(1) constant |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Has Force Index but no VF variant |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation (vf.pine) |

VF validation focuses on internal consistency between streaming, batch, and span modes (verified with 1e-10 tolerance) and formula correctness against manual calculations.

## Common Pitfalls

1. **First Bar Is Always Zero**: VF requires a previous close to compute price change. The first bar returns 0 regardless of volume. This is correct behavior, not a bug.

2. **Period Selection**: Shorter periods (5-10) respond quickly but are noisy. Longer periods (20-50) smooth heavily but lag. Default of 14 balances responsiveness and smoothness.

3. **Scale Interpretation**: VF values are in "volume × price" units. A VF of 100,000 means different things for different instruments. Focus on direction and relative magnitude rather than absolute values.

4. **Zero Crossings**: VF oscillates around zero. Positive values indicate net buying pressure; negative indicates selling. Zero crossings can signal momentum shifts but generate noise in ranging markets.

5. **Volume Spikes**: Extreme volume events (earnings, news) can create VF spikes that distort the EMA. Consider whether such events should inform your analysis or be filtered.

6. **Warmup Period**: While warmup compensation provides accurate early values, IsHot only becomes true after `period` bars. This matches EMA convention for statistical significance.

7. **NaN Handling**: VF substitutes last valid values for NaN/Infinity inputs. This maintains continuity but can mask data quality issues. Monitor your data feed.

8. **isNew Parameter**: Bar correction (isNew = false) properly restores EMA state including the warmup decay factor. Incorrect usage corrupts the smoothing calculation.

## Interpretation Guide

### Momentum Analysis

| VF Value | Volume | Price Move | Interpretation |
| :--- | :--- | :--- | :--- |
| Large positive | High | Up | Strong buying pressure |
| Small positive | Low | Up | Weak buying pressure |
| Large negative | High | Down | Strong selling pressure |
| Small negative | Low | Down | Weak selling pressure |
| Near zero | Any | Flat | No directional conviction |

### Divergence Signals

VF divergences often precede price reversals:

1. **Bullish divergence**: Price makes lower low, VF makes higher low
   - Selling pressure is weakening despite lower prices
   - Potential reversal to upside

2. **Bearish divergence**: Price makes higher high, VF makes lower high
   - Buying pressure is weakening despite higher prices
   - Potential reversal to downside

### Zero Line Crossings

| Crossing | Direction | Signal |
| :--- | :--- | :--- |
| Below → Above | Bullish | Net buying pressure emerges |
| Above → Below | Bearish | Net selling pressure emerges |

Filter zero crossings in ranging markets—they generate excessive signals without follow-through.

### Trend Confirmation

Use VF to confirm price trends:

- **Uptrend**: VF should stay predominantly positive
- **Downtrend**: VF should stay predominantly negative
- **Healthy trend**: VF pullbacks don't cross zero deeply

### Volume-Weighted Momentum

Compare VF to simple price momentum:

| VF vs Price Momentum | Interpretation |
| :--- | :--- |
| VF confirms | Volume supports the move |
| VF diverges | Volume doesn't support—potential reversal |
| VF leads | Volume commitment precedes price |
| VF lags | Volume follows price—chasing behavior |

## Parameter Selection Guide

| Period | Character | Use Case |
| :--- | :--- | :--- |
| 5-7 | Very responsive | Scalping, intraday momentum |
| 10-14 | Balanced | Swing trading (default: 14) |
| 20-30 | Smooth | Position trading |
| 50+ | Very smooth | Trend identification |

### Period vs Responsiveness Trade-off

$$
\alpha = \frac{2}{period + 1}
$$

| Period | α | Half-life (bars) |
| :--- | :--- | :--- |
| 5 | 0.333 | ~2.4 |
| 10 | 0.182 | ~5.5 |
| 14 | 0.133 | ~8.0 |
| 20 | 0.095 | ~12.0 |
| 50 | 0.039 | ~31.0 |

Half-life indicates how many bars until a spike decays to half its initial impact.

## Comparison with Related Indicators

| Indicator | Formula | Smoothing | Normalization |
| :--- | :--- | :--- | :--- |
| **VF** | ΔP × V, EMA smoothed | Yes (period) | None |
| **Force Index** | ΔP × V | None (raw) | None |
| **OBV** | Cumulative ±V | None | None |
| **MFI** | Money Flow Ratio | Period lookback | 0-100 |
| **CMF** | AD / Volume | Period average | -1 to +1 |

VF occupies a middle ground: more responsive than OBV/CMF (not cumulative), smoother than raw Force Index, unbounded unlike MFI.

## References

- Elder, A. (1993). "Trading for a Living." John Wiley & Sons.
- Ehlers, J. (2001). "Rocket Science for Traders." John Wiley & Sons.
- Murphy, J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- TradingView. "PineScript Volume Force." Community Reference.