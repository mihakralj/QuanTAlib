# SSFDSP: Ehlers SSF Detrended Synthetic Price

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 40)                      |
| **Outputs**      | Single series (SsfDsp)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `slowPeriod * 2` bars                          |

### TL;DR

- SSFDSP isolates the dominant cycle by subtracting a half-cycle Super-Smoother from a quarter-cycle Super-Smoother, producing a zero-centered oscill...
- Parameterized by `period` (default 40).
- Output range: Varies (see docs).
- Requires `slowPeriod * 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

SSFDSP isolates the dominant cycle by subtracting a half-cycle Super-Smoother from a quarter-cycle Super-Smoother, producing a zero-centered oscillator with superior noise rejection compared to the EMA-based DSP. The 2-pole Butterworth characteristic of the Super-Smoother filter provides zero phase lag at the cutoff frequency and sharper rolloff than exponential smoothing, making SSFDSP the preferred variant for cycle-aware trading when the approximate dominant period is known.

## Historical Context

John Ehlers introduced the concept of Detrended Synthetic Price in *Cybernetic Analysis for Stocks and Futures* (2004) as a principled method for removing the DC (trend) component while preserving cyclical energy. The original DSP used EMAs, which have a gradual frequency rolloff and non-zero phase lag. The SSF variant substitutes Super-Smoother filters, which are 2-pole Butterworth low-pass designs with matched coefficients that eliminate the Gibbs phenomenon (ringing) common in sharper filters. The result is a cleaner cycle extraction: the SSF's steeper rolloff better separates the quarter-cycle and half-cycle frequency bands, producing tighter zero crossings and more reliable turning point identification than EMA-DSP.

## Architecture & Physics

### 1. Filter Periods

From the user-specified dominant cycle period $P$:

$$P_{fast} = \max(2, \lfloor P / 4 + 0.5 \rfloor)$$

$$P_{slow} = \max(3, \lfloor P / 2 + 0.5 \rfloor)$$

### 2. Super-Smoother Coefficients

For each filter period $p$:

$$\alpha = \frac{\pi\sqrt{2}}{p}$$

$$c_2 = 2 e^{-\alpha} \cos(\alpha)$$

$$c_3 = -e^{-2\alpha}$$

$$c_1 = 1 - c_2 - c_3$$

### 3. SSF Recursion

$$SSF_t = c_1 \cdot \frac{P_t + P_{t-1}}{2} + c_2 \cdot SSF_{t-1} + c_3 \cdot SSF_{t-2}$$

The 2-bar input averaging provides an additional anti-aliasing stage.

### 4. SSFDSP Output

$$SSFDSP_t = SSF_{fast,t} - SSF_{slow,t}$$

### 5. Complexity

$O(1)$ per bar. Two independent 2-pole IIR filters with $O(1)$ memory. Warmup: approximately $2 \times P_{slow}$ for convergence. Recursive dependencies prevent SIMD vectorization.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Expected dominant cycle period | 40 | $\geq 4$ |

### Super-Smoother Frequency Response

The SSF has $-3$ dB attenuation at the cutoff period, $-12$ dB/octave rolloff (2-pole), and zero phase lag at the cutoff. This is equivalent to a critically-damped Butterworth filter.

### Pseudo-code

```
function SSFDSP(source, period):
    pFast ← max(2, round(period / 4))
    pSlow ← max(3, round(period / 2))

    // Fast SSF coefficients
    αf ← √2·π / pFast
    c2f ← 2·exp(-αf)·cos(αf)
    c3f ← -exp(-2·αf)
    c1f ← 1 - c2f - c3f

    // Slow SSF coefficients
    αs ← √2·π / pSlow
    c2s ← 2·exp(-αs)·cos(αs)
    c3s ← -exp(-2·αs)
    c1s ← 1 - c2s - c3s

    ssfFast_1 ← 0; ssfFast_2 ← 0
    ssfSlow_1 ← 0; ssfSlow_2 ← 0
    p_prev ← 0

    for each price in source:
        // Input averaging
        avg ← (price + p_prev) / 2

        // Fast SSF update
        ssfFast ← c1f·avg + c2f·ssfFast_1 + c3f·ssfFast_2

        // Slow SSF update
        ssfSlow ← c1s·avg + c2s·ssfSlow_1 + c3s·ssfSlow_2

        // SSFDSP
        ssfdsp ← ssfFast - ssfSlow

        // Shift state
        ssfFast_2 ← ssfFast_1; ssfFast_1 ← ssfFast
        ssfSlow_2 ← ssfSlow_1; ssfSlow_1 ← ssfSlow
        p_prev ← price

        emit ssfdsp
```

### DSP vs SSFDSP

| Aspect | DSP (EMA-based) | SSFDSP (Super-Smoother) |
|--------|-----------------|------------------------|
| Filter type | 1-pole IIR (exponential) | 2-pole Butterworth |
| Rolloff | $-6$ dB/octave | $-12$ dB/octave |
| Phase lag at cutoff | Non-zero | Zero |
| Noise rejection | Moderate | Superior |
| Turning points | Rounded | Sharper |

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| $SSFDSP > 0$ | Bullish cycle phase |
| $SSFDSP < 0$ | Bearish cycle phase |
| Zero crossing | Cycle phase transition |
| Divergence with price | Cycle energy waning; trend exhaustion |
| Amplitude shrinking | Cycle losing dominance; transition to trend |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| Input averaging | ~2 | 1 ADD + 1 MUL(×0.5) |
| Fast SSF (2-pole IIR) | ~5 | 1 MUL(c1f) + 2 FMA(c2f, c3f) |
| Slow SSF (2-pole IIR) | ~5 | 1 MUL(c1s) + 2 FMA(c2s, c3s) |
| Subtraction (output) | ~1 | 1 SUB |
| State shift | ~5 | 5 register moves |
| **Total** | **~18** | **O(1) fixed; pure FMA arithmetic, zero transcendentals** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | No: both SSF filters are recursive 2-pole IIR with sequential state dependencies |
| Bottleneck | None significant; pure multiply-accumulate with precomputed coefficients |
| Parallelism | None: each bar depends on two previous bars' filter state |
| Memory | O(1): 4 scalar filter states + 1 previous price (~40 bytes) |
| Throughput | Among fastest cycle indicators; comparable to dual-EMA DSP; no transcendentals at runtime |

## Resources

- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Ehlers, J.F.** *Cycle Analytics for Traders*. Wiley, 2013.
- **Butterworth, S.** "On the Theory of Filter Amplifiers." *Experimental Wireless*, 7, 1930.
