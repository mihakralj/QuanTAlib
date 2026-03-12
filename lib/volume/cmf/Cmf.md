# CMF: Chaikin Money Flow

> *Money flow tells you what the big players are doing. CMF tells you if they're winning.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (CMF)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period` bars                          |
| **PineScript**   | [cmf.pine](cmf.pine)                       |

- Chaikin Money Flow (CMF) is the normalized cousin of the Accumulation/Distribution Line.
- Parameterized by `period` (default 20).
- Output range: Unbounded.
- Requires `> period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Chaikin Money Flow (CMF) is the normalized cousin of the Accumulation/Distribution Line. While ADL is cumulative and unbounded, CMF oscillates between -1 and +1, measuring the persistence of buying or selling pressure over a rolling window.

The genius of CMF is that it answers not just "Are they buying?" but "Have they been buying *consistently*?" A CMF reading of +0.25 means 25% more money flow went into accumulation than distribution over the lookback period.

## Historical Context

Developed by Marc Chaikin as an evolution of his ADL work, CMF was designed to address ADL's major weakness: its unbounded nature made comparison across different securities impossible. By normalizing against volume, CMF became a true oscillator that traders could use with fixed thresholds.

Chaikin recommended watching for:
- CMF > 0: Bullish pressure dominates
- CMF < 0: Bearish pressure dominates
- CMF divergences: When price makes new highs but CMF fails to confirm

## Architecture & Physics

CMF builds on the Money Flow Multiplier concept but adds a rolling summation window. Instead of accumulating forever like ADL, it asks: "Over the last N periods, what's the net money flow relative to total volume?"

The key insight is **normalization by volume**. This means CMF can never exceed ±1, regardless of the absolute volume levels. A stock trading 10 million shares daily and one trading 10 thousand shares daily can both produce a CMF of 0.5—and that reading means the same thing for both.

### Component Breakdown

1. **Money Flow Multiplier (MFM)**: Same as ADL, ranges [-1, +1]
2. **Money Flow Volume (MFV)**: MFM × Volume
3. **Rolling Numerator**: Sum of MFV over period
4. **Rolling Denominator**: Sum of Volume over period
5. **CMF**: Numerator / Denominator

## Mathematical Foundation

### 1. Money Flow Multiplier (MFM)

$$
MFM_t = \frac{(Close_t - Low_t) - (High_t - Close_t)}{High_t - Low_t}
$$

Special case: If High = Low (no range), MFM = 0.

### 2. Money Flow Volume (MFV)

$$
MFV_t = MFM_t \times Volume_t
$$

### 3. Chaikin Money Flow (CMF)

$$
CMF_t = \frac{\sum_{i=t-n+1}^{t} MFV_i}{\sum_{i=t-n+1}^{t} Volume_i}
$$

where n is the lookback period (default: 20).

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 4 | Range calc, MFM numerator |
| DIV | 2 | MFM, final CMF |
| MUL | 1 | MFV calculation |
| ADD | 2 | Rolling sum updates |
| **Total** | ~9 | Per bar |

### Batch Mode (SIMD)

The MFM/MFV calculation is fully vectorizable. The rolling sum phase is inherently sequential but O(n) overall.

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Throughput** | 9 | O(1) per bar after warmup |
| **Allocations** | 0 | Two RingBuffers allocated once |
| **Complexity** | O(1) | Rolling sums, not recomputation |
| **Accuracy** | 10 | Matches reference implementations |
| **Timeliness** | 9 | 1-bar lag inherent in rolling window |
| **Overshoot** | 10 | Bounded [-1, +1] by construction |
| **Smoothness** | 5 | Smoother than raw ADL, but still responsive |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **QuanTAlib** | ✅ | Validated |
| **TA-Lib** | N/A | No direct CMF function |
| **Skender** | ✅ | Matches `GetCmf` exactly |
| **Tulip** | N/A | No CMF implementation |
| **Ooples** | ✅ | Matches `CalculateChaikinMoneyFlow` |

## Common Pitfalls

1. **Division by Zero**: If all volume in the period is zero (unlikely but possible with bad data), CMF is undefined. Implementation returns 0.

2. **Warmup Period**: CMF needs `period` bars before the rolling sums are meaningful. Before that, the calculation uses a growing window.

3. **Inside Bars**: When High = Low, the MFM is 0 regardless of close location. This is mathematically correct but can create unexpected readings.

4. **Volume Quality**: Like all volume-based indicators, CMF is only as good as the volume data. Crypto exchanges with wash trading, or futures with overnight gaps, can produce misleading readings.

5. **Threshold Fixation**: While ±0.25 is often cited as "strong" pressure, the appropriate threshold depends on the security's typical CMF volatility.

6. **isNew Parameter**: When correcting a bar (isNew=false), the implementation properly rolls back state. Failure to handle this causes cumulative errors.

## References

- Chaikin, M. (1996). "Chaikin Money Flow." *Technical Analysis of Stocks & Commodities*.
- StockCharts. "Chaikin Money Flow (CMF)." [Technical Indicators](https://school.stockcharts.com/doku.php?id=technical_indicators:chaikin_money_flow_cmf)
