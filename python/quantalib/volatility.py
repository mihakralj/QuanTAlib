"""quantalib volatility indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "adr",
    "atr",
    "atrn",
    "gkv",
    "hlv",
    "hv",
    "jvolty",
    "jvoltyn",
    "massi",
    "natr",
    "rsv",
    "rv",
    "rvi",
    "ui",
    "vov",
    "vr",
    "yzv",
    "tr",
    "bbw",
    "bbwn",
    "bbwp",
    "stddev",
    "variance",
    "etherm",
    "ccv",
    "cv",
    "cvi",
    "ewma",
]


def adr(open: object, high: object, low: object, close: object, volume: object, period: int = 14, method: int = 0, offset: int = 0, **kwargs) -> object:
    """Average Daily Range."""
    period = int(period)
    method = int(method)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_adr(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, method, n, _ptr(dst)))
    return _wrap(dst, idx, f"ADR_{period}", "volatility", offset)


def atr(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Average True Range."""
    period = int(period)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_atr(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"ATR_{period}", "volatility", offset)


def atrn(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Normalized ATR."""
    period = int(period)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_atrn(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"ATRN_{period}", "volatility", offset)


def gkv(open: object, high: object, low: object, close: object, period: int = 14, annualize: int = 1, annualPeriods: int = 252, offset: int = 0, **kwargs) -> object:
    """Garman-Klass Volatility."""
    period = int(period)
    annualize = int(annualize)
    annualPeriods = int(annualPeriods)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_gkv(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period, annualize, annualPeriods))
    return _wrap(output, idx, f"GKV_{period}", "volatility", offset)


def hlv(high: object, low: object, period: int = 14, annualize: int = 1, annualPeriods: int = 252, offset: int = 0, **kwargs) -> object:
    """High-Low Volatility."""
    period = int(period)
    annualize = int(annualize)
    annualPeriods = int(annualPeriods)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_hlv(_ptr(h), _ptr(l), _ptr(output), n, period, annualize, annualPeriods))
    return _wrap(output, idx, f"HLV_{period}", "volatility", offset)


def hv(close: object, period: int = 14, annualize: int = 1, annualPeriods: int = 252, offset: int = 0, **kwargs) -> object:
    """Historical Volatility."""
    period = int(period)
    annualize = int(annualize)
    annualPeriods = int(annualPeriods)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_hv(_ptr(src), _ptr(output), n, period, annualize, annualPeriods))
    return _wrap(output, idx, f"HV_{period}", "volatility", offset)


def jvolty(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Jurik Volatility."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jvolty(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"JVOLTY_{period}", "volatility", offset)


def jvoltyn(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Jurik Volatility Normalized."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_jvoltyn(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"JVOLTYN_{period}", "volatility", offset)


def massi(close: object, emaLength: int = 9, sumLength: int = 25, offset: int = 0, **kwargs) -> object:
    """Mass Index."""
    emaLength = int(emaLength)
    sumLength = int(sumLength)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_massi(_ptr(src), _ptr(output), n, emaLength, sumLength))
    return _wrap(output, idx, f"MASSI_{emaLength}", "volatility", offset)


def natr(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Normalized ATR."""
    period = int(period)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_natr(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"NATR_{period}", "volatility", offset)


def rsv(open: object, high: object, low: object, close: object, period: int = 14, annualize: int = 1, annualPeriods: int = 252, offset: int = 0, **kwargs) -> object:
    """Rogers-Satchell Volatility."""
    period = int(period)
    annualize = int(annualize)
    annualPeriods = int(annualPeriods)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_rsv(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period, annualize, annualPeriods))
    return _wrap(output, idx, f"RSV_{period}", "volatility", offset)


def rv(close: object, period: int = 14, smoothingPeriod: int = 14, annualize: int = 1, annualPeriods: int = 252, offset: int = 0, **kwargs) -> object:
    """Realized Volatility."""
    period = int(period)
    smoothingPeriod = int(smoothingPeriod)
    annualize = int(annualize)
    annualPeriods = int(annualPeriods)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rv(_ptr(src), _ptr(output), n, period, smoothingPeriod, annualize, annualPeriods))
    return _wrap(output, idx, f"RV_{period}", "volatility", offset)


def rvi(close: object, stdevLength: int = 10, rmaLength: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Volatility Index."""
    stdevLength = int(stdevLength)
    rmaLength = int(rmaLength)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_rvi(_ptr(src), _ptr(output), n, stdevLength, rmaLength))
    return _wrap(output, idx, f"RVI_{stdevLength}", "volatility", offset)


def ui(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Ulcer Index."""
    period = int(period)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ui(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"UI_{period}", "volatility", offset)


def vov(close: object, volatilityPeriod: int = 20, vovPeriod: int = 20, offset: int = 0, **kwargs) -> object:
    """Volatility of Volatility."""
    volatilityPeriod = int(volatilityPeriod)
    vovPeriod = int(vovPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vov(_ptr(src), _ptr(output), n, volatilityPeriod, vovPeriod))
    return _wrap(output, idx, f"VOV_{volatilityPeriod}", "volatility", offset)


def vr(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Volatility Ratio."""
    period = int(period)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_vr(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period))
    return _wrap(output, idx, f"VR_{period}", "volatility", offset)


def yzv(open: object, high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Yang-Zhang Volatility."""
    period = int(period)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    output = _out(n)
    _check(_lib.qtl_yzv(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period))
    return _wrap(output, idx, f"YZV_{period}", "volatility", offset)

def tr(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """True Range."""
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_tr(_ptr(h), _ptr(l), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "TR", "volatility", int(offset))


def bbw(close: object, length: int = 20, mult: float = 2.0,
        offset: int = 0, **kwargs) -> object:
    """Bollinger Band Width."""
    length = int(length); mult = float(mult); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbw(_ptr(src), n, _ptr(dst), length, mult))
    return _wrap(dst, idx, f"BBW_{length}", "volatility", offset)


def bbwn(close: object, length: int = 20, mult: float = 2.0,
         lookback: int = 252, offset: int = 0, **kwargs) -> object:
    """Bollinger Band Width Normalized."""
    length = int(length); mult = float(mult); lookback = int(lookback); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbwn(_ptr(src), n, _ptr(dst), length, mult, lookback))
    return _wrap(dst, idx, f"BBWN_{length}", "volatility", offset)


def bbwp(close: object, length: int = 20, mult: float = 2.0,
         lookback: int = 252, offset: int = 0, **kwargs) -> object:
    """Bollinger Band Width Percentile."""
    length = int(length); mult = float(mult); lookback = int(lookback); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbwp(_ptr(src), n, _ptr(dst), length, mult, lookback))
    return _wrap(dst, idx, f"BBWP_{length}", "volatility", offset)


def stddev(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Standard Deviation."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_stddev(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"STDDEV_{length}", "volatility", offset)


def variance(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Variance."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_variance(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"VAR_{length}", "volatility", offset)


def etherm(high: object, low: object, length: int = 14,
           offset: int = 0, **kwargs) -> object:
    """Elder Thermometer."""
    length = int(length)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_etherm(_ptr(h), _ptr(l), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ETHERM_{length}", "volatility", int(offset))


def ccv(close: object, short_period: int = 20, long_period: int = 1,
        offset: int = 0, **kwargs) -> object:
    """Close-to-Close Volatility."""
    short_period = int(short_period); long_period = int(long_period); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ccv(_ptr(src), n, _ptr(dst), short_period, long_period))
    return _wrap(dst, idx, f"CCV_{short_period}", "volatility", offset)


def cv(close: object, length: int = 20, min_vol: float = 0.2,
       max_vol: float = 0.7, offset: int = 0, **kwargs) -> object:
    """Coefficient of Variation."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cv(_ptr(src), n, _ptr(dst), length, float(min_vol), float(max_vol)))
    return _wrap(dst, idx, f"CV_{length}", "volatility", offset)


def cvi(close: object, ema_period: int = 10, roc_period: int = 10,
        offset: int = 0, **kwargs) -> object:
    """Chaikin Volatility Index."""
    ema_period = int(ema_period); roc_period = int(roc_period); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cvi(_ptr(src), n, _ptr(dst), ema_period, roc_period))
    return _wrap(dst, idx, f"CVI_{ema_period}", "volatility", offset)


def ewma(close: object, length: int = 20, is_pop: int = 1,
         ann_factor: int = 252, offset: int = 0, **kwargs) -> object:
    """Exponentially Weighted Moving Average (volatility)."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ewma(_ptr(src), n, _ptr(dst), length, int(is_pop), int(ann_factor)))
    return _wrap(dst, idx, f"EWMA_{length}", "volatility", offset)
