"""quantalib reversals indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "atrstop",
    "chandelier",
    "ckstop",
    "fractals",
    "pivot",
    "pivotcam",
    "pivotdem",
    "pivotext",
    "pivotfib",
    "pivotwood",
    "sar",
    "sarext",
    "swings",
    "ttm_scalper",
    "vstop",
]


def atrstop(high: ArrayLike, low: ArrayLike, close: ArrayLike, period: int = 21, multiplier: float = 3.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """ATR Trailing Stop."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_atrstop(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period, multiplier))
    return _wrap(output, idx, f"ATRSTOP_{period}", "reversals", offset)


def chandelier(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Chandelier Exit."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_chandelier(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period, multiplier))
    return _wrap(output, idx, f"CHANDELIER_{period}", "reversals", offset)


def ckstop(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, atrPeriod: int = 22, multiplier: float = 2.0, stopPeriod: int = 3, offset: int = 0, **kwargs: Any) -> ArrayLike:
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


def fractals(high: ArrayLike, low: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Williams Fractals."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    upOutput = _out(n)
    downOutput = _out(n)
    _check(_lib.qtl_fractals(_ptr(h), _ptr(l), _ptr(upOutput), _ptr(downOutput), n))
    return _wrap_multi({"upOutput": upOutput, "downOutput": downOutput}, idx, "reversals", offset)


def pivot(high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Pivot Points (Traditional)."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivot(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOT", "reversals", offset)


def pivotcam(high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Camarilla Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotcam(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTCAM", "reversals", offset)


def pivotdem(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """DeMark Pivot Points."""
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotdem(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTDEM", "reversals", offset)


def pivotext(high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Extended Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotext(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTEXT", "reversals", offset)


def pivotfib(high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Fibonacci Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotfib(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTFIB", "reversals", offset)


def pivotwood(high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Woodie Pivot Points."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    ppOutput = _out(n)
    _check(_lib.qtl_pivotwood(_ptr(h), _ptr(l), _ptr(c), _ptr(ppOutput), n))
    return _wrap(ppOutput, idx, "PIVOTWOOD", "reversals", offset)


def sar(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, afStart: float = 0.02, afIncrement: float = 0.02, afMax: float = 0.2, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Parabolic SAR."""
    afStart = float(afStart)
    afIncrement = float(afIncrement)
    afMax = float(afMax)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_sar(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, afStart, afIncrement, afMax))
    return _wrap(output, idx, "SAR", "reversals", offset)


def sarext(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, startValue: float = 0.0, offsetOnReverse: float = 0.0, afInitLong: float = 0.02, afLong: float = 0.02, afMaxLong: float = 0.2, afInitShort: float = 0.02, afShort: float = 0.02, afMaxShort: float = 0.2, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Parabolic SAR Extended."""
    startValue = float(startValue)
    offsetOnReverse = float(offsetOnReverse)
    afInitLong = float(afInitLong)
    afLong = float(afLong)
    afMaxLong = float(afMaxLong)
    afInitShort = float(afInitShort)
    afShort = float(afShort)
    afMaxShort = float(afMaxShort)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_sarext(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, startValue, offsetOnReverse, afInitLong, afLong, afMaxLong, afInitShort, afShort, afMaxShort))
    return _wrap(output, idx, "SAREXT", "reversals", offset)


def swings(high: ArrayLike, low: ArrayLike, lookback: int = 5, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Swing High/Low."""
    lookback = int(lookback)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    highOutput = _out(n)
    lowOutput = _out(n)
    _check(_lib.qtl_swings(_ptr(h), _ptr(l), _ptr(highOutput), _ptr(lowOutput), n, lookback))
    return _wrap_multi({"highOutput": highOutput, "lowOutput": lowOutput}, idx, "reversals", offset)


def ttm_scalper(high: ArrayLike, low: ArrayLike, close: ArrayLike, useCloses: int = 0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """TTM Scalper."""
    useCloses = int(useCloses)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    highOutput = _out(n)
    lowOutput = _out(n)
    _check(_lib.qtl_ttmscalper(_ptr(h), _ptr(l), _ptr(c), _ptr(highOutput), _ptr(lowOutput), n, useCloses))
    return _wrap_multi({"highOutput": highOutput, "lowOutput": lowOutput}, idx, "reversals", offset)


def vstop(high: ArrayLike, low: ArrayLike, close: ArrayLike, period: int = 7, multiplier: float = 3.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volatility Stop."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_vstop(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period, multiplier))
    return _wrap(output, idx, f"VSTOP_{period}", "reversals", offset)
