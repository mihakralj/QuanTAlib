"""quantalib oscillators indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "ac",
    "ao",
    "bbs",
    "bw_mfi",
    "coppock",
    "eri",
    "fi",
    "gator",
    "imi",
    "kdj",
    "kst",
    "marketfi",
    "mstoch",
    "pgo",
    "qqe",
    "reverseema",
    "rvgi",
    "rrsi",
    "smi",
    "squeeze",
    "stc",
    "stoch",
    "stochf",
    "stochrsi",
    "ttm_wave",
    "ultosc",
    "willr",
    "fisher",
    "fisher04",
    "dpo",
    "trix",
    "inertia",
    "rsx",
    "er",
    "cti",
    "reflex",
    "trendflex",
    "kri",
    "psl",
    "deco",
    "dosc",
    "dso",
    "dymi",
    "crsi",
    "bbb",
    "bbi",
    "dem",
    "brar",
]


def ac(high: object, low: object, fastPeriod: int = 12, slowPeriod: int = 26, acPeriod: int = 5, offset: int = 0, **kwargs) -> object:
    """Accelerator Oscillator."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    acPeriod = int(acPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_ac(_ptr(h), _ptr(l), _ptr(destination), n, fastPeriod, slowPeriod, acPeriod))
    return _wrap(destination, idx, f"AC_{fastPeriod}", "oscillators", offset)


def ao(high: object, low: object, fastPeriod: int = 12, slowPeriod: int = 26, offset: int = 0, **kwargs) -> object:
    """Awesome Oscillator."""
    fastPeriod = int(fastPeriod)
    slowPeriod = int(slowPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_ao(_ptr(h), _ptr(l), _ptr(destination), n, fastPeriod, slowPeriod))
    return _wrap(destination, idx, f"AO_{fastPeriod}", "oscillators", offset)


def bbs(high: object, low: object, close: object, bbPeriod: int = 20, bbMult: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Bollinger Band Squeeze."""
    bbPeriod = int(bbPeriod)
    bbMult = float(bbMult)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_bbs(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, bbPeriod, bbMult))
    return _wrap(output, idx, f"BBS_{bbPeriod}", "oscillators", offset)


def coppock(close: object, longRoc: int = 14, shortRoc: int = 11, wmaPeriod: int = 10, offset: int = 0, **kwargs) -> object:
    """Coppock Curve."""
    longRoc = int(longRoc)
    shortRoc = int(shortRoc)
    wmaPeriod = int(wmaPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_coppock(_ptr(src), _ptr(output), n, longRoc, shortRoc, wmaPeriod))
    return _wrap(output, idx, f"COPPOCK_{wmaPeriod}", "oscillators", offset)


def eri(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Elder Ray Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_eri(_ptr(src), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"ERI_{period}", "oscillators", offset)


def fi(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Force Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_fi(_ptr(src), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"FI_{period}", "oscillators", offset)


def gator(close: object, jawPeriod: int = 13, jawShift: int = 8, teethPeriod: int = 8, teethShift: int = 5, lipsPeriod: int = 5, lipsShift: int = 3, offset: int = 0, **kwargs) -> object:
    """Gator Oscillator."""
    jawPeriod = int(jawPeriod)
    jawShift = int(jawShift)
    teethPeriod = int(teethPeriod)
    teethShift = int(teethShift)
    lipsPeriod = int(lipsPeriod)
    lipsShift = int(lipsShift)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_gator(_ptr(src), _ptr(output), n, jawPeriod, jawShift, teethPeriod, teethShift, lipsPeriod, lipsShift))
    return _wrap(output, idx, f"GATOR_{jawPeriod}", "oscillators", offset)


def imi(open: object, high: object, low: object, close: object, volume: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Intraday Momentum Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    c, _ = _arr(close); v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(_lib.qtl_imi(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"IMI_{period}", "oscillators", offset)


def kdj(high: object, low: object, close: object, period: int = 14, signal: int = 3, offset: int = 0, **kwargs) -> object:
    """KDJ Indicator."""
    period = int(kwargs.get("length", period))
    signal = int(signal)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    kOut = _out(n)
    dOut = _out(n)
    jOut = _out(n)
    _check(_lib.qtl_kdj(_ptr(h), _ptr(l), _ptr(c), _ptr(kOut), _ptr(dOut), _ptr(jOut), n, period, signal))
    return _wrap_multi({"kOut": kOut, "dOut": dOut, "jOut": jOut}, idx, "oscillators", offset)


def kst(close: object, r1: int = 10, r2: int = 15, r3: int = 20, r4: int = 30, s1: int = 10, s2: int = 10, s3: int = 10, s4: int = 15, sigPeriod: int = 9, offset: int = 0, **kwargs) -> object:
    """Know Sure Thing."""
    r1 = int(r1)
    r2 = int(r2)
    r3 = int(r3)
    r4 = int(r4)
    s1 = int(s1)
    s2 = int(s2)
    s3 = int(s3)
    s4 = int(s4)
    sigPeriod = int(sigPeriod)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    kstOut = _out(n)
    sigOut = _out(n)
    _check(_lib.qtl_kst(_ptr(src), _ptr(kstOut), _ptr(sigOut), n, r1, r2, r3, r4, s1, s2, s3, s4, sigPeriod))
    return _wrap_multi({"kstOut": kstOut, "sigOut": sigOut}, idx, "oscillators", offset)


def marketfi(high: object, low: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Market Facilitation Index."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); v, _ = _arr(volume)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_marketfi(_ptr(h), _ptr(l), _ptr(v), _ptr(output), n))
    return _wrap(output, idx, "MARKETFI", "oscillators", offset)


def bw_mfi(high: object, low: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Bill Williams Market Facilitation Index with 4-zone classification."""
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); v, _ = _arr(volume)
    n = len(h)
    mfiOut = _out(n)
    zoneOut = _out(n)
    _check(_lib.qtl_bwmfi(_ptr(h), _ptr(l), _ptr(v), _ptr(mfiOut), _ptr(zoneOut), n))
    return _wrap_multi({"mfiOut": mfiOut, "zoneOut": zoneOut}, idx, "oscillators", offset)


def dstoch(high: object, low: object, close: object, period: int = 21, offset: int = 0, **kwargs) -> object:
    """Double Stochastic (Bressert DSS)."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_dstoch(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period))
    return _wrap(output, idx, f"DSTOCH_{period}", "oscillators", offset)


def mstoch(close: object, stochLength: int = 20, hpLength: int = 48, ssLength: int = 10, offset: int = 0, **kwargs) -> object:
    """Modified Stochastic."""
    stochLength = int(stochLength)
    hpLength = int(hpLength)
    ssLength = int(ssLength)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_mstoch(_ptr(src), _ptr(output), n, stochLength, hpLength, ssLength))
    return _wrap(output, idx, f"MSTOCH_{stochLength}", "oscillators", offset)


def pgo(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Pretty Good Oscillator."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    destination = _out(n)
    _check(_lib.qtl_pgo(_ptr(h), _ptr(l), _ptr(c), _ptr(destination), n, period))
    return _wrap(destination, idx, f"PGO_{period}", "oscillators", offset)


def qqe(close: object, rsiPeriod: int = 14, smoothFactor: int = 5, qqeFactor: float = 4.236, offset: int = 0, **kwargs) -> object:
    """Quantitative Qualitative Estimation."""
    rsiPeriod = int(rsiPeriod)
    smoothFactor = int(smoothFactor)
    qqeFactor = float(qqeFactor)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_qqe(_ptr(src), _ptr(output), n, rsiPeriod, smoothFactor, qqeFactor))
    return _wrap(output, idx, f"QQE_{rsiPeriod}", "oscillators", offset)


def reverseema(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Reverse EMA."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_reverseema(_ptr(src), _ptr(output), n, period))
    return _wrap(output, idx, f"REVERSEEMA_{period}", "oscillators", offset)


def rvgi(open: object, high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Vigor Index."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o)
    rvgiOutput = _out(n)
    signalOutput = _out(n)
    _check(_lib.qtl_rvgi(_ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(rvgiOutput), _ptr(signalOutput), n, period))
    return _wrap_multi({"rvgiOutput": rvgiOutput, "signalOutput": signalOutput}, idx, "oscillators", offset)


def rrsi(close: object, smoothLength: int = 10, rsiLength: int = 10, offset: int = 0, **kwargs) -> object:
    """Rocket RSI (Ehlers) — Fisher Transform of Super Smoother–filtered RSI."""
    src = _to_np(close)
    n = len(src)
    out = _np.empty(n, dtype=_np.float64)
    _bridge._check(_bridge._lib.qtl_rrsi(
        src.ctypes.data_as(_bridge._dp), n,
        out.ctypes.data_as(_bridge._dp), smoothLength, rsiLength))
    return _shift(out, offset)


def smi(high: object, low: object, close: object, kPeriod: int = 14, kSmooth: int = 3, dSmooth: int = 3, blau: int = 3, offset: int = 0, **kwargs) -> object:
    """Stochastic Momentum Index."""
    kPeriod = int(kPeriod)
    kSmooth = int(kSmooth)
    dSmooth = int(dSmooth)
    blau = int(blau)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    kOut = _out(n)
    dOut = _out(n)
    _check(_lib.qtl_smi(_ptr(h), _ptr(l), _ptr(c), _ptr(kOut), _ptr(dOut), n, kPeriod, kSmooth, dSmooth, blau))
    return _wrap_multi({"kOut": kOut, "dOut": dOut}, idx, "oscillators", offset)


def squeeze(high: object, low: object, close: object, period: int = 14, bbMult: float = 2.0, kcMult: float = 1.5, offset: int = 0, **kwargs) -> object:
    """Squeeze Momentum."""
    period = int(kwargs.get("length", period))
    bbMult = float(bbMult)
    kcMult = float(kcMult)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    momOut = _out(n)
    sqOut = _out(n)
    _check(_lib.qtl_squeeze(_ptr(h), _ptr(l), _ptr(c), _ptr(momOut), _ptr(sqOut), n, period, bbMult, kcMult))
    return _wrap_multi({"momOut": momOut, "sqOut": sqOut}, idx, "oscillators", offset)


def squeeze_pro(high: object, low: object, close: object, period: int = 20, bbMult: float = 2.0, kcMultWide: float = 2.0, kcMultNormal: float = 1.5, kcMultNarrow: float = 1.0, momLength: int = 12, momSmooth: int = 6, useSma: bool = True, offset: int = 0, **kwargs) -> object:
    """Squeeze Pro (LazyBear enhanced TTM Squeeze with 3 KC widths)."""
    period = int(kwargs.get("length", period))
    bbMult = float(bbMult)
    kcMultWide = float(kcMultWide)
    kcMultNormal = float(kcMultNormal)
    kcMultNarrow = float(kcMultNarrow)
    momLength = int(momLength)
    momSmooth = int(momSmooth)
    useSmaInt = int(bool(useSma))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    momOut = _out(n)
    sqOut = _out(n)
    _check(_lib.qtl_squeeze_pro(_ptr(h), _ptr(l), _ptr(c), _ptr(momOut), _ptr(sqOut), n, period, bbMult, kcMultWide, kcMultNormal, kcMultNarrow, momLength, momSmooth, useSmaInt))
    return _wrap_multi({"momOut": momOut, "sqOut": sqOut}, idx, "oscillators", offset)


def stc(close: object, kPeriod: int = 14, dPeriod: int = 3, fastLength: int = 23, slowLength: int = 50, smoothing: int = 10, offset: int = 0, **kwargs) -> object:
    """Schaff Trend Cycle."""
    kPeriod = int(kPeriod)
    dPeriod = int(dPeriod)
    fastLength = int(fastLength)
    slowLength = int(slowLength)
    smoothing = int(smoothing)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_stc(_ptr(src), _ptr(output), n, kPeriod, dPeriod, fastLength, slowLength, smoothing))
    return _wrap(output, idx, f"STC_{kPeriod}", "oscillators", offset)


def stoch(high: object, low: object, close: object, kLength: int = 14, dPeriod: int = 3, offset: int = 0, **kwargs) -> object:
    """Stochastic Oscillator."""
    kLength = int(kLength)
    dPeriod = int(dPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    kOut = _out(n)
    dOut = _out(n)
    _check(_lib.qtl_stoch(_ptr(h), _ptr(l), _ptr(c), _ptr(kOut), _ptr(dOut), n, kLength, dPeriod))
    return _wrap_multi({"kOut": kOut, "dOut": dOut}, idx, "oscillators", offset)


def stochf(high: object, low: object, close: object, kLength: int = 14, dPeriod: int = 3, offset: int = 0, **kwargs) -> object:
    """Fast Stochastic."""
    kLength = int(kLength)
    dPeriod = int(dPeriod)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    kOut = _out(n)
    dOut = _out(n)
    _check(_lib.qtl_stochf(_ptr(h), _ptr(l), _ptr(c), _ptr(kOut), _ptr(dOut), n, kLength, dPeriod))
    return _wrap_multi({"kOut": kOut, "dOut": dOut}, idx, "oscillators", offset)


def stochrsi(close: object, rsiLength: int = 14, stochLength: int = 14, kSmooth: int = 3, dSmooth: int = 3, offset: int = 0, **kwargs) -> object:
    """Stochastic RSI."""
    rsiLength = int(rsiLength)
    stochLength = int(stochLength)
    kSmooth = int(kSmooth)
    dSmooth = int(dSmooth)
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    output = _out(n)
    _check(_lib.qtl_stochrsi(_ptr(src), _ptr(output), n, rsiLength, stochLength, kSmooth, dSmooth))
    return _wrap(output, idx, f"STOCHRSI_{rsiLength}", "oscillators", offset)


def ttm_wave(close: object, offset: int = 0, **kwargs) -> object:
    """TTM Wave."""
    offset = int(offset)
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(_lib.qtl_ttmwave(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "TTM_WAVE", "oscillators", offset)


def ultosc(high: object, low: object, close: object, period1: int = 14, period2: int = 14, period3: int = 14, offset: int = 0, **kwargs) -> object:
    """Ultimate Oscillator."""
    period1 = int(period1)
    period2 = int(period2)
    period3 = int(period3)
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_ultosc(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period1, period2, period3))
    return _wrap(output, idx, f"ULTOSC_{period1}", "oscillators", offset)


def willr(high: object, low: object, close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Williams %R."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    output = _out(n)
    _check(_lib.qtl_willr(_ptr(h), _ptr(l), _ptr(c), _ptr(output), n, period))
    return _wrap(output, idx, f"WILLR_{period}", "oscillators", offset)

def fisher(close: object, period: int = 9, offset: int = 0, **kwargs) -> object:
    """Fisher Transform."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_fisher(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"FISHER_{period}", "oscillators", offset)


def fisher04(close: object, period: int = 9, offset: int = 0, **kwargs) -> object:
    """Fisher Transform (0.4 variant)."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_fisher04(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"FISHER04_{period}", "oscillators", offset)


def dpo(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Detrended Price Oscillator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dpo(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DPO_{period}", "oscillators", offset)


def trix(close: object, period: int = 18, offset: int = 0, **kwargs) -> object:
    """Triple EMA Rate of Change."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trix(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TRIX_{period}", "oscillators", offset)


def inertia(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Inertia."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_inertia(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"INERTIA_{period}", "oscillators", offset)


def rsx(close: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Strength Xtra."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rsx(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"RSX_{period}", "oscillators", offset)


def er(close: object, period: int = 10, offset: int = 0, **kwargs) -> object:
    """Efficiency Ratio."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_er(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"ER_{period}", "oscillators", offset)


def cti(close: object, period: int = 12, offset: int = 0, **kwargs) -> object:
    """Correlation Trend Indicator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cti(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"CTI_{period}", "oscillators", offset)


def reflex(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Reflex."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_reflex(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"REFLEX_{period}", "oscillators", offset)


def trendflex(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Trendflex."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trendflex(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"TRENDFLEX_{period}", "oscillators", offset)


def kri(close: object, period: int = 20, offset: int = 0, **kwargs) -> object:
    """Kairi Relative Index."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_kri(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"KRI_{period}", "oscillators", offset)


def psl(close: object, period: int = 12, offset: int = 0, **kwargs) -> object:
    """Psychological Line."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_psl(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"PSL_{period}", "oscillators", offset)


def deco(close: object, short_period: int = 30, long_period: int = 60,
         offset: int = 0, **kwargs) -> object:
    """DECO."""
    short_period = int(short_period); long_period = int(long_period); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_deco(_ptr(src), n, _ptr(dst), short_period, long_period))
    return _wrap(dst, idx, f"DECO_{short_period}_{long_period}", "oscillators", offset)


def dosc(close: object, rsi_period: int = 14, ema1_period: int = 5,
         ema2_period: int = 3, signal_period: int = 9,
         offset: int = 0, **kwargs) -> object:
    """DeMarker Oscillator."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dosc(_ptr(src), n, _ptr(dst),
                         int(rsi_period), int(ema1_period), int(ema2_period), int(signal_period)))
    return _wrap(dst, idx, f"DOSC_{rsi_period}", "oscillators", offset)


def dso(close: object, period: int = 40, offset: int = 0, **kwargs) -> object:
    """Ehlers Deviation-Scaled Oscillator."""
    period = int(kwargs.get("length", period)); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dso(_ptr(src), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DSO_{period}", "oscillators", offset)


def dymi(close: object, base_period: int = 14, short_period: int = 5,
         long_period: int = 10, min_period: int = 3, max_period: int = 30,
         offset: int = 0, **kwargs) -> object:
    """Dynamic Momentum Index."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dymi(_ptr(src), n, _ptr(dst),
                         int(base_period), int(short_period), int(long_period),
                         int(min_period), int(max_period)))
    return _wrap(dst, idx, "DYMI", "oscillators", offset)


def crsi(close: object, rsi_period: int = 3, streak_period: int = 2,
         rank_period: int = 100, offset: int = 0, **kwargs) -> object:
    """Connors RSI."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_crsi(_ptr(src), n, _ptr(dst),
                         int(rsi_period), int(streak_period), int(rank_period)))
    return _wrap(dst, idx, f"CRSI_{rsi_period}", "oscillators", offset)


def bbb(close: object, period: int = 20, mult: float = 2.0,
        offset: int = 0, **kwargs) -> object:
    """Bollinger Band Bounce."""
    period = int(kwargs.get("length", period)); mult = float(mult); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbb(_ptr(src), n, _ptr(dst), period, mult))
    return _wrap(dst, idx, f"BBB_{period}", "oscillators", offset)


def bbi(close: object, p1: int = 3, p2: int = 6, p3: int = 12, p4: int = 24,
        offset: int = 0, **kwargs) -> object:
    """Bull Bear Index."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbi(_ptr(src), n, _ptr(dst), int(p1), int(p2), int(p3), int(p4)))
    return _wrap(dst, idx, "BBI", "oscillators", offset)


def dem(high: object, low: object, period: int = 14,
        offset: int = 0, **kwargs) -> object:
    """DeMarker."""
    period = int(kwargs.get("length", period))
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_dem(_ptr(h), _ptr(l), n, _ptr(dst), period))
    return _wrap(dst, idx, f"DEM_{period}", "oscillators", int(offset))


def brar(open: object, high: object, low: object, close: object,
         length: int = 26, offset: int = 0, **kwargs) -> object:
    """Bull-Bear Ratio (BRAR)."""
    length = int(length); offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); br = _out(n); ar = _out(n)
    _check(_lib.qtl_brar(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(br), _ptr(ar), length))
    return _wrap_multi({f"BR_{length}": br, f"AR_{length}": ar}, idx, "oscillators", offset)
