# Cycles

> "The market is a discounting mechanism that anticipates cycles before they complete."  Unknown

Cycle analysis identifies repeating patterns in price data. John Ehlers pioneered digital signal processing techniques for financial cycles, using Hilbert transforms and autocorrelation to detect dominant periods. Cycles exist but are non-stationary: period and amplitude shift over time.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [CG](cg/Cg.md) | Center of Gravity | Ehlers. Weighted sum position. Minimal lag cycle indicator. |
| [DSP](dsp/Dsp.md) | Detrended Synthetic Price | Removes trend to reveal underlying cycles. |
| [EACP](eacp/Eacp.md) | Autocorrelation Periodogram | Ehlers. Spectral analysis via autocorrelation. Detects dominant period. |
| [EBSW](ebsw/Ebsw.md) | Even Better Sinewave | Ehlers. Improved sinewave extraction. Reduces false signals. |
| [HOMOD](homod/Homod.md) | Homodyne Discriminator | Dominant cycle detection via homodyne technique. |
| [HT_DCPERIOD](ht_dcperiod/Ht_dcperiod.md) | HT Dominant Cycle Period | Ehlers Hilbert Transform. Measures current cycle length. |
| [HT_DCPHASE](ht_dcphase/Ht_dcphase.md) | HT Dominant Cycle Phase | Ehlers Hilbert Transform. Measures current position in cycle. |
| [HT_PHASOR](ht_phasor/Ht_phasor.md) | HT Phasor Components | Ehlers. In-phase and quadrature components. |
| [HT_SINE](ht_sine/HtSine.md) | Hilbert Transform SineWave | Ehlers Hilbert Transform. Sine and lead sine for cycle timing. |
| [LUNAR](lunar/Lunar.md) | Lunar Phase | 29.5-day lunar cycle. Studied for market correlations. |
| [PHASOR](phasor/Phasor.md) | Phasor Analysis | Ehlers. Phase angle from Hilbert Transform. |
| [SINE](sine/Sine.md) | Sine Wave | Ehlers. Basic sinewave indicator for cycle mode. |
| [SOLAR](solar/Solar.md) | Solar Activity Cycle | ~11-year sunspot cycle. Long-term research indicator. |
| [SSFDSP](ssfdsp/Ssfdsp.md) | SSF Detrended Synthetic Price | Super Smoother Filter based DSP. Cleaner cycle extraction. |
| [STC](stc/Stc.md) | Schaff Trend Cycle | MACD + double Stochastic smoothing. Fast cycle oscillator (0-100). |
