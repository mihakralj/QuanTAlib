# HMA: Hull Moving Average

> *Alan Hull looked at the lag in moving averages and said, 'I can fix that.' And he did, by making the math do gymnastics.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Hma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period + sqrtPeriod - 1` bars                          |
| **PineScript**   | [hma.pine](hma.pine)                       |
| **Signature**    | [hma_signature](hma_signature.md) |

- HMA (Hull Moving Average) is a solution to the eternal struggle between smoothness and lag.
- **Similar:** [DEMA](../../trends_IIR/dema/dema.md), [TEMA](../../trends_IIR/tema/tema.md) | **Complementary:** Signal line crossover | **Trading note:** Alan Hulls MA; cascades WMAs to nearly eliminate lag while maintaining smoothness.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HMA (Hull Moving Average) is a solution to the eternal struggle between smoothness and lag. Most indicators force you to choose one; HMA gives you both. It achieves this by using weighted moving averages (WMAs) in a clever configuration that cancels out lag while maintaining the smoothing properties of the WMA.

## Historical Context

Developed by Alan Hull in 2005, the HMA was designed to be "responsive, accurate, and smooth." Hull realized that lag is essentially a function of the period, and by combining averages of different periods (specifically, a full period and a half period), he could mathematically offset the lag.

## Architecture & Physics

The HMA is built from three Weighted Moving Averages (WMAs):

1. **WMA(n/2)**: A fast WMA of half the period.
2. **WMA(n)**: A slow WMA of the full period.
3. **WMA(sqrt(n))**: A smoothing WMA applied to the difference.

The core logic is: $2 \times \text{WMA}(n/2) - \text{WMA}(n)$.
This operation "over-weights" the recent data, pushing the average forward to align with the current price. The final WMA smooths out the resulting noise.

## Mathematical Foundation

$$ \text{Raw} = 2 \times \text{WMA}(P, \frac{N}{2}) - \text{WMA}(P, N) $$

$$ \text{HMA} = \text{WMA}(\text{Raw}, \sqrt{N}) $$

Where $N$ is the period.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

HMA chains three WMA instances. Each WMA is O(1) with ~22 cycles (see WMA.md).

| Component | Operations | Cost (cycles) |
| :--- | :--- | :---: |
| WMA(N/2) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| WMA(N) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| Combiner: 2×WMA₁ - WMA₂ | 1 MUL, 1 SUB | ~4 |
| WMA(√N) | 4 ADD/SUB, 1 MUL, 1 DIV | ~22 |
| **Total** | **~18 ops** | **~70 cycles** |

**Hot path breakdown:**
- `Raw = 2 × WMA(n/2) - WMA(n)`: 1 MUL + 1 SUB
- Three independent WMA updates execute in sequence
- Each WMA uses O(1) dual running-sum algorithm

### Batch Mode (SIMD)

Each WMA component benefits from SIMD prefix-sum optimization:

| Component | Scalar (512 bars) | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| WMA(N/2) batch | ~11K cycles | ~3K cycles | ~4× |
| WMA(N) batch | ~11K cycles | ~3K cycles | ~4× |
| Combiner | ~2K cycles | ~250 cycles | ~8× |
| WMA(√N) batch | ~11K cycles | ~3K cycles | ~4× |
| **Total** | **~35K** | **~9K** | **~4×** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches Skender, Tulip exactly |
| **Timeliness** | 9/10 | Lag-compensated design; very responsive |
| **Overshoot** | 4/10 | Can overshoot on sharp reversals (algebraic correction side effect) |
| **Smoothness** | 6/10 | Final √N smoothing moderates noise |

### Zero-Allocation Design

HMA is implemented by chaining three `Wma` instances. Since `Wma` is zero-allocation, HMA inherits this property.

## Validation

Validated against Skender, Tulip, and Ooples.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetHma`. |
| **Tulip** | ✅ | Matches `hma`. |
| **Ooples** | ✅ | Matches `CalculateHullMovingAverage` (with rounding caveats). |
| **TA-Lib** | ❌ | Not implemented. |

### External Library Discrepancies

**OoplesFinance.StockIndicators**:
Discrepancies exist due to different rounding methods for integer periods.

* **QuanTAlib**: Uses integer truncation (floor) for $N/2$ and $\sqrt{N}$.
* **Ooples**: Uses `Math.Round` (nearest integer).

This results in different effective periods for $N=14$ ($\sqrt{14} \approx 3.74 \to 3$ vs $4$) and others where the fractional part $\ge 0.5$. Validation tests match exactly for periods where rounding logic aligns (e.g., $N=9, 20, 50$).