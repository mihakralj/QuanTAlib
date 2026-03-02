"""quantalib channels indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "aberr",
    "accbands",
    "apchannel",
    "apz",
    "atrbands",
    "bbands",
    "dchannel",
    "decaychannel",
    "fcb",
    "jbands",
    "kchannel",
    "maenv",
    "mmchannel",
    "pchannel",
    "regchannel",
    "sdchannel",
    "starchannel",
    "stbands",
    "ttm_lrc",
    "ubands",
    "uchannel",
    "vwapbands",
    "vwapsd",
]


def aberr(close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Aberration Bands."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_abber(_ptr(src), _ptr(middle), _ptr(upper), _ptr(lower), n, period, multiplier))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def accbands(high: object, low: object, close: object, period: int = 14, factor: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Acceleration Bands."""
    period = int(kwargs.get("length", period))
    factor = float(factor)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_accbands(_ptr(h), _ptr(l), _ptr(c), _ptr(middle), _ptr(upper), _ptr(lower), n, period, factor))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def apz(open: object, high: object, low: object, close: object, volume: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Adaptive Price Zone."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dstMiddle = _out(n)
    dstUpper = _out(n)
    dstLower = _out(n)
    _check(_lib.qtl_apz(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, multiplier, n, _ptr(dstMiddle), _ptr(dstUpper), _ptr(dstLower)))
    return _wrap_multi({"dstMiddle": dstMiddle, "dstUpper": dstUpper, "dstLower": dstLower}, idx, "channels", offset)


def dchannel(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Donchian Channel."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_dchannel(_ptr(h), _ptr(l), _ptr(middle), _ptr(upper), _ptr(lower), n, period))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def decaychannel(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Decay Channel."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_decaychannel(_ptr(h), _ptr(l), _ptr(middle), _ptr(upper), _ptr(lower), n, period))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def fcb(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Fractal Chaos Bands."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_fcb(_ptr(h), _ptr(l), _ptr(middle), _ptr(upper), _ptr(lower), n, period))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def jbands(close: object, period: int = 14, phase: int = 0, offset: int = 0, **kwargs) -> object:
    """J-Line Bands."""
    period = int(kwargs.get("length", period))
    phase = int(phase)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_jbands(_ptr(src), _ptr(middle), _ptr(upper), _ptr(lower), n, period, phase))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def kchannel(high: object, low: object, close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Keltner Channel."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_kchannel(_ptr(h), _ptr(l), _ptr(c), _ptr(middle), _ptr(upper), _ptr(lower), n, period, multiplier))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def maenv(close: object, period: int = 14, percentage: float = 2.5, maType: int = 0, offset: int = 0, **kwargs) -> object:
    """Moving Average Envelope."""
    period = int(kwargs.get("length", period))
    percentage = float(percentage)
    maType = int(maType)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_maenv(_ptr(src), _ptr(middle), _ptr(upper), _ptr(lower), n, period, percentage, maType))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def mmchannel(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Min-Max Channel."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_mmchannel(_ptr(h), _ptr(l), _ptr(upper), _ptr(lower), n, period))
    return _wrap_multi({"upper": upper, "lower": lower}, idx, "channels", offset)


def pchannel(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Price Channel."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_pchannel(_ptr(h), _ptr(l), _ptr(middle), _ptr(upper), _ptr(lower), n, period))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def regchannel(close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Regression Channel."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_regchannel(_ptr(src), _ptr(middle), _ptr(upper), _ptr(lower), n, period, multiplier))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def sdchannel(close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Standard Deviation Channel."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_sdchannel(_ptr(src), _ptr(middle), _ptr(upper), _ptr(lower), n, period, multiplier))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def starchannel(high: object, low: object, close: object, period: int = 14, multiplier: float = 2.0, atrPeriod: int = 22, offset: int = 0, **kwargs) -> object:
    """Stoller Average Range Channel (STARC)."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    atrPeriod = int(atrPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    middle = _out(n)
    upper = _out(n)
    lower = _out(n)
    _check(_lib.qtl_starchannel(_ptr(h), _ptr(l), _ptr(c), _ptr(middle), _ptr(upper), _ptr(lower), n, period, multiplier, atrPeriod))
    return _wrap_multi({"middle": middle, "upper": upper, "lower": lower}, idx, "channels", offset)


def stbands(high: object, low: object, close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """SuperTrend Bands."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    upper = _out(n)
    lower = _out(n)
    trend = _out(n)
    _check(_lib.qtl_stbands(_ptr(h), _ptr(l), _ptr(c), _ptr(upper), _ptr(lower), _ptr(trend), n, period, multiplier))
    return _wrap_multi({"upper": upper, "lower": lower, "trend": trend}, idx, "channels", offset)


def ttm_lrc(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """TTM Linear Regression Channel."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    midline = _out(n)
    upper1 = _out(n)
    lower1 = _out(n)
    upper2 = _out(n)
    lower2 = _out(n)
    _check(_lib.qtl_ttmlrc(_ptr(src), _ptr(midline), _ptr(upper1), _ptr(lower1), _ptr(upper2), _ptr(lower2), n, period))
    return _wrap_multi({"midline": midline, "upper1": upper1, "lower1": lower1, "upper2": upper2, "lower2": lower2}, idx, "channels", offset)


def ubands(close: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Upper/Lower Bands."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    upper = _out(n)
    middle = _out(n)
    lower = _out(n)
    _check(_lib.qtl_ubands(_ptr(src), _ptr(upper), _ptr(middle), _ptr(lower), n, period, multiplier))
    return _wrap_multi({"upper": upper, "middle": middle, "lower": lower}, idx, "channels", offset)


def uchannel(high: object, low: object, close: object, strPeriod: int = 14, centerPeriod: int = 20, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Ulcer Channel."""
    strPeriod = int(strPeriod)
    centerPeriod = int(centerPeriod)
    multiplier = float(multiplier)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    upper = _out(n)
    middle = _out(n)
    lower = _out(n)
    _check(_lib.qtl_uchannel(_ptr(h), _ptr(l), _ptr(c), _ptr(upper), _ptr(middle), _ptr(lower), n, strPeriod, centerPeriod, multiplier))
    return _wrap_multi({"upper": upper, "middle": middle, "lower": lower}, idx, "channels", offset)


def vwapbands(price: object, volume: object, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """VWAP Bands."""
    multiplier = float(multiplier)
    offset = int(offset)
    pr, idx = _arr(price); v, _ = _arr(volume)
    n = len(pr)
    upper1 = _out(n)
    lower1 = _out(n)
    upper2 = _out(n)
    lower2 = _out(n)
    vwap = _out(n)
    stdDev = _out(n)
    _check(_lib.qtl_vwapbands(_ptr(pr), _ptr(v), _ptr(upper1), _ptr(lower1), _ptr(upper2), _ptr(lower2), _ptr(vwap), _ptr(stdDev), n, multiplier))
    return _wrap_multi({"upper1": upper1, "lower1": lower1, "upper2": upper2, "lower2": lower2, "vwap": vwap, "stdDev": stdDev}, idx, "channels", offset)


def vwapsd(price: object, volume: object, numDevs: float = 2.0, offset: int = 0, **kwargs) -> object:
    """VWAP Standard Deviation."""
    numDevs = float(numDevs)
    offset = int(offset)
    pr, idx = _arr(price); v, _ = _arr(volume)
    n = len(pr)
    upper = _out(n)
    lower = _out(n)
    vwap = _out(n)
    stdDev = _out(n)
    _check(_lib.qtl_vwapsd(_ptr(pr), _ptr(v), _ptr(upper), _ptr(lower), _ptr(vwap), _ptr(stdDev), n, numDevs))
    return _wrap_multi({"upper": upper, "lower": lower, "vwap": vwap, "stdDev": stdDev}, idx, "channels", offset)


def bbands(close: object, length: int = 20, std: float = 2.0,
           offset: int = 0, **kwargs) -> object:
    """Bollinger Bands -> (upper, mid, lower) or DataFrame."""
    length = int(length); std = float(std); offset = int(offset)
    src, idx = _arr(close); n = len(src)
    upper = _out(n); mid = _out(n); lower = _out(n)
    _check(_lib.qtl_bbands(_ptr(src), n, _ptr(upper), _ptr(mid), _ptr(lower), length, std))
    return _wrap_multi(
        {f"BBU_{length}_{std}": upper, f"BBM_{length}_{std}": mid, f"BBL_{length}_{std}": lower},
        idx, "channels", offset)


def atrbands(high: object, low: object, close: object,
             length: int = 14, mult: float = 2.0,
             offset: int = 0, **kwargs) -> object:
    """ATR Bands -> (upper, mid, lower) or DataFrame."""
    length = int(length); mult = float(mult); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    upper = _out(n); mid = _out(n); lower = _out(n)
    _check(_lib.qtl_atrbands(_ptr(h), _ptr(l), _ptr(c), n, _ptr(upper), _ptr(mid), _ptr(lower), length, mult))
    return _wrap_multi(
        {f"ATRBU_{length}_{mult}": upper, f"ATRBM_{length}_{mult}": mid, f"ATRBL_{length}_{mult}": lower},
        idx, "channels", offset)


def apchannel(high: object, low: object, length: int = 20,
              offset: int = 0, **kwargs) -> object:
    """Average Price Channel -> (upper, lower) or DataFrame."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    upper = _out(n); lower = _out(n)
    _check(_lib.qtl_apchannel(_ptr(h), _ptr(l), n, _ptr(upper), _ptr(lower), float(length)))
    return _wrap_multi({f"APCU_{length}": upper, f"APCL_{length}": lower}, idx, "channels", offset)
