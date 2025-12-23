# DEMA: Double Exponential Moving Average

> "EMA is good. DEMA is better. It's like an EMA that drank a double espresso and stopped lagging behind the conversation."

DEMA (Double Exponential Moving Average) is not just "two EMAs." It's a clever mathematical hack to cancel out the lag inherent in a standard EMA. By subtracting the "error" (the difference between a single EMA and a double EMA) from the original EMA, DEMA produces a curve that hugs the price action much tighter.

## Historical Context

Introduced by Patrick Mulloy in the January 1994 issue of *Technical Analysis of Stocks & Commodities*, DEMA was designed to reduce the lag of trend-following indicators. Mulloy realized that smoothing always introduces lag, but by combining single and double smoothing, you could mathematically negate some of that delay.

## Architecture & Physics

DEMA is a composite indicator built from two EMAs.

1. **EMA1**: The standard EMA of the price.
2. **EMA2**: The EMA of EMA1.

The "physics" relies on the fact that EMA2 lags EMA1 roughly as much as EMA1 lags the price. Therefore, $2 \times \text{EMA1} - \text{EMA2}$ pushes the value forward, correcting the lag.

## Mathematical Foundation

$$ \text{EMA}_1 = \text{EMA}(P, N) $$

$$ \text{EMA}_2 = \text{EMA}(\text{EMA}_1, N) $$

$$ \text{DEMA} = 2 \times \text{EMA}_1 - \text{EMA}_2 $$

Where $N$ is the period.

## Performance Profile

DEMA is extremely fast, requiring only a few floating-point operations per update.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ★★★★★ | 2x EMA cost (still O(1)). |
| **Allocations** | ★★★★★ | 0 bytes; hot path is allocation-free. |
| **Complexity** | ★★★★★ | O(1) recursive calculation. |
| **Precision** | ★★★★★ | `double` precision. |

### Zero-Allocation Design

DEMA is implemented using two internal `Ema` instances (or equivalent scalar state variables). The calculation is purely algebraic and requires no heap allocations during the `Update` cycle.

## Validation

Validated against TA-Lib, Skender, Tulip, and Ooples logic.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_DEMA`. |
| **Skender** | ✅ | Matches `GetDema`. |
| **Tulip** | ✅ | Matches `dema`. |
| **Ooples** | ✅ | Matches logic `2*EMA - EMA(EMA)`. |

### Common Pitfalls

1. **Overshoot**: Because DEMA subtracts lag, it can sometimes overshoot price turns. It's more volatile than a standard EMA.
2. **"Double" Misconception**: It is *not* a moving average of a moving average (that would be slower). It is a lag-corrected composite.
3. **Warmup**: DEMA needs about $2 \times N$ bars to converge fully, as the second EMA needs the first EMA to stabilize.
