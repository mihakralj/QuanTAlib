# RAIN: Rainbow Moving Average

> *Mel Widner applied SMA ten times recursively, then weighted the layers like a rainbow: brightest at the top, fading toward the base. Ten colors of smoothing, one composite average that sees both fast and slow structure simultaneously.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Rain)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [rain.pine](rain.pine)                       |
| **Signature**    | [rain_signature](rain_signature.md) |

- RAIN recursively applies SMA 10 times, producing 10 layers of progressively smoother price representation, then computes a weighted average across ...
- **Similar:** [ALMA](../alma/alma.md), [FWMA](../fwma/fwma.md) | **Complementary:** ATR | **Trading note:** Raised-cosine MA; smooth taper at edges. Good sidelobe suppression for noise reduction.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

RAIN recursively applies SMA 10 times, producing 10 layers of progressively smoother price representation, then computes a weighted average across all layers. Layers 1-4 receive weights 5, 4, 3, 2 (emphasizing the more responsive layers), while layers 5-10 each receive weight 1, for a total divisor of 20. This multi-scale composition produces a moving average that responds to short-term price changes through the lightly smoothed upper layers while maintaining stability through the heavily smoothed lower layers.

## Historical Context

Mel Widner published "Rainbow Charts" in *Technical Analysis of Stocks & Commodities* (1998), introducing the concept of recursive SMA application as both a visualization technique and a composite smoothing method. The thinkorswim platform later standardized the weight vector as $[5, 4, 3, 2, 1, 1, 1, 1, 1, 1]$, which became the canonical RAIN MA.

The recursive SMA application has a deep mathematical interpretation: applying SMA $k$ times is equivalent to convolving the rectangular kernel with itself $k$ times, which produces a B-spline kernel of order $k$. Thus RAIN's 10 layers correspond to B-splines of orders 1 through 10, and the weighted average blends these spline approximations. The B-spline interpretation explains why higher layers are smoother: each convolution adds a degree of polynomial reproduction and reduces the spectral sidelobe level.

The weight vector $[5, 4, 3, 2, 1, 1, 1, 1, 1, 1]$ with sum 20 was chosen empirically rather than derived from optimization theory. The declining weights for layers 1-4 bias the output toward the more responsive layers, making RAIN track trends more closely than a uniform average of all 10 layers would.

## Architecture & Physics

### 1. Ten Cascaded SMA Layers

Each layer is an SMA applied to the previous layer's output:

$$
\text{MA}_1 = \text{SMA}(x, N), \quad \text{MA}_k = \text{SMA}(\text{MA}_{k-1}, N), \quad k = 2, \ldots, 10
$$

### 2. O(1) Running-Sum SMA

Each of the 10 SMA layers uses a circular buffer with a running sum, giving O(1) per-bar update cost per layer. Total cost: O(10) per bar, with O($10 \times N$) memory for the 10 buffers.

### 3. Weighted Composite

$$
\text{RAIN} = \frac{5 \cdot \text{MA}_1 + 4 \cdot \text{MA}_2 + 3 \cdot \text{MA}_3 + 2 \cdot \text{MA}_4 + \sum_{k=5}^{10} \text{MA}_k}{20}
$$

## Mathematical Foundation

**Layer computation (recursive SMA):**

$$
\text{MA}_1[t] = \frac{1}{N}\sum_{i=0}^{N-1} x_{t-i}
$$

$$
\text{MA}_k[t] = \frac{1}{N}\sum_{i=0}^{N-1} \text{MA}_{k-1}[t-i], \quad k = 2, \ldots, 10
$$

**Equivalent kernel:** The $k$-fold SMA is the $k$-th order B-spline kernel:

$$
B_k(x) = \underbrace{B_0 * B_0 * \cdots * B_0}_{k \text{ times}}(x)
$$

where $B_0$ is the rectangular pulse.

**Weighted output:**

$$
\text{RAIN} = \frac{\sum_{k=1}^{10} w_k \cdot \text{MA}_k}{20}
$$

with weights $\mathbf{w} = [5, 4, 3, 2, 1, 1, 1, 1, 1, 1]$.

**Group delay:** Each SMA layer adds $(N-1)/2$ bars of lag. However, the weighted composite lag is:

$$
\bar{d} = \frac{\sum w_k \cdot k \cdot (N-1)/2}{\sum w_k}
$$

For $N = 2$: $\bar{d} \approx 1.85$ bars. The upper-layer weighting significantly reduces the effective lag below what layer 10 alone would produce.

**Default parameters:** `period = 2`, `fixed layers = 10`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
// 10 circular buffers with running sums
for layer = 1 to 10:
    sum[layer] -= buf[layer][head]
    sum[layer] += input[layer]  // input is price for layer 1, MA[layer-1] for others
    buf[layer][head] = input[layer]
    MA[layer] = sum[layer] / count

head = (head + 1) % period

return (5*MA[1] + 4*MA[2] + 3*MA[3] + 2*MA[4] + MA[5] + MA[6] + MA[7] + MA[8] + MA[9] + MA[10]) / 20
```

## Resources

- Widner, M. (1998). "Rainbow Charts." *Technical Analysis of Stocks & Commodities*.
- thinkorswim / TD Ameritrade. "RainbowAverage" study documentation.
- Schoenberg, I.J. (1946). "Contributions to the Problem of Approximation of Equidistant Data by Analytic Functions." *Quarterly of Applied Mathematics*, 4(1), 45-99. (B-spline theory underlying recursive SMA.)

## Performance Profile

### Operation Count (Streaming Mode)

RAIN(N) composes 10 independent SMA(N) instances in parallel. Each SMA uses O(1) running-sum via its ring buffer. The composite output is a weighted sum of the 10 SMA results — all computed from the same input value.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Per-layer ring buffer push × 10 | 10 | 3 | ~30 |
| Per-layer running sum update × 10 (add new, subtract evicted) | 20 | 1 | ~20 |
| Per-layer SMA divide × 10 | 10 | 8 | ~80 |
| Weighted composite (10 FMA with weights 5,4,3,2,1,1,1,1,1,1) | 10 | 4 | ~40 |
| Final divide by 20 | 1 | 8 | ~8 |
| **Total** | **51** | — | **~178 cycles** |

O(1) per bar. Each of the 10 SMA layers is O(1); the composite sum is 10 FMA operations. WarmupPeriod = period × 10 (all layers must reach steady state).

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| 10 independent SMA running sums | Yes | All 10 sums independent per bar; `VADDPD` on 10-channel register set |
| 10 SMA divides | Yes | 10 `VDIVPD` ops; can be vectorized as 10-wide FP array |
| Weighted composite | Yes | 10-element dot product; fits in 2–3 AVX2 registers |
| Cross-bar independence | Yes | Outer loop fully vectorizable: 4 output bars per pass |

Because all 10 SMA layers are independent, the entire computation can be vectorized across layers AND across bars simultaneously. AVX2 can process 4 bars per pass, each bar updating all 10 layers via 10-register prefix sums. Estimated batch speedup for large series: ~6× over scalar.