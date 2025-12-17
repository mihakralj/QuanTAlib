# MGDI - McGinley Dynamic Indicator

The McGinley Dynamic Indicator (MGDI) is a type of moving average that was designed to track the market better than existing moving average indicators. It is a technical indicator that improves upon moving average lines by adjusting for shifts in market speed.

## Core Concepts

The McGinley Dynamic Indicator solves the problem of varying market speeds by incorporating an automatic adjustment factor into its formula. This factor speeds up or slows down the indicator in trending or ranging markets.

* **Adaptive:** Automatically adjusts to the speed of the market.
* **Smoothing:** Minimizes price separation and "price hugs" to avoid whipsaws.
* **Lag Reduction:** Reduces lag compared to traditional moving averages like SMA or EMA.

## Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `period` | `int` | 14 | The number of periods used for the calculation (N). |
| `k` | `double` | 0.6 | A constant factor, typically 60% (0.6). |

## Formula

The formula for the McGinley Dynamic Indicator is:

$$
MGDI_i = MGDI_{i-1} + \frac{Price_i - MGDI_{i-1}}{k \times N \times (\frac{Price_i}{MGDI_{i-1}})^4}
$$

Where:

* $MGDI_i$ is the current McGinley Dynamic value.
* $MGDI_{i-1}$ is the previous McGinley Dynamic value.
* $Price_i$ is the current price.
* $N$ is the period (number of periods).
* $k$ is the constant factor (usually 0.6).

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

// Create the indicator with default parameters (Period=14, k=0.6)
var mgdi = new Mgdi(period: 14, k: 0.6);

// Update with a new value
var result = mgdi.Update(new TValue(DateTime.UtcNow, 100.0));

Console.WriteLine($"MGDI: {result.Value}");
```

### Span API (High Performance)

```csharp
using QuanTAlib;

double[] input = { ... }; // Your price data
double[] output = new double[input.Length];

// Calculate MGDI over the entire span
Mgdi.Batch(input, output, period: 14, k: 0.6);
```

### Event-Driven Usage

```csharp
using QuanTAlib;

var source = new ObservableSource();
var mgdi = new Mgdi(source, period: 14, k: 0.6);

mgdi.Pub += (result) => {
    Console.WriteLine($"New MGDI Value: {result.Value}");
};

// When source updates, mgdi will automatically calculate and publish
```

## Interpretation

* **Trend Identification:** Like other moving averages, the MGDI helps identify the trend direction. If the price is above the MGDI line, it suggests an uptrend. If below, a downtrend.
* **Support/Resistance:** The MGDI line can act as dynamic support or resistance levels.
* **Crossovers:** Price crossovers with the MGDI line can signal potential entry or exit points, though it is designed to be a better trend follower than a signal generator.
* **Market Speed:** Because it adjusts to market speed, it hugs prices more closely in fast markets and moves further away in slow markets, reducing false signals.

## References

* [Investopedia: McGinley Dynamic Indicator](https://www.investopedia.com/terms/m/mcginley-dynamic.asp)
* [Stock Indicators for .NET: McGinley Dynamic](https://dotnet.stockindicators.dev/indicators/Dynamic/)
