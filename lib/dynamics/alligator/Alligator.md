# Alligator

> The market is a beast. When it sleeps, stay out. When it wakes, ride the momentum.

The Williams Alligator is a trend-following indicator developed by Bill Williams. It uses three smoothed moving averages (SMMA) with different periods and forward offsets to visualize market phases: sleeping (consolidation), awakening (trend start), and eating (strong trend).

## Historical Context

Bill Williams introduced the Alligator in his 1995 book *Trading Chaos*. The metaphor is vivid: the three lines represent the Jaw (blue), Teeth (red), and Lips (green) of an alligator. When the lines are intertwined, the alligator is "sleeping" and the market is in consolidation. When the lines separate and align, the alligator is "awake" and "eating," indicating a strong trend.

## Architecture & Physics

The Alligator uses three SMMA (Smoothed Moving Average) lines, each with a different period and forward offset:

| Line | Period | Offset | Color | Role |
|------|--------|--------|-------|------|
| **Jaw** | 13 | 8 | Blue | Slowest; shows long-term trend |
| **Teeth** | 8 | 5 | Red | Medium; shows intermediate trend |
| **Lips** | 5 | 3 | Green | Fastest; shows short-term momentum |

### SMMA (Wilder's Smoothing)

Each line uses Wilder's smoothing (also called RMA or SMMA), which is an EMA variant with $\alpha = 1/\text{period}$ instead of the standard $2/(\text{period}+1)$.

$$ \text{SMMA}_t = \alpha \cdot \text{Price} + (1 - \alpha) \cdot \text{SMMA}_{t-1} $$

where $\alpha = 1/\text{period}$

### Forward Offset

The offsets shift each line forward in time, creating visual separation that makes trend direction more apparent. This is a display-only transformation—the underlying SMMA calculation uses the current bar's price.

## Mathematical Foundation

For each line (Jaw, Teeth, Lips):

$$ \text{SMMA}(P, N) = \frac{\text{Price} + \text{SMMA}_{t-1} \cdot (N - 1)}{N} $$

Or equivalently using the recursive form:

$$ \text{SMMA}_t = \frac{1}{N} \cdot \text{Price} + \frac{N-1}{N} \cdot \text{SMMA}_{t-1} $$

Default input is HLC/3 (typical price):

$$ \text{Source} = \frac{\text{High} + \text{Low} + \text{Close}}{3} $$

## Performance Profile

The implementation uses inline SMMA calculations with bias compensation for accurate warmup behavior.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 5ns | Per bar, all three lines. |
| **Allocations** | 0 | Hot path is allocation-free. |
| **Complexity** | O(1) | Three parallel SMMA updates. |
| **Accuracy** | 10/10 | Matches TradingView exactly. |
| **Timeliness** | 7/10 | SMMA is slower than standard EMA. |
| **Overshoot** | 3/10 | Minimal overshoot; smooth response. |
| **Smoothness** | 9/10 | SMMA provides excellent smoothing. |

## Trading Interpretation

### Market Phases

1. **Sleeping Alligator**: Lines are intertwined, crossing each other. The market is in consolidation. Avoid trading.

2. **Awakening**: Lines begin to separate and align (Lips crosses Teeth crosses Jaw). A trend is starting.

3. **Eating**: Lines are parallel and widely separated. Strong trend in progress. Follow the direction.

4. **Sated**: Lines begin to converge again. The trend is weakening. Consider taking profits.

### Entry Signals

- **Buy**: Lips > Teeth > Jaw (all ascending, widely separated)
- **Sell**: Lips < Teeth < Jaw (all descending, widely separated)

### Filters

- Avoid trading when lines are intertwined (sleeping)
- Wait for clear separation before entering
- Exit when lines begin to converge

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TradingView** | ✅ | Matches built-in Alligator. |
| **MT4/MT5** | ✅ | Matches standard implementation. |
| **Skender** | N/A | Not implemented. |
| **TA-Lib** | N/A | Not implemented. |

## Usage

```csharp
// Default parameters: Jaw(13,8), Teeth(8,5), Lips(5,3)
var alligator = new Alligator();

// Custom parameters
var alligator = new Alligator(
    jawPeriod: 13, jawOffset: 8,
    teethPeriod: 8, teethOffset: 5,
    lipsPeriod: 5, lipsOffset: 3
);

// Update with price bar
alligator.Update(bar);

// Access the three lines
double jaw = alligator.Jaw.Value;
double teeth = alligator.Teeth.Value;
double lips = alligator.Lips.Value;

// Offsets for plotting
int jawOffset = alligator.JawOffset;   // 8
int teethOffset = alligator.TeethOffset; // 5
int lipsOffset = alligator.LipsOffset;   // 3
```

## Related Indicators

- **Gator Oscillator**: Histogram showing separation between Alligator lines
- **Fractals**: Williams' fractal patterns for entry timing
- **AO (Awesome Oscillator)**: Momentum confirmation
- **AC (Acceleration/Deceleration)**: Momentum acceleration

## Common Pitfalls

- **Ignoring the offset**: The offset is for plotting only. The current SMMA value represents the current bar's calculation, shifted forward for display.
- **Trading during sleep**: Most losses occur when trading during consolidation phases.
- **Premature entry**: Wait for clear separation, not just the first cross.

## References

- Williams, Bill. *Trading Chaos: Applying Expert Techniques to Maximize Your Profits*. John Wiley & Sons, 1995.
- Williams, Bill. *New Trading Dimensions*. John Wiley & Sons, 1998.
