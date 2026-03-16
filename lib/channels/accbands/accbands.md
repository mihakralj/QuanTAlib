# ACCBANDS: Acceleration Bands

> *Acceleration bands widen with high-low range, framing the expected reach of each bar's ambition.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`, `factor` (default 4.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [accbands.pine](accbands.pine)                       |

- Acceleration Bands construct a volatility envelope using the intra-bar high-low range rather than close-to-close standard deviation, creating chann...
- **Similar:** [BBands](../bbands/bbands.md), [KC](../kc/kc.md) | **Complementary:** ADX for trend strength | **Trading note:** Wider than Bollinger Bands; effective for breakout trading using high-low range volatility.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Acceleration Bands construct a volatility envelope using the intra-bar high-low range rather than close-to-close standard deviation, creating channels that accommodate the full price excursion of the underlying asset. Each bar's contribution to band width is normalized by price level ($w = (H-L)/(H+L)$), making the bands scale-invariant across instruments. Three independent Simple Moving Averages of the adjusted high, adjusted low, and close prices form the upper, lower, and middle bands respectively. Headley's original breakout rule declares a trend when price closes outside the bands for two consecutive bars.

## Historical Context

Price Headley developed Acceleration Bands and detailed them in *Big Trends in Trading* (Wiley, 2002). Headley observed that standard deviation bands often lag in fast-moving breakout scenarios because they require several bars of expanded volatility before the bands visibly widen. By incorporating High and Low prices directly into the band width calculation through a per-bar normalized range, he created a system that reacts immediately to range expansion.

The normalization $w = (H-L)/(H+L)$ is the key design choice. Dividing range by the sum of high and low produces a dimensionless ratio that is comparable across any price level. A $5 stock with a $0.50 range and a $500 stock with a $50 range both produce $w = 0.05$. The default factor of 4.0 was Headley's empirically determined value for equity markets on daily timeframes, matching the TA-Lib reference implementation.

## Architecture & Physics

### 1. Per-Bar Normalized Width

For each bar, compute the range as a fraction of total price:

$$w_t = \frac{H_t - L_t}{H_t + L_t}$$

When $H_t + L_t = 0$ (price is zero), $w_t = 0$ to prevent division by zero.

### 2. Adjusted Prices

The high and low are expanded by the normalized width scaled by the factor:

$$\text{AdjHigh}_t = H_t \times (1 + F \cdot w_t)$$

$$\text{AdjLow}_t = L_t \times (1 - F \cdot w_t)$$

### 3. Band Construction (Three SMAs)

$$\text{Upper}_t = \text{SMA}(\text{AdjHigh}, n)$$

$$\text{Lower}_t = \text{SMA}(\text{AdjLow}, n)$$

$$\text{Middle}_t = \text{SMA}(\text{Close}, n)$$

### 4. Complexity

Three independent circular buffers maintain running sums for $O(1)$ streaming updates. Each bar requires computing $w_t$, the two adjusted prices, and three buffer updates.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback period for the three SMAs ($n$) | 20 | $> 0$ |
| `factor` | Multiplier for normalized width ($F$) | 4.0 | $> 0$ |

### Breakout Rule (Headley)

A trend is confirmed when:

$$\text{Close}_t > \text{Upper}_t \quad \text{AND} \quad \text{Close}_{t-1} > \text{Upper}_{t-1}$$

(Two consecutive closes above the upper band.) Reverse logic for downside breakouts.

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | SMA of adjusted highs (resistance envelope) |
| `lower` | SMA of adjusted lows (support envelope) |
| `middle` | SMA of close (center line) |

## Performance Profile

### Operation Count (Streaming Mode)

ACCBANDS computes per-bar normalized width, two adjusted prices, and three independent SMA running sums:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD (H + L for denom) | 1 | 1 | 1 |
| SUB (H - L for range) | 1 | 1 | 1 |
| DIV (range / denom for w) | 1 | 15 | 15 |
| MUL (factor × w) | 1 | 3 | 3 |
| MUL (H × (1 + F·w), L × (1 - F·w)) | 2 | 3 | 6 |
| SUB (oldest from 3 running sums) | 3 | 1 | 3 |
| ADD (new value to 3 running sums) | 3 | 1 | 3 |
| DIV (sum / count, three SMAs) | 3 | 15 | 45 |
| **Total (hot)** | **15** | — | **~77 cycles** |

The three DIV operations dominate. When the denominator is zero ($H + L = 0$), a branch sets $w = 0$, adding one CMP.

### Batch Mode (SIMD Analysis)

The three SMA running sums are sequential. The per-bar width computation ($w$, adjusted prices) is independent across bars and vectorizable in a batch pre-pass:

| Optimization | Benefit |
| :--- | :--- |
| Width + adjusted price computation | Vectorizable with `Vector<double>` (ADD, SUB, MUL, DIV) |
| Three SMA running sums | Sequential; cannot parallelize across bars |
| Band output assembly | Trivial; already scalar from SMA |

## Resources

- **Headley, P.** *Big Trends in Trading*. Wiley, 2002. (Original Acceleration Bands specification)
- **TA-Lib** `TA_ACCBANDS` function. (Reference implementation with factor = 4.0)
