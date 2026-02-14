# Missing Indicators Report

> Generated: 2026-02-13 (refreshed) | Source: Cross-reference of `_index.md` files vs actual filesystem

## Summary

| Status | Count | Description |
|--------|------:|-------------|
| **Fully Implemented** | 258 | `.cs` + tests + docs |
| **Pine-Only** (has spec, no C#) | 25 | Directory exists with `.pine` file only |
| **No Directory** (listed in master index, no files) | 38 | Planned but nothing on disk |
| **Doc-Only** | 1 | Only `.md` file exists |
| **Index Discrepancies** | 4 | Count or category mismatches |

---

## Implemented Categories (fully in C#)

| Category | Subdirs | Implemented | Notes |
|----------|--------:|------------:|-------|
| Trends (FIR) | 17 | **17** | All complete |
| Trends (IIR) | 23 | **23** | All complete (LTMA planned, no subdir) |
| Filters | 18 | **18** | All complete |
| Oscillators | 19 | **19** | All complete (16 more planned, no subdir) |
| Dynamics | 17 | **17** | All complete (IMPULSE planned, no subdir) |
| Momentum | 16 | **16** | All complete |
| Volatility | 26 | **26** | All complete |
| Volume | 26 | **26** | All complete |
| Channels | 23 | **23** | All complete |
| Cycles | 14 | **14** | All complete (PHASOR planned, broken link) |
| Errors | 26 | **26** | All complete (no Quantower wrappers) |
| Numerics | 15 | **15** | All complete (incl. ACCEL, JERK, SLOPE) |
| Forecasts | 1 | **1** | AFIRMA only (MLP planned) |
| Statistics | 30 | **14** | 16 pine-only — see below |
| Reversals | 10 | **0** | 9 pine-only + 1 doc-only — see below |
| Feeds | 3 | **3** | CSV, GBM, IFeed |

---

## 1. PINE-ONLY Indicators (spec exists, no C# implementation)

These have directories with `.pine` reference files but **zero `.cs` files**. They need full implementation.

### Statistics (16 indicators — pine-only)

| Indicator | Directory |
|-----------|-----------|
| ENTROPY | `lib/statistics/entropy/` |
| GEOMEAN | `lib/statistics/geomean/` |
| GRANGER | `lib/statistics/granger/` |
| HARMEAN | `lib/statistics/harmean/` |
| HURST | `lib/statistics/hurst/` |
| IQR | `lib/statistics/iqr/` |
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

## 2. NO DIRECTORY at All (listed in master index, no files on disk)

These indicators appear in `lib/_index.md` as plain text (no markdown link), meaning they are planned but have **no directory, no files**.

### Oscillators (16 planned)

| Indicator | Full Name |
|-----------|-----------|
| BBI | Bulls Bears Index |
| BRAR | BRAR |
| COPPOCK | Coppock Curve |
| CRSI | Connors RSI |
| CTI | Correlation Trend Indicator |
| DOSC | Derivative Oscillator |
| ER | Efficiency Ratio |
| ERI | Elder Ray Index |
| FOSC | Forecast Oscillator |
| KRI | Kairi Relative Index |
| KST | KST Oscillator |
| PSL | Psychological Line |
| QQE | Quantitative Qualitative Estimation |
| RVGI | Relative Vigor Index |
| SQUEEZE | Squeeze |
| TD_SEQ | TD Sequential |

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

### Statistics (3 planned)

| Indicator | Full Name |
|-----------|-----------|
| POLYFIT | Polynomial Fitting |
| TSF | Time Series Forecast |
| WAVG | Weighted Average |

### Reversals (2 planned)

| Indicator | Full Name |
|-----------|-----------|
| CHANDELIER | Chandelier Exit |
| CKSTOP | Chande Kroll Stop |

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

## 4. Index Discrepancies

### Category Mismatches

| Issue | Details |
|-------|---------|
| **APO** | Listed under Momentum in `_index.md` (`momentum/apo/Apo.md`) but actually lives at `lib/oscillators/apo/`. Broken link. |
| **PHASOR** | Listed under Cycles with link `cycles/phasor/Phasor.md` but `lib/cycles/phasor/` does not exist. Broken link. |

### Count Mismatches in Master `_index.md` Header

| Category | Claimed | Actual Subdirs | Notes |
|----------|--------:|-----------:|-------|
| Oscillators | 18 | 19 | APO counted here, not in Momentum |
| Dynamics | 16 | 17 | Actual has 17 implemented subdirs |
| Momentum | 17 | 16 | APO missing (lives in oscillators) |
| Channels | 22 | 23 | Actual has 23 implemented subdirs |
| Cycles | 15 | 14 | PHASOR counted but subdir doesn't exist |
| Statistics | 31 | 30 | 30 subdirs (14 implemented + 16 pine-only); 3 more planned with no subdir |

---

## 5. Priority Implementation Order

### Tier 1 — High Value (pine-only, well-specified)

1. **Reversals** (9 pine-only) — Entire category unimplemented in C#
   - PSAR, FRACTALS, PIVOT, PIVOTCAM, PIVOTDEM, PIVOTEXT, PIVOTFIB, PIVOTWOOD, SWINGS

2. **Statistics** (16 pine-only) — Large gap in core functionality
   - ENTROPY, GEOMEAN, HARMEAN, KURTOSIS, PERCENTILE, QUANTILE, ZSCORE
   - HURST, KENDALL, SPEARMAN, IQR, JB, ZTEST, THEIL, MODE, GRANGER

### Tier 2 — Medium Value (planned oscillators/dynamics)

1. **Oscillators** (16 planned, no files) — Many well-known indicators
   - QQE, RVGI, COPPOCK, KST, CRSI, TD_SEQ, ERI, SQUEEZE
   - BBI, BRAR, CTI, DOSC, ER, FOSC, KRI, PSL

2. **Dynamics** — IMPULSE (Elder Impulse System)
3. **Reversals** — CHANDELIER, CKSTOP (no pine spec)
4. **TTM_SCALPER** — doc exists, needs C# implementation

### Tier 3 — Lower Priority (math/distributions/misc)

1. **Numerics distributions** (14 planned) — Statistical distributions + transforms
2. **Statistics** — POLYFIT, TSF, WAVG
3. **Trends IIR** — LTMA
4. **Forecasts** — MLP
5. **Cycles** — PHASOR

---

## 6. Totals

| Type | Count |
|------|------:|
| Pine-only (spec ready, no C#) | **25** |
| No directory (planned only) | **38** |
| Doc-only | **1** |
| **Total missing indicators** | **64** |
| **Total implemented (C#)** | **258** |
| **Total listed in master index** | **278** |
