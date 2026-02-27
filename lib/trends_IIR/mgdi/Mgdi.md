# MGDI: McGinley Dynamic Indicator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14), `k` (default 0.6)                      |
| **Outputs**      | Single series (Mgdi)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- MGDI (McGinley Dynamic Indicator) looks like a moving average but operates on a fundamentally different principle.
- Parameterized by `period` (default 14), `k` (default 0.6).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "John McGinley saw moving averages failing in fast markets and said, 'It's not the market's fault, it's the math's fault.' MGDI is the apology."

MGDI (McGinley Dynamic Indicator) looks like a moving average but operates on a fundamentally different principle. Rather than using a fixed smoothing factor, it dynamically adjusts based on the ratio between price and the indicator's current value. The result is a filter that accelerates to catch breakouts while decelerating to avoid overshooting reversals—a behavior that fixed-alpha filters cannot achieve.

## Historical Context

Published by John McGinley in the *Market Technicians Association Journal* (1991), the Dynamic was created to be a "market tool" rather than just an indicator. McGinley observed that traditional moving averages have a fundamental flaw: their fixed period means they're either too slow in fast markets or too jittery in slow ones.

His insight was that the appropriate smoothing should depend on the relationship between price and the average itself. When price pulls far ahead, the average should accelerate. When price falls back toward the average, it should decelerate to avoid overshooting. The fourth-power ratio term creates this asymmetric, self-correcting behavior.

## Architecture & Physics

MGDI uses a nonlinear feedback mechanism that creates adaptive smoothing.

### 1. The Core Recursion

The update formula resembles an EMA but with a dynamic denominator:

$$\text{MGDI}_t = \text{MGDI}_{t-1} + \frac{P_t - \text{MGDI}_{t-1}}{k \times N \times \left(\frac{P_t}{\text{MGDI}_{t-1}}\right)^4}$$

The numerator $(P_t - \text{MGDI}_{t-1})$ is the standard "error" term—how far price is from the current estimate.

### 2. The Adaptive Denominator

The denominator $k \times N \times (P_t / \text{MGDI}_{t-1})^4$ is where the magic happens:

- **When $P_t > \text{MGDI}_{t-1}$**: The ratio exceeds 1, the fourth power amplifies it, the denominator grows, and the adjustment shrinks. This prevents overshooting during rallies.

- **When $P_t < \text{MGDI}_{t-1}$**: The ratio is below 1, the fourth power shrinks it further, the denominator shrinks, and the adjustment grows. This allows faster catch-up during declines.

### 3. The Fourth Power Effect

The exponent of 4 creates strong nonlinearity:

| $P_t / \text{MGDI}_{t-1}$ | $(P_t / \text{MGDI}_{t-1})^4$ | Effect |
| :---: | :---: | :--- |
| 1.10 | 1.46 | Moderate slowdown |
| 1.20 | 2.07 | Significant slowdown |
| 0.95 | 0.81 | Mild speedup |
| 0.90 | 0.66 | Aggressive speedup |

This asymmetry is intentional: rallies are allowed to "run" without the indicator overshooting, while declines are tracked more aggressively.

## Mathematical Foundation

### The Update Formula

$$\text{MGDI}_t = \text{MGDI}_{t-1} + \frac{P_t - \text{MGDI}_{t-1}}{k \times N \times \left(\frac{P_t}{\text{MGDI}_{t-1}}\right)^4}$$

Where:
- $N$ is the period (calibration parameter, not a lookback window)
- $k$ is the McGinley constant (standard value: 0.6)
- $P_t$ is the current price
- $\text{MGDI}_{t-1}$ is the previous indicator value

### Effective Alpha

The effective smoothing factor varies with each bar:

$$\alpha_{eff} = \frac{1}{k \times N \times \left(\frac{P_t}{\text{MGDI}_{t-1}}\right)^4}$$

For $N=14$ and $k=0.6$, the effective alpha ranges from approximately 0.05 to 0.20 depending on price/indicator ratio.

### Relationship to EMA

When $P_t = \text{MGDI}_{t-1}$ (price equals indicator):

$$\alpha_{eff} = \frac{1}{k \times N} = \frac{1}{0.6 \times N}$$

For $N=14$: $\alpha_{eff} = 0.119$, which corresponds to roughly EMA(16).

### Initialization

The first value is typically set to the first price: $\text{MGDI}_0 = P_0$.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| DIV | 1 | 15 | 15 |
| POW (x^4) | 1 | ~12 | 12 |
| MUL | 2 | 3 | 6 |
| ADD/SUB | 2 | 1 | 2 |
| **Total** | **6** | — | **~35 cycles** |

The fourth power can be computed as two squarings: $(x^2)^2$, avoiding the expensive `Math.Pow` call.

### Batch Mode (SIMD/FMA Analysis)

MGDI is inherently recursive (each value depends on the previous), limiting SIMD parallelization. However, optimizations include:

| Optimization | Benefit |
| :--- | :--- |
| Inline squaring vs `Math.Pow` | ~20 cycles saved |
| FMA for numerator calc | ~2 cycles saved |
| Precompute $k \times N$ | Constant folding |

*Effective throughput: ~30 cycles/bar after optimization.*

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Hugs price closely without breaking |
| **Timeliness** | 8/10 | Accelerates to catch up to price |
| **Overshoot** | 9/10 | Specifically designed to minimize overshoot |
| **Smoothness** | 9/10 | Visually pleasing, organic curve |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~8 ns/bar | Division and power dominate |
| **Allocations** | 0 bytes | Stack-based calculations only |
| **Complexity** | O(1) | Constant time update |
| **State Size** | 16 bytes | Single double + flags |

*Benchmarked on Intel i7-12700K @ 3.6 GHz, AVX2, .NET 10.0*

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | Matches `GetDynamic` (tolerance: 1e-9) |
| **Ooples** | ✅ | Matches `CalculateMcGinleyDynamicIndicator` |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |

## Common Pitfalls

1. **Not an EMA**: MGDI does not have a fixed alpha. Period comparisons with EMA are approximate at best. MGDI(14) does not equal EMA(14) in behavior or lag characteristics.

2. **Period Is Calibration**: The "Period" $N$ is a calibration constant, not a lookback window. MGDI(14) doesn't examine 14 bars of history—it's tuned to track instruments that typically move in 14-bar cycles.

3. **K Factor Sensitivity**: The constant $k=0.6$ is McGinley's recommended value. Reducing it (e.g., 0.4) makes the indicator more responsive but increases overshoot risk. Increasing it (e.g., 0.8) smooths further but adds lag.

4. **Division by Zero**: If $\text{MGDI}_{t-1} = 0$ (only possible with zero or negative prices), the formula fails. Implementation must guard against this edge case.

5. **Warmup Behavior**: The first few bars can exhibit unusual behavior until the indicator "locks on" to the price series. Allow 5-10 bars for stabilization.

6. **Ratio Extremes**: In volatile markets, the ratio $P_t / \text{MGDI}_{t-1}$ can reach extreme values. Some implementations clamp this ratio to prevent numerical instability.

7. **Bar Correction**: Use `isNew=false` for same-bar updates. The nonlinear formula means small price changes can produce disproportionate output changes during bar formation.

## References

- McGinley, J.R. (1991). "The McGinley Dynamic." *Market Technicians Association Journal*, Fall 1991.
