# AFIRMA: Autoregressive Finite Impulse Response Moving Average

> "When ARMA met FIR at a signal processing conference and they had a baby with cubic spline DNA. The result filters noise like a surgeon and tracks price like a stalker."

AFIRMA is a hybrid smoothing filter that combines three signal processing techniques: autoregressive (AR) modeling, finite impulse response (FIR) filtering with windowed sinc coefficients, and cubic spline fitting for the leading edge. The result is a filter that achieves superior noise reduction while maintaining signal fidelity and minimizing lag.

## Historical Context

AFIRMA emerged from the intersection of econometric time series analysis (ARMA models from Box-Jenkins methodology, circa 1970) and digital signal processing (FIR filters with window functions). The combination addresses a fundamental problem: traditional moving averages either lag badly (SMA, EMA) or introduce ringing artifacts (sharp cutoff filters). AFIRMA uses the mathematically optimal sinc function—the ideal low-pass filter impulse response—tempered by window functions that trade off main lobe width against sidelobe suppression.

## Architecture & Physics

AFIRMA operates through a convolution of the input signal with pre-computed windowed sinc coefficients.

### The Sinc Function

The sinc function is the impulse response of an ideal low-pass filter:

$$ \text{sinc}(x) = \begin{cases} 1 & \text{if } x = 0 \\ \frac{\sin(x)}{x} & \text{otherwise} \end{cases} $$

In practice, the sinc function extends infinitely—inconvenient for real-time processing. AFIRMA truncates it to a finite number of taps and applies a window function to minimize the resulting spectral leakage.

### Window Functions

Window functions control the trade-off between frequency resolution (main lobe width) and spectral leakage (sidelobe suppression).

| Window | Main Lobe | Sidelobe | Use Case |
| :--- | :--- | :--- | :--- |
| **Rectangular** | Narrowest | Worst (-13 dB) | Maximum frequency resolution, high leakage |
| **Hanning** | Moderate | Good (-31 dB) | General purpose smoothing |
| **Hamming** | Moderate | Better (-42 dB) | Reduced leakage with decent resolution |
| **Blackman** | Wide | Excellent (-58 dB) | Low leakage, good for noisy data |
| **Blackman-Harris** | Widest | Best (-92 dB) | Minimum leakage, maximum smoothing |

The default Blackman-Harris window provides the best sidelobe suppression, making AFIRMA robust to impulsive noise in price data.

### Cubic Spline Component

The ARMA polynomial coefficients are precomputed during initialization to support least-squares cubic fitting at the leading edge. This reduces end-point distortion common in FIR filters, where the filter "sees" incomplete data at the boundaries.

## Mathematical Foundation

### 1. Windowed Sinc Coefficients

For tap $k$ of $N$ total taps:

$$ w_k = W(k) \cdot \text{sinc}\left(\frac{\pi (k - c)}{P}\right) $$

Where:

- $c = \frac{N-1}{2}$ is the center tap
- $P$ is the period parameter
- $W(k)$ is the window function value at tap $k$

### 2. Window Functions

**Hanning:**
$$ W(k) = 0.5 - 0.5 \cos\left(\frac{2\pi k}{N-1}\right) $$

**Hamming:**
$$ W(k) = 0.54 - 0.46 \cos\left(\frac{2\pi k}{N-1}\right) $$

**Blackman:**
$$ W(k) = 0.42 - 0.5 \cos\left(\frac{2\pi k}{N-1}\right) + 0.08 \cos\left(\frac{4\pi k}{N-1}\right) $$

**Blackman-Harris:**
$$ W(k) = 0.35875 - 0.48829 \cos\left(\frac{2\pi k}{N-1}\right) + 0.14128 \cos\left(\frac{4\pi k}{N-1}\right) - 0.01168 \cos\left(\frac{6\pi k}{N-1}\right) $$

### 3. Convolution

$$ \text{AFIRMA}_t = \frac{\sum_{k=0}^{N-1} w_k \cdot P_{t-k}}{\sum_{k=0}^{N-1} w_k} $$

## Parameters

| Parameter | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| **Period** | - | ≥ 1 | Controls the cutoff frequency. Higher values = more smoothing. |
| **Taps** | 6 | ≥ 1 (odd preferred) | Filter length. More taps = sharper frequency response. |
| **Window** | BlackmanHarris | Enum | Window function for sidelobe control. |

### Parameter Selection Guide

- **Period**: Start with half your expected cycle length. For intraday on 1-minute bars with 20-minute cycles, use Period=10.
- **Taps**: Use odd numbers (5, 7, 9...) for symmetric response. More taps = more lag but sharper cutoff. 6-12 is typical.
- **Window**: Blackman-Harris for noisy data, Hamming for faster response, Rectangular only for experimentation.

## Usage

### Streaming (Real-time)

```csharp
var afirma = new Afirma(period: 10, taps: 7, window: Afirma.WindowType.BlackmanHarris);

foreach (var bar in marketData)
{
    var smoothed = afirma.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"{bar.Time}: {smoothed.Value:F4}");
}
```

### Batch Processing

```csharp
var series = new TSeries(timestamps, prices);
var smoothed = Afirma.Batch(series, period: 10, taps: 7);
```

### Span API (Zero-Allocation)

```csharp
ReadOnlySpan<double> prices = GetPrices();
Span<double> output = stackalloc double[prices.Length];

Afirma.Batch(prices, output, period: 10, taps: 7);
```

### Event-Driven (Chaining)

```csharp
var source = new TSeries();
var afirma = new Afirma(source, period: 10, taps: 7);

// AFIRMA automatically updates when source changes
source.Add(new TValue(DateTime.UtcNow, 100.0));
Console.WriteLine(afirma.Last.Value);
```

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~50 ns/bar | O(n) per update where n = taps |
| **Allocations** | 0 | Zero-allocation in hot paths |
| **Complexity** | O(taps) | Linear in filter length |
| **Accuracy** | 9 | Excellent noise reduction |
| **Timeliness** | 7 | Lower lag than equivalent SMA |
| **Overshoot** | 2 | Minimal with proper window selection |
| **Smoothness** | 9 | Very smooth output |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Internal** | ✅ | Batch, Streaming, and Span modes match |
| **Mathematical** | ✅ | Variance reduction verified |

AFIRMA is a QuanTAlib-specific implementation. No direct external library comparison is available, but internal consistency across all API modes has been verified.

## Window Type Comparison

For the same Period and Taps, different windows produce different smoothing characteristics:

| Window | Smoothness | Responsiveness | Best For |
| :--- | :--- | :--- | :--- |
| Rectangular | Low | Highest | Testing/comparison only |
| Hanning | Medium | High | General use |
| Hamming | Medium-High | Medium-High | Balanced applications |
| Blackman | High | Medium | Noisy data |
| BlackmanHarris | Highest | Lower | Very noisy data, maximum smoothing |

## Common Pitfalls

1. **Too Many Taps**: More taps mean more lag. Don't use 50 taps "just because." Start with 5-9.

2. **Period vs. Taps Confusion**: Period controls smoothness (like EMA period). Taps control filter sharpness. They're independent parameters.

3. **Rectangular Window**: Almost never the right choice for financial data. The severe sidelobe leakage introduces ringing.

4. **Cold Values**: AFIRMA needs `taps` bars of history to be fully warmed up. The `IsHot` property indicates when the filter is primed.

## See Also

- [ALMA](../alma/Alma.md) - Gaussian-weighted moving average with offset
- [CONV](../conv/Conv.md) - General convolution filter
- [SSF](../ssf/Ssf.md) - Ehlers Super Smooth Filter (2-pole IIR)
