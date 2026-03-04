"""quantalib numerics indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "accel",
    "fdist",
    "fft",
    "gammadist",
    "highest",
    "ifft",
    "jerk",
    "lineartrans",
    "lognormdist",
    "logtrans",
    "lowest",
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


def accel(close: object, offset: int = 0, **kwargs) -> object:
    """Acceleration."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_accel(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "ACCEL", "numerics", offset)


def fdist(close: object, d1: int = 1, d2: int = 1, period: int = 14, offset: int = 0, **kwargs) -> object:
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


def fft(close: object, windowSize: int = 256, minPeriod: int = 6, maxPeriod: int = 48, offset: int = 0, **kwargs) -> object:
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


def gammadist(close: object, alpha: float = 2.0, beta: float = 1.0, period: int = 14, offset: int = 0, **kwargs) -> object:
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


def highest(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Highest Value."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_highest(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HIGHEST_{period}", "numerics", offset)


def ifft(close: object, windowSize: int = 256, numHarmonics: int = 10, offset: int = 0, **kwargs) -> object:
    """Inverse FFT."""
    windowSize = int(windowSize)
    numHarmonics = int(numHarmonics)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ifft(_ptr(src), _ptr(output), n, windowSize, numHarmonics))
    return _wrap(output, idx, "IFFT", "numerics", offset)


def jerk(close: object, offset: int = 0, **kwargs) -> object:
    """Jerk (3rd derivative)."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jerk(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "JERK", "numerics", offset)


def lineartrans(close: object, slope: float = 1.0, intercept: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Linear Transform."""
    slope = float(slope)
    intercept = float(intercept)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lineartrans(_ptr(src), _ptr(output), n, slope, intercept))
    return _wrap(output, idx, "LINEARTRANS", "numerics", offset)


def lognormdist(close: object, mu: float = 0.0, sigma: float = 1.0, period: int = 14, offset: int = 0, **kwargs) -> object:
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


def logtrans(close: object, offset: int = 0, **kwargs) -> object:
    """Logarithmic Transform."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_logtrans(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "LOGTRANS", "numerics", offset)


def lowest(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Lowest Value."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lowest(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"LOWEST_{period}", "numerics", offset)


def normalize(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Normalization."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_normalize(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"NORMALIZE_{period}", "numerics", offset)


def normdist(close: object, mu: float = 0.0, sigma: float = 1.0, period: int = 14, offset: int = 0, **kwargs) -> object:
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


def poissondist(close: object, lam: float = 1.0, period: int = 14, threshold: int = 5, offset: int = 0, **kwargs) -> object:
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


def relu(close: object, offset: int = 0, **kwargs) -> object:
    """ReLU Activation."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_relu(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "RELU", "numerics", offset)


def sigmoid(close: object, k: float = 1.0, x0: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Sigmoid Transform."""
    k = float(k)
    x0 = float(x0)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sigmoid(_ptr(src), _ptr(output), n, k, x0))
    return _wrap(output, idx, "SIGMOID", "numerics", offset)


def slope(close: object, offset: int = 0, **kwargs) -> object:
    """Slope (1st derivative)."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_slope(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "SLOPE", "numerics", offset)


def sqrttrans(close: object, offset: int = 0, **kwargs) -> object:
    """Square Root Transform."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sqrttrans(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "SQRTTRANS", "numerics", offset)


def tdist(close: object, nu: int = 10, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Student's t-Distribution."""
    nu = int(nu)
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_tdist(_ptr(src), _ptr(output), n, nu, period))
    return _wrap(output, idx, f"TDIST_{period}", "numerics", offset)


def weibulldist(close: object, k: float = 1.5, lam: float = 1.0, period: int = 14, offset: int = 0, **kwargs) -> object:
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

def change(close: object, period: int = 1, offset: int = 0, **kwargs) -> object:
    """Price Change."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_change(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CHANGE_{period}", "numerics", offset)


def exptrans(close: object, offset: int = 0, **kwargs) -> object:
    """Exponential Transform."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_exptrans(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "EXPTRANS", "numerics", offset)


def betadist(close: object, period: int = 50, alpha: float = 2.0,
             beta: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Beta Distribution."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_betadist(_ptr(src), n, _ptr(dst), period, float(alpha), float(beta)))
    return _wrap(dst, idx, f"BETADIST_{period}", "numerics", offset)


def expdist(close: object, period: int = 50, lam: float = 3.0,
            offset: int = 0, **kwargs) -> object:
    """Exponential Distribution."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_expdist(_ptr(src), n, _ptr(dst), period, float(lam)))
    return _wrap(dst, idx, f"EXPDIST_{period}", "numerics", offset)


def binomdist(close: object, period: int = 50, trials: int = 20,
              threshold: int = 10, offset: int = 0, **kwargs) -> object:
    """Binomial Distribution."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_binomdist(_ptr(src), n, _ptr(dst), period, int(trials), int(threshold)))
    return _wrap(dst, idx, f"BINOMDIST_{period}", "numerics", offset)


def cwt(close: object, scale: float = 10.0, omega: float = 6.0,
        offset: int = 0, **kwargs) -> object:
    """Continuous Wavelet Transform."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cwt(_ptr(src), n, _ptr(dst), float(scale), float(omega)))
    return _wrap(dst, idx, "CWT", "numerics", offset)


def dwt(close: object, period: int = 4, levels: int = 0,
        offset: int = 0, **kwargs) -> object:
    """Discrete Wavelet Transform."""
    period = int(kwargs.get("length", period)); levels = int(levels); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dwt(_ptr(src), n, _ptr(dst), period, levels))
    return _wrap(dst, idx, f"DWT_{period}", "numerics", offset)
