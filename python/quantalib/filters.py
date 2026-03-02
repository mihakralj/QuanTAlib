"""quantalib filters indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "gauss",
    "hann",
    "hp",
    "hpf",
    "kalman",
    "laguerre",
    "lms",
    "loess",
    "modf",
    "notch",
    "nw",
    "oneeuro",
    "rls",
    "rmed",
    "roofing",
    "sgf",
    "spbf",
    "ssf2",
    "ssf3",
    "usf",
    "voss",
    "wavelet",
    "wiener",
    "bessel",
    "butter2",
    "butter3",
    "cheby1",
    "cheby2",
    "elliptic",
    "edcf",
    "bpf",
    "alaguerre",
    "bilateral",
    "baxterking",
    "cfitz",
]


def gauss(close: object, sigma: float = 1.0, offset: int = 0, **kwargs) -> object:
    """Gaussian Filter."""
    sigma = float(sigma)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_gauss(_ptr(src), _ptr(output), n, sigma))
    return _wrap(output, idx, "GAUSS", "filters", offset)


def hann(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Hann Filter."""
    length = int(length)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hann(_ptr(src), _ptr(output), n, length))
    return _wrap(output, idx, f"HANN_{length}", "filters", offset)


def hp(close: object, lam: float = 1600.0, offset: int = 0, **kwargs) -> object:
    """Hodrick-Prescott Filter."""
    lam = float(lam)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hp(_ptr(src), _ptr(output), n, lam))
    return _wrap(output, idx, "HP", "filters", offset)


def hpf(close: object, length: int = 40, offset: int = 0, **kwargs) -> object:
    """High-Pass Filter."""
    length = int(length)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hpf(_ptr(src), _ptr(output), n, length))
    return _wrap(output, idx, f"HPF_{length}", "filters", offset)


def kalman(close: object, q: float = 0.01, r: float = 0.1, offset: int = 0, **kwargs) -> object:
    """Kalman Filter."""
    q = float(q)
    r = float(r)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_kalman(_ptr(src), _ptr(output), n, q, r))
    return _wrap(output, idx, "KALMAN", "filters", offset)


def laguerre(close: object, gamma: float = 0.8, offset: int = 0, **kwargs) -> object:
    """Laguerre Filter."""
    gamma = float(gamma)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_laguerre(_ptr(src), _ptr(output), n, gamma))
    return _wrap(output, idx, "LAGUERRE", "filters", offset)


def lms(close: object, order: int = 16, mu: float = 0.5, offset: int = 0, **kwargs) -> object:
    """Least Mean Squares Filter."""
    order = int(order)
    mu = float(mu)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lms(_ptr(src), _ptr(output), n, order, mu))
    return _wrap(output, idx, "LMS", "filters", offset)


def loess(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """LOESS Smoother."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_loess(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"LOESS_{period}", "filters", offset)


def modf(close: object, period: int = 14, beta: float = 0.8, feedback: int = 0, fbWeight: float = 0.5, offset: int = 0, **kwargs) -> object:
    """Modified Filter."""
    period = int(kwargs.get("length", period))
    beta = float(beta)
    feedback = int(feedback)
    fbWeight = float(fbWeight)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_modf(_ptr(src), _ptr(output), n, period, beta, feedback, fbWeight))
    return _wrap(output, idx, f"MODF_{period}", "filters", offset)


def notch(close: object, period: int = 14, q: float = 1.0, offset: int = 0, **kwargs) -> object:
    """Notch Filter."""
    period = int(kwargs.get("length", period))
    q = float(q)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_notch(_ptr(src), _ptr(output), n, period, q))
    return _wrap(output, idx, f"NOTCH_{period}", "filters", offset)


def nw(close: object, period: int = 64, bandwidth: float = 8.0, offset: int = 0, **kwargs) -> object:
    """Nadaraya-Watson Filter."""
    period = int(kwargs.get("length", period))
    bandwidth = float(bandwidth)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nw(_ptr(src), _ptr(output), n, period, bandwidth))
    return _wrap(output, idx, f"NW_{period}", "filters", offset)


def oneeuro(close: object, minCutoff: float = 1.0, beta: float = 0.007, dCutoff: float = 1.0, offset: int = 0, **kwargs) -> object:
    """1€ Filter."""
    minCutoff = float(minCutoff)
    beta = float(beta)
    dCutoff = float(dCutoff)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_oneeuro(_ptr(src), _ptr(output), n, minCutoff, beta, dCutoff))
    return _wrap(output, idx, "ONEEURO", "filters", offset)


def rls(close: object, order: int = 16, lam: float = 0.99, offset: int = 0, **kwargs) -> object:
    """Recursive Least Squares Filter."""
    order = int(order)
    lam = float(lam)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rls(_ptr(src), _ptr(output), n, order, lam))
    return _wrap(output, idx, "RLS", "filters", offset)


def rmed(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Running Median Filter."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rmed(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"RMED_{period}", "filters", offset)


def roofing(close: object, hpLength: int = 48, ssLength: int = 10, offset: int = 0, **kwargs) -> object:
    """Roofing Filter."""
    hpLength = int(hpLength)
    ssLength = int(ssLength)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_roofing(_ptr(src), _ptr(output), n, hpLength, ssLength))
    return _wrap(output, idx, f"ROOFING_{hpLength}", "filters", offset)


def sgf(close: object, period: int = 14, polyOrder: int = 2, offset: int = 0, **kwargs) -> object:
    """Savitzky-Golay Filter."""
    period = int(kwargs.get("length", period))
    polyOrder = int(polyOrder)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sgf(_ptr(src), _ptr(output), n, period, polyOrder))
    return _wrap(output, idx, f"SGF_{period}", "filters", offset)


def spbf(close: object, shortPeriod: int = 40, longPeriod: int = 60, rmsPeriod: int = 50, offset: int = 0, **kwargs) -> object:
    """Short-Period Bandpass Filter."""
    shortPeriod = int(shortPeriod)
    longPeriod = int(longPeriod)
    rmsPeriod = int(rmsPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_spbf(_ptr(src), _ptr(output), n, shortPeriod, longPeriod, rmsPeriod))
    return _wrap(output, idx, f"SPBF_{shortPeriod}", "filters", offset)


def ssf2(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Super Smoother (2-pole)."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ssf2(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"SSF2_{period}", "filters", offset)


def ssf3(close: object, period: int = 14, initialLast: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Super Smoother (3-pole)."""
    period = int(kwargs.get("length", period))
    initialLast = float(initialLast)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    destination = _out(n)
    _check(_lib.qtl_ssf3(_ptr(src), _ptr(destination), n, period, initialLast))
    return _wrap(destination, idx, f"SSF3_{period}", "filters", offset)


def usf(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Universal Smoother Filter."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_usf(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"USF_{period}", "filters", offset)


def voss(close: object, period: int = 14, predict: int = 3, bandwidth: float = 0.25, offset: int = 0, **kwargs) -> object:
    """Voss Predictor."""
    period = int(kwargs.get("length", period))
    predict = int(predict)
    bandwidth = float(bandwidth)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_voss(_ptr(src), _ptr(output), n, period, predict, bandwidth))
    return _wrap(output, idx, f"VOSS_{period}", "filters", offset)


def wavelet(close: object, levels: int = 4, threshMult: float = 1.0, offset: int = 0, **kwargs) -> object:
    """Wavelet Filter."""
    levels = int(levels)
    threshMult = float(threshMult)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_wavelet(_ptr(src), _ptr(output), n, levels, threshMult))
    return _wrap(output, idx, "WAVELET", "filters", offset)


def wiener(close: object, period: int = 14, smoothPeriod: int = 10, offset: int = 0, **kwargs) -> object:
    """Wiener Filter."""
    period = int(kwargs.get("length", period))
    smoothPeriod = int(smoothPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    destination = _out(n)
    _check(_lib.qtl_wiener(_ptr(src), _ptr(destination), n, period, smoothPeriod))
    return _wrap(destination, idx, f"WIENER_{period}", "filters", offset)

def bessel(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Bessel Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bessel(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"BESSEL_{length}", "filters", offset)


def butter2(close: object, length: int = 14, gain: float = 1.0,
            offset: int = 0, **kwargs) -> object:
    """2nd-order Butterworth."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_butter2(_ptr(src), n, _ptr(dst), length, float(gain)))
    return _wrap(dst, idx, f"BUTTER2_{length}", "filters", offset)


def butter3(close: object, length: int = 14, gain: float = 1.0,
            offset: int = 0, **kwargs) -> object:
    """3rd-order Butterworth."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_butter3(_ptr(src), n, _ptr(dst), length, float(gain)))
    return _wrap(dst, idx, f"BUTTER3_{length}", "filters", offset)


def cheby1(close: object, length: int = 14, ripple: float = 0.5,
           offset: int = 0, **kwargs) -> object:
    """Chebyshev Type I."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cheby1(_ptr(src), n, _ptr(dst), length, float(ripple)))
    return _wrap(dst, idx, f"CHEBY1_{length}", "filters", offset)


def cheby2(close: object, length: int = 14, ripple: float = 0.5,
           offset: int = 0, **kwargs) -> object:
    """Chebyshev Type II."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cheby2(_ptr(src), n, _ptr(dst), length, float(ripple)))
    return _wrap(dst, idx, f"CHEBY2_{length}", "filters", offset)


def elliptic(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Elliptic (Cauer) Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_elliptic(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ELLIPTIC_{length}", "filters", offset)


def edcf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Ehlers Distance Coefficient Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_edcf(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EDCF_{length}", "filters", offset)


def bpf(close: object, length: int = 14, bandwidth: int = 5,
        offset: int = 0, **kwargs) -> object:
    """Bandpass Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bpf(_ptr(src), n, _ptr(dst), length, int(bandwidth)))
    return _wrap(dst, idx, f"BPF_{length}", "filters", offset)


def alaguerre(close: object, length: int = 20, order: int = 5,
              offset: int = 0, **kwargs) -> object:
    """Adaptive Laguerre Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_alaguerre(_ptr(src), n, _ptr(dst), length, int(order)))
    return _wrap(dst, idx, f"ALAGUERRE_{length}", "filters", offset)


def bilateral(close: object, length: int = 14, sigma_s: float = 0.5,
              sigma_r: float = 1.0, offset: int = 0, **kwargs) -> object:
    """Bilateral Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bilateral(_ptr(src), n, _ptr(dst), length, float(sigma_s), float(sigma_r)))
    return _wrap(dst, idx, f"BILATERAL_{length}", "filters", offset)


def baxterking(close: object, length: int = 12, min_period: int = 6,
               max_period: int = 32, offset: int = 0, **kwargs) -> object:
    """Baxter-King Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_baxterking(_ptr(src), n, _ptr(dst), length, int(min_period), int(max_period)))
    return _wrap(dst, idx, f"BAXTERKING_{length}", "filters", offset)


def cfitz(close: object, length: int = 6, bw_period: int = 32,
          offset: int = 0, **kwargs) -> object:
    """Christiano-Fitzgerald Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cfitz(_ptr(src), n, _ptr(dst), length, int(bw_period)))
    return _wrap(dst, idx, f"CFITZ_{length}", "filters", offset)
