# DSP: Ehlers Detrended Synthetic Price

> *Detrended synthetic price removes the trend to expose the oscillation underneath — the signal beneath the drift.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 40)                      |
| **Outputs**      | Single series (Dsp)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `slowPeriod * 3` bars                          |
| **PineScript**   | [dsp.pine](dsp.pine)                       |

- DSP creates a zero-centered oscillator by subtracting a half-cycle EMA from a quarter-cycle EMA, isolating the dominant cyclical component of price...
- **Similar:** [SSFDSP](../ssfdsp/Ssfdsp.md), [Ccyc](../ccyc/Ccyc.md) | **Complementary:** ATR for volatility filter | **Trading note:** Digital Signal Processing filter; separates signal from noise in price data.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

DSP creates a zero-centered oscillator by subtracting a half-cycle EMA from a quarter-cycle EMA, isolating the dominant cyclical component of price while cancelling longer-term trends. Developed by John Ehlers, the indicator is grounded in cycle theory rather than arbitrary period selection, making it a principled alternative to MACD for cycle-aware trading. Bias-corrected EMAs ensure accurate amplitude during warmup.

## Historical Context

John Ehlers introduced the Detrended Synthetic Price as part of his cycle analytics framework. While MACD uses fixed periods (12/26), DSP calibrates its two EMAs to specific fractions of the dominant cycle period: quarter-cycle for the fast component and half-cycle for the slow. Subtracting aligned filters at these frequencies effectively bandpass-isolates the cycle of interest while suppressing both high-frequency noise and low-frequency trend. The "synthetic" label reflects that the output is a constructed signal that exposes cyclical energy invisible in raw price.

## Architecture & Physics

### 1. Component Periods

From the user-specified dominant cycle period $P$:

$$P_{fast} = \max(2, \lfloor P / 4 + 0.5 \rfloor)$$

$$P_{slow} = \max(3, \lfloor P / 2 + 0.5 \rfloor)$$

### 2. Alpha Coefficients

Standard EMA smoothing factors:

$$\alpha_{fast} = \frac{2}{P_{fast} + 1}, \qquad \alpha_{slow} = \frac{2}{P_{slow} + 1}$$

### 3. EMA Updates with Bias Correction

Raw EMA recursion:

$$EMA_{raw,t} = \alpha \cdot P_t + (1 - \alpha) \cdot EMA_{raw,t-1}$$

Warmup bias correction (prevents initial distortion):

$$EMA_t = \frac{EMA_{raw,t}}{1 - (1 - \alpha)^n}$$

where $n$ is the number of bars processed.

### 4. DSP Output

$$DSP_t = EMA_{fast,t} - EMA_{slow,t}$$

### 5. Complexity

$O(1)$ per bar with $O(1)$ memory. Two EMA state variables plus two bias correction accumulators.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Dominant cycle period | 40 | $\geq 4$ |

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| $DSP > 0$ | Fast EMA above slow: bullish cycle phase |
| $DSP < 0$ | Fast EMA below slow: bearish cycle phase |
| Zero crossing | Cycle phase transition point |
| Divergence from price | Cycle energy waning; potential trend exhaustion |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 3 | 1 | 3 |
| MUL | 4 | 3 | 12 |
| FMA | 2 | 4 | 8 |
| DIV | 2 | 15 | 30 |
| **Total** | **11** | — | **~53 cycles** |

O(1) per bar. Two EMA updates (fast + slow) using FMA, plus warmup bias-correction divisions. After warmup completes, the DIV cost drops to zero, reducing steady-state to ~23 cycles.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Bias-corrected EMAs eliminate warmup distortion |
| **Timeliness** | 8/10 | Quarter-cycle EMA responds quickly; half-cycle provides reference |
| **Smoothness** | 8/10 | Dual EMA differencing inherently smooths noise |
| **Memory** | 10/10 | O(1) state: 6 scalar values in record struct |

## Resources

- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
