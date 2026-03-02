"""quantalib core indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "ha",
    "midpoint",
    "midprice",
    "wclprice",
    "avgprice",
    "medprice",
    "typprice",
    "midbody",
]


def ha(open: object, high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Heikin-Ashi Candles."""
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    haOpenOut = _out(n)
    haHighOut = _out(n)
    haLowOut = _out(n)
    haCloseOut = _out(n)
    _check(_lib.qtl_ha(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(haOpenOut), _ptr(haHighOut), _ptr(haLowOut), _ptr(haCloseOut), n))
    return _wrap_multi({"haOpenOut": haOpenOut, "haHighOut": haHighOut, "haLowOut": haLowOut, "haCloseOut": haCloseOut}, idx, "core", offset)


def midpoint(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Midpoint = src[i] over period."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_midpoint(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MIDPOINT_{period}", "core", offset)


def midprice(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mid Price = (High+Low)/2 over period."""
    period = int(period)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_midprice(_ptr(h), _ptr(l), _ptr(output), n, period))
    return _wrap(output, idx, f"MIDPRICE_{period}", "core", offset)


def wclprice(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Weighted Close Price = (H+L+2*C)/4."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_wclprice(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n))
    return _wrap(output, idx, "WCLPRICE", "core", offset)

def avgprice(open: object, high: object, low: object, close: object,
             offset: int = 0, **kwargs) -> object:
    """Average Price = (O+H+L+C)/4."""
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_avgprice(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "AVGPRICE", "core", offset)


def medprice(high: object, low: object, offset: int = 0, **kwargs) -> object:
    """Median Price = (H+L)/2."""
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_medprice(_ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "MEDPRICE", "core", int(offset))


def typprice(open: object, high: object, low: object,
             offset: int = 0, **kwargs) -> object:
    """Typical Price = (O+H+L)/3 (QuanTAlib variant)."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_typprice(_ptr(o), _ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "TYPPRICE", "core", int(offset))


def midbody(open: object, close: object, offset: int = 0, **kwargs) -> object:
    """Mid Body = (O+C)/2."""
    o, idx = _arr(open); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_midbody(_ptr(o), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "MIDBODY", "core", int(offset))
