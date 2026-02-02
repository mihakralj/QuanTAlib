# CVI: Chaikin's Volatility

> "Volatility expansion precedes major moves—when the trading range starts widening, pay attention."

Chaikin's Volatility (CVI) measures the rate of change of the EMA-smoothed high-low trading range. Unlike traditional volatility measures that focus on returns, CVI directly tracks the expansion and contraction of price ranges over time. A positive CVI indicates expanding volatility (wider trading ranges), while a negative CVI signals contracting volatility (narrower ranges). This makes CVI particularly useful for identifying breakout conditions and market transitions.

## Historical Context

Marc Chaikin developed this indicator as part of his suite of technical analysis tools focused on price and volume dynamics. The indicator emerged from a practical observation: before significant price moves, the trading range often expands as buyers and sellers contest prices more aggressively.

Traditional volatility measures like standard deviation or ATR tell you the *level* of volatility, but CVI answers a different question: is volatility *increasing* or *decreasing*? This directional information can be more actionable for traders timing entries and exits.

The indicator combines two smoothing mechanisms: EMA smoothing on the raw high-low range to reduce noise, followed by a Rate of Change (ROC) calculation to measure the trend in volatility. This two-stage approach filters out day-to-day noise while capturing meaningful shifts in market character.

## Architecture & Physics

### 1. Range Calculation

The daily trading range is the difference between high and low prices:

$$
R_t = H_t - L_t
$$

where:

- $H_t$ = high price at time $t$
- $L_t$ = low price at time $t$
- $R_t$ = range at time $t$

This captures the full extent of intraday price movement.

### 2. EMA Smoothing

The range is smoothed using an Exponential Moving Average:

$$
EMA_t = \alpha \cdot R_t + (1 - \alpha) \cdot EMA_{t-1}
$$

where:

- $\alpha = \frac{2}{smoothLength + 1}$ (smoothing factor)
- Default $smoothLength = 10$ gives $\alpha \approx 0.182$

Equivalently, using FMA optimization:

$$
EMA_t = (R_t - EMA_{t-1}) \cdot \alpha + EMA_{t-1}
$$

### 3. Rate of Change Calculation

CVI is the percentage change of the smoothed range over the ROC period:

$$
CVI_t = \frac{EMA_t - EMA_{t-rocLength}}{EMA_{t-rocLength}} \times 100
$$

where:

- $rocLength$ = lookback period for ROC (default 10)
- Output is expressed as a percentage

### 4. Interpretation

$$
CVI_t = \begin{cases}
> 0 & \text{Expanding volatility (range increasing)} \\
= 0 & \text{Stable volatility (range unchanged)} \\
< 0 & \text{Contracting volatility (range decreasing)}
\end{cases}
$$

## Mathematical Foundation

### EMA Properties

**Smoothing Factor:**

$$
\alpha = \frac{2}{n + 1}
$$

| smoothLength | α | Half-life (bars) |
| :---: | :---: | :---: |
| 5 | 0.333 | 1.7 |
| 10 | 0.182 | 3.4 |
| 14 | 0.133 | 4.8 |
| 20 | 0.095 | 6.9 |

**Exponential Decay:**
The weight of a value $k$ bars ago is:

$$
w_k = \alpha (1 - \alpha)^k
$$

### ROC Properties

**Percentage Change Formula:**

$$
ROC = \frac{V_{current} - V_{prior}}{V_{prior}} \times 100
$$

**Symmetry Note:**
A +50% increase followed by -33% decrease returns to the original value. CVI preserves this percentage-based interpretation.

### Combined Effect

The warmup period is the sum of both smoothing requirements:

$$
WarmupPeriod = smoothLength + rocLength
$$

This ensures both the EMA has stabilized and enough history exists for the ROC calculation.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations after warmup:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (range) | 1 | 1 | 1 |
| FMA (EMA) | 1 | 4 | 4 |
| Buffer lookup | 1 | 3 | 3 |
| SUB | 1 | 1 | 1 |
| DIV | 1 | 15 | 15 |
| MUL (×100) | 1 | 3 | 3 |
| **Total** | — | — | **~27 cycles** |

The primary cost is the division for the ROC calculation.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Range calculation | 512 | 64 | 8× |
| EMA (sequential) | 512 | 512 | 1× |
| ROC calculation | 512 | 64 | 8× |

**Note:** EMA is inherently sequential due to the $EMA_{t-1}$ dependency. Total batch improvement is limited by this constraint.

### Memory Profile

- **Per instance:** ~80 bytes (state struct + RingBuffer header)
- **RingBuffer:** $(rocLength + 1) \times 8$ bytes for EMA history
- **Default (10,10):** ~80 + 88 = ~168 bytes per instance

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Direct measure of range dynamics |
| **Timeliness** | 7/10 | EMA introduces lag |
| **Smoothness** | 8/10 | Two-stage smoothing reduces noise |
| **Interpretability** | 9/10 | Clear meaning: + expanding, - contracting |
| **Robustness** | 8/10 | Handles gaps and spikes well |

## Validation

CVI is a classic indicator with multiple implementations:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **OoplesFinance** | N/A | Not implemented |
| **PineScript** | ✅ | Matches cvi.pine reference |
| **Manual** | ✅ | Validated against formula |

Note: While many libraries include ATR or standard deviation-based volatility, Chaikin's specific ROC-of-EMA-range formulation is less common.

## Common Pitfalls

1. **Warmup period**: CVI requires $smoothLength + rocLength$ bars before producing meaningful results. With defaults (10,10), this means 20 bars. The `IsHot` property indicates when warmup is complete.

2. **Zero/near-zero old EMA**: If the historical EMA value is very small (near zero), the division can produce extreme or infinite values. The implementation guards against this with an epsilon threshold.

3. **Interpretation of magnitude**: CVI values are percentages, not absolute ranges. A CVI of +50 means volatility increased 50% compared to $rocLength$ bars ago, regardless of the actual range values.

4. **Not a directional indicator**: CVI measures volatility direction, not price direction. High CVI can precede moves in either direction.

5. **Parameter sensitivity**:
   - Shorter $smoothLength$ = more responsive to range changes but noisier
   - Shorter $rocLength$ = more volatile CVI readings
   - Common combinations: (10,10), (14,10), (10,14)

6. **Requires OHLC data**: Unlike many indicators that work with closing prices only, CVI requires high and low prices. When using TValue input, the value is interpreted as a pre-calculated range.

7. **Negative ranges**: If TValue input has negative values (invalid for a range), the implementation substitutes the last valid value.

## Trading Applications

### Breakout Detection

High positive CVI values suggest expanding volatility, often preceding breakouts:

```
Entry signal: CVI crosses above +20 (volatility expanding)
Confirmation: Price breaks key support/resistance
```

### Consolidation Identification

Sustained negative CVI indicates contracting ranges, typical of consolidation:

```
Consolidation: CVI < -10 for several bars
Watch for: CVI reversal signaling potential breakout
```

### Volatility Regime Filter

CVI can filter other signals based on volatility conditions:

```
Trade breakouts when: CVI > 0 (expanding volatility)
Avoid range trades when: CVI rising sharply
```

## References

- Chaikin, M. (1966). "Stock Market Trading Systems." Various publications and interviews.
- Achelis, S. B. (2000). "Technical Analysis from A to Z." McGraw-Hill. Chapter on Chaikin Volatility.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.