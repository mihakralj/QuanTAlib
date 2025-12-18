# TEMA: Triple Exponential Moving Average

## What It Does

The Triple Exponential Moving Average (TEMA) is a technical indicator designed to smooth price data while virtually eliminating the lag associated with traditional moving averages. By combining a single, double, and triple Exponential Moving Average (EMA), TEMA creates a composite line that tracks price action with remarkable speed and accuracy.

## Historical Context

Developed by Patrick Mulloy and introduced in his 1994 article "Smoothing Data with Faster Moving Averages" in *Technical Analysis of Stocks & Commodities*, TEMA was created alongside DEMA (Double EMA) to solve the persistent problem of lag in trend-following indicators. Mulloy's innovation was to use the lag inherent in multiple EMA calculations to estimate and subtract the total lag from the original signal.

## How It Works

### The Core Idea

TEMA is not just "an EMA of an EMA of an EMA" (which would be very slow). Instead, it uses a clever formula to cancel out lag:

- $EMA_1$ has some lag.
- $EMA_2$ (EMA of EMA) has roughly double the lag.
- $EMA_3$ (EMA of EMA of EMA) has roughly triple the lag.

By combining these terms with specific weights ($3 \times EMA_1 - 3 \times EMA_2 + EMA_3$), the lag terms cancel out, leaving a moving average that hugs the price closely.

### Mathematical Foundation

$$ TEMA = (3 \times EMA_1) - (3 \times EMA_2) + EMA_3 $$

Where:

- $EMA_1 = EMA(Price)$
- $EMA_2 = EMA(EMA_1)$
- $EMA_3 = EMA(EMA_2)$

### Implementation Details

Our implementation uses three internal EMA instances.

- **Complexity:** O(1) per update.
- **Initialization:** We use Hunter's method for initializing the underlying EMAs to ensure the TEMA starts with valid values as early as possible.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Short (5-10) for scalping; Medium (20-50) for swing trading. |

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var tema = new Tema(period: 14);

// Process each new bar
TValue result = tema.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"TEMA: {result.Value:F2}");

// Check if buffer is full
if (tema.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries temaValues = Tema.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Tema.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var tema = new Tema(14);

// New bar
tema.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
tema.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | 3 EMA updates + scalar math |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Stores state for 3 internal EMAs |

## Interpretation

### Trading Signals

#### Trend Direction

- **Fast Response:** TEMA turns much faster than SMA or EMA. A turn in TEMA often precedes a turn in price trend.

#### Crossovers

- **Price Crossover:** Because TEMA hugs price so closely, crossovers are frequent. They are best used for short-term entries in the direction of a larger trend.

### When It Works Best

- **Momentum Trading:** TEMA is excellent for capturing short-term bursts of momentum.

### When It Struggles

- **Overshoot:** In a sudden V-shaped reversal, TEMA can "overshoot" the price briefly due to the momentum of its internal calculation components.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Composition

- **Implementation:** Composed of 3 `Ema` objects.
- **Rationale:** Reusing the robust `Ema` class ensures consistent behavior (like initialization and NaN handling) across the library.

## References

- Mulloy, Patrick G. "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, Jan 1994.
