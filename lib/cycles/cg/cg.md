# CG: Center of Gravity

## Overview and Purpose

The Center of Gravity (CG) indicator, developed by John Ehlers, is a cycle analysis tool that uses the physics concept of center of gravity to identify cycle turning points in financial markets. By calculating the balance point of price data over a specified period, the indicator creates an oscillator that can help traders anticipate potential reversal points in market cycles.

Unlike traditional moving averages that simply smooth price data, the Center of Gravity indicator treats price data as masses distributed over time and calculates where the "balance point" would be. This approach provides insights into the distribution of price momentum within the lookback period.

## Core Concepts

* **Physics-based approach:** Uses the center of gravity concept from physics where each price point represents a mass and the indicator finds the balance point
* **Oscillating indicator:** Provides an oscillator that fluctuates around zero based on price distribution
* **Cycle identification:** Particularly effective at identifying shifts in the dominant cycle within the lookback period
* **Zero-line analysis:** Oscillates around zero with crossovers indicating potential cycle phase changes

The core innovation of this indicator is its ability to measure where the "weight" of price data is concentrated within the lookback period, providing insights into market momentum distribution.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Length | 10 | Controls the lookback period for the Center of Gravity calculation | Increase for longer cycles and smoother signals, decrease for shorter cycles and more responsive signals |
| Source | source | Data source for calculation | Typically uses close; hlc3 provides balanced representation; hl2 for range-based analysis |

**Pro Tip:** The optimal length setting often correlates with the dominant cycle length in the market. Start with shorter periods (8-14) for active markets and longer periods (20-30) for smoother, longer-term cycle identification.

## Calculation and Mathematical Foundation

**Simplified explanation:**
The Center of Gravity calculates where the "balance point" would be if each price in the lookback period was treated as a mass at its time position. The result is then normalized to oscillate around zero by subtracting the theoretical center point.

**Technical formula:**
The Center of Gravity is calculated as:

CG = [Σ(i × Price[i-1]) / Σ(Price[i-1])] - (Length + 1) / 2

Where:
* i ranges from 1 to Length (representing position weights)
* Price[i-1] is the price at position i-1 bars ago (current bar when i=1)
* The subtraction of (Length + 1) / 2 centers the oscillator around zero
* This represents the "balance point" where price data would be in equilibrium

The calculation process:
```
numerator = Σ(i × Price[i-1]) for i = 1 to Length
denominator = Σ(Price[i-1]) for i = 1 to Length
raw_cg = numerator / denominator
CG = raw_cg - (Length + 1) / 2
```

> 🔍 **Technical Note:** The algorithm calculates the weighted average position of prices, then subtracts the theoretical center point to create an oscillator. When prices are distributed evenly, CG equals zero. When recent prices dominate, CG becomes positive; when older prices dominate, CG becomes negative.

## Interpretation Details

The Center of Gravity indicator provides several analytical perspectives:

* **Zero-line crossovers:**
  * Crossing above zero: Suggests recent prices have more weight (potential upward momentum)
  * Crossing below zero: Suggests older prices have more weight (potential downward momentum)
  * Multiple crossovers may indicate choppy, non-trending conditions

* **Extreme readings:**
  * High positive values: Recent prices significantly outweigh older prices
  * High negative values: Older prices significantly outweigh recent prices
  * The magnitude indicates the strength of the price distribution bias

* **Divergence analysis:**
  * Bullish divergence: Price makes lower lows while CG makes higher lows
  * Bearish divergence: Price makes higher highs while CG makes lower highs
  * These divergences can indicate potential shifts in price momentum

* **Mean reversion characteristics:**
  * CG tends to oscillate around zero over time
  * Extreme readings often precede moves back toward the center line
  * Can be used to identify potential reversal points

## Limitations and Considerations

* **Market conditions:** Most effective in cyclical markets; may provide less clear signals during strong trending periods
* **Whipsaw potential:** Can generate false signals during low-volatility, range-bound conditions
* **Parameter sensitivity:** Length setting significantly affects responsiveness and noise levels
* **Interpretation complexity:** Requires understanding of the balance point concept for proper interpretation
* **Complementary tools:** Best used with trend identification tools and volume confirmation for optimal results

The Center of Gravity works best when combined with other cycle analysis tools and should be part of a broader trading system that includes trend and momentum confirmation.

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | n+1 | 1 | n+1 |
| MUL | n | 3 | 3n |
| DIV | 1 | 15 | 15 |
| **Total** | **2n+2** | — | **~4n+16 cycles** |

*Where n = Length (default 10)*

**Default (n=10):** ~56 cycles per bar

**Breakdown:**
- Weighted sum Σ(i × price): n MUL + (n-1) ADD = 40 cycles
- Price sum Σ(price): (n-1) ADD = 9 cycles
- Division + centering: 1 DIV + 1 SUB = 16 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(n) | Full window iteration required (position weights) |
| Batch | O(n×m) | n = length, m = bars |

**Memory**: ~n×8 bytes (price buffer for lookback)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | ✅ | Weighted sum is dot product with constant weights |
| FMA | ✅ | `i × price + running_sum` pattern |
| Batch parallelism | ✅ | FIR structure allows full vectorization |

**SIMD Speedup (AVX2):** For n=10, weighted sum reduces from 10 MUL to ~2 vector ops (~5× speedup on dot product). Pre-computed weight vector [1,2,3,...,n] enables efficient `vfmadd` chains.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact weighted centroid calculation |
| **Timeliness** | 7/10 | FIR introduces group delay ≈ n/2 |
| **Overshoot** | 6/10 | Linear weights can amplify recent volatility |
| **Smoothness** | 7/10 | Moderate smoothing from averaging |

## References

* Ehlers, J. F. (2002). *Rocket Science for Traders: Digital Signal Processing Applications*. John Wiley & Sons.
* Ehlers, J. F. (2013). *Cycle Analytics for Traders: Advanced Technical Trading Concepts*. John Wiley & Sons.