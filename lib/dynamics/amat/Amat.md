# AMAT: Archer Moving Averages Trends

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `fastPeriod` (default 10), `slowPeriod` (default 50)                      |
| **Outputs**      | Multiple series (Strength, FastEma, SlowEma)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `slowPeriod` bars                          |

### TL;DR

- The Archer Moving Averages Trends indicator is a triple-confirmation trend identification system that uses dual EMAs to produce discrete directiona...
- Parameterized by `fastperiod` (default 10), `slowperiod` (default 50).
- Output range: Varies (see docs).
- Requires `slowPeriod` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Archer Moving Averages Trends indicator is a triple-confirmation trend identification system that uses dual EMAs to produce discrete directional signals (+1 bullish, -1 bearish, 0 neutral). Unlike simple crossover systems that trigger on any intersection, AMAT requires alignment of three conditions: relative position (fast above/below slow), fast EMA direction (rising/falling), and slow EMA direction (rising/falling). This triple gate filters out the whipsaw endemic to single-condition crossover systems in ranging markets. A secondary output quantifies trend strength as the percentage separation between EMAs, providing a conviction metric for position sizing.

## Historical Context

AMAT emerged from concepts attributed to Mark Whistler ("Archer" in trading circles) and was formalized by Tom Joseph in 2009. The indicator addresses a specific failure mode of traditional MA crossover systems: they generate excessive false signals during sideways markets because a crossover only measures relative position, not directional agreement. A fast EMA can cross above a slow EMA while both are falling — technically a "bullish crossover" but practically meaningless. AMAT's innovation is requiring all three conditions to align before committing to a directional call. The neutral state (output = 0) captures market indecision explicitly: when EMAs disagree on direction or their relative position contradicts their momentum, AMAT stays flat. Markets trend roughly 30% of the time. AMAT is designed to identify that 30% with high confidence and stay silent the other 70%.

## Architecture & Physics

### 1. Dual EMA Computation

Two independent EMAs with bias compensation during warmup:

$$\text{EMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

where $\alpha = \frac{2}{N + 1}$

Bias compensation removes initialization distortion:

$$e_t = e_{t-1} \times (1 - \alpha), \quad \text{EMA}_{\text{comp}} = \frac{\text{EMA}_t}{1 - e_t}$$

### 2. Direction Detection

$$\text{Dir}_t = \begin{cases} +1 & \text{if } \text{EMA}_t > \text{EMA}_{t-1} \\ -1 & \text{if } \text{EMA}_t < \text{EMA}_{t-1} \\ 0 & \text{otherwise} \end{cases}$$

### 3. Triple-Confirmation Logic

$$\text{Trend}_t = \begin{cases} +1 & \text{if Fast} > \text{Slow} \;\land\; \text{FastDir} = +1 \;\land\; \text{SlowDir} = +1 \\ -1 & \text{if Fast} < \text{Slow} \;\land\; \text{FastDir} = -1 \;\land\; \text{SlowDir} = -1 \\ 0 & \text{otherwise} \end{cases}$$

### 4. Trend Strength

$$\text{Strength}_t = \frac{|\text{Fast}_t - \text{Slow}_t|}{\text{Slow}_t} \times 100$$

### 5. Complexity

- **Time:** $O(1)$ per bar — two EMA updates plus comparisons
- **Space:** $O(1)$ — scalar state only
- **Warmup:** slowPeriod bars

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N_f$ | fastPeriod | 10 | $N_f \geq 1$ |
| $N_s$ | slowPeriod | 50 | $N_s > N_f$ |

### Pseudo-code

```
Initialize:
  α_fast = 2 / (fastPeriod + 1)
  α_slow = 2 / (slowPeriod + 1)
  ema_fast = ema_slow = 0
  e_fast = e_slow = 1.0
  prev_fast = prev_slow = 0
  bar_count = 0

On each bar (price, isNew):
  if !isNew: restore previous state

  // EMA updates
  ema_fast = FMA(ema_fast, 1 - α_fast, α_fast × price)
  e_fast = e_fast × (1 - α_fast)
  fast = ema_fast / (1 - e_fast)

  ema_slow = FMA(ema_slow, 1 - α_slow, α_slow × price)
  e_slow = e_slow × (1 - α_slow)
  slow = ema_slow / (1 - e_slow)

  // Direction detection
  fastDir = fast > prev_fast ? +1 : fast < prev_fast ? -1 : 0
  slowDir = slow > prev_slow ? +1 : slow < prev_slow ? -1 : 0

  // Triple-confirmation
  if fast > slow AND fastDir == +1 AND slowDir == +1:
    trend = +1
  else if fast < slow AND fastDir == -1 AND slowDir == -1:
    trend = -1
  else:
    trend = 0

  // Strength
  strength = slow > 0 ? |fast - slow| / slow × 100 : 0

  prev_fast = fast
  prev_slow = slow

  output:
    Trend = trend       // +1, -1, or 0
    Strength = strength  // percentage
```

### Period Selection Guidelines

| Use Case | Fast | Slow | Ratio |
|----------|------|------|-------|
| Scalping | 5 | 13 | 1:2.6 |
| Swing | 10 | 50 | 1:5 |
| Position | 20 | 100 | 1:5 |
| Investment | 50 | 200 | 1:4 |

Fast periods too close to slow periods produce excessive neutral readings. A ratio of 1:4 to 1:5 provides effective separation.

### Discrete Output Properties

- **+1:** All three conditions align bullish — high-confidence uptrend
- **-1:** All three conditions align bearish — high-confidence downtrend
- **0:** Any disagreement — indeterminate; no position recommended
- **Strength:** Quantifies EMA separation as percentage of slow EMA; useful for position sizing but not directional signal

## Performance Profile

### Operation Count (Streaming Mode)

AMAT compares a fast EMA against a slow EMA to determine trend direction.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA × 2 (fast EMA, slow EMA updates) | 2 | 4 | 8 |
| CMP (fast > slow → trend = 1 else 0) | 1 | 1 | 1 |
| **Total** | **3** | — | **~9 cycles** |

Two independent EMA streams with a single comparison. One of the cheapest dynamics indicators: ~9 cycles per bar at steady state.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| EMA (fast) | **No** | Recursive IIR — sequential |
| EMA (slow) | **No** | Recursive IIR — sequential |
| Comparison | Yes | VCMPPD after both EMA arrays computed |

Both EMA passes are recursive and sequential. The final comparison step is trivially vectorizable once both arrays exist.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact EMA arithmetic; binary output eliminates rounding nuance |
| **Timeliness** | 7/10 | Slow EMA period determines lag; faster than SMA-based versions |
| **Smoothness** | 10/10 | Binary 0/1 output is maximally smooth by definition |
| **Noise Rejection** | 7/10 | EMA crossover can whipsaw in sideways markets |

## Resources

- Joseph, T. — AMAT trend confirmation methodology (2009)
- PineScript reference: `amat.pine` in indicator directory
