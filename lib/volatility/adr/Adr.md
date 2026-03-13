# ADR: Average Daily Range

> *The simplest measure is often the most useful. Why complicate what doesn't need complicating?*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`, `method` (default AdrMethod.Sma)                      |
| **Outputs**      | Single series (Adr)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `ma.WarmupPeriod` bars                          |
| **PineScript**   | [adr.pine](adr.pine)                       |

- The Average Daily Range (ADR) measures the average distance between High and Low prices over a specified period.
- **Similar:** [ATR](../atr/atr.md) | **Complementary:** Session range targets | **Trading note:** Average Daily Range; intraday range expectation.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Average Daily Range (ADR) measures the average distance between High and Low prices over a specified period. Unlike its cousin ATR, ADR ignores gaps entirely. It answers a straightforward question: "How much does this asset typically move within a single bar?"

This simplicity is ADR's strength. When you don't care about overnight gaps—perhaps you're day trading or analyzing intraday bars—ADR gives you exactly what you need without the complexity of True Range calculations.

## Historical Context

ADR predates ATR conceptually. Traders have been calculating average ranges since price charts existed. While Wilder formalized ATR in 1978 to account for gaps, the original range-based volatility measure never disappeared.

ADR remains popular among:

- **Day traders**: Gaps don't matter when you close positions before the session ends.
- **Intraday analysts**: 5-minute bars rarely gap; High-Low is the relevant measure.
- **Forex traders**: 24-hour markets gap infrequently; ADR and ATR often produce nearly identical results.

## Architecture & Physics

ADR uses composition to delegate smoothing to proven moving average implementations. The range calculation is trivial; the smoothing method determines ADR's character.

### Core Formula

$$
Range_t = High_t - Low_t
$$

### Smoothing Options

1. **SMA (Simple Moving Average)**: Equal weight to all bars in the period. Classic, stable, but can be "jumpy" when old values drop off.
2. **EMA (Exponential Moving Average)**: More recent bars weighted higher ($\alpha = 2/(N+1)$). Responsive to recent volatility changes.
3. **WMA (Weighted Moving Average)**: Linear weighting. Middle ground between SMA and EMA.

### The Gap Non-Problem

ADR intentionally ignores gaps. This is not a flaw—it's a feature.

- **Scenario**: Close = 100. Next Open = 110. High = 112. Low = 109.
- **ADR Range**: $112 - 109 = 3$.
- **ATR Range**: $112 - 100 = 12$.

If you're trading intraday and won't hold through the gap, ADR's 3 is the relevant number, not ATR's 12.

## Mathematical Foundation

### 1. Daily Range (DR)

$$
DR_t = H_t - L_t
$$

Where:

- $H_t$: Current High
- $L_t$: Current Low

### 2. Average Daily Range (ADR)

$$
ADR_t = MA(DR, N, method)
$$

Where $MA$ is one of:

**SMA:**
$$
ADR_t = \frac{1}{N} \sum_{i=0}^{N-1} DR_{t-i}
$$

**EMA:**
$$
ADR_t = \alpha \cdot DR_t + (1 - \alpha) \cdot ADR_{t-1}, \quad \alpha = \frac{2}{N+1}
$$

**WMA:**
$$
ADR_t = \frac{\sum_{i=0}^{N-1} (N-i) \cdot DR_{t-i}}{\sum_{i=0}^{N-1} (N-i)}
$$

## Performance Profile

### Operation Count (Streaming Mode)

ADR (Average Daily Range) uses a RingBuffer of daily ranges with a running sum for O(1) update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Daily range = High - Low | 1 | 1 cy | ~1 cy |
| RingBuffer add/evict | 1 | 3 cy | ~3 cy |
| running_sum += new - evict | 2 | 1 cy | ~2 cy |
| ADR = running_sum / N | 1 | 4 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~12 cy** |

O(1) sliding mean of daily ranges. Same running-sum pattern as SMA but applied to H-L. Throughput ~4 ns/bar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) via EMA, O(N) initial for SMA/WMA. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Streaming updates are constant time. |
| **Accuracy** | 10 | Simple calculation; no numerical edge cases. |
| **Timeliness** | 5-7 | Depends on smoothing method (EMA most responsive). |
| **Overshoot** | 0 | Absolute measure; cannot overshoot. |
| **Smoothness** | 6-8 | Depends on smoothing method (SMA smoothest). |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Manual SMA** | ✅ | Matches manual High-Low SMA calculation. |
| **Manual EMA** | ✅ | Matches manual High-Low EMA calculation. |
| **Manual WMA** | ✅ | Matches manual High-Low WMA calculation. |

**Note**: ADR is not a standard indicator in TA-Lib, Skender, Tulip, or Ooples. Validation is performed against manual calculations and cross-method consistency checks.

## ADR vs ATR: When to Use Which

| Scenario | Use ADR | Use ATR |
| :--- | :---: | :---: |
| Day trading (no overnight holds) | ✅ | |
| Intraday charts (1m, 5m, 15m) | ✅ | |
| 24-hour markets (Forex, Crypto) | ✅ | ✅ |
| Swing trading (overnight holds) | | ✅ |
| Daily charts with gaps | | ✅ |
| Position sizing through gaps | | ✅ |

### Common Pitfalls

- **Confusing ADR with ATR**: They measure different things. ADR ignores gaps; ATR accounts for them. Know which you need.
- **Wrong smoothing method**: SMA is stable but can jump when old values exit the window. EMA is smoother for trending volatility. Match the method to your use case.
- **Scale dependence**: Like ATR, ADR is absolute. An ADR of 5 on a \$100 stock is 5% volatility; on a \$10 stock, it's 50% volatility. Normalize if comparing across assets.
- **Assuming direction**: High ADR means wide bars, not up or down. Crashes and rallies both produce high ADR.