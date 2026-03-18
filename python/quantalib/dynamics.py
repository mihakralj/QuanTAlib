"""quantalib dynamics indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "adx",
    "adxr",
    "alligator",
    "amat",
    "aroon",
    "aroonosc",
    "chop",
    "dmh",
    "dmx",
    "dx",
    "minus_di",
    "minus_dm",
    "ghla",
    "ht_trendmode",
    "ichimoku",
    "impulse",
    "pfe",
    "plus_di",
    "plus_dm",
    "qstick",
    "ravi",
    "supertrend",
    "ttm_squeeze",
    "ttm_trend",
    "vhf",
    "vortex",
]


def adx(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Average Directional Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_adx(_ptr(h), _ptr(l), _ptr(c), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"ADX_{period}", "dynamics", offset)


def adxr(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """ADX Rating."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_adxr(_ptr(h), _ptr(l), _ptr(c), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"ADXR_{period}", "dynamics", offset)


def alligator(open: object, high: object, low: object, close: object, volume: object, jawPeriod: int = 13, jawOffset: int = 8, teethPeriod: int = 8, teethOffset: int = 5, lipsPeriod: int = 5, lipsOffset: int = 3, offset: int = 0, **kwargs) -> object:
    """Williams Alligator."""
    jawPeriod = int(jawPeriod)
    jawOffset = int(jawOffset)
    teethPeriod = int(teethPeriod)
    teethOffset = int(teethOffset)
    lipsPeriod = int(lipsPeriod)
    lipsOffset = int(lipsOffset)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_alligator(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), jawPeriod, jawOffset, teethPeriod, teethOffset, lipsPeriod, lipsOffset, n, _ptr(dst)))
    return _wrap(dst, idx, f"ALLIGATOR_{jawPeriod}", "dynamics", offset)


def amat(close: object, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs) -> object:
    """Archer Moving Average Trends."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    trend = _out(n)
    strength = _out(n)
    _check(_lib.qtl_amat(_ptr(src), _ptr(trend), _ptr(strength), n, fastPeriod, slowPeriod))
    return _wrap_multi({"trend": trend, "strength": strength}, idx, "dynamics", offset)


def aroon(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Aroon."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_aroon(_ptr(h), _ptr(l), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"AROON_{period}", "dynamics", offset)


def aroonosc(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Aroon Oscillator."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_aroonosc(_ptr(h), _ptr(l), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"AROONOSC_{period}", "dynamics", offset)


def chop(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Choppiness Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_chop(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"CHOP_{period}", "dynamics", offset)


def dmh(high: object, low: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Ehlers Directional Movement with Hann Windowing."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    dst = _out(n)
    _check(_lib.qtl_dmh(_ptr(h), _ptr(l), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"DMH_{period}", "dynamics", offset)


def dmx(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Directional Movement Extended."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_dmx(_ptr(h), _ptr(l), _ptr(c), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"DMX_{period}", "dynamics", offset)


def dx(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Directional Movement Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_dx(_ptr(h), _ptr(l), _ptr(c), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"DX_{period}", "dynamics", offset)


def minus_di(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Minus Directional Indicator."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_minusdi(_ptr(h), _ptr(h), _ptr(l), _ptr(c), _ptr(destination), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"MINUS_DI_{period}", "dynamics", offset)


def minus_dm(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Minus Directional Movement."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_minusdm(_ptr(h), _ptr(h), _ptr(l), _ptr(c), _ptr(destination), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"MINUS_DM_{period}", "dynamics", offset)


def ghla(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Gann Hi-Lo Activator."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_ghla(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period))
    return _wrap(output, idx, f"GHLA_{period}", "dynamics", offset)


def ht_trendmode(close: object, offset: int = 0, **kwargs) -> object:
    """Hilbert Transform Trend Mode."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_httrendmode(_ptr(src), _ptr(output), n))
    return _wrap(output, idx, "HT_TRENDMODE", "dynamics", offset)


def ichimoku(open: object, high: object, low: object, close: object, volume: object, tenkanPeriod: int = 9, kijunPeriod: int = 26, senkouBPeriod: int = 52, displacement: int = 26, offset: int = 0, **kwargs) -> object:
    """Ichimoku Cloud."""
    tenkanPeriod = int(tenkanPeriod)
    kijunPeriod = int(kijunPeriod)
    senkouBPeriod = int(senkouBPeriod)
    displacement = int(displacement)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dstTenkan = _out(n)
    dstKijun = _out(n)
    dstSenkouA = _out(n)
    dstSenkouB = _out(n)
    dstChikou = _out(n)
    _check(_lib.qtl_ichimoku(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), tenkanPeriod, kijunPeriod, senkouBPeriod, displacement, n, _ptr(dstTenkan), _ptr(dstKijun), _ptr(dstSenkouA), _ptr(dstSenkouB), _ptr(dstChikou)))
    return _wrap_multi({"dstTenkan": dstTenkan, "dstKijun": dstKijun, "dstSenkouA": dstSenkouA, "dstSenkouB": dstSenkouB, "dstChikou": dstChikou}, idx, "dynamics", offset)


def impulse(close: object, emaPeriod: int = 13, macdFast: int = 12, macdSlow: int = 26, macdSignal: int = 9, offset: int = 0, **kwargs) -> object:
    """Elder Impulse System."""
    emaPeriod = int(emaPeriod)
    macdFast = int(macdFast)
    macdSlow = int(macdSlow)
    macdSignal = int(macdSignal)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_impulse(_ptr(src), emaPeriod, macdFast, macdSlow, macdSignal, n, _ptr(dst)))
    return _wrap(dst, idx, f"IMPULSE_{emaPeriod}", "dynamics", offset)


def pfe(close: object, period: int = 14, smoothPeriod: int = 5, offset: int = 0, **kwargs) -> object:
    """Polarized Fractal Efficiency."""
    period = int(kwargs.get("length", period))
    smoothPeriod = int(smoothPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pfe(_ptr(src), _ptr(output), n, period, smoothPeriod))
    return _wrap(output, idx, f"PFE_{period}", "dynamics", offset)


def plus_di(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Plus Directional Indicator."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_plusdi(_ptr(h), _ptr(h), _ptr(l), _ptr(c), _ptr(destination), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"PLUS_DI_{period}", "dynamics", offset)


def plus_dm(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Plus Directional Movement."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_plusdm(_ptr(h), _ptr(h), _ptr(l), _ptr(c), _ptr(destination), period, n, _ptr(destination)))
    return _wrap(destination, idx, f"PLUS_DM_{period}", "dynamics", offset)


def pta(close: object, longPeriod: int = 250, shortPeriod: int = 40, offset: int = 0, **kwargs) -> object:
    """Ehlers Precision Trend Analysis."""
    longPeriod = int(kwargs.get("long_period", longPeriod))
    shortPeriod = int(kwargs.get("short_period", shortPeriod))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_pta(_ptr(src), _ptr(output), n, longPeriod, shortPeriod))
    return _wrap(output, idx, f"PTA_{longPeriod}_{shortPeriod}", "dynamics", offset)


def qstick(open: object, high: object, low: object, close: object, volume: object, period: int = 14, useEma: int = 0, offset: int = 0, **kwargs) -> object:
    """QStick."""
    period = int(kwargs.get("length", period))
    useEma = int(useEma)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_qstick(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, useEma, n, _ptr(dst)))
    return _wrap(dst, idx, f"QSTICK_{period}", "dynamics", offset)


def ravi(close: object, shortPeriod: int = 12, longPeriod: int = 26, offset: int = 0, **kwargs) -> object:
    """Range Action Verification Index."""
    shortPeriod = int(shortPeriod)
    longPeriod = int(longPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_ravi(_ptr(src), _ptr(output), n, shortPeriod, longPeriod))
    return _wrap(output, idx, f"RAVI_{shortPeriod}", "dynamics", offset)


def supertrend(open: object, high: object, low: object, close: object, volume: object, period: int = 14, multiplier: float = 2.0, offset: int = 0, **kwargs) -> object:
    """SuperTrend."""
    period = int(kwargs.get("length", period))
    multiplier = float(multiplier)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_super(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, multiplier, n, _ptr(dst)))
    return _wrap(dst, idx, f"SUPER_{period}", "dynamics", offset)


def ttm_squeeze(open: object, high: object, low: object, close: object, volume: object, bbPeriod: int = 20, bbMult: float = 2.0, kcPeriod: int = 10, kcMult: float = 1.5, momPeriod: int = 12, offset: int = 0, **kwargs) -> object:
    """TTM Squeeze."""
    bbPeriod = int(bbPeriod)
    bbMult = float(bbMult)
    kcPeriod = int(kcPeriod)
    kcMult = float(kcMult)
    momPeriod = int(momPeriod)
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_ttmsqueeze(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), bbPeriod, bbMult, kcPeriod, kcMult, momPeriod, n, _ptr(dst)))
    return _wrap(dst, idx, f"TTM_SQUEEZE_{bbPeriod}", "dynamics", offset)


def ttm_trend(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """TTM Trend."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_ttmtrend(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"TTM_TREND_{period}", "dynamics", offset)


def vhf(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Vertical Horizontal Filter."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_vhf(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"VHF_{period}", "dynamics", offset)


def vortex(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Vortex Indicator."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    viPlus = _out(n)
    viMinus = _out(n)
    _check(_lib.qtl_vortex(_ptr(h), _ptr(l), _ptr(c), period, _ptr(viPlus), n, _ptr(viMinus)))
    return _wrap_multi({"viPlus": viPlus, "viMinus": viMinus}, idx, "dynamics", offset)
