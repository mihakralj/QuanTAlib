# AO - Awesome Oscillator

The Awesome Oscillator (AO) is a momentum indicator used to measure market momentum. It calculates the difference between a 34-period and 5-period Simple Moving Average (SMA) of the median prices (High + Low) / 2.

## Formula

$$Median Price = \frac{High + Low}{2}$$

$$AO = SMA(Median Price, 5) - SMA(Median Price, 34)$$

Where:

- $SMA$ is the Simple Moving Average.

## Usage

### C# Code

```csharp
using QuanTAlib;

// Create AO with default periods (5, 34)
var ao = new Ao();

// Or specify custom periods
var aoCustom = new Ao(5, 34);

// Update with a bar
var result = ao.Update(bar);

// Result contains the AO value
Console.WriteLine($"AO: {result.Value}");
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| fastPeriod | int | 5 | The period for the fast SMA. |
| slowPeriod | int | 34 | The period for the slow SMA. |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| Last | TValue | The latest calculated AO value. |
| IsHot | bool | Indicates if the indicator has enough data to be valid (slow period reached). |
| Name | string | The name of the indicator, e.g., "Ao(5,34)". |

## Methods

| Method | Description |
|--------|-------------|
| Update(TBar bar) | Updates the indicator with a new bar. |
| Update(TValue val) | Updates the indicator with a new value (assumed to be Median Price). |
| Reset() | Resets the indicator state. |
