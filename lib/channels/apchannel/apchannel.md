# APCHANNEL: Adaptive Price Channel

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `alpha` (default 0.2)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `⌈3/alpha⌉` bars (default 15)                          |
| **PineScript**   | [apchannel.pine](apchannel.pine)                       |

- APCHANNEL applies exponential smoothing independently to price highs and lows, creating a dynamic envelope that "remembers" significant extremes wh...
- Parameterized by `alpha` (default 0.2).
- Output range: Tracks input.
- Requires `⌈3/alpha⌉` bars (default 15) of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

APCHANNEL applies exponential smoothing independently to price highs and lows, creating a dynamic envelope that "remembers" significant extremes while gradually fading their influence over time. Unlike rigid Donchian channels that drop price extremes abruptly when they exit the lookback window (the "cliff effect"), APCHANNEL decays them smoothly through leaky integration. The result is a channel with continuously sloping boundaries that responds to volatility without the discontinuous jumps that plague fixed-window approaches. The algorithm is $O(1)$ per bar with only two state variables and no buffers.

## Historical Context

Traditional Price Channels (Donchian, 1960s) define range by the absolute highest high and lowest low over a fixed period. When a major high from $n$ bars ago drops out of the window, the upper boundary can collapse instantaneously, producing discontinuous channel behavior that generates false signals. The Adaptive Price Channel addresses this by borrowing the exponential smoothing concept from signal processing, applying the same "leaky integrator" principle that electrical engineers use for envelope detection in AM radio circuits.

The approach is equivalent to running two independent EMAs: one on the High series and one on the Low series. This connection to EMA theory means the channel inherits well-understood convergence properties. The half-life of influence is $\ln(2) / \ln(1/(1-\alpha))$ bars, and the channel is considered warm after approximately $3/\alpha$ bars. The single-parameter design ($\alpha$) makes APCHANNEL simpler to tune than multi-parameter alternatives.

## Architecture & Physics

### 1. Dual EMA Recursion

The upper and lower bands are independent EMA filters on High and Low:

$$\text{Upper}_t = \alpha \cdot H_t + (1 - \alpha) \cdot \text{Upper}_{t-1}$$

$$\text{Lower}_t = \alpha \cdot L_t + (1 - \alpha) \cdot \text{Lower}_{t-1}$$

Using the FMA pattern with $\text{decay} = 1 - \alpha$:

$$\text{Upper}_t = \text{FMA}(\text{decay}, \text{Upper}_{t-1}, \alpha \cdot H_t)$$

### 2. Midpoint

$$\text{Middle}_t = \frac{\text{Upper}_t + \text{Lower}_t}{2}$$

### 3. Alpha Semantics

- **High $\alpha$ (e.g., 0.8)**: Short memory. Channel snaps quickly to new extremes, forgets old ones rapidly.
- **Low $\alpha$ (e.g., 0.1)**: Long memory. Significant highs persist as resistance for dozens of bars.
- **Period approximation**: $\alpha \approx 2 / (P + 1)$ where $P$ is the equivalent EMA period.

### 4. Complexity

$O(1)$ per bar: 2 FMA operations + 1 addition + 1 division. No buffers, no history. The two bands are independent and can be computed in parallel.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `alpha` | Smoothing factor (higher = faster decay) | 0.2 | $(0, 1]$ |

### Initialization

On the first bar:

$$\text{Upper}_0 = H_0, \quad \text{Lower}_0 = L_0$$

### Half-Life

The number of bars for a price extreme's influence to decay by 50%:

$$t_{1/2} = \frac{\ln 2}{\ln(1 / (1 - \alpha))}$$

For $\alpha = 0.2$: $t_{1/2} \approx 3.1$ bars. For $\alpha = 0.05$: $t_{1/2} \approx 13.5$ bars.

### Pseudo-code

```
function APCHANNEL(high, low, alpha):
    validate: 0 < alpha ≤ 1
    decay = 1 - alpha

    // EMA of highs
    if first_bar:
        upper = high
    else:
        upper = decay * upper + alpha * high

    // EMA of lows
    if first_bar:
        lower = low
    else:
        lower = decay * lower + alpha * low

    middle = (upper + lower) / 2

    return [middle, upper, lower]
```

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | Exponentially smoothed high (resistance) |
| `lower` | Exponentially smoothed low (support) |
| `middle` | Arithmetic mean of upper and lower |

## Performance Profile

### Operation Count (Streaming Mode)

APCHANNEL is pure IIR with no buffers. Two independent EMA updates plus a midpoint:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA (decay × Upper + α × H) | 1 | 4 | 4 |
| FMA (decay × Lower + α × L) | 1 | 4 | 4 |
| ADD (Upper + Lower) | 1 | 1 | 1 |
| MUL (× 0.5 for midpoint) | 1 | 3 | 3 |
| **Total (hot)** | **4** | — | **~12 cycles** |

No warmup overhead. First bar initializes directly from input, adding one CMP.

### Batch Mode (SIMD Analysis)

Both EMA recursions are state-dependent ($\text{Upper}_t$ depends on $\text{Upper}_{t-1}$), preventing SIMD parallelization across bars:

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | Already using 2 FMAs per bar; hardware-accelerated |
| State locality | Upper + Lower fit in 2 registers; zero cache pressure |
| Midpoint computation | Vectorizable in a post-pass across output arrays |

## Resources

- **Wilder, J.W.** *New Concepts in Technical Trading Systems*. Trend Research, 1978. (EMA smoothing foundations)
- **Donchian, R.** "Trend Following Methods in Commodity Price Analysis." *Commodity Research Bureau*, 1960. (Fixed-window channel predecessor)
- **Haykin, S.** *Adaptive Filter Theory*. Prentice Hall, 2002. (Leaky integrator / exponential smoothing theory)
