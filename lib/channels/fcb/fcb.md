# FCB: Fractal Chaos Bands

> *Fractal chaos bands connect swing pivots into a channel, letting the market's own geometry define containment.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period + 2` bars                          |
| **PineScript**   | [fcb.pine](fcb.pine)                       |

- Fractal Chaos Bands filter raw price action through Bill Williams' fractal detection logic, tracking the highest confirmed fractal high and lowest ...
- **Similar:** [DC](../dc/dc.md), [PC](../pc/pc.md) | **Complementary:** Williams Fractals for additional confirmation | **Trading note:** Based on Bill Williams' fractal theory; bands update only on fractal pivots, creating a staircase pattern.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Fractal Chaos Bands filter raw price action through Bill Williams' fractal detection logic, tracking the highest confirmed fractal high and lowest confirmed fractal low over a lookback period. Unlike Donchian Channels which use every bar's high and low, FCB uses only structurally significant turning points — bars where the middle element of a 3-bar pattern is a local extremum. The result is a "cleaner" channel that ignores transient spikes and focuses on confirmed support and resistance levels. The bands tend to remain flat during trends and step discretely when new structural pivots form, making them useful for identifying genuine breakouts versus noise.

## Historical Context

Bill Williams introduced fractal analysis to financial markets in *Trading Chaos* (1995) and *New Trading Dimensions* (1998), drawing inspiration from Benoit Mandelbrot's work on fractal geometry and chaos theory. Williams defined a fractal as a 5-bar pattern (later simplified to 3-bar in many implementations) where the central bar represents a local extremum — a point where supply and demand reached temporary equilibrium.

The Fractal Chaos Bands indicator extends Williams' fractal concept by tracking the monotonic extremes of these structural turning points over a lookback window, rather than raw price extremes. This filtering eliminates noise from transient wicks and gap spikes while preserving meaningful market structure. The 3-bar fractal requires one future bar for confirmation, providing inherent stability at the cost of a 1-bar lag.

## Architecture & Physics

### 1. Fractal Detection (3-Bar Pattern)

**Up Fractal** (local high at $t-1$):

$$\text{UpFractal}_t = (H_{t-1} > H_{t-2}) \;\wedge\; (H_{t-1} > H_t)$$

**Down Fractal** (local low at $t-1$):

$$\text{DownFractal}_t = (L_{t-1} < L_{t-2}) \;\wedge\; (L_{t-1} < L_t)$$

### 2. Fractal Value Tracking

A persistent state variable holds the most recent fractal value:

- On up fractal confirmation: $\text{hiFractal} = H_{t-1}$
- On down fractal confirmation: $\text{loFractal} = L_{t-1}$
- Between fractals: values persist (hold last fractal)

### 3. Band Construction (Sliding Window Max/Min of Fractals)

$$\text{Upper}_t = \max(\text{hiFractal values over period})$$

$$\text{Lower}_t = \min(\text{loFractal values over period})$$

The monotonic deque operates on fractal values rather than raw prices, using the same $O(1)$ amortized algorithm as Donchian Channels.

### 4. Structural Filtering Property

FCB bands are always within or equal to Donchian bounds:

$$\text{FCB}_{\text{Upper}} \leq \text{Donchian}_{\text{Upper}}$$

$$\text{FCB}_{\text{Lower}} \geq \text{Donchian}_{\text{Lower}}$$

This is because fractals are a subset of raw highs/lows. A breakout above FCB upper is more significant than above Donchian upper because it breaks a confirmed structural level.

### 5. Complexity

Fractal detection is $O(1)$ (3 comparisons). Deque maintenance is $O(1)$ amortized. Total: $O(1)$ amortized per bar.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window for highest/lowest fractal values | 20 | $> 0$ |

### Output Interpretation

| Output | Description |
|--------|-------------|
| `upper` | Highest confirmed fractal high over lookback (structural resistance) |
| `lower` | Lowest confirmed fractal low over lookback (structural support) |

## Performance Profile

### Operation Count (Streaming Mode)

FCB combines 3-bar fractal detection ($O(1)$) with two monotonic deques for sliding-window max/min of fractal values:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| CMP (H[t-1] > H[t-2]) | 1 | 1 | 1 |
| CMP (H[t-1] > H[t]) | 1 | 1 | 1 |
| CMP (L[t-1] < L[t-2]) | 1 | 1 | 1 |
| CMP (L[t-1] < L[t]) | 1 | 1 | 1 |
| Deque ops (max, amortized) | ~2 | 1 | 2 |
| Deque ops (min, amortized) | ~2 | 1 | 2 |
| **Total (amortized)** | **~8** | — | **~8 cycles** |

The fractal detection requires retaining 3 bars of H and L history (6 values). Between fractals, only the deque expiry/push operations execute. Fractal confirmation adds one assignment per detected fractal.

### Batch Mode (SIMD Analysis)

Fractal detection involves comparisons that could theoretically be vectorized, but the conditional fractal-value tracking and deque operations are sequential:

| Optimization | Benefit |
| :--- | :--- |
| Fractal detection (4 comparisons) | Vectorizable with `Vector.GreaterThan` / `Vector.LessThan` |
| Deque max/min maintenance | Sequential (amortized O(1) already optimal) |
| Fractal value persistence | Sequential (conditional state update) |

## Resources

- **Williams, B.** *Trading Chaos*. Wiley, 1995. (Original fractal definition for markets)
- **Williams, B.** *New Trading Dimensions*. Wiley, 1998. (Extended fractal analysis)
- **Mandelbrot, B.** *The Fractal Geometry of Nature*. W.H. Freeman, 1982. (Mathematical fractal theory)
