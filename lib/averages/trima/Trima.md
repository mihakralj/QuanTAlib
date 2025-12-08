# TRIMA: Triangular Moving Average

## Overview and Purpose

The Triangular Moving Average (TRIMA) is a technical indicator that applies a triangular weighting scheme to price data, providing enhanced smoothing compared to simpler moving averages. Originating in the early 1970s as technical analysts sought more effective noise filtering methods, the TRIMA was first popularized through the work of market technician Arthur Merrill. Its formal mathematical properties were established in the 1980s, and the indicator gained widespread adoption in the 1990s as computerized charting became standard. TRIMA effectively filters out market noise while maintaining important trends through its unique center-weighted calculation method.

## Core Concepts

* **Double-smoothing process:** TRIMA can be viewed as applying a simple moving average twice, creating more effective noise filtering
* **Triangular weighting:** Uses a symmetrical weight distribution that emphasizes central data points and reduces emphasis toward both ends
* **Market application:** Particularly effective for identifying the underlying trend in noisy market conditions where standard moving averages generate too many false signals
* **Timeframe flexibility:** Works across multiple timeframes, with longer periods providing cleaner trend signals in higher timeframes

The core innovation of TRIMA is its unique triangular weighting scheme, which can be viewed either as a specialized weight distribution or as a twice-applied simple moving average with adjusted period. This creates more effective noise filtering without the excessive lag penalty typically associated with longer-period averages. The symmetrical nature of the weight distribution ensures zero phase distortion, preserving the timing of important market turning points.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Length | 14 | Controls the lookback period | Increase for smoother signals in volatile markets, decrease for responsiveness |
| Source | close | Price data used for calculation | Consider using hlc3 for a more balanced price representation |

**Pro Tip:** For a good balance between smoothing and responsiveness, try using a TRIMA with period N instead of an SMA with period 2N - you'll get similar smoothing characteristics but with less lag.

## Calculation and Mathematical Foundation

**Simplified explanation:**
TRIMA calculates a weighted average of prices where the weights form a triangle shape. The middle prices get the most weight, and weights gradually decrease toward both the recent and older ends. This creates a smooth filter that effectively removes random price fluctuations while preserving the underlying trend.

**Technical formula:**
TRIMA = Σ(Price[i] × Weight[i]) / Σ(Weight[i])

Where the triangular weights form a symmetric pattern:

* Weight[i] = min(i, n-1-i) + 1
* Example for n=5: weights = [1,2,3,2,1]
* Example for n=4: weights = [1,2,2,1]

Alternatively, TRIMA can be calculated as:
TRIMA(source, p) = SMA(SMA(source, (p+1)/2), (p+1)/2)

> 🔍 **Technical Note:** The double application of SMA explains why TRIMA provides better smoothing than a single SMA or WMA. This approach effectively applies smoothing twice with optimal period adjustment, creating a -18dB/octave roll-off in the frequency domain compared to -6dB/octave for a simple moving average.

## C# Implementation

### Eventing and Reactive Support

This indicator implements the `ITValuePublisher` interface, enabling event-driven and reactive workflows.

* **Subscription:** Can be constructed with an `ITValuePublisher` (e.g., `TSeries`) to automatically update when the source emits a new value.
* **Publication:** Emits a `Pub` event with the new `TValue` whenever it is updated.

```csharp
using QuanTAlib;

// 1. Setup a source (publisher)
var source = new TSeries();

// 2. Create indicator subscribed to source
// It waits for events from 'source'
var trima = new Trima(source, period: 14);

// 3. Optional: Subscribe to indicator's output
trima.Pub += (item) => Console.WriteLine($"TRIMA Updated: {item.Value}");

// 4. Ingest data into source
// This triggers the chain: source -> trima -> Console.WriteLine
source.Add(new TValue(DateTime.Now, 100));
source.Add(new TValue(DateTime.Now, 105));
```

This pattern allows building complex, reactive processing pipelines without manual update loops.

## Interpretation Details

TRIMA can be used in various trading strategies:

* **Trend identification:** The direction of TRIMA indicates the prevailing trend
* **Signal generation:** Crossovers between price and TRIMA generate trade signals with fewer false alarms than SMA
* **Support/resistance levels:** TRIMA can act as dynamic support during uptrends and resistance during downtrends
* **Trend strength assessment:** Distance between price and TRIMA can indicate trend strength
* **Multiple timeframe analysis:** Using TRIMAs with different periods can confirm trends across different timeframes

## Limitations and Considerations

* **Market conditions:** Like all moving averages, less effective in choppy, sideways markets
* **Lag factor:** More lag than WMA or EMA due to center-weighted emphasis
* **Limited adaptability:** Fixed weighting scheme cannot adapt to changing market volatility
* **Response time:** Takes longer to reflect sudden price changes than directionally-weighted averages
* **Complementary tools:** Best used with momentum oscillators or volume indicators for confirmation

## References

* Ehlers, John F. "Cycle Analytics for Traders." Wiley, 2013
* Kaufman, Perry J. "Trading Systems and Methods." Wiley, 2013
* Colby, Robert W. "The Encyclopedia of Technical Market Indicators." McGraw-Hill, 2002
