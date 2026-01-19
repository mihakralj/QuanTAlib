# CHEBY2 (Chebyshev Type II / Inverse Chebyshev)

A Chebyshev Type II filter (also known as Inverse Chebyshev) with O(1) complexity. Unlike the Type I filter, Type II is maximally flat in the passband (like Butterworth) but has equiripple in the stopband.

## Algorithm

The filter calculates 2nd order IIR coefficients based on partial fraction expansion of the Chebyshev Type II transfer function.

### Parameters

- `Period`: The cutoff period (related to cutoff frequency).
- `Attenuation`: The minimum attenuation in the stopband in decibels (dB), default 5.0.

### Formula

The coefficients are derived from the poles and zeros of the Chebyshev Type II polynomial:

1. Calculate filter parameters from period and attenuation.
2. Determine poles (`sigma_p +/- j*omega_p`) and zeros (`+/- j*omega_z`).
3. Construct the IIR filter coefficients (`a0, a1, a2, b0, b1, b2`).
4. Apply difference equation:
   $$ y[n] = b_0 x[n] + b_1 x[n-1] + b_2 x[n-2] - a_1 y[n-1] - a_2 y[n-2] $$

## Usage

```csharp
using QuanTAlib;

// Create a Cheby2 filter with period 10 and 5dB stopband attenuation
var filter = new Cheby2(period: 10, attenuation: 5.0);

// Update with a new value
var result = filter.Update(new TValue(DateTime.UtcNow, price));

// Access result
Console.WriteLine($"Filter value: {result.Value}");
```

## complexity

- **Time**: O(1) per update.
- **Space**: O(1) constant storage.
