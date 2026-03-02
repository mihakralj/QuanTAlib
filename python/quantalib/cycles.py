"""quantalib cycles indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "homod",
    "ht_dcperiod",
    "ht_dcphase",
    "ht_phasor",
    "ht_sine",
    "lunar",
    "solar",
    "ssfdsp",
    "cg",
    "dsp",
    "ccor",
    "ebsw",
    "eacp",
]


def homod(close: object, minPeriod: float = 6, maxPeriod: float = 48, offset: int = 0, **kwargs) -> object:
    """Homodyne Discriminator."""
    minPeriod = float(minPeriod)
    maxPeriod = float(maxPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_homod(_ptr(src), _ptr(output), n, minPeriod, maxPeriod))
    return _wrap(output, idx, f"HOMOD_{minPeriod}", "cycles", offset)


def ht_dcperiod(close: object, offset: int = 0, **kwargs) -> object:
    """Hilbert Transform Dominant Cycle Period."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_htdcperiod(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HT_DCPERIOD", "cycles", offset)


def ht_dcphase(close: object, offset: int = 0, **kwargs) -> object:
    """Hilbert Transform Dominant Cycle Phase."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_htdcphase(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HT_DCPHASE", "cycles", offset)


def ht_phasor(close: object, offset: int = 0, **kwargs) -> object:
    """Hilbert Transform Phasor."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    inPhase = _out(n)
    quadrature = _out(n)
    _check(_lib.qtl_htphasor(_ptr(src), _ptr(inPhase), _ptr(quadrature), n))
    return _wrap_multi({"inPhase": inPhase, "quadrature": quadrature}, idx, "cycles", offset)


def ht_sine(close: object, offset: int = 0, **kwargs) -> object:
    """Hilbert Transform Sine."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    sine = _out(n)
    leadSine = _out(n)
    _check(_lib.qtl_htsine(_ptr(src), _ptr(sine), _ptr(leadSine), n))
    return _wrap_multi({"sine": sine, "leadSine": leadSine}, idx, "cycles", offset)


def lunar(close: object, offset: int = 0, **kwargs) -> object:
    """Lunar Cycle."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_lunar(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "LUNAR", "cycles", offset)


def solar(close: object, offset: int = 0, **kwargs) -> object:
    """Solar Cycle."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_solar(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "SOLAR", "cycles", offset)


def ssfdsp(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Supersmoother DSP."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ssfdsp(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"SSFDSP_{period}", "cycles", offset)

def cg(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Center of Gravity."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cg(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CG_{length}", "cycles", offset)


def dsp(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Dominant Cycle Period (DSP)."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dsp(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DSP_{length}", "cycles", offset)


def ccor(close: object, length: int = 20, alpha: float = 0.07,
         offset: int = 0, **kwargs) -> object:
    """Circular Correlation."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ccor(_ptr(src), n, _ptr(dst), length, float(alpha)))
    return _wrap(dst, idx, f"CCOR_{length}", "cycles", offset)


def ebsw(close: object, hp_length: int = 40, ssf_length: int = 10,
         offset: int = 0, **kwargs) -> object:
    """Even Better Sinewave."""
    hp_length = int(hp_length); ssf_length = int(ssf_length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ebsw(_ptr(src), n, _ptr(dst), hp_length, ssf_length))
    return _wrap(dst, idx, f"EBSW_{hp_length}", "cycles", offset)


def eacp(close: object, min_period: int = 8, max_period: int = 48,
         avg_length: int = 3, enhance: int = 1,
         offset: int = 0, **kwargs) -> object:
    """Ehlers Autocorrelation Periodogram."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_eacp(_ptr(src), n, _ptr(dst), int(min_period), int(max_period), int(avg_length), int(enhance)))
    return _wrap(dst, idx, f"EACP_{min_period}_{max_period}", "cycles", offset)
