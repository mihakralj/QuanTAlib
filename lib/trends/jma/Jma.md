# JMA - Jurik Moving Average

The Jurik Moving Average (JMA) is an advanced adaptive moving average that provides superior smoothing with minimal lag. It dynamically adjusts its response based on market volatility using a sophisticated multi-stage algorithm involving volatility distribution analysis and adaptive IIR filtering.

## Core Concepts

- **Volatility-Based Adaptation:** JMA uses a 128-sample volatility distribution with trimmed mean to estimate market conditions.
- **Dynamic Exponent:** The smoothing factor adjusts automatically based on the ratio of local deviation to reference volatility.
- **Phase Control:** Fine-tunes the balance between responsiveness and stability (-100 to +100).
- **Minimal Lag:** Tracks price action closely while filtering noise, outperforming traditional moving averages.
- **Warmup Period:** JMA requires approximately `20 + 80 × period^0.36` bars to stabilize its internal volatility distribution.

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| Period    | 10      | The base period for the moving average calculation. |
| Phase     | 0       | Phase shift (-100 to 100). Negative values reduce lag but may increase overshoot. Positive values increase smoothing and stability. |
| Power     | 0.45    | Legacy parameter kept for API compatibility. Not actively used in current implementation. |

## Algorithm

JMA employs a sophisticated multi-stage process:

1. **Adaptive Envelope:** Maintains upper and lower bands that adapt to price movement using dynamic smoothing.

2. **Local Deviation:** Calculates the maximum absolute distance between price and the envelope bands.

3. **Short-Term Volatility:** Computes a 10-bar simple moving average of the local deviation.

4. **Volatility Distribution:** Maintains a rolling 128-sample buffer of the short-term volatility values.

5. **Reference Volatility:** Calculates a trimmed mean of the volatility distribution:
   - Sorts the 128 samples
   - Takes the central 65 samples (indices 32-96)
   - Computes their mean, effectively removing outliers from both tails

6. **Dynamic Exponent:** Derives an adaptive smoothing factor:
   - Computes ratio: `local_deviation / reference_volatility`
   - Raises ratio to power `p = max(logParam - 2.0, 0.5)`
   - Clamps result between 1.0 and `logParam`

7. **2-Pole IIR Filter:** Applies a dual-pole Infinite Impulse Response filter using the dynamic exponent to produce the final JMA value with controlled phase shift.

This implementation is a high-fidelity port of the reverse-engineered JMA algorithm found in AmiBroker and MT4, optimized for performance using logarithmic transformations for power calculations.

## Usage

### Standard Usage

```csharp
using QuanTAlib;

// Create JMA with period 10, phase 0
var jma = new Jma(period: 10, phase: 0);

// Update with new values
var result = jma.Update(new TValue(DateTime.UtcNow, 100.0));

// Check if the indicator has warmed up
if (jma.IsHot)
{
    Console.WriteLine($"JMA: {jma.Last.Value}");
}
```

### Streaming (Event-driven)

```csharp
var source = new TSeries();
var jma = new Jma(source, period: 10);

source.Pub += (item) => {
    if (jma.IsHot)
    {
        Console.WriteLine($"JMA: {jma.Last.Value}");
    }
};

source.Add(new TValue(DateTime.UtcNow, 100.0));
```

### Batch Calculation

For high-performance batch processing:

```csharp
double[] prices = { 100.0, 101.5, 99.8, ... };
double[] output = new double[prices.Length];

Jma.Batch(prices, output, period: 10, phase: 0);
```

### Batch with TSeries

```csharp
TSeries prices = GetPriceData();
var jma = new Jma(period: 10);
TSeries results = jma.Update(prices);
```

## Key Properties

- **IsHot:** Returns `true` when JMA has processed enough bars to stabilize its internal volatility distribution (approximately `20 + 80 × period^0.36` bars).
- **Last:** The most recent calculated JMA value.
- **Name:** Identifier string in format `"Jma(period,phase,power)"`.

## Interpretation

- **Trend Identification:** Rising JMA indicates uptrend; falling JMA indicates downtrend.
- **Dynamic Support/Resistance:** JMA often acts as adaptive support in uptrends and resistance in downtrends.
- **Crossovers:** Price crossing above JMA can signal bullish momentum; crossing below can signal bearish momentum.
- **Phase Adjustment:**
  - Phase < 0: More responsive, faster signals, but may overshoot
  - Phase = 0: Balanced (default)
  - Phase > 0: Smoother, more stable, but with slightly more lag
- **Multi-Phase Ribbons:** Using multiple JMAs with different phases creates a visual "ribbon" showing trend strength and potential reversals.

## Performance Notes

- Uses `Math.Exp` optimization for power calculations (faster than `Math.Pow`)
- Employs SIMD operations for trimmed mean calculation
- Maintains minimal memory footprint with efficient buffer management
- Supports `isNew` parameter for bar amendment scenarios

## References

- [Jurik Research](http://www.jurikres.com/) - Original JMA developer
- [Pine Script Implementation](https://github.com/mihakralj/pinescript/blob/main/indicators/trends_IIR/jma.pine)
