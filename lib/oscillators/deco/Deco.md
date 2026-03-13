# DECO: Ehlers Decycler Oscillator

> *Ehlers' decycler oscillator removes the trend and isolates residual oscillation — what remains when the drift is subtracted.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `shortPeriod` (default 30), `longPeriod` (default 60)                      |
| **Outputs**      | Single series (Deco)                       |
| **Output range** | $0$ to $1$                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [deco.pine](deco.pine)                       |

- The Decycler Oscillator (DECO) is a DSP-based oscillator developed by John F.
- **Similar:** [DPO](../dpo/Dpo.md), [Reflex](../reflex/Reflex.md) | **Complementary:** Cycle analysis | **Trading note:** Ehlers' Decycler Oscillator; removes trend to isolate cycle component for timing.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Overview

The Decycler Oscillator (DECO) is a DSP-based oscillator developed by John F. Ehlers that isolates intermediate-frequency market cycles. It computes the difference between two 2-pole Butterworth high-pass filters with different cutoff periods, revealing the spectral band between the two cutoff frequencies.

## Origin

- **Author:** John F. Ehlers
- **Source:** "Decyclers", Technical Analysis of Stocks & Commodities, September 2015
- **Category:** Oscillator / Digital Signal Processing

## Formula

The DECO uses two 2-pole Butterworth high-pass filters:

```
α = (cos(0.707 × 2π/period) + sin(0.707 × 2π/period) - 1) / cos(0.707 × 2π/period)
HP[n] = (1 - α/2)² × (x[n] - 2×x[n-1] + x[n-2]) + 2×(1-α) × HP[n-1] - (1-α)² × HP[n-2]

DECO = HP_long - HP_short
```

The 0.707 factor (1/√2) places the filter response at the -3 dB Butterworth design point.

### Transfer Function

Each HP filter has the z-domain transfer function:

```
H(z) = (1-α/2)² × (1 - 2z⁻¹ + z⁻²) / (1 - 2(1-α)z⁻¹ + (1-α)²z⁻²)
```

The DECO output is the difference H_long(z) - H_short(z), which forms a bandpass response isolating cycles between the short and long cutoff periods.

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| shortPeriod | int | 30 | > 0 | Short HP cutoff period (bars) |
| longPeriod | int | 60 | > shortPeriod | Long HP cutoff period (bars) |

## Interpretation

The Decycler Oscillator provides several analytical perspectives:

- **Zero-Line Crossovers:**
  - Crossing above zero indicates bullish cycle momentum
  - Crossing below zero indicates bearish cycle momentum
  - The zero-crossing timing is relatively lag-free

- **Band Isolation:**
  - The oscillator extracts only cycles within the frequency band defined by the two cutoff periods
  - Shorter cycles and longer trends are both rejected
  - This makes the oscillator highly selective

- **Divergence Analysis:**
  - Bullish divergence: price makes lower lows while DECO makes higher lows
  - Bearish divergence: price makes higher highs while DECO makes lower highs
  - Indicates potential trend reversal

- **Multiple Instance Analysis:**
  - Ehlers recommends using multiple DECO instances with different period pairs
  - Crossovers between instances with different coefficients can identify trend reversals

## Warmup Period

The indicator requires `longPeriod` bars before producing reliable output. The first two bars always output zero (insufficient price history for the 2-pole HP filter).

## Properties

- **Range:** Unbounded (oscillates around zero)
- **Complexity:** O(1) per bar (pure IIR filter, no lookback buffer needed)
- **Memory:** O(1) — only stores filter state variables

## Related Indicators

- **Decycler (DECYCLER):** The low-pass complement — removes cycles, keeps trend
- **SSF-DSP:** Similar concept using Super Smooth Filters instead of HP filters
- **Roofing Filter:** HP + SSF combination for cycle isolation
- **BandPass Filter:** Ehlers' direct bandpass approach

## Performance Profile

### Operation Count (Streaming Mode)

DECO (Detrended Correlation Oscillator) subtracts a linear regression from price then computes correlation.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Linear regression (O(N) or O(1) with prefix sums) | ~4 | 1 | 4 |
| SUB (detrend: price − regression) | 1 | 1 | 1 |
| Correlation pipeline (see CTI) | ~22 | 7 | 156 |
| **Total** | **~27** | — | **~161 cycles** |

Dominated by the correlation computation. ~161 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Linear regression | Yes | Prefix-sum dot products; VFMADD |
| Detrend subtraction | Yes | VSUBPD |
| Correlation | Yes | See CTI batch analysis |

Fully vectorizable in batch. Regression and correlation both benefit from AVX2 VFMADD chains.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact linear detrend + Pearson r |
| **Timeliness** | 5/10 | Two N-bar windows compound lag |
| **Smoothness** | 8/10 | Detrending removes linear drift; correlation bounded |
| **Noise Rejection** | 8/10 | Linear detrend + correlation is doubly robust to trend contamination |

## References

1. Ehlers, J. F. (2015). "Decyclers." *Technical Analysis of Stocks & Commodities*, September 2015.
2. Ehlers, J. F. (2013). *Cycle Analytics for Traders*. Wiley. Chapter 4.