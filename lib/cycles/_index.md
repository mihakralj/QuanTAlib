# Cycles

> "The market is a discounting mechanism that anticipates cycles before they complete."  Unknown

Cycle analysis identifies repeating patterns in price data. John Ehlers pioneered digital signal processing techniques for financial cycles, using Hilbert transforms and autocorrelation to detect dominant periods. Cycles exist but are non-stationary: period and amplitude shift over time.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [CG](/lib/cycles/cg/Cg.md) | Center of Gravity | Ehlers. Weighted sum position. Minimal lag cycle indicator. |
| [DSP](/lib/cycles/dsp/Dsp.md) | Detrended Synthetic Price | Removes trend to reveal underlying cycles. |
| [EACP](/lib/cycles/eacp/Eacp.md) | Autocorrelation Periodogram | Ehlers. Spectral analysis via autocorrelation. Detects dominant period. |
| [EBSW](/lib/cycles/ebsw/Ebsw.md) | Even Better Sinewave | Ehlers. Improved sinewave extraction. Reduces false signals. |
| [HOMOD](/lib/cycles/homod/Homod.md) | Homodyne Discriminator | Dominant cycle detection via homodyne technique. |
| [HT_DCPERIOD](/lib/cycles/ht_dcperiod/Ht_dcperiod.md) | HT Dominant Cycle Period | Ehlers Hilbert Transform. Measures current cycle length. |
| [HT_DCPHASE](/lib/cycles/ht_dcphase/Ht_dcphase.md) | HT Dominant Cycle Phase | Ehlers Hilbert Transform. Measures current position in cycle. |
| [HT_PHASOR](/lib/cycles/ht_phasor/Ht_phasor.md) | HT Phasor Components | Ehlers. In-phase and quadrature components. |
| [HT_SINE](/lib/cycles/ht_sine/Ht_sine.md) | HT SineWave | Ehlers. Sine and lead sine for cycle timing. |
| [LUNAR](/lib/cycles/lunar/Lunar.md) | Lunar Phase | 29.5-day lunar cycle. Studied for market correlations. |
| [PHASOR](/lib/cycles/phasor/Phasor.md) | Phasor Analysis | Ehlers. Phase angle from Hilbert Transform. |
| [SINE](/lib/cycles/sine/Sine.md) | Sine Wave | Ehlers. Basic sinewave indicator for cycle mode. |
| [SOLAR](/lib/cycles/solar/Solar.md) | Solar Activity Cycle | ~11-year sunspot cycle. Long-term research indicator. |
| [SSFDSP](/lib/cycles/ssfdsp/Ssfdsp.md) | SSF Detrended Synthetic Price | Super Smoother Filter based DSP. Cleaner cycle extraction. |
| [STC](/lib/cycles/stc/Stc.md) | Schaff Trend Cycle | MACD + double Stochastic smoothing. Fast cycle oscillator (0-100). |
