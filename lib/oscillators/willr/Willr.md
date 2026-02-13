# WILLR: Williams %R

> "The market tells you where it closed relative to where it traded. That single fact contains more information than most traders realize." -- George Lane (on the principle shared with Williams %R)

## Overview

Williams %R measures where the closing price sits within the highest-high to lowest-low range over a lookback period, scaled to \(-100, 0\). It is the arithmetic inverse of the Fast Stochastic %K: identical math, different scale. A reading near 0 means the close is near the period high; a reading near \(-100\) means the close is near the period low.

Default period: 14 bars. Output range: \(-100\) to \(0\). Warmup: `period` bars.

## Historical Context

Larry Williams introduced Williams %R in his 1973 book *How I Made One Million Dollars Last Year Trading Commodities*. The indicator predates widespread computerized trading and was designed for quick manual calculation: find the highest high, find the lowest low, see where the close falls in that range.

Williams %R and the Stochastic Oscillator (George Lane, late 1950s) share the same core logic. The only difference is the output mapping:

$$\text{Stoch \%K} = 100 \times \frac{C - LL}{HH - LL}, \quad \text{Williams \%R} = -100 \times \frac{HH - C}{HH - LL}$$

This means $\text{Williams \%R} = \text{Stoch \%K} - 100$. The inverted scale places "overbought" at the top (near 0) and "oversold" at the bottom (near \(-100\)), which some traders find more intuitive for spotting reversals.

## Architecture and Physics

### 1. Streaming Path (O(1) Amortized)

The streaming implementation uses **MonotonicDeque** pairs for O(1) amortized highest-high and lowest-low tracking over the sliding window:

- **MonotonicDeque (max)**: Maintains decreasing order of high values. Front always holds the current window maximum.
- **MonotonicDeque (min)**: Maintains increasing order of low values. Front always holds the current window minimum.
- **Circular buffers** (`_hBuf`, `_lBuf`): Store raw high/low values for deque rebuild on bar correction.

Bar correction (`isNew=false`) triggers a full deque rebuild from the circular buffer, restoring correct state without allocation.

### 2. State Management

```text
State record struct:
  LastValidHigh   -- NaN/Infinity protection for high
  LastValidLow    -- NaN/Infinity protection for low
  LastValidClose  -- NaN/Infinity protection for close
```

The standard `_s` / `_ps` pattern enables bar correction:

- `isNew=true`: `_ps = _s`, advance index/count
- `isNew=false`: `_s = _ps`, recalculate from previous valid state

### 3. Batch Path

Static `Batch()` methods delegate to `Highest.Batch()` and `Lowest.Batch()` for vectorized min/max computation over the full series. Intermediate buffers use `stackalloc` for inputs up to 256 elements and `ArrayPool<double>` for larger inputs.

### 4. Edge Case: Zero Range

When $HH = LL$ (all bars in the window have identical high and low), the range is zero and division is undefined. The implementation returns $-50$ (midpoint of the \(-100, 0\) scale). This differs from the Stochastic Oscillator, which returns $0$ for zero range.

## Mathematical Foundation

### Core Formula

$$\text{Williams \%R} = -100 \times \frac{HH_n - C}{HH_n - LL_n}$$

Where:

- $HH_n = \max(H_i)$ for $i \in [t - n + 1, \, t]$
- $LL_n = \min(L_i)$ for $i \in [t - n + 1, \, t]$
- $C$ = current close price
- $n$ = lookback period (default 14)

### Relationship to Stochastic

$$\text{Williams \%R} = \text{Stoch \%K} - 100$$

Proof:

$$\text{Stoch \%K} = 100 \times \frac{C - LL}{HH - LL}$$

$$\text{Williams \%R} = -100 \times \frac{HH - C}{HH - LL} = -100 \times \frac{(HH - LL) - (C - LL)}{HH - LL}$$

$$= -100 + 100 \times \frac{C - LL}{HH - LL} = \text{Stoch \%K} - 100$$

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $n$ | 14 | $n \geq 1$ |

## Performance Profile

| Metric | Value |
|--------|-------|
| Time complexity (streaming) | O(1) amortized per bar |
| Time complexity (batch) | O(n) total |
| Space complexity | O(period) |
| Warmup period | `period` bars |
| Output range | \(-100\) to \(0\) |
| Allocations in `Update()` | Zero |

### Operation Count (per bar, streaming)

| Operation | Count |
|-----------|-------|
| Comparisons | 2-3 (deque push) |
| Divisions | 1 |
| Multiplications | 1 |
| NaN checks | 3 (high, low, close) |

### Quality Metrics

| Metric | Score (1-10) |
|--------|-------------|
| Noise rejection | 3 |
| Lag | 2 (minimal) |
| Sensitivity | 8 |
| Computational cost | 2 (very cheap) |
| Implementation complexity | 3 |

## Interpretation

### Overbought / Oversold Zones

| Zone | Williams %R Level | Interpretation |
|------|-------------------|----------------|
| Overbought | > \(-20\) | Close near period high. Potential reversal down. |
| Neutral | \(-80\) to \(-20\) | Normal trading range. |
| Oversold | < \(-80\) | Close near period low. Potential reversal up. |

### Signal Patterns

- **Overbought reversal**: %R rises above \(-20\) then drops back below. Bearish signal.
- **Oversold reversal**: %R falls below \(-80\) then rises back above. Bullish signal.
- **Divergence**: Price makes new highs while %R does not (or vice versa). Potential trend exhaustion.
- **Failure swing**: %R reaches an extreme, pulls back, fails to re-reach the extreme, then reverses. Stronger signal than simple crossover.

### Practical Notes

In strong uptrends, Williams %R can remain above \(-20\) for extended periods. Treating every overbought reading as a sell signal in a bull market is a reliable way to underperform. Use trend filters (ADX, moving average slope) to contextualize overbought/oversold readings.

## Validation

| Library | Match | Notes |
|---------|-------|-------|
| Skender | ✔️ | `GetWilliamsR(lookbackPeriods)` -- `WilliamsR` property |
| TA-Lib | ✔️ | `WillR(high, low, close, period)` |
| Tulip | ✔️ | `willr(high, low, close, period)` |
| Ooples | ❔ | Not validated |

All validated libraries agree within $1 \times 10^{-9}$ tolerance after warmup convergence.

## Common Pitfalls

1. **Inverted scale confusion**: Williams %R uses \(-100\) to \(0\), not 0 to 100. Overbought is near 0, oversold is near \(-100\). Reversing the mental model from Stochastic is the most common mistake.

2. **Zero range returns \(-50\)**: When all bars in the window share the same high and low (e.g., constant-price instruments), the range is zero. This implementation returns \(-50\) (midpoint). Other implementations may return 0 or NaN.

3. **Overbought does not equal sell**: In trending markets, %R stays overbought/oversold for long stretches. Fading the trend based solely on %R readings without a trend filter leads to significant drawdowns.

4. **Short lookback noise**: Period < 5 creates excessive whipsaws. The default 14 balances responsiveness and noise rejection for most timeframes.

5. **No signal line**: Unlike the Stochastic Oscillator, Williams %R traditionally has no %D signal line. Traders who want smoothed crossover signals should either use Stochastic or apply a separate SMA to Williams %R output.

6. **NaN propagation**: If the first bar contains NaN for all OHLC fields, the output is NaN until valid data arrives. After the first valid bar, subsequent NaN inputs are replaced with the last valid value.

7. **Bar correction with deque rebuild**: Correcting a bar (`isNew=false`) triggers a full deque rebuild from the circular buffer. This is O(period) worst case, not O(1). In practice this is negligible since bar corrections are infrequent, but batch-correcting thousands of bars in a tight loop would show the cost.

## References

- Williams, L. (1973). *How I Made One Million Dollars Last Year Trading Commodities*. Windsor Books.
- Lane, G. C. (1984). "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*.
- Murphy, J. J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
- Achelis, S. B. (2000). *Technical Analysis from A to Z*. McGraw-Hill.
- [TradingView Williams %R](https://www.tradingview.com/support/solutions/43000502218/)
- [StockCharts Williams %R](https://school.stockcharts.com/doku.php?id=technical_indicators:williams_r)
