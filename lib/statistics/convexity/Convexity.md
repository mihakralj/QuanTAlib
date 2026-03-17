# Convexity: Beta Convexity

> *The asymmetry between upside and downside beta reveals whether an asset delivers convex payoffs — the holy grail of portfolio construction.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Dual (asset price, market price) |
| **Parameters**   | `period`                         |
| **Outputs**      | 5 series (BetaStd, BetaUp, BetaDown, Ratio, Convexity) |
| **Output range** | Convexity ≥ 0; BetaStd/Up/Down unbounded |
| **Warmup**       | `period + 1` bars                |

- Convexity measures how asymmetrically an asset responds to market up-moves vs. down-moves.
- **Similar:** [Beta](../beta/Beta.md), [Correl](../correl/Correl.md) | **Trading note:** Convexity > 0 signals favorable payoff asymmetry. Ratio > 1 = asset amplifies gains more than losses.
- Based on Skender.Stock.Indicators `GetBeta(BetaType.All)` implementation.

Beta Convexity decomposes the standard beta coefficient into its upside and downside components, then measures their squared difference. An asset with positive convexity captures more upside than downside — the ideal characteristic for portfolio construction. Harry Markowitz's Modern Portfolio Theory shows that investors should seek assets that maximise `(β⁺ - β⁻)²`.

## Historical Context

The concept of separating upside and downside beta was pioneered by Bawa and Lindenberg (1977) in their work on lower partial moments. It gained mainstream traction through Ang, Chen, and Xing's landmark 2006 paper "Downside Risk," which demonstrated that stocks with high downside beta earn higher returns — the so-called "downside risk premium." Skender's .NET implementation packages this as `BetaType.All`, computing standard, upside, downside, ratio, and convexity in a single pass.

## Architecture & Physics

Convexity is built on the same dual-input pattern as Beta, but adds a conditional filtering step:

1. **Standard Beta** uses O(1) Kahan-compensated running sums for `Cov(Ra, Rm) / Var(Rm)`
2. **Filtered Betas** perform an O(period) scan of the ring buffer, partitioning returns by market direction:
   - `Rm > 0` → contributes to BetaUp sums
   - `Rm < 0` → contributes to BetaDown sums
   - `Rm = 0` → excluded (following Skender's convention)

### The Bar Correction Pattern

For streaming bar corrections (`isNew = false`), Convexity follows the proven Beta.cs pattern: only compensation values are saved/restored (not full sums), and a Kahan delta swaps the old return contribution for the new one. This ensures numerical stability across long-running sessions.

## Mathematical Foundation

### Standard Beta (all bars)

$$ \beta = \frac{N \sum R_a R_m - \sum R_a \sum R_m}{N \sum R_m^2 - (\sum R_m)^2} $$

### Upside Beta (market up bars only, $R_m > 0$)

$$ \beta^+ = \frac{N^+ \sum_{R_m > 0} R_a R_m - \sum_{R_m > 0} R_a \sum_{R_m > 0} R_m}{N^+ \sum_{R_m > 0} R_m^2 - (\sum_{R_m > 0} R_m)^2} $$

### Downside Beta (market down bars only, $R_m < 0$)

$$ \beta^- = \frac{N^- \sum_{R_m < 0} R_a R_m - \sum_{R_m < 0} R_a \sum_{R_m < 0} R_m}{N^- \sum_{R_m < 0} R_m^2 - (\sum_{R_m < 0} R_m)^2} $$

### Derived Outputs

$$ \text{Ratio} = \frac{\beta^+}{\beta^-} $$

$$ \text{Convexity} = (\beta^+ - \beta^-)^2 $$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict (2 inputs) | 2 | 3 cy | ~6 cy |
| Compute asset + market returns | 2 | 3 cy | ~6 cy |
| Update 4 Kahan running sums | 4 | 4 cy | ~16 cy |
| Compute standard beta (FMA) | 1 | 5 cy | ~5 cy |
| O(period) scan for Up/Down beta | period | 4 cy | ~80 cy* |
| Compute ratio + convexity | 2 | 3 cy | ~6 cy |
| **Total** | **O(period)** | — | **~119 cy** |

*Assuming period = 20. The O(period) scan is a simple branch-free iteration with no allocations.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~40 ns/bar | O(period) scan dominates. |
| **Allocations** | 0 | Zero-allocation hot path. |
| **Complexity** | O(period) | Linear scan for up/down filtering. |
| **Accuracy** | 9 | Kahan compensation prevents drift. |
| **Timeliness** | Lagged | Depends on the lookback period. |
| **Smoothness** | Low | Sensitive to period and market regime. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Reference implementation. |
| **Skender** | ✅ | Matches `GetBeta(BetaType.All)` — 5-output bundle. |

### Common Pitfalls

1. **Zero Variance in Up/Down Subsets**: If all market up-day returns are identical, `Var(Rm|Rm>0) = 0` and BetaUp is undefined (returns 0). This occurs with synthetic data; real market data always has variance.
2. **Period Sensitivity**: Short periods (< 10) may have too few up/down bars for meaningful beta decomposition. Typical institutional use: period = 60 (3-month daily).
3. **Interpretation**: Convexity = 0 does NOT mean beta = 0. It means upside and downside betas are equal (symmetric risk profile).

## C# Usage

```csharp
// Initialize with period 20
var conv = new Convexity(20);

// Update with Asset and Market prices
conv.Update(assetPrice, marketPrice);

Console.WriteLine($"BetaStd: {conv.BetaStd:F4}");
Console.WriteLine($"BetaUp:  {conv.BetaUp:F4}");
Console.WriteLine($"BetaDown: {conv.BetaDown:F4}");
Console.WriteLine($"Ratio:   {conv.Ratio:F4}");
Console.WriteLine($"Convexity: {conv.ConvexityValue:F4}");
```

### Batch Mode

```csharp
var (betaStd, betaUp, betaDown, ratio, convexity) =
    Convexity.Batch(assetSeries, marketSeries, period: 20);
```

### Bar Correction

```csharp
// New bar
conv.Update(assetPrice, marketPrice, isNew: true);

// Update same bar (price correction)
conv.Update(correctedAsset, correctedMarket, isNew: false);
```

## Resources

- [Skender GetBeta](https://dotnet.stockindicators.dev/indicators/Beta/) — BetaType.All returns all 5 outputs.
- Ang, Chen, Xing (2006). ["Downside Risk"](https://academic.oup.com/rfs/article/19/4/1191/1572624) — Empirical evidence for downside risk premium.
- Bawa, Lindenberg (1977). "Capital Market Equilibrium in a Mean-Lower Partial Moment Framework" — Original lower partial moment theory.
