# DWMA: Double Weighted Moving Average

> "If one WMA is good, two must be better. DWMA is for when you want your signal so smooth it looks like it's been sanded, polished, and waxed."

DWMA (Double Weighted Moving Average) is exactly what it says on the tin: a Weighted Moving Average of a Weighted Moving Average. Unlike DEMA, which tries to *remove* lag, DWMA accepts lag as the price of admission for superior noise reduction. It produces a curve that is incredibly smooth, ideal for identifying long-term trends without getting faked out by market chop.

## Historical Context

There is no single "inventor" of DWMA; it's a natural extension of linear filtering. It represents a higher-order filter that prioritizes recent data (via WMA) but applies a second pass to iron out any remaining wrinkles. It's the heavy artillery of smoothing.

## Architecture & Physics

DWMA applies a linear weight kernel (triangle window) twice.

1. **Pass 1**: Calculate WMA of the price.
2. **Pass 2**: Calculate WMA of the result from Pass 1.

The effective window size is roughly $2 \times \text{Period}$, and the lag is cumulative. This is not for high-frequency scalping; this is for determining if the market is actually bullish or just having a manic episode.

## Mathematical Foundation

$$ \text{WMA}_1 = \text{WMA}(P, N) $$

$$ \text{DWMA} = \text{WMA}(\text{WMA}_1, N) $$

The weight profile of a single WMA is triangular. The weight profile of a DWMA approaches a Gaussian-like shape (central limit theorem in action), but heavily skewed towards recent data due to the WMA's linear weighting.

## Performance Profile

Despite the double pass, it remains O(1) thanks to the optimized WMA implementation.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ★★★★☆ | 2x cost of WMA (still O(1)). |
| **Allocations** | ★★★★★ | 0 bytes; hot path is allocation-free. |
| **Complexity** | ★★★★★ | O(1) constant time update. |
| **Precision** | ★★★★★ | `double` precision. |

### Zero-Allocation Design

DWMA is implemented by chaining two `Wma` instances. Since `Wma` is zero-allocation, DWMA inherits this property.

## Validation

Validated against chained WMA implementations in standard libraries.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against `WMA(WMA)`. |
| **Skender** | ✅ | Validated against chained `GetWma`. |
| **TA-Lib** | ✅ | Validated against chained `TA_WMA`. |
| **Tulip** | ✅ | Validated against chained `wma`. |
| **Ooples** | ✅ | Validated against chained `CalculateWeightedMovingAverage`. |

### Common Pitfalls

1. **Lag**: This indicator lags. A lot. Do not use it for entry signals on tight timeframes. Use it for trend filtering (e.g., "only buy if price > DWMA").
2. **Warmup**: It takes roughly $2 \times N$ bars to produce valid data.
3. **Confusion with DEMA**: DEMA = Fast, DWMA = Smooth. Do not mix them up.
