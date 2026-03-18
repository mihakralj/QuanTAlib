# Cycles

Cycle analysis identifies repeating patterns in price data. John Ehlers pioneered digital signal processing techniques for financial cycles, using Hilbert transforms and autocorrelation to detect dominant periods. Cycles exist but are non-stationary: period and amplitude shift over time.

## Indicators

| Indicator                                | Full Name                                              | Description                                                                  |
| :--------------------------------------- | :----------------------------------------------------- | :--------------------------------------------------------------------------- |
| [AMFM](amfm/Amfm.md)                    | Ehlers AM Detector / FM Demodulator                    | Ehlers. DSP decomposition into amplitude (volatility) and frequency (timing).|
| [CCOR](ccor/Ccor.md)                     | Ehlers Correlation Cycle                               | Ehlers. Dual Pearson correlation (cos + -sin). Phasor angle + market state.  |
| [CCYC](ccyc/Ccyc.md)                     | Ehlers Cyber Cycle                                     | Ehlers. 4-tap FIR + 2-pole high-pass IIR. Isolates dominant cycle component. |
| [CG](cg/Cg.md)                           | Ehlers Center of Gravity                               | Ehlers. Weighted sum position. Minimal lag cycle indicator.                  |
| [DSP](dsp/Dsp.md)                        | Ehlers Detrended Synthetic Price                       | Removes trend to reveal underlying cycles.                                   |
| [ACP](acp/Acp.md)                        | Ehlers Autocorrelation Periodogram                     | Ehlers. Spectral analysis via autocorrelation. Detects dominant period.      |
| [EBSW](ebsw/Ebsw.md)                     | Ehlers Even Better Sinewave                            | Ehlers. Improved sinewave extraction. Reduces false signals.                 |
| [HOMOD](homod/Homod.md)                  | Ehlers Homodyne Discriminator                          | Dominant cycle detection via homodyne technique.                             |
| [HT_DCPERIOD](ht_dcperiod/Htdcperiod.md) | Ehlers Hilbert Transform Dominant Cycle Period         | Ehlers Hilbert Transform. Measures current cycle length.                     |
| [HT_DCPHASE](ht_dcphase/Htdcphase.md)    | Ehlers Hilbert Transform Dominant Cycle Phase          | Ehlers Hilbert Transform. Measures current position in cycle.                |
| [HT_PHASOR](ht_phasor/HtPhasor.md)       | Ehlers Hilbert Transform Phasor Components             | Ehlers. In-phase and quadrature components.                                  |
| [HT_SINE](ht_sine/HtSine.md)             | Ehlers Hilbert Transform SineWave (also known as SINE) | Ehlers Hilbert Transform. Sine and lead sine for cycle timing.               |
| [LPF](lpf/Lpf.md)                        | Ehlers Linear Predictive Filter                        | Ehlers. Griffiths LMS predictor coefficients → DFT spectrum → dominant cycle.|
| [LUNAR](lunar/Lunar.md)                  | Lunar Phase                                            | 29.5-day lunar cycle. Studied for market correlations.                       |
| [SOLAR](solar/Solar.md)                  | Solar Activity Cycle                                   | ~11-year sunspot cycle. Long-term research indicator.                        |
| [SSFDSP](ssfdsp/Ssfdsp.md)               | Ehlers SSF Detrended Synthetic Price                   | Super Smoother Filter based DSP. Cleaner cycle extraction.                   |
