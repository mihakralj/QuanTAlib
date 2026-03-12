# RSI: Relative Strength Index

> *Momentum is the premier anomaly.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Rsi)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [rsi.pine](rsi.pine)                       |

- The Relative Strength Index measures the speed and magnitude of price changes.
- Parameterized by `period` (default 14).
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Relative Strength Index measures the speed and magnitude of price changes. Introduced by J. Welles Wilder Jr. in 1978, it oscillates between 0 and 100, identifying overbought and oversold conditions. The "Relative Strength" name is misleading: RSI measures internal strength (price versus itself) not relative strength (asset versus benchmark). Wilder knew this. He kept the name anyway. Marketing, perhaps.

## Historical Context

Wilder introduced RSI in *New Concepts in Technical Trading Systems*, the same book that gave us ATR, ADX, and the Parabolic SAR. He was a mechanical engineer turned real estate developer turned trader. This background shows: RSI has the elegance of an engineered solution rather than the messiness of a discovered pattern.

The key insight: by normalizing gains against losses, RSI produces a bounded oscillator immune to the scale problems plaguing other momentum measures. A stock moving from $10 to $11 and a stock moving from $100 to $110 produce identical RSI readings. This scale independence made RSI the default momentum oscillator across every asset class from corn futures to cryptocurrency.

What most implementations get wrong: the smoothing method. Wilder specified RMA (Wilder's Moving Average, also called SMMA), not SMA or EMA. RMA has infinite memory. This gives RSI its characteristic "stickiness" near extremes.

## Architecture & Physics

RSI is built on three concepts: change classification, exponential smoothing, and normalization.

### 1. Change Classification

Each bar's price change is classified as either a gain or a loss:

$$
\text{Gain}_t = \max(P_t - P_{t-1}, 0)
$$

$$
\text{Loss}_t = \max(P_{t-1} - P_t, 0)
$$

A flat bar (no change) produces zero for both. This is correct: no movement means no momentum in either direction.

### 2. RMA Smoothing (Wilder's Method)

Gains and losses are smoothed separately using RMA:

$$
\text{AvgGain}_t = \frac{\text{AvgGain}_{t-1} \times (N-1) + \text{Gain}_t}{N}
$$

$$
\text{AvgLoss}_t = \frac{\text{AvgLoss}_{t-1} \times (N-1) + \text{Loss}_t}{N}
$$

RMA is an EMA variant with $\alpha = 1/N$ instead of $\alpha = 2/(N+1)$. The decay is slower, the memory longer. A 14-period RSI "remembers" price action from hundreds of bars ago, with exponentially diminishing influence.

### 3. Normalization to [0, 100]

The Relative Strength ratio and final normalization:

$$
RS = \frac{\text{AvgGain}}{\text{AvgLoss}}
$$

$$
RSI = 100 - \frac{100}{1 + RS}
$$

### 4. Edge Case Handling

When AvgLoss approaches zero (sustained uptrend):
- If AvgGain also near zero → RSI = 50 (no momentum either direction)
- If AvgGain positive → RSI = 100 (maximum bullish)

When AvgGain approaches zero (sustained downtrend):
- RSI approaches 0 (maximum bearish)

QuanTAlib uses $\epsilon = 10^{-10}$ as the zero threshold.

## Mathematical Foundation

### Transfer Function

RSI is a nonlinear function of two parallel RMA filters. The transfer function for each RMA:

$$
H_{RMA}(z) = \frac{\alpha}{1 - (1-\alpha)z^{-1}}
$$

where $\alpha = 1/N$.

The ratio and normalization introduce nonlinearity, preventing closed-form frequency analysis. RSI responds to both frequency and amplitude of price changes.

### Half-Life Analysis

For RMA with $\alpha = 1/N$:

$$
t_{1/2} = \frac{\ln(2)}{\ln(1/(1-\alpha))} \approx (N-1) \times \ln(2) \approx 0.693N
$$

A 14-period RSI has half-life of approximately 10 bars. Price action from 70 bars ago still contributes ~1% to the current reading.

### Warmup Period

RSI requires $N + 1$ bars minimum: one bar to establish the first change, then $N$ bars for the RMA to initialize. Full convergence (within 1% of steady-state) requires approximately $4.6N$ bars.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| SUB (change calculation) | 1 | 1 | 1 |
| CMP (gain/loss branch) | 2 | 1 | 2 |
| MUL (RMA decay × N-1) | 2 | 3 | 6 |
| ADD (RMA numerator) | 2 | 1 | 2 |
| DIV (RMA by N) | 2 | 15 | 30 |
| DIV (RS = gain/loss) | 1 | 15 | 15 |
| ADD (1 + RS) | 1 | 1 | 1 |
| DIV (100 / (1+RS)) | 1 | 15 | 15 |
| SUB (100 - result) | 1 | 1 | 1 |
| **Total** | **13** | — | **~73 cycles** |

The three divisions dominate (~82% of cycles). FMA optimization provides minimal benefit here.

### SIMD Analysis (Batch Mode)

The `Calculate` method vectorizes gain/loss classification:

| Operation | Scalar | SIMD (AVX2) | Speedup |
| :-------- | -----: | ----------: | ------: |
| Change calculation | N SUB | N/4 VSUBPD | 4× |
| Gain classification | N CMP + conditional | N/4 VMAXPD | 4× |
| RSI final calculation | N | N/4 VDIVPD | 4× |

The RMA smoothing remains sequential (recursive dependency).

### Benchmark Results

Test environment: Apple M4, .NET 10.0, AdvSIMD, 500,000 bars.

| Metric | Value | Notes |
| :----- | ----: | :---- |
| **Span throughput** | 18 ns/bar | Including SIMD gain/loss |
| **Streaming throughput** | ~25 ns/bar | Single `Update()` call |
| **Allocations (hot path)** | 0 bytes | ArrayPool for batch temps |
| **Complexity** | O(1) | Per bar |
| **State size** | ~80 bytes | Two RMA instances + prev value |

### Comparative Performance

| Library | Time (500K bars) | Allocated | Relative |
| :------ | ---------------: | --------: | :------- |
| **QuanTAlib (Span)** | ~9 ms | 0 B | baseline |
| TA-Lib | ~8 ms | 40 B | 0.89× |
| Tulip | ~8 ms | 0 B | 0.89× |
| Skender | ~95 ms | 28 MB | 10.6× slower |

### Quality Metrics

| Metric | Score | Notes |
| :----- | ----: | :---- |
| **Accuracy** | 10/10 | Matches Wilder's definition exactly |
| **Timeliness** | 8/10 | Responsive, but RMA adds some lag |
| **Overshoot** | 10/10 | Bounded [0, 100], cannot overshoot by design |
| **Smoothness** | 8/10 | RMA provides good filtering |

## Validation

Validated against external libraries in `Rsi.Validation.Tests.cs`. Tests run against 5,000 bars with tolerance of 1e-9.

| Library | Batch | Streaming | Span | Notes |
| :------ | :---: | :-------: | :--: | :---- |
| **TA-Lib** | ✅ | ✅ | ✅ | Matches `TA_RSI` after warmup |
| **Skender** | ✅ | ✅ | ✅ | Matches `GetRsi` |
| **Tulip** | ✅ | ✅ | ✅ | Matches `rsi` |
| **Ooples** | ✅ | — | — | Matches `CalculateRelativeStrengthIndex` |

## Common Pitfalls

1. **Warmup Initialization**: The first RSI value requires $N+1$ bars of data. During the first bar, there is no previous price to calculate change. QuanTAlib returns the RSI value from bar 1 onward, but values stabilize fully after approximately $4.6N$ bars.

2. **RMA vs SMA Confusion**: Many online calculators use SMA for the first average, then switch to RMA. This produces different values for the first ~3N bars. QuanTAlib uses RMA throughout for consistency.

3. **Overbought/Oversold Interpretation**: RSI above 70 means "overbought" but does not mean "sell." In strong uptrends, RSI can stay above 70 for extended periods. The market can remain irrational longer than the RSI can remain extreme.

4. **Period Selection**: The 14-period default works for daily charts. For intraday trading, consider 7 or 9 periods. For weekly charts, consider 21 or 28. Match the period to your signal horizon.

5. **Divergence False Signals**: RSI divergence (price makes new high, RSI does not) is a popular strategy but produces many false signals in trending markets. Use confirmation.

6. **Bar Correction Handling**: When `isNew=false`, RSI correctly restores the previous price state before recalculating. Both internal RMA instances are also rolled back. Incorrect `isNew` usage causes RSI to calculate changes from wrong reference points.

7. **Scale Independence Assumption**: RSI is scale-independent for price levels but not for volatility regimes. A stock with 5% daily swings produces different RSI dynamics than one with 0.5% swings, even at the same period setting.

## Usage Examples

```csharp
// Streaming: one bar at a time
var rsi = new Rsi(14);
foreach (var bar in liveStream)
{
    var result = rsi.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"RSI: {result.Value:F2}");
}

// Batch processing with Span (zero allocation)
double[] prices = LoadHistoricalData();
double[] rsiValues = new double[prices.Length];
Rsi.Calculate(prices.AsSpan(), rsiValues.AsSpan(), period: 14);

// Batch processing with TSeries
var series = new TSeries();
// ... populate series ...
var results = Rsi.Batch(series, period: 14);

// Event-driven chaining
var source = new TSeries();
var rsi14 = new Rsi(source, 14);
source.Add(new TValue(DateTime.UtcNow, 100.0));  // RSI updates automatically

// Prime with historical data
var rsi = new Rsi(14);
rsi.Prime(historicalPrices);  // Ready for live data
```

## References

- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*. Trend Research. Chapter: Relative Strength Index.
- Constance Brown. (1999). *Technical Analysis for the Trading Professional*. McGraw-Hill. (RSI divergence patterns)
- Cutler, David. (1991). "RSI Revisited." *Technical Analysis of Stocks & Commodities*. (Smoothed RSI variants)
