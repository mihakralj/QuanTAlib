# DX: Directional Movement Index

> "ADX tells you how strong the trend is; DX tells you how strong it is *right now*, without the smoothing delay."

The Directional Movement Index (DX) measures the strength of directional movement in a market, regardless of whether that movement is up or down. Unlike its more famous cousin ADX (Average Directional Index), DX is the raw, unsmoothed version—more responsive but also more noisy.

## Historical Context

J. Welles Wilder Jr. introduced the Directional Movement System in his 1978 book *New Concepts in Technical Trading Systems*. The system decomposes price action into three components: upward movement (+DM), downward movement (-DM), and volatility (True Range). These components are then normalized and combined to create directional indicators (+DI, -DI) and the index itself (DX).

DX is often overlooked in favor of ADX, which applies an additional smoothing layer. However, DX provides faster signals for traders who can tolerate more noise in exchange for reduced lag.

## Architecture & Physics

The DX calculation is a multi-stage pipeline:

1. **Directional Movement Decomposition**: Price expansion is broken into +DM (upward) and -DM (downward) components
2. **Volatility Normalization**: Raw movements are normalized by True Range to create +DI and -DI
3. **Index Calculation**: The absolute difference of the DIs is divided by their sum, scaled to 0-100

### Key Difference from ADX

- **DX**: Raw directional strength, updated every bar
- **ADX**: DX smoothed with RMA (Wilder's Moving Average)

DX responds immediately to changes in trend strength; ADX lags by approximately one period.

## Mathematical Foundation

### 1. Directional Movement (DM)

Today's high/low expansion is compared to yesterday's:

$$ \text{UpMove} = H_t - H_{t-1} $$
$$ \text{DownMove} = L_{t-1} - L_t $$

$$ +DM = \begin{cases} \text{UpMove} & \text{if } \text{UpMove} > \text{DownMove} \text{ and } \text{UpMove} > 0 \\ 0 & \text{otherwise} \end{cases} $$

$$ -DM = \begin{cases} \text{DownMove} & \text{if } \text{DownMove} > \text{UpMove} \text{ and } \text{DownMove} > 0 \\ 0 & \text{otherwise} \end{cases} $$

### 2. True Range (TR)

$$ TR = \max(H_t - L_t, |H_t - C_{t-1}|, |L_t - C_{t-1}|) $$

### 3. Smoothing (RMA)

Wilder's Moving Average is applied to +DM, -DM, and TR:

$$ +DM_{smoothed} = RMA(+DM, N) $$
$$ -DM_{smoothed} = RMA(-DM, N) $$
$$ TR_{smoothed} = RMA(TR, N) $$

Where RMA uses $\alpha = 1/N$ (equivalent to EMA with period $2N-1$).

### 4. Directional Indicators (DI)

$$ +DI = 100 \times \frac{+DM_{smoothed}}{TR_{smoothed}} $$
$$ -DI = 100 \times \frac{-DM_{smoothed}}{TR_{smoothed}} $$

### 5. Directional Index (DX)

$$ DX = 100 \times \frac{|+DI - -DI|}{+DI + -DI} $$

### 6. Wilder's Smoothing

The smoothing uses Wilder's original method (not standard RMA/EMA):

$$ Smooth_{t} = Smooth_{t-1} - \frac{Smooth_{t-1}}{N} + Input_{t} $$

This differs from standard RMA which divides the input by N.

## Performance Profile

The implementation uses O(1) updates with aggressive inlining and FMA operations.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 3ns | Per-bar update (Apple M1 Max) |
| **Allocations** | 0 | Hot path is allocation-free |
| **Complexity** | O(1) | Constant time for streaming updates |
| **Accuracy** | 10/10 | Matches TA-Lib to 1e-9 |
| **Timeliness** | 6/10 | Less lag than ADX due to no final smoothing |
| **Overshoot** | 5/10 | More volatile than ADX |
| **Smoothness** | 4/10 | Raw signal, noisy |

### Quality Metrics

| Quality | Score | Justification |
| :--- | :---: | :--- |
| Accuracy | 9 | Preserves trend structure |
| Timeliness | 6 | One period faster than ADX |
| Overshoot | 5 | Can spike on volatile bars |
| Smoothness | 4 | Unsmoothed, reflects bar-to-bar changes |

## Usage

### Scalar (Streaming)

```csharp
var dx = new Dx(14);

foreach (var bar in bars)
{
    dx.Update(bar);
    Console.WriteLine($"DX: {dx.Last.Value:F2}, +DI: {dx.DiPlus.Value:F2}, -DI: {dx.DiMinus.Value:F2}");
}
```

### Batch (Span-based)

```csharp
Span<double> output = stackalloc double[close.Length];
Dx.Calculate(high, low, close, 14, output);
```

### With Bar Correction

```csharp
// New bar arrives
dx.Update(bar, isNew: true);

// Same bar updates (intra-bar corrections)
dx.Update(modifiedBar, isNew: false);
```

## Interpretation

| DX Value | Trend Strength |
| :---: | :--- |
| 0-15 | Weak or no trend |
| 15-25 | Developing trend |
| 25-50 | Strong trend |
| 50-75 | Very strong trend |
| 75-100 | Extreme trend (rare) |

### Trading Signals

- **DX Rising**: Trend is strengthening
- **DX Falling**: Trend is weakening
- **+DI > -DI**: Uptrend dominates
- **-DI > +DI**: Downtrend dominates
- **DI Crossover**: Potential trend reversal

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_DX` |
| **Skender** | ✅ | Matches `GetDx` |
| **Tulip** | ✅ | Matches `ti.dx` |
| **TradingView** | ✅ | Matches Pine Script `ta.dm` components |

## Common Pitfalls

1. **Confusing DX with ADX**: DX is unsmoothed; ADX is RMA(DX). If you want the classic ADX behavior, use the ADX indicator.

2. **Period Too Short**: Periods below 7 make DX extremely noisy. The standard is 14.

3. **First N Bars**: The first `period` bars output 0 as they're needed for warmup. Don't trade on these values.

4. **DI Sum Near Zero**: When both +DI and -DI approach zero (no directional movement), DX becomes unstable. The implementation guards against division by zero.

5. **Not a Direction Indicator**: DX measures trend *strength*, not direction. Use +DI vs -DI for direction.

## References

- Wilder, J. W. (1978). *New Concepts in Technical Trading Systems*
- [TradingView DX Documentation](https://www.tradingview.com/support/solutions/43000502250-directional-movement-dm/)
- [StockCharts ADX/DX](https://school.stockcharts.com/doku.php?id=technical_indicators:average_directional_index_adx)
