# FRACTALS: Williams Fractals

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (FRACTALS)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [fractals.pine](fractals.pine)                       |

- Williams Fractals detect local price extremes using a strict five-bar pattern: an Up Fractal marks a bar whose high exceeds the highs of the two ba...
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Markets leave fingerprints at their turning points. Five bars is all it takes to read them."

Williams Fractals detect local price extremes using a strict five-bar pattern: an Up Fractal marks a bar whose high exceeds the highs of the two bars before and after it; a Down Fractal marks a bar whose low undercuts the lows of the two bars before and after it. No parameters, no smoothing, no lag compensation. The pattern either exists or it does not. Developed by Bill Williams and published in *Trading Chaos* (1995).

## Historical Context

Bill Williams introduced fractals as part of his "Trading Chaos" methodology in the mid-1990s, drawing loosely on Benoit Mandelbrot's fractal geometry. The connection to actual mathematical fractals is tenuous at best. Mandelbrot's fractals describe self-similar structures across scales; Williams' fractals are fixed five-bar patterns. The naming was marketing, not mathematics.

That said, the underlying observation is sound. Local extremes in price data correspond to temporary exhaustion of buying or selling pressure. A high that exceeds both its immediate predecessors and successors represents a point where bulls pushed price to a local maximum and then retreated. The five-bar window is the minimum viable detection size: two bars of context on each side of the pivot bar.

Williams originally used fractals as entry signals within his Alligator trading system: buy above an Up Fractal, sell below a Down Fractal, but only when the Alligator's jaws/teeth/lips confirm the trend direction. In isolation, fractals produce many signals. Combined with trend filters, they become structural support/resistance markers.

The indicator is closely related to Swing High/Low detection (which uses configurable lookback periods) and Fractal Chaos Bands (FCB, which draws upper/lower bands from the most recent fractal highs/lows). Where Swings offer flexibility via adjustable window size, Fractals commit to the five-bar pattern. Where FCB extends fractals into a channel overlay, Fractals provides the raw detection layer.

Most implementations report the fractal on the center bar (bar[2] in a 0-indexed five-bar window). This creates an inherent two-bar reporting delay: you cannot confirm a fractal until two bars after the pivot bar completes. This QuanTAlib implementation reports the fractal value on the confirming bar (bar[0]), not the pivot bar, matching TradingView/PineScript convention.

## Architecture and Physics

The computation is a pure pattern match with no recursive state:

### 1. Five-Bar Window

The indicator maintains a five-element circular buffer for highs and a five-element circular buffer for lows. Each new bar shifts the window forward by one position.

### 2. Up Fractal Detection

An Up Fractal is detected when the center bar's high strictly exceeds all four neighbors:

$$ \text{UpFractal}_t = \begin{cases} H_{t-2} & \text{if } H_{t-2} > H_{t-4} \text{ and } H_{t-2} > H_{t-3} \text{ and } H_{t-2} > H_{t-1} \text{ and } H_{t-2} > H_{t} \\ \text{NaN} & \text{otherwise} \end{cases} $$

Where $t$ is the current bar index and $H_{t-2}$ represents the high of the center (pivot) bar.

### 3. Down Fractal Detection

A Down Fractal is detected when the center bar's low is strictly less than all four neighbors:

$$ \text{DownFractal}_t = \begin{cases} L_{t-2} & \text{if } L_{t-2} < L_{t-4} \text{ and } L_{t-2} < L_{t-3} \text{ and } L_{t-2} < L_{t-1} \text{ and } L_{t-2} < L_{t} \\ \text{NaN} & \text{otherwise} \end{cases} $$

### 4. Dual Output

Both fractal values are available simultaneously. At any given bar, either, both, or neither fractal may be present. The primary output (`Last.Val`) defaults to `UpFractal` when present; the `DownFractal` is always accessible via the `DownFractal` property.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| UpFractal is not NaN | Local high identified two bars ago; potential resistance level |
| DownFractal is not NaN | Local low identified two bars ago; potential support level |
| Both present | Simultaneous peak and trough (possible inside bar patterns nearby) |
| Neither present | No five-bar pattern formed; trend continuation likely |
| Consecutive Up Fractals rising | Higher highs in local structure; bullish tendency |
| Consecutive Down Fractals rising | Higher lows in local structure; bullish tendency |

## Mathematical Foundation

### Parameters

Williams Fractals has no configurable parameters. The five-bar window is fixed by definition.

| Parameter | Value | Notes |
| :--- | :---: | :--- |
| Window size | 5 | Fixed; 2 bars before + pivot + 2 bars after |
| Comparison | Strict inequality | Pivot must strictly exceed (not equal) all neighbors |

### Warmup Period

$$ W = 5 $$

The indicator requires exactly 5 bars before producing valid output. Prior to that, both UpFractal and DownFractal output NaN.

### Comparison to Configurable Swings

Williams Fractals is equivalent to `Swings(period=2)` where the pivot bar must exceed exactly 2 bars on each side. Increasing the period to $n$ generalizes the pattern to $(2n+1)$-bar fractals, which is what the Swings indicator provides. The fixed five-bar pattern was chosen because it balances detection sensitivity against false positives in typical daily equity data.

## Performance Profile

### Operation Count (Streaming Mode)

Williams Fractals compare bar[i] high/low against 2 neighbors on each side — O(1) fixed 5-bar lookback.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer update (high + low) | 2 | 3 cy | ~6 cy |
| Compare center high against 4 neighbors | 4 | 2 cy | ~8 cy |
| Compare center low against 4 neighbors | 4 | 2 cy | ~8 cy |
| Output fractal up/down signals | 2 | 1 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~26 cy** |

O(1) constant-width 5-bar window. Signal is delayed 2 bars (confirmed only when later bars are available). No warm-up needed beyond 5 bars.

### Implementation Design

The implementation uses two five-element circular buffers (highs and lows) with index arithmetic. No sorting, no searching, no auxiliary data structures. The pattern check is four comparisons per fractal direction, evaluated only when the buffer is full.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(1) | Fixed 4 comparisons per direction; no loops |
| **Allocations** | 0 | Hot path is allocation-free; fixed-size buffers |
| **Warmup** | 5 bars | Minimum viable for the pattern |
| **Accuracy** | 10/10 | Exact match with Skender at precision 6 (decimal to double) |
| **Timeliness** | 5/10 | Inherent 2-bar reporting delay by definition |
| **Smoothness** | N/A | Binary signal; smooth/noisy not applicable |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. The state tracks last-valid values for high, low, and close to handle NaN/Infinity input substitution. Bar correction via `isNew` flag enables same-timestamp rewrites without state corruption.

### SIMD Applicability

Not applicable. The five-bar window is too small (5 elements) to benefit from SIMD vectorization. The comparison logic is branchy by nature and cannot be meaningfully parallelized. The Batch span API processes multiple bars but each bar requires sequential buffer state.

## Validation

Self-consistency validation confirms all API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | Passed | Bar-by-bar with `isNew` support |
| **Batch** (`Batch(TBarSeries)`) | Passed | Matches streaming output |
| **Span** (`Batch(Span)`) | Passed | Matches streaming output |
| **Event** (`Pub` subscription) | Passed | Matches streaming output |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | Passed | All modes self-consistent |
| **Skender** | Passed | Matches via `GetFractal(2)` at precision 6 (decimal-to-double rounding) |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not validated |

Cross-validation with Skender.Stock.Indicators uses `GetFractal(windowSpan: 2)`. Skender names the outputs `FractalBear` (high-point fractal, our UpFractal) and `FractalBull` (low-point fractal, our DownFractal). Skender reports the fractal on the pivot bar itself; QuanTAlib reports on the confirming bar (2 bars later). Tolerance of $10^{-6}$ accounts for Skender's `decimal` to QuanTAlib's `double` conversion.

## Common Pitfalls

1. **Naming confusion with Skender.** Skender calls the high-point fractal `FractalBear` (because it signals a bearish turning point) and the low-point fractal `FractalBull` (bullish turning point). QuanTAlib uses `UpFractal` (high was up) and `DownFractal` (low was down). Same data, opposite naming convention. When cross-validating, map `UpFractal` to `FractalBear` and `DownFractal` to `FractalBull`.

2. **Two-bar reporting offset.** QuanTAlib reports the fractal on the confirming bar (when all five bars of the pattern are available). Skender reports on the pivot bar itself (retroactively placing the value two bars back). When comparing arrays: `QuanTAlib[i]` corresponds to `Skender[i - 2]`.

3. **Strict inequality is non-negotiable.** If the pivot bar's high equals a neighbor's high, no Up Fractal is detected. This is Williams' original definition and matches PineScript. Some implementations use `>=`, which produces more signals but deviates from the standard.

4. **Most bars produce NaN.** In typical market data, fractals fire on roughly 15-25% of bars. The remaining 75-85% return NaN for both outputs. This is expected behavior, not a bug.

5. **Not a standalone trading signal.** Williams designed fractals as a component of his Alligator system. Using fractals in isolation generates excessive signals. Pair with trend filters (Alligator, moving averages, ADX) to filter for signals aligned with the prevailing trend.

6. **Decimal-to-double precision loss.** Skender returns `decimal?` values. Converting to `double` introduces rounding beyond the 15th significant digit. Validation tolerances of $10^{-6}$ accommodate this conversion. If you see differences only at the 7th decimal place, this is the cause.

7. **Equal highs/lows in flat markets.** In low-volatility or range-bound conditions with many equal price levels, fractals become sparse. This is correct behavior: the strict inequality filter prevents false signals from price congestion zones.

## References

- Williams, B. M. (1995). *Trading Chaos: Applying Expert Techniques to Maximize Your Profits*. John Wiley and Sons.
- Williams, B. M. (2004). *Trading Chaos: Maximize Profits with Proven Technical Techniques* (2nd ed.). John Wiley and Sons.
- Mandelbrot, B. B. (1982). *The Fractal Geometry of Nature*. W. H. Freeman.
- TradingView PineScript Reference: [`ta.pivothigh()`](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.pivothigh), [`ta.pivotlow()`](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.pivotlow)
- Skender.Stock.Indicators: [`GetFractal()`](https://dotnet.stockindicators.dev/indicators/Fractal/)
