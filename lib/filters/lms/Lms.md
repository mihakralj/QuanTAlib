# LMS: Least Mean Squares Adaptive Filter

> "The filter that learns from its mistakes, one gradient step at a time."

The **Least Mean Squares (LMS) Adaptive Filter** is the Widrow-Hoff adaptive FIR filter, the simplest and most widely deployed adaptive algorithm in signal processing. It maintains an `order`-tap weight vector that learns to predict the current input from its recent history, updating weights via the Normalized LMS (NLMS) gradient descent rule. The result is a price-following overlay filter that automatically adapts its frequency response to changing market conditions with O(order) per-bar complexity.

## Historical Context

Bernard Widrow and Marcian Hoff introduced the LMS algorithm in 1960 at Stanford, originally for adaptive noise cancellation in telephone circuits. The algorithm's appeal was immediate: it requires no matrix inversions (unlike the Wiener-Hopf solution) and no eigenvalue decomposition (unlike RLS). It simply nudges each weight in the direction that reduces the squared prediction error, one sample at a time.

The Normalized LMS (NLMS) variant divides the step size by the input power $\|\mathbf{x}\|^2$, making convergence independent of signal amplitude. Without normalization, a step size that works for a \$10 stock diverges on a \$1000 stock. NLMS fixes this with one extra division per update.

In financial applications, LMS occupies a middle ground between fixed FIR filters (SMA, WMA) that cannot adapt and the Wiener filter that requires batch autocorrelation estimation. LMS adapts continuously and incrementally, making it natural for streaming price data where the statistical regime shifts over time.

## Architecture and Physics

### 1. Adaptive FIR Prediction

The filter maintains a weight vector $\mathbf{w} = [w_0, w_1, \ldots, w_{M-1}]$ where $M$ is the filter order. At each bar, it forms the input vector from past values:

$$\mathbf{x}[t] = [x[t-1], x[t-2], \ldots, x[t-M]]$$

The prediction is the inner product:

$$\hat{x}[t] = \mathbf{w}^T \mathbf{x}[t] = \sum_{i=0}^{M-1} w_i \cdot x[t-i-1]$$

Note: the filter predicts $x[t]$ from $x[t-1] \ldots x[t-M]$ (no look-ahead). The output is the prediction $\hat{x}[t]$, which serves as the filtered estimate of the current price.

### 2. NLMS Weight Update

The prediction error is:

$$e[t] = x[t] - \hat{x}[t]$$

The weight update follows the NLMS rule:

$$\mathbf{w}[t+1] = \mathbf{w}[t] + \frac{\mu}{\epsilon + \|\mathbf{x}[t]\|^2} \cdot e[t] \cdot \mathbf{x}[t]$$

where:

- $\mu \in (0, 2)$ is the learning rate (step size)
- $\epsilon = 10^{-10}$ prevents division by zero
- $\|\mathbf{x}[t]\|^2 = \sum_{i=0}^{M-1} x[t-i-1]^2$ is the input power

### 3. Convergence Properties

- **Stability**: NLMS is guaranteed stable for $0 < \mu < 2$
- **Misadjustment**: Excess MSE above the Wiener optimum scales as $\mu M / (2 - \mu)$
- **Convergence speed**: Time constant $\approx M / \mu$ bars to reach steady state
- **Tracking**: Higher $\mu$ tracks faster but with more noise; lower $\mu$ is smoother but lags regime changes

### Inertial Physics

- **Overlay Behavior**: Output follows the price level (not zero-centered like bandpass filters)
- **Adaptive Frequency Response**: The filter's effective transfer function evolves as weights change, automatically emphasizing frequencies present in recent data
- **Memory**: Unlike IIR filters, FIR filters have finite memory. The effective memory horizon is approximately `order` bars
- **No Stability Risk**: FIR filters cannot have poles outside the unit circle. The filter is inherently BIBO stable regardless of weight values

## Mathematical Foundation

### NLMS Derivation

Starting from the instantaneous gradient of the squared error:

$$J = e[t]^2 = (x[t] - \mathbf{w}^T\mathbf{x}[t])^2$$

$$\nabla_{\mathbf{w}} J = -2 e[t] \mathbf{x}[t]$$

Standard LMS: $\mathbf{w} \leftarrow \mathbf{w} + \mu \cdot e[t] \cdot \mathbf{x}[t]$

Normalizing by input power for scale-invariant convergence:

$$\mathbf{w} \leftarrow \mathbf{w} + \frac{\mu}{\epsilon + \|\mathbf{x}\|^2} \cdot e[t] \cdot \mathbf{x}[t]$$

### Default Parameters

| Parameter | Default | Purpose |
| :--- | :--- | :--- |
| `order` | 16 | Number of FIR taps. Higher = more frequency resolution, slower adaptation. |
| `mu` | 0.5 | Learning rate. Higher = faster tracking, more noise. |

### Parameter Selection Guidelines

| Regime | Order | Mu | Behavior |
| :--- | :--- | :--- | :--- |
| Fast scalping | 4-8 | 0.8-1.5 | Quick adaptation, noisy |
| Swing trading | 8-32 | 0.3-0.7 | Balanced tracking/smoothness |
| Position/trend | 32-128 | 0.1-0.3 | Smooth, slow adaptation |

## Performance Profile

| Metric | Impact | Notes |
| :--- | :--- | :--- |
| **Throughput** | O(order)/bar | Two inner products + one weight update per bar. |
| **Allocations** | 0 | Zero-allocation in Update() hot path. Weight arrays pre-allocated. |
| **FMA** | Yes | Inner products and weight updates use FusedMultiplyAdd. |
| **Accuracy** | 7/10 | Converges to Wiener solution in stationary regime. |
| **Timeliness** | 8/10 | Adapts continuously; no batch recomputation needed. |
| **Smoothness** | 7/10 | Depends on mu/order tradeoff. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Pine Script** | Validated | Ported from validated Widrow-Hoff implementation. |
| **Convergence** | Validated | MSE decreases monotonically on periodic signals. |
| **Self-Consistency** | Validated | Streaming, batch, span, and eventing modes produce identical results. |
| **Deterministic** | Validated | Same input always produces same output. |
| **Stability** | Validated | 10,000-bar GBM series produces all-finite output. |
| **NLMS property** | Validated | Higher mu produces faster adaptation after step input. |

## Common Pitfalls

1. **Setting mu outside (0, 2)**: The NLMS algorithm diverges for $\mu \geq 2$ and does nothing for $\mu \leq 0$. The constructor enforces this constraint. In practice, $\mu > 1.5$ is rarely useful due to excessive noise amplification.

2. **Order too large relative to data**: If `order` exceeds the number of available bars, the filter passes through raw input during warmup. Plan for `order + 1` bars of warmup before trusting output.

3. **Expecting zero-centered output**: Unlike bandpass filters (SPBF, BPF), LMS is a price-following overlay. Its output tracks the price level, not deviations from it. For mean-reversion signals, use the prediction error $e[t]$ instead.

4. **Ignoring the misadjustment tradeoff**: Large $\mu$ with large `order` maximizes the misadjustment $\mu M / (2 - \mu)$. The excess noise above the Wiener optimum grows linearly with both parameters. Keep $\mu \cdot \text{order} < 2$ as a rule of thumb for low-noise output.

5. **Comparing against SMA/EMA directly**: LMS is adaptive. Its effective smoothing changes with market regime. In trending markets it tracks closely; in mean-reverting markets it smooths aggressively. Fixed filters cannot do this.

6. **Not resetting after regime changes**: If market microstructure changes fundamentally (e.g., different asset, different timeframe), the learned weights carry stale information. Call `Reset()` or construct a new instance.

7. **Using raw LMS for signal generation**: The primary output is the prediction $\hat{x}[t]$. The error signal $e[t] = x[t] - \hat{x}[t]$ is often more useful for trading signals (it measures surprise/innovation).

## References

1. B. Widrow and M. E. Hoff. "Adaptive Switching Circuits." IRE WESCON Convention Record, 1960.
2. S. Haykin. "Adaptive Filter Theory." 5th edition, Prentice Hall, 2014.
3. A. H. Sayed. "Fundamentals of Adaptive Filtering." Wiley, 2003.
4. B. Widrow and S. D. Stearns. "Adaptive Signal Processing." Prentice Hall, 1985.
5. D. G. Manolakis et al. "Statistical and Adaptive Signal Processing." McGraw-Hill, 2000.

## Usage

```csharp
using QuanTAlib;

// Default: order=16, mu=0.5
var lms = new Lms(order: 16, mu: 0.5);

// Streaming update
var result = lms.Update(new TValue(DateTime.UtcNow, price));
// result.Value = adaptive prediction of current price

// Static batch
double[] output = new double[prices.Length];
Lms.Batch(prices, output, order: 16, mu: 0.5);

// Event-driven chaining
var source = new TSeries();
var lmsChained = new Lms(source, order: 16, mu: 0.5);
source.Add(new TValue(DateTime.UtcNow, price)); // lmsChained.Last auto-updates
```
