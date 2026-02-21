# BBANDS: Bollinger Bands

Bollinger Bands construct a volatility-adaptive envelope around a Simple Moving Average using population standard deviation as the width measure. The bands expand during high-volatility periods and contract during consolidation, dynamically adapting to changing market conditions. Under Gaussian assumptions, $\pm 2\sigma$ contains approximately 95.4% of price action, but financial returns exhibit fat tails and volatility clustering, so the bands function more as a volatility-normalized reference frame than a strict probability envelope. The derived metrics %B (price position as a fraction of band width) and BandWidth (normalized band spread) extend the raw bands into a complete analytical toolkit.

## Historical Context

John Bollinger developed Bollinger Bands in the early 1980s while working as a market technician. He registered the name as a trademark in 1996 and published *Bollinger on Bollinger Bands* (McGraw-Hill, 2001). The indicator emerged from Bollinger's observation that fixed-percentage envelopes fail to account for changing volatility: a 5% envelope that works during quiet markets becomes useless during volatile phases, and vice versa.

Bollinger drew on statistical probability theory. Under the normal distribution, approximately 68% of observations fall within $\pm 1\sigma$, 95% within $\pm 2\sigma$, and 99.7% within $\pm 3\sigma$. By defaulting the multiplier to 2.0, he created bands targeting the 95% containment level. Chebyshev's inequality guarantees at least 75% containment at $\pm 2\sigma$ regardless of distribution shape. In practice, with fat-tailed market returns (typical kurtosis 4-6), expect 92-95% containment rather than 95.4%.

The "Squeeze" pattern (BandWidth at multi-period lows) became one of the most recognized technical analysis signals, predating and influencing the TTM Squeeze indicator. Walking the bands (price hugging the upper or lower band during trends) is a momentum signal, not a reversal signal. Bollinger Bands became one of the most widely adopted technical analysis tools, available in virtually every charting platform.

## Architecture & Physics

### 1. Middle Band (SMA)

$$\text{Middle}_t = \frac{1}{n} \sum_{i=0}^{n-1} x_{t-i}$$

### 2. Population Standard Deviation

Using the computational formula (running sums of $x$ and $x^2$):

$$\sigma_t = \sqrt{\frac{\sum x_i^2}{n} - \left(\frac{\sum x_i}{n}\right)^2}$$

Note: this is population standard deviation (divide by $n$), not sample standard deviation (divide by $n-1$). Bollinger specified population $\sigma$, and most reference implementations (TA-Lib, TradingView) use this convention.

### 3. Band Construction

$$\text{Upper}_t = \text{Middle}_t + k \cdot \sigma_t$$

$$\text{Lower}_t = \text{Middle}_t - k \cdot \sigma_t$$

### 4. Derived Metrics

**BandWidth** (normalized volatility measure):

$$\text{BandWidth}_t = \frac{\text{Upper}_t - \text{Lower}_t}{\text{Middle}_t}$$

**%B** (price position oscillator, 0-1 range when inside bands):

$$\%B_t = \frac{x_t - \text{Lower}_t}{\text{Upper}_t - \text{Lower}_t}$$

### 5. Complexity

The circular buffer maintains running sums of $x$ and $x^2$, enabling $O(1)$ computation of both mean and variance per bar. The square root for $\sigma$ is the most expensive operation.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback for SMA and standard deviation ($n$) | 20 | $> 0$ |
| `multiplier` | Number of standard deviations ($k$) | 2.0 | $> 0$ |
| `source` | Input price series | close | |

### Containment Guarantees

| Multiplier ($k$) | Normal Distribution | Chebyshev (any dist.) |
|:-:|:-:|:-:|
| 1.0 | 68.3% | 0% (trivial bound) |
| 2.0 | 95.4% | 75.0% |
| 3.0 | 99.7% | 88.9% |

### Pseudo-code

```
function BBANDS(source, period, multiplier):
    validate: period > 0, multiplier > 0

    // Circular buffer maintains running sums
    sum += source;  sumSq += source²
    oldest = buffer[head]
    if oldest exists: sum -= oldest; sumSq -= oldest²

    // SMA (middle band)
    middle = sum / count

    // Population standard deviation
    variance = max(0, sumSq/count - middle²)
    sigma = √variance
    dev = multiplier * sigma

    // Bands
    upper = middle + dev
    lower = middle - dev

    // Derived metrics
    bandwidth = (upper - lower) / middle
    percentB = (source - lower) / (upper - lower)

    return [middle, upper, lower, bandwidth, percentB]
```

### Output Interpretation

| Output | Range | Meaning |
|--------|-------|---------|
| `middle` | price-scale | SMA center line |
| `upper` | price-scale | Middle + $k\sigma$ |
| `lower` | price-scale | Middle - $k\sigma$ |
| `bandwidth` | $[0, \infty)$ | Normalized volatility; low values signal "squeeze" |
| `percentB` | typically $[0, 1]$ | $> 1$: above upper band; $< 0$: below lower band |

## Resources

- **Bollinger, J.** *Bollinger on Bollinger Bands*. McGraw-Hill, 2001. (Definitive reference)
- **Bollinger, J.** "Using Bollinger Bands." *Technical Analysis of Stocks & Commodities*, 1992.
- **Chebyshev, P.L.** "Des valeurs moyennes." *Journal de Mathématiques Pures et Appliquées*, 1867. (Distribution-free containment bound)
- **TA-Lib** `TA_BBANDS` function. (Population standard deviation reference implementation)
