# STOCHRSI: Stochastic RSI Oscillator

> "RSI tells you whether momentum is overbought. Stochastic RSI tells you whether RSI itself is overbought. It's turtles all the way down." -- Anonymous quant

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Single series (close) |
| **Parameters** | `rsiLength` (default 14), `stochLength` (default 14), `kSmooth` (default 3), `dSmooth` (default 3) |
| **Outputs** | Dual series (%K line, %D signal line) |
| **Output range** | $0$ to $100$ |
| **Warmup** | `rsiWarmup + stochLength - 1 + kSmooth - 1 + dSmooth - 1` bars |

### Key takeaways

- Applies the Stochastic formula to RSI values instead of price, measuring RSI's position within its own recent range.
- More sensitive than RSI alone: a modest RSI move from 45 to 55 can produce a StochRSI swing from 0 to 100.
- Four parameters create a large configuration space. The defaults (14, 14, 3, 3) match TradingView convention.
- Uses `MonotonicDeque` for O(1) amortized RSI min/max tracking plus circular-buffer SMA for %K and %D smoothing.
- Recursive RSI dependency makes SIMD vectorization impractical; batch mode uses streaming replay.

## Historical Context

Tushar Chande and Stanley Kroll introduced the Stochastic RSI in *The New Technical Trader* (1994). Their motivation was practical: RSI frequently lingers in the 40-60 range during strong trends, making it difficult to identify shorter-term turning points. By applying Stochastic normalization to RSI, they created an indicator that oscillates across its full $[0, 100]$ range regardless of the underlying trend strength.

The key insight is that StochRSI measures RSI's position within its own recent range, not price's position within its range. This double transformation amplifies sensitivity at the cost of increased noise. A tradeoff that suits short-term mean-reversion strategies but misleads trend followers who mistake every extreme reading for a reversal.

Most implementations follow the TradingView convention of smoothing both %K and %D with SMA, producing what is effectively a "Slow StochRSI." Setting `kSmooth=1` gives the raw stochastic of RSI, matching TA-Lib's `STOCHRSI` function.

## What It Measures and Why It Matters

StochRSI measures where the current RSI value sits within the highest and lowest RSI values over the past `stochLength` bars, normalized to $[0, 100]$. A reading of $100$ means RSI is at its highest point in the lookback window. A reading of $0$ means RSI is at its lowest.

The double transformation (price to RSI, then RSI to Stochastic) makes StochRSI react faster to momentum changes than either indicator alone. Where RSI might take several bars to move from neutral to overbought, StochRSI can snap to $100$ the moment RSI reaches a new local high within its window.

This amplified sensitivity is useful for short-term mean-reversion setups in range-bound markets. In trending markets, StochRSI stays pinned at extremes for extended periods, which confirms trend strength but generates false reversal signals. Understanding the market regime determines whether StochRSI's sensitivity is a feature or a liability.

## Mathematical Foundation

### Core Formula

**Step 1: RSI**

$$
\text{RSI}_t = 100 - \frac{100}{1 + \frac{\text{AvgGain}_t}{\text{AvgLoss}_t}}
$$

where AvgGain and AvgLoss use Wilder's exponential smoothing with period `rsiLength`.

**Step 2: Stochastic normalization**

$$
\text{rawStoch}_t = 100 \times \frac{\text{RSI}_t - \min(\text{RSI}, n_s)}{\max(\text{RSI}, n_s) - \min(\text{RSI}, n_s)}
$$

where $n_s$ is `stochLength`. When $\max = \min$, rawStoch $= 50$.

**Step 3: %K smoothing**

$$
\%K_t = \text{SMA}(\text{rawStoch}, k)
$$

where $k$ is `kSmooth`.

**Step 4: %D signal line**

$$
\%D_t = \text{SMA}(\%K, d)
$$

where $d$ is `dSmooth`.

### Parameter Mapping

| Parameter | Code | Default | Constraints |
|-----------|------|---------|-------------|
| RSI Length | `rsiLength` | 14 | `> 0` |
| Stoch Length | `stochLength` | 14 | `> 0` |
| K Smoothing | `kSmooth` | 3 | `> 0` |
| D Smoothing | `dSmooth` | 3 | `> 0` |

### Warmup Period

$$
W = W_{\text{RSI}} + (n_s - 1) + (k - 1) + (d - 1)
$$

With defaults: $W_{\text{RSI}} = 15$, total $= 15 + 13 + 2 + 2 = 32$ bars.

## Architecture & Physics

### 1. Three-Stage Pipeline

```text
Source -> RSI(rsiLength) -> Stochastic(stochLength) -> SMA(kSmooth) -> %K
                                                                        |
                                                                   SMA(dSmooth) -> %D
```

Each stage maintains O(1) streaming state independently.

### 2. RSI Subsystem

An internal `Rsi` instance handles the first transformation. RSI manages its own bar correction via the `isNew` parameter, keeping state synchronized with the outer indicator.

### 3. MonotonicDeque Min/Max

A `MonotonicDeque` pair tracks the sliding min/max of RSI values over `stochLength` bars. Circular buffer (`_rsiBuf`) stores raw RSI values for deque rebuild on bar correction.

### 4. Dual SMA Smoothing

Two circular buffers (`_kBuf`, `_dBuf`) with running sums compute the %K and %D SMAs in O(1). Progressive fill: the SMA denominator ramps up from 1 to the full period as bars accumulate.

### 5. Edge Cases

| Condition | Behavior |
|-----------|----------|
| Any parameter `<= 0` | `ArgumentException` with `nameof()` |
| `NaN` / `Infinity` input | Substitutes last valid value |
| Flat RSI ($\max = \min$) | rawStoch returns $50$ |
| `kSmooth = 1` | No %K smoothing (matches TA-Lib convention) |
| `isNew = false` | Restores `_ps` + saved buffer slots; RSI handles its own rollback |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Overbought | `%K > 80` | RSI near top of its recent range |
| Neutral | `20 ≤ %K ≤ 80` | Normal RSI fluctuation |
| Oversold | `%K < 20` | RSI near bottom of its recent range |

### Signal Patterns

- **%K/%D crossover in extremes**: Bullish when %K crosses above %D below 20 (oversold reversal). Bearish when %K crosses below %D above 80. Mid-range crossovers are less reliable.
- **Divergence**: Price makes lower lows while StochRSI makes higher lows (bullish) or price makes higher highs while StochRSI makes lower highs (bearish). More frequent than RSI divergences due to amplified sensitivity.
- **Extended extremes**: StochRSI pinned at 0 or 100 indicates strong directional momentum, not an imminent reversal.

### Practical Notes

- StochRSI is best suited for mean-reversion strategies in ranging markets. In trending markets, it generates persistent false reversal signals.
- The four-parameter configuration space is large. Start with the TradingView defaults (14, 14, 3, 3) and adjust only with evidence.
- Use `kSmooth=1` to match TA-Lib's `STOCHRSI` output. Use `kSmooth=3` to match TradingView/Skender.

## Related Indicators

- [**Stoch**](../stoch/Stoch.md): Applies the stochastic formula to price instead of RSI.
- [**Stochf**](../stochf/Stochf.md): Fast Stochastic on price with shorter default lookback.
- [**RSI**](../../momentum/rsi/Rsi.md): The underlying momentum oscillator that StochRSI normalizes.
- [**SMI**](../smi/Smi.md): Measures distance from range midpoint instead of boundary; less sensitive but smoother.

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Skender | ✅ | `GetStochRsi(rsiLen, stochLen, dSmooth, kSmooth)` matches within `1e-6` |
| TA-Lib | ✅ | `StochRsi(close, rsiLen, stochLen, dSmooth)` with `kSmooth=1` matches within `1e-6` |
| Ooples | ⚠️ | Smoke test only. Fundamentally different algorithm (EMA-based); not directly comparable |

## Performance Profile

### Key Optimizations

- **O(1) amortized streaming**: MonotonicDeque for RSI min/max; circular buffers for both SMA stages.
- **Zero allocation**: `Update` uses pre-allocated buffers and `record struct State`.
- **Bar correction**: Saved buffer slot values (`PrevRsiBufVal`, `PrevKBufVal`, `PrevDBufVal`) enable rollback without buffer cloning.
- **Streaming replay batch**: Batch mode replays streaming to guarantee exact consistency; no separate SIMD path.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| RSI computation | ~6 ops (see RSI profile) |
| Deque push (amortized) | 2-3 comparisons |
| %K SMA update | 3 (sub + add + div) |
| %D SMA update | 3 (sub + add + div) |
| NaN check | 1 |
| **Total** | **~15 ops** |

### SIMD Analysis (Batch Mode)

| Property | Value |
|----------|-------|
| Vectorizable | No |
| Reason | Recursive RSI dependency prevents parallelization |
| Fallback | Streaming replay for batch consistency |

## Common Pitfalls

1. **Double sensitivity trap**: StochRSI amplifies RSI's movements. A modest RSI move from 45 to 55 can produce a StochRSI swing from 0 to 100. Not every extreme reading is a strong signal.
2. **Warmup underestimation**: With defaults (14, 14, 3, 3), StochRSI needs 32 bars, not 14. Trading on early output produces unreliable signals.
3. **Flat RSI = midpoint**: When RSI is constant over the stochastic window, max equals min and the formula returns 50. Some implementations return 0 or NaN.
4. **Parameter interaction complexity**: Four parameters create a large configuration space. Shorter `rsiLength` increases noise; longer `stochLength` increases lag; larger smoothing periods reduce signal frequency.
5. **Cross-library comparison hazards**: TA-Lib uses `kSmooth=1` (no %K smoothing). Skender/TradingView use `kSmooth=3`. Always match smoothing parameters before comparing outputs.
6. **Overbought persistence**: In strong trends, StochRSI stays above 80 for extended periods. Counter-trend trades based solely on StochRSI readings produce drawdowns.

## FAQ

**Q: Why does StochRSI not match between TA-Lib and TradingView?**
A: TA-Lib's `STOCHRSI` does not smooth %K (equivalent to `kSmooth=1`). TradingView applies `kSmooth=3` by default. Set `kSmooth=1` in QuanTAlib to match TA-Lib; use `kSmooth=3` to match TradingView.

**Q: When should I use StochRSI instead of RSI?**
A: When you need faster signals and can tolerate more noise. StochRSI is better for short-term mean-reversion in ranging markets. RSI is better for trend-following and longer-term momentum analysis.

**Q: Why is the batch path slower than other indicators?**
A: The recursive RSI dependency prevents SIMD vectorization. Batch mode replays streaming updates to guarantee exact consistency between API modes. The trade-off is correctness over throughput.

## References

- Chande, T. S.; Kroll, S. *The New Technical Trader*. Wiley, 1994.
- Wilder, J. W. *New Concepts in Technical Trading Systems*. Trend Research, 1978.
- Murphy, J. J. *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
