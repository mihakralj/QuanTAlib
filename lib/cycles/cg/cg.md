# CG: Ehlers Center of Gravity

> *Center of Gravity locates the balance point of price over a window, anticipating turns before they arrive.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 10)                      |
| **Outputs**      | Single series (Cg)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [cg.pine](cg.pine)                       |

- CG identifies potential turning points using the physics concept of weighted center of mass applied to a price window.
- **Similar:** [Ccyc](../ccyc/Ccyc.md), [ACP](../acp/acp.md) | **Complementary:** RSI for momentum confirmation | **Trading note:** Center of Gravity oscillator by Ehlers; leads price turns with minimal lag.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

CG identifies potential turning points using the physics concept of weighted center of mass applied to a price window. Developed by John Ehlers, the oscillator measures where the "weight" of prices is concentrated within a lookback period, producing a leading indicator that oscillates around zero with minimal lag compared to traditional moving average crossover systems.

## Historical Context

John Ehlers introduced the Center of Gravity oscillator in *Cybernetic Analysis for Stocks and Futures* (2002). Drawing from classical mechanics, the indicator applies the concept that the center of mass of a distribution reveals its balance point. In the price context, the CG identifies where momentum is concentrated within a sliding window. Unlike momentum oscillators that differentiate price (and amplify noise), CG integrates position-weighted price, providing smoother turning point detection. The indicator's leading characteristic arises from the weighting scheme: as new prices shift the balance point, the CG responds before the window's simple average would.

## Architecture & Physics

### 1. Weighted Sum (Numerator)

Position-weighted accumulation over the lookback window:

$$Num = \sum_{i=1}^{n} i \cdot P_{t-n+i}$$

where $i$ ranges from 1 (oldest) to $n$ (newest), giving linearly increasing weight to more recent data.

### 2. Simple Sum (Denominator)

$$Den = \sum_{i=1}^{n} P_{t-n+i}$$

### 3. Center of Gravity

$$CG_t = \frac{Num}{Den} - \frac{n + 1}{2}$$

The term $\frac{n + 1}{2}$ is the geometric center of the window, centering the output around zero. When recent prices dominate, $CG > 0$ (bullish); when older prices dominate, $CG < 0$ (bearish).

### 4. Complexity

Streaming uses running sums for both numerator and denominator: $O(1)$ per bar with $O(n)$ memory for the ring buffer.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback window length | 10 | $> 0$ |

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| $CG > 0$ | Weight concentrated in recent prices (bullish momentum) |
| $CG < 0$ | Weight concentrated in older prices (bearish momentum) |
| Zero crossing up | Momentum shifting bullish |
| Zero crossing down | Momentum shifting bearish |
| Hanging at extremes | Strong trend in progress |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 2×N | 1 | 2N |
| MUL | N | 3 | 3N |
| DIV | 1 | 15 | 15 |
| **Total** | **~3N+1** | — | **~5N+15** |

The `RecalculateSums()` loop iterates over the full buffer each bar, making this O(N) per bar. For default $N = 10$: ~65 cycles. A periodic resync every 1000 bars maintains numerical stability.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact weighted center-of-mass calculation |
| **Timeliness** | 9/10 | Leads price movement by construction |
| **Smoothness** | 7/10 | Raw oscillator; no internal smoothing |
| **Memory** | 9/10 | O(N) ring buffer + 2 running sums |

## Resources

- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2002.
- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
