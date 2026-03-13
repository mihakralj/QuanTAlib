# CFITZ: Christiano-Fitzgerald Band-Pass Filter

> *Christiano-Fitzgerald isolates a frequency band from the time series, extracting cycles of a chosen wavelength range.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `pLow` (default 6), `pHigh` (default 32)                      |
| **Outputs**      | Single series (Cfitz)                       |
| **Output range** | Oscillates around zero           |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [cfitz.pine](cfitz.pine)                       |

- The **Christiano-Fitzgerald Band-Pass Filter** is an asymmetric full-sample filter that approximates the ideal spectral band-pass by using time-var...
- **Similar:** [BaxterKing](../baxterking/BaxterKing.md), [HP](../hp/Hp.md) | **Complementary:** Trend indicators | **Trading note:** Christiano-Fitzgerald bandpass filter; asymmetric, optimal for finite samples.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Overview

The **Christiano-Fitzgerald Band-Pass Filter** is an asymmetric full-sample filter that approximates the ideal spectral band-pass by using time-varying weights that adapt to each bar's position in the sample. Unlike the symmetric Baxter-King filter, CF uses **all available data** and produces output for every bar — including endpoints — with no data loss.

The filter is optimal (minimizes mean squared error) under the assumption that the input data follows a random walk. Endpoint correction weights force the total weight sum to zero, ensuring DC rejection (trend removal).

Output oscillates around zero and represents the cyclical component of the input signal.

## Origin

Lawrence J. Christiano and Terry J. Fitzgerald. "The Band Pass Filter." *International Economic Review*, 44(2), 435-465, 2003.

## Parameters

| Parameter | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| `pLow` | 6 | ≥ 2 | Minimum period of the passband (bars). Cycles faster than this are rejected. |
| `pHigh` | 32 | > pLow | Maximum period of the passband (bars). Cycles slower than this are rejected. |

Standard NBER business cycle parameters: pLow=6, pHigh=32 for quarterly data; pLow=18, pHigh=96 for monthly.

## Mathematics

### Ideal Band-Pass Weights

The ideal (infinite-length) band-pass filter weights are:

$$B_0 = \frac{\omega_h - \omega_l}{\pi}$$

$$B_j = \frac{\sin(j \omega_h) - \sin(j \omega_l)}{\pi j} \quad \text{for } j \geq 1$$

where $\omega_l = 2\pi / p_{High}$ and $\omega_h = 2\pi / p_{Low}$.

### Asymmetric CF Formula

For the current (last) bar $t = T$:

$$c_T = \frac{1}{2} B_0 \cdot y_T + \sum_{j=1}^{T-2} B_j \cdot y_{T-j} + \tilde{b}_{T-1} \cdot y_1$$

For interior bars $t = 2, 3, \ldots, T-1$:

$$c_t = B_0 \cdot y_t + \sum_{j=1}^{T-t-1} B_j \cdot y_{t+j} + \tilde{b}_{fwd} \cdot y_T + \sum_{j=1}^{t-2} B_j \cdot y_{t-j} + \tilde{b}_{bwd} \cdot y_1$$

### Endpoint Corrections (Nonstationary)

$$\tilde{b}_{fwd} = -\frac{1}{2} B_0 - \sum_{j=1}^{T-t-1} B_j$$

$$\tilde{b}_{bwd} = -\frac{1}{2} B_0 - \sum_{j=1}^{t-2} B_j$$

These force the weight sum to exactly zero for each bar, guaranteeing DC rejection.

### Weight Zero-Sum Proof

For any bar $t$: center weight + sum of interior weights + endpoint correction = 0.

## Architecture

### State Management

```
record struct State {
    LastValid: double    // last non-NaN, non-Inf input
    Count: int           // bars seen
}
```

### Internal Storage

- `List<double> _history` — stores all accumulated input values for lookback
- No ring buffer — CF needs access to ALL history (position-indexed)
- Precomputed angular frequencies `_wl`, `_wh`, and central weight `_b0`

### Streaming vs Batch

| Mode | Algorithm | Complexity |
| :--- | :--- | :--- |
| **Streaming** (`Update()`) | Computes CF formula for last bar only, using all accumulated history | O(T) per bar |
| **Batch** (`Batch(span)`) | True full-sample filter — computes CF for ALL bars with forward AND backward weights | O(T²) total |

**Important**: Streaming and Batch produce different intermediate values by design. Streaming treats the accumulated history as the full sample at each step. Batch has access to the entire series and uses both forward and backward weights. They agree only on the **last bar**.

### Bar Correction

Standard `isNew` / restore pattern:
- `isNew=true`: save state to `_p_state`, add to `_history`
- `isNew=false`: restore from `_p_state`, update last element of `_history`

## Comparison with Baxter-King

| Feature | Baxter-King | Christiano-Fitzgerald |
| :--- | :--- | :--- |
| Symmetry | Symmetric (fixed K lags) | Asymmetric (time-varying) |
| Data loss | Loses 2K bars at endpoints | No data loss |
| Weights | Fixed, precomputed | Vary by position in sample |
| Optimality | Truncated approximation | MSE-optimal under random walk |
| Parameters | pLow, pHigh, K | pLow, pHigh |
| Delay | Fixed K-bar delay | No fixed delay |
| Complexity per bar | O(K) | O(T) streaming, O(T) per bar in batch |


## Performance Profile

### Operation Count (Streaming Mode)

CFITZ accumulates O(N) ideal band-pass weights per new bar, with endpoint correction forcing total weight to zero. Window size N grows until the sample fills, at which point it stabilizes at O(N) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Cosine/sine weight computation | N | ~20 cy | ~640 cy (N=32) |
| Endpoint correction (sum-to-zero) | 2 | ~3 cy | ~6 cy |
| Weighted sum (FMA) | N | ~5 cy | ~160 cy |
| Sum normalization | 1 | ~3 cy | ~3 cy |
| **Total** | **2N+3** | — | **~810 cycles (N=32)** |

At N=32 the per-bar cost is ~810 cycles. The O(N) weight recomputation each bar is the dominant cost. Batch mode can precompute a weight matrix for fixed N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Cosine/sine weight table | Yes | Precompute once per period pair; vectorize weight application |
| Dot-product convolution | Yes | `Vector<double>` over N-length window; 4x-8x speedup |
| Endpoint sum correction | No | Scalar update to weights |
| Sum normalization | No | Single scalar division |

SIMD cuts the dot-product pass to ~100 cycles for N=32 with AVX2 (4 doubles/vector). Cosine weight table is computed once at initialization.

## Validation

| Source | Status | Notes |
| :--- | :--- | :--- |
| **Pine Script** | Validated | Ported from PineScript v6 reference implementation. |
| **Synthetic** | Validated | DC rejection, linear trend rejection, in-band passthrough, out-of-band rejection. |
| **Mathematical** | Validated | Weight zero-sum verified for constant and linear inputs. |

No external library implements CFITZ for cross-validation (not in TA-Lib, Skender, Tulip, or Ooples).

## Performance Considerations

- **Streaming**: O(T) per bar — each `Update()` sums over the entire accumulated history. For very long series (T > 10000), consider using the Batch API.
- **Batch**: O(T²) total — precomputes all ideal weights once, then applies the full-sample formula for each bar. Uses `stackalloc` for weight arrays up to 256 elements, `ArrayPool` for larger.
- **Memory**: O(T) for history storage (`List<double>`).
- **No SIMD**: Convolution is position-dependent (asymmetric weights), making vectorization impractical.

## Usage

```csharp
// Streaming (bar-by-bar)
var cf = new Cfitz(pLow: 6, pHigh: 32);
foreach (var bar in series)
{
    double cycle = cf.Update(bar).Value;
}

// Batch (full-sample, stateless)
double[] output = new double[prices.Length];
Cfitz.Batch(prices, output, pLow: 6, pHigh: 32);

// Calculate factory method
var (results, indicator) = Cfitz.Calculate(series, pLow: 6, pHigh: 32);

// Event-driven chaining
var source = new TSeries();
var cfChained = new Cfitz(source, pLow: 6, pHigh: 32);
source.Add(new TValue(DateTime.UtcNow, price)); // cfChained.Last auto-updates
```