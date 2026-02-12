# Stochastic Oscillator (STOCH)

## Overview

The Stochastic Oscillator measures the position of the closing price relative to the high-low range over a lookback period. Developed by George C. Lane in the late 1950s, it is one of the most widely used momentum oscillators in technical analysis.

The indicator produces two lines:
- **%K** (Fast Stochastic): Raw position within the range, scaled 0–100
- **%D** (Signal line): Simple Moving Average of %K

## Origin and Sources

George C. Lane introduced the Stochastic Oscillator based on the observation that closing prices tend to cluster near the high of the trading range during uptrends and near the low during downtrends. The indicator quantifies this tendency.

**Key references:**
- Lane, George C. "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*, 1984
- Murphy, John J. *Technical Analysis of the Financial Markets*, 1999
- Appel, Gerald & Hitschler, Fred. *Stock Market Trading Systems*, 1980

## Mathematical Formula

### Core Calculation

```
%K = 100 × (Close − Lowest Low) / (Highest High − Lowest Low)

Where:
  Lowest Low  = min(Low[i])  for i ∈ [0, kLength-1]
  Highest High = max(High[i]) for i ∈ [0, kLength-1]

%D = SMA(%K, dPeriod)
```

### Edge Case

When `Highest High = Lowest Low` (zero range), `%K = 0`.

### Signal Line

`%D` is computed as a Simple Moving Average of `%K` values using a circular buffer with a running sum for O(1) per-bar computation.

## Architecture

### Streaming Path

The streaming implementation uses **monotonic deques** for O(1) amortized highest-high and lowest-low tracking:

- **MonotonicDeque** (max): Maintains decreasing order of high values; front always holds the current maximum
- **MonotonicDeque** (min): Maintains increasing order of low values; front always holds the current minimum
- **Circular buffer** + running sum for SMA(%K → %D)

Bar correction (`isNew=false`) triggers deque rebuild from the circular buffer, ensuring correct state without allocation.

### State Management

```
State record struct:
  DSum        — running sum of %K values in the SMA window
  DHead       — circular buffer head index for %D SMA
  PrevDVal    — previous buffer value at DHead (for rollback)
  LastValidHigh/Low/Close — NaN/Infinity protection
```

The standard `_s` / `_ps` pattern enables bar correction:
- `isNew=true`: `_ps = _s`, advance index/count
- `isNew=false`: `_s = _ps`, recalculate from previous state

### Batch Path

Static `Batch()` methods use `Highest.Batch()` and `Lowest.Batch()` for vectorized min/max computation, with `ArrayPool` for buffers exceeding 256 elements and `stackalloc` for smaller inputs.

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `kLength` | int | 14 | ≥ 1 | Lookback period for highest high / lowest low |
| `dPeriod` | int | 3 | ≥ 1 | SMA smoothing period for %D signal line |

## Performance Profile

| Metric | Value |
|--------|-------|
| Time complexity (streaming) | O(1) amortized per bar |
| Time complexity (batch) | O(n) |
| Space complexity | O(kLength + dPeriod) |
| Warmup period | kLength bars |
| Output range | 0–100 (both %K and %D) |

## Interpretation

### Overbought / Oversold

| Zone | %K Level | Interpretation |
|------|----------|----------------|
| Overbought | > 80 | Price near top of range — potential reversal |
| Neutral | 20–80 | Normal trading range |
| Oversold | < 20 | Price near bottom of range — potential reversal |

### Signal Patterns

- **%K/%D Crossover**: Bullish when %K crosses above %D; bearish when %K crosses below %D
- **Divergence**: Price makes new highs/lows while Stochastic doesn't — potential reversal
- **Failure Swings**: %K reaches overbought/oversold then reverses before re-reaching the extreme
- **Hook**: Short-term reversal pattern when %K or %D hooks at extremes

### Fast vs Slow Stochastic

This implementation is the **Fast Stochastic** where:
- `%K` is the raw (unsmoothed) oscillator
- `%D` is the SMA of `%K`

The "Slow Stochastic" smooths both lines: Slow %K = SMA(Fast %K), Slow %D = SMA(Slow %K). Use this implementation with a separate SMA wrapper if slow smoothing is desired.

## Validation

| Library | Match | Notes |
|---------|-------|-------|
| Skender | ✔️ | Via `GetStoch(kLength, dPeriod, smoothPeriods=1)` — smoothPeriods=1 produces Fast %K |

## Common Pitfalls

1. **Zero range**: When all bars in the window have identical H/L, range = 0 and %K = 0 (not 50 or NaN)
2. **Fast vs Slow confusion**: Many platforms default to "Slow Stochastic"; this indicator outputs Fast %K
3. **Overbought ≠ sell signal**: In strong trends, %K can stay above 80 for extended periods
4. **Short lookback noise**: kLength < 5 creates excessive whipsaws in volatile markets
5. **SMA warmup for %D**: The first dPeriod bars use the PineScript convention of filling the SMA buffer with the first %K value, not NaN

## References

- Lane, G. C. (1984). "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*
- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance
- Achelis, S. B. (2000). *Technical Analysis from A to Z*. McGraw-Hill
- [TradingView Stochastic](https://www.tradingview.com/support/solutions/43000502332/)
- [StockCharts Stochastic Oscillator](https://school.stockcharts.com/doku.php?id=technical_indicators:stochastic_oscillator_fast_slow_and_full)
