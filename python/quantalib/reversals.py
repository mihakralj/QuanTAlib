"""quantalib reversals indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "chandelier",
    "ckstop",
    "fractals",
    "pivot",
    "pivotcam",
    "pivotdem",
    "pivotext",
    "pivotfib",
    "pivotwood",
    "psar",
    "swings",
    "ttm_scalper",
]


def chandelier(open: object, high: object, low: object, close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Chandelier Exit."""
    period = int(period)
    multiplier = float(multiplier)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_chandelier(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period, multiplier))
    return _wrap(output, idx, f"CHANDELIER_{period}", "reversals", offset)


def ckstop(open: object, high: object, low: object, close: object, atrPeriod: int = 22, multiplier: float = 2.0, stopPeriod: int = 3, offset: int = 0, **kwargs) -> object:
    """Chuck LeBeau Stop."""
    atrPeriod = int(atrPeriod)
    multiplier = float(multiplier)
    stopPeriod = int(stopPeriod)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_ckstop(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, atrPeriod, multiplier, stopPeriod))
    return _wrap(output, idx, f"CKSTOP_{atrPeriod}", "reversals", offset)


def fractals(high: object, low: object, offset: int = 0, **kwargs) -> object:
    """Williams Fractals."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    upOutput = _out(n)
    downOutput = _out(n)
    _check(_lib.qtl_fractals(_ptr(h), _ptr(l), _ptr(upOutput), _ptr(downOutput), n))
    return _wrap_multi({"upOutput": upOutput, "downOutput": downOutput}, idx, "reversals", offset)


def pivot(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Pivot Points (Traditional)."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivot(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOT", "reversals", offset)


def pivotcam(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Camarilla Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotcam(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTCAM", "reversals", offset)


def pivotdem(open: object, high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """DeMark Pivot Points."""
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotdem(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTDEM", "reversals", offset)


def pivotext(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Extended Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotext(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTEXT", "reversals", offset)


def pivotfib(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Fibonacci Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotfib(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTFIB", "reversals", offset)


def pivotwood(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """Woodie Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotwood(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTWOOD", "reversals", offset)


def psar(open: object, high: object, low: object, close: object, afStart: float = 0.02, afIncrement: float = 0.02, afMax: float = 0.2, offset: int = 0, **kwargs) -> object:
    """Parabolic SAR."""
    afStart = float(afStart)
    afIncrement = float(afIncrement)
    afMax = float(afMax)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_psar(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, afStart, afIncrement, afMax))
    return _wrap(output, idx, "PSAR", "reversals", offset)


def swings(high: object, low: object, lookback: int = 5, offset: int = 0, **kwargs) -> object:
    """Swing High/Low."""
    lookback = int(lookback)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    highOutput = _out(n)
    lowOutput = _out(n)
    _check(_lib.qtl_swings(_ptr(h), _ptr(l), _ptr(highOutput), _ptr(lowOutput), n, lookback))
    return _wrap_multi({"highOutput": highOutput, "lowOutput": lowOutput}, idx, "reversals", offset)


def ttm_scalper(high: object, low: object, close: object, useCloses: int = 0, offset: int = 0, **kwargs) -> object:
    """TTM Scalper."""
    useCloses = int(useCloses)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    highOutput = _out(n)
    lowOutput = _out(n)
    _check(_lib.qtl_ttmscalper(_ptr(h), _ptr(l), _ptr(c), _ptr(highOutput), _ptr(lowOutput), n, useCloses))
    return _wrap_multi({"highOutput": highOutput, "lowOutput": lowOutput}, idx, "reversals", offset)
