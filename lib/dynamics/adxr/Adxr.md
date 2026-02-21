# ADXR: Average Directional Movement Rating

The Average Directional Movement Rating is a smoothed version of ADX that dampens short-term fluctuations in trend strength by averaging the current ADX with a historical ADX value. This creates a doubly-lagged metric that sacrifices all timing utility in exchange for stable regime classification. ADXR answers one question: does the current market environment reward trend-following strategies? If ADXR is high, deploy momentum logic. If low, deploy mean-reversion. It is a strategic filter, not a tactical signal.

## Historical Context

J. Welles Wilder Jr. introduced ADXR alongside ADX in *New Concepts in Technical Trading Systems* (1978). His reasoning was pragmatic: ADX itself can be erratic during transitions between trending and ranging regimes, producing whipsaw readings that confuse systematic allocation. By averaging the current ADX with its value from $N-1$ bars ago, Wilder created a "momentum of momentum" indicator smoothed to geological stability. The ADXR found its architectural niche not as a trading signal but as a capital allocation filter — determining whether a trend-following system should be active at all. Its double lag (ADX already lags price; ADXR lags ADX) makes it useless for entry timing by design.

## Architecture & Physics

### 1. ADX Dependency

ADXR is a composite indicator that does not interact with price directly. It instantiates and maintains a full ADX pipeline internally:

$$\text{Price} \rightarrow \text{DM/TR} \rightarrow \text{RMA} \rightarrow \text{DI} \rightarrow \text{DX} \rightarrow \text{ADX} \rightarrow \text{ADXR}$$

### 2. Historical Buffer

A circular buffer of size $N$ stores historical ADX values, providing $O(1)$ access to the value from $N-1$ bars ago.

### 3. Rating Calculation

$$ADXR_t = \frac{ADX_t + ADX_{t-(N-1)}}{2}$$

The $N-1$ lag (rather than $N$) matches TA-Lib's reference implementation exactly.

### 4. Complexity

- **Time:** $O(1)$ per bar — ADX update plus one buffer lookup and one average
- **Space:** $O(N)$ — circular buffer for ADX history
- **Warmup:** $\approx 3N$ bars (ADX convergence + buffer fill)

## Mathematical Foundation

### Parameters

| Symbol | Parameter | Default | Constraint |
|--------|-----------|---------|------------|
| $N$ | period | 14 | $N \geq 2$ |

The period controls both the internal ADX calculation and the historical lookback depth.

### Pseudo-code

```
Initialize:
  adx = new Adx(period)
  adxBuffer = RingBuffer(period)
  bar_count = 0

On each bar (high, low, close, isNew):
  if !isNew: restore previous state

  // Full ADX pipeline
  adxValue = adx.Update(high, low, close, isNew)

  // Store in history
  adxBuffer.Add(adxValue)
  bar_count++

  // ADXR = average of current and (N-1)-lagged ADX
  if bar_count >= period:
    historicalAdx = adxBuffer[0]   // oldest value in buffer
    ADXR = (adxValue + historicalAdx) / 2.0
  else:
    ADXR = adxValue  // insufficient history

  output = ADXR
```

### Lag Analysis

| Component | Lag Source |
|-----------|-----------|
| DM → RMA | $\approx N$ bars (Wilder smoothing) |
| DX → ADX | $\approx N$ bars (second RMA) |
| ADX → ADXR | $N-1$ bars (historical average) |
| **Total effective lag** | $\approx 3N - 1$ bars |

For the default period of 14, ADXR carries roughly 41 bars of effective lag. This is a feature, not a limitation — it ensures that only sustained regime changes register in the output.

### Regime Classification

| ADXR Value | Interpretation |
|------------|----------------|
| < 20 | Sustained range-bound; favor mean-reversion |
| 20–25 | Ambiguous regime; reduce position sizing |
| > 25 | Sustained trending; favor momentum strategies |

## Resources

- Wilder, J.W. — *New Concepts in Technical Trading Systems* (Trend Research, 1978)
- PineScript reference: `adxr.pine` in indicator directory
