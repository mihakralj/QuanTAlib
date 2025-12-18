# VEL - Jurik Velocity

A momentum oscillator that measures the market's "acceleration" by comparing two different weighting schemes. It isolates the rate of change without the noise inherent in simple price differencing.

## What It Does

VEL answers the question: "Is the trend speeding up or slowing down?"

Standard momentum indicators (like ROC) simply compare today's price to the price $N$ days ago. This is noisy and laggy. VEL takes a smarter approach: it compares a **Parabolic Weighted Moving Average (PWMA)** to a **Linear Weighted Moving Average (WMA)** of the same period.

Because PWMA weights recent data more aggressively (parabolically) than WMA (linearly), the difference between them reveals the "velocity" of the price movement. If prices are accelerating, the parabolic average pulls away from the linear one.

## Historical Context

Mark Jurik designed VEL to be a smoother, more responsive alternative to Momentum and ROC. By using the differential between two smoothed averages, he created a "derivative" indicator that captures the second-order characteristics of price movement (acceleration) while filtering out the high-frequency jitter that plagues raw rate-of-change calculations.

## How It Works

The magic lies in the weighting curves of the two underlying averages.

### The Math

$$ \text{VEL} = \text{PWMA}(n) - \text{WMA}(n) $$

Where:

- **PWMA**: Parabolic Weighted Moving Average. Weights decrease rapidly as you go back in time ($weight \propto x^2$).
- **WMA**: Weighted Moving Average. Weights decrease linearly as you go back in time ($weight \propto x$).

### The Logic

1. **Uptrend Acceleration**: Price is rising fast. The aggressive PWMA reacts quicker than the linear WMA. VEL becomes positive and rising.
2. **Uptrend Deceleration**: Price is still rising, but slower. The PWMA starts to converge with the WMA. VEL peaks and turns down (while price is still going up).
3. **Zero Cross**: The momentum has shifted. The "speed" is now zero, marking a potential reversal or transition to a downtrend.

## Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `period` | `int` | 14 | The lookback period for both underlying averages. |

## Performance Profile

VEL is a composite indicator that delegates its work to two efficient moving averages.

- **Complexity**: $O(1)$ per update. Both PWMA and WMA are implemented with $O(1)$ running sum algorithms.
- **Memory**: Constant space. Stores state for the two internal averages.
- **Allocations**: Zero heap allocations during the `Update` cycle.

| Operation | Time Complexity | Space Complexity |
|-----------|-----------------|------------------|
| Update    | $O(1)$          | $O(1)$           |
| Batch     | $O(N)$          | $O(N)$           |

## Interpretation

VEL is a classic centered oscillator.

### 1. Zero Line Crossover

- **Bullish Cross**: VEL crosses above 0. Momentum has shifted from negative to positive.
- **Bearish Cross**: VEL crosses below 0. Momentum has shifted from positive to negative.

### 2. Leading Indicator

VEL often turns *before* the price.

- **Peak**: A peak in VEL indicates that the *rate* of the price rise has maxed out. Price may continue to rise, but the "fuel" is running low.
- **Valley**: A trough in VEL indicates that the selling pressure has maxed out.

### 3. Divergence

- **Bearish Divergence**: Price makes a higher high, VEL makes a lower high. The trend is exhausting.
- **Bullish Divergence**: Price makes a lower low, VEL makes a higher low. The sell-off is losing steam.

## Architecture Notes

- **Composite Structure**: `Vel` wraps instances of `Pwma` and `Wma`.
- **Batch Optimization**: The static `Batch` method uses SIMD vector subtraction (`SimdExtensions.Subtract`) to compute the difference between the two averages efficiently over large datasets.
- **Warmup**: The indicator is considered "hot" when both underlying averages are hot.

## References

- Jurik Research: [VEL - Velocity](http://www.jurikres.com/catalog/ms_vel.htm)

## C# Usage

```csharp
using QuanTAlib;

// 1. Initialize
var vel = new Vel(period: 14);

// 2. Process a Value
var result = vel.Update(new TValue(DateTime.UtcNow, 105.5));

Console.WriteLine($"Velocity: {result.Value:F2}");

// 3. Batch Calculation
var series = new TBarSeries();
// ... populate series ...
var velSeries = Vel.Batch(series, period: 14);
