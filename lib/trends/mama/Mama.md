# MAMA: MESA Adaptive Moving Average

## Overview and Purpose

The MESA Adaptive Moving Average (MAMA) is an advanced technical indicator that automatically adjusts its responsiveness based on market cycles. Developed by John Ehlers and introduced in 2001 in his book "MESA and Trading Market Cycles," MAMA applies sophisticated signal processing techniques from electrical engineering to market analysis.

Unlike other adaptive moving averages that typically adjust based on volatility or momentum, MAMA uses the Hilbert Transform to identify the dominant cycle period and phase of the market. This unique approach allows the indicator to adapt more intelligently to changing market conditions. MAMA consists of two lines - the primary MAMA line and a Following Adaptive Moving Average (FAMA) that serves as a confirmation signal and helps identify trend direction.

## Core Concepts

* **Cycle-based adaptation:** Uses Hilbert Transform techniques to detect dominant market cycles and adjust responsiveness accordingly
* **Phase measurement:** Calculates instantaneous phase angles to determine optimal adaptation speed rather than relying on simple volatility measures
* **Dual-line system:** Provides both a primary signal (MAMA) and a confirmation line (FAMA) for improved trend identification
* **Self-optimizing smoothing:** Automatically adjusts alpha (smoothing factor) based on detected market cycle characteristics

MAMA achieves its adaptive nature through sophisticated digital signal processing techniques that identify the market's dominant cycle length and phase. By measuring the rate of phase change, the indicator can determine precisely how fast it should adapt to price changes - becoming more responsive during trending markets with clear cycles and more stable during choppy, unclear conditions.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Fast Limit | 0.5 | Maximum adaptation rate | Lower for less sensitivity in volatile markets, increase for faster response |
| Slow Limit | 0.05 | Minimum adaptation rate | Raise for more stability in ranging markets, lower for more reactivity |
| Source | Close | Data point used for calculation | Change to HL2 or HLC3 for more balanced price representation |

**Pro Tip:** Many professional traders find that slight adjustments to the Fast Limit (0.4-0.5) while keeping the Slow Limit steady (0.05) creates an optimal balance between responsiveness and stability across most market conditions.

## Calculation and Mathematical Foundation

**Simplified explanation:**
MAMA works by identifying the market's current dominant cycle and how quickly that cycle is changing. It then uses this information to adjust how fast the moving average responds to price changes. The faster the market's cycle is changing, the more responsive MAMA becomes; the more stable the cycle, the smoother MAMA becomes.

**Technical formula:**

1. Apply initial smoothing and Hilbert Transform to generate in-phase (I) and quadrature (Q) components
2. Calculate instantaneous phase: Phase = arctan(Q/I)
3. Measure delta phase (phase change rate): DeltaPhase = Previous Phase - Current Phase
4. Calculate adaptive alpha: Alpha = FastLimit / (DeltaPhase/0.5 + 1), constrained between SlowLimit and FastLimit
5. Apply to price: MAMA = Alpha × Price + (1-Alpha) × Previous MAMA
6. Calculate following average: FAMA = 0.5 × Alpha × MAMA + (1-0.5×Alpha) × Previous FAMA

> 🔍 **Technical Note:** The Hilbert Transform implementation in MAMA uses specialized digital signal processing techniques to create a 90-degree phase-shifted version of the price series. This allows for precise measurement of instantaneous phase angles and cycle periods. The phase calculation is critical - when markets have a clear cycle, phase changes remain consistent, resulting in moderate adaptation; when cycles break down or change rapidly, phase shifts dramatically, causing MAMA to adjust its responsiveness accordingly.

## Interpretation Details

MAMA provides several key insights for traders:

* When MAMA crosses above FAMA, it often signals the beginning of an uptrend
* When MAMA crosses below FAMA, it often signals the beginning of a downtrend
* The distance between MAMA and FAMA indicates trend strength - wider separation suggests stronger trends
* The slope of both lines provides insight into trend momentum and potential continuation
* When MAMA and FAMA flatten and move together, it suggests consolidation or trend exhaustion
* The adaptation speed of MAMA itself offers insight into market cycle clarity

MAMA is particularly valuable for identifying trends in markets with varying cycle characteristics. Its cycle-based adaptation approach provides cleaner signals in markets that alternate between trending and cyclical behavior, making it especially useful for swing trading and position trading strategies.

## Limitations and Considerations

* **Market conditions:** May struggle in markets with very erratic or rapidly changing cycles
* **Computational complexity:** More resource-intensive than most moving averages due to Hilbert Transform calculations
* **Parameter sensitivity:** While adaptive, the Fast/Slow Limit settings still influence overall behavior
* **Mathematical complexity:** Requires proper implementation of digital signal processing concepts for accurate results
* **Complementary tools:** Works best when combined with momentum indicators or volume analysis for confirmation

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

// Create MAMA with default parameters
var mama = new Mama(fastLimit: 0.5, slowLimit: 0.05);

// Update with new price
var result = mama.Update(new TValue(DateTime.UtcNow, 100.0));
Console.WriteLine($"MAMA: {result.Value}");
Console.WriteLine($"FAMA: {mama.Fama.Value}");
```

### Static API (High Performance)

```csharp
// Calculate MAMA for an entire array
double[] prices = { ... };
double[] results = new double[prices.Length];

Mama.Batch(prices, results, fastLimit: 0.5, slowLimit: 0.05);
```

### Event-Driven

```csharp
var source = new TSeries();
var mama = new Mama(source);

mama.Pub += (item) => {
    Console.WriteLine($"MAMA: {item.Value}");
    Console.WriteLine($"FAMA: {mama.Fama.Value}");
};
```

## References

1. Ehlers, J. (2001). *MESA and Trading Market Cycles*. John Wiley & Sons.
2. Ehlers, J. (2002). "Using the MESA Adaptive Moving Average," *Technical Analysis of Stocks & Commodities*, Volume 20: June.
3. Ehlers, J. (2013). *Cycle Analytics for Traders*. Wiley Trading.
