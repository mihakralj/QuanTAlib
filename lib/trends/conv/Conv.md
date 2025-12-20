# CONV: Convolution Moving Average

> "If you want a moving average that behaves exactly how you want it to, build it yourself. CONV is the 'Bring Your Own Kernel' of indicators."

CONV (Convolution Moving Average) is the ultimate tool for the signal processing purist. It doesn't presume to know what kind of smoothing you need; it simply asks for a kernel (a set of weights) and applies it to the data. Want a Gaussian filter? A Sinc filter? A custom edge-detection filter? CONV runs them all.

## Historical Context

Convolution is the fundamental operation of digital signal processing (DSP). While traders were busy inventing "new" moving averages by tweaking alpha values, engineers were using convolution to process audio, images, and radar signals for decades. CONV brings this raw power to financial time series, allowing for arbitrary FIR (Finite Impulse Response) filtering.

## Architecture & Physics

CONV applies a sliding dot product between the data window and your custom kernel. The "physics" are entirely defined by the kernel you provide.

- **Symmetric Kernel**: Zero phase shift (if centered correctly).
- **Asymmetric Kernel**: Introduces lag or lead.
- **Positive Weights**: Smoothing.
- **Mixed Weights**: Differentiation or band-pass filtering.

### Zero-Allocation Design

We treat your kernel with the respect it deserves.

- **RingBuffer**: Stores the price history to avoid array shifting.
- **SIMD Dot Product**: The core convolution operation uses hardware intrinsics (`Vector<double>`) to multiply-accumulate the kernel and data window in parallel.
- **Branchless Logic**: The circular buffer handling is optimized to minimize branching in the hot path.

## Mathematical Foundation

The value at time $t$ is the sum of the element-wise product of the kernel $K$ and the price vector $P$:

$$ \text{CONV}_t = \sum_{i=0}^{N-1} P_{t-i} \cdot K_i $$

Where:

- $N$ is the length of the kernel.
- $K_0$ multiplies the most recent price (or oldest, depending on convention; our implementation aligns $K_0$ with the oldest data in the window and $K_{N-1}$ with the newest).

## Performance Profile

Performance depends linearly on the kernel length ($N$).

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | Moderate | Kernel convolution per bar |
| **Complexity** | O(N) | Window iteration required |
| **Accuracy** | 8/10 | Depends on kernel, generally high |
| **Timeliness** | 7/10 | Depends on kernel design |
| **Overshoot** | 8/10 | Depends on kernel design |
| **Smoothness** | 8/10 | Depends on kernel design |

## Validation

Validated against standard DSP convolution implementations (e.g., SciPy `signal.convolve`).

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **SciPy** | $10^{-12}$ | Matches standard 'valid' convolution mode |

### Common Pitfalls

1. **Kernel Direction**: Our implementation applies the kernel such that the last element of the kernel multiplies the most recent data point. If you import kernels from other DSP libraries, you might need to reverse them.
2. **Normalization**: We do *not* automatically normalize your kernel. If the sum of your weights is not 1.0, the output scale will be different from the input scale. This is a feature, not a bug (allows for differential filters).
3. **Performance**: A kernel size of 1000 will be 100x slower than a kernel size of 10. Use FFT-based convolution for massive kernels (not implemented here; this is for trading, not searching for extraterrestrial life).
