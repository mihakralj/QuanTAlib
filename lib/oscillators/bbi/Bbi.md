# BBI: Bulls Bears Index

> "Average four moving averages of doubling periods and you get a single line that votes on whether bulls or bears own the tape. It is a committee of trends, each watching a different time horizon, forced to agree on one number."

BBI (Bulls Bears Index) computes the arithmetic mean of four Simple Moving Averages with geometrically spaced periods (3, 6, 12, 24 by default). The result is a price-overlay line that captures trend consensus across ultra-short, short, medium, and long timeframes simultaneously. Price above BBI signals bullish dominance; price below BBI signals bearish control. The crossover point marks the regime boundary between long and short markets.

## Historical Context

BBI originated in the Chinese stock market technical analysis community, where it became a standard indicator on domestic trading platforms and textbooks. The Chinese name (多空指标, duō kōng zhǐbiāo, literally "long-short indicator") reflects its primary purpose: determining whether the market is in a bullish ("long") or bearish ("short") regime.

The specific period set (3, 6, 12, 24) follows a doubling progression that spans from intraday noise (3 bars) to nearly a full trading month (24 bars on a daily chart). This geometric spacing ensures each SMA captures a distinct frequency band of price behavior. The equal-weight average ($1/4$ each) treats all four timeframes as equally important, which is a deliberate design choice: no single timeframe dominates the composite signal.

BBI is functionally equivalent to a single weighted moving average with a composite kernel. The kernel is the sum of four rectangular windows of lengths 3, 6, 12, and 24, normalized by 4. This means each price bar contributes to the output based on how many of the four SMA windows it falls within: the most recent 3 bars are counted by all four SMAs (effective weight $4/4$), bars 4-6 by three SMAs ($3/4$), bars 7-12 by two ($2/4$), and bars 13-24 by one ($1/4$). The result is a stepped triangular-like kernel that naturally emphasizes recent prices without requiring explicit weight parameters.

## Architecture & Physics

### 1. Four Independent SMA Buffers

Four circular buffers of sizes $N_1, N_2, N_3, N_4$ maintain running sums for O(1) per-bar SMA updates:

$$
\text{SMA}_k[t] = \frac{1}{N_k} \sum_{i=0}^{N_k - 1} x_{t-i}, \quad k = 1, 2, 3, 4
$$

### 2. Composite Average

$$
\text{BBI}[t] = \frac{\text{SMA}_1[t] + \text{SMA}_2[t] + \text{SMA}_3[t] + \text{SMA}_4[t]}{4}
$$

### 3. Warmup Behavior

Each SMA produces valid output from bar 1 using available data (partial window). The composite BBI is valid from bar 1, with full-window accuracy achieved once all four SMAs have filled: $\text{WarmupPeriod} = \max(N_1, N_2, N_3, N_4) = 24$ bars with default parameters.

## Mathematical Foundation

**Individual SMAs with running sums:**

$$
S_k[t] = S_k[t-1] - x_{t-N_k} + x_t
$$

$$
\text{SMA}_k[t] = \frac{S_k[t]}{N_k}
$$

**Composite output:**

$$
\text{BBI}[t] = \frac{1}{4} \sum_{k=1}^{4} \text{SMA}_k[t]
$$

**Equivalent single-pass kernel:** Substituting the SMA definitions:

$$
\text{BBI}[t] = \frac{1}{4} \sum_{k=1}^{4} \frac{1}{N_k} \sum_{i=0}^{N_k - 1} x_{t-i} = \sum_{i=0}^{N_4 - 1} w_i \cdot x_{t-i}
$$

where the effective weight for lag $i$ is:

$$
w_i = \frac{1}{4} \sum_{k=1}^{4} \frac{\mathbf{1}_{[i < N_k]}}{N_k}
$$

For default periods $(3, 6, 12, 24)$:

| Lag range | Contributing SMAs | Weight |
| :--- | :---: | :---: |
| $0 \leq i < 3$ | All 4 | $\frac{1}{4}\left(\frac{1}{3} + \frac{1}{6} + \frac{1}{12} + \frac{1}{24}\right) \approx 0.1528$ |
| $3 \leq i < 6$ | SMA2, SMA3, SMA4 | $\frac{1}{4}\left(\frac{1}{6} + \frac{1}{12} + \frac{1}{24}\right) \approx 0.0694$ |
| $6 \leq i < 12$ | SMA3, SMA4 | $\frac{1}{4}\left(\frac{1}{12} + \frac{1}{24}\right) \approx 0.0313$ |
| $12 \leq i < 24$ | SMA4 only | $\frac{1}{4} \cdot \frac{1}{24} \approx 0.0104$ |

**Group delay:** The weighted centroid of the composite kernel determines the effective lag:

$$
\bar{d} = \frac{1}{4} \sum_{k=1}^{4} \frac{N_k - 1}{2} = \frac{1}{4} \cdot \frac{(3-1) + (6-1) + (12-1) + (24-1)}{2} = \frac{42}{8} = 5.25 \text{ bars}
$$

**Default parameters:** `p1 = 3`, `p2 = 6`, `p3 = 12`, `p4 = 24`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
// Four circular buffers with running sums
for k = 1 to 4:
    sum[k] -= buf[k][head[k]]
    sum[k] += src
    buf[k][head[k]] = src
    head[k] = (head[k] + 1) % period[k]
    sma[k] = sum[k] / min(count, period[k])

return (sma[1] + sma[2] + sma[3] + sma[4]) / 4
```

## Resources

- TradingView. "BBI - Bull and Bear Index." Community Scripts. (Standard implementation reference.)
- Chinese Securities Association. Technical analysis indicator specifications. (Origin of 3/6/12/24 period convention.)
- Binance Square. "BBI Indicator Usage Tutorial." (Modern application to cryptocurrency markets.)
