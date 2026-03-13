# VROC: Volume Rate of Change

> *Yesterday's volume is ancient history; what matters is how fast it's changing.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 12), `usePercent` (default true)                      |
| **Outputs**      | Single series (Vroc)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period + 1` bars                          |
| **PineScript**   | [vroc.pine](vroc.pine)                       |

- VROC (Volume Rate of Change) measures the percentage or absolute change in volume over a specified lookback period.
- **Similar:** [VO](../vo/Vo.md), [PVO](../pvo/Pvo.md) | **Complementary:** Price ROC | **Trading note:** Volume Rate of Change; percentage change in volume. Spikes indicate potential breakouts.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

VROC (Volume Rate of Change) measures the percentage or absolute change in volume over a specified lookback period. Unlike moving average-based volume indicators that smooth data, VROC provides a direct comparison between current volume and historical volume, making it particularly useful for detecting sudden volume surges or contractions that may signal significant market events.

## Historical Context

The Rate of Change concept has been applied to price data since the early days of technical analysis. Gerald Appel and Fred Hitschler popularized applying ROC to volume in their 1979 work, recognizing that volume changes often precede price movements. The logic is straightforward: if volume is the fuel that drives price trends, then measuring how quickly that fuel is being consumed provides insight into trend sustainability.

VROC gained traction among commodity traders who observed that volume spikes often accompanied breakouts from consolidation patterns. The indicator's simplicity—requiring only current and historical volume—made it accessible for manual calculation before electronic charting became ubiquitous.

## Architecture & Physics

VROC operates on a simple lookback comparison with two calculation modes:

### 1. Ring Buffer Storage

The indicator maintains a circular buffer of size `period + 1` to store historical volume values. This enables O(1) lookback without requiring the entire price history:

$$
\text{Buffer}[i] = V_{t-i} \quad \text{for } i \in [0, \text{period}]
$$

### 2. Rate of Change Calculation

**Percentage Mode** (default):
$$
\text{VROC}_t = \frac{V_t - V_{t-n}}{V_{t-n}} \times 100
$$

**Point Mode**:
$$
\text{VROC}_t = V_t - V_{t-n}
$$

where:
- $V_t$ = current volume
- $V_{t-n}$ = volume from $n$ periods ago
- $n$ = lookback period

### 3. Division by Zero Protection

When historical volume equals zero, percentage mode returns 0 to avoid division errors:

$$
\text{VROC}_t = \begin{cases}
0 & \text{if } V_{t-n} = 0 \\
\frac{V_t - V_{t-n}}{V_{t-n}} \times 100 & \text{otherwise}
\end{cases}
$$

## Mathematical Foundation

### Percentage Interpretation

VROC percentage values have intuitive meanings:
- **VROC = 100%**: Volume has doubled compared to $n$ periods ago
- **VROC = 0%**: Volume is unchanged
- **VROC = -50%**: Volume has halved
- **VROC = -100%**: Volume has dropped to zero (theoretical)

### Point Mode Interpretation

Point mode shows absolute volume change in the same units as volume:
- Useful when comparing volume changes across consistent timeframes
- Not normalized—larger securities will show larger absolute changes

### Lookback Period Selection

Common period selections:
- **12 periods**: Standard setting, balances responsiveness with noise filtering
- **20-25 periods**: Approximates monthly trading days for daily charts
- **5-7 periods**: Weekly comparison for faster signals

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Buffer read | 1 | 1 | 1 |
| Buffer write | 1 | 1 | 1 |
| SUB | 1 | 1 | 1 |
| DIV | 1 | 15 | 15 |
| MUL | 1 | 3 | 3 |
| CMP | 1 | 1 | 1 |
| **Total** | **6** | — | **~22 cycles** |

### Memory Footprint

Per instance: `8 bytes × (period + 1)` for the ring buffer plus ~32 bytes for state.
- Period 12 (default): ~136 bytes
- Period 100: ~840 bytes

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact calculation, no approximations |
| **Timeliness** | 10/10 | Zero lag—direct comparison |
| **Smoothness** | 3/10 | No smoothing applied; can be noisy |
| **Simplicity** | 10/10 | Single parameter, intuitive output |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Matches ROC function applied to volume |
| **Skender** | N/A | No dedicated VROC; use ROC on volume series |
| **Tulip** | ✅ | roc function on volume matches |
| **TradingView** | ✅ | Built-in VROC matches percentage mode |

## Common Pitfalls

1. **Warmup Period**: VROC requires `period + 1` bars before producing valid output. Before warmup, the indicator returns 0. For a 12-period VROC, the first 12 values are unreliable.

2. **Zero Volume Handling**: Illiquid instruments or off-hours data may contain zero-volume bars. Percentage mode returns 0 when historical volume is zero; point mode handles this naturally.

3. **Scale Differences**: Percentage mode normalizes across securities; point mode does not. Don't compare point-mode VROC values between instruments with different typical volumes.

4. **No Smoothing**: Raw VROC can be noisy on intraday data. Consider applying an SMA or EMA to the VROC output for cleaner signals.

5. **Interpretation Asymmetry**: A 100% increase (doubling) and a 50% decrease (halving) are mathematically equivalent in magnitude but feel different psychologically. Be aware of this when setting threshold alerts.

6. **TValue Limitations**: VROC requires volume data. Using the TValue Update method (which lacks volume) preserves the last calculated VROC value but does not compute a new one.

## References

- Appel, G., & Hitschler, F. (1979). *Stock Market Trading Systems*. Dow Jones-Irwin.
- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Achelis, S. B. (2001). *Technical Analysis from A to Z*. McGraw-Hill.