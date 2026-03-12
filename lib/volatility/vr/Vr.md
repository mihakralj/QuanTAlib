# VR: Volatility Ratio

> *When today's range dwarfs the average, pay attention—the market is telling you something unusual is happening.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Vr)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [vr.pine](vr.pine)                       |

- Volatility Ratio (VR) measures the current bar's True Range relative to its Average True Range (ATR), providing a normalized indicator of short-ter...
- Parameterized by `period` (default 14).
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Volatility Ratio (VR) measures the current bar's True Range relative to its Average True Range (ATR), providing a normalized indicator of short-term volatility expansion or contraction. Values above 1.0 indicate above-average volatility (potential breakouts), while values below 1.0 suggest below-average volatility (consolidation). This simple yet powerful ratio helps traders identify when markets are moving unusually, often preceding significant price moves.

## Historical Context

The Volatility Ratio emerged from the practical need to normalize volatility readings across different market conditions and timeframes. While ATR (developed by J. Welles Wilder Jr. in 1978) provides an absolute measure of volatility, traders needed a relative measure to answer: "Is today's movement unusual compared to recent history?"

The ratio concept is straightforward: divide today's True Range by the average True Range. This normalization allows:
1. Cross-market comparison (a VR of 2.0 means the same thing whether trading stocks, futures, or forex)
2. Breakout detection (VR > threshold signals unusual movement)
3. Volatility regime identification (sustained high/low VR indicates market character)

The implementation uses Wilder's RMA (also known as SMMA or modified EMA) with bias correction for accurate ATR calculation from the first bar.

## Architecture & Physics

### 1. True Range Calculation

True Range captures the full extent of price movement including gaps:

$$
TR_t = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|)
$$

where:
- $H_t$ = Current high
- $L_t$ = Current low
- $C_{t-1}$ = Previous close

For the first bar (no previous close), TR = High - Low.

### 2. Bias-Corrected ATR (RMA)

ATR uses Wilder's smoothing (RMA) with bias correction:

$$
\text{RMA}_{raw,t} = \alpha \cdot TR_t + (1 - \alpha) \cdot \text{RMA}_{raw,t-1}
$$

where $\alpha = 1/\text{period}$.

**Bias compensator:**
$$
e_t = (1 - \alpha)^t
$$

**Corrected ATR:**
$$
ATR_t = \frac{\text{RMA}_{raw,t}}{1 - e_t}
$$

This correction eliminates the startup bias that would otherwise cause ATR to be understated during the warmup period.

### 3. Volatility Ratio

$$
VR_t = \frac{TR_t}{ATR_t}
$$

When ATR is near zero, VR returns 0 to avoid division by zero.

## Mathematical Foundation

### True Range Properties

True Range has three components to handle gaps:
1. **H - L**: Intraday range (no gap)
2. **|H - PrevClose|**: Gap up scenario (high extends above previous close)
3. **|L - PrevClose|**: Gap down scenario (low extends below previous close)

The maximum of these three captures the full extent of price movement.

### RMA vs EMA

Wilder's RMA uses $\alpha = 1/n$ rather than EMA's $\alpha = 2/(n+1)$:

| Period | RMA α | EMA α | RMA Halflife | EMA Halflife |
| :---: | :---: | :---: | :---: | :---: |
| 14 | 0.0714 | 0.1333 | 9.6 bars | 4.8 bars |
| 20 | 0.0500 | 0.0952 | 13.9 bars | 6.9 bars |

RMA is slower to respond, providing a more stable reference for the ratio.

### Example Calculation

Period = 3, Bars with previous close = 100:

| Bar | H | L | C | TR | RMA_raw | e | ATR | VR |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | 102 | 98 | 101 | 4.0 | 4.0 | 0.667 | 12.0 | 0.33 |
| 2 | 106 | 100 | 105 | 6.0 | 4.67 | 0.444 | 8.40 | 0.71 |
| 3 | 108 | 103 | 106 | 5.0 | 4.78 | 0.296 | 6.79 | 0.74 |
| 4 | 115 | 104 | 112 | 9.0 | 6.19 | 0.198 | 7.72 | 1.17 |

Note: Bar 4 shows VR > 1.0, indicating above-average volatility.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB | 3 | 1 | 3 |
| ABS | 2 | 1 | 2 |
| MAX | 2 | 2 | 4 |
| MUL | 3 | 3 | 9 |
| DIV | 2 | 15 | 30 |
| FMA | 1 | 5 | 5 |
| **Total** | — | — | **~53 cycles** |

Extremely lightweight—dominated by two divisions.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| TR calculation | 3584 | 448 | 8× |
| RMA smoothing | 1536 | N/A (recursive) | 1× |
| Division | 1024 | 128 | 8× |

**Batch efficiency:**

| Mode | Cycles/bar | Total (512 bars) | Notes |
| :--- | :---: | :---: | :--- |
| Scalar streaming | ~53 | ~27k | Baseline |
| Hybrid SIMD | ~35 | ~18k | TR vectorized, RMA scalar |
| **Improvement** | **34%** | **9k saved** | Limited by RMA recursion |

### Memory Profile

- **Per instance:** ~72 bytes (state record + backup)
- **100 instances:** ~7.2 KB
- **No ring buffers**: RMA is fully recursive

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Simplicity** | 10/10 | Single ratio, intuitive interpretation |
| **Timeliness** | 10/10 | Immediate response to current bar |
| **Stability** | 8/10 | ATR smoothing provides stable denominator |
| **Signal Quality** | 8/10 | Clear breakout signals when VR > threshold |
| **Cross-Market** | 9/10 | Normalized for comparison |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches vr.pine reference |
| **Self-consistency** | ✅ | Streaming = Batch modes match |

## Common Pitfalls

1. **First bar handling**: On the first bar, there's no previous close. TR = H - L for this bar only, and ATR initialization uses bias correction to prevent understating early values.

2. **Warmup period**: VR needs approximately `Period` bars for ATR to stabilize. During warmup, bias correction helps but early readings may be less reliable. The implementation tracks warmup via `IsHot`.

3. **Threshold interpretation**: VR = 1.0 means "average" volatility. Common breakout thresholds:
   - VR > 1.5: Moderate breakout signal
   - VR > 2.0: Strong breakout signal
   - VR < 0.5: Extremely low volatility (consolidation)

4. **Denominator protection**: When ATR ≈ 0 (nearly flat market), the implementation returns 0 rather than causing division errors.

5. **Scale is relative**: VR = 2.0 always means "twice normal volatility" regardless of the underlying instrument's absolute price or typical ATR value.

6. **Period selection**: Shorter periods (7-10) make ATR more responsive, causing VR to spike less dramatically. Longer periods (20-30) create a more stable baseline, making VR spikes more pronounced.

## Trading Applications

### Breakout Detection

The primary use case—identify unusual volatility expansion:

```
VR > 2.0: Potential breakout in progress
VR > 1.5 && Volume > 2×Avg: High-conviction breakout
VR < 0.7 sustained: Building energy for eventual breakout
```

### Position Sizing

Scale position size inversely with current VR:

```
Base Position × (Target_VR / Current_VR)
Example: 1000 shares × (1.0 / 2.0) = 500 shares during high volatility
```

### Stop Loss Adjustment

Widen stops when VR is elevated:

```
Stop Distance = ATR × Multiplier × VR
Higher VR → Wider stops to avoid noise
```

### Volatility Squeeze Detection

Identify consolidation before expansion:

```
VR < 0.6 for 5+ bars → Volatility squeeze
Watch for VR breakout above 1.5 to signal expansion
```

### Regime Classification

```
VR < 0.7: Low volatility (trend following works)
VR 0.7-1.3: Normal volatility (standard strategies)
VR > 1.3: High volatility (reduce size, widen stops)
VR > 2.0: Extreme volatility (defensive positioning)
```

### Entry Timing

```
Breakout entry: Wait for VR > 1.5 to confirm move
Mean reversion: Enter when VR > 2.0 starts declining
Trend following: Best when VR 1.0-1.5 (movement with stability)
```

## Relationship to Other Indicators

| Indicator | Relationship to VR |
| :--- | :--- |
| **ATR** | VR = TR/ATR; VR normalizes ATR for comparison |
| **NATR** | NATR = ATR/Close×100; VR uses TR ratio instead |
| **Bollinger Width** | Both measure volatility; VR uses TR, BB uses std dev |
| **Keltner Width** | KC uses ATR; VR provides ratio view of same data |
| **ADX** | ADX measures trend strength; VR measures volatility expansion |
| **NATR** | NATR = ATR/Close×100; VR = TR/ATR |

## Implementation Notes

### State Management

The indicator maintains a compact state record:
- `RawAtr`: Running RMA value (before bias correction)
- `ECompensator`: Bias compensator $(1-\alpha)^n$
- `PrevClose`: Previous bar's close for True Range
- `LastValidVr`: Last valid output for NaN handling
- `Count`: Bar count for warmup tracking
- `HasPrevClose`: Flag for first-bar handling

### NaN/Infinity Handling

Invalid HLC inputs are detected and the last valid VR is substituted. This prevents NaN propagation through the calculation chain.

### Numerical Stability

The implementation uses:
- Epsilon guard (1e-10) for ATR division safety
- Zero return when ATR < epsilon
- Last-valid substitution for non-finite results

## References

- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research.
- Kaufman, P. J. (2013). *Trading Systems and Methods* (5th ed.). John Wiley & Sons.
- Kirkpatrick, C. D., & Dahlquist, J. R. (2010). *Technical Analysis: The Complete Resource for Financial Market Technicians* (2nd ed.). FT Press.
