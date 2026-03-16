"""quantalib statistics indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


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
    "correlation",
    "covariance",
    "cointegration",
]


def adf(close: object, period: int = 50, max_lag: int = 0, regression: int = 1, offset: int = 0, **kwargs) -> object:
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


def acf(close: object, period: int = 14, lag: int = 10, offset: int = 0, **kwargs) -> object:
    """Autocorrelation Function."""
    period = int(kwargs.get("length", period))
    lag = int(lag)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_acf(_ptr(src), _ptr(output), n, period, lag))
    return _wrap(output, idx, f"ACF_{period}", "statistics", offset)


def geomean(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Geometric Mean."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_geomean(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"GEOMEAN_{period}", "statistics", offset)


def granger(x: object, y: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Granger Causality."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_granger(_ptr(yarr), _ptr(xarr), _ptr(output), n, period))
    return _wrap(output, idx, f"GRANGER_{period}", "statistics", offset)


def harmean(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Harmonic Mean."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_harmean(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HARMEAN_{period}", "statistics", offset)


def hurst(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Hurst Exponent."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hurst(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HURST_{period}", "statistics", offset)


def iqr(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Interquartile Range."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_iqr(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"IQR_{period}", "statistics", offset)


def jb(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Jarque-Bera Test."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jb(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"JB_{period}", "statistics", offset)


def kendall(x: object, y: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Kendall Rank Correlation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_kendall(_ptr(xarr), _ptr(yarr), _ptr(output), n, period))
    return _wrap(output, idx, f"KENDALL_{period}", "statistics", offset)


def kurtosis(close: object, period: int = 14, isPopulation: int = 0, offset: int = 0, **kwargs) -> object:
    """Kurtosis."""
    period = int(kwargs.get("length", period))
    isPopulation = int(isPopulation)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_kurtosis(_ptr(src), _ptr(output), n, period, isPopulation))
    return _wrap(output, idx, f"KURTOSIS_{period}", "statistics", offset)


def linreg(close: object, period: int = 14, initialLastValid: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Linear Regression."""
    period = int(kwargs.get("length", period))
    initialLastValid = float(initialLastValid)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_linreg(_ptr(src), _ptr(output), n, period, initialLastValid))
    return _wrap(output, idx, f"LINREG_{period}", "statistics", offset)


def meandev(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Deviation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_meandev(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MEANDEV_{period}", "statistics", offset)


def median(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Rolling Median."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_median(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MEDIAN_{period}", "statistics", offset)


def mode(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Rolling Mode."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mode(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MODE_{period}", "statistics", offset)


def pacf(close: object, period: int = 14, lag: int = 10, offset: int = 0, **kwargs) -> object:
    """Partial Autocorrelation Function."""
    period = int(kwargs.get("length", period))
    lag = int(lag)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pacf(_ptr(src), _ptr(output), n, period, lag))
    return _wrap(output, idx, f"PACF_{period}", "statistics", offset)


def percentile(close: object, period: int = 14, percent: float = 50.0, offset: int = 0, **kwargs) -> object:
    """Rolling Percentile."""
    period = int(kwargs.get("length", period))
    percent = float(percent)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_percentile(_ptr(src), _ptr(output), n, period, percent))
    return _wrap(output, idx, f"PERCENTILE_{period}", "statistics", offset)


def polyfit(close: object, period: int = 14, degree: int = 2, initialLastValid: float = 0.0, offset: int = 0, **kwargs) -> object:
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


def quantile(close: object, period: int = 14, quantileLevel: float = 0.5, offset: int = 0, **kwargs) -> object:
    """Rolling Quantile."""
    period = int(kwargs.get("length", period))
    quantileLevel = float(quantileLevel)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_quantile(_ptr(src), _ptr(output), n, period, quantileLevel))
    return _wrap(output, idx, f"QUANTILE_{period}", "statistics", offset)


def skew(close: object, period: int = 14, isPopulation: int = 0, offset: int = 0, **kwargs) -> object:
    """Skewness."""
    period = int(kwargs.get("length", period))
    isPopulation = int(isPopulation)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_skew(_ptr(src), _ptr(output), n, period, isPopulation))
    return _wrap(output, idx, f"SKEW_{period}", "statistics", offset)


def spearman(x: object, y: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Spearman Rank Correlation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_spearman(_ptr(xarr), _ptr(yarr), _ptr(output), n, period))
    return _wrap(output, idx, f"SPEARMAN_{period}", "statistics", offset)


def stderr(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Standard Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_stderr(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"STDERR_{period}", "statistics", offset)


def sum(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Rolling Sum."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sum(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"SUM_{period}", "statistics", offset)


def theil(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Theil U Statistic."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_theil(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"THEIL_{period}", "statistics", offset)


def trim(close: object, period: int = 14, trimPct: float = 0.1, offset: int = 0, **kwargs) -> object:
    """Trimmed Mean."""
    period = int(kwargs.get("length", period))
    trimPct = float(trimPct)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_trim(_ptr(src), _ptr(output), n, period, trimPct))
    return _wrap(output, idx, f"TRIM_{period}", "statistics", offset)


def wavg(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Weighted Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_wavg(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"WAVG_{period}", "statistics", offset)


def wins(close: object, period: int = 14, winPct: float = 0.05, offset: int = 0, **kwargs) -> object:
    """Winsorized Mean."""
    period = int(kwargs.get("length", period))
    winPct = float(winPct)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_wins(_ptr(src), _ptr(output), n, period, winPct))
    return _wrap(output, idx, f"WINS_{period}", "statistics", offset)


def ztest(close: object, period: int = 14, mu0: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Z-Test."""
    period = int(kwargs.get("length", period))
    mu0 = float(mu0)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ztest(_ptr(src), _ptr(output), n, period, mu0))
    return _wrap(output, idx, f"ZTEST_{period}", "statistics", offset)

def zscore(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Z-Score."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_zscore(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"ZSCORE_{period}", "statistics", offset)


def cma(close: object, offset: int = 0, **kwargs) -> object:
    """Cumulative Moving Average."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cma(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "CMA", "statistics", offset)


def entropy(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Shannon Entropy."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_entropy(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"ENTROPY_{period}", "statistics", offset)


def correlation(x: object, y: object, period: int = 20,
                offset: int = 0, **kwargs) -> object:
    """Pearson Correlation."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_correlation(_ptr(xarr), _ptr(yarr), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CORR_{period}", "statistics", offset)


def covariance(x: object, y: object, period: int = 20,
               is_sample: bool = True, offset: int = 0, **kwargs) -> object:
    """Covariance."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_covariance(_ptr(xarr), _ptr(yarr), n, _ptr(dst), period, int(is_sample)))
    return _wrap(dst, idx, f"COV_{period}", "statistics", offset)


def cointegration(x: object, y: object, period: int = 20,
                  offset: int = 0, **kwargs) -> object:
    """Cointegration."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_cointegration(_ptr(xarr), _ptr(yarr), n, _ptr(dst), period))
    return _wrap(dst, idx, f"COINT_{period}", "statistics", offset)
