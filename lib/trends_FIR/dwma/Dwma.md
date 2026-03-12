# DWMA: Double Weighted Moving Average

> *If one WMA is good, two must be better. DWMA is for when you want your signal so smooth it looks like it's been sanded, polished, and waxed.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Dwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `(period * 2) - 1` bars                          |
| **PineScript**   | [dwma.pine](dwma.pine)                       |
| **Signature**    | [dwma_signature](dwma_signature.md) |

- DWMA (Double Weighted Moving Average) is exactly what it says on the tin: a Weighted Moving Average of a Weighted Moving Average.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `(period * 2) - 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

### Operation Count (Streaming Mode, Scalar)

DWMA chains two WMA instances. Each WMA is O(1) with ~22 cycles (see WMA.md).

| Component | Operations | Cost (cycles) |
| :--- | :--- | :---: |
| WMA₁(Price) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| WMA₂(WMA₁) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| **Total** | **8 ADD/SUB, 2 MUL, 2 DIV** | **~44 cycles** |

**Hot path breakdown:**
- First WMA smooths the raw price → ~22 cycles
- Second WMA smooths the first WMA's output → ~22 cycles
- No additional combining math required

### Batch Mode (SIMD)

Each WMA component benefits from SIMD prefix-sum optimization:

| Component | Scalar (512 bars) | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| WMA₁ prefix sum | ~11K cycles | ~2.8K cycles | ~4× |
| WMA₂ prefix sum | ~11K cycles | ~2.8K cycles | ~4× |
| **Total** | **~22K** | **~5.6K** | **~4×** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches chained WMA exactly |
| **Timeliness** | 3/10 | Significant lag; double smoothing delays signals |
| **Overshoot** | 10/10 | Never overshoots input data range (FIR property) |
| **Smoothness** | 9/10 | Very smooth; approaches Gaussian-like profile |

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
