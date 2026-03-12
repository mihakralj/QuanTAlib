# KAMA: Kaufman's Adaptive Moving Average

> *Perry Kaufman asked a simple question: 'Why should I use the same smoothing in a trending market as in a chopping market?' KAMA is the answer.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 10), `fastPeriod` (default 2), `slowPeriod` (default 30)                      |
| **Outputs**      | Single series (Kama)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [kama.pine](kama.pine)                       |
| **Signature**    | [kama_signature](kama_signature.md) |

- KAMA (Kaufman's Adaptive Moving Average) is an intelligent moving average that adjusts its smoothing speed based on market noise.
- Parameterized by `period` (default 10), `fastperiod` (default 2), `slowperiod` (default 30).
- Output range: Tracks input.
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

KAMA (Kaufman's Adaptive Moving Average) is an intelligent moving average that adjusts its smoothing speed based on market noise. When the price is moving steadily (high signal-to-noise ratio), KAMA speeds up to capture the trend. When the price is chopping sideways (low signal-to-noise ratio), KAMA slows down to filter out the noise.

## Historical Context

Perry Kaufman introduced KAMA in his book *Smarter Trading* (1998). It was one of the first widely adopted adaptive indicators, solving the problem of "whipsaws" in sideways markets without sacrificing responsiveness in trends.

## Architecture & Physics

KAMA uses an **Efficiency Ratio (ER)** to drive the smoothing constant of an EMA.

1. **Efficiency Ratio (ER)**: Measures the fractal efficiency of price movement.
    * $ER = \frac{\text{Net Change}}{\text{Sum of Absolute Changes}}$
    * ER approaches 1.0 in a straight line trend.
    * ER approaches 0.0 in pure noise.
2. **Smoothing Constant (SC)**: Scales between a "Fast" EMA (e.g., 2-period) and a "Slow" EMA (e.g., 30-period) based on ER.

## Mathematical Foundation

$$ ER = \frac{|P_t - P_{t-n}|}{\sum_{i=0}^{n-1} |P_{t-i} - P_{t-i-1}|} $$

$$ SC = \left( ER \times (\text{FastAlpha} - \text{SlowAlpha}) + \text{SlowAlpha} \right)^2 $$

$$ \text{KAMA}_t = \text{KAMA}_{t-1} + SC \times (P_t - \text{KAMA}_{t-1}) $$

Note the squaring of the SC, which suppresses the response to noise even further.

## Performance Profile

KAMA is very efficient, with O(1) complexity thanks to the incremental volatility update.

### Operation Count (Streaming Mode, Scalar)

**Hot path (buffer full):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ABS | 3 | 1 | 3 |
| ADD/SUB | 3 | 1 | 3 |
| DIV | 1 | 15 | 15 |
| FMA | 2 | 4 | 8 |
| MUL | 1 | 3 | 3 |
| CMP | 2 | 1 | 2 |
| **Total** | **12** | — | **~34 cycles** |

The hot path consists of:
1. Volatility update: `diff_in = |new - prev|`, `diff_out = |oldest - next_oldest|` — 2 ABS + 2 ADD/SUB
2. Change calculation: `|current - oldest|` — 1 ABS
3. Efficiency Ratio: `change / volatility` — 1 DIV
4. Smoothing Constant: `FMA(er, fast-slow, slow)`, then `sc * sc` — 1 FMA + 1 MUL
5. KAMA update: `FMA(sc, price - kama, kama)` — 1 FMA + 1 SUB
6. Bounds checks (ER cap, div-by-zero guard) — 2 CMP

**Warmup path (building volatility sum):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ABS | 1 | 1 | 1 |
| ADD | 1 | 1 | 1 |
| **Total** | **2** | — | **~2 cycles** |

During warmup, only accumulates `diff_in` without removal.

### Batch Mode (SIMD Analysis)

KAMA is an IIR filter with adaptive alpha — not vectorizable across bars due to recursive state dependency. The sliding-window volatility sum uses O(1) incremental updates rather than O(n) window scans.

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | Saves ~2 cycles per bar |
| Incremental volatility | O(1) vs O(period) per bar |
| stackalloc buffer | Zero heap allocation for period ≤256 |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Flattens in noise, tracks in trends |
| **Timeliness** | 8/10 | Accelerates quickly in strong trends |
| **Overshoot** | 9/10 | Very stable in sideways markets |
| **Smoothness** | 8/10 | Aggressive noise filtering via SC² |

## Validation

Validated against TA-Lib, Skender, Tulip, and Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `Kama` |
| **Skender** | ✅ | Matches `GetKama` |
| **Tulip** | ✅ | Matches `kama` |
| **Ooples** | ✅ | Matches `CalculateKaufmanAdaptiveMovingAverage` |
