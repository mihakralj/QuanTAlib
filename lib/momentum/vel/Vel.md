# VEL: Jurik Velocity

> Momentum is easy. Smooth momentum without lag is hard. Jurik Velocity is the answer.

Jurik Velocity (VEL) is a momentum oscillator that measures the rate of change of price, but with a twist: it uses the difference between two sophisticated moving averages to smooth out the noise inherent in raw "price minus previous price" calculations.

## The Jurik Standard

Standard momentum ($P_t - P_{t-n}$) is notoriously jagged. It amplifies noise. Jurik's insight was to measure the divergence between a Parabolic Weighted Moving Average (PWMA) and a linear Weighted Moving Average (WMA). This creates a smoother, more reliable velocity metric that doesn't sacrifice responsiveness.

## Architecture & Physics

The physics of VEL rely on the different "inertia" of the two moving averages.

1. **PWMA**: A Parabolic Weighted Moving Average places extreme weight on the most recent data (quadratic weighting). It is highly responsive and "fast."
2. **WMA**: A standard Weighted Moving Average places linear weight on recent data. It is slightly "slower" than the PWMA.
3. **Differential**: By subtracting the slower WMA from the faster PWMA, we isolate the *acceleration* of the price.

### The Smoothing Effect

Because both components are weighted averages, they inherently filter out high-frequency noise. The difference between them represents the "clean" momentum of the trend. This is far superior to simply subtracting $P_{t-n}$ from $P_t$, which is sensitive to single-bar outliers.

### Zero-Allocation Design

The implementation leverages existing `Pwma` and `Wma` classes. The `Update` method is allocation-free. For batch processing, we use `stackalloc` for intermediate buffers when the dataset is small (<= 1024 bars), ensuring zero GC pressure.

## Mathematical Foundation

The calculation is elegantly simple, relying on the properties of the underlying averages.

### 1. Parabolic Weighted Moving Average

$$
PWMA_t = \frac{\sum_{i=0}^{N-1} (N-i)^2 P_{t-i}}{\sum_{i=0}^{N-1} (N-i)^2}
$$

### 2. Weighted Moving Average

$$
WMA_t = \frac{\sum_{i=0}^{N-1} (N-i) P_{t-i}}{\sum_{i=0}^{N-1} (N-i)}
$$

### 3. Velocity

$$
VEL = PWMA(Period) - WMA(Period)
$$

## Performance Profile

The complexity is linear with respect to the period for the initial calculation, but O(1) for streaming updates if the underlying averages are optimized.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~10ns / bar | Dependent on underlying MA performance |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **Precision** | `double` | Standard floating-point precision |

## Validation

We validate against **Jurik's published methodology**.

- **Smoothness**: VEL is significantly smoother than raw ROC or Momentum indicators.
- **Responsiveness**: Despite the smoothing, VEL leads simple moving average crossovers.

### Common Pitfalls

- **Not Normalized**: Unlike RSI or Stochastic, VEL is not bounded. It can go to +Infinity or -Infinity. You cannot use fixed overbought/oversold levels (e.g., +80/-80) across different assets or timeframes.
- **Zero Cross**: The zero line is the most important level. Crossing zero indicates a shift in momentum direction.
