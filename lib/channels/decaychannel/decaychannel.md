# DECAYCHANNEL: Decay Min-Max Channel

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

### Pseudo-code

```
function DECAYCHANNEL(high, low, period):
    validate: period > 0
    lambda = ln(2) / period

    // Scan buffer for Donchian bounds
    periodMax = max(high_buffer over period)
    periodMin = min(low_buffer over period)
    periodAvg = avg(midpoints over period)

    // Snap or age
    if high ≥ currentMax:
        currentMax = high; ageMax = 0
    else:
        ageMax += 1

    if low ≤ currentMin:
        currentMin = low; ageMin = 0
    else:
        ageMin += 1

    // Decay toward midpoint
    midpoint = (currentMax + currentMin) / 2
    maxDecay = 1 - exp(-lambda * ageMax)
    minDecay = 1 - exp(-lambda * ageMin)
    currentMax -= maxDecay * (currentMax - midpoint)
    currentMin -= minDecay * (currentMin - midpoint)

    // Clamp to Donchian bounds
    currentMax = min(currentMax, periodMax)
    currentMin = max(currentMin, periodMin)

    return [currentMax, currentMin]
```

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | Decayed high (resistance that fades with time) |
| `lower` | Decayed low (support that fades with time) |

## Resources

- **Rutherford, E.** "Radioactive Substances and their Radiations." Cambridge University Press, 1913. (Exponential decay / half-life mathematics)
- **Newton, I.** "Scala Graduum Caloris." *Philosophical Transactions*, 1701. (Newton's Law of Cooling)
- **Donchian, R.** "High Finance in Copper." *Financial Analysts Journal*, 1960. (Donchian Channel predecessor)
