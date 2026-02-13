# STOCHRSI: Stochastic RSI Oscillator

> "RSI tells you whether momentum is overbought. Stochastic RSI tells you whether RSI itself is overbought. It's turtles all the way down." -- Anonymous quant

## Overview

The **Stochastic RSI (StochRSI)** applies the Stochastic Oscillator formula to RSI values instead of raw price. The result is a bounded oscillator (0-100) that is more sensitive to short-term overbought/oversold conditions than RSI alone. Where RSI might linger in the 40-60 range during consolidation, StochRSI pushes to extremes more frequently, giving traders earlier (though noisier) reversal signals.

The indicator produces two lines:

- **%K**: SMA-smoothed stochastic of RSI values
- **%D**: SMA of %K (signal line)

## Historical Context

Tushar Chande and Stanley Kroll introduced the Stochastic RSI in their 1994 book *The New Technical Trader*. Their motivation was straightforward: RSI often spends long periods in non-extreme territory during strong trends, making it difficult to identify shorter-term turning points. By applying the Stochastic normalization to RSI, they created an indicator that oscillates across its full 0-100 range regardless of the underlying trend strength.

The key insight is that StochRSI measures RSI's position within its own recent range, not price's position within its range. This double transformation amplifies sensitivity at the cost of increased noise, a tradeoff that suits short-term mean-reversion strategies but can mislead trend followers.

Most implementations follow the TradingView convention of smoothing both %K and %D with SMA, producing what is effectively a "Slow StochRSI." The unsmoothed variant (kSmooth=1) gives the raw stochastic of RSI.

## Architecture

```
Source ──→ RSI(rsiLength) ──→ Stochastic(stochLength) ──→ SMA(kSmooth) ──→ %K
                                                                            │
                                                                     SMA(dSmooth) ──→ %D
```

### Streaming (O(1) amortized per bar)

The streaming path chains three computation stages, each maintaining O(1) state:

| Component | Data Structure | Role |
|-----------|---------------|------|
| RSI | Internal `Rsi` instance | Computes RSI values from source prices |
| Min/Max tracking | `MonotonicDeque` pair | O(1) amortized sliding min/max of RSI over `stochLength` |
| %K smoothing | Circular buffer + running sum | O(1) SMA of raw stochastic values |
| %D smoothing | Circular buffer + running sum | O(1) SMA of %K values |

### State Management

```
State record struct:
  Count          -- bar counter for warmup tracking
  KSum / KHead   -- running sum and circular buffer head for %K SMA
  DSum / DHead   -- running sum and circular buffer head for %D SMA
  LastValidValue -- NaN/Infinity protection (last valid source price)
  K / D          -- current %K and %D output values
  PrevRsiBufVal  -- saved RSI buffer slot for bar correction rollback
  PrevKBufVal    -- saved %K buffer slot for bar correction rollback
  PrevDBufVal    -- saved %D buffer slot for bar correction rollback
```

The standard `_s` / `_ps` state snapshot pair enables bar correction:

- `isNew=true`: `_ps = _s`, save buffer slot values before overwrite, advance counters
- `isNew=false`: `_s = _ps`, restore buffer slot values, recompute from previous state

The RSI instance also supports bar correction through its own `isNew` parameter.

### Warmup

$$
\text{WarmupPeriod} = \text{RSI warmup} + \text{stochLength} - 1 + \text{kSmooth} - 1 + \text{dSmooth} - 1
$$

With default parameters (14, 14, 3, 3): RSI warmup = 15, total = 15 + 13 + 2 + 2 = 32 bars.

`IsHot` fires when `Count >= WarmupPeriod`.

### Batch Path

`Update(TSeries)` uses streaming replay, not a separate span-based batch path. This ensures exact consistency between streaming and batch modes at the cost of batch throughput. The recursive RSI dependency makes SIMD vectorization impractical for the full pipeline.

## Mathematical Foundation

### RSI Stage

$$
\text{RSI}[n] = 100 - \frac{100}{1 + \frac{\text{AvgGain}[n]}{\text{AvgLoss}[n]}}
$$

Where AvgGain and AvgLoss use Wilder's exponential smoothing with period `rsiLength`.

### Stochastic Normalization

$$
\text{rawStoch}[n] = 100 \times \frac{\text{RSI}[n] - \min(\text{RSI}, \text{stochLength})}{\max(\text{RSI}, \text{stochLength}) - \min(\text{RSI}, \text{stochLength})}
$$

When $\max = \min$ (RSI flat over the window), rawStoch = 0.

### %K Smoothing

$$
\%K[n] = \text{SMA}(\text{rawStoch}, \text{kSmooth})
$$

### %D Signal Line

$$
\%D[n] = \text{SMA}(\%K, \text{dSmooth})
$$

### Warmup Seeding

Following the PineScript convention, SMA buffers are pre-filled with the first computed value rather than NaN. This produces usable output from bar 1 of each SMA stage, matching TradingView behavior.

## Performance Profile

| Metric | Value |
|--------|-------|
| Time complexity | O(1) amortized per bar (streaming) |
| Space complexity | O(stochLength + kSmooth + dSmooth) |
| Allocations | Zero per update |
| NaN handling | Last valid value substitution |
| SIMD | Not applicable (recursive RSI dependency) |
| FMA | Not used (SMA arithmetic too simple to benefit) |

| Quality Metric | Score (1-10) |
|----------------|-------------|
| Sensitivity | 9 |
| Smoothness | 5 (with kSmooth=3, dSmooth=3) |
| Noise rejection | 4 |
| Overbought/Oversold detection | 9 |
| Trend following | 3 |

## Validation

Cross-validated against independent implementations:

| Library | Mode | Tolerance | Status | Notes |
|---------|------|-----------|--------|-------|
| Skender | Batch | 1e-9 | Pass | Exact match after warmup |
| Skender | Streaming | 1e-9 | Pass | Bar-by-bar verification |
| Skender | Span | 1e-9 | Pass | Span API consistency |
| TA-Lib | Batch | 1e-9 | Pass | Lookback-aligned comparison |
| Ooples | Smoke | N/A | Smoke | Fundamentally different implementation (incompatible) |

Self-consistency validated across streaming, batch, span, and eventing API modes with exact match verification.

### Ooples Incompatibility

OoplesFinance uses a structurally different StochRSI calculation that produces values on a different scale and with different smoothing. This is not a bug in either implementation; the two libraries interpret "Stochastic RSI" differently. The Ooples test runs as a smoke test (verifies no crashes) without value comparison.

## Common Pitfalls

1. **Double sensitivity trap.** StochRSI amplifies RSI's movements. A modest RSI move from 45 to 55 can produce a StochRSI swing from 0 to 100 if that range spans the recent RSI min/max. Do not treat every 0 or 100 reading as a strong signal.

2. **Flat RSI = zero division.** When RSI is constant over the stochastic window (common during low-volatility consolidation), max = min and the stochastic formula produces 0. Some implementations return 50 or NaN here; QuanTAlib returns 0, matching TradingView.

3. **Warmup period underestimation.** StochRSI needs RSI to stabilize first, then the stochastic window to fill, then both SMA smoothers to fill. With defaults (14,14,3,3), that is 32 bars, not 14.

4. **Confusing %K and %D roles.** In standard Stochastic, %K is the fast line. In StochRSI with kSmooth > 1, %K is already smoothed. The "fast" vs "slow" distinction from regular Stochastic does not directly apply.

5. **Overbought does not mean sell.** In strong uptrends, StochRSI can stay above 80 for extended periods. Use StochRSI for mean-reversion strategies in ranging markets, not as a counter-trend tool in trending markets.

6. **Parameter interaction complexity.** Four parameters (rsiLength, stochLength, kSmooth, dSmooth) create a large configuration space. The defaults (14,14,3,3) are the TradingView standard. Shorter rsiLength increases noise; longer stochLength increases lag; larger smoothing periods reduce signal frequency.

7. **Cross-library comparison hazards.** Different libraries handle warmup, SMA seeding, and edge cases differently. Always align warmup periods before comparing output arrays.

## Usage

```csharp
// Streaming (returns K line)
var stochrsi = new Stochrsi(rsiLength: 14, stochLength: 14, kSmooth: 3, dSmooth: 3);
TValue result = stochrsi.Update(new TValue(time, price));

// Access K and D values
double k = stochrsi.K;
double d = stochrsi.D;

// Event-based chaining
var source = new TSeries();
var stochrsi = new Stochrsi(source, rsiLength: 14, stochLength: 14);

// Batch (TSeries) - returns K line
TSeries kResults = Stochrsi.Batch(source);

// Batch with K and D lines
var indicator = new Stochrsi();
var (kSeries, dSeries) = indicator.UpdateKD(source);

// Calculate (returns indicator for state inspection)
var (results, ind) = Stochrsi.Calculate(source);
```

## Interpretation

- **Overbought / Oversold:**

  | Zone | Level | Interpretation |
  |------|-------|----------------|
  | Overbought | > 80 | RSI is near the top of its recent range |
  | Neutral | 20-80 | Normal RSI fluctuation |
  | Oversold | < 20 | RSI is near the bottom of its recent range |

- **%K/%D Crossovers:**
  - Bullish: %K crosses above %D below 20 (oversold reversal)
  - Bearish: %K crosses below %D above 80 (overbought reversal)
  - Mid-range crossovers are less reliable

- **Divergence:**
  - Bullish: Price makes lower lows while StochRSI makes higher lows
  - Bearish: Price makes higher highs while StochRSI makes lower highs
  - More frequent than RSI divergences due to amplified sensitivity

- **Zero and 100 extremes:**
  - StochRSI = 0: RSI is at the lowest point in its stochastic window
  - StochRSI = 100: RSI is at the highest point in its stochastic window
  - Extended stays at 0 or 100 indicate strong directional momentum

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `rsiLength` | int | 14 | > 0 | RSI calculation period |
| `stochLength` | int | 14 | > 0 | Stochastic lookback period for RSI min/max |
| `kSmooth` | int | 3 | > 0 | SMA smoothing period for %K |
| `dSmooth` | int | 3 | > 0 | SMA smoothing period for %D signal line |

## References

- Chande, Tushar S. and Kroll, Stanley. *The New Technical Trader*. John Wiley & Sons, 1994
- Wilder, J. Welles. *New Concepts in Technical Trading Systems*. Trend Research, 1978
- Murphy, John J. *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999
- [TradingView Stochastic RSI](https://www.tradingview.com/support/solutions/43000502333/)
- [Investopedia Stochastic RSI](https://www.investopedia.com/terms/s/stochrsi.asp)
