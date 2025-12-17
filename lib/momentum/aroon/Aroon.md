# Aroon Indicator

The Aroon indicator is a technical indicator used to identify trend changes in the price of an asset, as well as the strength of that trend. It consists of two lines: Aroon Up and Aroon Down.

## Calculation

The Aroon indicator measures the time between highs and the time between lows over a time period.

$$
\text{Aroon Up} = \frac{\text{Period} - \text{Days Since Period High}}{\text{Period}} \times 100
$$

$$
\text{Aroon Down} = \frac{\text{Period} - \text{Days Since Period Low}}{\text{Period}} \times 100
$$

$$
\text{Aroon Oscillator} = \text{Aroon Up} - \text{Aroon Down}
$$

Where:

- **Period**: The lookback period (typically 25).
- **Days Since Period High**: The number of days since the highest high within the period.
- **Days Since Period Low**: The number of days since the lowest low within the period.

## Interpretation

- **Aroon Up**: Measures the strength of the uptrend. Values close to 100 indicate a strong uptrend, while values close to 0 indicate a weak uptrend.
- **Aroon Down**: Measures the strength of the downtrend. Values close to 100 indicate a strong downtrend, while values close to 0 indicate a weak downtrend.
- **Crossovers**: When Aroon Up crosses above Aroon Down, it signals a potential uptrend. When Aroon Down crosses above Aroon Up, it signals a potential downtrend.
- **Extremes**: Values above 70 indicate a strong trend, while values below 30 indicate a weak trend.

## Usage

### C# code

```csharp
using QuanTAlib;

// Create Aroon with period 14
var aroon = new Aroon(14);

// Update with a TBar
var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
var result = aroon.Update(bar);

// Access values
var osc = result.Value;
var up = aroon.Up.Value;
var down = aroon.Down.Value;

Console.WriteLine($"Aroon Osc: {osc:F2}, Up: {up:F2}, Down: {down:F2}");
```

### Quantower

The Aroon indicator is available in Quantower as "Aroon".

- **Period**: The lookback period (default: 14).
- **Show cold values**: Whether to show values before the indicator is fully warmed up.

## References

- [Investopedia: Aroon Indicator](https://www.investopedia.com/terms/a/aroon.asp)
- Tushar Chande (1995)
