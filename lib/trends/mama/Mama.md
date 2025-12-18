# MAMA: MESA Adaptive Moving Average

## What It Does

The MESA Adaptive Moving Average (MAMA) is a sophisticated trend-following indicator that adapts its responsiveness based on the rate of change of the market's phase (cycle). Unlike conventional adaptive averages that rely on volatility, MAMA uses the Hilbert Transform to determine the dominant cycle period and phase. It produces two lines: the MAMA line (primary) and the FAMA line (Following Adaptive Moving Average), which acts as a confirmation signal.

## Historical Context

Developed by John Ehlers and introduced in his 2001 book *"MESA and Trading Market Cycles"*, MAMA represents a significant leap in applying Digital Signal Processing (DSP) to technical analysis. Ehlers, an electrical engineer, adapted techniques used in geophysical exploration to financial markets, aiming to solve the perennial problem of lag in moving averages by distinguishing between cycle mode (ranging) and trend mode.

## How It Works

### The Core Idea

MAMA assumes that markets cycle. By measuring the phase rate of change of these cycles, MAMA determines whether the market is trending or cycling.

- **Trending Market:** Phase changes rapidly. MAMA increases its `alpha` (smoothing factor) to track price closely.
- **Cycling Market:** Phase changes slowly. MAMA decreases its `alpha` to filter out noise and avoid whipsaws.

### Mathematical Foundation

The algorithm involves several DSP steps:

1. **Hilbert Transform:** Decomposes the price series into In-Phase ($I$) and Quadrature ($Q$) components.
2. **Phase Calculation:** Determines the instantaneous phase angle: $\text{Phase} = \arctan(Q/I)$.
3. **Adaptive Alpha:** Calculated based on the rate of change of the phase ($\Delta\phi$):
   $$ \alpha = \frac{\text{FastLimit}}{\Delta\phi} $$
   The result is clamped between `SlowLimit` and `FastLimit`.
4. **MAMA Calculation:**
   $$ \text{MAMA} = \alpha \cdot \text{Price} + (1 - \alpha) \cdot \text{MAMA}_{prev} $$
5. **FAMA Calculation:**
   $$ \text{FAMA} = 0.5 \cdot \alpha \cdot \text{MAMA} + (1 - 0.5 \cdot \alpha) \cdot \text{FAMA}_{prev} $$

### Implementation Details

The implementation uses a Homodyne Discriminator to measure the cycle period and phase. It requires a lookback buffer of 7 samples to perform the necessary smoothing and Hilbert Transform operations. Despite the mathematical complexity, the update step is **O(1)** as it relies on a fixed-size window.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Fast Limit | 0.5 | Maximum adaptation rate | Controls sensitivity in trending markets. Higher = faster response. |
| Slow Limit | 0.05 | Minimum adaptation rate | Controls stability in ranging markets. Lower = smoother. |

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var mama = new Mama(fastLimit: 0.5, slowLimit: 0.05);

// Process each new bar
TValue result = mama.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"MAMA: {result.Value:F2}");
Console.WriteLine($"FAMA: {mama.Fama.Value:F2}");

// Check if buffer is full
if (mama.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API (object-oriented)
TSeries prices = ...;
TSeries mamaValues = Mama.Batch(prices, fastLimit: 0.5, slowLimit: 0.05);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Mama.Calculate(prices.AsSpan(), output.AsSpan(), fastLimit: 0.5, slowLimit: 0.05);
```

### Event-Driven Architecture

```csharp
var source = new TSeries();
var mama = new Mama(source);

// Subscribe to MAMA output
mama.Pub += (value) => {
    Console.WriteLine($"New MAMA value: {value.Value}");
    Console.WriteLine($"New FAMA value: {mama.Fama.Value}");
};

// Feeding source automatically triggers the chain
source.Add(new TValue(DateTime.Now, 105.2));
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Constant time DSP calculation |
| Batch processing | O(n) | Fast sequential processing |
| Memory footprint | O(1) | Fixed-size RingBuffers (7 elements) |

## Interpretation

### Trading Signals

#### Crossovers

- **Bullish:** MAMA crosses above FAMA. This typically happens early in a new uptrend.
- **Bearish:** MAMA crosses below FAMA. This signals the start of a downtrend.

#### Trend Strength

- **Separation:** The distance between MAMA and FAMA indicates the strength of the trend. Wide separation suggests a strong trend; convergence suggests consolidation.

### When It Works Best

- **Cycle-to-Trend Transitions:** MAMA excels at identifying when a market breaks out of a cycle into a trend, adapting its speed instantly.

### When It Struggles

- **Erratic Volatility:** Extremely noisy markets with no discernible cycle or trend can cause the phase calculation to be erratic, leading to false signals.

### Architecture Notes

This implementation makes specific trade-offs:

### Choice: Fixed-Size Buffers

- **Implementation:** Uses `RingBuffer` of size 7.
- **Rationale:** The Hilbert Transform and smoothing filters used by Ehlers have fixed coefficients requiring exactly 7 historical points. This ensures O(1) memory usage.

### Choice: Stack Allocation for Batch

- **Implementation:** Uses `stackalloc` for internal buffers in the static `Calculate` method.
- **Rationale:** Eliminates heap allocations during batch processing, maximizing performance for large datasets.

## References

- Ehlers, John F. "MESA and Trading Market Cycles." John Wiley & Sons, 2001.
- Ehlers, John F. "Cycle Analytics for Traders." John Wiley & Sons, 2013.
