# WAVG: Weighted Average

> *Weighted average assigns importance by position, giving recent or central observations a louder voice in the mean.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Wavg)                       |
| **Output range** | $0$ to $1$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [wavg.pine](wavg.pine)                       |

- The Weighted Average computes a rolling linearly-weighted mean where the most recent observation receives weight $N$ and the oldest receives weight...
- **Similar:** [WMA](../../trends_FIR/wma/wma.md), [EMA](../../trends_IIR/ema/ema.md) | **Trading note:** Weighted average with custom weights; flexible aggregation for composite indicators.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Weighted Average computes a rolling linearly-weighted mean where the most recent observation receives weight $N$ and the oldest receives weight 1, making it mathematically identical to the Weighted Moving Average (WMA) but categorized as a statistical measure. The implementation uses a circular buffer with an $O(1)$ incremental update scheme: rather than recomputing the full weighted sum each bar, it maintains running sums and adjusts them through add/subtract operations as values enter and exit the window. This makes WAVG one of the most efficient weighted estimators available, with constant per-bar cost regardless of the lookback period.

## Historical Context

The linearly-weighted average is one of the oldest weighted estimators, predating formal statistical theory. The concept of assigning decreasing importance to older observations appears in early actuarial work (17th-18th centuries) and was formalized in weather forecasting by the mid-19th century. In technical analysis, the Weighted Moving Average became popular through the work of Martin Pring and other chartists who sought a middle ground between the SMA (equal weights, excessive lag) and the EMA (exponential weights, infinite memory).

The linear weighting scheme assigns weight $w_i = i + 1$ to the $i$-th sample from oldest ($i = 0$) to newest ($i = N-1$). This produces a centroid (center of mass) that is biased toward recent data: the effective lag is $N/3$ bars compared to $(N-1)/2$ for the SMA. The triangular weight distribution means the most recent value contributes $2/(N+1)$ times the total weight, versus $1/N$ for the SMA.

The $O(1)$ update trick used in this implementation is well known in DSP: the weighted sum $W = \sum i \cdot x_i$ can be maintained incrementally by tracking the unweighted sum $S = \sum x_i$ and noting that when all indices shift by 1, $W_{\text{new}} = W_{\text{old}} - S_{\text{old}} + N \cdot x_{\text{new}}$.

## Architecture and Physics

The implementation uses a circular buffer of size `period` with three state variables:

- `weightedSum`: The current linearly-weighted sum $\sum_{i=1}^{n} i \cdot x_{(i)}$ where $(i)$ is position from oldest.
- `runningSum`: The unweighted sum $\sum x_i$ of all values in the buffer.
- `count`: The current fill level (increases during warmup, equals `period` at steady state).

**Per-bar update** ($O(1)$ operations):

1. **Remove departing value**: If the buffer position being overwritten contains a valid value, subtract it from `runningSum`.
2. **Shift weights down**: Subtract `runningSum` from `weightedSum`. This decrements every existing value's weight by 1 (equivalent to aging all observations).
3. **Add new value**: Add `srcVal` to `runningSum` and add `count * srcVal` to `weightedSum` (new value gets the highest weight).
4. **Store and advance**: Write to the circular buffer and advance the head pointer.

**Normalization**: The denominator is $n(n+1)/2$ where $n$ is the current count. This handles the warmup period naturally: when only $k < N$ values have been received, the result uses $k$-based weights.

## Mathematical Foundation

The linearly-weighted average with window size $n$:

$$\text{WAVG} = \frac{\sum_{i=0}^{n-1} (i + 1) \cdot x_{n-1-i}}{\sum_{i=0}^{n-1} (i + 1)} = \frac{\sum_{i=1}^{n} i \cdot x_i}{\frac{n(n+1)}{2}}$$

where $x_n$ is the most recent value (weight $n$) and $x_1$ is the oldest (weight 1).

**Effective lag** (centroid offset from current bar):

$$\text{lag} = \frac{\sum_{i=0}^{n-1} i \cdot (n - i)}{\sum_{i=0}^{n-1}(n-i)} = \frac{n-1}{3}$$

**O(1) incremental update** on arrival of new value $x_{\text{new}}$ and departure of $x_{\text{old}}$:

$$S_{\text{new}} = S_{\text{old}} - x_{\text{old}} + x_{\text{new}}$$

$$W_{\text{new}} = W_{\text{old}} - S_{\text{old}} + n \cdot x_{\text{new}}$$

$$\text{WAVG} = \frac{W_{\text{new}}}{n(n+1)/2}$$

**Weight distribution**: Weight of position $i$ from newest is $\frac{n - i}{n(n+1)/2}$. Most recent: $\frac{2}{n+1}$. Oldest: $\frac{2}{n(n+1)}$.

**Parameter constraints**: `period` $> 0$.

```
WAVG(source, period):
    // State variables (persistent)
    var buffer[period], head = 0, weightedSum = 0, runningSum = 0, count = 0

    srcVal = nz(source)
    oldest = buffer[head]

    if oldest is valid:
        runningSum -= oldest
    else:
        count += 1

    weightedSum -= runningSum        // shift all weights down by 1
    runningSum  += srcVal
    weightedSum += count * srcVal    // new value gets highest weight

    buffer[head] = srcVal
    head = (head + 1) % period

    denom = count * (count + 1) / 2
    return denom > 0 ? weightedSum / denom : srcVal
```


## Performance Profile

### Operation Count (Streaming Mode)

Weighted Average (WAVG) applies linearly increasing weights [1, 2, 3, ..., N] to the sliding window, using a precomputed weight sum denominator.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Weighted sum via FMA | N | 1 cy | ~N cy |
| Divide by weight sum | 1 | 4 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=14)** | **O(N)** | — | **~23 cy** |

O(N) per update; weight sum denominator N(N+1)/2 precomputed in constructor. Hot path is a FMA loop over the window — amenable to vectorization.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Weight vector generation | Yes | Static precomputed array, reused |
| Weighted dot product | Yes | Vector<double> FMA across window |
| Sliding window eviction | Partial | Ring buffer update is scalar |

Batch span path benefits from Vector<double> dot product for the weight application. AVX2 processes 4 doubles per cycle, giving ~3.5× speedup for N≥16.

## Resources

- Pring, M.J. "Technical Analysis Explained." 5th edition, McGraw-Hill, 2014.
- Murphy, J.J. "Technical Analysis of the Financial Markets." New York Institute of Finance, 1999.
- Oppenheim, A.V. & Schafer, R.W. "Discrete-Time Signal Processing." 3rd edition, Pearson, 2010.
- Haykin, S. "Adaptive Filter Theory." 5th edition, Pearson, 2013.