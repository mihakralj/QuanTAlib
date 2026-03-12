# TRIMA: Triangular Moving Average

> *The weighted blanket of moving averages. It doesn't care where the price is going right now; it cares where the price feels most comfortable.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Trima)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `p1 + p2 - 1` bars                          |
| **PineScript**   | [trima.pine](trima.pine)                       |
| **Signature**    | [trima_signature](trima_signature.md) |

- The Triangular Moving Average (TRIMA) places the majority of its weight on the middle of the data window, tapering off linearly towards the ends.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `p1 + p2 - 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

### Operation Count (Streaming Mode, Scalar)

TRIMA chains two SMA instances. Each SMA is O(1) with ~17 cycles (see SMA.md).

| Component | Operations | Cost (cycles) |
| :--- | :--- | :---: |
| SMA(P₁) | 2 ADD/SUB, 1 DIV | ~17 |
| SMA(P₂) | 2 ADD/SUB, 1 DIV | ~17 |
| **Total** | **4 ADD/SUB, 2 DIV** | **~34 cycles** |

**Hot path breakdown:**
- First SMA smooths the raw price → ~17 cycles
- Second SMA smooths the first SMA's output → ~17 cycles
- No additional combining math required

### Batch Mode (SIMD)

Each SMA component benefits from SIMD prefix-sum optimization:

| Component | Scalar (512 bars) | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| SMA(P₁) prefix sum | ~8.5K cycles | ~1K cycles | ~8× |
| SMA(P₂) prefix sum | ~8.5K cycles | ~1K cycles | ~8× |
| **Total** | **~17K** | **~2K** | **~8×** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib exactly |
| **Timeliness** | 2/10 | Significant lag; double smoothing delays signals |
| **Overshoot** | 10/10 | Never overshoots input data range (FIR property) |
| **Smoothness** | 9/10 | Very smooth; triangular weighting suppresses noise |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_TRIMA` exactly. |
| **Skender** | ✅ | Matches composite `SMA(SMA)` logic. |
| **Tulip** | ✅ | Matches `trima` exactly. |
| **Ooples** | N/A | Not implemented. |
