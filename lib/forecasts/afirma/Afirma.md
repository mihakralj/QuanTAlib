# AFIRMA: Autoregressive FIR Moving Average

> "Standard Moving Averages assume linear or exponential weights. AFIRMA asks: what if we used signal processing window functions instead?"

AFIRMA is a Windowed Weighted Moving Average (WMA) that replaces the standard linear weighting of a WMA with weights derived from signal processing window functions (Hanning, Hamming, Blackman, Blackman-Harris). This approach allows for specific frequency response characteristics tailored to the type of noise in the data.

Unlike ALMA, which uses a Gaussian distribution with offset, AFIRMA uses standard Digital Signal Processing (DSP) windows to achieve excellent smoothing with controlled spectral leakage.

## Historical Context

Moving averages have traditionally been limited to Simple (Rectangular window), Weighted (Triangular window), or Exponential (Recursive). The DSP world had long solved the problem of finite filter design using window functions to minimize spectral leakage (ringing).

AFIRMA applies these coefficients directly to the price series. It is effectively an FIR filter where the coefficients are determined solely by the chosen window function. It simplifies the design of FIR filters for traders who don't want to calculate coefficients manually.

## Architecture & Physics

AFIRMA is a standard convolution filter (FIR). It maintains a sliding window of the last $P$ prices and calculates a weighted average.

### The Window Weights

Instead of linear weights ($1, 2, 3...$), AFIRMA generates weights ($w_k$) using a cosine-sum series:

$$ w_k = \sum_{m=0}^{M} (-1)^m a_m \cos\left(\frac{2\pi m k}{P-1}\right) $$

where $P$ is the period and $k$ is the index from $0$ to $P-1$.

These windows are designed to taper the edges of the inputs to zero, reducing the impact of new and old data entering/leaving the window, which suppresses the "ringing" artifacts seen in simple moving averages.

| Window | Main Characteristic | Use Case |
| :--- | :--- | :--- |
| **Rectangular** | Equal weights (SMA) | Baseline / Maximum frequency resolution |
| **Hanning** | Smooth cosine bell | General purpose smoothing |
| **Hamming** | Optimized cosine bell | Better side-lobe suppression than Hanning |
| **Blackman** | Steeper falloff | Low leakage, good for noisy data |
| **Blackman-Harris** | Steurman-Nuttall class | Maximum sidelobe suppression (-92dB) |

The default **Blackman-Harris** window provides the best noise suppression characteristics for financial time series, which often contain significant non-Gaussian noise.

## Mathematical Foundation

### 1. Filter Equation

$$ \text{AFIRMA}_t = \frac{\sum_{k=0}^{P-1} w_k \cdot x_{t-k}}{\sum_{k=0}^{P-1} w_k} $$

Where $x$ is the input series and $w_k$ are the window weights.

### 2. Window Coefficients

Weights are pre-calculated based on the selected `WindowType`:

**Hanning:**
$$ w_k = 0.5 - 0.5 \cos\left(\frac{2\pi k}{P}\right) $$

**Hamming:**
$$ w_k = 0.54 - 0.46 \cos\left(\frac{2\pi k}{P}\right) $$

**Blackman:**
$$ w_k = 0.42 - 0.5 \cos\left(\frac{2\pi k}{P}\right) + 0.08 \cos\left(\frac{4\pi k}{P}\right) $$

**Blackman-Harris:**
$$ w_k = 0.35875 - 0.48829 \cos\left(\frac{2\pi k}{P}\right) + 0.14128 \cos\left(\frac{4\pi k}{P}\right) - 0.01168 \cos\left(\frac{6\pi k}{P}\right) $$

*(Note: The actual implementation uses optimized cosine coefficients for performance)*

## Parameters

| Parameter | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| **Period** | 10 | ≥ 1 | The length of the window (number of weights). |
| **Window** | BlackmanHarris | Enum | The window function used to generate weights. |
| **Least Squares** | false | bool | Uses cubic regression data for the most recent window segment if true. |

## Usage

### Streaming (Real-time)

```csharp
var afirma = new Afirma(period: 10, window: Afirma.WindowType.BlackmanHarris);

foreach (var bar in marketData)
{
    var smoothed = afirma.Update(new TValue(bar.Time, bar.Close));
    Console.WriteLine($"{bar.Time}: {smoothed.Value:F4}");
}
```

### Batch Processing

```csharp
var series = new TSeries(timestamps, prices);
var smoothed = Afirma.Batch(series, period: 10);
```

### Span API (Zero-Allocation)

```csharp
ReadOnlySpan<double> prices = GetPrices();
Span<double> output = stackalloc double[prices.Length];

Afirma.Batch(prices, output, period: 10);
```

### Event-Driven (Chaining)

```csharp
var source = new TSeries();
var afirma = new Afirma(source, period: 10);

source.Add(new TValue(DateTime.UtcNow, 100.0));
```

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~20 ns/bar | O(1) mostly, linear dependence on Period for buffer management |
| **Allocations** | 0 | Zero-allocation in hot paths (stackalloc used for weights/buffer) |
| **Complexity** | O(P) | Linear in filter length (convolution) |
| **Accuracy** | 9 | Excellent noise reduction |
| **Timeliness** | 7 | Moderate lag, tradeoff for smoothness |
| **Overshoot** | 1 | No overshoot (unlike Sinc filters) |
| **Smoothness** | 10 | Exceptional smoothness due to high-order window functions |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Pine Script** | ✅ | Matches `afirma` function logic |
| **Internal** | ✅ | Batch, Streaming, and Span modes match |

## Window Type Comparison

For the same Period, different windows produce different smoothing characteristics:

| Window | Smoothness | Responsiveness | Best For |
| :--- | :--- | :--- | :--- |
| Rectangular | Low | Highest | Equivalent to SMA |
| Hanning | Medium | High | Balanced |
| Hamming | Medium-High | Medium-High | Similar to Hanning, better spectral properties |
| Blackman | High | Medium | Noisy trends |
| BlackmanHarris | Highest | Lower | Maximum smoothing, very noisy data |

## Common Pitfalls

1. **Period Selection:** Since this is a WMA, the lag is roughly proportional to Period/2. Don't use excessively large periods (e.g., >50) unless you are measuring long-term trends.
2. **Window Selection:** Use Blackman-Harris for the "signature" AFIRMA look—very smooth. Use Rectangular only if you explicitly want an SMA.

## See Also

* [ALMA](../../trends/alma/Alma.md) - Arnaud Legoux Moving Average (Gaussian weights)
* [WMA](../../trends/wma/Wma.md) - Weighted Moving Average (Linear weights)
* [SMA](../../trends/sma/Sma.md) - Simple Moving Average (Equal weights)
