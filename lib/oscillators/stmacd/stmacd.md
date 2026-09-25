# STMACD: Stochastic MACD Oscillator

> *Traditional MACD produces absolute point-difference values that drift with price level. STMACD normalises each EMA against the high-low range, yielding a bounded, range-relative momentum reading that stays comparable across instruments and timeframes.*

| Property         | Value                                  |
| ---------------- | -------------------------------------- |
| **Category**     | Oscillator                             |
| **Inputs**       | TBar (high, low, close)                |
| **Parameters**   | `periods` (default 45), `fastLength` (default 12), `slowLength` (default 26), `signalLength` (default 9) |
| **Outputs**      | Dual: STMACD line + Signal line        |
| **Output range** | Unbounded (typically −100…+100)        |
| **Warmup**       | `periods` bars                         |
| **OB / OS**      | +10 / −10                              |
| **PineScript**   | [stmacd.pine](stmacd.pine)            |

- STMACD normalises the classic MACD line by the high-low price range over a lookback window. Each EMA is separately positioned within the range using stochastic normalisation, then the difference produces a percentage-scale momentum reading with a signal line for crossover detection.
- **Similar:** [MACD](../../momentum/macd/) (absolute MACD) | **Complementary:** ATR for volatility context | **Trading note:** Zero-line crossings for trend; ±10 overbought/oversold; STMACD × Signal crossovers for timing.
- No external validation libraries implement STMACD. Validated through self-consistency and behavioral testing against the published algorithm.

## Historical Context

Vitali Apirine published the Stochastic MACD Oscillator in the November 2019 issue of *Technical Analysis of Stocks & Commodities* magazine. The key insight is that standard MACD values are denominated in price units, making cross-instrument comparison meaningless. By normalising each EMA against the rolling high-low range (stochastic positioning), STMACD converts the MACD concept into a percentage-scale oscillator that remains comparable regardless of price level.

## Architecture & Mathematics

### Stage 1: Sliding-Window Extremes (2× MonotonicDeque)

| Value | Computation |
|---|---|
| **HHigh** | `Highest(High, periods)` — rolling max of high prices |
| **LLow** | `Lowest(Low, periods)` — rolling min of low prices |

Default `periods = 45`. MonotonicDeque provides O(1) amortised per bar.

### Stage 2: Dual EMA of Close

| EMA | Computation |
|---|---|
| **FastEMA** | `EMA(Close, fastLength)` — default 12 |
| **SlowEMA** | `EMA(Close, slowLength)` — default 26 |

Standard exponential moving average: $\alpha = \frac{2}{\text{period}+1}$.

### Stage 3: Stochastic Normalisation

$$\text{FastStoch} = \frac{\text{FastEMA} - \text{LLow}}{\text{HHigh} - \text{LLow}}$$

$$\text{SlowStoch} = \frac{\text{SlowEMA} - \text{LLow}}{\text{HHigh} - \text{LLow}}$$

$$\text{STMACD} = 100 \times (\text{FastStoch} - \text{SlowStoch}) = \frac{100 \times \text{MACD}}{\text{HHigh} - \text{LLow}}$$

### Stage 4: Signal Line

$$\text{Signal} = \text{EMA}(\text{STMACD},\;\text{signalLength})$$

Default `signalLength = 9`.

## Interpretation

| Condition | Meaning |
|---|---|
| STMACD > 0 | Fast EMA above slow EMA relative to range (bullish) |
| STMACD < 0 | Fast EMA below slow EMA relative to range (bearish) |
| STMACD > +10 | Overbought zone |
| STMACD < −10 | Oversold zone |
| STMACD crosses Signal | Momentum shift (trade signal) |

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Stage | Ops/bar |
|---|---|
| MonotonicDeque PushMax/PushMin | O(1) amortised |
| Two EMA updates | 4 multiply + 2 add |
| Stochastic normalisation | 2 subtract + 1 divide + 1 multiply |
| Signal EMA | 2 multiply + 1 add |
| **Total** | ~12 FLOPs + O(1) deque |

### Batch Mode

Uses `Highest.Batch` and `Lowest.Batch` for O(n) sliding window, then single-pass dual EMA + normalisation. Zero-allocation with `stackalloc` ≤ 256, `ArrayPool` above.

### Quality Metrics

| Property | Value |
|---|---|
| Warmup period | `periods` (default 45) |
| Output range | unbounded (typically −100…+100) |
| Allocations per bar | 0 (streaming) |
| NaN handling | last-valid substitution |
| Bar correction (isNew=false) | full deque rebuild |

## Validation

STMACD is validated through self-consistency tests (streaming ≡ batch) and behavioral tests (constant input → 0, trending signals, crossover detection).

### Behavioral Test Summary

| Category | Tests |
|---|---|
| Constructor validation | 5 |
| Streaming correctness | 8 |
| Batch parity | 3 |
| Edge cases (NaN, constant) | 4 |
| Quantower adapter | 10 |
| Cross-validation | 3 |

## Common Pitfalls

1. **Not a classic stochastic of MACD**: STMACD normalises each EMA separately against the H-L range, then takes the difference. The simplified form is `100 × MACD / PriceRange`.

2. **Unbounded in theory**: If EMA values fall outside the H-L range the output can exceed ±100, though this is rare in practice.

3. **Range compression**: In extremely tight ranges the denominator approaches zero, amplifying noise. The implementation clamps to 0 when range = 0.

4. **Window length matters**: The default `periods = 45` controls the normalisation range. Too short = noisy denominator; too long = stale range that doesn't reflect current volatility.
