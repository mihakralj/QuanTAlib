"""quantalib volume indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "adl",
    "adosc",
    "iii",
    "kvo",
    "twap",
    "va",
    "vo",
    "vroc",
    "vwad",
    "vwap",
    "wad",
    "obv",
    "pvt",
    "pvr",
    "vf",
    "nvi",
    "pvi",
    "tvi",
    "pvd",
    "vwma",
    "evwma",
    "efi",
    "aobv",
    "mfi",
    "cmf",
    "eom",
    "pvo",
]


def adl(high: object, low: object, close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Accumulation/Distribution Line."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_adl(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "ADL", "volume", offset)


def adosc(high: object, low: object, close: object, volume: object, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs) -> object:
    """Accumulation/Distribution Oscillator."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_adosc(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, fastPeriod, slowPeriod))
    return _wrap(output, idx, f"ADOSC_{fastPeriod}", "volume", offset)


def iii(high: object, low: object, close: object, volume: object, period: int = 14, cumulative: int = 0, offset: int = 0, **kwargs) -> object:
    """Intraday Intensity Index."""
    period = int(period)
    cumulative = int(cumulative)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_iii(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, period, cumulative))
    return _wrap(output, idx, f"III_{period}", "volume", offset)


def kvo(high: object, low: object, close: object, volume: object, fastPeriod: int = 12, slowPeriod: int = 26, signalPeriod: int = 9, offset: int = 0, **kwargs) -> object:
    """Klinger Volume Oscillator."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    signalPeriod = int(signalPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    signal = _out(n)
    _check(_lib.qtl_kvo(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), _ptr(signal), n, fastPeriod, slowPeriod, signalPeriod))
    return _wrap_multi({"output": output, "signal": signal}, idx, "volume", offset)


def twap(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Time Weighted Average Price."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_twap(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"TWAP_{period}", "volume", offset)


def va(high: object, low: object, close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Volume Accumulation."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_va(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "VA", "volume", offset)


def vo(volume: object, shortPeriod: int = 12, longPeriod: int = 26, offset: int = 0, **kwargs) -> object:
    """Volume Oscillator."""
    shortPeriod = int(shortPeriod)
    longPeriod = int(longPeriod)
    offset = int(offset)
    src, idx = _arr(volume)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vo(_ptr(src), _ptr(output), n, shortPeriod, longPeriod))
    return _wrap(output, idx, f"VO_{shortPeriod}", "volume", offset)


def vroc(volume: object, period: int = 14, usePercent: int = 1, offset: int = 0, **kwargs) -> object:
    """Volume Rate of Change."""
    period = int(period)
    usePercent = int(usePercent)
    offset = int(offset)
    src, idx = _arr(volume)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vroc(_ptr(src), _ptr(output), n, period, usePercent))
    return _wrap(output, idx, f"VROC_{period}", "volume", offset)


def vwad(high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Volume Weighted Accumulation/Distribution."""
    period = int(period)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_vwad(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, period))
    return _wrap(output, idx, f"VWAD_{period}", "volume", offset)


def vwap(high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Volume Weighted Average Price."""
    period = int(period)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_vwap(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, period))
    return _wrap(output, idx, f"VWAP_{period}", "volume", offset)


def wad(high: object, low: object, close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Williams Accumulation/Distribution."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_wad(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "WAD", "volume", offset)

def obv(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """On-Balance Volume."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_obv(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "OBV", "volume", offset)


def pvt(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Price Volume Trend."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvt(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVT", "volume", offset)


def pvr(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Price Volume Rank."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvr(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVR", "volume", offset)


def vf(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Volume Flow."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_vf(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "VF", "volume", offset)


def nvi(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Negative Volume Index."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_nvi(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "NVI", "volume", offset)


def pvi(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Positive Volume Index."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvi(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVI", "volume", offset)


def tvi(close: object, volume: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """Trade Volume Index."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_tvi(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TVI_{length}", "volume", offset)


def pvd(close: object, volume: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """Price Volume Divergence."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvd(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"PVD_{length}", "volume", offset)


def vwma(close: object, volume: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Volume Weighted Moving Average."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_vwma(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"VWMA_{length}", "volume", offset)


def evwma(close: object, volume: object, length: int = 20,
          offset: int = 0, **kwargs) -> object:
    """Elastic Volume Weighted Moving Average."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_evwma(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EVWMA_{length}", "volume", offset)


def efi(close: object, volume: object, length: int = 13,
        offset: int = 0, **kwargs) -> object:
    """Elder Force Index."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_efi(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EFI_{length}", "volume", offset)


def aobv(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Archer OBV -> (fast, slow) or DataFrame."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); obv_out = _out(n); sig = _out(n)
    _check(_lib.qtl_aobv(_ptr(c), _ptr(v), n, _ptr(obv_out), _ptr(sig)))
    return _wrap_multi({"AOBV": obv_out, "AOBV_SIG": sig}, idx, "volume", offset)


def mfi(high: object, low: object, close: object, volume: object,
        length: int = 14, offset: int = 0, **kwargs) -> object:
    """Money Flow Index."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_mfi(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MFI_{length}", "volume", offset)


def cmf(high: object, low: object, close: object, volume: object,
        length: int = 20, offset: int = 0, **kwargs) -> object:
    """Chaikin Money Flow."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_cmf(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CMF_{length}", "volume", offset)


def eom(high: object, low: object, volume: object,
        length: int = 14, offset: int = 0, **kwargs) -> object:
    """Ease of Movement."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_eom(_ptr(h), _ptr(l), _ptr(v), n, _ptr(dst), length, 1e9))
    return _wrap(dst, idx, f"EOM_{length}", "volume", offset)


def pvo(volume: object, fast: int = 12, slow: int = 26, signal: int = 9,
        offset: int = 0, **kwargs) -> object:
    """Percentage Volume Oscillator -> (pvo, signal, histogram) or DataFrame."""
    fast = int(fast); slow = int(slow); signal = int(signal); offset = int(offset)
    v, idx = _arr(volume); n = len(v)
    pvo_out = _out(n); sig = _out(n); hist = _out(n)
    _check(_lib.qtl_pvo(_ptr(v), n, _ptr(pvo_out), _ptr(sig), _ptr(hist), fast, slow, signal))
    return _wrap_multi(
        {f"PVO_{fast}_{slow}_{signal}": pvo_out, f"PVOs_{fast}_{slow}_{signal}": sig, f"PVOh_{fast}_{slow}_{signal}": hist},
        idx, "volume", offset)
