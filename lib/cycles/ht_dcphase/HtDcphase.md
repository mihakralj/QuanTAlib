# HT_DCPHASE: Ehlers Hilbert Transform Dominant Cycle Phase

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (HT_DCPHASE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `LOOKBACK` bars                          |
| **PineScript**   | [ht_dcphase.pine](ht_dcphase.pine)                       |

- HT_DCPHASE measures the instantaneous phase angle of the dominant market cycle using Ehlers' Hilbert Transform cascade.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `LOOKBACK` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HT_DCPHASE measures the instantaneous phase angle of the dominant market cycle using Ehlers' Hilbert Transform cascade. The output ranges from $-45°$ to $315°$, with phase discontinuities at cycle completions marking the transition from one cycle to the next. Compatible with TA-Lib's `HT_DCPHASE` function, the indicator enables cycle-position timing for entries and exits based on where price currently sits within the dominant cycle.

## Historical Context

John Ehlers developed the Hilbert Transform cycle indicators in *Rocket Science for Traders* (2001) as extensions of David Hilbert's 1905 mathematical transform to financial data. While HT_DCPERIOD measures *how long* a cycle takes, HT_DCPHASE measures *where within the cycle* the market currently sits. This distinction matters for timing: a 20-bar cycle at phase 0° (bottom) has different implications than the same cycle at phase 180° (top). The TA-Lib implementation uses a DFT-like accumulation over the smoothed period to compute the DC phase from smoothed price history, requiring 63 bars of lookback for stable output. QuanTAlib matches TA-Lib within floating-point tolerance.

## Architecture & Physics

### 1. Hilbert Transform Cascade

Identical pipeline to HT_DCPERIOD: 4-bar WMA smoothing, Hilbert FIR detrender with coefficients $A = 0.0962$, $B = 0.5769$, phasor component extraction ($I_2$, $Q_2$), and homodyne period estimation.

### 2. Smoothed Period

The dominant cycle period from the homodyne discriminator, clamped to $[6, 50]$ and EMA-smoothed ($\alpha = 0.33$).

### 3. DC Phase via DFT Accumulation

Over the smoothed period $P$, accumulate weighted contributions from the price history:

$$RealPart = \sum_{i=0}^{P-1} \sin\!\left(\frac{2\pi i}{P}\right) \cdot SmoothPrice_{t-i}$$

$$ImagPart = \sum_{i=0}^{P-1} \cos\!\left(\frac{2\pi i}{P}\right) \cdot SmoothPrice_{t-i}$$

$$DCPhase_{raw} = \arctan\!\left(\frac{RealPart}{ImagPart}\right) \cdot \frac{180°}{\pi}$$

### 4. Phase Adjustment

If $ImagPart > 0$: $DCPhase \mathrel{-}= 180°$

Final unwrapping: $DCPhase \mathrel{+}= 90°$, then if $DCPhase < -45°$: $DCPhase \mathrel{+}= 360°$.

Result is wrapped to $[-45°, 315°]$.

### 5. Complexity

$O(P)$ per bar where $P$ is the smoothed period (typically 6-50), due to the DFT accumulation loop over the price history. Memory is approximately 1.2 KB per instance for circular buffers and state. Warmup: 63 bars (TA-Lib lookback).

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

All internal constants are fixed by the TA-Lib specification.

### Pseudo-code

```
function HT_DCPHASE(source):
    // Same Hilbert cascade as HT_DCPERIOD
    // ... (WMA smooth, Hilbert FIR, phasor, homodyne)
    // Produces: smoothPeriod, smoothPriceBuf

    for each bar (after warmup):
        P ← round(smoothPeriod)

        // DFT accumulation over dominant period
        realPart ← 0; imagPart ← 0
        for i = 0 to P-1:
            realPart += sin(2π·i / P) · smoothPriceBuf[t - i]
            imagPart += cos(2π·i / P) · smoothPriceBuf[t - i]

        // Phase extraction
        if |imagPart| > 0:
            dcPhase ← atan(realPart / imagPart) · (180/π)
        else:
            dcPhase ← 90 · sign(realPart)

        if imagPart > 0: dcPhase -= 180
        dcPhase += 90

        // Wrap to [-45, 315]
        if dcPhase < -45: dcPhase += 360

        emit dcPhase
```

### Phase Quadrant Interpretation

| Phase Range | Cycle Position |
|-------------|----------------|
| $-45°$ to $45°$ | Bottom zone (start of uptrend) |
| $45°$ to $135°$ | Rising phase (mid-uptrend) |
| $135°$ to $225°$ | Top zone (start of downtrend) |
| $225°$ to $315°$ | Falling phase (mid-downtrend) |
| $315°$ to $-45°$ jump | Cycle completion (discontinuity) |

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| Phase advancing steadily | Regular cyclical market |
| Phase stuck or slow | Trending market (cycle suppressed) |
| Rapid phase change | Potential reversal imminent |
| Discontinuity ($315° \to -45°$) | One cycle complete, new cycle begins |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| Hilbert cascade (WMA + 4×FIR + phasor + homodyne) | ~84 | Same as HT_DCPERIOD pipeline |
| DFT sin/cos evaluation | 2P | `Math.Sin` + `Math.Cos` per iteration (~15-20 cycles each) |
| DFT multiply-accumulate | 2P | realPart/imagPart FMA per iteration |
| ATAN phase extraction | ~15 | `Math.Atan` transcendental |
| Phase adjustment + wrapping | ~5 | 2 ADD + 2 comparisons + 1 conditional ADD |
| **Total (P=20 typical)** | **~184** | **O(P) dominated by DFT sin/cos loop** |
| **Total (P=50 worst case)** | **~384** | **Upper bound when period near maximum** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Partially: DFT inner loop sin/cos accumulation is vectorizable with precomputed twiddle factors |
| Bottleneck | DFT loop: P transcendental calls per bar; Hilbert cascade is sequential |
| Parallelism | DFT accumulation independent per frequency bin; `Vector<double>` applicable to sin/cos MACs |
| Memory | O(P): ~50-element smooth price circular buffer + Hilbert state (~1.2 KB) |
| Throughput | ~2-4× slower than O(1) Hilbert-only indicators (HOMOD, HT_DCPERIOD) due to variable-length DFT |

## Resources

- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
- **TA-Lib** `TA_HT_DCPHASE()` reference implementation.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Hilbert, D.** *Grundzüge einer allgemeinen Theorie der linearen Integralgleichungen*. Teubner, 1912.
