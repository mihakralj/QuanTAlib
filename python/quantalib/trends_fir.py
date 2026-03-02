"""quantalib trends_fir indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


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


def fwma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Fibonacci Weighted Moving Average."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_fwma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"FWMA_{period}", "trends_fir", offset)


def gwma(close: object, period: int = 14, sigma: float = 0.4, offset: int = 0, **kwargs) -> object:
    """Gaussian Weighted Moving Average."""
    period = int(period)
    sigma = float(sigma)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_gwma(_ptr(src), _ptr(output), n, period, sigma))
    return _wrap(output, idx, f"GWMA_{period}", "trends_fir", offset)


def hamma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Hamming Moving Average."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hamma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HAMMA_{period}", "trends_fir", offset)


def hend(close: object, period: int = 14, nanValue: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Henderson Moving Average."""
    period = int(period)
    nanValue = float(nanValue)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hend(_ptr(src), _ptr(output), n, period, nanValue))
    return _wrap(output, idx, f"HEND_{period}", "trends_fir", offset)


def ilrs(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Integral of Linear Regression Slope."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ilrs(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ILRS_{period}", "trends_fir", offset)


def kaiser(close: object, period: int = 14, beta: float = 3.0, nanValue: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Kaiser Window Moving Average."""
    period = int(period)
    beta = float(beta)
    nanValue = float(nanValue)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_kaiser(_ptr(src), _ptr(output), n, period, beta, nanValue))
    return _wrap(output, idx, f"KAISER_{period}", "trends_fir", offset)


def lanczos(close: object, period: int = 14, nanValue: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Lanczos Moving Average."""
    period = int(period)
    nanValue = float(nanValue)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_lanczos(_ptr(src), _ptr(output), n, period, nanValue))
    return _wrap(output, idx, f"LANCZOS_{period}", "trends_fir", offset)


def nlma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Non-Lag Moving Average."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nlma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"NLMA_{period}", "trends_fir", offset)


def nyqma(close: object, period: int = 14, nyquistPeriod: int = 2, offset: int = 0, **kwargs) -> object:
    """Nyquist Moving Average."""
    period = int(period)
    nyquistPeriod = int(nyquistPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nyqma(_ptr(src), _ptr(output), n, period, nyquistPeriod))
    return _wrap(output, idx, f"NYQMA_{period}", "trends_fir", offset)


def pma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Predictive Moving Average."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    pmaOutput = _out(n)
    triggerOutput = _out(n)
    _check(_lib.qtl_pma(_ptr(src), _ptr(pmaOutput), _ptr(triggerOutput), n, period))
    return _wrap_multi({"pmaOutput": pmaOutput, "triggerOutput": triggerOutput}, idx, "trends_fir", offset)


def pwma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Pascal Weighted Moving Average."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pwma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"PWMA_{period}", "trends_fir", offset)


def qrma(close: object, period: int = 14, initialLastValid: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Quick Reaction Moving Average."""
    period = int(period)
    initialLastValid = float(initialLastValid)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_qrma(_ptr(src), _ptr(output), n, period, initialLastValid))
    return _wrap(output, idx, f"QRMA_{period}", "trends_fir", offset)


def rwma(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Range Weighted Moving Average."""
    period = int(period)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_rwma(_ptr(c), _ptr(h), _ptr(l), _ptr(output), n, period))
    return _wrap(output, idx, f"RWMA_{period}", "trends_fir", offset)

def sma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Simple Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SMA_{length}", "trends_fir", offset)


def wma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_wma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"WMA_{length}", "trends_fir", offset)


def hma(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Hull Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"HMA_{length}", "trends_fir", offset)


def trima(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Triangular Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trima(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TRIMA_{length}", "trends_fir", offset)


def swma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Symmetric Weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_swma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SWMA_{length}", "trends_fir", offset)


def dwma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Double Weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dwma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DWMA_{length}", "trends_fir", offset)


def blma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Blackman Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_blma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"BLMA_{length}", "trends_fir", offset)


def alma(close: object, length: int = 10, alma_offset: float = 0.85,
         sigma: float = 6.0, offset: int = 0, **kwargs) -> object:
    """Arnaud Legoux Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_alma(_ptr(src), n, _ptr(dst), length, float(alma_offset), float(sigma)))
    return _wrap(dst, idx, f"ALMA_{length}", "trends_fir", offset)


def lsma(close: object, length: int = 25, offset: int = 0, **kwargs) -> object:
    """Least Squares Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lsma(_ptr(src), n, _ptr(dst), length, 0, 1.0))
    return _wrap(dst, idx, f"LSMA_{length}", "trends_fir", offset)


def sgma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Savitzky-Golay Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sgma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SGMA_{length}", "trends_fir", offset)


def sinema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Sine-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sinema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SINEMA_{length}", "trends_fir", offset)


def hanma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Hann-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hanma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"HANMA_{length}", "trends_fir", offset)


def parzen(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Parzen-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_parzen(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"PARZEN_{length}", "trends_fir", offset)


def tsf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Time Series Forecast."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tsf(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TSF_{length}", "trends_fir", offset)


def conv(close: object, kernel: list | None = None,
         offset: int = 0, **kwargs) -> object:
    """Convolution with custom kernel."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    if kernel is None:
        kernel = [1.0]
    k = np.ascontiguousarray(kernel, dtype=_F64)
    _check(_lib.qtl_conv(_ptr(src), n, _ptr(dst), _ptr(k), len(k)))
    return _wrap(dst, idx, "CONV", "trends_fir", offset)


def bwma(close: object, length: int = 10, order: int = 0,
         offset: int = 0, **kwargs) -> object:
    """Butterworth-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bwma(_ptr(src), n, _ptr(dst), length, int(order)))
    return _wrap(dst, idx, f"BWMA_{length}", "trends_fir", offset)


def crma(close: object, length: int = 10, volume_factor: float = 1.0,
         offset: int = 0, **kwargs) -> object:
    """Cosine-Ramp Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_crma(_ptr(src), n, _ptr(dst), length, float(volume_factor)))
    return _wrap(dst, idx, f"CRMA_{length}", "trends_fir", offset)


def sp15(close: object, length: int = 15, offset: int = 0, **kwargs) -> object:
    """SP-15 Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sp15(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SP15_{length}", "trends_fir", offset)


def tukey_w(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Tukey-windowed Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tukey_w(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TUKEY_{length}", "trends_fir", offset)


def rain(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """RAIN Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rain(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"RAIN_{length}", "trends_fir", offset)


def afirma(close: object, length: int = 10, window_type: int = 0,
           use_simd: bool = False, offset: int = 0, **kwargs) -> object:
    """Adaptive FIR Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_afirma(_ptr(src), n, _ptr(dst), length, int(window_type), int(use_simd)))
    return _wrap(dst, idx, f"AFIRMA_{length}", "trends_fir", offset)
