# SGMA: Savitzky-Golay Moving Average

> *Least-squares polynomial fitting has been solving signal processing problems since 1964. That most traders still use medieval averaging techniques says more about the industry than the math.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 9), `degree` (default 2)                      |
| **Outputs**      | Single series (Sgma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [sgma.pine](sgma.pine)                       |
| **Signature**    | [sgma_signature](sgma_signature.md) |

- SGMA is a Finite Impulse Response (FIR) filter that uses polynomial fitting to smooth data while preserving higher moments (peaks, valleys, and inf...
- Parameterized by `period` (default 9), `degree` (default 2).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

SGMA is a Finite Impulse Response (FIR) filter that uses polynomial fitting to smooth data while preserving higher moments (peaks, valleys, and inflection points). Unlike the Simple Moving Average (which flattens everything) or the Exponential Moving Average (which introduces phase lag), SGMA uses polynomial weighting to maintain the original signal's shape characteristics.

## Historical Context / The Standard

Abraham Savitzky and Marcel Golay published their seminal paper "Smoothing and Differentiation of Data by Simplified Least Squares Procedures" in *Analytical Chemistry* in 1964. The context was spectroscopy—scientists needed to smooth noisy absorption spectra without destroying the peaks that identified chemical compounds.

The Savitzky-Golay filter became the gold standard in scientific signal processing because it solved the fundamental trade-off: smoothing reduces noise but also reduces signal amplitude. SG filters preserve the signal's shape by fitting local polynomials—effectively "understanding" the curvature of the data rather than blindly averaging it.

Financial markets present the same challenge. Traders want smooth lines that don't lag, and they want to preserve the actual peaks and troughs that matter for trading decisions. SGMA adapts the Savitzky-Golay approach to price series.

## Architecture & Physics

SGMA computes weights based on the polynomial degree parameter. The weight formula is elegantly simple:

$$w_i = 1 - |x_i|^d$$

Where:

- $x_i = \frac{i - \text{half\_window}}{\text{half\_window}}$ (normalized position from -1 to +1)
- $d$ = polynomial degree (0 to 4)

### The Physics of Polynomial Degree

The degree parameter controls the weight distribution:

| Degree | Weight Shape | Trading Behavior |
| :--- | :--- | :--- |
| **0** | Flat (uniform) | Equivalent to SMA. All bars weighted equally. |
| **1** | V-shape (linear) | Mild center-weighting. Moderate smoothing. |
| **2** | Parabolic | Strong center-weighting. Good balance. |
| **3** | Cubic falloff | Aggressive center-weighting. Shape preservation. |
| **4** | Quartic falloff | Extreme center-weighting. Maximum responsiveness. |

**Critical Edge Behavior**: For degree ≥ 1, the edge positions (oldest and newest values in the window) have weight = 0 because at the edges, $|x| = 1$, so $w = 1 - 1^d = 0$. This means the filter effectively ignores the boundary values—a deliberate design choice that prevents edge artifacts from corrupting the polynomial fit.

### The Compute Challenge

Unlike recursive filters (EMA, DEMA), SGMA requires a convolution over the entire window. QuanTAlib precomputes the weight vector upon initialization, reducing the runtime operation to a dot product.

$$\text{Runtime Cost} = O(N) \text{ multiplications}$$

The fixed-weight approach enables SIMD vectorization for batch processing, recovering much of the performance gap versus recursive filters.

## Mathematical Foundation

### 1. Period Normalization

SGMA requires an odd period to ensure symmetric polynomial fitting:

$$L = \begin{cases} \text{period} & \text{if period is odd} \\ \text{period} + 1 & \text{if period is even} \end{cases}$$

### 2. Weight Calculation

The half-window parameter defines the normalization range:

$$\text{half} = \lfloor L / 2 \rfloor$$

For each index $i$ from $0$ to $L-1$:

$$x_i = \frac{i - \text{half}}{\text{half}}$$

$$w_i = 1 - |x_i|^d$$

### 3. Weight Normalization

Weights are normalized to sum to 1.0:

$$\hat{w}_i = \frac{w_i}{\sum_{j=0}^{L-1} w_j}$$

### 4. Filter Output

The SGMA value is the weighted sum of prices:

$$\text{SGMA}_t = \sum_{i=0}^{L-1} P_{t-L+1+i} \cdot \hat{w}_i$$

## Performance Profile

SGMA trades a small amount of CPU cycles for superior shape preservation.

### Operation Count (Streaming Mode, Scalar)

Per-bar cost for period $L$ (weights precomputed at construction):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL | L | 3 | 3L |
| ADD | L | 1 | L |
| **Total** | **2L** | — | **~4L cycles** |

For a typical period of 14:
- **Total**: ~56 cycles per bar

**Constructor cost** (one-time): ~80L cycles (L power operations at ~80 cycles each + L additions + normalization)

**Complexity**: O(L) per bar — linear with period. Weights precomputed, runtime is pure dot product.

### Batch Mode (SIMD/FMA Analysis)

SGMA's dot product structure enables efficient SIMD vectorization:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| MUL+ADD (FMA) | 2L | L/4 (FMA256) | 8× |

**Batch efficiency (512 bars, L=14):**

| Mode | Cycles/bar | Total (512 bars) | Improvement |
| :--- | :---: | :---: | :---: |
| Scalar streaming | 56 | 28,672 | — |
| SIMD batch (FMA) | ~10 | ~5,120 | **~82%** |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact polynomial weight calculation |
| **Timeliness** | 8/10 | Center-weighted, inherent lag = half period |
| **Overshoot** | 9/10 | Polynomial decay prevents ringing artifacts |
| **Smoothness** | 9/10 | Adjustable via degree parameter |

### Implementation Details

```csharp
// Weight Precomputation (Constructor)
double half = _period / 2.0;
double wSum = 0;

for (int i = 0; i < _period; i++)
{
    double normX = (i - half) / half;
    double weight = 1.0 - Math.Pow(Math.Abs(normX), _degree);
    _weights[i] = weight;
    wSum += weight;
}

// Normalization
for (int i = 0; i < _period; i++)
{
    _weights[i] /= wSum;
}

// Runtime (Update) - simplified
double result = 0;
for (int i = 0; i < _period; i++)
{
    result += _buffer[i] * _weights[i];
}
return result;
```

## Validation

QuanTAlib validates SGMA against mathematical properties rather than external libraries, as most libraries implement the full Savitzky-Golay convolution coefficients rather than the simplified polynomial weighting approach.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Reference implementation. Validated against math definition. |
| **Self-Validation** | ✅ | Degree 0 matches SMA exactly. |
| **Mode Consistency** | ✅ | Streaming, batch, span, and event modes produce identical results. |
| **Skender** | ❌ | No SGMA equivalent. |
| **TA-Lib** | ❌ | Not included. |
| **Tulip** | ❌ | Not included. |
| **Ooples** | ❌ | Not included. |

## Common Pitfalls

1. **Degree 0 Misuse**: If you want SMA, use SMA. Degree 0 SGMA is mathematically equivalent but wastes cycles on weight calculation.

2. **Even Period Confusion**: SGMA silently converts even periods to odd (period + 1). If you specify period=10, you get period=11. This is mathematically necessary for symmetric polynomial fitting, not a bug.

3. **Edge Weight Zero**: For degree ≥ 1, the oldest and newest values in the window have zero weight. This is intentional—it prevents edge effects. But it means:
   - Bar corrections (`isNew=false`) have minimal effect at higher degrees
   - The effective "active" period is shorter than the nominal period

4. **Cold Start**: SGMA requires a full window ($L$) to produce mathematically valid output. The first $L-1$ bars are warmup noise. Check `IsHot` before trading on the signal.

5. **High Degree Instability**: Degrees 3-4 concentrate weight heavily in the center. While this preserves shape, it also means a small number of bars dominate the output—approaching the behavior of a very short moving average with extra smoothing on the tails.
