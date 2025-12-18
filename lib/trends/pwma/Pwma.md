# PWMA: Parabolic Weighted Moving Average

## What It Does

The Parabolic Weighted Moving Average (PWMA) applies a squared weighting scheme to historical prices, assigning significantly higher importance to the most recent data points than a standard Weighted Moving Average (WMA). While WMA uses linear weights ($1, 2, 3, \dots, n$), PWMA uses parabolic weights ($1^2, 2^2, 3^2, \dots, n^2$). This results in an indicator that tracks price action with exceptional responsiveness, making it ideal for fast-moving markets and momentum calculations.

## Historical Context

The concept of parabolic weighting is often associated with advanced signal processing techniques in finance, notably appearing as a core component in Jurik Research's "Velocity" indicator ($Velocity = PWMA - WMA$). By shifting the center of gravity even closer to the current price than a linear WMA, it minimizes lag to near-zero levels for recent price changes.

## How It Works

### The Core Idea

Imagine a 5-day window.

- **SMA:** Weights are $1, 1, 1, 1, 1$.
- **WMA:** Weights are $1, 2, 3, 4, 5$.
- **PWMA:** Weights are $1, 4, 9, 16, 25$.

In the PWMA, the most recent price (weight 25) is 25 times more important than the oldest price (weight 1), whereas in the WMA it is only 5 times more important. This aggressive weighting allows the PWMA to turn almost instantly when the trend changes.

### Mathematical Foundation

$$ PWMA = \frac{\sum_{i=1}^{n} i^2 \cdot P_i}{\sum_{i=1}^{n} i^2} $$

Where:

- $n$ = period length
- $P_i$ = price at position $i$ (oldest to newest)
- Denominator = $\frac{n(n+1)(2n+1)}{6}$ (sum of squares)

### Implementation Details: O(1) Streaming

Calculating the sum of $i^2 \cdot P_i$ for every bar would be computationally expensive ($O(n)$). We achieve **O(1)** complexity using a triple running sum technique:

1. **S1 (Simple Sum):** $\sum P_i$
2. **S2 (Linear Weighted Sum):** $\sum i \cdot P_i$
3. **S3 (Parabolic Weighted Sum):** $\sum i^2 \cdot P_i$

When the window slides:
$$ S1_{new} = S1_{old} - P_{oldest} + P_{new} $$
$$ S2_{new} = S2_{old} - S1_{old} + n \cdot P_{new} $$
$$ S3_{new} = S3_{old} - 2 \cdot S2_{old} + S1_{old} + n^2 \cdot P_{new} $$

This allows the indicator to update in constant time, regardless of the period length.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Shorter (5-10) for momentum; Longer (20+) for trend smoothing. |

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var pwma = new Pwma(period: 14);

// Process each new bar
TValue result = pwma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"PWMA: {result.Value:F2}");

// Check if buffer is full
if (pwma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API (object-oriented)
TSeries prices = ...;
TSeries pwmaValues = Pwma.Batch(prices, period: 14);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Pwma.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var pwma = new Pwma(14);

// New bar arrives
pwma.Update(new TValue(time, 100.5), isNew: true);

// Intra-bar price updates (real-time tick data)
pwma.Update(new TValue(time, 101.0), isNew: false); // Updates current bar
pwma.Update(new TValue(time, 100.8), isNew: false); // Updates current bar

// Next bar
pwma.Update(new TValue(time + 60, 101.2), isNew: true); // Advances state
```

### Event-Driven Architecture

```csharp
var source = new TSeries();
var pwma = new Pwma(source, period: 14);

// Subscribe to PWMA output
pwma.Pub += (value) => {
    Console.WriteLine($"New PWMA value: {value.Value}");
};

// Feeding source automatically triggers the chain
source.Add(new TValue(DateTime.Now, 105.2));
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Constant time triple-sum update |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(n) | Fast sequential processing |
| Memory footprint | O(period) | Uses a RingBuffer to store the lookback window |

## Interpretation

### Trading Signals

#### Momentum

- **Rapid Turns:** PWMA is excellent for identifying the exact moment a trend loses momentum, often turning before the price itself peaks or troughs.

#### Velocity

- **PWMA - WMA:** Subtracting a WMA from a PWMA of the same period creates a powerful momentum oscillator (Velocity) that is smoother than ROC but with less lag.

### When It Works Best

- **Fast Trends:** Markets that move parabolically or have sharp V-bottoms/tops.

### When It Struggles

- **Noise:** The extreme sensitivity to recent data means PWMA can be noisy in choppy markets. It is often best used as part of a composite indicator rather than a standalone filter.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Triple Running Sums

- **Implementation:** Maintains S1, S2, and S3.
- **Rationale:** Enables O(1) updates. A naive implementation would be O(n), which is unacceptable for large periods or high-frequency trading.

### Choice: Periodic Resync

- **Implementation:** Recalculates sums from scratch every 1,000 ticks.
- **Rationale:** Floating-point errors accumulate rapidly in the $S3$ term (which involves $n^2$). Periodic resync ensures long-term stability.

## References

- Colby, Robert W. "The Encyclopedia of Technical Market Indicators." McGraw-Hill, 2002.
- Jurik Research. "Velocity."
