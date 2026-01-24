# BBANDS: Bollinger Bands

> "In markets, as in everything else, adaptation is the law of survival. Bollinger Bands adapt to volatility, expand during turbulence, and contract during calm—a visual representation of market uncertainty."

Bollinger Bands (BBands) stand as one of the most ubiquitous volatility indicators in technical analysis, yet most implementations settle for the textbook formula without addressing the nuances that matter in production systems: NaN handling, streaming updates, bar corrections, and computational efficiency. This implementation delivers the canonical three-band system while maintaining O(1) streaming performance and zero-allocation hot paths.

## Historical Context

John Bollinger introduced Bollinger Bands in the early 1980s, publishing the methodology broadly in the 1990s and formalizing it in his 2001 book "Bollinger on Bollinger Bands." Unlike earlier fixed-percentage bands, Bollinger's innovation was to anchor band width to standard deviation—making the indicator adaptive to volatility regimes rather than assuming constant market behavior.

The original formulation is deceptively simple: a simple moving average (middle band) with upper and lower bands positioned at ±N standard deviations. Most implementations use N=2 (the default) based on statistical properties of normal distributions, where roughly 95% of observations fall within two standard deviations of the mean. However, financial returns are decidedly non-normal, making this more of a heuristic than a theoretical guarantee.

What distinguishes production-grade implementations from textbook examples is handling edge cases that real data presents: intrabar corrections (when a bar's OHLC values update before the bar closes), NaN values from data gaps or suspended trading, and the efficiency demands of processing thousands of symbols in real-time. This implementation addresses all three while maintaining exact parity with established libraries (TA-Lib, Skender, Tulip) across batch, streaming, and span-based calculation modes.

## Architecture & Physics

Bollinger Bands consist of three components operating in concert, each with distinct responsibilities and failure modes:

### 1. Middle Band (Simple Moving Average)

The foundation is a simple moving average over the lookback period:

$$
\text{SMA}_t = \frac{1}{n} \sum_{i=t-n+1}^{t} P_i
$$

where $P_i$ represents the input price (typically close, but configurable) and $n$ is the period. This serves as the baseline reference—the "fair value" estimate around which bands expand and contract.

**Implementation note:** We delegate to the `Sma` class rather than reimplementing the running sum, ensuring consistent behavior across indicators. The SMA handles NaN substitution internally, replacing non-finite values with the last valid observation.

### 2. Standard Deviation Calculation

The band width is determined by the sample standard deviation over the same period:

$$
\sigma_t = \sqrt{\frac{1}{n} \sum_{i=t-n+1}^{t} (P_i - \text{SMA}_t)^2}
$$

This is the population standard deviation formula (dividing by $n$ rather than $n-1$), matching the behavior of TA-Lib and most financial software. While statisticians prefer the unbiased estimator (Bessel's correction), the difference is negligible for typical periods (≥10) and consistency with established implementations takes priority.

**Numerical stability:** We compute variance as $E[X^2] - (E[X])^2$ rather than the two-pass definition, avoiding the catastrophic cancellation that can occur when mean and data are similar magnitudes. The `Math.Max(0.0, variance)` guard prevents negative variance from floating-point rounding errors.

### 3. Upper and Lower Bands

The bands extend symmetrically from the middle:

$$
\text{Upper}_t = \text{SMA}_t + k \cdot \sigma_t
$$

$$
\text{Lower}_t = \text{SMA}_t - k \cdot \sigma_t
$$

where $k$ is the multiplier parameter (default 2.0). The multiplier controls band sensitivity: higher values produce wider bands (fewer signals, less noise), lower values produce tighter bands (more signals, more whipsaws).

### 4. Derived Metrics

The implementation provides two additional outputs that extend Bollinger's original work:

**Band Width:**
$$
\text{Width}_t = \text{Upper}_t - \text{Lower}_t = 2k\sigma_t
$$

This metric isolates volatility from price level, useful for detecting "squeeze" setups where volatility contracts before directional moves.

**Percent B (%B):**
$$
\%B_t = \frac{P_t - \text{Lower}_t}{\text{Upper}_t - \text{Lower}_t}
$$

This normalizes price position within the bands to [0, 1] (though it can exceed these bounds when price moves beyond the bands). Values near 0 indicate price at the lower band; near 1 indicates upper band. We guard against division by zero when `Width` approaches machine epsilon.

## Mathematical Foundation

### Running Calculation (Streaming Mode)

For streaming updates, we maintain two indicator instances internally:

- `Sma` instance with period $n$
- `Stdev` instance with period $n$

Each incoming value $P_t$ updates both instances in O(1) time:

1. **NaN Handling:**

   ```csharp
   double finiteValue = double.IsFinite(input.Value) ? input.Value : Middle.Value;
   ```

   Non-finite inputs (NaN, ±Infinity) are replaced with the current middle band value, preventing error propagation.

2. **Component Updates:**

   ```csharp
   TValue smaValue = _sma.Update(new TValue(input.Time, finiteValue), isNew);
   TValue stdevValue = _stdev.Update(new TValue(input.Time, finiteValue), isNew);
   ```

3. **Band Calculation:**

   ```csharp
   double offset = _multiplier * stdDev;
   double upper = middle + offset;
   double lower = middle - offset;
   ```

4. **Derived Metrics:**

   ```csharp
   double width = upper - lower;
   double percentB = (width > double.Epsilon) ? (finiteValue - lower) / width : 0.0;
   ```

### Bar Correction Protocol

The `isNew` parameter controls whether a bar update advances the history or modifies the current bar:

- `isNew = true`: Advance to new bar, shift window, incorporate new data
- `isNew = false`: Replace current bar's value, recalculate without advancing

Both `Sma` and `Stdev` support this protocol natively, maintaining previous state (`_p_state`) to enable rollback. This is critical for real-time applications where the most recent bar's OHLC values update continuously until the bar closes.

### Batch Calculation (Span Mode)

For bulk processing, the span-based `Calculate` method operates in two passes:

#### Pass 1: SMA Calculation

```csharp
Sma.Calculate(source, middle, period);
```

#### Pass 2: Standard Deviation and Bands

For each index $i \geq n-1$:

```csharp
double sum = 0.0, sumSq = 0.0;
for (int j = i - period + 1; j <= i; j++) {
    double val = source[j];
    if (double.IsFinite(val)) {
        sum += val;
        sumSq += val * val;
    }
}
double variance = (sumSq / count) - (mean * mean);
variance = Math.Max(0.0, variance);
double stdDev = Math.Sqrt(variance);
upper[i] = middle[i] + multiplier * stdDev;
lower[i] = middle[i] - multiplier * stdDev;
```

This two-pass approach sacrifices the theoretical possibility of a single-pass variance calculation (Welford's algorithm) for clarity and maintainability. Modern CPUs execute both passes faster than the overhead of a more complex single-pass implementation would save.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per bar update with period $n$:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SMA update | 1 | ~15 | 15 |
| StdDev update | 1 | ~25 | 25 |
| MUL (offset) | 1 | 3 | 3 |
| ADD (upper) | 1 | 1 | 1 |
| SUB (lower, width) | 2 | 1 | 2 |
| DIV (%B) | 1 | 15 | 15 |
| CMP (epsilon guard) | 1 | 1 | 1 |
| **Total** | **~8 ops** | — | **~62 cycles** |

The dominant cost is the StdDev calculation (~25 cycles for running variance update). The total of ~62 cycles per bar assumes both SMA and StdDev maintain running state (no re-summation). For comparison, a naive re-scan approach would cost ~$3n$ cycles per bar for SMA + $5n$ cycles for variance, making streaming 80-90% faster for typical periods (n≥10).

### Batch Mode (512 values, Period=20)

The span-based `Calculate` method processes 512 bars with period=20:

**Pass 1 (SMA):**

- Warmup: 19 × 3 = 57 ops (initial window accumulation)
- Main loop: 493 × 3 = 1,479 ops (rolling sum updates)
- Subtotal: ~1,536 scalar operations

**Pass 2 (StdDev + Bands):**

- Per-bar cost: 20 × 3 (sum, sumSq accumulation) + 1 DIV + 1 SQRT + 2 MUL + 2 ADD = ~68 scalar ops
- 493 bars: 493 × 68 = 33,524 ops
- Subtotal: ~33,524 scalar operations

Total: ~35,060 scalar operations for 512 bars ≈ 68 ops/bar

**SIMD Applicability:**

- SMA pass can leverage `Vector<double>` for the running sum (4× speedup on AVX2)
- StdDev pass is inherently sequential due to windowed variance calculation
- Overall speedup: modest (~2× for the SMA portion, negligible for StdDev)

**SIMD/FMA optimization estimates:**

| Component | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| SMA calculation | 1,536 | ~384 | 4× |
| StdDev + Bands | 33,524 | 33,524 | 1× |

**Per-bar savings with SIMD:**

| Optimization | Cycles Saved | New Total |
| :--- | :---: | :---: |
| SMA vectorization | ~1,152 ops | ~33,908 ops |
| **Total improvement** | **~3%** | **~66 ops/bar** |

The modest SIMD benefit reflects the sequential nature of standard deviation over sliding windows. For indicators where variance is cheap (e.g., exponentially weighted), SIMD offers larger gains.

**Batch efficiency (512 bars, period=20):**

| Mode | Ops/bar | Total (512 bars) | Overhead |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 62 | 31,744 | — |
| Scalar batch | 68 | 34,816 | +10% |
| SIMD batch | 66 | 33,792 | +6% |
| **Improvement (batch)** | **+6%** | — | — |

Batch mode adds ~10% overhead from the two-pass design, but SIMD claws back 4%, landing at +6% total. The primary value of batch mode isn't speed—it's avoiding state management and enabling parallelization across multiple series.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib, Skender, Tulip to floating-point precision |
| **Timeliness** | 6/10 | Period/2 lag from SMA foundation; bands react to volatility faster than middle band reacts to trend |
| **Overshoot** | 8/10 | Minimal overshoot by design; bands expand/contract with volatility, not price direction |
| **Smoothness** | 7/10 | Inherits SMA smoothness; standard deviation adds slight jitter during choppy markets |
| **Adaptability** | 9/10 | Excels at volatility adaptation; band width responds immediately to changes in price dispersion |

## Validation

This implementation has been validated against four reference libraries using the NVIDIA dataset (2,517 daily bars):

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Exact match across all bands (middle, upper, lower) within 1e-8 tolerance |
| **Skender** | ✅ | Exact match for SMA, upper, lower, width, %B within 1e-8 tolerance |
| **Tulip** | ✅ | Exact match for all three bands within 1e-8 tolerance |
| **Ooples** | ✅ | Match within 1e-5 tolerance (lower precision typical of this library) |

**Validation scope:**

- **Batch mode:** All 2,517 bars calculated via span-based method
- **Streaming mode:** Incremental updates via `Update(TValue, isNew)`
- **Span mode:** Direct span-to-span calculation
- **Consistency check:** All three modes produce identical results for the final 100 bars

**Test dataset:** NVIDIA daily OHLC (2014-2023), chosen for:

- Sufficient length (2,517 bars) to test warmup and steady-state behavior
- Multiple volatility regimes (2018 correction, 2020 COVID crash, 2021-2023 AI boom)
- No gaps or halts that would inject NaN handling complexity
- Well-established reference values from widely-used libraries

## Common Pitfalls

1. **Warmup Period Awareness**: BBands requires $n$ bars before producing valid output. For $n=20$, the first 19 bars return NaN (or uninitialized values in unsafe implementations). `IsHot` transitions to `true` at bar 20.

   **Formula:**
   $$
   \text{WarmupPeriod} = n
   $$

   **Impact:** Attempting to trade on early bars produces undefined behavior. Always check `IsHot` before using indicator values in production.

2. **Multiplier Confusion**: The multiplier parameter ($k$) is often conflated with "number of standard deviations," but it's a direct scaling factor. $k=2$ means bands are positioned at exactly $\pm 2\sigma$, not approximately. Other indicators (Keltner Channels) use similar syntax but measure ATR instead of standard deviation—don't assume equivalence.

3. **Standard Deviation Formula Variant**: Financial software uses population standard deviation ($\sigma = \sqrt{\frac{1}{n}\sum(x_i - \mu)^2}$) rather than sample standard deviation ($s = \sqrt{\frac{1}{n-1}\sum(x_i - \bar{x})^2}$). The difference is negligible for $n \geq 20$ but can cause 5-10% discrepancies for small periods. This implementation matches TA-Lib/Skender convention (population formula).

4. **Computational Cost**: While streaming updates are O(1), batch recalculation is O(n²) in the naive implementation and O(n) with running statistics. For 10,000 bars with period=50, this translates to 500k operations (naive) vs 10k operations (optimized). Use streaming mode for real-time applications; batch mode for historical analysis.

   **Batch cost estimate:**
   $$
   \text{Total ops} \approx L \times (3 + 3n)
   $$
   where $L$ is series length, 3 ops for rolling sum, $3n$ ops for variance window scan. For $L=10000$, $n=50$: ~1.5M operations, or ~150 ops/bar.

5. **Memory Footprint**: Each BBands instance maintains two sub-indicators (SMA + StdDev), each storing a `RingBuffer` of size $n$. Total memory per instance:
   $$
   \text{Memory} \approx 2 \times (n \times 16\text{ bytes}) + \text{overhead} \approx 32n + 200\text{ bytes}
   $$

   For $n=20$: ~840 bytes/instance. For 1000 symbols: ~820 KB. Negligible for most applications, but beware of over-parameterization (running 10 BBands instances per symbol with varying periods adds up).

6. **Edge Case: Zero Volatility**: When all values in the window are identical, $\sigma=0$ and bands collapse to the SMA line. This is mathematically correct but visually confusing. The `Width` output makes this condition explicit. %B becomes undefined (0/0); we return 0.0 by convention when `Width < epsilon`.

7. **API Usage (isNew parameter)**: Forgetting `isNew=false` for bar updates (as opposed to new bars) corrupts state. Always pair intrabar updates with `isNew=false`:

   ```csharp
   // Correct
   bbands.Update(openTick, isNew: true);   // New bar
   bbands.Update(updateTick, isNew: false); // Same bar update
   bbands.Update(closeTick, isNew: false);  // Bar close

   // Wrong
   bbands.Update(openTick, isNew: true);
   bbands.Update(updateTick, isNew: true);  // This starts a NEW bar
   ```

## References

- Bollinger, John. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill.
- Bollinger, John. (1992). "Using Bollinger Bands." *Stocks & Commodities*, V. 10:2 (47-51).
- [Official Bollinger Bands website](https://www.bollingerbands.com/)
- [TA-Lib documentation](https://ta-lib.org/function.html?name=BBANDS)
- [Skender Stock Indicators](https://dotnet.stockindicators.dev/indicators/BollingerBands/)
