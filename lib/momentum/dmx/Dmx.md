# DMX - Jurik Directional Movement Index

A high-fidelity replacement for Welles Wilder's DMI/ADX that eliminates the "lag vs. noise" trade-off. By substituting Jurik Moving Average (JMA) for standard smoothing, DMX delivers a cleaner, faster-reacting signal that combines trend direction and strength into a single bipolar oscillator.

## What It Does

DMX answers two questions simultaneously: "Which way is the market going?" and "How strong is the move?"

It takes the core logic of Wilder's Directional Movement System—comparing daily highs and lows to determine directional bias—but upgrades the engine. Instead of the sluggish Wilder's Smoothing (RMA), DMX uses the adaptive JMA to process the raw directional components.

The result is a single line that oscillates between -100 and +100:

- **Positive**: Bulls are in control.
- **Negative**: Bears are in control.
- **Magnitude**: The distance from zero indicates the intensity of the trend.

## Historical Context

Welles Wilder's DMI (1978) is a classic, but its reliance on simple smoothing makes it notoriously slow. To filter out noise, traders had to increase the period, which introduced unacceptable lag. Mark Jurik developed DMX to solve this specific problem. By applying his proprietary JMA smoothing to the raw directional vectors, he created an indicator that could filter noise *without* sacrificing timeliness.

## How It Works

The calculation mirrors the classic DMI structure but swaps the smoothing mechanism.

### The Math

1. **Raw Directional Movement**:
    We compare today's range to yesterday's range to see if the expansion is Up or Down.
    $$ \text{UpMove} = \text{High}_t - \text{High}_{t-1} $$
    $$ \text{DownMove} = \text{Low}_{t-1} - \text{Low}_t $$

    $$ DM^+_{raw} = \begin{cases} \text{UpMove} & \text{if } \text{UpMove} > \text{DownMove} \text{ and } \text{UpMove} > 0 \\ 0 & \text{otherwise} \end{cases} $$
    $$ DM^-_{raw} = \begin{cases} \text{DownMove} & \text{if } \text{DownMove} > \text{UpMove} \text{ and } \text{DownMove} > 0 \\ 0 & \text{otherwise} \end{cases} $$

2. **True Range (TR)**:
    The greatest of: current high-low, high-prevClose, or low-prevClose.

3. **JMA Smoothing** (The Secret Sauce):
    Instead of RMA, we use JMA to smooth the components.
    $$ DM^+_{smooth} = \text{JMA}(DM^+_{raw}, \text{Period}) $$
    $$ DM^-_{smooth} = \text{JMA}(DM^-_{raw}, \text{Period}) $$
    $$ \text{ATR}_{smooth} = \text{JMA}(\text{TR}, \text{Period}) $$

4. **Normalization**:
    $$ DI^+ = 100 \times \frac{DM^+_{smooth}}{\text{ATR}_{smooth}} $$
    $$ DI^- = 100 \times \frac{DM^-_{smooth}}{\text{ATR}_{smooth}} $$

5. **The Oscillator**:
    $$ \text{DMX} = DI^+ - DI^- $$

## Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `period` | `int` | 14 | The lookback period for the internal JMA smoothing. |

## Performance Profile

DMX is computationally heavier than standard DMI due to the JMA calculations, but remains efficient enough for high-frequency use.

- **Complexity**: $O(1)$ per update. The heavy lifting is done by the three internal JMA instances.
- **Memory**: Constant space. Stores state for the three JMAs and the previous bar.
- **Allocations**: Zero heap allocations during the `Update` cycle.

| Operation | Time Complexity | Space Complexity |
|-----------|-----------------|------------------|
| Update    | $O(1)$          | $O(1)$           |
| Batch     | $O(N)$          | $O(N)$           |

## Interpretation

DMX simplifies the traditional three-line DMI system (ADX, DI+, DI-) into a single, intuitive metric.

### 1. Direction (Zero Cross)

- **Bullish**: DMX crosses above 0.
- **Bearish**: DMX crosses below 0.
*Note: Because JMA is low-lag, these crossovers occur significantly earlier than in standard DMI.*

### 2. Strength (Magnitude)

- **Strong Trend**: Values > 25 (or < -25).
- **Extreme Trend**: Values > 50 (or < -50).
- **Chop/Range**: Values hovering near 0.

### 3. Divergence

- **Bearish Divergence**: Price makes a higher high, but DMX makes a lower high (momentum is waning).
- **Bullish Divergence**: Price makes a lower low, but DMX makes a higher low (selling pressure is exhausting).

## Architecture Notes

- **Composite Indicator**: `Dmx` is a wrapper around three `Jma` instances (`_jmaDMp`, `_jmaDMm`, `_jmaTR`).
- **Input Requirement**: Requires `TBar` (High, Low, Close) to calculate directional movement. It cannot be calculated from a simple stream of `double` values.
- **Initialization**: The first bar establishes the baseline; valid values begin appearing immediately, but the indicator warms up over the specified `period`.

## References

- Jurik Research: [DMX - Directional Movement Index](http://www.jurikres.com/catalog/ms_dmx.htm)
- Wilder, J. Welles. *New Concepts in Technical Trading Systems*. Trend Research, 1978.

## C# Usage

```csharp
using QuanTAlib;

// 1. Initialize
var dmx = new Dmx(period: 14);

// 2. Process a Bar
var bar = new TBar(DateTime.UtcNow, open: 100, high: 105, low: 95, close: 102, volume: 1000);
var result = dmx.Update(bar);

Console.WriteLine($"DMX: {result.Value:F2}");

// 3. Batch Calculation
var series = new TBarSeries();
// ... populate series ...
var dmxSeries = Dmx.Batch(series, period: 14);
