# ADOSC: Chaikin A/D Oscillator

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

- **Fast EMA (3)**: Represents the immediate, short-term money flow.
- **Slow EMA (10)**: Represents the established, medium-term money flow.
- **Difference**: The spread between them represents the momentum of accumulation.

### Zero-Allocation Design

Our implementation composes existing zero-allocation components (`Adl` and `Ema`). The `Update` method simply pipes the bar into the ADL, and the ADL result into the two EMAs.

## Mathematical Foundation

$$
ADOSC_t = EMA(ADL, 3)_t - EMA(ADL, 10)_t
$$

Where:

- $ADL$ is the Accumulation/Distribution Line.
- $EMA(X, N)$ is the Exponential Moving Average of X over N periods.

## Performance Profile

ADOSC is slightly heavier than ADL because it involves two EMAs.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15ns / bar | 1 ADL update + 2 EMA updates |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **Precision** | `double` | Required for EMA convergence |

## Validation

We validate against **TA-Lib**, **Skender.Stock.Indicators**, and **OoplesFinance**.

- **Accuracy**: Matches external libraries to 9 decimal places.
- **Note**: Tulip's `adosc` implementation diverges significantly from other libraries and is excluded from validation.

### Common Pitfalls

- **Volatility**: ADOSC is extremely volatile. It whipsaws frequently. It should never be used in isolation.
- **Trend Confirmation**: Use it to confirm a trend, not to predict it. If price is rising but ADOSC is falling (divergence), the rally is running on fumes.
- **Zero Line**: Crosses above zero indicate that short-term accumulation is overpowering long-term accumulation (Bullish). Crosses below zero indicate the opposite (Bearish).
