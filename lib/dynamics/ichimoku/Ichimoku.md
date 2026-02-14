# ICHIMOKU: Ichimoku Kinko Hyo (One Glance Equilibrium Chart)

> "Five lines, two clouds, one chart. Goichi Hosoda spent thirty years developing a system that tells you trend, momentum, support, and resistance simultaneously. Most traders use it for about thirty seconds before getting confused."

Ichimoku Kinko Hyo is a comprehensive trend-following system developed by Japanese journalist Goichi Hosoda (pen name Ichimoku Sanjin), published in 1969 after three decades of research. It provides five distinct components that together reveal trend direction, momentum, support/resistance levels, and potential future price zones. The "cloud" (Kumo) formed between Senkou Span A and B is particularly valued for identifying trend strength and equilibrium zones.

## Historical Context

Hosoda began developing Ichimoku in 1935, enlisting university students to hand-calculate the indicator across historical data long before computers made such work trivial. He published the complete system in a seven-volume series in 1969. The indicator remained largely unknown outside Japan until the 1990s, when it gained international recognition through the work of traders and analysts who translated and popularized the system.

The original parameters (9, 26, 52) were calibrated to the Japanese trading week: 9 days (1.5 weeks), 26 days (one month of 6-day trading weeks), and 52 days (two months). While modern 5-day trading weeks would suggest different values, the original parameters remain standard due to their widespread adoption and self-fulfilling nature in liquid markets.

The system is unique among technical indicators in projecting values into the future (Senkou spans displaced 26 periods forward) and into the past (Chikou span displaced 26 periods back), providing a temporal dimension that most indicators lack.

## Architecture & Physics

### 1. Tenkan-sen (Conversion Line)

The short-term equilibrium, calculated as the midpoint of the highest high and lowest low over the Tenkan period:

$$
\text{Tenkan}_t = \frac{\max(H, \text{tenkanPeriod}) + \min(L, \text{tenkanPeriod})}{2}
$$

This is not a moving average but a midpoint of the price range, making it responsive to breakouts rather than smooth trends.

### 2. Kijun-sen (Base Line)

The medium-term equilibrium, calculated identically but over a longer period:

$$
\text{Kijun}_t = \frac{\max(H, \text{kijunPeriod}) + \min(L, \text{kijunPeriod})}{2}
$$

Kijun-sen serves as the primary trend reference and key support/resistance level.

### 3. Senkou Span A (Leading Span A)

The average of Tenkan and Kijun, displaced forward:

$$
\text{SenkouA}_t = \frac{\text{Tenkan}_t + \text{Kijun}_t}{2} \quad \text{(plotted at } t + \text{displacement)}
$$

Forms the first boundary of the cloud. More reactive than Senkou Span B.

### 4. Senkou Span B (Leading Span B)

The long-term midpoint, displaced forward:

$$
\text{SenkouB}_t = \frac{\max(H, \text{senkouBPeriod}) + \min(L, \text{senkouBPeriod})}{2} \quad \text{(plotted at } t + \text{displacement)}
$$

Forms the second boundary of the cloud. Flatter and more stable than Span A.

### 5. Chikou Span (Lagging Span)

The current close price, displaced backward:

$$
\text{Chikou}_t = P_t \quad \text{(plotted at } t - \text{displacement)}
$$

Confirms trend by comparing current price to historical context.

### 6. State Management

The indicator implements `ITValuePublisher` directly (not `AbstractBase`) and manages its own ring buffer arrays for high/low tracking. State rollback uses `_state` / `_p_state` with full buffer snapshots (`_highBuffer` / `_p_highBuffer`, `_lowBuffer` / `_p_lowBuffer`).

## Mathematical Foundation

### Component Summary

| Component | Formula | Default Period | Displacement |
|-----------|---------|---------------|-------------|
| Tenkan-sen | $(HH_9 + LL_9) / 2$ | 9 | None |
| Kijun-sen | $(HH_{26} + LL_{26}) / 2$ | 26 | None |
| Senkou A | $(\text{Tenkan} + \text{Kijun}) / 2$ | N/A | +26 forward |
| Senkou B | $(HH_{52} + LL_{52}) / 2$ | 52 | +26 forward |
| Chikou | Close price | N/A | -26 backward |

where $HH_n$ = highest high over $n$ periods, $LL_n$ = lowest low over $n$ periods.

### Cloud (Kumo) Interpretation

$$
\text{Cloud}_{bullish} \iff \text{SenkouA} > \text{SenkouB}
$$

$$
\text{Cloud}_{bearish} \iff \text{SenkouA} < \text{SenkouB}
$$

$$
\text{Cloud thickness} = |\text{SenkouA} - \text{SenkouB}|
$$

### Default Parameters

| Parameter | Default | Original Meaning |
|-----------|---------|-----------------|
| tenkanPeriod | 9 | 1.5 trading weeks (6-day week) |
| kijunPeriod | 26 | 1 trading month |
| senkouBPeriod | 52 | 2 trading months |
| displacement | 26 | 1 trading month forward/back |

### Warmup

$$
\text{WarmupPeriod} = \max(\text{tenkanPeriod}, \text{kijunPeriod}, \text{senkouBPeriod})
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Min/Max scan | 3 | Tenkan, Kijun, SenkouB windows |
| ADD | 3 | Midpoint calculations |
| DIV | 3 | Midpoint /2 |
| Buffer update | 2 | High + low ring buffers |
| State copy | 2 | Buffer snapshots for rollback |
| **Total** | **~11 ops** | Plus O(n) for min/max scans |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact min/max arithmetic |
| **Timeliness** | 6/10 | Midpoint-based, moderate lag |
| **Smoothness** | 5/10 | Step-like behavior on breakouts |
| **Complexity** | 4/10 | Five components, displacement logic |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Skender** | ✅ | All 5 components match |
| **TA-Lib** | N/A | No Ichimoku function |
| **Tulip** | N/A | No Ichimoku function |
| **Ooples** | ✅ | Matches within tolerance |
| **TradingView** | ✅ | Matches PineScript reference |

## Common Pitfalls

1. **Displacement is charting-only**: The implementation computes current values for Senkou A/B. The forward displacement (plotting these values 26 bars ahead) is the responsibility of the charting layer, not the indicator.

2. **TBar input required**: Ichimoku operates on OHLC bars, not single values. It implements `ITValuePublisher` directly and accepts `TBar` input, not `TValue`.

3. **Multiple outputs**: The indicator exposes five separate properties (Tenkan, Kijun, SenkouA, SenkouB, Chikou). The `Last` property returns Kijun-sen as the primary reference.

4. **Not a moving average**: Tenkan and Kijun are midpoints of high-low ranges, not averages of closing prices. They respond to range breakouts, not gradual price changes.

5. **Cloud twist**: When Senkou A crosses Senkou B, the cloud color changes. This is a leading signal because the spans are displaced forward.

6. **Chikou confirmation**: Chikou Span is simply the current close plotted 26 bars back. When Chikou is above the price from 26 bars ago, the trend is confirmed bullish.

7. **Parameter sensitivity**: Changing from the standard (9, 26, 52) parameters alters the equilibrium model. Some practitioners use (7, 22, 44) for crypto markets with 7-day trading weeks.

## References

- Hosoda, G. (1969). "Ichimoku Kinko Hyo." Tokyo, Japan.
- Patel, M. (2010). "Trading with Ichimoku Clouds." Wiley.
- Elliott, N. (2007). "Ichimoku Charts: An Introduction to Ichimoku Kinko Clouds." Harriman House.
- StockCharts.com: "Ichimoku Cloud" Technical Analysis documentation.
- Investopedia: "Ichimoku Cloud Definition and Uses."
