"""quantalib trends_iir indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "adxvma",
    "frama",
    "holt",
    "htit",
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


def adxvma(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """ADX Variable Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_adxvma(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"ADXVMA_{period}", "trends_iir", offset)


def frama(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Fractal Adaptive Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_frama(_ptr(h), _ptr(l), _ptr(output), n, period))
    return _wrap(output, idx, f"FRAMA_{period}", "trends_iir", offset)


def holt(close: object, period: int = 14, gamma: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Holt Exponential Smoothing."""
    period = int(kwargs.get("length", period))
    gamma = float(gamma)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_holt(_ptr(src), _ptr(output), n, period, gamma))
    return _wrap(output, idx, f"HOLT_{period}", "trends_iir", offset)


def htit(close: object, offset: int = 0, **kwargs) -> object:
    """Hilbert Transform Instantaneous Trendline."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_htit(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HTIT", "trends_iir", offset)


def hwma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Holt-Winter Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hwma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"HWMA_{period}", "trends_iir", offset)


def jma(close: object, period: int = 14, phase: int = 0, offset: int = 0, **kwargs) -> object:
    """Jurik Moving Average."""
    period = int(kwargs.get("length", period))
    phase = int(phase)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jma(_ptr(src), _ptr(output), n, period, phase))
    return _wrap(output, idx, f"JMA_{period}", "trends_iir", offset)


def kama(close: object, period: int = 14, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs) -> object:
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


def ltma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Low-Lag Triple Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ltma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"LTMA_{period}", "trends_iir", offset)


def mama(close: object, fastLimit: float = 0.5, slowLimit: float = 0.05, offset: int = 0, **kwargs) -> object:
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


def mavp(x: object, periods: object, minPeriod: int = 6, maxPeriod: int = 48, offset: int = 0, **kwargs) -> object:
    """Moving Average Variable Period."""
    minPeriod = int(minPeriod)
    maxPeriod = int(maxPeriod)
    offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(periods)
    n = len(xarr)
    output = _out(n)
    _check(_lib.qtl_mavp(_ptr(xarr), _ptr(yarr), _ptr(output), n, minPeriod, maxPeriod))
    return _wrap(output, idx, f"MAVP_{minPeriod}", "trends_iir", offset)


def mcnma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """McNicholl Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mcnma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MCNMA_{period}", "trends_iir", offset)


def mgdi(close: object, period: int = 14, k: float = 0.6, offset: int = 0, **kwargs) -> object:
    """McGinley Dynamic."""
    period = int(kwargs.get("length", period))
    k = float(k)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mgdi(_ptr(src), _ptr(output), n, period, k))
    return _wrap(output, idx, f"MGDI_{period}", "trends_iir", offset)


def mma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Modified Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"MMA_{period}", "trends_iir", offset)


def nma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Normalized Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_nma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"NMA_{period}", "trends_iir", offset)


def qema(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Quadruple EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_qema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"QEMA_{period}", "trends_iir", offset)


def rema(close: object, period: int = 14, lam: float = 0.5, offset: int = 0, **kwargs) -> object:
    """Regularized EMA."""
    period = int(kwargs.get("length", period))
    lam = float(lam)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rema(_ptr(src), _ptr(output), n, period, lam))
    return _wrap(output, idx, f"REMA_{period}", "trends_iir", offset)


def rgma(close: object, period: int = 14, passes: int = 3, offset: int = 0, **kwargs) -> object:
    """Recursive Gaussian Moving Average."""
    period = int(kwargs.get("length", period))
    passes = int(passes)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rgma(_ptr(src), _ptr(output), n, period, passes))
    return _wrap(output, idx, f"RGMA_{period}", "trends_iir", offset)


def rma(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Rolling Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rma(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"RMA_{period}", "trends_iir", offset)


def t3(close: object, period: int = 14, vfactor: float = 0.7, offset: int = 0, **kwargs) -> object:
    """Tillson T3."""
    period = int(kwargs.get("length", period))
    vfactor = float(vfactor)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_t3(_ptr(src), _ptr(output), n, period, vfactor))
    return _wrap(output, idx, f"T3_{period}", "trends_iir", offset)


def trama(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Triangular Adaptive Moving Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_trama(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"TRAMA_{period}", "trends_iir", offset)


def vama(open: object, high: object, low: object, close: object, volume: object, baseLength: int = 20, shortAtrPeriod: int = 14, longAtrPeriod: int = 50, minLength: int = 5, maxLength: int = 50, offset: int = 0, **kwargs) -> object:
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


def vidya(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Variable Index Dynamic Average."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vidya(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"VIDYA_{period}", "trends_iir", offset)


def yzvama(open: object, high: object, low: object, close: object, volume: object, yzvShortPeriod: int = 10, yzvLongPeriod: int = 100, percentileLookback: int = 252, minLength: int = 5, maxLength: int = 50, offset: int = 0, **kwargs) -> object:
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


def zldema(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Zero-Lag Double EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_zldema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ZLDEMA_{period}", "trends_iir", offset)


def zlema(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Zero-Lag EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_zlema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ZLEMA_{period}", "trends_iir", offset)


def zltema(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Zero-Lag Triple EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_zltema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"ZLTEMA_{period}", "trends_iir", offset)

def ema(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Exponential Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"EMA_{period}", "trends_iir", offset)


def ema_alpha(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """EMA with explicit alpha."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"EMA_a{alpha:.4f}", "trends_iir", offset)


def dema(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Double Exponential Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DEMA_{period}", "trends_iir", offset)


def dema_alpha(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """DEMA with explicit alpha."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"DEMA_a{alpha:.4f}", "trends_iir", offset)


def tema(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Triple Exponential Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TEMA_{period}", "trends_iir", offset)


def lema(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Laguerre-based EMA."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"LEMA_{period}", "trends_iir", offset)


def hema(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Henderson EMA."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hema(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"HEMA_{period}", "trends_iir", offset)


def ahrens(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Ahrens Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ahrens(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"AHRENS_{period}", "trends_iir", offset)


def decycler(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Simple Decycler."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_decycler(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DECYCLER_{period}", "trends_iir", offset)


def dsma(close: object, period: int = 10, factor: float = 0.5,
         offset: int = 0, **kwargs) -> object:
    """Deviation-Scaled Moving Average."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dsma(_ptr(src), n, _ptr(dst), period, float(factor)))
    return _wrap(dst, idx, f"DSMA_{period}", "trends_iir", offset)


def gdema(close: object, period: int = 10, vfactor: float = 1.0,
          offset: int = 0, **kwargs) -> object:
    """Generalized DEMA."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_gdema(_ptr(src), n, _ptr(dst), period, float(vfactor)))
    return _wrap(dst, idx, f"GDEMA_{period}", "trends_iir", offset)


def coral(close: object, period: int = 10, friction: float = 0.4,
          offset: int = 0, **kwargs) -> object:
    """CORAL Trend."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_coral(_ptr(src), n, _ptr(dst), period, float(friction)))
    return _wrap(dst, idx, f"CORAL_{period}", "trends_iir", offset)


def agc(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """Automatic Gain Control."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_agc(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"AGC_a{alpha:.4f}", "trends_iir", offset)


def ccyc(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """Cyber Cycle."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ccyc(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"CCYC_a{alpha:.4f}", "trends_iir", offset)
