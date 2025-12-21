# APO: Absolute Price Oscillator

> Percentages are for analysts. Traders pay bills in cash. APO tells you the cash value of the trend.

The Absolute Price Oscillator (APO) measures the raw currency difference between two exponential moving averages. Unlike its percentage-based cousin (PPO), APO speaks in dollars and cents, making it the preferred tool for spread traders, arbitrageurs, and anyone whose P&L is denominated in currency rather than basis points.

## The Cash Reality

A \$5 move on a \$100 stock (5%) feels different than a \$5 move on a \$20 stock (25%), but to a spread trader balancing a hedge, \$5 is \$5. Percentage oscillators distort this reality.

APO strips away the normalization. It simply asks: "How far is the fast trend from the slow trend in absolute terms?" This provides a direct read on the cash momentum of the asset.

## Architecture & Physics

APO is built on the foundation of the high-performance QuanTAlib `Ema` kernel. It inherits the $O(1)$ computational complexity and zero-allocation characteristics of the underlying moving averages.

1. **Dual EMA Engine**: Two independent Exponential Moving Averages (Fast and Slow) are maintained.
2. **Differential**: The arithmetic difference between them is computed.
3. **SIMD Acceleration**: For batch processing, hardware intrinsics are used to perform the subtraction across the entire dataset in parallel.

### Computational Efficiency

The EMAs are not recalculated from scratch. The state of both the fast and slow EMAs is maintained, allowing the APO update to be computed in constant time, regardless of the lookback period.

- **Time Complexity**: $O(1)$ per update.
- **Space Complexity**: $O(1)$ (two EMA state structs).
- **Allocations**: 0 bytes on the hot path.

## Mathematical Foundation

The formula is the definition of simplicity.

$$
APO_t = EMA(P, n_{fast}) - EMA(P, n_{slow})
$$

Where:

- $EMA$ is the recursive Exponential Moving Average.
- $n_{fast}$ is the fast period (default 12).
- $n_{slow}$ is the slow period (default 26).

## Performance Profile

APO performance is effectively the sum of two EMA calculations plus a subtraction.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15ns / bar | Sum of two EMA updates |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Batch** | SIMD | Uses `SimdExtensions.Subtract` for vectorization |
| **Precision** | `double` | Standard floating-point precision |

## Validation

The implementation is validated against industry standards to ensure correctness.

- **TA-Lib**: Matches `APO` with `MAType.Ema` (Precision: $10^{-9}$).
- **Tulip**: Note that Tulip's default `apo` may use SMA or different defaults; QuanTAlib strictly adheres to the EMA-based definition used by TA-Lib and major trading platforms.

### Common Pitfalls

- **Scale Sensitivity**: APO values are not normalized. An APO of 10.0 on Bitcoin is noise; on EUR/USD, it's a catastrophe. Use PPO for cross-asset comparisons.
- **Lag**: As a derivative of moving averages, APO lags price. The lag is a function of the slow period.
