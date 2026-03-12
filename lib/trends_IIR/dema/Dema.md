# DEMA: Double Exponential Moving Average

> *EMA is good. DEMA is better. It's like an EMA that drank a double espresso and stopped lagging behind the conversation.*

## Quick Reference

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (int > 0)               |
| **Outputs**      | Single series (Dema)                    |
| **Output range** | Tracks input                            |
| **Warmup**       | `period` bars                         |
| **PineScript**   | [dema.pine](dema.pine)                       |
| **Signature**    | [dema_signature](dema_signature.md) |

## Key Takeaways

- **Lag Reduction**: Cuts EMA lag by ~50% through mathematical extrapolation
- **Trend Hugging**: Tighter fit to price action than standard EMA
- **Overshoot Risk**: Can amplify reversals due to predictive nature
- **Computational Cost**: ~2× EMA operations for the dual-stage design
- **Best For**: Trend-following systems needing responsive signals

## What It Measures and Why It Matters

DEMA measures the smoothed trend of price action with significantly reduced lag compared to traditional moving averages. It matters because lag is the enemy of timely signals—traditional EMAs lag by roughly N/2 bars, making them slow to react to trend changes. DEMA's extrapolation formula (2×EMA₁ - EMA₂) mathematically projects the EMA forward by one lag unit, creating a "lead indicator" that anticipates rather than follows.

This makes DEMA particularly valuable for:

- **Trend-following strategies** requiring quick entries/exits
- **Oscillator construction** (MACD uses DEMA variants)
- **Signal generation** where timeliness trumps smoothness
- **High-frequency trading** where every bar counts

## Historical Context

Introduced by Patrick Mulloy in the January 1994 issue of *Technical Analysis of Stocks & Commodities*, DEMA was designed to reduce the lag of trend-following indicators. Mulloy realized that smoothing always introduces lag, but by combining single and double smoothing, you could mathematically negate some of that delay.

The insight was elegant: if EMA1 lags price by $L$ bars, and EMA2 lags EMA1 by another $L$ bars, then the expression $2 \times \text{EMA1} - \text{EMA2}$ extrapolates forward by $L$, canceling the lag for linear trends. This principle later inspired TEMA (triple) and the broader family of lag-compensating filters.

## Architecture & Physics

DEMA is a composite indicator built from two EMAs in a cascade arrangement.

### 1. First EMA Stage (EMA1)

The primary smoother applied directly to price:

$$\text{EMA}_1 = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{1,t-1}$$

where $\alpha = \frac{2}{N + 1}$ and $N$ is the period.

### 2. Second EMA Stage (EMA2)

The secondary smoother applied to EMA1's output:

$$\text{EMA}_2 = \alpha \cdot \text{EMA}_1 + (1 - \alpha) \cdot \text{EMA}_{2,t-1}$$

### 3. Lag Cancellation Combiner

The final output extrapolates using the difference between stages:

$$\text{DEMA} = 2 \times \text{EMA}_1 - \text{EMA}_2$$

The "physics" relies on the fact that EMA2 lags EMA1 roughly as much as EMA1 lags the price. The coefficient 2 on EMA1 and -1 on EMA2 creates a unity-gain filter ($2 - 1 = 1$) that projects forward by one lag unit.

## Interpretation and Signals

### Trend Direction

- **Above Price**: Bullish trend signal (DEMA acting as support)
- **Below Price**: Bearish trend signal (DEMA acting as resistance)
- **Slope Analysis**: Positive slope = uptrend, negative slope = downtrend

### Crossover Signals

- **Price crosses above DEMA**: Potential buy signal in uptrends
- **Price crosses below DEMA**: Potential sell signal in downtrends
- **Zero crossings**: Momentum shifts (less reliable than EMA due to overshoot)

### Divergence Analysis

- **Bullish Divergence**: Price makes lower low, DEMA makes higher low
- **Bearish Divergence**: Price makes higher high, DEMA makes lower high
- **Convergence**: Price and DEMA moving toward each other (caution signal)

### Signal Quality Factors

- **Strength**: Distance between price and DEMA (larger = stronger trend)
- **Consistency**: How long the trend has maintained direction
- **Volume Confirmation**: Higher volume on breakouts improves reliability

## Mathematical Foundation

### EMA Alpha Calculation

$$\alpha = \frac{2}{N + 1}$$

### Lag Analysis

For a single EMA with smoothing factor $\alpha$, the mean lag is:

$$L = \frac{1 - \alpha}{\alpha} = \frac{N - 1}{2}$$

For cascaded EMAs:

- EMA1 lag: $L$
- EMA2 lag (from price): $2L$

The DEMA formula extrapolates:

$$\text{DEMA} = \text{EMA}_1 + (\text{EMA}_1 - \text{EMA}_2)$$

This adds the "velocity" (difference) to the position (EMA1), projecting forward.

### Transfer Function

In the z-domain, DEMA's transfer function:

$$H(z) = 2 \cdot H_{EMA}(z) - H_{EMA}^2(z)$$

where $H_{EMA}(z) = \frac{\alpha}{1 - (1-\alpha)z^{-1}}$
## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | 4 | 3 | 12 |
| ADD/SUB | 4 | 1 | 4 |
| **Total** | **8** | — | **~16 cycles** |

DEMA requires exactly 2× the operations of a single EMA.

### Batch Mode (SIMD/FMA Analysis)

Due to the recursive nature of EMA, SIMD vectorization is limited. However, FMA can reduce multiply-add pairs:

| Optimization | Operations | Cycles Saved |
| :--- | :---: | :---: |
| FMA for EMA1 update | 1 FMA vs MUL+ADD | ~2 |
| FMA for EMA2 update | 1 FMA vs MUL+ADD | ~2 |
| **Per-bar savings** | — | **~4 cycles** |

*Effective throughput: ~12 cycles/bar with FMA optimization.*

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Good trend tracking, overshoots on reversals |
| **Timeliness** | 8/10 | Significantly reduced lag vs EMA |
| **Overshoot** | 4/10 | Can overshoot significantly on sharp reversals |
| **Smoothness** | 6/10 | Less smooth than EMA due to extrapolation |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~3 ns/bar | 2× EMA cost |
| **Allocations** | 0 bytes | Hot path allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **State Size** | 48 bytes | Two EMA states |

*Benchmarked on Intel i7-12700K @ 3.6 GHz, AVX2, .NET 10.0*

## Related Indicators

- **EMA**: Single exponential smoothing (higher lag, smoother)
- **TEMA**: Triple exponential (even less lag, more overshoot)
- **KAMA**: Adaptive smoothing based on volatility
- **VIDYA**: Variable index dynamic average
- **WMA**: Weighted moving average (FIR, no lag reduction)
- **SMA**: Simple moving average (maximum lag)

## Usage Examples

### Basic Trend Following

```csharp
var dema = new Dema(source, 20);
if (price > dema.Last.Value && dema.Last.Value > dema.Previous.Value)
{
    // Bullish trend confirmed
    EnterLong();
}
```

### MACD Construction

```csharp
// DEMA is often used in MACD for signal line
var fast = new Dema(source, 12);
var slow = new Dema(source, 26);
var macd = fast.Last.Value - slow.Last.Value;
var signal = new Dema(new TSeries() { macd }, 9);
```

### Adaptive Period Selection

```csharp
// Shorter periods for ranging markets, longer for trending
var volatility = CalculateVolatility(source);
int period = volatility > threshold ? 10 : 20; // Responsive in trends
var dema = new Dema(source, period);
```

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Matches `TA_DEMA` (tolerance: 1e-9) |
| **Skender** | ✅ | Matches `GetDema` (tolerance: 1e-9) |
| **Tulip** | ✅ | Matches `dema` (tolerance: 1e-9) |
| **Ooples** | ✅ | Matches `2*EMA - EMA(EMA)` formula |

## Reference Calculation Table

| Period | Price Sequence | EMA₁ | EMA₂ | DEMA | Notes |
|--------|----------------|------|------|------|-------|
| 5 | 10 | 10.00 | 10.00 | 10.00 | Initial values |
| 5 | 10, 20 | 13.33 | 11.11 | 15.56 | First calculation |
| 5 | 10, 20, 30 | 18.52 | 13.58 | 23.46 | Trend acceleration |
| 5 | 10, 20, 30, 40 | 24.69 | 16.80 | 32.58 | Extrapolation effect |
| 5 | 10, 20, 30, 40, 50 | 31.13 | 20.74 | 41.52 | Full convergence |

*α = 2/(5+1) = 0.333, Decay = 1-α = 0.667*

## FAQ

**Q: How does DEMA compare to EMA for period N?**
A: DEMA(N) responds roughly like EMA(N×0.7) but with more overshoot. The lag reduction makes it faster but noisier.

**Q: When should I use DEMA vs TEMA?**
A: DEMA for most cases—it's 80% of TEMA's lag reduction with 50% less computation. Use TEMA only if DEMA still lags too much.

**Q: Does DEMA work well in sideways markets?**
A: Poorly. The extrapolation amplifies noise, creating false signals. Combine with trend strength filters.

**Q: Can DEMA be used for any period?**
A: Yes, but very short periods (<5) amplify noise excessively. Very long periods (>50) lose the lag-reduction benefit.

**Q: How does bar correction work?**
A: When `isNew=false`, QuanTAlib rolls back both EMA states to pre-update values, then reapplies the correction. This ensures identical results regardless of update order.

## Common Pitfalls

1. **Overshoot on Reversals**: Because DEMA extrapolates using the EMA "velocity," it overshoots when price reverses direction. This is the fundamental tradeoff for reduced lag—the filter commits to trends and resists reversals.

2. **"Double" Misconception**: DEMA is *not* a double-smoothed average (EMA of EMA). That would increase lag. DEMA uses the double-smooth as a correction term to reduce lag.

3. **Warmup Period**: DEMA needs approximately $2N$ bars to converge fully, as EMA2 requires EMA1 to stabilize first. Use `IsHot` to detect convergence.

4. **Comparing Periods with EMA**: DEMA(20) is not equivalent to EMA(20) in responsiveness. Due to lag reduction, DEMA(20) behaves more like EMA(14-16) in terms of crossover timing.

5. **Signal Noise Amplification**: The extrapolation amplifies high-frequency components. In choppy markets, DEMA produces more whipsaws than EMA.

6. **Bar Correction**: Use `isNew=false` when correcting the current bar (same timestamp, revised price). State rollback ensures consistent results.

## References

- Mulloy, P. (1994). "Smoothing Data with Faster Moving Averages." *Technical Analysis of Stocks & Commodities*, 12(1), 11-19.
