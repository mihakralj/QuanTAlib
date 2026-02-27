# EVWMA: Elastic Volume Weighted Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (EVWMA)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period` bars                          |

### TL;DR

- EVWMA (Elastic Volume Weighted Moving Average) is a volume-adaptive moving average that weights each bar's contribution to the average by its volum...
- Parameterized by `period` (default 20).
- Output range: Unbounded.
- Requires `> period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Volume is the one technical indicator that never lies." — Joe Granville

## Introduction

EVWMA (Elastic Volume Weighted Moving Average) is a volume-adaptive moving average that weights each bar's contribution to the average by its volume relative to a rolling volume sum. High-volume bars shift the average more aggressively toward the current price; low-volume bars barely nudge it. The "elastic" behavior emerges from volume-proportional blending: the smoothing factor is not fixed (like EMA's alpha) but varies dynamically with each bar's volume share.

Unlike VWMA (which computes a windowed weighted mean), EVWMA is recursive. Each output depends on the previous output, making it structurally closer to an EMA with a variable smoothing factor than to a simple weighted average.

## Historical Context

EVWMA was introduced by Christian P. Fries as a volume-aware alternative to traditional exponential smoothing. The core insight: fixed-alpha smoothing treats all bars identically regardless of trading activity. A bar with 10x normal volume carries the same weight as a quiet bar. EVWMA corrects this by making the effective smoothing factor proportional to the current bar's volume share within the lookback window.

Several implementations exist in the wild, most derived from the same recursive formula. The rolling volume sum approach (circular buffer) was popularized by TradingView implementations and provides O(1) streaming updates without requiring a full window rescan.

## Architecture and Physics

### 1. Rolling Volume Sum (Circular Buffer)

A circular buffer of length `period` tracks historical volumes. On each bar:

1. Remove the oldest volume from the running sum
2. Add the current volume to the running sum
3. Overwrite the oldest slot with current volume

This yields O(1) per-bar cost for maintaining the volume denominator.

### 2. Recursive EVWMA Calculation

Given:

- `sumVol` = rolling sum of volumes over the last `period` bars
- `curVol` = current bar's volume
- `curPrice` = current bar's close price
- `prevResult` = previous EVWMA value

The update rule:

$$
\text{EVWMA}_t = \frac{(\text{sumVol} - \text{curVol}) \cdot \text{EVWMA}_{t-1} + \text{curVol} \cdot \text{curPrice}}{\text{sumVol}}
$$

Equivalently, defining the effective alpha as $\alpha_t = \frac{\text{curVol}}{\text{sumVol}}$:

$$
\text{EVWMA}_t = (1 - \alpha_t) \cdot \text{EVWMA}_{t-1} + \alpha_t \cdot \text{curPrice}
$$

This is an EMA with a time-varying smoothing factor driven by volume proportion.

### 3. Initialization

First bar: `result = curPrice` (no prior state to blend with).

### 4. Edge Cases

- **Zero volume**: curVol = 0 means alpha = 0, so result = prevResult (no change). The price is ignored when volume is zero.
- **All-zero volume window**: sumVol = 0, result holds at previous value.
- **Volume clamping**: Negative volumes are clamped to zero.

## Mathematical Foundation

### Transfer Function

In the z-domain, EVWMA can be written as:

$$
H(z) = \frac{\alpha_t}{1 - (1 - \alpha_t) z^{-1}}
$$

where $\alpha_t = V_t / \sum_{k=0}^{P-1} V_{t-k}$ is the volume-dependent smoothing factor.

### Effective Alpha Range

- **Minimum alpha**: When curVol is tiny relative to sumVol (quiet bar amid heavy trading). Result barely moves.
- **Maximum alpha**: When curVol dominates sumVol (volume spike). Result snaps toward current price.
- **Uniform volume**: alpha = 1/count (degenerates toward a specific recursive average).

### FMA Optimization

The numerator uses fused multiply-add for reduced rounding error:

```text
numerator = FMA(remainVol, prevResult, curVol * curPrice)
```

where `remainVol = sumVol - curVol`.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count per bar |
|-----------|--------------|
| Additions | 3 |
| Subtractions | 2 |
| Multiplications | 2 |
| Divisions | 1 |
| FMA | 1 |
| Comparisons | 3-4 |
| **Total** | **~12 FLOPs** |

### Memory

| Component | Size |
|-----------|------|
| Volume buffer | `period * 8` bytes |
| State struct | ~48 bytes |
| **Total** | `period * 8 + 48` bytes |

### Quality Metrics

| Metric | Score (1-10) |
|--------|-------------|
| Responsiveness | 8 (volume-adaptive) |
| Smoothness | 7 (recursive, single-pole) |
| Lag | 7 (reduced on high-volume bars) |
| Noise rejection | 6 (volume-gated) |

## Validation

### External Libraries

| Library | Available | Notes |
|---------|-----------|-------|
| Skender | No | Not implemented |
| TA-Lib | No | Not implemented |
| Tulip | No | Not implemented |
| Ooples | No | Not implemented |

### Internal Consistency

All four API modes (streaming, batch TBarSeries, batch TSeries, span) produce identical results within floating-point tolerance (1e-10).

Known-value tests verify manual calculations against the recursive formula.

## Common Pitfalls

1. **Confusing EVWMA with VWMA**: VWMA is a windowed weighted mean (non-recursive); EVWMA is recursive with volume-varying alpha. They converge when volume is uniform but diverge significantly with volume spikes.

2. **Zero-volume bars**: By design, zero-volume bars do not move the average. If your data source produces zero-volume bars (e.g., overnight gaps), EVWMA will "freeze" during those periods.

3. **Period selection**: The period controls the volume window, not a price window. A period of 20 means "rolling sum of last 20 bars' volume." Shorter periods make the volume-weighting more responsive but amplify noise from individual volume spikes.

4. **Negative volume data**: Some data feeds report negative volume for corrections or adjustments. EVWMA clamps volume to zero, treating negative volume as no-activity.

5. **Floating-point drift**: Running sums accumulate drift over thousands of bars. The implementation resyncs every 1000 bars by recalculating the volume sum from the buffer.

6. **First-bar sensitivity**: The first bar initializes to the current price regardless of volume. This creates a brief transient; wait for the full warmup period before trusting the output.

## References

- Fries, C. P. "Elastic Volume Weighted Moving Average." Technical analysis research notes.
- Granville, J. "New Key to Stock Market Profits." Prentice-Hall, 1963. (Volume analysis foundations)
- Ehlers, J. F. "Cybernetic Analysis for Stocks and Futures." Wiley, 2004. (Adaptive smoothing concepts)
