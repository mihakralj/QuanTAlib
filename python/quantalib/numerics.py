"""quantalib numerics indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "accel",
    "fdist",
    "fft",
    "gammadist",
    "highest",
    "maxindex",
    "ifft",
    "jerk",
    "lineartrans",
    "lognormdist",
    "logtrans",
    "lowest",
    "minindex",
    "normalize",
    "normdist",
    "poissondist",
    "relu",
    "sigmoid",
    "slope",
    "sqrttrans",
    "tdist",
    "weibulldist",
    "change",
    "exptrans",
    "betadist",
    "expdist",
    "binomdist",
    "cwt",
    "dwt",
]


def accel(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Acceleration."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_accel(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "ACCEL", "numerics", offset)


def fdist(close: ArrayLike, d1: int = 1, d2: int = 1, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """F-Distribution."""
    d1 = int(d1)
    d2 = int(d2)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_fdist(_ptr(src), _ptr(output), n, d1, d2, period))
    return _wrap(output, idx, f"FDIST_{period}", "numerics", offset)


def fft(close: ArrayLike, windowSize: int = 256, minPeriod: int = 6, maxPeriod: int = 48, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Fast Fourier Transform."""
    windowSize = int(windowSize)
    minPeriod = int(minPeriod)
    maxPeriod = int(maxPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_fft(_ptr(src), _ptr(output), n, windowSize, minPeriod, maxPeriod))
    return _wrap(output, idx, f"FFT_{minPeriod}", "numerics", offset)


def gammadist(close: ArrayLike, alpha: float = 2.0, beta: float = 1.0, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Gamma Distribution."""
    alpha = float(alpha)
    beta = float(beta)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_gammadist(_ptr(src), _ptr(output), n, alpha, beta, period))
    return _wrap(output, idx, f"GAMMADIST_{period}", "numerics", offset)


def highest(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Highest Value."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_highest(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HIGHEST_{period}", "numerics", offset)


def maxindex(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Index of Highest Value."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_maxindex(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MAXINDEX_{period}", "numerics", offset)


def ifft(close: ArrayLike, windowSize: int = 256, numHarmonics: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Inverse FFT."""
    windowSize = int(windowSize)
    numHarmonics = int(numHarmonics)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ifft(_ptr(src), _ptr(output), n, windowSize, numHarmonics))
    return _wrap(output, idx, "IFFT", "numerics", offset)


def jerk(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Jerk (3rd derivative)."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jerk(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "JERK", "numerics", offset)


def lineartrans(close: ArrayLike, slope: float = 1.0, intercept: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Linear Transform."""
    slope = float(slope)
    intercept = float(intercept)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lineartrans(_ptr(src), _ptr(output), n, slope, intercept))
    return _wrap(output, idx, "LINEARTRANS", "numerics", offset)


def lognormdist(close: ArrayLike, mu: float = 0.0, sigma: float = 1.0, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Log-Normal Distribution."""
    mu = float(mu)
    sigma = float(sigma)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lognormdist(_ptr(src), _ptr(output), n, mu, sigma, period))
    return _wrap(output, idx, f"LOGNORMDIST_{period}", "numerics", offset)


def logtrans(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Logarithmic Transform."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_logtrans(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "LOGTRANS", "numerics", offset)


def lowest(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Lowest Value."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lowest(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"LOWEST_{period}", "numerics", offset)


def minindex(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Index of Lowest Value."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_minindex(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MININDEX_{period}", "numerics", offset)


def normalize(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Normalization."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_normalize(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"NORMALIZE_{period}", "numerics", offset)


def normdist(close: ArrayLike, mu: float = 0.0, sigma: float = 1.0, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Normal Distribution."""
    mu = float(mu)
    sigma = float(sigma)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_normdist(_ptr(src), _ptr(output), n, mu, sigma, period))
    return _wrap(output, idx, f"NORMDIST_{period}", "numerics", offset)


def poissondist(close: ArrayLike, lam: float = 1.0, period: int = 14, threshold: int = 5, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Poisson Distribution."""
    lam = float(lam)
    period = int(kwargs.get("length", period))
    threshold = int(threshold)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_poissondist(_ptr(src), _ptr(output), n, lam, period, threshold))
    return _wrap(output, idx, f"POISSONDIST_{period}", "numerics", offset)


def relu(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """ReLU Activation."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_relu(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "RELU", "numerics", offset)


def sigmoid(close: ArrayLike, k: float = 1.0, x0: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Sigmoid Transform."""
    k = float(k)
    x0 = float(x0)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sigmoid(_ptr(src), _ptr(output), n, k, x0))
    return _wrap(output, idx, "SIGMOID", "numerics", offset)


def slope(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Slope (1st derivative)."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_slope(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "SLOPE", "numerics", offset)


def sqrttrans(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Square Root Transform."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sqrttrans(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "SQRTTRANS", "numerics", offset)


def tdist(close: ArrayLike, nu: int = 10, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Student's t-Distribution."""
    nu = int(nu)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_tdist(_ptr(src), _ptr(output), n, nu, period))
    return _wrap(output, idx, f"TDIST_{period}", "numerics", offset)


def weibulldist(close: ArrayLike, k: float = 1.5, lam: float = 1.0, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Weibull Distribution."""
    k = float(k)
    lam = float(lam)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_weibulldist(_ptr(src), _ptr(output), n, k, lam, period))
    return _wrap(output, idx, f"WEIBULLDIST_{period}", "numerics", offset)

def change(close: ArrayLike, period: int = 1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Price Change."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_change(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CHANGE_{period}", "numerics", offset)


def exptrans(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Exponential Transform."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_exptrans(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "EXPTRANS", "numerics", offset)


def betadist(close: ArrayLike, period: int = 50, alpha: float = 2.0,
             beta: float = 2.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Beta Distribution."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_betadist(_ptr(src), n, _ptr(dst), period, float(alpha), float(beta)))
    return _wrap(dst, idx, f"BETADIST_{period}", "numerics", offset)


def expdist(close: ArrayLike, period: int = 50, lam: float = 3.0,
            offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Exponential Distribution."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_expdist(_ptr(src), n, _ptr(dst), period, float(lam)))
    return _wrap(dst, idx, f"EXPDIST_{period}", "numerics", offset)


def binomdist(close: ArrayLike, period: int = 50, trials: int = 20,
              threshold: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Binomial Distribution."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_binomdist(_ptr(src), n, _ptr(dst), period, int(trials), int(threshold)))
    return _wrap(dst, idx, f"BINOMDIST_{period}", "numerics", offset)


def cwt(close: ArrayLike, scale: float = 10.0, omega: float = 6.0,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Continuous Wavelet Transform."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cwt(_ptr(src), n, _ptr(dst), float(scale), float(omega)))
    return _wrap(dst, idx, "CWT", "numerics", offset)


def dwt(close: ArrayLike, period: int = 4, levels: int = 0,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Discrete Wavelet Transform."""
    period = int(kwargs.get("length", period)); levels = int(levels); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dwt(_ptr(src), n, _ptr(dst), period, levels))
    return _wrap(dst, idx, f"DWT_{period}", "numerics", offset)
