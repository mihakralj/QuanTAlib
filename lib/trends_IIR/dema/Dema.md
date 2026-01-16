# DEMA: Double Exponential Moving Average

> "EMA is good. DEMA is better. It's like an EMA that drank a double espresso and stopped lagging behind the conversation."

DEMA (Double Exponential Moving Average) is not just "two EMAs." It's a clever mathematical hack to cancel out the lag inherent in a standard EMA. By subtracting the "error" (the difference between a single EMA and a double EMA) from the original EMA, DEMA produces a curve that hugs the price action much tighter. The extrapolation formula $2 \times \text{EMA}_1 - \text{EMA}_2$ effectively predicts where EMA "should be" based on its current trajectory.

## Historical Context

Introduced by Patrick Mulloy in the January 1994 issue of *Technical Analysis of Stocks & Commodities*, DEMA was designed to reduce the lag of trend-following indicators. Mulloy realized that smoothing always introduces lag, but by combining single and double smoothing, you could mathematically negate some of that delay.

The insight was elegant: if EMA1 lags price by $L$ bars, and EMA2 lags EMA1 by another $L$ bars, then the expression $2 \times \text{EMA1} - \text{EMA2}$ extrapolates forward by $L$, canceling the lag for linear trends. This principle later inspired TEMA (triple) and the broader family of lag-compensating filters.

## Architecture & Physics

DEMA is a composite indicator built from two EMAs in a cascade arrangement.

### 1. First EMA Stage (EMA1)

The primary smoother applied directly to price:

$$\text{EMA}_1 = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{1,t-1}$$

where $\alpha = \frac{2}{N + 1}$ and $N$ is the period.

### 2. Second EMA Stage (EMA2)

The secondary smoother applied to EMA1's output:

$$\text{EMA}_2 = \alpha \cdot \text{EMA}_1 + (1 - \alpha) \cdot \text{EMA}_{2,t-1}$$

### 3. Lag Cancellation Combiner

The final output extrapolates using the difference between stages:

$$\text{DEMA} = 2 \times \text{EMA}_1 - \text{EMA}_2$$

The "physics" relies on the fact that EMA2 lags EMA1 roughly as much as EMA1 lags the price. The coefficient 2 on EMA1 and -1 on EMA2 creates a unity-gain filter ($2 - 1 = 1$) that projects forward by one lag unit.

## Mathematical Foundation

### EMA Alpha Calculation

$$\alpha = \frac{2}{N + 1}$$

### Lag Analysis

For a single EMA with smoothing factor $\alpha$, the mean lag is:

$$L = \frac{1 - \alpha}{\alpha} = \frac{N - 1}{2}$$

For cascaded EMAs:
- EMA1 lag: $L$
- EMA2 lag (from price): $2L$

The DEMA formula extrapolates:

$$\text{DEMA} = \text{EMA}_1 + (\text{EMA}_1 - \text{EMA}_2)$$

This adds the "velocity" (difference) to the position (EMA1), projecting forward.

### Transfer Function

In the z-domain, DEMA's transfer function:

$$H(z) = 2 \cdot H_{EMA}(z) - H_{EMA}^2(z)$$

where $H_{EMA}(z) = \frac{\alpha}{1 - (1-\alpha)z^{-1}}$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 4 | 3 | 12 |
| ADD/SUB | 4 | 1 | 4 |
| **Total** | **8** | — | **~16 cycles** |

DEMA requires exactly 2× the operations of a single EMA.

### Batch Mode (SIMD/FMA Analysis)

Due to the recursive nature of EMA, SIMD vectorization is limited. However, FMA can reduce multiply-add pairs:

| Optimization | Operations | Cycles Saved |
| :--- | :---: | :---: |
| FMA for EMA1 update | 1 FMA vs MUL+ADD | ~2 |
| FMA for EMA2 update | 1 FMA vs MUL+ADD | ~2 |
| **Per-bar savings** | — | **~4 cycles** |

*Effective throughput: ~12 cycles/bar with FMA optimization.*

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Good trend tracking, overshoots on reversals |
| **Timeliness** | 8/10 | Significantly reduced lag vs EMA |
| **Overshoot** | 4/10 | Can overshoot significantly on sharp reversals |
| **Smoothness** | 6/10 | Less smooth than EMA due to extrapolation |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~3 ns/bar | 2× EMA cost |
| **Allocations** | 0 bytes | Hot path allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **State Size** | 48 bytes | Two EMA states |

*Benchmarked on Intel i7-12700K @ 3.6 GHz, AVX2, .NET 10.0*

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Matches `TA_DEMA` (tolerance: 1e-9) |
| **Skender** | ✅ | Matches `GetDema` (tolerance: 1e-9) |
| **Tulip** | ✅ | Matches `dema` (tolerance: 1e-9) |
| **Ooples** | ✅ | Matches `2*EMA - EMA(EMA)` formula |

## Common Pitfalls

1. **Overshoot on Reversals**: Because DEMA extrapolates using the EMA "velocity," it overshoots when price reverses direction. This is the fundamental tradeoff for reduced lag—the filter commits to trends and resists reversals.

2. **"Double" Misconception**: DEMA is *not* a double-smoothed average (EMA of EMA). That would increase lag. DEMA uses the double-smooth as a correction term to reduce lag.

3. **Warmup Period**: DEMA needs approximately $2N$ bars to converge fully, as EMA2 requires EMA1 to stabilize first. Use `IsHot` to detect convergence.

4. **Comparing Periods with EMA**: DEMA(20) is not equivalent to EMA(20) in responsiveness. Due to lag reduction, DEMA(20) behaves more like EMA(14-16) in terms of crossover timing.

5. **Signal Noise Amplification**: The extrapolation amplifies high-frequency components. In choppy markets, DEMA produces more whipsaws than EMA.

6. **Bar Correction**: Use `isNew=false` when correcting the current bar (same timestamp, revised price). State rollback ensures consistent results.

## References

- Mulloy, P. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1), 11-19.