# CONV: Convolution Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | double[] kernel                      |
| **Outputs**      | Single series (Conv)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- CONV (Convolution Moving Average) is the ultimate tool for the signal processing purist.
- Parameterized by double[] kernel.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "If you want a moving average that behaves exactly how you want it to, build it yourself. CONV is the 'Bring Your Own Kernel' of indicators."

CONV (Convolution Moving Average) is the ultimate tool for the signal processing purist. It doesn't presume to know what kind of smoothing you need; it simply asks for a kernel (a set of weights) and applies it to the data. Want a Gaussian filter? A Sinc filter? A custom edge-detection filter? CONV runs them all.

## Historical Context

Convolution is the fundamental operation of digital signal processing (DSP). While traders were busy inventing "new" moving averages by tweaking alpha values, engineers were using convolution to process audio, images, and radar signals for decades. CONV brings this raw power to financial time series, allowing for arbitrary FIR (Finite Impulse Response) filtering.

## Architecture & Physics

CONV applies a sliding dot product between the data window and your custom kernel. The "physics" are entirely defined by the kernel you provide.

* **Symmetric Kernel**: Zero phase shift (if centered correctly).
* **Asymmetric Kernel**: Introduces lag or lead.
* **Positive Weights**: Smoothing.
* **Mixed Weights**: Differentiation or band-pass filtering.

## Mathematical Foundation

The value at time $t$ is the sum of the element-wise product of the kernel $K$ and the price vector $P$:

$$ \text{CONV}_t = \sum_{i=0}^{N-1} P_{t-i} \cdot K_i $$

Where:

* $N$ is the length of the kernel.
* $K_0$ multiplies the most recent price (or oldest, depending on convention; the QuanTAlib implementation aligns $K_0$ with the oldest data in the window and $K_{N-1}$ with the newest).

## Performance Profile

Performance depends linearly on the kernel length ($N$).

### Operation Count (Streaming Mode, Scalar)

Per-bar cost for kernel length $N$:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | N | 3 | 3N |
| ADD | N | 1 | N |
| **Total** | **2N** | — | **~4N cycles** |

For a typical kernel length of 14:
- **Total**: ~56 cycles per bar

**Complexity**: O(N) — linear with kernel length. No recursion, pure FIR convolution.

### Batch Mode (SIMD/FMA Analysis)

CONV's dot product structure is ideal for SIMD vectorization:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| MUL+ADD (FMA) | 2N | N/4 (FMA256) | 8× |
| Horizontal sum | — | 1 | — |

**Batch efficiency (512 bars, N=14):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 56 | 28,672 | — |
| SIMD batch (FMA) | ~10 | ~5,120 | **~82%** |

SIMD achieves excellent speedup because the dot product is embarrassingly parallel.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact convolution to double precision |
| **Timeliness** | Variable | Depends on kernel (symmetric = lag N/2) |
| **Overshoot** | Variable | Depends on kernel design |
| **Smoothness** | Variable | Kernel-dependent |

Quality characteristics are entirely determined by the user-provided kernel.

### Zero-Allocation Design

CONV stores the kernel in a pre-allocated array. The `Update` method performs a dot product using a circular buffer for the price history, requiring no new allocations.

## Validation

Validation is performed by reproducing standard moving averages (SMA, WMA, TRIMA) using their equivalent kernels and comparing against external libraries.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated against internal SMA, WMA, TRIMA. |
| **Skender** | ✅ | Validated against WMA (using WMA kernel). |
| **TA-Lib** | ✅ | Validated against WMA (using WMA kernel). |
| **Tulip** | ✅ | Validated against WMA (using WMA kernel). |
| **Ooples** | ✅ | Validated against WMA (using WMA kernel). |

### C# Implementation Considerations

The QuanTAlib CONV implementation optimizes convolution through pre-allocation and SIMD-accelerated dot products:

**Defensive Kernel Copy**
```csharp
_kernel = new double[_period];
Array.Copy(kernel, _kernel, _period);
```
The kernel is copied to prevent external mutation. This one-time allocation at construction ensures the indicator owns its weight array.

**State Record Struct**
```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State(double LastValidValue);
private State _state;
private State _p_state;
```
Minimal state (just last valid value) enables efficient bar correction via `_p_state` snapshot/restore.

**Circular Buffer Dot Product**
```csharp
int head = _buffer.StartIndex;
int part1Len = _period - head;
result = internalBuf.Slice(head, part1Len).DotProduct(kernelSpan[..part1Len])
       + internalBuf[..head].DotProduct(kernelSpan[part1Len..]);
```
Full buffer requires two `DotProduct` calls to handle the circular wrap. The `DotProduct` extension leverages AVX2/FMA intrinsics when available.

**Stackalloc for Batch Processing**
```csharp
Span<double> window = period <= 256 ? stackalloc double[period] : new double[period];
```
Small kernels (≤256 elements) use stack allocation to avoid heap pressure during batch operations.

**Pre-sized Output Collections**
```csharp
CollectionsMarshal.SetCount(t, len);
CollectionsMarshal.SetCount(v, len);
```
Batch processing pre-sizes lists to avoid reallocation during population.

**Memory Layout**

| Field | Type | Size | Notes |
|:------|:-----|-----:|:------|
| `_period` | int | 4B | Kernel length |
| `_kernel` | double[] | 8B + N×8B | Weight array reference + data |
| `_buffer` | RingBuffer | ~40B + N×8B | Circular data buffer |
| `_state` | State | 8B | Current last valid value |
| `_p_state` | State | 8B | Previous state for rollback |
| **Total** | | ~68B + 2N×8B | Plus object overhead |

For a typical 14-period kernel: ~68 + 224 ≈ **292 bytes** per instance.

### Common Pitfalls

1. **Kernel Direction**: Our implementation applies the kernel such that the last element of the kernel multiplies the most recent data point. If you import kernels from other DSP libraries, you might need to reverse them.
2. **Normalization**: Kernel weights are *not* automatically normalized. If the sum of the weights is not 1.0, the output scale will be different from the input scale. This is a feature, not a bug (allows for differential filters).
3. **Performance**: A kernel size of 1000 will be 100x slower than a kernel size of 10. Use FFT-based convolution for massive kernels (not implemented here; this is for trading, not searching for extraterrestrial life).
