# HT_DCPERIOD: Ehlers Hilbert Transform Dominant Cycle Period

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (HT_DCPERIOD)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `LOOKBACK` bars                          |
| **PineScript**   | [ht_dcperiod.pine](ht_dcperiod.pine)                       |

- HT_DCPERIOD estimates the period of the dominant market cycle using Ehlers' Hilbert Transform cascade.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `LOOKBACK` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HT_DCPERIOD estimates the period of the dominant market cycle using Ehlers' Hilbert Transform cascade. The algorithm extracts In-Phase and Quadrature components from price, computes instantaneous phase via homodyne discrimination, and derives the period from the phase rate of change. Output is a continuously varying period (typically 6-50 bars) compatible with TA-Lib's `HT_DCPERIOD` function. The indicator enables dynamic tuning of other indicators to the market's actual rhythm rather than fixed-parameter assumptions.

## Historical Context

John Ehlers introduced the Hilbert Transform Dominant Cycle Period in *Rocket Science for Traders* (2001) to overcome the fundamental limitation of fixed-period technical indicators. Markets cycle at variable rates, yet traditional indicators like RSI-14 or SMA-20 assume constant periodicity. HT_DCPERIOD measures the actual cycle length present in price data, enabling adaptive parameter selection. The TA-Lib implementation codified specific Hilbert Transform coefficients ($A = 0.0962$, $B = 0.5769$) and smoothing algorithms that became the de facto standard. QuanTAlib matches the TA-Lib implementation within floating-point tolerance, including the 32-bar lookback convention.

## Architecture & Physics

### 1. WMA Price Smoothing

A 4-bar weighted moving average removes Nyquist-frequency noise:

$$SmoothPrice_t = \frac{4P_t + 3P_{t-1} + 2P_{t-2} + P_{t-3}}{10}$$

### 2. Hilbert Transform FIR

The discrete Hilbert approximation generates the detrender and quadrature components using coefficients $A = 0.0962$ and $B = 0.5769$. The detrender, $Q_1$, and Hilbert transforms of $I_1$ and $Q_1$ ($jI$, $jQ$) are all computed with the same 4-tap FIR structure.

### 3. Phasor Components

$$I_{2,t} = I_{1,t} - jQ_t, \qquad Q_{2,t} = Q_{1,t} + jI_t$$

Both smoothed with EMA ($\alpha = 0.2$).

### 4. Homodyne Period Extraction

$$Re_t = 0.2(I_{2,t} \cdot I_{2,t-1} + Q_{2,t} \cdot Q_{2,t-1}) + 0.8 \cdot Re_{t-1}$$

$$Im_t = 0.2(I_{2,t} \cdot Q_{2,t-1} - Q_{2,t} \cdot I_{2,t-1}) + 0.8 \cdot Im_{t-1}$$

$$Period_{raw} = \frac{2\pi}{\arctan(Im_t / Re_t)}$$

### 5. Period Smoothing

Clamped to $[6, 50]$ bars, then smoothed:

$$Period_t = 0.33 \cdot Period_{raw} + 0.67 \cdot Period_{t-1}$$

### 6. Complexity

$O(1)$ per bar. Fixed Hilbert cascade with circular buffers totaling approximately 1.2 KB per instance. Warmup: 32 bars (TA-Lib lookback).

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

The period range [6, 50] and all smoothing constants are fixed by the TA-Lib specification.

### Pseudo-code

```
function HT_DCPERIOD(source):
    A ← 0.0962; B ← 0.5769
    smoothBuf ← CircularBuffer(7)
    detBuf, q1Buf, i1Buf ← CircularBuffers

    I2 ← 0; Q2 ← 0
    Re ← 0; Im ← 0
    period ← 15   // initial estimate

    for each price in source:
        // Step 1: WMA smooth
        smooth ← (4·price + 3·p[1] + 2·p[2] + p[3]) / 10

        // Step 2: Hilbert FIR (adaptive to period)
        adj ← A + B   // coefficient adjustment
        det ← adj·(smooth[0] - smooth[6]) + B·(smooth[2] - smooth[4])
        Q1 ← adj·(det[0] - det[6]) + B·(det[2] - det[4])
        I1 ← det[3]
        jI ← adj·(I1[0] - I1[6]) + B·(I1[2] - I1[4])
        jQ ← adj·(Q1[0] - Q1[6]) + B·(Q1[2] - Q1[4])

        // Step 3: Phasor (EMA smoothed)
        I2 ← 0.2·(I1 - jQ) + 0.8·I2
        Q2 ← 0.2·(Q1 + jI) + 0.8·Q2

        // Step 4: Homodyne discriminator
        Re ← 0.2·(I2·I2_prev + Q2·Q2_prev) + 0.8·Re
        Im ← 0.2·(I2·Q2_prev - Q2·I2_prev) + 0.8·Im

        // Step 5: Period
        if Im ≠ 0 and Re ≠ 0:
            p ← 2π / atan(Im / Re)
        p ← clamp(p, 6, 50)
        period ← 0.33·p + 0.67·period

        emit period
```

### Output Interpretation

| Output | Meaning |
|--------|---------|
| `period` $\approx 6$-$15$ | Short-cycle market; fast oscillator settings appropriate |
| `period` $\approx 15$-$30$ | Medium-cycle; standard indicator periods work |
| `period` $\approx 30$-$50$ | Long-cycle or trending; period drifting toward upper bound suggests trend |
| Stable value | Regular cyclical market, ideal for oscillator-based strategies |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| 4-bar WMA | ~5 | 3 MUL + 1 ADD + 1 MUL(×0.1) |
| Hilbert FIR (detrender) | ~7 | 4-tap FIR with period-adaptive coefficients |
| Hilbert FIR (Q1) | ~7 | Same structure applied to detrender buffer |
| Hilbert FIR (jI) | ~7 | Applied to I1 history buffer |
| Hilbert FIR (jQ) | ~7 | Applied to Q1 history buffer |
| Phasor EMA (I2, Q2) | ~8 | 2 SUB/ADD + 4 FMA |
| Homodyne mixing + EMA | ~12 | 4 MUL + 2 ADD/SUB + 2 FMA |
| ATAN | ~15 | `Math.Atan` transcendental |
| Period division (2π/θ) | ~2 | 1 DIV |
| Clamp + EMA smoothing | ~4 | 2 comparisons + 1 FMA |
| Buffer management | ~10 | 4 circular buffer writes + index arithmetic |
| **Total** | **~84** | **O(1) fixed; identical pipeline to HOMOD** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | No: full Hilbert cascade is sequentially dependent IIR chain |
| Bottleneck | `Math.Atan` transcendental + 4 Hilbert FIR passes per bar |
| Parallelism | None: each bar's phasor depends on previous bar's EMA state |
| Memory | O(1): 4 circular buffers (7 elements each) + 6 scalar EMA states (~280 bytes) |
| Throughput | Moderate; ~3× slower than simple EMA; matches HOMOD performance |

## Resources

- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
- **TA-Lib** `TA_HT_DCPERIOD()` reference implementation.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
