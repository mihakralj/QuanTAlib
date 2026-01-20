# Cycles

> "The market is a discounting mechanism that anticipates cycles before they complete."  Unknown

Cycle analysis identifies repeating patterns in price data. John Ehlers pioneered digital signal processing techniques for financial cycles, using Hilbert transforms and autocorrelation to detect dominant periods. Cycles exist but are non-stationary: period and amplitude shift over time.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| CG | Center of Gravity | Ehlers. Weighted sum position. Minimal lag cycle indicator. |
| DSP | Detrended Synthetic Price | Removes trend to reveal underlying cycles. |
| EACP | Autocorrelation Periodogram | Ehlers. Spectral analysis via autocorrelation. Detects dominant period. |
| EBSW | Even Better Sinewave | Ehlers. Improved sinewave extraction. Reduces false signals. |
| HOMOD | Homodyne Discriminator | Dominant cycle detection via homodyne technique. |
| HT_DCPERIOD | HT Dominant Cycle Period | Ehlers Hilbert Transform. Measures current cycle length. |
| HT_DCPHASE | HT Dominant Cycle Phase | Ehlers Hilbert Transform. Measures current position in cycle. |
| HT_PHASOR | HT Phasor Components | Ehlers. In-phase and quadrature components. |
| HT_SINE | HT SineWave | Ehlers. Sine and lead sine for cycle timing. |
| LUNAR | Lunar Phase | 29.5-day lunar cycle. Studied for market correlations. |
| PHASOR | Phasor Analysis | Ehlers. Phase angle from Hilbert Transform. |
| SINE | Sine Wave | Ehlers. Basic sinewave indicator for cycle mode. |
| SOLAR | Solar Activity Cycle | ~11-year sunspot cycle. Long-term research indicator. |
| SSFDSP | SSF Detrended Synthetic Price | Super Smoother Filter based DSP. Cleaner cycle extraction. |
| STC | Schaff Trend Cycle | MACD + double Stochastic smoothing. Fast cycle oscillator (0-100). |
