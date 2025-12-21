# TEMA: Triple Exponential Moving Average

> "Patrick Mulloy looked at the lag of an EMA and took it personally. TEMA is what happens when you apply algebra to impatience."

The Triple Exponential Moving Average (TEMA) is a lag-reducing filter that combines a single, double, and triple EMA. Unlike a simple triple smoothing (which would be incredibly slow), TEMA uses a weighted combination of the three to cancel out the lag, resulting in an indicator that hugs price action tighter than a spandex cycling short.

## Historical Context

Introduced by Patrick Mulloy in *Technical Analysis of Stocks & Commodities* (Jan 1994), "Smoothing Data With Less Lag." Mulloy's goal was to replace the standard moving averages in MACD and other indicators to reduce the delay in signal generation.

## Architecture & Physics

TEMA is not just "EMA applied three times." That would be $EMA(EMA(EMA(x)))$. TEMA is a composite:
$$ TEMA = 3 \cdot EMA_1 - 3 \cdot EMA_2 + EMA_3 $$

This formula effectively projects the trend forward to compensate for the delay inherent in smoothing.

### Convergence Speed

Because of the aggressive weighting, TEMA converges (warms up) faster than a standard EMA. While an EMA takes $\approx 3.45(N+1)$ steps to converge to 99.9%, TEMA stabilizes quicker due to the subtraction terms canceling out the initial error.

## Mathematical Foundation

### 1. The Cascade

$$ EMA_1 = EMA(Price) $$
$$ EMA_2 = EMA(EMA_1) $$
$$ EMA_3 = EMA(EMA_2) $$

### 2. The Combination

$$ TEMA = (3 \times EMA_1) - (3 \times EMA_2) + EMA_3 $$

## Performance Profile

## Validation

Validated against TA-Lib (`TA_TEMA`) and Skender.Stock.Indicators.

### Common Pitfalls

1. **Overshoot**: TEMA is so responsive it can overshoot price turns, creating a "whiplash" effect in volatile markets.
2. **Noise**: By reducing lag, TEMA sacrifices some noise suppression. It is "nervous" compared to an SMA.
3. **Identity Crisis**: Often confused with T3 (Tillson). T3 is a generalized version; TEMA is specifically T3 with $v=1$.
