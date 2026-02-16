# Missing Indicators Report

> Generated: 2026-02-13 | Refreshed: 2026-02-16 | Source: Cross-reference of `_index.md` files vs actual filesystem + planned additions

## Summary

| Status | Count | Description |
|--------|------:|-------------|
| **Fully Implemented** | 266 | `.cs` + tests + docs |
| **Pine-Only** (has spec, no C#) | 20 | Directory exists with `.pine` file only |
| **No Directory** (listed in master index or planned, no files) | 100 | Planned but nothing on disk |
| **Doc-Only** | 1 | Only `.md` file exists |
| **Index Discrepancies** | 0 | All 7 mismatches in `lib/_index.md` fixed on 2026-02-16 |

---

## Implemented Categories (fully in C#)

| Category | Subdirs | Implemented | Notes |
|----------|--------:|------------:|-------|
| Trends (FIR) | 17 | **17** | All complete; 23 more planned |
| Trends (IIR) | 23 | **23** | All complete; 17 more planned |
| Trends (Adaptive) | — | **0** | New category; 5 planned |
| Filters | 18 | **18** | All complete; 12 more planned |
| Oscillators | 19 | **19** | All complete; 20 more planned |
| Dynamics | 18 | **18** | All complete |
| Momentum | 16 | **16** | All complete |
| Volatility | 26 | **26** | All complete |
| Volume | 26 | **26** | All complete; 1 more planned |
| Channels | 23 | **23** | All complete |
| Cycles | 14 | **14** | All complete; 2 more planned |
| Errors | 26 | **26** | All complete (no Quantower wrappers) |
| Numerics | 15 | **15** | All complete; 14 distributions planned |
| Forecasts | 1 | **1** | AFIRMA only (MLP planned) |
| Statistics | 30 | **19** | 11 pine-only — see below |
| Reversals | 12 | **2** | 9 pine-only + 1 doc-only + 1 planned |
| Feeds | 3 | **3** | CSV, GBM, IFeed |

---

## Recently Implemented (since last refresh)

| Indicator | Category | Date | Notes |
|-----------|----------|------|-------|
| **ENTROPY** | Statistics | 2026-02-13 | Shannon entropy with Kahan-Babuška summation |
| **GEOMEAN** | Statistics | 2026-02-13 | Geometric mean via log-sum with compensated summation |
| **GRANGER** | Statistics | 2026-02-13 | Granger causality test (F-statistic) |
| **HARMEAN** | Statistics | 2026-02-14 | Harmonic mean via reciprocal sum with Kahan-Babuška |
| **HURST** | Statistics | 2026-02-16 | Hurst Exponent via R/S analysis with OLS log-log regression |
| **IQR** | Statistics | 2026-02-16 | Interquartile Range via sorted-window with BinarySearch insert |
| **CHANDELIER** | Reversals | 2026-02-13 | Chandelier Exit (ATR-based trailing stop) |
| **CKSTOP** | Reversals | 2026-02-13 | Chande Kroll Stop |
| **IMPULSE** | Dynamics | 2026-02-13 | Elder Impulse System (EMA + MACD histogram) |

---

## 1. PINE-ONLY Indicators (spec exists, no C# implementation)

These have directories with `.pine` reference files but **zero `.cs` files**. They need full implementation.

### Statistics (10 indicators — pine-only)

| Indicator | Directory |
|-----------|-----------|
| JB | `lib/statistics/jb/` |
| KENDALL | `lib/statistics/kendall/` |
| KURTOSIS | `lib/statistics/kurtosis/` |
| MODE | `lib/statistics/mode/` |
| PERCENTILE | `lib/statistics/percentile/` |
| QUANTILE | `lib/statistics/quantile/` |
| SPEARMAN | `lib/statistics/spearman/` |
| THEIL | `lib/statistics/theil/` |
| ZSCORE | `lib/statistics/zscore/` |
| ZTEST | `lib/statistics/ztest/` |

### Reversals (9 indicators — pine-only)

| Indicator | Directory |
|-----------|-----------|
| FRACTALS | `lib/reversals/fractals/` |
| PIVOT | `lib/reversals/pivot/` |
| PIVOTCAM | `lib/reversals/pivotcam/` |
| PIVOTDEM | `lib/reversals/pivotdem/` |
| PIVOTEXT | `lib/reversals/pivotext/` |
| PIVOTFIB | `lib/reversals/pivotfib/` |
| PIVOTWOOD | `lib/reversals/pivotwood/` |
| PSAR | `lib/reversals/psar/` |
| SWINGS | `lib/reversals/swings/` |

---

## 2. NO DIRECTORY at All (planned, no files on disk)

These indicators are planned but have **no directory, no files**. Organized by category.

### Filters (12 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| LAGUERRE | Laguerre Filter | Ehlers; 4-element IIR with damping factor |
| ALAGUERRE | Adaptive Laguerre Filter | Ehlers; variable-gamma Laguerre |
| ROOFING | Roofing Filter | Ehlers; HP + super smoother composite |
| VOSS | Voss Predictive Filter | Ehlers; predictive bandpass |
| AGC | Automatic Gain Control | Ehlers; amplitude normalization |
| SPBF | Super Passband Filter | Ehlers; wide-band bandpass |
| LMS | Least Mean Squares Adaptive Filter | Widrow-Hoff adaptive FIR |
| RLS | Recursive Least Squares Adaptive Filter | Faster convergence than LMS |
| WAVELET | Denoising Wavelet Filter | Wavelet-based noise removal (distinct from CWT/DWT transforms) |
| BAXTERKING | Baxter-King Filter | Symmetric band-pass for business cycle extraction |
| CHRISTIANOFITZGERALD | Christiano-Fitzgerald Filter | Asymmetric band-pass; handles endpoints |
| ONEEURO | One Euro Filter | Low-latency jitter removal; speed-adaptive |

### Trends — IIR (17 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| DECYCLER | Decycler | Ehlers; HP subtracted from price |
| TRENDFLEX | Trendflex Indicator | Ehlers; trend-following zero-lag |
| PMA | Predictive Moving Average | Ehlers; 2×EMA − EMA(EMA) extrapolation |
| REVERSEEMA | Reverse Exponential Moving Average | Ehlers; backward-looking EMA reconstruction |
| EVWMA | Elastic Volume Weighted Moving Average | Volume-elastic EMA variant |
| MAVP | Moving Average Variable Period | TA-Lib; period varies per bar |
| EHMA | Exponential Hull Moving Average | Hull concept with EMA instead of WMA |
| HOLT | Holt Exponential Smoothing | Double exponential smoothing (level + trend) |
| HWTS | Holt-Winters Triple Smoothing | Triple smoothing (level+trend+seasonality); ⚠️ check overlap with HWMA |
| CORAL | Coral Trend Filter | LazyBear; multi-pole IIR with color coding |
| GDEMA | Generalized Double EMA | Generalized DEMA with tunable volume factor |
| NLMA | Non-Lag Moving Average | Zero-lag via Kalman-like error correction |
| LEMA | Leader Exponential Moving Average | EMA + momentum lead term |
| MCNMA | McNicholl EMA | McNicholl's zero-lag EMA variant |
| NYQMA | Nyquist Moving Average | Nyquist-frequency-aware smoothing |
| AHRENS | Ahrens Moving Average | Richard Ahrens' recursive MA |
| RAINBOW | Rainbow Moving Average | Cascaded SMA stack (10 layers averaged) |

### Trends — Adaptive (5 planned — new category)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| ADXVMA | ADX Variable Moving Average | ADX-scaled smoothing factor |
| TRAMA | Trend Regularity Adaptive MA | Adapts to trend regularity |
| NMA | Natural Moving Average | Natural cycle-adaptive MA |
| VMA | Variable Moving Average | Tushar Chande; volatility-adaptive |
| EDCF | Distance Coefficient Filter | Distance-based adaptive coefficient |

### Trends — FIR (23 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| RWMA | Range Weighted Moving Average | Weights by bar range |
| MIDPOINT | Midpoint | (Highest + Lowest) / 2 over period; ⚠️ name collision with `numerics/midpoint` |
| MIDPRICE | Midprice | (Highest High + Lowest Low) / 2 over period |
| SWMA | Symmetric Weighted Moving Average | Symmetric triangular weights |
| KAISER | Kaiser Window Moving Average | Kaiser-Bessel window FIR |
| BLACKMANHARRIS | Blackman-Harris Window MA | 4-term Blackman-Harris window |
| LANCZOS | Lanczos Window Moving Average | Lanczos (sinc) window FIR |
| PARZEN | Parzen Window Moving Average | Parzen (de la Vallée-Poussin) window |
| NUTTALL | Nuttall Window Moving Average | 4-term Nuttall window; minimal sidelobe |
| BOHMAN | Bohman Window Moving Average | Bohman window FIR |
| DOLPH | Dolph-Chebyshev Window MA | Equiripple sidelobe window |
| TSF | Time Series Forecast | Linear regression extrapolation (moved from Statistics) |
| QRMA | Quadratic Regression MA | 2nd-order polynomial regression |
| CRMA | Cubic Regression MA | 3rd-order polynomial regression |
| NW | Nadaraya-Watson Kernel Regression | Gaussian kernel weighted regression |
| HENDERSON | Henderson Moving Average | Henderson symmetric filter (ABS standard) |
| SP15 | Spencer 15-Point Moving Average | Spencer's classic 15-weight filter |
| SP21 | Spencer 21-Point Moving Average | Spencer's extended 21-weight filter |
| TRIMMED | Trimmed Mean Moving Average | Outlier-trimmed arithmetic mean |
| WINSOR | Winsorized Mean Moving Average | Outlier-capped (Winsorized) mean |
| GEOMMA | Geometric Mean Moving Average | Geometric mean as FIR weight basis |
| HARMMA | Harmonic Mean Moving Average | Harmonic mean as FIR weight basis |

> **Note:** HAMMING and BLACKMAN window MAs already exist as **HAMMA** and **BLMA** respectively.

### Oscillators (20 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| BBI | Bulls Bears Index | — |
| BRAR | BRAR | — |
| COPPOCK | Coppock Curve | — |
| CRSI | Connors RSI | — |
| CTI | Correlation Trend Indicator | — |
| DECO | Decycler Oscillator | Ehlers; HP component of Decycler |
| DOSC | Derivative Oscillator | — |
| ER | Efficiency Ratio | — |
| ERI | Elder Ray Index | — |
| FOSC | Forecast Oscillator | — |
| KRI | Kairi Relative Index | — |
| KST | KST Oscillator | — |
| PSL | Psychological Line | — |
| QQE | Quantitative Qualitative Estimation | — |
| REFLEX | Reflex Indicator | Ehlers; momentum with cycle correction |
| RVGI | Relative Vigor Index | — |
| SQUEEZE | Squeeze | — |
| TD_SEQ | TD Sequential | — |
| CYBERCYCLE | Cyber Cycle | Ehlers; 2-pole IIR cycle isolator |
| MSTOCH | MESA Stochastic | Ehlers; Hilbert-based stochastic |

### Cycles (2 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| CCOR | Correlation Cycle | Ehlers; cycle detection via autocorrelation |
| GOERTZEL | Goertzel Frequency Detector | Single-bin DFT for dominant cycle |

### Volume (1 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| FVE | Finite Volume Elements | Markos Katsanos; volume-price flow |

### Reversals (1 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| SAREXT | Parabolic SAR Extended | TA-Lib extended SAR with configurable acceleration |

### Statistics (2 planned)

| Indicator | Full Name | Notes |
|-----------|-----------|-------|
| POLYFIT | Polynomial Fitting | — |
| WAVG | Weighted Average | — |

### Numerics (14 planned — distributions and transforms)

| Indicator | Full Name |
|-----------|-----------|
| BETADIST | Beta Distribution |
| BINOMDIST | Binomial Distribution |
| CWT | Continuous Wavelet Transform |
| DWT | Discrete Wavelet Transform |
| EXPDIST | Exponential Distribution |
| FDIST | F-Distribution |
| FFT | Fast Fourier Transform |
| GAMMADIST | Gamma Distribution |
| IFFT | Inverse Fast Fourier Transform |
| LOGNORMDIST | Log-normal Distribution |
| NORMDIST | Normal Distribution |
| POISSONDIST | Poisson Distribution |
| TDIST | Student's t-Distribution |
| WEIBULLDIST | Weibull Distribution |

### Forecasts (1 planned)

| Indicator | Full Name |
|-----------|-----------|
| MLP | Multilayer Perceptron |

---

## 3. DOC-ONLY (has `.md` but no `.cs` or `.pine`)

| Indicator | Directory | Files Present |
|-----------|-----------|---------------|
| TTM_SCALPER | `lib/reversals/ttm_scalper/` | `TtmScalper.md` only |

---

## 4. Index Discrepancies (FIXED 2026-02-16)

All category mismatches and count errors in `lib/_index.md` have been corrected.

### Fixes Applied

| Issue | Fix | Status |
|-------|-----|--------|
| **APO** category wrong (Momentum → Oscillators) | Path changed to `oscillators/apo/Apo.md`, category to Oscillators | ✅ Fixed |
| **PHASOR** broken link (subdir doesn't exist) | Removed markdown link, kept as plain text planned entry | ✅ Fixed |
| **CHANDELIER** missing link (now implemented) | Added `[CHANDELIER](reversals/chandelier/Chandelier.md)` | ✅ Fixed |
| **CKSTOP** missing link (now implemented) | Added `[CKSTOP](reversals/ckstop/Ckstop.md)` | ✅ Fixed |
| **IMPULSE** missing link (now implemented) | Added `[IMPULSE](dynamics/impulse/Impulse.md)` | ✅ Fixed |
| **TSF** wrong category (Statistics → Trends FIR) | Category changed to `Trends (FIR)` | ✅ Fixed |
| **All category counts** | Updated to match actual subdirectory counts | ✅ Fixed |
| **Total** | Updated from 278 → **284** | ✅ Fixed |

### Name Collisions

| Name | Existing | New (Planned) | Resolution |
|------|----------|---------------|------------|
| MIDPOINT | `lib/numerics/midpoint/` (math function) | Trends (FIR) — (highest+lowest)/2 | Different indicators; use distinct directory paths |
| TUKEY | `lib/errors/tukey/` (Tukey fence metric) | Trends (FIR) — Tukey window MA | Different indicators; use distinct directory paths |
| HAMMING | — | Trends (FIR) — Hamming window MA | **Already exists as HAMMA** (`lib/trends_FIR/hamma/`). Do not add. |
| BLACKMAN | — | Trends (FIR) — Blackman window MA | **Already exists as BLMA** (`lib/trends_FIR/blma/`). Do not add. |
| HWTS | — | Trends (IIR) — Holt-Winters Triple | **May overlap with HWMA** (`lib/trends_FIR/hwma/`). Review before implementing. |
| WAVELET | CWT/DWT in Numerics (transforms) | Filters — Denoising wavelet filter | Different purpose (denoising vs transform). Both valid. |
| TSF | Was under Statistics planned | Now Trends (FIR) | Moved to correct category |

---

## 5. Priority Implementation Order

### Tier 1 — High Value (pine-only, well-specified)

1. **Statistics** (10 pine-only) — Core statistical functionality gap
   - KURTOSIS, PERCENTILE, QUANTILE, ZSCORE
   - KENDALL, SPEARMAN, JB, ZTEST, THEIL, MODE

2. **Reversals** (9 pine-only) — Large category gap
   - PSAR, FRACTALS, PIVOT, PIVOTCAM, PIVOTDEM, PIVOTEXT, PIVOTFIB, PIVOTWOOD, SWINGS

### Tier 2 — Ehlers DSP Suite (high-value, well-documented algorithms)

1. **Filters** — LAGUERRE, ALAGUERRE, ROOFING, VOSS, AGC, SPBF (all Ehlers)
2. **Oscillators** — DECO, REFLEX, CYBERCYCLE, MSTOCH (all Ehlers)
3. **Cycles** — CCOR, GOERTZEL (Ehlers cycle detection)
4. **Trends (IIR)** — DECYCLER, TRENDFLEX, PMA, REVERSEEMA (Ehlers trend)

### Tier 3 — Window Functions & Regression MAs

1. **Trends (FIR)** — Window functions: KAISER, BLACKMANHARRIS, LANCZOS, PARZEN, NUTTALL, BOHMAN, DOLPH
2. **Trends (FIR)** — Regression: TSF, QRMA, CRMA, NW, HENDERSON, SP15, SP21
3. **Trends (FIR)** — Robust means: TRIMMED, WINSOR, GEOMMA, HARMMA

### Tier 4 — IIR & Adaptive MAs

1. **Trends (IIR)** — EHMA, HOLT, CORAL, GDEMA, NLMA, LEMA, MCNMA, NYQMA, AHRENS, RAINBOW, EVWMA, MAVP, HWTS
2. **Trends (Adaptive)** — ADXVMA, TRAMA, NMA, VMA, EDCF (new category)

### Tier 5 — Adaptive Filters & Miscellaneous

1. **Filters** — LMS, RLS, BAXTERKING, CHRISTIANOFITZGERALD, ONEEURO, WAVELET
2. **Oscillators** — Legacy: QQE, RVGI, COPPOCK, KST, CRSI, TD_SEQ, ERI, SQUEEZE, BBI, BRAR, CTI, DOSC, ER, FOSC, KRI, PSL
3. **Volume** — FVE
4. **Reversals** — SAREXT

### Tier 6 — Lower Priority

1. **Statistics** — POLYFIT, WAVG
2. **Numerics** — 14 distributions + transforms
3. **Trends (IIR)** — LTMA
4. **Forecasts** — MLP
5. **Cycles** — PHASOR
6. **Reversals** — TTM_SCALPER (doc exists, needs C#)

---

## 6. Planned Count by Category

| Category | Existing | Pine-Only | Planned (no dir) | Total When Done |
|----------|--------:|----------:|-----------------:|----------------:|
| Trends (FIR) | 17 | 0 | 23 | 40 |
| Trends (IIR) | 23 | 0 | 17 | 40 |
| Trends (Adaptive) | 0 | 0 | 5 | 5 |
| Filters | 18 | 0 | 12 | 30 |
| Oscillators | 19 | 0 | 20 | 39 |
| Dynamics | 18 | 0 | 0 | 18 |
| Momentum | 16 | 0 | 0 | 16 |
| Volatility | 26 | 0 | 0 | 26 |
| Volume | 26 | 0 | 1 | 27 |
| Channels | 23 | 0 | 0 | 23 |
| Cycles | 14 | 0 | 2 | 16 |
| Errors | 26 | 0 | 0 | 26 |
| Numerics | 15 | 0 | 14 | 29 |
| Forecasts | 1 | 0 | 1 | 2 |
| Statistics | 19 | 11 | 2 | 32 |
| Reversals | 2 | 9 | 1 | 12 |
| Feeds | 3 | 0 | 0 | 3 |
| **Total** | **266** | **20** | **98** | **384** |

## 7. Grand Totals

| Type | Count |
|------|------:|
| Pine-only (spec ready, no C#) | **20** |
| No directory (planned only) | **100** |
| Doc-only | **1** |
| **Total missing indicators** | **121** |
| **Total implemented (C#)** | **266** |
| **Grand total (implemented + missing)** | **387** |
