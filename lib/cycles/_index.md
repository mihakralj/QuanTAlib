# Cycles

> "The market is a discounting mechanism that anticipates cycles before they complete."  Unknown

Cycle analysis identifies repeating patterns in price data. John Ehlers pioneered digital signal processing techniques for financial cycles, using Hilbert transforms and autocorrelation to detect dominant periods. Cycles exist but are non-stationary: period and amplitude shift over time.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| CFB | Composite Fractal Behavior | =Ë | Jurik's fractal-based cycle detection. Adapts to changing volatility. |
| CG | Center of Gravity | =Ë | Ehlers. Weighted sum position. Minimal lag cycle indicator. |
| DSP | Detrended Synthetic Price | =Ë | Removes trend to reveal underlying cycles. |
| EACP | Autocorrelation Periodogram | =Ë | Ehlers. Spectral analysis via autocorrelation. Detects dominant period. |
| EBSW | Even Better Sinewave | =Ë | Ehlers. Improved sinewave extraction. Reduces false signals. |
| SSFDSP | SSF Detrended Synthetic Price | =Ë | Super Smoother Filter based DSP. Cleaner cycle extraction. |
| HOMOD | Homodyne Discriminator | =Ë | Dominant cycle detection via homodyne technique. |
| HT_DCPERIOD | HT Dominant Cycle Period | =Ë | Ehlers Hilbert Transform. Measures current cycle length. |
| HT_DCPHASE | HT Dominant Cycle Phase | =Ë | Ehlers Hilbert Transform. Measures current position in cycle. |
| HT_PHASOR | HT Phasor Components | =Ë | Ehlers. In-phase and quadrature components. |
| HT_SINE | HT SineWave | =Ë | Ehlers. Sine and lead sine for cycle timing. |
| LUNAR | Lunar Phase | =Ë | 29.5-day lunar cycle. Studied for market correlations. |
| PHASOR | Phasor Analysis | =Ë | Ehlers. Phase angle from Hilbert Transform. |
| SINE | Sine Wave | =Ë | Ehlers. Basic sinewave indicator for cycle mode. |
| SOLAR | Solar Activity Cycle | =Ë | ~11-year sunspot cycle. Long-term research indicator. |
| [STC](lib/cycles/stc/Stc.md) | Schaff Trend Cycle |  | MACD + double Stochastic smoothing. Fast cycle oscillator (0-100). |

**Status Key:**  Implemented | =Ë Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| Dominant cycle detection | EACP, HT_DCPERIOD | Spectral analysis identifies strongest periodic component. |
| Cycle timing | HT_SINE, EBSW | Sine/lead-sine crossovers signal cycle turns. |
| Trend + cycle hybrid | STC | Combines MACD trend with Stochastic cycle. Fast signals. |
| Minimal lag | CG | Center of Gravity has theoretical zero lag at cycle frequency. |
| Phase analysis | HT_PHASOR, PHASOR | Track position within current cycle. |

## Ehlers Cycle Framework

John Ehlers developed most modern cycle indicators using DSP principles:

| Component | Purpose | Implementation |
| :--- | :--- | :--- |
| Hilbert Transform | Extracts instantaneous phase | 90° phase shift via FIR filter |
| Super Smoother | Pre-filter noise | 2-pole Butterworth variant |
| Homodyne | Period detection | Multiplies signal by delayed version |
| Autocorrelation | Spectral density | Correlates signal with lagged self |

Key insight: Financial cycles are non-stationary. Fixed-period indicators fail. Adaptive techniques (EACP, HT_DCPERIOD) measure the current dominant period and adjust accordingly.

## Cycle vs Trend

| Market Condition | Use Cycles | Use Trends |
| :--- | :--- | :--- |
| Ranging/choppy |  Cycles excel | L Whipsaws |
| Strong trend | L False signals |  Trend-following works |
| Transition periods |   Regime detection |   Lag at turns |

Combine cycle indicators with trend filters. Trade cycles only when trend strength is low.