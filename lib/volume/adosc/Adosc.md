# ADOSC: Chaikin A/D Oscillator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `fastPeriod` (default 3), `slowPeriod` (default 10)                      |
| **Outputs**      | Single series (Adosc)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `slowPeriod` bars                          |
| **PineScript**   | [adosc.pine](adosc.pine)                       |

- The Chaikin Oscillator (ADOSC) is an indicator of an indicator.
- Parameterized by `fastperiod` (default 3), `slowperiod` (default 10).
- Output range: Unbounded.
- Requires `slowPeriod` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Momentum precedes price. Volume momentum precedes price momentum."

The Chaikin Oscillator (ADOSC) is an indicator of an indicator. It applies the MACD formula to the Accumulation/Distribution Line (ADL) instead of the price.

While the ADL is great for spotting long-term flow, it can be sluggish. ADOSC acts as a turbocharger, measuring the *momentum* of that flow. It anticipates changes in the ADL, often signaling a reversal before the ADL itself turns.

## Historical Context

Marc Chaikin created this oscillator because he found the standard ADL too slow for timing entries. He realized that applying the moving average convergence/divergence (MACD) logic to the ADL would highlight the acceleration and deceleration of buying pressure.

## Architecture & Physics

ADOSC is a derivative indicator. It depends on:

1. **ADL**: The base volume flow metric.
2. **EMA**: Two exponential moving averages of that metric.

The physics here is identical to MACD:

* **Fast EMA (3)**: Represents the immediate, short-term money flow.
* **Slow EMA (10)**: Represents the established, medium-term money flow.
* **Difference**: The spread between them represents the momentum of accumulation.

## Mathematical Foundation

$$
ADOSC_t = EMA(ADL, 3)_t - EMA(ADL, 10)_t
$$

Where:

* $ADL$ is the Accumulation/Distribution Line.
* $EMA(X, N)$ is the Exponential Moving Average of X over N periods.

## Performance Profile

### Operation Count (Streaming Mode)

ADOSC = short EMA of ADL minus long EMA of ADL. Two parallel EMA updates per bar — O(1).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADL accumulation (MFM * Vol) | 1 | 8 cy | ~8 cy |
| Short EMA update (FMA) | 1 | 1 cy | ~1 cy |
| Long EMA update (FMA) | 1 | 1 cy | ~1 cy |
| ADOSC = shortEMA - longEMA | 1 | 1 cy | ~1 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~13 cy** |

O(1) per bar. Two EMA states maintained in parallel. After warmup (longPeriod bars), both EMAs are hot. FMA used for EMA update: new = FMA(prev, decay, alpha*adl).

ADOSC is slightly heavier than ADL because it involves two EMAs.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 15ns | 1 ADL update + 2 EMA updates |
| **Allocations** | 0 | Hot path is allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Matches all major libraries |
| **Timeliness** | 10/10 | Leading indicator of momentum |
| **Overshoot** | 8/10 | Can be volatile in choppy markets |
| **Smoothness** | 8/10 | Smoothed by EMAs |

## Validation

Validation is performed against **TA-Lib**, **Skender**, **Tulip**, and **OoplesFinance**.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `AdOsc` exactly. |
| **Skender** | ✅ | Matches `ChaikinOsc`. |
| **Tulip** | ✅ | Matches `adosc`. |
| **Ooples** | ✅ | Matches `ChaikinOscillator`. |

### Common Pitfalls

* **Volatility**: ADOSC is extremely volatile. It whipsaws frequently. It should never be used in isolation.
* **Trend Confirmation**: Use it to confirm a trend, not to predict it. If price is rising but ADOSC is falling (divergence), the rally is running on fumes.
* **Zero Line**: Crosses above zero indicate that short-term accumulation is overpowering long-term accumulation (Bullish). Crosses below zero indicate the opposite (Bearish).
