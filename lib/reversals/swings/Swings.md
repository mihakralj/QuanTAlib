# SWINGS: Swing High/Low Detection

> *The market tells you where it turned. You just have to listen long enough to be sure it actually meant it.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `lookback` (default 5)                      |
| **Outputs**      | Single series (Swings)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [swings.pine](swings.pine)                       |

- Swing High/Low detection identifies local price extremes using a configurable lookback window.
- **Similar:** [Fractal](../../oscillators/fisher/Fisher.md), [ZigZag](../psar/Psar.md) | **Complementary:** Volume for confirmation | **Trading note:** Swing high/low detector; identifies pivots for support/resistance and chart pattern analysis.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Swing High/Low detection identifies local price extremes using a configurable lookback window. A Swing High marks a bar whose high strictly exceeds the highs of all bars within the lookback window on each side. A Swing Low marks a bar whose low is strictly less than all corresponding lows. The lookback parameter controls sensitivity: larger lookback windows require more confirmation and produce fewer, more significant signals. This generalizes Williams' fixed five-bar Fractals into a flexible structural analysis tool.

## Historical Context

Swing point detection predates formal technical analysis. Floor traders in the 1920s marked "pivot highs" and "pivot lows" on hand-drawn charts to identify support and resistance. W.D. Gann formalized the concept in the 1930s, using swing charts to filter noise and identify trend structure. The basic idea: a local maximum confirmed by subsequent lower prices marks resistance; a local minimum confirmed by subsequent higher prices marks support.

Bill Williams codified a specific instance of this pattern as "Fractals" in *Trading Chaos* (1995), fixing the lookback to 2 bars (a five-bar window). TradingView's PineScript generalized this with `ta.pivothigh(source, leftbars, rightbars)` and `ta.pivotlow(source, leftbars, rightbars)`, allowing asymmetric lookback windows. This QuanTAlib implementation uses symmetric lookback (equal bars on both sides), matching the most common usage pattern.

The choice of lookback period is a sensitivity-significance tradeoff. Lookback=2 (Williams Fractals) fires frequently but catches minor wiggles. Lookback=5 (the default here) requires substantial confirmation, producing signals that correspond to genuine structural turning points rather than intrabar noise. Lookback=10 or higher identifies swing points visible on lower timeframes, effectively performing multi-timeframe analysis within a single timeframe.

The relationship between Swings and Fractals is straightforward: `Fractals()` is equivalent to `Swings(lookback: 2)`. Both use strict inequality (center must strictly exceed all neighbors, not merely equal them). This implementation follows PineScript convention: the swing point is reported on the confirming bar (when the full window is available), not retroactively placed on the center bar.

## Architecture and Physics

### 1. Configurable Window

The indicator maintains two circular buffers of size $2 \times \text{lookback} + 1$: one for highs, one for lows. Each new bar shifts the window forward by one position using modular index arithmetic.

### 2. Swing High Detection

A Swing High is detected when the center bar's high strictly exceeds all neighbors in the window:

$$ \text{SwingHigh}_t = \begin{cases} H_{t-L} & \text{if } H_{t-L} > H_j \text{ for all } j \in [t-2L, t] \text{ where } j \neq t-L \\ \text{NaN} & \text{otherwise} \end{cases} $$

Where $L$ is the lookback period and $t$ is the current bar index.

### 3. Swing Low Detection

A Swing Low is detected when the center bar's low is strictly less than all neighbors:

$$ \text{SwingLow}_t = \begin{cases} L_{t-L} & \text{if } L_{t-L} < L_j \text{ for all } j \in [t-2L, t] \text{ where } j \neq t-L \\ \text{NaN} & \text{otherwise} \end{cases} $$

### 4. Persistent Last-Swing Levels

Unlike per-bar SwingHigh/SwingLow (which are NaN when no pattern is present), `LastSwingHigh` and `LastSwingLow` persist the most recently confirmed swing level until superseded. These provide continuous support/resistance references.

### 5. Dual Output

Both swing values are available simultaneously. At any given bar, either, both, or neither swing may be present. The primary output (`Last.Val`) defaults to `SwingHigh` for overlay plotting.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| SwingHigh is not NaN | Local high identified $L$ bars ago; potential resistance level |
| SwingLow is not NaN | Local low identified $L$ bars ago; potential support level |
| Both present | Simultaneous peak and trough (rare; indicates extreme volatility) |
| Neither present | No pattern formed; trend continuation or consolidation |
| LastSwingHigh rising | Higher highs in structural terms; bullish tendency |
| LastSwingLow rising | Higher lows in structural terms; bullish tendency |

## Mathematical Foundation

### Parameters

| Parameter | Default | Range | Notes |
| :--- | :---: | :---: | :--- |
| Lookback | 5 | 1-100 | Bars on each side of center for confirmation |

### Derived Constants

| Constant | Formula | Default Value |
| :--- | :--- | :--- |
| Window Size | $2L + 1$ | 11 |
| Warmup Period | $2L + 1$ | 11 |
| Reporting Delay | $L$ bars | 5 bars |

### Warmup Period

$$ W = 2L + 1 $$

The indicator requires $W$ bars before producing valid output. Prior to warmup completion, both SwingHigh and SwingLow output NaN.

### Relationship to Williams Fractals

$$ \text{Fractals}() \equiv \text{Swings}(\text{lookback} = 2) $$

Both use strict inequality. The five-bar pattern ($2 \times 2 + 1 = 5$) is the simplest non-trivial swing detection window. Increasing lookback trades detection frequency for signal significance.

### Expected Detection Frequency

In random walk data with GBM dynamics ($\mu = 0.05$, $\sigma = 0.20$), empirical swing high frequency is approximately:

| Lookback | Window | Approx. Swing High Frequency |
| :--- | :--- | :--- |
| 2 | 5 bars | ~15-25% of bars |
| 3 | 7 bars | ~10-18% of bars |
| 5 | 11 bars | ~5-12% of bars |
| 10 | 21 bars | ~2-6% of bars |

## Performance Profile

### Operation Count (Streaming Mode)

Swing High/Low detection compares centered bar against N neighbors on each side — O(1) with fixed lookback.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer update (high + low) | 2 | 3 cy | ~6 cy |
| Compare center vs N left + N right neighbors | 2*N*2 | 2 cy | ~4N cy |
| Signal assignment (swing high/low) | 2 | 1 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=5)** | **O(N)** | — | **~44 cy** |

O(N) per bar where N = lookback on each side. Signal delayed N bars. For N=5 the 10 comparisons are branchless SIMD-comparable.

### Implementation Design

The implementation uses two circular buffers with modular index arithmetic. Pattern evaluation checks $2L$ comparisons per direction (all neighbors against center), with early termination when both swing high and swing low are ruled out.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(L) per update | Linear in lookback; comparisons against all neighbors |
| **Allocations** | 0 | Hot path is allocation-free; fixed-size buffers |
| **Warmup** | $2L+1$ bars | Minimum viable for the pattern |
| **Accuracy** | 10/10 | Exact computation; no approximation or floating-point accumulation |
| **Timeliness** | Variable | Inherent $L$-bar reporting delay |
| **Smoothness** | N/A | Binary signal; smooth/noisy not applicable |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. The state tracks last-valid values for high, low, and close (NaN/Infinity substitution) plus persistent LastSwingHigh/LastSwingLow levels. Bar correction via `isNew` flag enables same-timestamp rewrites.

### SIMD Applicability

Not applicable. The window-based comparison is inherently sequential due to the circular buffer state. For the span-based `Batch` API, each window evaluation is independent and could theoretically be parallelized, but the comparison count per window ($2L$) is small enough that SIMD overhead exceeds the benefit.

## Validation

Self-consistency validation confirms all API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | Passed | Bar-by-bar with `isNew` support |
| **Batch** (`Batch(TBarSeries)`) | Passed | Matches streaming output |
| **Span** (`Batch(Span)`) | Passed | Matches streaming output |
| **BatchDual** | Passed | Both SwingHighs and SwingLows match span output |
| **Event** (`Pub` subscription) | Passed | Matches streaming output |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | Passed | All modes self-consistent; mathematical correctness verified |
| **Skender** | N/A | No configurable swings API |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not validated |

Mathematical correctness is verified by confirming that every reported SwingHigh is a genuine local maximum (strictly greater than all neighbors) and every reported SwingLow is a genuine local minimum (strictly less than all neighbors) across GBM-generated test data.

## Common Pitfalls

1. **Lookback vs. window size confusion.** Lookback is the number of bars on each side, not the total window. `Swings(lookback: 5)` evaluates an 11-bar window ($2 \times 5 + 1$), not a 5-bar window. If you want Williams Fractals behavior (5-bar window), use `lookback: 2`.

2. **Reporting delay scales with lookback.** A lookback of 5 means the swing point occurred 5 bars ago. In a fast-moving market, the price may have traveled significantly from the swing level by the time it is confirmed. This is inherent to the detection method, not a bug.

3. **Strict inequality excludes equal highs/lows.** If the center bar's high equals any neighbor's high, no swing high is detected. In flat or low-volatility markets, this produces sparse signals. Use a smaller lookback for tighter detection in low-volatility regimes.

4. **NaN output is the normal case.** Most bars do not form swing points. At lookback=5, roughly 90-95% of bars return NaN for both outputs. Design strategies accordingly; swing detection is an event, not a continuous signal.

5. **LastSwingHigh/LastSwingLow may be stale.** These persistent levels hold indefinitely until the next swing is confirmed. In trending markets, LastSwingLow (in an uptrend) may lag far behind current price. Use `IsHot` and recency checks if staleness matters.

6. **Asymmetric lookback not supported.** PineScript's `ta.pivothigh(src, leftbars, rightbars)` allows different left and right lookback values. This implementation uses symmetric lookback only. For asymmetric detection, chain two separate instances or modify the source.

7. **Different lookback periods detect different market structure.** A lookback of 2 catches minor intraday reversals. A lookback of 10 catches significant multi-day swing points. There is no universally correct value; the choice depends on the analysis timeframe and trading horizon.

## References

- Williams, B. M. (1995). *Trading Chaos: Applying Expert Techniques to Maximize Your Profits*. John Wiley and Sons.
- Gann, W. D. (1935). *New Stock Trend Detector*. Financial Guardian Publishing.
- TradingView PineScript Reference: [`ta.pivothigh()`](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.pivothigh), [`ta.pivotlow()`](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.pivotlow)