"""quantalib trends_fir indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "fwma",
    "gwma",
    "hamma",
    "hend",
    "ilrs",
    "kaiser",
    "lanczos",
    "nlma",
    "nyqma",
    "pma",
    "pwma",
    "qrma",
    "rwma",
    "sma",
    "wma",
    "hma",
    "trima",
    "swma",
    "dwma",
    "blma",
    "alma",
    "lsma",
    "sgma",
    "sinema",
    "hanma",
    "parzen",
    "tsf",
    "conv",
    "bwma",
    "crma",
    "sp15",
    "tukey_w",
    "rain",
    "afirma",
]


def fwma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Fibonacci Weighted Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_fwma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"FWMA_{period}", "trends_fir", offset)


def gwma(close: ArrayLike, period: int = 14, sigma: float = 0.4, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Gaussian Weighted Moving Average."""
    period = int(kwargs.get("length", period))
    sigma = float(sigma)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_gwma(_ptr(src), _ptr(output), n, period, sigma))
    return _wrap(output, idx, f"GWMA_{period}", "trends_fir", offset)


def hamma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hamming Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hamma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HAMMA_{period}", "trends_fir", offset)


def hend(close: ArrayLike, period: int = 14, nanValue: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Henderson Moving Average."""
    period = int(kwargs.get("length", period))
    nanValue = float(nanValue)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hend(_ptr(src), _ptr(output), n, period, nanValue))
    return _wrap(output, idx, f"HEND_{period}", "trends_fir", offset)


def ilrs(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Integral of Linear Regression Slope."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ilrs(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ILRS_{period}", "trends_fir", offset)


def kaiser(close: ArrayLike, period: int = 14, beta: float = 3.0, nanValue: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Kaiser Window Moving Average."""
    period = int(kwargs.get("length", period))
    beta = float(beta)
    nanValue = float(nanValue)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_kaiser(_ptr(src), _ptr(output), n, period, beta, nanValue))
    return _wrap(output, idx, f"KAISER_{period}", "trends_fir", offset)


def lanczos(close: ArrayLike, period: int = 14, nanValue: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Lanczos Moving Average."""
    period = int(kwargs.get("length", period))
    nanValue = float(nanValue)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lanczos(_ptr(src), _ptr(output), n, period, nanValue))
    return _wrap(output, idx, f"LANCZOS_{period}", "trends_fir", offset)


def nlma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Non-Lag Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nlma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"NLMA_{period}", "trends_fir", offset)


def nyqma(close: ArrayLike, period: int = 14, nyquistPeriod: int = 2, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Nyquist Moving Average."""
    period = int(kwargs.get("length", period))
    nyquistPeriod = int(nyquistPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nyqma(_ptr(src), _ptr(output), n, period, nyquistPeriod))
    return _wrap(output, idx, f"NYQMA_{period}", "trends_fir", offset)


def pma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Predictive Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    pmaOutput = _out(n)
    triggerOutput = _out(n)
    _check(_lib.qtl_pma(_ptr(src), _ptr(pmaOutput), _ptr(triggerOutput), n, period))
    return _wrap_multi({"pmaOutput": pmaOutput, "triggerOutput": triggerOutput}, idx, "trends_fir", offset)


def pwma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Pascal Weighted Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pwma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"PWMA_{period}", "trends_fir", offset)


def qrma(close: ArrayLike, period: int = 14, initialLastValid: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Quick Reaction Moving Average."""
    period = int(kwargs.get("length", period))
    initialLastValid = float(initialLastValid)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_qrma(_ptr(src), _ptr(output), n, period, initialLastValid))
    return _wrap(output, idx, f"QRMA_{period}", "trends_fir", offset)


def rwma(high: ArrayLike, low: ArrayLike, close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Range Weighted Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_rwma(_ptr(c), _ptr(h), _ptr(l), _ptr(output), n, period))
    return _wrap(output, idx, f"RWMA_{period}", "trends_fir", offset)

def sma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Simple Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"SMA_{period}", "trends_fir", offset)


def wma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_wma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"WMA_{period}", "trends_fir", offset)


def hma(close: ArrayLike, period: int = 9, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hull Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"HMA_{period}", "trends_fir", offset)


def trima(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Triangular Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trima(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TRIMA_{period}", "trends_fir", offset)


def swma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Symmetric Weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_swma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"SWMA_{period}", "trends_fir", offset)


def dwma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Double Weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dwma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DWMA_{period}", "trends_fir", offset)


def blma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Blackman Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_blma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"BLMA_{period}", "trends_fir", offset)


def alma(close: ArrayLike, period: int = 10, alma_offset: float = 0.85,
         sigma: float = 6.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Arnaud Legoux Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_alma(_ptr(src), n, _ptr(dst), period, float(alma_offset), float(sigma)))
    return _wrap(dst, idx, f"ALMA_{period}", "trends_fir", offset)


def lsma(close: ArrayLike, period: int = 25, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Least Squares Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lsma(_ptr(src), n, _ptr(dst), period, 0, 1.0))
    return _wrap(dst, idx, f"LSMA_{period}", "trends_fir", offset)


def sgma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Savitzky-Golay Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sgma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"SGMA_{period}", "trends_fir", offset)


def sinema(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Sine-weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sinema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"SINEMA_{period}", "trends_fir", offset)


def hanma(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hann-weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hanma(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"HANMA_{period}", "trends_fir", offset)


def parzen(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Parzen-weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_parzen(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"PARZEN_{period}", "trends_fir", offset)


def tsf(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Time Series Forecast."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tsf(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TSF_{period}", "trends_fir", offset)


def conv(close: ArrayLike, kernel: list | None = None,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Convolution with custom kernel."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    if kernel is None:
        kernel = [1.0]
    k = np.ascontiguousarray(kernel, dtype=_F64)
    _check(_lib.qtl_conv(_ptr(src), n, _ptr(dst), _ptr(k), len(k)))
    return _wrap(dst, idx, "CONV", "trends_fir", offset)


def bwma(close: ArrayLike, period: int = 10, order: int = 0,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Butterworth-weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bwma(_ptr(src), n, _ptr(dst), period, int(order)))
    return _wrap(dst, idx, f"BWMA_{period}", "trends_fir", offset)


def crma(close: ArrayLike, period: int = 10, volume_factor: float = 1.0,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Cosine-Ramp Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_crma(_ptr(src), n, _ptr(dst), period, float(volume_factor)))
    return _wrap(dst, idx, f"CRMA_{period}", "trends_fir", offset)


def sp15(close: ArrayLike, period: int = 15, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """SP-15 Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sp15(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"SP15_{period}", "trends_fir", offset)


def tukey_w(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Tukey-windowed Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tukey_w(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TUKEY_{period}", "trends_fir", offset)


def rain(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """RAIN Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rain(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"RAIN_{period}", "trends_fir", offset)


def afirma(close: ArrayLike, period: int = 10, window_type: int = 0,
           use_simd: bool = False, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Adaptive FIR Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_afirma(_ptr(src), n, _ptr(dst), period, int(window_type), int(use_simd)))
    return _wrap(dst, idx, f"AFIRMA_{period}", "trends_fir", offset)
