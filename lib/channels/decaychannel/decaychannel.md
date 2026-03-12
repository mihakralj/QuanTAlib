# DECAYCHANNEL: Decay Min-Max Channel

> *Extremes that decay over time give recent boundaries more weight, fading yesterday's peaks gradually.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [decaychannel.pine](decaychannel.pine)                       |

- Decay Channel combines the absolute price boundaries of Donchian Channels with exponential decay toward the midpoint, creating an envelope that exp...
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Decay Channel combines the absolute price boundaries of Donchian Channels with exponential decay toward the midpoint, creating an envelope that expands instantly on new volatility but contracts smoothly during consolidation. While Donchian Channels hold their width until an extreme exits the lookback window, Decay Channel allows the bands to "forget" old extremes over time using a half-life model. The period parameter serves as the half-life: after that many bars without a new extreme, the band has decayed 50% of the distance back toward center. The decayed values are always clamped within Donchian bounds, ensuring they never extrapolate beyond actual price history.

## Historical Context

The Decay Channel is a QuanTAlib design that applies principles from physics — specifically radioactive decay and Newton's Law of Cooling — to price channel construction. Standard Donchian Channels exhibit a discontinuous "cliff edge" behavior: bands remain static until an old extreme exits the lookback window, then jump abruptly. This doesn't reflect how markets work: traders naturally give less weight to older price extremes as time passes.

The mathematical foundation uses the decay constant $\lambda = \ln(2)/T$, the same formula used in carbon dating and thermal cooling. A signal extreme from $T$ bars ago retains exactly half its influence on band width. This produces asymmetric behavior that matches market reality: breakouts are sudden (bands snap to new extremes), consolidations are gradual (bands decay smoothly).

## Architecture & Physics

### 1. Decay Constant

$$\lambda = \frac{\ln 2}{\text{period}}$$

### 2. Extreme Tracking

For each bar, the algorithm tracks how many bars have elapsed since the last new high (or low):

- If $H_t \geq \text{currentMax}$: snap $\text{currentMax} = H_t$, reset $\text{age}_{\max} = 0$
- Otherwise: increment $\text{age}_{\max}$

Symmetric logic for the minimum.

### 3. Exponential Decay Toward Midpoint

When no new extreme occurs, the band decays toward the channel midpoint:

$$\text{decayRate} = 1 - e^{-\lambda \cdot \text{age}}$$

$$\text{midpoint} = \frac{\text{currentMax} + \text{currentMin}}{2}$$

$$\text{currentMax} \leftarrow \text{currentMax} - \text{decayRate} \cdot (\text{currentMax} - \text{midpoint})$$

$$\text{currentMin} \leftarrow \text{currentMin} - \text{decayRate} \cdot (\text{currentMin} - \text{midpoint})$$

### 4. Donchian Clamping

The decayed values are constrained to never exceed the raw Donchian extremes:

$$\text{Upper} = \min(\text{currentMax},\; \text{DonchianUpper})$$

$$\text{Lower} = \max(\text{currentMin},\; \text{DonchianLower})$$

### 5. Complexity

The Donchian scan is $O(n)$ per bar in the reference implementation (loop over the buffer). The decay computation adds 2 exponentials per bar. Total: $O(n)$ per bar.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Half-life in bars and Donchian lookback window | 100 | $> 0$ |

### Half-Life Property

After $T$ bars without a new extreme, the band has decayed exactly 50% of the distance from its initial position to the midpoint:

$$\text{decayRate}(T) = 1 - e^{-\lambda T} = 1 - e^{-\ln 2} = 0.5$$

After $2T$ bars: 75% decay. After $3T$ bars: 87.5% decay.

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | Decayed high (resistance that fades with time) |
| `lower` | Decayed low (support that fades with time) |

## Performance Profile

### Operation Count (Streaming Mode)

DECAYCHANNEL scans the circular buffer for Donchian bounds ($O(n)$) plus exponential decay computation:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (scan buffer for max, $n$ bars) | $n$ | 1 | $n$ |
| CMP (scan buffer for min, $n$ bars) | $n$ | 1 | $n$ |
| CMP (H ≥ currentMax, snap check) | 1 | 1 | 1 |
| CMP (L ≤ currentMin, snap check) | 1 | 1 | 1 |
| MUL (-λ × age) | 2 | 3 | 6 |
| EXP (e^{-λ·age}, two bands) | 2 | 25 | 50 |
| SUB (1 - exp result) | 2 | 1 | 2 |
| MUL + SUB (decay × distance) | 2 | 4 | 8 |
| ADD (midpoint) | 1 | 1 | 1 |
| MUL (× 0.5) | 1 | 3 | 3 |
| CMP (clamp to Donchian) | 2 | 1 | 2 |
| **Total** | **$2n + 14$** | — | **~$2n + 74$ cycles** |

For period 100: ~274 cycles/bar. The two EXP calls and the $O(n)$ Donchian scan dominate.

### Batch Mode (SIMD Analysis)

The Donchian scan is vectorizable for max/min reduction. The decay computation per bar depends on mutable age counters, limiting parallelism:

| Optimization | Benefit |
| :--- | :--- |
| Donchian max/min scan | Vectorizable with `Vector.Max` / `Vector.Min` reduction |
| EXP computation | Sequential (depends on age state) |
| Decay application + clamping | Sequential (depends on currentMax/Min state) |

## Resources

- **Rutherford, E.** "Radioactive Substances and their Radiations." Cambridge University Press, 1913. (Exponential decay / half-life mathematics)
- **Newton, I.** "Scala Graduum Caloris." *Philosophical Transactions*, 1701. (Newton's Law of Cooling)
- **Donchian, R.** "High Finance in Copper." *Financial Analysts Journal*, 1960. (Donchian Channel predecessor)
