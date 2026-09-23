# VWMACD: Volume-Weighted MACD

| Property | Value |
| --- | --- |
| **Category** | Momentum |
| **Inputs** | Close and volume |
| **Parameters** | `fastPeriod` (12), `slowPeriod` (26), `signalPeriod` (9) |
| **Outputs** | VWMACD, signal, and histogram |
| **Warmup** | `max(fastPeriod, slowPeriod) + signalPeriod - 2` bars |
| **PineScript** | [vwmacd.pine](vwmacd.pine) |

VWMACD replaces the two price EMAs in MACD with volume-weighted moving averages. High-volume bars therefore contribute more strongly to the fast and slow averages. The signal line is an EMA of the VWMACD line.

$$VWMACD_t = VWMA(C,V,fast) - VWMA(C,V,slow)$$

$$Signal_t = EMA(VWMACD,signal), \qquad Histogram_t = VWMACD_t - Signal_t$$

Non-positive volume is treated as zero. When the rolling volume sum is zero, the current close is used as the VWMA fallback.
