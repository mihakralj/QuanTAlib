"""quantalib momentum indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "bop",
    "cci",
    "macd",
    "pmo",
    "ppo",
    "rs",
    "rocp",
    "rocr",
    "sam",
    "vel",
    "rsi",
    "roc",
    "mom",
    "cmo",
    "tsi",
    "apo",
    "bias",
    "cfo",
    "cfb",
    "asi",
    "vwmacd",
]


def bop(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Balance of Power."""
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    destination = _out(n)
    _check(_lib.qtl_bop(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(destination), n))
    return _wrap(destination, idx, "BOP", "momentum", offset)


def cci(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Commodity Channel Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_cci(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"CCI_{period}", "momentum", offset)


def macd(close: ArrayLike, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Moving Average Convergence Divergence."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    destination = _out(n)
    _check(_lib.qtl_macd(_ptr(src), _ptr(destination), n, fastPeriod, slowPeriod))
    return _wrap(destination, idx, f"MACD_{fastPeriod}", "momentum", offset)


def pmo(close: ArrayLike, timePeriods: int = 14, smoothPeriods: int = 14, signalPeriods: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Price Momentum Oscillator."""
    timePeriods = int(timePeriods)
    smoothPeriods = int(smoothPeriods)
    signalPeriods = int(signalPeriods)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pmo(_ptr(src), _ptr(output), n, timePeriods, smoothPeriods, signalPeriods))
    return _wrap(output, idx, f"PMO_{timePeriods}", "momentum", offset)


def ppo(close: ArrayLike, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Percentage Price Oscillator."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    destination = _out(n)
    _check(_lib.qtl_ppo(_ptr(src), _ptr(destination), n, fastPeriod, slowPeriod))
    return _wrap(destination, idx, f"PPO_{fastPeriod}", "momentum", offset)


def rs(x: ArrayLike, y: ArrayLike, smoothPeriod: int = 5, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Price Relative Strength."""
    smoothPeriod = int(smoothPeriod)
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_rs(_ptr(xarr), _ptr(yarr), _ptr(output), n, smoothPeriod))
    return _wrap(output, idx, f"RS_{smoothPeriod}", "momentum", offset)


def rocp(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rate of Change (Fractional). Matches TA-Lib's ROCP (0.05 = 5%)."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rocp(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ROCP_{period}", "momentum", offset)


def rocr(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rate of Change (Ratio)."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rocr(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ROCR_{period}", "momentum", offset)


def sam(close: ArrayLike, alpha: float = 0.07, cutoff: int = 8, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Simple Alpha Momentum."""
    alpha = float(alpha)
    cutoff = int(cutoff)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_sam(_ptr(src), _ptr(output), n, alpha, cutoff))
    return _wrap(output, idx, "SAM", "momentum", offset)


def vel(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Velocity."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vel(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"VEL_{period}", "momentum", offset)

def rsi(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Relative Strength Index."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rsi(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"RSI_{period}", "momentum", offset)


def roc(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rate of Change (Percentage). Matches TA-Lib's ROC (5.0 = 5%)."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_roc(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"ROC_{period}", "momentum", offset)


def mom(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Momentum."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_mom(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"MOM_{period}", "momentum", offset)


def cmo(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Chande Momentum Oscillator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cmo(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CMO_{period}", "momentum", offset)


def tsi(close: ArrayLike, long_period: int = 25, short_period: int = 13,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """True Strength Index."""
    long_period = int(long_period); short_period = int(short_period); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tsi(_ptr(src), n, _ptr(dst), long_period, short_period))
    return _wrap(dst, idx, f"TSI_{long_period}_{short_period}", "momentum", offset)


def apo(close: ArrayLike, fast: int = 12, slow: int = 26,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Absolute Price Oscillator."""
    fast = int(fast); slow = int(slow); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_apo(_ptr(src), n, _ptr(dst), fast, slow))
    return _wrap(dst, idx, f"APO_{fast}_{slow}", "momentum", offset)


def bias(close: ArrayLike, period: int = 26, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Bias."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bias(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"BIAS_{period}", "momentum", offset)


def cfo(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Chande Forecast Oscillator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cfo(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CFO_{period}", "momentum", offset)


def cfb(close: ArrayLike, lengths: list | None = None,
        offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Composite Fractal Behavior."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    if lengths:
        arr_t = (ctypes.c_int * len(lengths))(*lengths)
        _check(_lib.qtl_cfb(_ptr(src), n, _ptr(dst), arr_t, len(lengths)))
    else:
        _check(_lib.qtl_cfb(_ptr(src), n, _ptr(dst), None, 0))
    return _wrap(dst, idx, "CFB", "momentum", offset)


def asi(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike,
        limit: float = 3.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Accumulative Swing Index."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_asi(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(dst), float(limit)))
    return _wrap(dst, idx, "ASI", "momentum", int(offset))


def vwmacd(close: ArrayLike, volume: ArrayLike, fastPeriod: int = 12,
           slowPeriod: int = 26, signalPeriod: int = 9,
           offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume-Weighted MACD -> (vwmacd, signal, histogram) or DataFrame."""
    fastPeriod = int(kwargs.get("fast", fastPeriod))
    slowPeriod = int(kwargs.get("slow", slowPeriod))
    signalPeriod = int(kwargs.get("signal", signalPeriod))
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c)
    d_vwmacd = _out(n); d_signal = _out(n); d_hist = _out(n)
    _check(_lib.qtl_vwmacd(
        _ptr(c), _ptr(v), n,
        _ptr(d_vwmacd), _ptr(d_signal), _ptr(d_hist),
        fastPeriod, slowPeriod, signalPeriod))
    return _wrap_multi(
        {f"VWMACD_{fastPeriod}_{slowPeriod}": d_vwmacd,
         f"VWMACDs_{signalPeriod}": d_signal,
         f"VWMACDh_{fastPeriod}_{slowPeriod}": d_hist},
        idx, "momentum", offset)
