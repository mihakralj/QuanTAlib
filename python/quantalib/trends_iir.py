"""quantalib trends_iir indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from typing import Any

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib, ArrayLike


__all__ = [
    "adxvma",
    "frama",
    "holt",
    "ht_trendline",
    "hwma",
    "jma",
    "kama",
    "ltma",
    "mama",
    "mavp",
    "mcnma",
    "mgdi",
    "mma",
    "nma",
    "qema",
    "rema",
    "rgma",
    "rma",
    "t3",
    "trama",
    "vama",
    "vidya",
    "yzvama",
    "zldema",
    "zlema",
    "zltema",
    "ema",
    "ema_alpha",
    "dema",
    "dema_alpha",
    "tema",
    "lema",
    "hema",
    "ahrens",
    "decycler",
    "dsma",
    "gdema",
    "coral",
    "agc",
    "ccyc",
]


def adxvma(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """ADX Variable Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_adxvma(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"ADXVMA_{period}", "trends_iir", offset)


def frama(high: ArrayLike, low: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Fractal Adaptive Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_frama(_ptr(h), _ptr(l), _ptr(output), n, period))
    return _wrap(output, idx, f"FRAMA_{period}", "trends_iir", offset)


def holt(close: ArrayLike, period: int = 14, gamma: float = 0.0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Holt Exponential Smoothing."""
    period = int(kwargs.get("length", period))
    gamma = float(gamma)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_holt(_ptr(src), _ptr(output), n, period, gamma))
    return _wrap(output, idx, f"HOLT_{period}", "trends_iir", offset)


def ht_trendline(close: ArrayLike, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Hilbert Transform Instantaneous Trendline."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_httrendline(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HT_TRENDLINE", "trends_iir", offset)


def hwma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Holt-Winter Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hwma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HWMA_{period}", "trends_iir", offset)


def jma(close: ArrayLike, period: int = 14, phase: int = 0, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Jurik Moving Average."""
    period = int(kwargs.get("length", period))
    phase = int(phase)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jma(_ptr(src), _ptr(output), n, period, phase))
    return _wrap(output, idx, f"JMA_{period}", "trends_iir", offset)


def kama(close: ArrayLike, period: int = 14, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Kaufman Adaptive Moving Average."""
    period = int(kwargs.get("length", period))
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_kama(_ptr(src), _ptr(output), n, period, fastPeriod, slowPeriod))
    return _wrap(output, idx, f"KAMA_{period}", "trends_iir", offset)


def ltma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Low-Lag Triple Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ltma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"LTMA_{period}", "trends_iir", offset)


def mama(close: ArrayLike, fastLimit: float = 0.5, slowLimit: float = 0.05, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """MESA Adaptive Moving Average."""
    fastLimit = float(fastLimit)
    slowLimit = float(slowLimit)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    famaOutput = _out(n)
    _check(_lib.qtl_mama(_ptr(src), _ptr(output), fastLimit, n, slowLimit, _ptr(famaOutput)))
    return _wrap_multi({"output": output, "famaOutput": famaOutput}, idx, "trends_iir", offset)


def mavp(x: ArrayLike, periods: ArrayLike, minPeriod: int = 6, maxPeriod: int = 48, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Moving Average Variable Period."""
    minPeriod = int(minPeriod)
    maxPeriod = int(maxPeriod)
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(periods)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_mavp(_ptr(xarr), _ptr(yarr), _ptr(output), n, minPeriod, maxPeriod))
    return _wrap(output, idx, f"MAVP_{minPeriod}", "trends_iir", offset)


def mcnma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """McNicholl Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mcnma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MCNMA_{period}", "trends_iir", offset)


def mgdi(close: ArrayLike, period: int = 14, k: float = 0.6, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """McGinley Dynamic."""
    period = int(kwargs.get("length", period))
    k = float(k)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mgdi(_ptr(src), _ptr(output), n, period, k))
    return _wrap(output, idx, f"MGDI_{period}", "trends_iir", offset)


def mma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Modified Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MMA_{period}", "trends_iir", offset)


def nma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Normalized Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"NMA_{period}", "trends_iir", offset)


def qema(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Quadruple EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_qema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"QEMA_{period}", "trends_iir", offset)


def rema(close: ArrayLike, period: int = 14, lam: float = 0.5, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Regularized EMA."""
    period = int(kwargs.get("length", period))
    lam = float(lam)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rema(_ptr(src), _ptr(output), n, period, lam))
    return _wrap(output, idx, f"REMA_{period}", "trends_iir", offset)


def rgma(close: ArrayLike, period: int = 14, passes: int = 3, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Recursive Gaussian Moving Average."""
    period = int(kwargs.get("length", period))
    passes = int(passes)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rgma(_ptr(src), _ptr(output), n, period, passes))
    return _wrap(output, idx, f"RGMA_{period}", "trends_iir", offset)


def rma(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Rolling Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"RMA_{period}", "trends_iir", offset)


def t3(close: ArrayLike, period: int = 14, vfactor: float = 0.7, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Tillson T3."""
    period = int(kwargs.get("length", period))
    vfactor = float(vfactor)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_t3(_ptr(src), _ptr(output), n, period, vfactor))
    return _wrap(output, idx, f"T3_{period}", "trends_iir", offset)


def trama(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Triangular Adaptive Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_trama(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"TRAMA_{period}", "trends_iir", offset)


def vama(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, baseLength: int = 20, shortAtrPeriod: int = 14, longAtrPeriod: int = 50, minLength: int = 5, maxLength: int = 50, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Volume Adjusted Moving Average."""
    baseLength = int(baseLength)
    shortAtrPeriod = int(shortAtrPeriod)
    longAtrPeriod = int(longAtrPeriod)
    minLength = int(minLength)
    maxLength = int(maxLength)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_vama(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), baseLength, shortAtrPeriod, longAtrPeriod, minLength, maxLength, n, _ptr(dst)))
    return _wrap(dst, idx, f"VAMA_{baseLength}", "trends_iir", offset)


def vidya(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Variable Index Dynamic Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vidya(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"VIDYA_{period}", "trends_iir", offset)


def yzvama(open: ArrayLike, high: ArrayLike, low: ArrayLike, close: ArrayLike, volume: ArrayLike, yzvShortPeriod: int = 10, yzvLongPeriod: int = 100, percentileLookback: int = 252, minLength: int = 5, maxLength: int = 50, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Yang Zhang Volatility Adaptive MA."""
    yzvShortPeriod = int(yzvShortPeriod)
    yzvLongPeriod = int(yzvLongPeriod)
    percentileLookback = int(percentileLookback)
    minLength = int(minLength)
    maxLength = int(maxLength)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_yzvama(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), yzvShortPeriod, yzvLongPeriod, percentileLookback, minLength, maxLength, n, _ptr(dst)))
    return _wrap(dst, idx, f"YZVAMA_{yzvShortPeriod}", "trends_iir", offset)


def zldema(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Zero-Lag Double EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_zldema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ZLDEMA_{period}", "trends_iir", offset)


def zlema(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Zero-Lag EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_zlema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ZLEMA_{period}", "trends_iir", offset)


def zltema(close: ArrayLike, period: int = 14, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Zero-Lag Triple EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_zltema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ZLTEMA_{period}", "trends_iir", offset)

def ema(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Exponential Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"EMA_{period}", "trends_iir", offset)


def ema_alpha(close: ArrayLike, alpha: float = 0.1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """EMA with explicit alpha."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"EMA_a{alpha:.4f}", "trends_iir", offset)


def dema(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Double Exponential Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DEMA_{period}", "trends_iir", offset)


def dema_alpha(close: ArrayLike, alpha: float = 0.1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """DEMA with explicit alpha."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"DEMA_a{alpha:.4f}", "trends_iir", offset)


def tema(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Triple Exponential Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TEMA_{period}", "trends_iir", offset)


def lema(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Laguerre-based EMA."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"LEMA_{period}", "trends_iir", offset)


def hema(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Henderson EMA."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"HEMA_{period}", "trends_iir", offset)


def ahrens(close: ArrayLike, period: int = 10, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Ahrens Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ahrens(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"AHRENS_{period}", "trends_iir", offset)


def decycler(close: ArrayLike, period: int = 20, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Simple Decycler."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_decycler(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DECYCLER_{period}", "trends_iir", offset)


def dsma(close: ArrayLike, period: int = 10, factor: float = 0.5,
         offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Deviation-Scaled Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dsma(_ptr(src), n, _ptr(dst), period, float(factor)))
    return _wrap(dst, idx, f"DSMA_{period}", "trends_iir", offset)


def gdema(close: ArrayLike, period: int = 10, vfactor: float = 1.0,
          offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Generalized DEMA."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_gdema(_ptr(src), n, _ptr(dst), period, float(vfactor)))
    return _wrap(dst, idx, f"GDEMA_{period}", "trends_iir", offset)


def coral(close: ArrayLike, period: int = 10, friction: float = 0.4,
          offset: int = 0, **kwargs: Any) -> ArrayLike:
    """CORAL Trend."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_coral(_ptr(src), n, _ptr(dst), period, float(friction)))
    return _wrap(dst, idx, f"CORAL_{period}", "trends_iir", offset)


def agc(close: ArrayLike, alpha: float = 0.1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Automatic Gain Control."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_agc(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"AGC_a{alpha:.4f}", "trends_iir", offset)


def ccyc(close: ArrayLike, alpha: float = 0.1, offset: int = 0, **kwargs: Any) -> ArrayLike:
    """Cyber Cycle."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ccyc(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"CCYC_a{alpha:.4f}", "trends_iir", offset)
