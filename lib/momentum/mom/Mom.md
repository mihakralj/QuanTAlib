# MOM: Momentum (Absolute Price Change)

> "The market's simplest question answered: how much has price moved in N bars? No ratios, no percentages. Just the raw delta."

MOM (Momentum) calculates the absolute price difference between the current value and the value N periods ago. It is the purest expression of directional price movement, returning a signed value in the same units as the input. Positive MOM indicates rising prices; negative indicates falling. This is functionally identical to ROC but with a configurable lookback period (default 10 vs ROC's convention), and maps directly to TA-Lib's `MOM` function.

## Historical Context

Momentum is arguably the oldest quantitative concept in technical analysis. Before oscillators, before moving averages, traders measured "how much has price changed" by simple subtraction. The term appears in Gerald Appel's work on MACD (1979) and in Martin Pring's "Technical Analysis Explained" as a foundation concept.

Different libraries handle the naming inconsistently:

- **TA-Lib / Tulip**: `MOM` = absolute change (this calculation)
- **TradingView / PineScript**: `ta.mom` = absolute change
- **QuanTAlib**: `Mom` = absolute change, `Roc` = absolute change (same formula, different default period), `Change` = percentage change

The implementation uses a ring buffer of size `period + 1` for O(1) streaming with zero allocations on the hot path.

## Architecture & Physics

### 1. Ring Buffer Storage

The indicator maintains a sliding window of `period + 1` values:

$$
\text{buffer} = [v_{t-n}, v_{t-n+1}, \ldots, v_{t-1}, v_t]
$$

where $n$ is the lookback period. Only the oldest and newest values participate in the calculation.

### 2. Absolute Change Calculation

$$
\text{MOM}_t = v_t - v_{t-n}
$$

The result is in the same units as the input (dollars, points, ticks). No normalization is applied.

### 3. State Management

The indicator uses `record struct State` with `_state` / `_p_state` pairs for bar correction:

```text
if isNew:
    _p_state = _state       // snapshot for rollback
else:
    _state = _p_state        // restore on correction
```

NaN/Infinity inputs are sanitized via last-valid-value substitution stored in `State.LastValid`.

## Mathematical Foundation

### Core Formula

$$
\text{MOM}_t = P_t - P_{t-n}
$$

where:

- $P_t$ = current price
- $P_{t-n}$ = price from $n$ periods ago
- Default $n = 10$

### Relationship to Other Momentum Variants

| Indicator | Formula | Output |
|-----------|---------|--------|
| **MOM** | $P_t - P_{t-n}$ | Absolute (price units) |
| **ROC** | $P_t - P_{t-n}$ | Absolute (same formula) |
| **ROCP** | $\frac{P_t - P_{t-n}}{P_{t-n}} \times 100$ | Percentage (%) |
| **ROCR** | $\frac{P_t}{P_{t-n}}$ | Ratio (dimensionless) |
| **CHANGE** | $\frac{P_t - P_{t-n}}{P_{t-n}}$ | Decimal fraction |

### Conversions

$$
\text{ROCP} = \frac{\text{MOM}}{P_{t-n}} \times 100
$$

$$
\text{ROCR} = \frac{P_t}{P_{t-n}} = \frac{\text{MOM}}{P_{t-n}} + 1
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | current - past |
| Buffer add | 1 | O(1) ring buffer |
| State copy | 1 | rollback support |
| **Total** | **~3 ops** | Extremely lightweight |

### Batch Mode (Span-based)

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Single subtraction |
| Total | O(n) | Linear scan |
| Memory | O(1) | No additional allocation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic, no approximation |
| **Timeliness** | 10/10 | Zero lag by definition |
| **Smoothness** | 3/10 | No smoothing, reflects raw volatility |
| **Simplicity** | 10/10 | Single subtraction |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | Matches exactly |
| **TA-Lib** | ✅ | MOM function matches |
| **Tulip** | ✅ | MOM matches exactly |
| **Ooples** | ✅ | Matches within tolerance |

## Common Pitfalls

1. **Unit confusion**: MOM returns absolute values in price units, not percentages. A MOM of 5 means price moved 5 dollars/points, not 5%.

2. **Scale dependency**: MOM values are not comparable across instruments with different price levels. Use ROCP or CHANGE for normalized comparisons.

3. **Warmup period**: The first `period` values return 0.0 because there is no historical reference point yet. `IsHot` becomes true after `period + 1` bars.

4. **Zero handling**: Unlike percentage-based variants, MOM has no division-by-zero risk.

5. **Sign interpretation**: Positive MOM indicates price increase over the lookback window; negative indicates decrease. The magnitude indicates the size of the move.

6. **ROC vs MOM naming**: In QuanTAlib, both `Mom` and `Roc` compute the same formula ($P_t - P_{t-n}$). The difference is the default period (MOM=10, ROC=10) and naming convention alignment with different library ecosystems.

## References

- Pring, M. J. (2014). "Technical Analysis Explained." McGraw-Hill.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- Appel, G. (2005). "Technical Analysis: Power Tools for Active Investors." FT Press.
- TA-Lib documentation: MOM function reference
- TradingView PineScript Reference: ta.mom
