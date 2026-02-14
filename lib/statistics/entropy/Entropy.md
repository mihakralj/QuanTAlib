# ENTROPY: Shannon Entropy

> "Information is the resolution of uncertainty." — Claude Shannon

Shannon Entropy measures the unpredictability or randomness of a time series over a sliding window. A low entropy value indicates the series is highly predictable (clustered values), while a high entropy value indicates the data is spread uniformly across its range — maximum randomness.

## Historical Context

Claude Shannon introduced the concept of information entropy in his landmark 1948 paper "A Mathematical Theory of Communication." Originally applied to communication channels, entropy has since become fundamental in information theory, statistical mechanics, and quantitative finance. In trading, entropy helps identify market regimes — low entropy suggests trending/consolidated behavior, high entropy suggests random/choppy conditions.

## Architecture & Physics

`Entropy` extends `AbstractBase` for single-value input streaming. It uses a `RingBuffer` to maintain the sliding window and rebuilds a histogram from the buffer contents on each update.

### Design Decisions

- **O(period) per update**: Unlike indicators that can use O(1) running sums, entropy requires min/max tracking and histogram construction that must be rebuilt when the window composition changes. This is inherent to the algorithm — bin boundaries shift with the range.
- **Bin count**: `bins = min(max(count, 2), 100)` — matches the PineScript reference. During warmup, bins scale with available data; once warmed up with period ≥ 100, always 100 bins.
- **NaN/Infinity guard**: Non-finite values are replaced with the last valid value.
- **No SIMD**: Histogram construction involves branching and random-access bin updates that don't vectorize well.
- **stackalloc**: Frequency arrays use stack allocation (100 ints = 400 bytes) to avoid heap pressure.

## Mathematical Foundation

Values in the sliding window are normalized to [0, 1] based on the window's min/max range, then bucketed into a histogram.

Shannon entropy is computed from the bin frequencies:

$$ H = -\sum_{i=1}^{B} p_i \ln(p_i) $$

where $p_i = \frac{f_i}{N}$ is the probability of bin $i$, $f_i$ is the bin count, $N$ is total observations, and $B$ is the number of bins.

The result is normalized by the maximum possible entropy:

$$ H_{\text{norm}} = \frac{H}{\ln(B)} $$

Output is in [0, 1] where:
- **0** = perfectly predictable (all values identical, zero variance)
- **1** = maximum randomness (uniform distribution across all bins)

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~50ns/bar | Histogram rebuild each update. |
| **Allocations** | 0 | stackalloc for frequency array. |
| **Complexity** | O(period) | Linear scan for min/max and histogram. |
| **Accuracy** | 10/10 | Exact histogram-based computation. |

## Validation

Self-validated against mathematical properties. No external library provides an equivalent histogram-based windowed Shannon entropy for direct comparison.

| Property | Status | Notes |
| :--- | :--- | :--- |
| **Constant series** | ✅ | Returns exactly 0. |
| **Two distinct values** | ✅ | Returns 1.0 with period=2. |
| **Range [0, 1]** | ✅ | All outputs within bounds. |
| **Batch = Streaming** | ✅ | Exact match across all modes. |

## Usage

```csharp
using QuanTAlib;

// Create a 14-period Shannon Entropy
var entropy = new Entropy(14);

// Update with a new value
var result = entropy.Update(new TValue(DateTime.UtcNow, 100.0));

// Get the last value (normalized 0-1)
double value = entropy.Last.Value;

// Batch mode
var series = Entropy.Batch(source, period: 14);

// Span mode
Entropy.Batch(inputSpan, outputSpan, period: 14);
```
