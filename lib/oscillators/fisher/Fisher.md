# Fisher Transform (FISHER)

## Overview

The Fisher Transform converts price data into a Gaussian normal distribution using the inverse hyperbolic tangent function (arctanh), producing sharp turning points that aid in identifying potential price reversals. Developed by John Ehlers in 2002.

## Formula

```
displacement = floor(period / 2) + 1
normalized = 2 × (price − lowest) / (highest − lowest) − 1
value = α × normalized + (1 − α) × value[1]
value = clamp(value, −0.999, 0.999)
Fisher = 0.5 × ln((1 + value) / (1 − value))
Signal = α × Fisher + (1 − α) × Signal[1]
```

Where:
- `highest` / `lowest` = highest high / lowest low over `period` bars
- `α` = EMA smoothing factor (default: 0.33)
- The transform applies arctanh to the smoothed, normalized price

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| period | int | 10 | 1–500 | Lookback for min/max normalization |
| alpha | double | 0.33 | (0, 1] | EMA smoothing factor |

## Outputs

| Output | Description |
|--------|-------------|
| Fisher | Primary Fisher Transform line |
| Signal | EMA-smoothed signal line |

## Interpretation

- **Extreme Values**: Fisher > +2 suggests overbought; Fisher < −2 suggests oversold
- **Crossovers**: Fisher crossing above Signal = bullish; below = bearish  
- **Zero-Line**: Crossing zero indicates trend direction change
- **Divergence**: Price vs. Fisher divergence warns of potential reversal
- **Sharp Turns**: Fisher produces sharper peaks/troughs than raw oscillators

## Limitations

- Not bounded — extreme values depend on price volatility
- Can produce whipsaw signals in choppy/ranging markets
- Lagging due to EMA smoothing
- Normalization range affected by lookback period choice
- Domain protection (clamping to ±0.999) can compress extreme values

## References

- Ehlers, John F. "Using The Fisher Transform." *Stocks & Commodities*, 2002.
- PineScript source: `fisher.pine`

## Source

[Fisher.cs](Fisher.cs) | [Tests](Fisher.Tests.cs) | [Validation](Fisher.Validation.Tests.cs)
