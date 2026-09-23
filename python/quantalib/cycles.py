"""quantalib cycles indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "homod",
    "ht_dcperiod",
    "ht_dcphase",
    "ht_phasor",
    "ht_sine",
    "lpf",
    "lunar",
    "solar",
    "ssfdsp",
    "cg",
    "dsp",
    "ccor",
    "ebsw",
    "acp",
    "amfm",
    "fsi",
    "epa",
]


def homod(close: ArrayLike, minPeriod: float = 6, maxPeriod: float = 48, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Homodyne Discriminator."""
    minPeriod = float(minPeriod)
    maxPeriod = float(maxPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_homod(_ptr(src), _ptr(output), n, minPeriod, maxPeriod))
    return _wrap(output, idx, f"HOMOD_{minPeriod}", "cycles", offset)


def ht_dcperiod(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hilbert Transform Dominant Cycle Period."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_htdcperiod(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HT_DCPERIOD", "cycles", offset)


def ht_dcphase(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hilbert Transform Dominant Cycle Phase."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_htdcphase(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HT_DCPHASE", "cycles", offset)


def ht_phasor(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hilbert Transform Phasor."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    inPhase = _out(n)
    quadrature = _out(n)
    _check(_lib.qtl_htphasor(_ptr(src), _ptr(inPhase), _ptr(quadrature), n))
    return _wrap_multi({"inPhase": inPhase, "quadrature": quadrature}, idx, "cycles", offset)


def ht_sine(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hilbert Transform Sine."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    sine = _out(n)
    leadSine = _out(n)
    _check(_lib.qtl_htsine(_ptr(src), _ptr(sine), _ptr(leadSine), n))
    return _wrap_multi({"sine": sine, "leadSine": leadSine}, idx, "cycles", offset)


def lpf(close: ArrayLike, lower_bound: int = 18, upper_bound: int = 40,
        data_length: int = 40, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ehlers Linear Predictive Filter (dominant cycle)."""
    lower_bound = int(lower_bound); upper_bound = int(upper_bound)
    data_length = int(data_length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lpf(_ptr(src), n, _ptr(dst), lower_bound, upper_bound, data_length))
    return _wrap(dst, idx, f"LPF_{lower_bound}_{upper_bound}", "cycles", offset)


def lunar(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Lunar Cycle."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_lunar(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "LUNAR", "cycles", offset)


def solar(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Solar Cycle."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_solar(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "SOLAR", "cycles", offset)


def ssfdsp(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Supersmoother DSP."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ssfdsp(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"SSFDSP_{period}", "cycles", offset)

def cg(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Center of Gravity."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cg(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CG_{period}", "cycles", offset)


def dsp(close: ArrayLike, period: int = 20, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Dominant Cycle Period (DSP)."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dsp(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DSP_{period}", "cycles", offset)


def ccor(close: ArrayLike, period: int = 20, alpha: float = 0.07,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Circular Correlation."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ccor(_ptr(src), n, _ptr(dst), period, float(alpha)))
    return _wrap(dst, idx, f"CCOR_{period}", "cycles", offset)


def ebsw(close: ArrayLike, hp_length: int = 40, ssf_length: int = 10,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Even Better Sinewave."""
    hp_length = int(hp_length); ssf_length = int(ssf_length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ebsw(_ptr(src), n, _ptr(dst), hp_length, ssf_length))
    return _wrap(dst, idx, f"EBSW_{hp_length}", "cycles", offset)


def acp(close: ArrayLike, min_period: int = 8, max_period: int = 48,
        avg_length: int = 3, enhance: int = 1,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ehlers Autocorrelation Periodogram."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_acp(_ptr(src), n, _ptr(dst), int(min_period), int(max_period), int(avg_length), int(enhance)))
    return _wrap(dst, idx, f"ACP_{min_period}_{max_period}", "cycles", offset)


def amfm(open: ArrayLike, close: ArrayLike, period: int = 30,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ehlers AM Detector / FM Demodulator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    o, idx = _arr(open); c, _ = _arr(close)
    n = len(o); fm = _out(n); am = _out(n)
    _check(_lib.qtl_amfm(_ptr(o), _ptr(c), n, _ptr(fm), _ptr(am), period))
    return _wrap_multi({f"FM_{period}": fm, f"AM_{period}": am}, idx, "cycles", offset)


def fsi(close: ArrayLike, period: int = 20, bandwidth: float = 0.1,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ehlers Fourier Series Indicator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_fsi(_ptr(src), n, _ptr(dst), period, float(bandwidth)))
    return _wrap(dst, idx, f"FSI_{period}", "cycles", offset)


def epa(close: ArrayLike, period: int = 28,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ehlers Phasor Analysis."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_epa(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"EPA_{period}", "cycles", offset)
