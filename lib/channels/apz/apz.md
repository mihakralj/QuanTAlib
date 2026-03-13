# APZ: Adaptive Price Zone

> *The adaptive price zone contracts in calm and expands in chaos, mapping volatility into a living boundary.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`, `multiplier` (default 2.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [apz.pine](apz.pine)                       |

- APZ constructs a volatility-adaptive envelope using double-smoothed exponential moving averages with an aggressive smoothing factor derived from $\...
- **Similar:** [BBands](../bbands/bbands.md), [KChannel](../kchannel/kchannel.md) | **Complementary:** RSI for overbought/oversold confirmation | **Trading note:** EMA-based deviation adapts faster than standard deviation bands; responsive to recent volatility changes.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

APZ constructs a volatility-adaptive envelope using double-smoothed exponential moving averages with an aggressive smoothing factor derived from $\sqrt{\text{period}}$, making it significantly faster than standard EMA-based channels. The center line is a double-EMA of price; the band width is a double-EMA of the high-low range, scaled by a multiplier. Designed specifically for mean-reversion trading in non-trending markets, APZ identifies overbought/oversold extremes where price is likely to reverse rather than continue. A closing price outside the zone signals an immediate overshoot, not a breakout.

## Historical Context

Lee Leibfarth created the Adaptive Price Zone and published it in *Technical Analysis of Stocks & Commodities* (September 2006) under the article "Trading With An Adaptive Price Zone." Leibfarth recognized that most indicators fail in choppy, range-bound markets: trend followers get whipsawed, and oscillators saturate at extremes. APZ fills this gap by adapting its bandwidth dynamically to statistical noise, allowing traders to fade extremes in consolidation phases.

The critical design decision is the square-root smoothing factor: $\alpha = 2 / (\sqrt{P} + 1)$. For a period of 20, $\sqrt{20} \approx 4.47$, producing $\alpha \approx 0.365$, which behaves like an EMA of period $\sim$3.5. This makes APZ extremely responsive compared to a standard 20-period EMA ($\alpha = 0.095$). The double-smoothing (EMA of EMA) adds some lag back, but the net result is still far faster than conventional approaches. The compound warmup compensator $e = \beta^{2t}$ (where $\beta = 1 - \alpha$) ensures accurate values from bar 1 without the typical EMA initialization bias.

## Architecture & Physics

### 1. Aggressive Smoothing Factor

$$\alpha = \frac{2}{\sqrt{P} + 1}, \quad \beta = 1 - \alpha$$

### 2. Center Line (Double-Smoothed EMA of Price)

First EMA:

$$\text{EMA1}_t = \alpha \cdot x_t + \beta \cdot \text{EMA1}_{t-1}$$

Second EMA (double-smoothing):

$$\text{Center}_t = \alpha \cdot \text{EMA1}_t + \beta \cdot \text{Center}_{t-1}$$

### 3. Adaptive Range (Double-Smoothed EMA of High-Low)

$$R_t = H_t - L_t$$

$$\text{EMA1R}_t = \alpha \cdot R_t + \beta \cdot \text{EMA1R}_{t-1}$$

$$\text{SmoothRange}_t = \alpha \cdot \text{EMA1R}_t + \beta \cdot \text{SmoothRange}_{t-1}$$

### 4. Band Construction

$$\text{Width}_t = F \cdot \text{SmoothRange}_t$$

$$\text{Upper}_t = \text{Center}_t + \text{Width}_t$$

$$\text{Lower}_t = \text{Center}_t - \text{Width}_t$$

### 5. Warmup Compensation

To eliminate EMA initialization bias, a compound compensator tracks the accumulated decay:

$$e_t = \beta^2 \cdot e_{t-1}, \quad e_0 = 1$$

During warmup ($e > 10^{-10}$):

$$\text{Center}_t^* = \frac{\text{Center}_t}{1 - e_t}, \quad \text{SmoothRange}_t^* = \frac{\text{SmoothRange}_t}{1 - e_t}$$

### 6. Complexity

$O(1)$ per bar: 4 EMA updates (2 for price, 2 for range), plus band arithmetic. The square root is computed once at initialization. No buffers required.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Input period ($P$); $\sqrt{P}$ used for smoothing | 20 | $> 0$ |
| `multiplier` | Band width factor ($F$) | 2.0 | $> 0$ |
| `source` | Input price series | close | |

### Effective EMA Period

The actual smoothing period experienced by the double-EMA is much shorter than the input period:

$$P_{\text{effective}} = \sqrt{P} \approx \frac{2}{\alpha} - 1$$

For $P = 20$: $P_{\text{eff}} \approx 4.47$. For $P = 100$: $P_{\text{eff}} \approx 10$.

### Output Interpretation

| Output | Description |
|--------|-------------|
| `center` | Double-smoothed EMA of price (fast center line) |
| `upper` | Center + scaled adaptive range (overbought zone) |
| `lower` | Center - scaled adaptive range (oversold zone) |

## Performance Profile

### Operation Count (Streaming Mode)

APZ runs four EMA updates (double-smoothed price + double-smoothed range) plus warmup compensation and band arithmetic:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (H - L for range) | 1 | 1 | 1 |
| FMA (EMA1 price) | 1 | 4 | 4 |
| FMA (EMA2 price → center) | 1 | 4 | 4 |
| FMA (EMA1 range) | 1 | 4 | 4 |
| FMA (EMA2 range → smoothRange) | 1 | 4 | 4 |
| MUL (multiplier × smoothRange) | 1 | 3 | 3 |
| ADD/SUB (center ± width) | 2 | 1 | 2 |
| **Total (hot)** | **8** | — | **~22 cycles** |

During warmup (compensator active):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL (e × β²) | 1 | 3 | 3 |
| SUB (1 - e) | 1 | 1 | 1 |
| DIV (center / compensator) | 1 | 15 | 15 |
| DIV (smoothRange / compensator) | 1 | 15 | 15 |
| CMP (e > threshold) | 1 | 1 | 1 |
| **Warmup overhead** | **5** | — | **~35 cycles** |

**Total during warmup:** ~57 cycles/bar; **Post-warmup:** ~22 cycles/bar.

### Batch Mode (SIMD Analysis)

All four EMA recursions are state-dependent, preventing SIMD parallelization across bars:

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | 4 hardware FMAs per bar; no software emulation |
| State locality | 4 EMA states + compensator fit in registers |
| Band arithmetic | Vectorizable in a post-pass across output arrays |

## Resources

- **Leibfarth, L.** "Trading With An Adaptive Price Zone." *Technical Analysis of Stocks & Commodities*, September 2006. (Original APZ specification)
- **Mulloy, P.** "Smoothing Data with Less Lag." *Technical Analysis of Stocks & Commodities*, February 1994. (Double-smoothed EMA theory)
