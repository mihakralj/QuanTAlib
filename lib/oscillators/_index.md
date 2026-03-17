# Oscillators

Oscillators fluctuate above and below a centerline or within bounded ranges. Useful for identifying overbought/oversold conditions, momentum shifts, and divergences. Best in ranging markets; trend-following indicators work better in trending markets.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [AC](ac/Ac.md) | Acceleration Oscillator | Second derivative of AO. Measures acceleration of market driving force. |
| [AO](ao/Ao.md) | Awesome Oscillator | 5-period SMA minus 34-period SMA of bar midpoint. Bill Williams creation. |
| [APO](apo/Apo.md) | Absolute Price Oscillator | Raw currency difference between fast and slow EMAs. Unbounded. |
| [BBB](bbb/Bbb.md) | Bollinger %B | Position within Bollinger Bands. 0=lower band, 1=upper band. |
| [BBI](bbi/Bbi.md) | Bulls Bears Index | Multi-period SMA composite. Measures aggregate trend strength. |
| [BBS](bbs/Bbs.md) | Bollinger Band Squeeze | BB width < KC width indicates consolidation. Breakout imminent. |
| [BRAR](brar/Brar.md) | BRAR | Bull-Bear power ratio from open-relative ranges. Japanese origin. |
| [BW_MFI](bw_mfi/BwMfi.md) | Bill Williams MFI | MFI with 4-zone classification: Green (trend), Fade (fading), Fake (unsupported), Squat (breakout imminent). |
| [CFO](cfo/Cfo.md) | Chande Forecast Oscillator | Percentage difference between price and linear regression forecast. Also known as FOSC. |
| [COPPOCK](coppock/Coppock.md) | Coppock Curve | Long-term momentum via weighted sum of ROC periods. Buy signals only. |
| [CRSI](crsi/Crsi.md) | Connors RSI | Composite of RSI, streak RSI, and percentile rank. Mean-reversion. |
| [CTI](cti/Cti.md) | Ehlers Correlation Trend Indicator | Linear regression correlation coefficient as trend strength. |
| [DECO](deco/Deco.md) | Ehlers Decycler Oscillator | Dual HP bandpass isolating intermediate-frequency market cycles. |
| [DEM](dem/Dem.md) | DeMarker Oscillator | Bounded 0-1 oscillator comparing sequential highs and lows. |
| [DOSC](dosc/Dosc.md) | Derivative Oscillator | Double-smoothed RSI minus signal line. Momentum acceleration. |
| [DSO](dso/Dso.md) | Ehlers Deviation-Scaled Oscillator | SSF-filtered zeros with RMS normalization and Fisher Transform. TASC Oct 2018. |
| [DPO](dpo/Dpo.md) | Detrended Price Oscillator | Removes trend via displaced SMA. Reveals cycles. |
| [DSTOCH](dstoch/Dstoch.md) | Double Stochastic (Bressert) | Stochastic applied to Stochastic with EMA smoothing. Bounded 0-100. |
| [DYMI](dymi/Dymi.md) | Dynamic Momentum Index | RSI with volatility-adaptive period. Shorter in volatile markets. |
| [ER](er/Er.md) | Efficiency Ratio | Measures directional efficiency. Net movement / total path length. |
| [ERI](eri/Eri.md) | Elder Ray Index | Separates bull and bear power relative to EMA. |
| [FI](fi/Fi.md) | Force Index | Combines price change, direction, and volume to measure buying/selling power. |
| [FISHER](fisher/Fisher.md) | Ehlers Fisher Transform | Converts prices to Gaussian distribution. Sharp reversals. |
| [FISHER04](fisher04/Fisher04.md) | Ehlers Fisher Transform (2004) | Cybernetic Analysis variant with gentler arctanh scaling. |
| [GATOR](gator/Gator.md) | Williams Gator Oscillator | Dual histogram from Alligator SMMA lines. Visualizes trend convergence/divergence. |
| [IMI](imi/Imi.md) | Intraday Momentum Index | RSI variant using open-close range. Intraday overbought/oversold 0-100. |
| [INERTIA](inertia/Inertia.md) | Inertia | Linear regression residual. Raw deviation from trend forecast. |
| [KDJ](kdj/Kdj.md) | KDJ Indicator | Enhanced Stochastic. J = 3K - 2D provides leading signal. |
| [KRI](kri/Kri.md) | Kairi Relative Index | Percentage deviation of price from SMA. Overbought/oversold. |
| [KST](kst/Kst.md) | KST Oscillator | Summed weighted ROCs across 4 timeframes. Martin Pring. |
| [LRSI](lrsi/Lrsi.md) | Ehlers Laguerre RSI | RSI computed over Laguerre filter stages. Single γ trades lag vs smoothness. Output [0,1]. |
| [MARKETFI](marketfi/Marketfi.md) | Market Facilitation Index | Bill Williams' price-range-per-unit-of-volume efficiency measure. O(1), no period. |
| [MSTOCH](mstoch/Mstoch.md) | Ehlers MESA Stochastic | Hilbert Transform cycle-tuned Stochastic. Adaptive period. |
| [PGO](pgo/Pgo.md) | Pretty Good Oscillator | Distance from SMA normalized by ATR. Units: ATR multiples. |
| [PSL](psl/Psl.md) | Psychological Line | Ratio of up periods to total periods. Crowd sentiment gauge. |
| [QQE](qqe/Qqe.md) | Quantitative Qualitative Estimation | Smoothed RSI with dynamic volatility bands. |
| [REFLEX](reflex/Reflex.md) | Ehlers Reflex | Ehlers zero-centered reversal oscillator using super smoother with normalized sum-of-differences. |
| [REVERSEEMA](reverseema/ReverseEma.md) | Ehlers Reverse EMA | 8-stage cascaded Z-transform inversion subtracts EMA lag, producing zero-centered oscillator signal. |
| [RVGI](rvgi/Rvgi.md) | Ehlers Relative Vigor Index | Open-close vs high-low ratio with SMA smoothing. Measures conviction. |
| [RRSI](rrsi/Rrsi.md) | Ehlers Rocket RSI | Fisher Transform of Super Smoother–filtered RSI. Sharp cyclic reversal signals. |
| [RSIH](rsih/Rsih.md) | Ehlers Hann-Windowed RSI | Hann-weighted CU/CD RSI, zero-mean [-1, +1]. FIR filter. TASC Jan 2022. |
| [SMI](smi/Smi.md) | Stochastic Momentum Index | Distance from range midpoint. More sensitive than classic Stochastic. |
| [SQUEEZE](squeeze/Squeeze.md) | Squeeze | BB width < KC width indicates consolidation. Breakout imminent. |
| [SQUEEZE_PRO](squeeze_pro/squeeze_pro.md) | Squeeze Pro | Multi-level BB vs KC squeeze (wide/normal/narrow) with MOM-smoothed momentum. LazyBear. |
| [STC](stc/Stc.md) | Schaff Trend Cycle | MACD + double Stochastic smoothing. Fast momentum oscillator (0-100). |
| [STOCH](stoch/Stoch.md) | Stochastic Oscillator | Close position within N-period high-low range. Classic overbought/oversold. |
| [STOCHF](stochf/Stochf.md) | Stochastic Fast | Unsmoothed Stochastic. Faster but noisier. |
| [STOCHRSI](stochrsi/Stochrsi.md) | Stochastic RSI | Stochastic applied to RSI. More sensitive than either alone. |
| [TD_SEQ](td_seq/Td_seq.md) | TD Sequential | DeMark sequential countdown. Exhaustion pattern recognition. |
| [TRENDFLEX](trendflex/Trendflex.md) | Ehlers Trendflex | Ehlers zero-lag trend oscillator using super smoother with sum-of-differences normalization. |
| [TRIX](trix/Trix.md) | Triple Exponential Average | ROC of triple EMA. Filters noise through three smoothings. |
| [TTM_WAVE](ttm_wave/TtmWave.md) | TTM Wave | Fibonacci-period MACD composite (Waves A/B/C). John Carter. |
| [ULTOSC](ultosc/Ultosc.md) | Ultimate Oscillator | Multi-timeframe oscillator. Combines 7, 14, 28 period buying pressure. |
| [WILLR](willr/Willr.md) | Williams %R | Inverse Stochastic. -100 to 0 range. Overbought/oversold. |
