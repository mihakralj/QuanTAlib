# SRSI: Apirine Slow Relative Strength Index

> *Standard RSI measures Close vs Close[1]. SRSI measures Close vs EMA(Close) — the smoothed baseline removes noise, producing a slower, more deliberate momentum reading.*

| Property         | Value                                  |
| ---------------- | -------------------------------------- |
| **Category**     | Oscillator                             |
| **Inputs**       | Source (close)                         |
| **Parameters**   | `emaLength` (default 6), `rsiLength` (default 14) |
| **Outputs**      | Single series (Srsi)                   |
| **Output range** | Bounded [0, 100]                       |
| **Warmup**       | `emaLength + rsiLength` bars           |
| **PineScript**   | [srsi.pine](srsi.pine)                |

- SRSI computes an EMA of closing prices, then applies the standard RSI formula to the difference between close and that EMA, using Wilder's smoothing (RMA) for gain/loss averaging.
- **Similar:** [RSI](../../momentum/rsi/Rsi.md) (uses Close - Close[1] instead of Close - EMA) | **Complementary:** Moving averages for trend direction | **Trading note:** Output bounded [0, 100]; 80/20 overbought/oversold levels.
- No external validation libraries implement SRSI. Validated through self-consistency and behavioral testing against the published algorithm.

SRSI is Vitali Apirine's 2015 alternative to the standard RSI. By replacing the simple price-change basis (Close - Close[1]) with the deviation from an EMA baseline (Close - EMA(Close)), SRSI responds more slowly to quick price spikes and instead tracks the broader price-to-trend relationship. The EMA smoothing parameter controls how much trend is factored out, while the RSI length controls the momentum smoothing.

## Historical Context

Vitali Apirine published the Slow Relative Strength Index in the April 2015 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "The Slow Relative Strength Index." The article proposes replacing the standard RSI's change basis (Close - Close[1]) with the deviation from an exponential moving average (Close - EMA). This produces a "slower" RSI that filters out short-term noise and focuses on the broader momentum relationship between price and its smoothed trend.

## Architecture & Mathematics

### Stage 1: EMA of Close

$$\alpha = \frac{2}{\text{emaLength} + 1}$$

$$\text{EMA}_t = \alpha \cdot \text{Close}_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

Standard exponential moving average. The first value is seeded with the first close.

### Stage 2: Deviation from EMA

$$\text{diff}_t = \text{Close}_t - \text{EMA}_t$$

$$\text{posDiff} = \max(\text{diff}, 0), \quad \text{negDiff} = \max(-\text{diff}, 0)$$

The key innovation: instead of Close - Close[1] (standard RSI), SRSI uses the distance from the EMA. This removes short-term noise and measures momentum relative to the trend.

### Stage 3: Wilder's Smoothing (RMA)

$$\alpha_{\text{rma}} = \frac{1}{\text{rsiLength}}$$

$$\text{avgPos}_t = \text{avgPos}_{t-1} + \alpha_{\text{rma}} \cdot (\text{posDiff}_t - \text{avgPos}_{t-1})$$

$$\text{avgNeg}_t = \text{avgNeg}_{t-1} + \alpha_{\text{rma}} \cdot (\text{negDiff}_t - \text{avgNeg}_{t-1})$$

This is Wilder's original smoothing method (also called RMA), identical to that used in standard RSI. The first values are seeded directly.

### Stage 4: RSI Formula

$$\text{SRS} = \frac{\text{avgPos}}{\text{avgNeg}}$$

$$\text{SRSI} = 100 - \frac{100}{1 + \text{SRS}}$$

Standard RSI ratio formula. When avgNeg ≈ 0, output clips to 100 (pure positive momentum). When both ≈ 0, output returns 50 (neutral).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation             | Count | Notes                          |
|:--------------------- |:----- |:------------------------------ |
| FMA (EMA update)      | 1     | α * (Close - EMA) + EMA       |
| Subtraction (diff)    | 1     | Close - EMA                    |
| Max/classify × 2      | 2     | pos/neg diff classification    |
| FMA × 2 (RMA)         | 2     | Wilder's smoothing             |
| Division + arithmetic | 2     | SRS ratio + RSI formula        |
| **Total per bar**     | **~8** | Constant O(1), no buffers     |

### Batch Mode (SIMD Analysis)

The algorithm is purely recursive (IIR) — no lookback window needed. Batch mode processes sequentially but avoids per-bar allocation overhead. No RingBuffer is required; all state is scalar.

### Quality Metrics

| Metric              | Score   | Notes                                   |
|:------------------- |:------- |:--------------------------------------- |
| Lag                 | Medium  | EMA stage adds smoothing lag            |
| Noise rejection     | High    | EMA pre-filter removes short-term noise |
| Sensitivity         | Lower   | Slower than standard RSI by design      |
| Bounded output      | Yes     | [0, 100] from RSI formula              |
| Parameter count     | 2       | emaLength + rsiLength                   |
| Memory footprint    | Minimal | No buffers, pure scalar state           |

## Validation

SRSI is validated through self-consistency tests (streaming ≡ batch ≡ span ≡ eventing) and behavioral tests (constant input → 50, trending signals, overbought/oversold boundary conditions).

### Behavioral Test Summary

| Test                    | Expected Result           |
|:----------------------- |:------------------------- |
| Constant input          | Output → 50              |
| Strong uptrend          | Output > 50 (→ 100)     |
| Strong downtrend        | Output < 50 (→ 0)       |
| Ascending vs descending | Opposite positions       |
| NaN/Inf input           | Finite output (fallback)  |
| Bar correction (isNew)  | State restored correctly  |

## Common Pitfalls

1. **RSI vs SRSI confusion**: Standard RSI uses Close - Close[1]. SRSI uses Close - EMA(Close). They are not interchangeable and will produce different signals, especially during choppy markets.

2. **EMA length choice**: The default emaLength=6 produces a fast EMA that closely tracks price. Increasing emaLength creates a smoother baseline, making SRSI even slower. Very large emaLength values make SRSI approach a flat line near 50.

3. **Overbought/oversold levels**: The standard 80/20 levels (not the RSI-standard 70/30) are recommended by Apirine for SRSI due to its smoother nature.

4. **Warmup period**: The indicator needs emaLength + rsiLength bars to stabilize. Output before warmup should be treated as preliminary.
