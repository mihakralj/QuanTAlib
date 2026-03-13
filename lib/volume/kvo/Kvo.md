# KVO: Klinger Volume Oscillator

> *Volume is the fuel that drives the market train.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `fastPeriod` (default 34), `slowPeriod` (default 55), `signalPeriod` (default 13)                      |
| **Outputs**      | Single series (Kvo)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `slowPeriod` bars                          |
| **PineScript**   | [kvo.pine](kvo.pine)                       |

- The Klinger Volume Oscillator (KVO), developed by Stephen Klinger in the 1970s, measures the long-term trend of money flow while remaining sensitiv...
- **Similar:** [CMF](../cmf/Cmf.md), [Adosc](../adosc/Adosc.md) | **Complementary:** MACD | **Trading note:** Klinger Volume Oscillator; volume-based trend indicator. Signal line crossovers for entries.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Klinger Volume Oscillator (KVO), developed by Stephen Klinger in the 1970s, measures the long-term trend of money flow while remaining sensitive to short-term fluctuations. Unlike simple volume indicators, KVO incorporates price direction and range into its volume analysis, creating a comprehensive measure of buying and selling pressure that can identify divergences before they appear in price action.

## Historical Context

Stephen Klinger developed this oscillator to address a fundamental limitation of traditional volume analysis: the inability to distinguish between accumulation (buying pressure) and distribution (selling pressure) in a mathematically rigorous way. The innovation was combining volume with a "Cumulation Measure" (CM) that weights volume based on where the close falls within the bar's range, multiplied by the prevailing trend direction.

The indicator gained popularity in the 1980s and 1990s among professional traders who valued its ability to confirm trends and spot divergences. The signal line crossover system provides clear entry/exit signals similar to MACD but focused entirely on volume dynamics.

## Architecture & Physics

### 1. Typical Price (HLC3) Calculation

The foundation uses the typical price for trend determination:

$$
HLC3_t = \frac{High_t + Low_t + Close_t}{3}
$$

### 2. Trend Direction

The trend is determined by comparing consecutive HLC3 values:

$$
Trend_t = \begin{cases}
+1 & \text{if } HLC3_t > HLC3_{t-1} \\
-1 & \text{if } HLC3_t < HLC3_{t-1} \\
Trend_{t-1} & \text{otherwise}
\end{cases}
$$

### 3. Cumulation Measure (CM)

The CM quantifies where the close falls within the bar's range:

$$
Range_t = High_t - Low_t
$$

$$
CM_t = \begin{cases}
\left|2 \times \frac{Range_t - (Close_t - Low_t)}{Range_t} - 1\right| & \text{if } Range_t > 0 \\
0 & \text{otherwise}
\end{cases}
$$

### 4. Direction Multiplier (DM)

The DM combines trend, volume, and cumulation:

$$
DM_t = Trend_t \times Volume_t \times CM_t
$$

### 5. EMA Calculations with Compensator

The oscillator uses compensated EMAs for proper warmup handling:

$$
\alpha_{fast} = \frac{2}{FastPeriod + 1}, \quad \alpha_{slow} = \frac{2}{SlowPeriod + 1}
$$

$$
EMA_{fast,t} = \alpha_{fast} \times DM_t + (1 - \alpha_{fast}) \times EMA_{fast,t-1}
$$

During warmup (compensator > 1e-10):

$$
CompensatedEMA = \frac{EMA}{1 - (1-\alpha)^t}
$$

### 6. KVO Line

$$
KVO_t = FastEMA_t - SlowEMA_t
$$

### 7. Signal Line

An EMA of the KVO line:

$$
Signal_t = EMA(KVO_t, SignalPeriod)
$$

## Mathematical Foundation

### EMA Compensator Pattern

The implementation uses an EMA compensator to eliminate early-stage bias:

```
e *= decay  // decay = 1 - alpha
compensatedValue = ema / (1 - e)
```

When `e` decays below threshold (1e-10), the compensator is disabled and raw EMA values are used.

### FMA Optimization

Hot path calculations use fused multiply-add for precision and performance:

$$
EMA_{t} = FMA(EMA_{t-1}, decay, \alpha \times input)
$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 15 | 1 | 15 |
| MUL | 12 | 3 | 36 |
| DIV | 4 | 15 | 60 |
| CMP | 4 | 1 | 4 |
| ABS | 1 | 1 | 1 |
| FMA | 3 | 4 | 12 |
| **Total** | **39** | — | **~128 cycles** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | EMA compensator eliminates warmup bias |
| **Timeliness** | 7/10 | EMA smoothing introduces lag proportional to periods |
| **Overshoot** | 8/10 | Minimal overshoot due to EMA characteristics |
| **Smoothness** | 8/10 | Dual EMA provides good noise rejection |
| **Volume Sensitivity** | 9/10 | Direct volume incorporation with CM weighting |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | ✅ | Has kvo; formula details may differ |
| **Ooples** | ✅ | Has KlingerVolumeOscillator; EMA warmup may differ |

## Common Pitfalls

1. **Warmup Period**: The indicator requires at least `SlowPeriod` bars for meaningful values. The EMA compensator handles warmup mathematically but early signals should be treated cautiously.

2. **Period Relationship**: Fast period must be less than slow period (`FastPeriod < SlowPeriod`). Violating this constraint throws an exception.

3. **Volume Dependency**: KVO is fundamentally a volume indicator. Markets with unreliable or artificial volume data (forex, some crypto exchanges) may produce misleading signals.

4. **Zero Range Bars**: Doji candles (High == Low) result in CM = 0, producing no volume contribution for that bar regardless of volume.

5. **Signal Crossovers**: Like MACD, the KVO generates signals through crossovers. The signal line is an EMA of KVO, so crossovers lag the actual inflection points.

6. **Memory Footprint**: Per instance: ~200 bytes for state struct. Scales linearly with number of indicator instances.

## Interpretation

- **Positive KVO**: Indicates accumulation (buying pressure exceeds selling pressure)
- **Negative KVO**: Indicates distribution (selling pressure exceeds buying pressure)
- **Signal Line Crossover**: When KVO crosses above signal line, bullish signal; below, bearish
- **Zero Line Crossover**: Confirms trend direction change
- **Divergences**: When price makes new highs/lows but KVO does not, potential reversal signal

## References

- Klinger, S. (1977). "Summing Up Volume." *Stocks & Commodities Magazine*.
- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- https://github.com/mihakralj/pinescript/blob/main/indicators/volume/kvo.md