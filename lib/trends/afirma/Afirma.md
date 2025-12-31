# AFIRMA: Adaptive FIR Moving Average

> "When engineers realized that the mathematically perfect filter requires infinite memory, they reached for window functions—the art of graceful compromise between theory and reality."

AFIRMA is a high-quality FIR (Finite Impulse Response) low-pass filter using windowed sinc coefficients. It attempts to solve the fundamental paradox of technical analysis: the inverse relationship between smoothness and timeliness. The sinc function represents the theoretically optimal low-pass filter, but it extends to infinity. AFIRMA truncates it using window functions to create a practical, finite-length filter with excellent noise rejection.

AFIRMA does not offer "vision." It offers a convolution engine that trades CPU cycles for signal fidelity.

## Historical Context

In the 1970s, Box and Jenkins formalized ARMA models for econometrics. Simultaneously, digital signal processing (DSP) engineers were perfecting FIR filters using window functions to chop infinite Sinc waves into usable finite buffers.

The two worlds rarely spoke. Economists accepted lag; engineers accepted latency.

AFIRMA is a modern synthesis. It acknowledges that financial time series data is neither a pure radio wave nor a predictable economic cycle. It is a noisy, non-stationary mess. Traditional Moving Averages (SMA, EMA) use simple averaging which leaks high-frequency noise (lag) or reacts too violently (overshoot). AFIRMA uses the mathematically optimal Sinc function—the theoretical limit of a perfect low-pass filter—tempered by window functions to exist in reality.

## Architecture & Physics

AFIRMA operates through a convolution of the input signal with pre-computed windowed sinc coefficients. It is not a recursive loop (like EMA); it is a sliding weighted ruler.

### The Physics of the Sinc

The heart of the filter is the normalized sinc function:

$$ \text{sinc}(x) = \frac{\sin(\pi x)}{\pi x} $$

In the frequency domain, this is a brick wall: it passes everything below a certain frequency and kills everything above it. Perfect.

**The catch:** To achieve this perfection in the time domain, the sinc function must extend from negative infinity to positive infinity. Since systems do not have infinite RAM or a time machine, the function must be truncated.

### The Windowing Compromise

Chopping a sinc function abruptly (a "Rectangular" window) causes the Gibbs phenomenon—ringing artifacts where the filter oscillates wildly around sharp price changes. To prevent this, a "Window Function" gently tapers the edges of the filter to zero.

This is a trade-off:

1. **Main Lobe Width:** Determines frequency resolution (sharpness).
2. **Sidelobe Amplitude:** Determines spectral leakage (noise suppression).

You cannot optimize both simultaneously. This is the Heisenberg uncertainty principle applied to moving averages.

| Window | Main Lobe | Sidelobe | Use Case |
| :--- | :--- | :--- | :--- |
| **Rectangular** | Narrowest | Worst (-13 dB) | Maximum frequency resolution, high leakage |
| **Hanning** | Moderate | Good (-31 dB) | General purpose smoothing |
| **Hamming** | Moderate | Better (-42 dB) | Reduced leakage with decent resolution |
| **Blackman** | Wide | Excellent (-58 dB) | Low leakage, good for noisy data |
| **Blackman-Harris** | Widest | Best (-92 dB) | Minimum leakage, maximum smoothing |

The default Blackman-Harris window provides the best sidelobe suppression, making AFIRMA robust to impulsive noise in price data.

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

1. **Tap Inflation:** There is a temptation to set `Taps = 50` thinking it provides "more accuracy." It provides more lag. Keep taps between 5 and 15 for trading. If you need 50 taps, you don't need a filter; you need a weekly chart.

2. **Period vs. Taps Confusion:**
   - **Period** is the *what* (which frequencies to remove).
   - **Taps** is the *how* (how much math to throw at the removal).
   - Increasing Taps without changing Period just makes the filter steeper, not smoother.

3. **The "Cold Start" Reality:** AFIRMA is an FIR filter. It requires `Taps` number of bars to fill its buffer. The first `Taps-1` values are approximations. Check `.IsHot` before trading real money.

4. **Rectangular Windows:** Do not use the Rectangular window unless you enjoy seeing price oscillations that don't exist. The severe sidelobe leakage (-13 dB) introduces ringing artifacts around sharp price changes.

## See Also

- [ALMA](../alma/Alma.md) - Arnaud Legoux's Gaussian approach (similar goal, different math)
- [JMA](../jma/Jma.md) - Jurik's proprietary-turned-open filter (often slower, high overshoot)
- [SSF](../ssf/Ssf.md) - Ehlers Super Smoother (2-pole IIR, infinite memory)
