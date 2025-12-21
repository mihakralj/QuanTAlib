# TRIMA: Triangular Moving Average

> "The weighted blanket of moving averages. It doesn't care where the price is going right now; it cares where the price feels most comfortable."

The Triangular Moving Average (TRIMA) places the majority of its weight on the middle of the data window, tapering off linearly towards the ends. This creates a triangular weight distribution (hence the name). It is mathematically equivalent to a double-smoothed SMA.

## Historical Context

TRIMA has been a staple in cycle analysis. By double-smoothing the data, it effectively removes high-frequency noise, making it ideal for identifying dominant market cycles. However, this smoothness comes at the cost of significant lag.

## Architecture & Physics

TRIMA is implemented as a cascade of two Simple Moving Averages.
$$ TRIMA = SMA(SMA(Price, P_1), P_2) $$

Where $P_1$ and $P_2$ are roughly half the total period.

### The Weight Distribution

An SMA has a rectangular weight distribution (all weights equal). A WMA has a linear distribution (heaviest at the end). TRIMA has a triangular distribution (heaviest in the center).

## Mathematical Foundation

### 1. Period Splitting

$$ P_1 = \lfloor \frac{N}{2} \rfloor + 1 $$
$$ P_2 = \lceil \frac{N+1}{2} \rceil $$

### 2. The Cascade

$$ TRIMA = SMA(SMA(Price, P_1), P_2) $$

## Performance Profile

## Validation

Validated against TA-Lib (`TA_TRIMA`) and Skender.Stock.Indicators.

### Common Pitfalls

1. **Lag**: TRIMA has more lag than SMA, EMA, or WMA. It is a lagging indicator, not a leading one.
2. **Signal Generation**: Due to its lag, TRIMA is poor for crossover signals. It is best used for visual trend identification or as a baseline for envelopes (e.g., TMA Bands).
3. **Even/Odd Periods**: The exact calculation of $P_1$ and $P_2$ differs slightly between implementations for even periods. QuanTAlib matches the standard definition used by TA-Lib.
