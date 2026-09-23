"""quantalib volume indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "ad",
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


def ad(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Accumulation/Distribution Line."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_ad(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "AD", "volume", offset)


def adosc(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Accumulation/Distribution Oscillator."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_adosc(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, fastPeriod, slowPeriod))
    return _wrap(output, idx, f"ADOSC_{fastPeriod}", "volume", offset)


def iii(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, period: int = 14, cumulative: int = 0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Intraday Intensity Index."""
    period = int(kwargs.get("length", period))
    cumulative = int(cumulative)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_iii(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, period, cumulative))
    return _wrap(output, idx, f"III_{period}", "volume", offset)


def kvo(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, fastPeriod: int = 12, slowPeriod: int = 26, signalPeriod: int = 9, offset: int = 0, **kwargs: Any) -> ArrayLike:
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


def twap(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Time Weighted Average Price."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_twap(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"TWAP_{period}", "volume", offset)


def va(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Accumulation."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_va(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "VA", "volume", offset)


def vo(volume: ArrayLike, shortPeriod: int = 12, longPeriod: int = 26, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Oscillator."""
    shortPeriod = int(shortPeriod)
    longPeriod = int(longPeriod)
    offset = int(offset)
    src, idx = _arr(volume)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vo(_ptr(src), _ptr(output), n, shortPeriod, longPeriod))
    return _wrap(output, idx, f"VO_{shortPeriod}", "volume", offset)


def vroc(volume: ArrayLike, period: int = 14, usePercent: int = 1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Rate of Change."""
    period = int(kwargs.get("length", period))
    usePercent = int(usePercent)
    offset = int(offset)
    src, idx = _arr(volume)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vroc(_ptr(src), _ptr(output), n, period, usePercent))
    return _wrap(output, idx, f"VROC_{period}", "volume", offset)


def vwad(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Weighted Accumulation/Distribution."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_vwad(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, period))
    return _wrap(output, idx, f"VWAD_{period}", "volume", offset)


def vwap(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Weighted Average Price."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_vwap(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n, period))
    return _wrap(output, idx, f"VWAP_{period}", "volume", offset)


def wad(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Williams Accumulation/Distribution."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_wad(_ptr(h), _ptr(l), _ptr(c), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "WAD", "volume", offset)

def obv(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """On-Balance Volume."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_obv(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "OBV", "volume", offset)


def pvt(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Price Volume Trend."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvt(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVT", "volume", offset)


def pvr(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Price Volume Rank."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvr(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVR", "volume", offset)


def vf(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Flow."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_vf(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "VF", "volume", offset)


def nvi(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Negative Volume Index."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_nvi(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "NVI", "volume", offset)


def pvi(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Positive Volume Index."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvi(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVI", "volume", offset)


def tvi(close: ArrayLike, volume: ArrayLike, period: int = 14,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Trade Volume Index."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_tvi(_ptr(c), _ptr(v), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TVI_{period}", "volume", offset)


def pvd(close: ArrayLike, volume: ArrayLike, period: int = 14,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Price Volume Divergence."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvd(_ptr(c), _ptr(v), n, _ptr(dst), period))
    return _wrap(dst, idx, f"PVD_{period}", "volume", offset)


def vwma(close: ArrayLike, volume: ArrayLike, period: int = 20,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_vwma(_ptr(c), _ptr(v), n, _ptr(dst), period))
    return _wrap(dst, idx, f"VWMA_{period}", "volume", offset)


def evwma(close: ArrayLike, volume: ArrayLike, period: int = 20,
          offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Elastic Volume Weighted Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_evwma(_ptr(c), _ptr(v), n, _ptr(dst), period))
    return _wrap(dst, idx, f"EVWMA_{period}", "volume", offset)


def efi(close: ArrayLike, volume: ArrayLike, period: int = 13,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Elder Force Index."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_efi(_ptr(c), _ptr(v), n, _ptr(dst), period))
    return _wrap(dst, idx, f"EFI_{period}", "volume", offset)


def aobv(close: ArrayLike, volume: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Archer OBV -> (fast, slow) or DataFrame."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); obv_out = _out(n); sig = _out(n)
    _check(_lib.qtl_aobv(_ptr(c), _ptr(v), n, _ptr(obv_out), _ptr(sig)))
    return _wrap_multi({"AOBV": obv_out, "AOBV_SIG": sig}, idx, "volume", offset)


def mfi(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike,
        length: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Money Flow Index."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_mfi(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MFI_{length}", "volume", offset)


def cmf(high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike,
        length: int = 20, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Chaikin Money Flow."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_cmf(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CMF_{length}", "volume", offset)


def eom(high: ArrayLike, low: ArrayLike, volume: ArrayLike,
        length: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ease of Movement."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_eom(_ptr(h), _ptr(l), _ptr(v), n, _ptr(dst), length, 1e9))
    return _wrap(dst, idx, f"EOM_{length}", "volume", offset)


def pvo(volume: ArrayLike, fast: int = 12, slow: int = 26, signal: int = 9,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Percentage Volume Oscillator -> (pvo, signal, histogram) or DataFrame."""
    fast = int(fast); slow = int(slow); signal = int(signal); offset = int(offset)
    v, idx = _arr(volume); n = len(v)
    pvo_out = _out(n); sig = _out(n); hist = _out(n)
    _check(_lib.qtl_pvo(_ptr(v), n, _ptr(pvo_out), _ptr(sig), _ptr(hist), fast, slow, signal))
    return _wrap_multi(
        {f"PVO_{fast}_{slow}_{signal}": pvo_out, f"PVOs_{fast}_{slow}_{signal}": sig, f"PVOh_{fast}_{slow}_{signal}": hist},
        idx, "volume", offset)
