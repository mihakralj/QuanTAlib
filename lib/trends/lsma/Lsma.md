# LSMA (Least Squares Moving Average)

The Least Squares Moving Average (LSMA), also known as the Moving Linear Regression or End Point Moving Average, calculates the linear regression line for a specified period and returns the value at the current bar (or a projected point). Unlike traditional moving averages that simply average past prices, LSMA fits a straight line to the data to minimize the sum of squared errors, providing a better representation of the trend direction and strength.

## Core Concepts

- **Linear Regression:** Fits a line $y = mx + b$ to the price data over the lookback period.
- **Trend Following:** The slope of the regression line indicates the trend direction.
- **Reduced Lag:** By projecting the line to the current bar (or future), LSMA reacts faster to price changes than SMA or EMA.
- **Projection:** Can project the value into the future (positive offset) or past (negative offset).

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `period` | `int` | 14 | The number of bars to include in the regression calculation. |
| `offset` | `int` | 0 | The offset from the current bar. 0 = current bar, >0 = future projection, <0 = past value. |

## Formula

For a period $n$, we fit a line $y = mx + b$ where $x$ represents the time index ($0$ to $n-1$).

The slope $m$ and intercept $b$ are calculated as:

$$ m = \frac{n \sum(xy) - \sum x \sum y}{n \sum(x^2) - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{n} $$

The LSMA value is then calculated at the desired offset:

$$ LSMA = b + m \times (n - 1 + \text{offset}) $$

*Note: In the implementation, we may adjust the coordinate system (e.g., $x=0$ as current bar) for computational efficiency, but the geometric result is identical.*

## C# Implementation

### Standard Usage

```csharp
using QuanTAlib;

// Create LSMA with period 14
var lsma = new Lsma(14);

// Update with new values
var result = lsma.Update(new TValue(DateTime.Now, 100.0));
Console.WriteLine($"LSMA: {result.Value}");
```

### With Offset

```csharp
// Create LSMA with period 14 and offset 1 (project 1 bar into future)
var lsma = new Lsma(14, offset: 1);
```

### Span API (High Performance)

```csharp
double[] input = { ... };
double[] output = new double[input.Length];

// Calculate LSMA in-place
Lsma.Batch(input, output, period: 14);
```

### Bar Correction

```csharp
var lsma = new Lsma(14);

// Update for the current bar
lsma.Update(new TValue(time, 100.0));

// Correction for the same bar (e.g., market data update)
lsma.Update(new TValue(time, 101.0), isNew: false);
```

## Interpretation

- **Trend Direction:** If LSMA is moving up, the trend is bullish. If moving down, the trend is bearish.
- **Crossovers:** Price crossing above LSMA can be a buy signal; crossing below can be a sell signal.
- **Support/Resistance:** LSMA often acts as dynamic support or resistance in trending markets.
- **Slope:** The steepness of the LSMA line indicates the strength of the trend.

## References

- [Linear Regression in Technical Analysis](https://www.investopedia.com/terms/l/linearregression.asp)
- [Least Squares Moving Average](https://www.tradingview.com/support/solutions/43000502584-least-squares-moving-average-lsma/)
