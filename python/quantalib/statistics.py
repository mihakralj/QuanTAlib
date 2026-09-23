"""quantalib statistics indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "adf",
    "acf",
    "geomean",
    "granger",
    "harmean",
    "hurst",
    "iqr",
    "jb",
    "kendall",
    "kurtosis",
    "linreg",
    "meandev",
    "median",
    "mode",
    "pacf",
    "percentile",
    "polyfit",
    "quantile",
    "skew",
    "spearman",
    "stderr",
    "sum",
    "theil",
    "trim",
    "wavg",
    "wins",
    "ztest",
    "zscore",
    "cma",
    "entropy",
    "correl",
    "covariance",
    "cointegration",
    "convexity",
]


def adf(close: ArrayLike, period: int = 50, max_lag: int = 0, regression: int = 1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Augmented Dickey-Fuller test p-value."""
    period = int(kwargs.get("length", period))
    max_lag = int(max_lag)
    regression = int(regression)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_adf(_ptr(src), _ptr(output), n, period, max_lag, regression))
    return _wrap(output, idx, f"ADF_{period}", "statistics", offset)


def acf(close: ArrayLike, period: int = 14, lag: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Autocorrelation Function."""
    period = int(kwargs.get("length", period))
    lag = int(lag)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_acf(_ptr(src), _ptr(output), n, period, lag))
    return _wrap(output, idx, f"ACF_{period}", "statistics", offset)


def geomean(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Geometric Mean."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_geomean(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"GEOMEAN_{period}", "statistics", offset)


def granger(x: ArrayLike, y: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Granger Causality."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_granger(_ptr(yarr), _ptr(xarr), _ptr(output), n, period))
    return _wrap(output, idx, f"GRANGER_{period}", "statistics", offset)


def harmean(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Harmonic Mean."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_harmean(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HARMEAN_{period}", "statistics", offset)


def hurst(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hurst Exponent."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hurst(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HURST_{period}", "statistics", offset)


def iqr(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Interquartile Range."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_iqr(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"IQR_{period}", "statistics", offset)


def jb(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Jarque-Bera Test."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jb(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"JB_{period}", "statistics", offset)


def kendall(x: ArrayLike, y: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Kendall Rank Correlation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_kendall(_ptr(xarr), _ptr(yarr), _ptr(output), n, period))
    return _wrap(output, idx, f"KENDALL_{period}", "statistics", offset)


def kurtosis(close: ArrayLike, period: int = 14, isPopulation: int = 0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Kurtosis."""
    period = int(kwargs.get("length", period))
    isPopulation = int(isPopulation)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_kurtosis(_ptr(src), _ptr(output), n, period, isPopulation))
    return _wrap(output, idx, f"KURTOSIS_{period}", "statistics", offset)


def linreg(close: ArrayLike, period: int = 14, initialLastValid: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Linear Regression."""
    period = int(kwargs.get("length", period))
    initialLastValid = float(initialLastValid)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_linreg(_ptr(src), _ptr(output), n, period, initialLastValid))
    return _wrap(output, idx, f"LINREG_{period}", "statistics", offset)


def meandev(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Mean Deviation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_meandev(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MEANDEV_{period}", "statistics", offset)


def median(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rolling Median."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_median(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MEDIAN_{period}", "statistics", offset)


def mode(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rolling Mode."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mode(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MODE_{period}", "statistics", offset)


def pacf(close: ArrayLike, period: int = 14, lag: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Partial Autocorrelation Function."""
    period = int(kwargs.get("length", period))
    lag = int(lag)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pacf(_ptr(src), _ptr(output), n, period, lag))
    return _wrap(output, idx, f"PACF_{period}", "statistics", offset)


def percentile(close: ArrayLike, period: int = 14, percent: float = 50.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rolling Percentile."""
    period = int(kwargs.get("length", period))
    percent = float(percent)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_percentile(_ptr(src), _ptr(output), n, period, percent))
    return _wrap(output, idx, f"PERCENTILE_{period}", "statistics", offset)


def polyfit(close: ArrayLike, period: int = 14, degree: int = 2, initialLastValid: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Polynomial Fit."""
    period = int(kwargs.get("length", period))
    degree = int(degree)
    initialLastValid = float(initialLastValid)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_polyfit(_ptr(src), _ptr(output), n, period, degree, initialLastValid))
    return _wrap(output, idx, f"POLYFIT_{period}", "statistics", offset)


def quantile(close: ArrayLike, period: int = 14, quantileLevel: float = 0.5, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rolling Quantile."""
    period = int(kwargs.get("length", period))
    quantileLevel = float(quantileLevel)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_quantile(_ptr(src), _ptr(output), n, period, quantileLevel))
    return _wrap(output, idx, f"QUANTILE_{period}", "statistics", offset)


def skew(close: ArrayLike, period: int = 14, isPopulation: int = 0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Skewness."""
    period = int(kwargs.get("length", period))
    isPopulation = int(isPopulation)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_skew(_ptr(src), _ptr(output), n, period, isPopulation))
    return _wrap(output, idx, f"SKEW_{period}", "statistics", offset)


def spearman(x: ArrayLike, y: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Spearman Rank Correlation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_spearman(_ptr(xarr), _ptr(yarr), _ptr(output), n, period))
    return _wrap(output, idx, f"SPEARMAN_{period}", "statistics", offset)


def stderr(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Standard Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_stderr(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"STDERR_{period}", "statistics", offset)


def sum(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rolling Sum."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sum(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"SUM_{period}", "statistics", offset)


def theil(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Theil U Statistic."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_theil(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"THEIL_{period}", "statistics", offset)


def trim(close: ArrayLike, period: int = 14, trimPct: float = 0.1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Trimmed Mean."""
    period = int(kwargs.get("length", period))
    trimPct = float(trimPct)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_trim(_ptr(src), _ptr(output), n, period, trimPct))
    return _wrap(output, idx, f"TRIM_{period}", "statistics", offset)


def wavg(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Weighted Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_wavg(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"WAVG_{period}", "statistics", offset)


def wins(close: ArrayLike, period: int = 14, winPct: float = 0.05, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Winsorized Mean."""
    period = int(kwargs.get("length", period))
    winPct = float(winPct)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_wins(_ptr(src), _ptr(output), n, period, winPct))
    return _wrap(output, idx, f"WINS_{period}", "statistics", offset)


def ztest(close: ArrayLike, period: int = 14, mu0: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Z-Test."""
    period = int(kwargs.get("length", period))
    mu0 = float(mu0)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ztest(_ptr(src), _ptr(output), n, period, mu0))
    return _wrap(output, idx, f"ZTEST_{period}", "statistics", offset)

def zscore(close: ArrayLike, period: int = 20, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Z-Score."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_zscore(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"ZSCORE_{period}", "statistics", offset)


def cma(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Cumulative Moving Average."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cma(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "CMA", "statistics", offset)


def entropy(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Shannon Entropy."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_entropy(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"ENTROPY_{period}", "statistics", offset)


def correl(x: ArrayLike, y: ArrayLike, period: int = 20,
           offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Pearson Correlation."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_correl(_ptr(xarr), _ptr(yarr), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CORR_{period}", "statistics", offset)


def covariance(x: ArrayLike, y: ArrayLike, period: int = 20,
               is_sample: bool = True, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Covariance."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_covariance(_ptr(xarr), _ptr(yarr), n, _ptr(dst), period, int(is_sample)))
    return _wrap(dst, idx, f"COV_{period}", "statistics", offset)


def cointegration(x: ArrayLike, y: ArrayLike, period: int = 20,
                  offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Cointegration."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_cointegration(_ptr(xarr), _ptr(yarr), n, _ptr(dst), period))
    return _wrap(dst, idx, f"COINT_{period}", "statistics", offset)


def convexity(x: ArrayLike, y: ArrayLike, period: int = 20,
              offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Beta Convexity (up/down beta asymmetry).

    Returns dict with keys: beta_std, beta_up, beta_down, ratio, convexity.
    """
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    d_std = _out(n); d_up = _out(n); d_down = _out(n)
    d_ratio = _out(n); d_cvx = _out(n)
    _check(_lib.qtl_convexity(
        _ptr(xarr), _ptr(yarr), n,
        _ptr(d_std), _ptr(d_up), _ptr(d_down),
        _ptr(d_ratio), _ptr(d_cvx), period))
    return {
        "beta_std": _wrap(d_std, idx, f"BETA_STD_{period}", "statistics", offset),
        "beta_up": _wrap(d_up, idx, f"BETA_UP_{period}", "statistics", offset),
        "beta_down": _wrap(d_down, idx, f"BETA_DOWN_{period}", "statistics", offset),
        "ratio": _wrap(d_ratio, idx, f"RATIO_{period}", "statistics", offset),
        "convexity": _wrap(d_cvx, idx, f"CONVEXITY_{period}", "statistics", offset),
    }
