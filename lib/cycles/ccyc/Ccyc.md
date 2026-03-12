# CCYC: Ehlers Cyber Cycle

> *The Cyber Cycle isolator extracts the dominant cycle component while suppressing trend — pure periodicity distilled.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `alpha` (default 0.07)                      |
| **Outputs**      | Single series (Ccyc)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `7` bars                          |
| **PineScript**   | [ccyc.pine](ccyc.pine)                       |

- CCYC isolates the dominant cycle component from price data using a 2-pole high-pass IIR filter applied to a 4-tap FIR-smoothed input, producing an ...
- Parameterized by `alpha` (default 0.07).
- Output range: Varies (see docs).
- Requires `7` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

CCYC isolates the dominant cycle component from price data using a 2-pole high-pass IIR filter applied to a 4-tap FIR-smoothed input, producing an oscillator that strips trend while preserving cyclical content with minimal lag. The companion trigger line (one-bar delay of the cycle output) provides crossover signals for timing entries and exits. Unlike band-pass approaches that require specifying a center frequency, CCYC's high-pass architecture extracts whatever cyclic energy exists above a cutoff controlled by a single $\alpha$ damping parameter, making it adaptive to the dominant period present in the data.

## Historical Context

John F. Ehlers introduced the Cyber Cycle in Chapter 4 of *Cybernetic Analysis for Stocks and Futures* (Wiley, 2004) as part of his broader framework applying digital signal processing to market data. The name "cybernetic" references Norbert Wiener's cybernetics, the study of control and communication in systems, reflecting Ehlers' view that markets are feedback-driven systems with measurable oscillatory modes.

The Cyber Cycle was designed to solve a specific problem with earlier cycle extraction methods: the Hilbert Transform phasor (Ehlers, 2001) and simple band-pass filters both require assumptions about the dominant period. The Cyber Cycle's high-pass design avoids this by removing trend (the zero-frequency component) and letting whatever cycle energy remains pass through. The 4-tap FIR pre-smoother eliminates 2-bar and 3-bar cycle artifacts that would otherwise contaminate the output with aliased noise.

The $\alpha$ parameter controls the high-pass cutoff: smaller values of $\alpha$ push the cutoff to lower frequencies, extracting only long-period cycles and producing smoother output at the cost of additional lag. The default $\alpha = 0.07$ was empirically tuned by Ehlers to balance responsiveness against noise rejection for typical equity and futures data on daily timeframes.

## Architecture & Physics

### 1. FIR Pre-Smoother (4-Tap)

A weighted average eliminates 2-bar and 3-bar cycle components:

$$\text{smooth}_t = \frac{x_t + 2x_{t-1} + 2x_{t-2} + x_{t-3}}{6}$$

The weights $[1, 2, 2, 1] / 6$ form a symmetric FIR kernel. The frequency response has exact zeros at periods 2 and 3 bars, which are below the Nyquist frequency for most meaningful market cycles and represent sampling artifacts rather than real cyclical content.

### 2. High-Pass IIR Filter (2-Pole)

The core filter applies a second-order high-pass IIR to the smoothed input:

$$\text{cycle}_t = c_{hp} \cdot (\text{smooth}_t - 2 \cdot \text{smooth}_{t-1} + \text{smooth}_{t-2}) + c_{fb1} \cdot \text{cycle}_{t-1} + c_{fb2} \cdot \text{cycle}_{t-2}$$

Where the coefficients are derived from $\alpha$:

$$c_{hp} = (1 - 0.5\alpha)^2$$

$$c_{fb1} = 2(1 - \alpha)$$

$$c_{fb2} = -(1 - \alpha)^2$$

The term $(\text{smooth}_t - 2 \cdot \text{smooth}_{t-1} + \text{smooth}_{t-2})$ is a second-difference operator, equivalent to a discrete Laplacian that emphasizes curvature. The feedback terms $c_{fb1}$ and $c_{fb2}$ create resonance, amplifying signals near the natural frequency determined by $\alpha$.

### 3. Initialization Bootstrap

For the first 6 bars (before the IIR has sufficient history), the cycle is bootstrapped using a simple second-difference of raw price:

$$\text{cycle}_t = \frac{x_t - 2x_{t-1} + x_{t-2}}{4} \quad \text{for } t < 7$$

This seeds the IIR filter with a reasonable approximation, allowing convergence within the first few cycles rather than requiring a long zero-state warmup.

### 4. Trigger Line

$$\text{trigger}_t = \text{cycle}_{t-1}$$

A one-bar delay. Crossovers between cycle and trigger identify turning points.

### 5. Complexity

The algorithm is $O(1)$ per bar: 6 multiplications, 5 additions, and 2 state variables. No loops, no window scans, no allocations.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `alpha` | Damping factor controlling high-pass cutoff | 0.07 | $(0, 1)$ exclusive |
| `source` | Input price series | hl2 | |

### z-Domain Transfer Function

The FIR pre-smoother:

$$H_{\text{FIR}}(z) = \frac{1 + 2z^{-1} + 2z^{-2} + z^{-3}}{6}$$

The IIR high-pass filter:

$$H_{\text{IIR}}(z) = \frac{c_{hp}(1 - 2z^{-1} + z^{-2})}{1 - c_{fb1} z^{-1} - c_{fb2} z^{-2}}$$

Combined transfer function:

$$H(z) = H_{\text{FIR}}(z) \cdot H_{\text{IIR}}(z)$$

### Coefficient Derivation

Given damping factor $\alpha \in (0, 1)$:

$$c_{hp} = \left(1 - \frac{\alpha}{2}\right)^2$$

$$c_{fb1} = 2(1 - \alpha)$$

$$c_{fb2} = -(1 - \alpha)^2$$

The characteristic equation of the IIR section:

$$z^2 - 2(1-\alpha)z + (1-\alpha)^2 = 0$$

has a double pole at $z = 1 - \alpha$. For $\alpha = 0.07$, the pole is at $z = 0.93$, well inside the unit circle (stable), with a $-3$ dB cutoff period of approximately $\frac{2\pi}{\alpha} \approx 90$ bars.

### Output Interpretation

| Output | Description |
|--------|-------------|
| `cycle` | Dominant cycle component, zero-mean oscillator |
| `trigger` | One-bar delayed cycle for crossover detection |

### Signal Interpretation

- **Cycle crosses above trigger**: potential cycle trough (buy signal)
- **Cycle crosses below trigger**: potential cycle peak (sell signal)
- **Both near zero**: minimal cyclic energy; trend-dominated regime

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 5 | 1 | 5 |
| MUL | 6 | 3 | 18 |
| FMA | 2 | 4 | 8 |
| **Total** | **13** | — | **~31 cycles** |

O(1) per bar. The 4-tap FIR smoother uses 3 MUL + 2 ADD; the 2-pole IIR high-pass uses 2 FMA + 1 MUL. Bootstrap path (bars < 7) is even cheaper: 2 MUL + 1 SUB.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | IIR filter faithfully isolates cycle component |
| **Timeliness** | 9/10 | Only 7-bar warmup; 2 state variables converge fast |
| **Smoothness** | 8/10 | 4-tap FIR + 2-pole IIR suppresses aliased noise |
| **Memory** | 10/10 | O(1) state: 12 scalar values in record struct |

## Resources

- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004. Chapter 4: "Cyber Cycle."
- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001. (Foundational DSP framework for markets)
- **Ehlers, J.F.** "Cybernetic Analysis." *Technical Analysis of Stocks & Commodities*, 2004.
- **Wiener, N.** *Cybernetics: Or Control and Communication in the Animal and the Machine*. MIT Press, 1948. (Origin of the "cybernetic" terminology)
- **Oppenheim, A.V. & Schafer, R.W.** *Discrete-Time Signal Processing*. Pearson, 2009. (IIR filter theory, z-domain analysis)
