# Vortex Indicator

> When bulls and bears clash, the Vortex measures the violence. Two opposing forces, one decisive signal.

The Vortex Indicator captures the directional momentum of price movement by measuring positive and negative trend movements relative to true range. Unlike directional indicators that rely on smoothing, Vortex uses pure ratio analysis over a rolling period, making it responsive yet stable.

## Historical Context

Etienne Botes and Douglas Siepman introduced the Vortex Indicator in a 2010 article for *Technical Analysis of Stocks & Commodities*. Inspired by the natural vortex patterns in water flow and the work of Viktor Schauberger, they designed a dual-line indicator that captures the essence of trend direction through geometric relationships between consecutive bars.

## Architecture & Physics

The Vortex Indicator is built on a simple geometric insight: in a strong uptrend, the current high tends to be far from the previous low. In a strong downtrend, the current low tends to be far from the previous high.

1. **Vortex Movement (VM)**: Measures directional distance.
   * **VM+**: Distance from current high to previous low (upward force).
   * **VM-**: Distance from current low to previous high (downward force).

2. **True Range (TR)**: The denominator that normalizes the movements.

3. **Vortex Index**: The ratio of summed VM to summed TR over $N$ periods.

### The Physics of Trend

* **VI+ > VI-**: Bullish momentum dominates. The market is reaching up.
* **VI- > VI+**: Bearish momentum dominates. The market is reaching down.
* **VI+ ≈ VI-**: Equilibrium. No clear trend; potential consolidation or reversal.
* **Crossover**: When VI+ crosses VI-, a trend change is signaled.

## Mathematical Foundation

The calculations are straightforward geometric relationships.

### Vortex Movement

$$ VM^+ = |High_t - Low_{t-1}| $$

$$ VM^- = |Low_t - High_{t-1}| $$

### True Range

$$ TR = \max(High_t - Low_t, |High_t - Close_{t-1}|, |Low_t - Close_{t-1}|) $$

### Vortex Indicator

$$ VI^+ = \frac{\sum_{i=1}^{N} VM^+_i}{\sum_{i=1}^{N} TR_i} $$

$$ VI^- = \frac{\sum_{i=1}^{N} VM^-_i}{\sum_{i=1}^{N} TR_i} $$

## Performance Profile

The implementation uses running sums for O(1) updates after the initial warmup period.

### Zero-Allocation Design

Three circular buffers maintain the VM+, VM-, and TR values. Running sums are updated incrementally:
- Add new value
- Subtract oldest value when buffer is full
- Compute ratio

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 8ns | 8ns / bar after warmup. |
| **Allocations** | 0 | Hot path is allocation-free. |
| **Complexity** | O(1) | Constant time updates with running sums. |
| **Accuracy** | 10/10 | Matches Skender reference implementation. |
| **Timeliness** | 9/10 | Responsive to trend changes. |
| **Overshoot** | 3/10 | Values typically 0.5-1.5, rarely extreme. |
| **Smoothness** | 7/10 | Period-based smoothing via summation. |

## Interpretation

### Crossover Signals

* **Bullish Crossover**: VI+ crosses above VI-. Indicates potential uptrend beginning.
* **Bearish Crossover**: VI- crosses above VI+. Indicates potential downtrend beginning.

### Reference Line

The value 1.0 serves as a natural reference:
- **VI+ > 1**: Strong upward pressure exceeds average true range.
- **VI- > 1**: Strong downward pressure exceeds average true range.
- **Both < 1**: Subdued market activity.

### Threshold Strategy

Some practitioners use thresholds for confirmation:
- **Strong Trend**: VI+ > 1.1 and VI+ > VI- (bullish) or VI- > 1.1 and VI- > VI+ (bearish).
- **Weak/No Trend**: Both VI+ and VI- below 0.9 or very close to each other.

## Validation

Validation is performed against industry-standard libraries.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Skender** | ✅ | Matches `GetVortex` (Pvi, Nvi). |
| **TA-Lib** | N/A | Not implemented in TA-Lib. |
| **Tulip** | N/A | Not implemented in Tulip. |

### Common Pitfalls

* **Period Selection**: Too short a period (< 7) creates noise; too long (> 28) creates excessive lag. 14-21 is typical.
* **False Crossovers**: In choppy markets, VI+ and VI- oscillate around each other, creating whipsaws. Use with trend filters.
* **Single Line Trading**: Don't use VI+ or VI- in isolation. The relationship between them is the signal.
* **Ignoring True Range**: Low TR periods (consolidation) can cause extreme VI values. Always consider the market context.

## References

* Botes, E., & Siepman, D. (2010). "The Vortex Indicator." *Technical Analysis of Stocks & Commodities*, January 2010.
* Wikipedia: [Vortex Indicator](https://en.wikipedia.org/wiki/Vortex_indicator)
