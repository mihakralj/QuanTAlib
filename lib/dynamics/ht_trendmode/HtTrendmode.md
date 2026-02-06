# HT_TRENDMODE: Hilbert Transform Trend Mode

## Historical Context

The Hilbert Transform Trend Mode indicator was developed by **John Ehlers** as part of his cycle analysis toolkit. It uses the Hilbert Transform—a signal processing technique—to determine whether price action is dominated by **trending behavior** or **cyclical/mean-reverting behavior**.

This implementation follows **TA-Lib's Ehlers-faithful algorithm** from his February 2002 publication "The Instantaneous Trendline." The key insight: trend mode is detected via multiple criteria including SineWave crossings, phase rate analysis, and price-trendline deviation.

## Architecture & Physics

### The Trend/Cycle Duality

Markets alternate between two fundamental states:

| State | Characteristic | Strategy |
|-------|---------------|----------|
| **Trend Mode (1)** | Directional momentum | Trend-following |
| **Cycle Mode (0)** | Mean-reverting oscillation | Range-trading |

The TA-Lib algorithm uses **four criteria** to determine trend mode:

1. **SineWave Crossings**: Reset trend counter when Sine crosses LeadSine
2. **Days in Trend**: Must exceed half the smooth period
3. **Phase Rate Check**: Normal phase change rate indicates cycle mode
4. **Price-Trendline Deviation**: ≥1.5% deviation forces trend mode

## Mathematical Foundation

### 1. Hilbert Transform Components

The indicator uses the same Hilbert Transform core as HT_DCPERIOD:

```
smooth_price = (4×P₀ + 3×P₁ + 2×P₂ + P₃) / 10

detrender = FIR(smooth_price) × bandwidth
Q1 = FIR(detrender) × bandwidth
I1 = detrender[3]

// Phasor rotation
I2 = I1 - jQ
Q2 = Q1 + jI
```

### 2. Period and DC Phase

```
Re = 0.2×(I2×I2[1] + Q2×Q2[1]) + 0.8×Re[1]
Im = 0.2×(I2×Q2[1] - Q2×I2[1]) + 0.8×Im[1]

period = 360 / (atan(Im/Re) × RAD2DEG)
smooth_period = 0.33×period + 0.67×smooth_period[1]

// DC Phase calculation
realPart = Σ sin(i × 360/dcPeriod) × smoothPrice[i]
imagPart = Σ cos(i × 360/dcPeriod) × smoothPrice[i]
dcPhase = atan(realPart/imagPart) × RAD2DEG + 90 + lag_compensation
```

### 3. SineWave Indicators

```
sine = sin(dcPhase × DEG2RAD)
leadSine = sin((dcPhase + 45) × DEG2RAD)
```

### 4. Trendline Calculation

```
// SMA over dominant cycle period
sma = average(price, dcPeriodInt)

// WMA smoothing
trendline = (4×sma₀ + 3×sma₁ + 2×sma₂ + sma₃) / 10
```

### 5. Trend Mode Decision (TA-Lib Algorithm)

```
trend = 1  // Assume trend by default

// Criterion 1: SineWave crossing resets counter
if (sine crosses leadSine):
    daysInTrend = 0
    trend = 0

daysInTrend++

// Criterion 2: Must be trending for half a cycle
if (daysInTrend < 0.5 × smoothPeriod):
    trend = 0

// Criterion 3: Normal phase rate → cycle mode
phaseChange = dcPhase - prevDcPhase
expectedChange = 360 / smoothPeriod
if (phaseChange > 0.67×expectedChange AND phaseChange < 1.5×expectedChange):
    trend = 0

// Criterion 4: Price deviation override
if (abs((smoothPrice - trendline) / trendline) >= 0.015):
    trend = 1
```

## Performance Profile

- **Complexity**: O(1) per update
- **Memory**: ~450 bytes state + circular buffers
- **Lookback**: 63 bars (TA-Lib compatible)

### Zero-Allocation Design

```csharp
[SkipLocalsInit]
public sealed class HtTrendmode : AbstractBase
{
    // All state in value types
    private State _state;
    private State _p_state;

    // Pre-allocated buffers for Hilbert Transform
    private readonly double[] _circBuffer;
    private readonly double[] _smoothPrice;
    private readonly double[] _priceHistory;
}
```

### Bar Correction Pattern

Supports streaming updates with correction:

```csharp
// New bar
var result = indicator.Update(price, isNew: true);

// Same bar, corrected price
var corrected = indicator.Update(newPrice, isNew: false);
```

## Usage

### Streaming

```csharp
var indicator = new HtTrendmode();

foreach (var bar in bars)
{
    var result = indicator.Update(bar.Close, isNew: true);
    
    if (indicator.TrendMode == 1)
    {
        // Use trend-following strategy
        ApplyMomentumStrategy();
    }
    else
    {
        // Use mean-reversion strategy
        ApplyRangeStrategy();
    }
}
```

### Batch

```csharp
var result = HtTrendmode.Calculate(closePrices);
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `TrendMode` | int | Current mode: 1=trend, 0=cycle |
| `SmoothPeriod` | double | Smoothed dominant cycle period [6-50] |
| `InstPeriod` | double | Instantaneous (unsmoothed) period |
| `DCPhase` | double | Dominant cycle phase in degrees |
| `Trendline` | double | WMA-smoothed SMA over cycle period |
| `DaysInTrend` | int | Days since last SineWave crossing |

## Interpretation

### Signal Interpretation

| Value | Mode | Interpretation |
|-------|------|----------------|
| **1** | Trend | Price is trending; momentum strategies preferred |
| **0** | Cycle | Price is oscillating; mean-reversion preferred |

### Common Patterns

1. **Trend Confirmation**: When TrendMode flips from 0→1 after a breakout
2. **Cycle Entry**: When TrendMode flips from 1→0 at potential reversal zones
3. **Mode Persistence**: Long runs of 1s indicate strong trends
4. **Mode Oscillation**: Rapid flipping indicates choppy markets

### Using Auxiliary Properties

```csharp
// Access the trendline for support/resistance
double trend = indicator.Trendline;

// Check how long in current trend
int duration = indicator.DaysInTrend;

// Use phase for timing entries
double phase = indicator.DCPhase;
```

## Validation

### Cross-Library Comparison

| Library | Function | Notes |
|---------|----------|-------|
| TA-Lib | `HT_TRENDMODE` | Reference implementation (matched) |
| TradingView | Built-in | PineScript version (differs) |

### Common Pitfalls

1. **Lag**: Hilbert Transform has inherent lag (~32-63 bars for reliable signal)
2. **Whipsaws**: Mode can flip rapidly in transitional markets
3. **Warmup**: Requires 63+ bars before valid output
4. **Division Safety**: Use epsilon checks to avoid division by zero

## References

- Ehlers, J.F. "The Instantaneous Trendline" (February 2002)
- Ehlers, J.F. "MESA and Trading Market Cycles" (2002)
- Ehlers, J.F. "Rocket Science for Traders" (2001)
- [TA-Lib HT_TRENDMODE Source](https://github.com/TA-Lib/ta-lib/blob/main/src/ta_func/ta_HT_TRENDMODE.c)
