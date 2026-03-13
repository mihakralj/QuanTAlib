# Kalman Filter (KALMAN)

> *Prediction is very difficult, especially if it's about the future.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `q` (default 0.01), `r` (default 0.1)                      |
| **Outputs**      | Single series (Kalman)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `10` bars                          |
| **PineScript**   | [kalman.pine](kalman.pine)                       |
| **Signature**    | [kalman_signature](kalman_signature.md) |


- The **Kalman Filter** is a recursive algorithm that estimates the state of a dynamic system from a series of incomplete and noisy measurements.
- **Similar:** [LMS](../lms/Lms.md), [RLS](../rls/Rls.md) | **Complementary:** ATR for measurement noise estimation | **Trading note:** Kalman filter; optimal linear estimator. Adapts to changing market dynamics. Widely used in quant finance.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The **Kalman Filter** is a recursive algorithm that estimates the state of a dynamic system from a series of incomplete and noisy measurements. In technical analysis, it acts as a sophisticated smoothing filter that adapts to price changes based on specified noise covariances. Unlike simple moving averages that treat all past data equally or with fixed weights, the Kalman Filter dynamically adjusts its "trust" between its own prediction and the new price data.

## Historical Context

Developed by Rudolf E. Kalman in 1960, the Kalman Filter was crucial for the Apollo program's navigation. It solved the problem of estimating a trajectory when sensors (measurements) were noisy and the model (prediction) wasn't perfect. In finance, it applies the same logic: "Price is truth plus noise." By estimating the "truth," we get a lag-efficient smoother.

## Architecture & Physics

This implementation is a **1-Dimensional Kalman Filter** tailored for time-series smoothing. It maintains two pieces of state:
1.  **Estimate ($x$)**: The current "true" price.
2.  **Error Covariance ($p$)**: Currently estimated uncertainty of $x$.

The filter operates in a "Predict-Correct" loop:
1.  **Predict**: Before seeing the new price, assume the price stays the same ($x_{pred} = x_{t-1}$) but uncertainty increases ($p_{pred} = p_{t-1} + q$).
2.  **Correct**: Compare prediction to actual price. The difference is weighed by the **Kalman Gain** ($k$), which is calculated from the uncertainties ($p$ and $r$).

If the process noise ($q$) is high, the filter assumes price moves a lot, so it trusts new data more (less smoothing). If measurement noise ($r$) is high, it assumes price is noisy, so it trusts its own prediction more (more smoothing).

### Complexity

The algorithm is strictly **O(1)**. It only requires the previous state ($x, p$) to calculate the next. It is zero-allocation in the hot path.

## Mathematical Foundation

The 1D Kalman Filter equations:

### 1. Prediction Step

$$ x_{pred} = x_{t-1} $$
$$ p_{pred} = p_{t-1} + q $$

### 2. Update Step

Calculate Kalman Gain ($k$):

$$ k = \frac{p_{pred}}{p_{pred} + r} $$

Update Estimate ($x_{t}$):

$$ x_{t} = x_{pred} + k \cdot (measurement_t - x_{pred}) $$

Update Error Covariance ($p_{t}$):

$$ p_{t} = (1 - k) \cdot p_{pred} $$

Where:
- $q$: Process Noise Covariance (User Parameter)
- $r$: Measurement Noise Covariance (User Parameter)

## Parameters

| Name | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **q** | `double` | 0.01 | Process Noise Covariance. Controls sensitivity to trend changes. Higher = Faster/Noisier. |
| **r** | `double` | 0.1 | Measurement Noise Covariance. Controls smoothing. Higher = Smoother/Laggier. |

## Performance Profile

### Operation Count (Streaming Mode)

1D Kalman filter: scalar predict-update cycle. Two phases: predict (extrapolate state + grow variance) and update (apply gain, update state and variance). O(1) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Predict: state extrapolation | 1 | ~2 cy | ~2 cy |
| Predict: covariance growth | 1 | ~2 cy | ~2 cy |
| Update: Kalman gain = P/(P+R) | 1 | ~10 cy | ~10 cy |
| Update: state = state + K*(z-state) | 1 | ~4 cy | ~4 cy |
| Update: covariance shrink | 1 | ~3 cy | ~3 cy |
| **Total** | **5** | — | **~21 cycles** |

O(1) per bar. Division for gain computation dominates. ~21 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| State recursion | No | Sequential: state[n] = f(state[n-1]) |
| Gain computation | No | Depends on running covariance |

Batch throughput: ~21 cy/bar scalar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 2 ns/bar | Extrememly fast O(1) operations. |
| **Allocations** | 0 | Pure inputs/outputs on stack/structs. |
| **Complexity** | O(1) | No loops, no buffers. |
| **Accuracy** | 10/10 | Exact implementation of standard KF equations. |
| **Timeliness** | 8/10 | Very low lag compared to SMAs of similar smoothness. |
| **Smoothness** | 9/10 | Excellent noise reduction. |

## C# Usage

```csharp
// Standard usage with defaults
var kf = new Kalman(q: 0.01, r: 0.1);
TValue smoothed = kf.Update(new TValue(DateTime.UtcNow, 100.0));

// Static span calculation for high-performance batch processing
double[] inputs = ...;
double[] outputs = new double[inputs.Length];
Kalman.Calculate(inputs, outputs, q: 0.05, r: 0.5);

// Chaining
var source = new TSeries();
var kf1 = new Kalman(source, q: 0.01, r: 0.1);
var kf2 = new Kalman(kf1, q: 0.001, r: 0.1); // Double smoothing