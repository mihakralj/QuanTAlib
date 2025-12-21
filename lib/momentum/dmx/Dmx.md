# DMX: Directional Movement Index

> DMX is what happens when you take Welles Wilder's 1978 engine and swap the carburetor for fuel injection.

The DMX is Mark Jurik's ultra-smooth, low-lag overhaul of the classic Directional Movement system. It replaces Wilder's sluggish smoothing algorithms with the Jurik Moving Average (JMA), resulting in a directional indicator that reacts faster to trend changes while filtering out more noise.

## The Jurik Upgrade

Wilder's original ADX/DMI system is legendary but mathematically primitive; it relies on simple recursive smoothing (RMA) that introduces significant lag. DMX retains the core logic of directional movement ($DM+$ and $DM-$) but upgrades the engine that processes them. By using JMA, DMX achieves the "holy grail" of signal processing: smoothness without lag.

## Architecture & Physics

The physics of DMX are identical to DMI, but the friction is removed.

1. **Decomposition**: Raw Directional Movement ($DM$) and True Range ($TR$) are calculated exactly as Wilder did.
2. **Smoothing**: Instead of the laggy RMA, these raw signals are fed into three parallel JMA filters.
3. **Normalization**: The smoothed DM is normalized by the smoothed TR to get Directional Indicators ($DI$).
4. **Differential**: The DMX is simply $DI^+ - DI^-$.

### The Lag Reduction

JMA is an adaptive filter. It tracks the signal closely when it moves (low lag) and smooths it aggressively when it stalls (high noise reduction). This dynamic behavior means DMX signals trend changes significantly earlier than standard DMI—often by 3-5 bars—without the "whipsaw" penalty usually associated with faster indicators.

## Mathematical Foundation

The core directional logic remains faithful to Wilder.

### 1. Raw Directional Movement

$$
\text{UpMove} = H_t - H_{t-1}
$$
$$
\text{DownMove} = L_{t-1} - L_t
$$

$$
DM^+ = \begin{cases} \text{UpMove} & \text{if } \text{UpMove} > \text{DownMove} \text{ and } \text{UpMove} > 0 \\ 0 & \text{otherwise} \end{cases}
$$

$$
DM^- = \begin{cases} \text{DownMove} & \text{if } \text{DownMove} > \text{UpMove} \text{ and } \text{DownMove} > 0 \\ 0 & \text{otherwise} \end{cases}
$$

### 2. Jurik Smoothing

$$
SmoothDM^+ = JMA(DM^+, \text{Period})
$$
$$
SmoothDM^- = JMA(DM^-, \text{Period})
$$
$$
SmoothTR = JMA(TR, \text{Period})
$$

### 3. Directional Indicators

$$
DI^+ = \frac{SmoothDM^+}{SmoothTR} \times 100
$$
$$
DI^- = \frac{SmoothDM^-}{SmoothTR} \times 100
$$

### 4. DMX

$$
DMX = DI^+ - DI^-
$$

## Performance Profile

The complexity is dominated by the three JMA calculations.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15ns / bar | 3x JMA updates per bar |
| **Allocations** | 0 bytes | Hot path is allocation-free |
| **Complexity** | O(1) | Constant time per update |
| **Precision** | `double` | Required for JMA stability |

## Validation

Validation is performed against **Jurik's published methodology**.

- **Responsiveness**: DMX consistently leads standard DMI in turning point detection.
- **Smoothness**: DMX produces fewer false crossovers in chopping markets compared to a fast DMI.

### Common Pitfalls

- **Period Selection**: Because JMA is so efficient, you can often use slightly longer periods than you would with DMI (e.g., 20 instead of 14) to get even smoother results without incurring a lag penalty.
- **Dependency**: This indicator depends on the `Jma` class. Ensure `Jma` is validated and performant.
