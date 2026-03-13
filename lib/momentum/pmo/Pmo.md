# PMO: Price Momentum Oscillator

> *Double-smooth the rate of change and you get something that actually tells you where momentum is headed, not where it was five bars ago.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `timePeriods` (default DefaultTimePeriods), `smoothPeriods` (default DefaultSmoothPeriods), `signalPeriods` (default DefaultSignalPeriods)                      |
| **Outputs**      | Single series (Pmo)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `timePeriods + smoothPeriods` bars                          |
| **PineScript**   | [pmo.pine](pmo.pine)                       |

- PMO (Price Momentum Oscillator), developed by Carl Swenlin at DecisionPoint, is a double-smoothed 1-bar rate of change.
- **Similar:** [MACD](../macd/Macd.md), [TSI](../tsi/Tsi.md) | **Complementary:** Signal line for crossovers | **Trading note:** Price Momentum Oscillator; double-smoothed ROC. Decisionpoint.com creation.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

PMO (Price Momentum Oscillator), developed by Carl Swenlin at DecisionPoint, is a double-smoothed 1-bar rate of change. It applies two custom EMA passes to a percentage ROC, producing a momentum oscillator that is smoother than raw ROC yet more responsive than triple-smoothed alternatives like TRIX. The custom EMA uses $\alpha = 2/N$ rather than the standard $2/(N+1)$, and seeds with the SMA of the first N values. PMO oscillates around zero: positive values indicate upward momentum, negative values indicate downward momentum.

## Historical Context

Carl Swenlin introduced PMO through DecisionPoint.com as a refinement of the standard rate of change. The insight was that raw ROC (percentage change) is too noisy for reliable signal generation, but standard smoothing methods introduce too much lag. Swenlin's solution was a two-stage custom EMA pipeline applied to a 1-bar ROC, scaled by a factor of 10 after the first smoothing stage.

The implementation details matter: Swenlin specified $\alpha = 2/N$, not the standard EMA formula $2/(N+1)$. This subtle difference produces a slightly more responsive filter. Both Skender.Stock.Indicators and OoplesFinance implement this custom alpha, confirming the specification.

PMO is frequently used with a signal line (an EMA of the PMO itself) to generate crossover signals, similar to MACD. The default parameters (35, 20, 10) provide a balance between responsiveness and smoothness on daily charts.

## Architecture & Physics

### 1. One-Bar Percentage ROC

$$
\text{ROC}_t = \left(\frac{P_t}{P_{t-1}} - 1\right) \times 100
$$

This is always a 1-bar lookback regardless of parameters. The percentage form normalizes across price levels.

### 2. First Custom EMA (ROC Smoothing)

$$
\text{RocEma}_t = \text{CustomEMA}(\text{ROC}, \text{timePeriods}) \times 10
$$

The custom EMA uses $\alpha_1 = 2 / \text{timePeriods}$ and is seeded with the SMA of the first N ROC values. The $\times 10$ scaling amplifies the signal to a more readable range.

### 3. Second Custom EMA (PMO Smoothing)

$$
\text{PMO}_t = \text{CustomEMA}(\text{RocEma}, \text{smoothPeriods})
$$

The second pass uses $\alpha_2 = 2 / \text{smoothPeriods}$, also seeded with SMA. This produces the final PMO value.

### 4. State Management

The indicator uses `record struct State` with 12 fields tracking both EMA pipelines, seeding progress, and bar correction state. The `_state` / `_p_state` pattern enables rollback for streaming bar corrections.

## Mathematical Foundation

### Core Formulas

**Step 1 - Percentage ROC (1-bar):**

$$
\text{ROC}_t = \left(\frac{P_t}{P_{t-1}} - 1\right) \times 100
$$

**Step 2 - Custom EMA smoothing:**

The custom EMA differs from standard EMA:

| Property | Standard EMA | Custom EMA (PMO) |
|----------|-------------|-----------------|
| Alpha | $\frac{2}{N+1}$ | $\frac{2}{N}$ |
| Seed | First value | SMA of first N values |

$$
\text{CustomEMA}_t = \alpha \cdot x_t + (1 - \alpha) \cdot \text{CustomEMA}_{t-1}
$$

**Step 3 - Scale and second smooth:**

$$
\text{RocEma}_t = \text{CustomEMA}_1(\text{ROC}_t) \times 10
$$

$$
\text{PMO}_t = \text{CustomEMA}_2(\text{RocEma}_t)
$$

### Default Parameters

| Parameter | Default | Purpose |
|-----------|---------|---------|
| timePeriods | 35 | First EMA smoothing of 1-bar ROC |
| smoothPeriods | 20 | Second EMA smoothing for PMO |
| signalPeriods | 10 | Signal line EMA (future use) |

### Warmup

$$
\text{WarmupPeriod} = \text{timePeriods} + \text{smoothPeriods}
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| DIV | 1 | ROC percentage calculation |
| MUL | 3 | alpha multiplications + scale |
| ADD/SUB | 4 | EMA updates + ROC |
| State copy | 1 | rollback support |
| **Total** | **~9 ops** | Lightweight double-EMA |

### Batch Mode (Span-based)

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Fixed operations per bar |
| Total | O(n) | Linear scan |
| Memory | O(1) | No additional allocation beyond state |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Custom EMA matches DecisionPoint spec |
| **Timeliness** | 7/10 | Double smoothing adds moderate lag |
| **Smoothness** | 8/10 | Substantially smoother than raw ROC |
| **Simplicity** | 6/10 | Two-stage pipeline with custom alpha |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | Matches within 1e-9 tolerance |
| **TA-Lib** | N/A | No PMO function |
| **Tulip** | N/A | No PMO function |
| **Ooples** | ✅ | Matches within 1e-6 tolerance |

## Common Pitfalls

1. **Custom alpha confusion**: PMO uses $\alpha = 2/N$, not the standard $2/(N+1)$. Using standard EMA alpha produces different results that do not match the DecisionPoint specification.

2. **SMA seeding**: The custom EMA must be seeded with the SMA of the first N values, not with the first value. This affects the convergence behavior during warmup.

3. **Scale factor**: The $\times 10$ multiplier is applied after the first EMA pass, not before. Misplacing this scaling produces values off by an order of magnitude.

4. **1-bar ROC only**: PMO always uses a 1-bar ROC regardless of the timePeriods parameter. The timePeriods parameter controls only the first EMA smoothing length.

5. **Division by zero**: When `PrevClose` is zero, the ROC calculation would produce Infinity. The implementation guards against this with last-valid-value substitution.

6. **Warmup length**: PMO requires `timePeriods + smoothPeriods` bars before producing stable values. Early values are heavily influenced by the SMA seed.

7. **Signal line**: The signalPeriods parameter is reserved for future signal line implementation. Currently only the PMO line is computed.

## References

- Swenlin, C. "DecisionPoint Price Momentum Oscillator (PMO)." DecisionPoint.com.
- StockCharts.com: "DecisionPoint Price Momentum Oscillator (PMO)" Technical Analysis documentation.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.