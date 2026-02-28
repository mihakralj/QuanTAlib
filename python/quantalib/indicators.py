"""High-level indicator wrappers for quantalib.

Each function accepts numpy arrays (or pandas Series / DataFrame) and
returns the same type.  Multi-output indicators return a tuple of arrays
or a DataFrame depending on input type.

Signature conventions follow pandas-ta where practical:
    sma(close, length=10, offset=0, **kwargs)
"""
from __future__ import annotations

import numpy as np
from numpy.typing import NDArray

from ._bridge import _lib, _check, _dp, _ci, _cd

# Optional pandas support
try:
    import pandas as pd  # type: ignore[import-untyped]
except ImportError:  # pragma: no cover
    pd = None  # type: ignore[assignment]

# ---------------------------------------------------------------------------
#  Internal helpers
# ---------------------------------------------------------------------------
_F64 = np.float64


def _arr(x: object) -> tuple[NDArray[np.float64], object]:
    """Return (contiguous float64 array, original_index_or_None)."""
    idx = None
    if pd is not None and isinstance(x, pd.Series):
        idx = x.index
        x = x.to_numpy(dtype=_F64, copy=False)
    elif pd is not None and isinstance(x, pd.DataFrame):
        # Use first column
        idx = x.index
        x = x.iloc[:, 0].to_numpy(dtype=_F64, copy=False)
    return np.ascontiguousarray(x, dtype=_F64), idx  # type: ignore[arg-type]


def _ptr(a: NDArray[np.float64]):  # noqa: ANN202
    """Get ctypes double* from array."""
    return a.ctypes.data_as(_dp)


def _out(n: int) -> NDArray[np.float64]:
    """Allocate output array."""
    return np.empty(n, dtype=_F64)


def _offset(arr: NDArray[np.float64], off: int) -> NDArray[np.float64]:
    """Apply offset (roll + NaN fill)."""
    if off and off != 0:
        arr = np.roll(arr, off)
        if off > 0:
            arr[:off] = np.nan
        else:
            arr[off:] = np.nan
    return arr


def _wrap(
    arr: NDArray[np.float64],
    idx: object,
    name: str,
    category: str,
    offset: int = 0,
):
    """Wrap result: apply offset, optionally convert to pd.Series."""
    arr = _offset(arr, offset)
    if idx is not None and pd is not None:
        s = pd.Series(arr, index=idx, name=name)
        s.category = category
        return s
    return arr


def _wrap_multi(
    arrays: dict[str, NDArray[np.float64]],
    idx: object,
    category: str,
    offset: int = 0,
):
    """Wrap multi-output result into tuple or DataFrame."""
    for k in arrays:
        arrays[k] = _offset(arrays[k], offset)
    if idx is not None and pd is not None:
        df = pd.DataFrame(arrays, index=idx)
        df.category = category
        return df
    return tuple(arrays.values())


# ═══════════════════════════════════════════════════════════════════════════
#  Pattern A: single-input + period  (most common)
# ═══════════════════════════════════════════════════════════════════════════

def _pa(
    fn_name: str, close: object, length: int, offset: int,
    default_length: int, label: str, category: str,
) -> object:
    """Generic Pattern A wrapper."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", category, offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.1  Core
# ═══════════════════════════════════════════════════════════════════════════

def avgprice(open: object, high: object, low: object, close: object,
             offset: int = 0, **kwargs) -> object:
    """Average Price = (O+H+L+C)/4."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_avgprice(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "AVGPRICE", "core", int(offset) if offset is not None else 0)


def medprice(high: object, low: object, offset: int = 0, **kwargs) -> object:
    """Median Price = (H+L)/2."""
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_medprice(_ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "MEDPRICE", "core", int(offset) if offset is not None else 0)


def typprice(open: object, high: object, low: object,
             offset: int = 0, **kwargs) -> object:
    """Typical Price = (O+H+L)/3 (QuanTAlib variant)."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_typprice(_ptr(o), _ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "TYPPRICE", "core", int(offset) if offset is not None else 0)


def midbody(open: object, close: object, offset: int = 0, **kwargs) -> object:
    """Mid Body = (O+C)/2."""
    o, idx = _arr(open); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_midbody(_ptr(o), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "MIDBODY", "core", int(offset) if offset is not None else 0)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.2  Momentum
# ═══════════════════════════════════════════════════════════════════════════

def rsi(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Strength Index."""
    return _pa("qtl_rsi", close, length, offset, 14, "RSI", "momentum")

def roc(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Rate of Change."""
    return _pa("qtl_roc", close, length, offset, 10, "ROC", "momentum")

def mom(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Momentum."""
    return _pa("qtl_mom", close, length, offset, 10, "MOM", "momentum")

def cmo(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Chande Momentum Oscillator."""
    return _pa("qtl_cmo", close, length, offset, 14, "CMO", "momentum")

def tsi(close: object, long_period: int = 25, short_period: int = 13,
        offset: int = 0, **kwargs) -> object:
    """True Strength Index."""
    long_period = int(long_period) if long_period is not None else 25
    short_period = int(short_period) if short_period is not None else 13
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_tsi(_ptr(src), n, _ptr(dst), long_period, short_period))
    return _wrap(dst, idx, f"TSI_{long_period}_{short_period}", "momentum", offset)

def apo(close: object, fast: int = 12, slow: int = 26,
        offset: int = 0, **kwargs) -> object:
    """Absolute Price Oscillator."""
    fast = int(fast) if fast is not None else 12
    slow = int(slow) if slow is not None else 26
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_apo(_ptr(src), n, _ptr(dst), fast, slow))
    return _wrap(dst, idx, f"APO_{fast}_{slow}", "momentum", offset)

def bias(close: object, length: int = 26, offset: int = 0, **kwargs) -> object:
    """Bias."""
    return _pa("qtl_bias", close, length, offset, 26, "BIAS", "momentum")

def cfo(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Chande Forecast Oscillator."""
    return _pa("qtl_cfo", close, length, offset, 14, "CFO", "momentum")

def cfb(close: object, lengths: list[int] | None = None,
        offset: int = 0, **kwargs) -> object:
    """Composite Fractal Behavior."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    import ctypes
    if lengths:
        arr_t = (ctypes.c_int * len(lengths))(*lengths)
        _check(_lib.qtl_cfb(_ptr(src), n, _ptr(dst), arr_t, len(lengths)))
    else:
        _check(_lib.qtl_cfb(_ptr(src), n, _ptr(dst), None, 0))
    return _wrap(dst, idx, "CFB", "momentum", offset)

def asi(open: object, high: object, low: object, close: object,
        limit: float = 0.0, offset: int = 0, **kwargs) -> object:
    """Accumulative Swing Index."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_asi(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(dst), float(limit)))
    return _wrap(dst, idx, "ASI", "momentum", int(offset) if offset is not None else 0)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.3  Oscillators
# ═══════════════════════════════════════════════════════════════════════════

def fisher(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Fisher Transform."""
    return _pa("qtl_fisher", close, length, offset, 9, "FISHER", "oscillator")

def fisher04(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Fisher Transform (0.4 variant)."""
    return _pa("qtl_fisher04", close, length, offset, 9, "FISHER04", "oscillator")

def dpo(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Detrended Price Oscillator."""
    return _pa("qtl_dpo", close, length, offset, 20, "DPO", "oscillator")

def trix(close: object, length: int = 18, offset: int = 0, **kwargs) -> object:
    """Triple EMA Rate of Change."""
    return _pa("qtl_trix", close, length, offset, 18, "TRIX", "oscillator")

def inertia(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Inertia."""
    return _pa("qtl_inertia", close, length, offset, 20, "INERTIA", "oscillator")

def rsx(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Strength Xtra."""
    return _pa("qtl_rsx", close, length, offset, 14, "RSX", "oscillator")

def er(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Efficiency Ratio."""
    return _pa("qtl_er", close, length, offset, 10, "ER", "oscillator")

def cti(close: object, length: int = 12, offset: int = 0, **kwargs) -> object:
    """Correlation Trend Indicator."""
    return _pa("qtl_cti", close, length, offset, 12, "CTI", "oscillator")

def reflex(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Reflex."""
    return _pa("qtl_reflex", close, length, offset, 20, "REFLEX", "oscillator")

def trendflex(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Trendflex."""
    return _pa("qtl_trendflex", close, length, offset, 20, "TRENDFLEX", "oscillator")

def kri(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Kairi Relative Index."""
    return _pa("qtl_kri", close, length, offset, 20, "KRI", "oscillator")

def psl(close: object, length: int = 12, offset: int = 0, **kwargs) -> object:
    """Psychological Line."""
    return _pa("qtl_psl", close, length, offset, 12, "PSL", "oscillator")

def deco(close: object, short_period: int = 30, long_period: int = 60,
         offset: int = 0, **kwargs) -> object:
    """DECO."""
    short_period = int(short_period) if short_period is not None else 30
    long_period = int(long_period) if long_period is not None else 60
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_deco(_ptr(src), n, _ptr(dst), short_period, long_period))
    return _wrap(dst, idx, f"DECO_{short_period}_{long_period}", "oscillator", offset)

def dosc(close: object, rsi_period: int = 14, ema1_period: int = 5,
         ema2_period: int = 3, signal_period: int = 9,
         offset: int = 0, **kwargs) -> object:
    """DeMarker Oscillator."""
    rsi_period = int(rsi_period) if rsi_period is not None else 14
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_dosc(_ptr(src), n, _ptr(dst),
                         rsi_period, int(ema1_period), int(ema2_period), int(signal_period)))
    return _wrap(dst, idx, f"DOSC_{rsi_period}", "oscillator", offset)

def dymoi(close: object, base_period: int = 14, short_period: int = 5,
          long_period: int = 10, min_period: int = 3, max_period: int = 30,
          offset: int = 0, **kwargs) -> object:
    """Dynamic Momentum Index."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_dymoi(_ptr(src), n, _ptr(dst),
                          int(base_period), int(short_period), int(long_period),
                          int(min_period), int(max_period)))
    return _wrap(dst, idx, "DYMOI", "oscillator", offset)

def crsi(close: object, rsi_period: int = 3, streak_period: int = 2,
         rank_period: int = 100, offset: int = 0, **kwargs) -> object:
    """Connors RSI."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_crsi(_ptr(src), n, _ptr(dst),
                         int(rsi_period), int(streak_period), int(rank_period)))
    return _wrap(dst, idx, f"CRSI_{rsi_period}", "oscillator", offset)

def bbb(close: object, length: int = 20, mult: float = 2.0,
        offset: int = 0, **kwargs) -> object:
    """Bollinger Band Bounce."""
    length = int(length) if length is not None else 20
    mult = float(mult) if mult is not None else 2.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_bbb(_ptr(src), n, _ptr(dst), length, mult))
    return _wrap(dst, idx, f"BBB_{length}", "oscillator", offset)

def bbi(close: object, p1: int = 3, p2: int = 6, p3: int = 12, p4: int = 24,
        offset: int = 0, **kwargs) -> object:
    """Bull Bear Index."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_bbi(_ptr(src), n, _ptr(dst), int(p1), int(p2), int(p3), int(p4)))
    return _wrap(dst, idx, "BBI", "oscillator", offset)

def dem(high: object, low: object, offset: int = 0, **kwargs) -> object:
    """DeMarker."""
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_dem(_ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "DEM", "oscillator", int(offset) if offset is not None else 0)

def brar(open: object, high: object, low: object, close: object,
         length: int = 26, offset: int = 0, **kwargs) -> object:
    """Bull-Bear Ratio (BRAR)."""
    length = int(length) if length is not None else 26
    offset = int(offset) if offset is not None else 0
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); br = _out(n); ar = _out(n)
    _check(_lib.qtl_brar(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(br), _ptr(ar), length))
    return _wrap_multi(
        {f"BR_{length}": br, f"AR_{length}": ar},
        idx, "oscillator", offset,
    )


# ═══════════════════════════════════════════════════════════════════════════
#  §8.4  Trends — FIR
# ═══════════════════════════════════════════════════════════════════════════

def sma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Simple Moving Average."""
    return _pa("qtl_sma", close, length, offset, 10, "SMA", "trend")

def wma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Weighted Moving Average."""
    return _pa("qtl_wma", close, length, offset, 10, "WMA", "trend")

def hma(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Hull Moving Average."""
    return _pa("qtl_hma", close, length, offset, 9, "HMA", "trend")

def trima(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Triangular Moving Average."""
    return _pa("qtl_trima", close, length, offset, 10, "TRIMA", "trend")

def swma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Symmetric Weighted Moving Average."""
    return _pa("qtl_swma", close, length, offset, 10, "SWMA", "trend")

def dwma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Double Weighted Moving Average."""
    return _pa("qtl_dwma", close, length, offset, 10, "DWMA", "trend")

def blma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Blackman Moving Average."""
    return _pa("qtl_blma", close, length, offset, 10, "BLMA", "trend")

def alma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Arnaud Legoux Moving Average."""
    return _pa("qtl_alma", close, length, offset, 10, "ALMA", "trend")

def lsma(close: object, length: int = 25, offset: int = 0, **kwargs) -> object:
    """Least Squares Moving Average."""
    return _pa("qtl_lsma", close, length, offset, 25, "LSMA", "trend")

def sgma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Savitzky-Golay Moving Average."""
    return _pa("qtl_sgma", close, length, offset, 10, "SGMA", "trend")

def sinema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Sine-weighted Moving Average."""
    return _pa("qtl_sinema", close, length, offset, 10, "SINEMA", "trend")

def hanma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Hann-weighted Moving Average."""
    return _pa("qtl_hanma", close, length, offset, 10, "HANMA", "trend")

def parzen(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Parzen-weighted Moving Average."""
    return _pa("qtl_parzen", close, length, offset, 10, "PARZEN", "trend")

def tsf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Time Series Forecast."""
    return _pa("qtl_tsf", close, length, offset, 14, "TSF", "trend")

def conv(close: object, kernel: list[float] | NDArray[np.float64] | None = None,
         offset: int = 0, **kwargs) -> object:
    """Convolution with custom kernel."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    if kernel is None:
        kernel = [1.0]
    k = np.ascontiguousarray(kernel, dtype=_F64)
    _check(_lib.qtl_conv(_ptr(src), n, _ptr(dst), _ptr(k), len(k)))
    return _wrap(dst, idx, "CONV", "trend", offset)

def bwma(close: object, length: int = 10, order: int = 0,
         offset: int = 0, **kwargs) -> object:
    """Butterworth-weighted Moving Average."""
    length = int(length) if length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_bwma(_ptr(src), n, _ptr(dst), length, int(order)))
    return _wrap(dst, idx, f"BWMA_{length}", "trend", offset)

def crma(close: object, length: int = 10, volume_factor: float = 1.0,
         offset: int = 0, **kwargs) -> object:
    """Cosine-Ramp Moving Average."""
    length = int(length) if length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_crma(_ptr(src), n, _ptr(dst), length, float(volume_factor)))
    return _wrap(dst, idx, f"CRMA_{length}", "trend", offset)

def sp15(close: object, length: int = 15, offset: int = 0, **kwargs) -> object:
    """SP-15 Moving Average."""
    return _pa("qtl_sp15", close, length, offset, 15, "SP15", "trend")

def tukey_w(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Tukey-windowed Moving Average."""
    return _pa("qtl_tukey_w", close, length, offset, 10, "TUKEY", "trend")

def rain(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """RAIN Moving Average."""
    return _pa("qtl_rain", close, length, offset, 10, "RAIN", "trend")

def afirma(close: object, length: int = 10, window_type: int = 0,
           use_simd: bool = False, offset: int = 0, **kwargs) -> object:
    """Adaptive FIR Moving Average."""
    length = int(length) if length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_afirma(_ptr(src), n, _ptr(dst), length, int(window_type), int(use_simd)))
    return _wrap(dst, idx, f"AFIRMA_{length}", "trend", offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.5  Trends — IIR
# ═══════════════════════════════════════════════════════════════════════════

def ema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Exponential Moving Average."""
    return _pa("qtl_ema", close, length, offset, 10, "EMA", "trend")

def ema_alpha(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """EMA with explicit alpha."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_ema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"EMA_a{alpha:.4f}", "trend", offset)

def dema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Double Exponential Moving Average."""
    return _pa("qtl_dema", close, length, offset, 10, "DEMA", "trend")

def dema_alpha(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """DEMA with explicit alpha."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_dema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"DEMA_a{alpha:.4f}", "trend", offset)

def tema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Triple Exponential Moving Average."""
    return _pa("qtl_tema", close, length, offset, 10, "TEMA", "trend")

def lema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Laguerre-based EMA."""
    return _pa("qtl_lema", close, length, offset, 10, "LEMA", "trend")

def hema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Henderson EMA."""
    return _pa("qtl_hema", close, length, offset, 10, "HEMA", "trend")

def ahrens(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Ahrens Moving Average."""
    return _pa("qtl_ahrens", close, length, offset, 10, "AHRENS", "trend")

def decycler(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Simple Decycler."""
    return _pa("qtl_decycler", close, length, offset, 20, "DECYCLER", "trend")

def dsma(close: object, length: int = 10, factor: float = 0.5,
         offset: int = 0, **kwargs) -> object:
    """Deviation-Scaled Moving Average."""
    length = int(length) if length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_dsma(_ptr(src), n, _ptr(dst), length, float(factor)))
    return _wrap(dst, idx, f"DSMA_{length}", "trend", offset)

def gdema(close: object, length: int = 10, vfactor: float = 1.0,
          offset: int = 0, **kwargs) -> object:
    """Generalized DEMA."""
    length = int(length) if length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_gdema(_ptr(src), n, _ptr(dst), length, float(vfactor)))
    return _wrap(dst, idx, f"GDEMA_{length}", "trend", offset)

def coral(close: object, length: int = 10, friction: float = 0.4,
          offset: int = 0, **kwargs) -> object:
    """CORAL Trend."""
    length = int(length) if length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_coral(_ptr(src), n, _ptr(dst), length, float(friction)))
    return _wrap(dst, idx, f"CORAL_{length}", "trend", offset)

def agc(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """Automatic Gain Control."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_agc(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"AGC_a{alpha:.4f}", "trend", offset)

def ccyc(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """Cyber Cycle."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_ccyc(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"CCYC_a{alpha:.4f}", "trend", offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.6  Channels
# ═══════════════════════════════════════════════════════════════════════════

def bbands(close: object, length: int = 20, std: float = 2.0,
           offset: int = 0, **kwargs) -> object:
    """Bollinger Bands → (upper, mid, lower) or DataFrame."""
    length = int(length) if length is not None else 20
    std = float(std) if std is not None else 2.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src)
    upper = _out(n); mid = _out(n); lower = _out(n)
    _check(_lib.qtl_bbands(
        _ptr(src), n, _ptr(upper), _ptr(mid), _ptr(lower), length, std,
    ))
    return _wrap_multi(
        {
            f"BBU_{length}_{std}": upper,
            f"BBM_{length}_{std}": mid,
            f"BBL_{length}_{std}": lower,
        },
        idx, "channels", offset,
    )


def aberr(close: object, length: int = 20, mult: float = 2.0,
          offset: int = 0, **kwargs) -> object:
    """Aberration Bands → (upper, mid, lower) or DataFrame."""
    length = int(length) if length is not None else 20
    mult = float(mult) if mult is not None else 2.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src)
    upper = _out(n); mid = _out(n); lower = _out(n)
    _check(_lib.qtl_abber(
        _ptr(src), _ptr(mid), _ptr(upper), _ptr(lower), n, length, mult,
    ))
    return _wrap_multi(
        {
            f"ABERRU_{length}_{mult}": upper,
            f"ABERRM_{length}_{mult}": mid,
            f"ABERRL_{length}_{mult}": lower,
        },
        idx, "channels", offset,
    )

def atrbands(high: object, low: object, close: object,
             length: int = 14, mult: float = 2.0,
             offset: int = 0, **kwargs) -> object:
    """ATR Bands → (upper, mid, lower) or DataFrame."""
    length = int(length) if length is not None else 14
    mult = float(mult) if mult is not None else 2.0
    offset = int(offset) if offset is not None else 0
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h)
    upper = _out(n); mid = _out(n); lower = _out(n)
    _check(_lib.qtl_atrbands(
        _ptr(h), _ptr(l), _ptr(c), n, _ptr(upper), _ptr(mid), _ptr(lower), length, mult,
    ))
    return _wrap_multi(
        {
            f"ATRBU_{length}_{mult}": upper,
            f"ATRBM_{length}_{mult}": mid,
            f"ATRBL_{length}_{mult}": lower,
        },
        idx, "channels", offset,
    )


def apchannel(high: object, low: object, length: int = 20,
              offset: int = 0, **kwargs) -> object:
    """Average Price Channel → (upper, lower) or DataFrame."""
    length = int(length) if length is not None else 20
    offset = int(offset) if offset is not None else 0
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h)
    upper = _out(n); lower = _out(n)
    _check(_lib.qtl_apchannel(_ptr(h), _ptr(l), n, _ptr(upper), _ptr(lower), length))
    return _wrap_multi(
        {f"APCU_{length}": upper, f"APCL_{length}": lower},
        idx, "channels", offset,
    )


# ═══════════════════════════════════════════════════════════════════════════
#  §8.7  Volatility
# ═══════════════════════════════════════════════════════════════════════════

def tr(high: object, low: object, close: object, offset: int = 0, **kwargs) -> object:
    """True Range."""
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_tr(_ptr(h), _ptr(l), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "TR", "volatility", int(offset) if offset is not None else 0)

def bbw(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Bollinger Band Width."""
    return _pa("qtl_bbw", close, length, offset, 20, "BBW", "volatility")

def bbwn(close: object, length: int = 20, mult: float = 2.0,
         lookback: int = 252, offset: int = 0, **kwargs) -> object:
    """Bollinger Band Width Normalized."""
    length = int(length) if length is not None else 20
    mult = float(mult) if mult is not None else 2.0
    lookback = int(lookback) if lookback is not None else 252
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_bbwn(_ptr(src), n, _ptr(dst), length, mult, lookback))
    return _wrap(dst, idx, f"BBWN_{length}", "volatility", offset)

def bbwp(close: object, length: int = 20, mult: float = 2.0,
         lookback: int = 252, offset: int = 0, **kwargs) -> object:
    """Bollinger Band Width Percentile."""
    length = int(length) if length is not None else 20
    mult = float(mult) if mult is not None else 2.0
    lookback = int(lookback) if lookback is not None else 252
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_bbwp(_ptr(src), n, _ptr(dst), length, mult, lookback))
    return _wrap(dst, idx, f"BBWP_{length}", "volatility", offset)

def stddev(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Standard Deviation."""
    return _pa("qtl_stddev", close, length, offset, 20, "STDDEV", "volatility")

def variance(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Variance."""
    return _pa("qtl_variance", close, length, offset, 20, "VARIANCE", "volatility")

def etherm(high: object, low: object, offset: int = 0, **kwargs) -> object:
    """Elder Thermometer."""
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_etherm(_ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "ETHERM", "volatility", int(offset) if offset is not None else 0)

def ccv(close: object, short_period: int = 20, long_period: int = 1,
        offset: int = 0, **kwargs) -> object:
    """Close-to-Close Volatility."""
    short_period = int(short_period) if short_period is not None else 20
    long_period = int(long_period) if long_period is not None else 1
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_ccv(_ptr(src), n, _ptr(dst), short_period, long_period))
    return _wrap(dst, idx, f"CCV_{short_period}", "volatility", offset)

def cv(close: object, length: int = 20, min_vol: float = 0.2,
       max_vol: float = 0.7, offset: int = 0, **kwargs) -> object:
    """Coefficient of Variation."""
    length = int(length) if length is not None else 20
    min_vol = float(min_vol) if min_vol is not None else 0.2
    max_vol = float(max_vol) if max_vol is not None else 0.7
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_cv(_ptr(src), n, _ptr(dst), length, min_vol, max_vol))
    return _wrap(dst, idx, f"CV_{length}", "volatility", offset)

def cvi(close: object, ema_period: int = 10, roc_period: int = 10,
        offset: int = 0, **kwargs) -> object:
    """Chaikin Volatility Index."""
    ema_period = int(ema_period) if ema_period is not None else 10
    roc_period = int(roc_period) if roc_period is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_cvi(_ptr(src), n, _ptr(dst), ema_period, roc_period))
    return _wrap(dst, idx, f"CVI_{ema_period}", "volatility", offset)

def ewma(close: object, length: int = 20, is_pop: int = 1,
         ann_factor: int = 252, offset: int = 0, **kwargs) -> object:
    """Exponentially Weighted Moving Average (volatility)."""
    length = int(length) if length is not None else 20
    is_pop = int(is_pop) if is_pop is not None else 1
    ann_factor = int(ann_factor) if ann_factor is not None else 252
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_ewma(_ptr(src), n, _ptr(dst), length, is_pop, ann_factor))
    return _wrap(dst, idx, f"EWMA_{length}", "volatility", offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.8  Volume
# ═══════════════════════════════════════════════════════════════════════════

def _pg(fn_name: str, close: object, volume: object,
        offset: int, label: str) -> object:
    """Pattern G (source+volume, no period)."""
    offset = int(offset) if offset is not None else 0
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, label, "volume", offset)


def _pg2(fn_name: str, close: object, volume: object, length: int,
         offset: int, default_length: int, label: str) -> object:
    """Pattern G2 (source+volume+period)."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", "volume", offset)


def obv(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """On-Balance Volume."""
    return _pg("qtl_obv", close, volume, offset, "OBV")

def pvt(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Price Volume Trend."""
    return _pg("qtl_pvt", close, volume, offset, "PVT")

def pvr(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Price Volume Rank."""
    return _pg("qtl_pvr", close, volume, offset, "PVR")

def vf(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Volume Flow."""
    return _pg("qtl_vf", close, volume, offset, "VF")

def nvi(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Negative Volume Index."""
    return _pg("qtl_nvi", close, volume, offset, "NVI")

def pvi(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Positive Volume Index."""
    return _pg("qtl_pvi", close, volume, offset, "PVI")

def tvi(close: object, volume: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """Trade Volume Index."""
    return _pg2("qtl_tvi", close, volume, length, offset, 14, "TVI")

def pvd(close: object, volume: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """Price Volume Divergence."""
    return _pg2("qtl_pvd", close, volume, length, offset, 14, "PVD")

def vwma(close: object, volume: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Volume Weighted Moving Average."""
    return _pg2("qtl_vwma", close, volume, length, offset, 20, "VWMA")

def evwma(close: object, volume: object, length: int = 20,
          offset: int = 0, **kwargs) -> object:
    """Elastic Volume Weighted Moving Average."""
    return _pg2("qtl_evwma", close, volume, length, offset, 20, "EVWMA")

def efi(close: object, volume: object, length: int = 13,
        offset: int = 0, **kwargs) -> object:
    """Elder's Force Index."""
    return _pg2("qtl_efi", close, volume, length, offset, 13, "EFI")

def aobv(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Archer OBV → (obv, signal) or DataFrame."""
    offset = int(offset) if offset is not None else 0
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); obv_out = _out(n); sig = _out(n)
    _check(_lib.qtl_aobv(_ptr(c), _ptr(v), n, _ptr(obv_out), _ptr(sig)))
    return _wrap_multi({"AOBV": obv_out, "AOBV_SIG": sig}, idx, "volume", offset)

def mfi(high: object, low: object, close: object, volume: object,
        length: int = 14, offset: int = 0, **kwargs) -> object:
    """Money Flow Index."""
    length = int(length) if length is not None else 14
    offset = int(offset) if offset is not None else 0
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_mfi(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MFI_{length}", "volume", offset)

def cmf(high: object, low: object, close: object, volume: object,
        length: int = 20, offset: int = 0, **kwargs) -> object:
    """Chaikin Money Flow."""
    length = int(length) if length is not None else 20
    offset = int(offset) if offset is not None else 0
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_cmf(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CMF_{length}", "volume", offset)

def eom(high: object, low: object, volume: object,
        length: int = 14, offset: int = 0, **kwargs) -> object:
    """Ease of Movement."""
    length = int(length) if length is not None else 14
    offset = int(offset) if offset is not None else 0
    h, idx = _arr(high); l, _ = _arr(low); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_eom(_ptr(h), _ptr(l), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EOM_{length}", "volume", offset)

def pvo(volume: object, fast: int = 12, slow: int = 26, signal: int = 9,
        offset: int = 0, **kwargs) -> object:
    """Percentage Volume Oscillator → (pvo, signal, histogram) or DataFrame."""
    fast = int(fast) if fast is not None else 12
    slow = int(slow) if slow is not None else 26
    signal = int(signal) if signal is not None else 9
    offset = int(offset) if offset is not None else 0
    v, idx = _arr(volume)
    n = len(v); pvo_out = _out(n); sig = _out(n); hist = _out(n)
    _check(_lib.qtl_pvo(
        _ptr(v), n, _ptr(pvo_out), _ptr(sig), _ptr(hist), fast, slow, signal,
    ))
    return _wrap_multi(
        {
            f"PVO_{fast}_{slow}_{signal}": pvo_out,
            f"PVOs_{fast}_{slow}_{signal}": sig,
            f"PVOh_{fast}_{slow}_{signal}": hist,
        },
        idx, "volume", offset,
    )


# ═══════════════════════════════════════════════════════════════════════════
#  §8.9  Statistics
# ═══════════════════════════════════════════════════════════════════════════

def zscore(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Z-Score."""
    return _pa("qtl_zscore", close, length, offset, 20, "ZSCORE", "statistics")

def cma(close: object, offset: int = 0, **kwargs) -> object:
    """Cumulative Moving Average."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_cma(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "CMA", "statistics", offset)

def entropy(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Shannon Entropy."""
    return _pa("qtl_entropy", close, length, offset, 10, "ENTROPY", "statistics")

def correlation(x: object, y: object, length: int = 20,
                offset: int = 0, **kwargs) -> object:
    """Pearson Correlation."""
    length = int(length) if length is not None else 20
    offset = int(offset) if offset is not None else 0
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_correlation(_ptr(xarr), _ptr(yarr), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CORR_{length}", "statistics", offset)

def covariance(x: object, y: object, length: int = 20,
               is_sample: bool = True, offset: int = 0, **kwargs) -> object:
    """Covariance."""
    length = int(length) if length is not None else 20
    offset = int(offset) if offset is not None else 0
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_covariance(
        _ptr(xarr), _ptr(yarr), n, _ptr(dst), length, int(is_sample),
    ))
    return _wrap(dst, idx, f"COV_{length}", "statistics", offset)

def cointegration(x: object, y: object, length: int = 20,
                  offset: int = 0, **kwargs) -> object:
    """Cointegration."""
    length = int(length) if length is not None else 20
    offset = int(offset) if offset is not None else 0
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_cointegration(_ptr(xarr), _ptr(yarr), n, _ptr(dst), length))
    return _wrap(dst, idx, f"COINT_{length}", "statistics", offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.10  Errors
# ═══════════════════════════════════════════════════════════════════════════

def _pf(fn_name: str, actual: object, predicted: object,
        length: int, offset: int, default_length: int, label: str) -> object:
    """Pattern F (actual+predicted+period)."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", "errors", offset)

def mse(actual: object, predicted: object, length: int = 20,
        offset: int = 0, **kwargs) -> object:
    """Mean Squared Error."""
    return _pf("qtl_mse", actual, predicted, length, offset, 20, "MSE")

def rmse(actual: object, predicted: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Root Mean Squared Error."""
    return _pf("qtl_rmse", actual, predicted, length, offset, 20, "RMSE")

def mae(actual: object, predicted: object, length: int = 20,
        offset: int = 0, **kwargs) -> object:
    """Mean Absolute Error."""
    return _pf("qtl_mae", actual, predicted, length, offset, 20, "MAE")

def mape(actual: object, predicted: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Mean Absolute Percentage Error."""
    return _pf("qtl_mape", actual, predicted, length, offset, 20, "MAPE")


# ═══════════════════════════════════════════════════════════════════════════
#  §8.11  Filters
# ═══════════════════════════════════════════════════════════════════════════

def bessel(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Bessel Filter."""
    return _pa("qtl_bessel", close, length, offset, 14, "BESSEL", "filter")

def butter2(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """2nd-order Butterworth."""
    return _pa("qtl_butter2", close, length, offset, 14, "BUTTER2", "filter")

def butter3(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """3rd-order Butterworth."""
    return _pa("qtl_butter3", close, length, offset, 14, "BUTTER3", "filter")

def cheby1(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Chebyshev Type I."""
    return _pa("qtl_cheby1", close, length, offset, 14, "CHEBY1", "filter")

def cheby2(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Chebyshev Type II."""
    return _pa("qtl_cheby2", close, length, offset, 14, "CHEBY2", "filter")

def elliptic(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Elliptic (Cauer) Filter."""
    return _pa("qtl_elliptic", close, length, offset, 14, "ELLIPTIC", "filter")

def edcf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Ehlers Distance Coefficient Filter."""
    return _pa("qtl_edcf", close, length, offset, 14, "EDCF", "filter")

def bpf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Bandpass Filter."""
    return _pa("qtl_bpf", close, length, offset, 14, "BPF", "filter")

def alaguerre(close: object, length: int = 20, order: int = 5,
              offset: int = 0, **kwargs) -> object:
    """Adaptive Laguerre Filter."""
    length = int(length) if length is not None else 20
    order = int(order) if order is not None else 5
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_alaguerre(_ptr(src), n, _ptr(dst), length, order))
    return _wrap(dst, idx, f"ALAGUERRE_{length}", "filter", offset)

def bilateral(close: object, length: int = 14, sigma_s: float = 0.5,
              sigma_r: float = 1.0, offset: int = 0, **kwargs) -> object:
    """Bilateral Filter."""
    length = int(length) if length is not None else 14
    sigma_s = float(sigma_s) if sigma_s is not None else 0.5
    sigma_r = float(sigma_r) if sigma_r is not None else 1.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_bilateral(_ptr(src), n, _ptr(dst), length, sigma_s, sigma_r))
    return _wrap(dst, idx, f"BILATERAL_{length}", "filter", offset)

def baxterking(close: object, length: int = 12, min_period: int = 6,
               max_period: int = 32, offset: int = 0, **kwargs) -> object:
    """Baxter-King Filter."""
    length = int(length) if length is not None else 12
    min_period = int(min_period) if min_period is not None else 6
    max_period = int(max_period) if max_period is not None else 32
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_baxterking(_ptr(src), n, _ptr(dst), length, min_period, max_period))
    return _wrap(dst, idx, f"BAXTERKING_{length}", "filter", offset)

def cfitz(close: object, length: int = 6, bw_period: int = 32,
          offset: int = 0, **kwargs) -> object:
    """Christiano-Fitzgerald Filter."""
    length = int(length) if length is not None else 6
    bw_period = int(bw_period) if bw_period is not None else 32
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_cfitz(_ptr(src), n, _ptr(dst), length, bw_period))
    return _wrap(dst, idx, f"CFITZ_{length}", "filter", offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.12  Cycles
# ═══════════════════════════════════════════════════════════════════════════

def cg(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Center of Gravity."""
    return _pa("qtl_cg", close, length, offset, 10, "CG", "cycles")

def dsp(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Dominant Cycle Period (DSP)."""
    return _pa("qtl_dsp", close, length, offset, 20, "DSP", "cycles")

def ccor(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Circular Correlation."""
    return _pa("qtl_ccor", close, length, offset, 20, "CCOR", "cycles")

def ebsw(close: object, hp_length: int = 40, ssf_length: int = 10,
         offset: int = 0, **kwargs) -> object:
    """Even Better Sinewave."""
    hp_length = int(hp_length) if hp_length is not None else 40
    ssf_length = int(ssf_length) if ssf_length is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_ebsw(_ptr(src), n, _ptr(dst), hp_length, ssf_length))
    return _wrap(dst, idx, f"EBSW_{hp_length}", "cycles", offset)

def eacp(close: object, min_period: int = 8, max_period: int = 48,
         avg_length: int = 3, enhance: int = 1,
         offset: int = 0, **kwargs) -> object:
    """Ehlers Autocorrelation Periodogram."""
    min_period = int(min_period) if min_period is not None else 8
    max_period = int(max_period) if max_period is not None else 48
    avg_length = int(avg_length) if avg_length is not None else 3
    enhance = int(enhance) if enhance is not None else 1
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_eacp(_ptr(src), n, _ptr(dst), min_period, max_period, avg_length, enhance))
    return _wrap(dst, idx, f"EACP_{min_period}_{max_period}", "cycles", offset)


# ═══════════════════════════════════════════════════════════════════════════
#  §8.14  Numerics
# ═══════════════════════════════════════════════════════════════════════════

def change(close: object, length: int = 1, offset: int = 0, **kwargs) -> object:
    """Price Change."""
    return _pa("qtl_change", close, length, offset, 1, "CHANGE", "numerics")

def exptrans(close: object, offset: int = 0, **kwargs) -> object:
    """Exponential Transform."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_exptrans(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "EXPTRANS", "numerics", offset)

def betadist(close: object, length: int = 50, alpha: float = 2.0,
             beta: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Beta Distribution."""
    length = int(length) if length is not None else 50
    alpha = float(alpha) if alpha is not None else 2.0
    beta = float(beta) if beta is not None else 2.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_betadist(_ptr(src), n, _ptr(dst), length, alpha, beta))
    return _wrap(dst, idx, f"BETADIST_{length}", "numerics", offset)

def expdist(close: object, length: int = 50, lam: float = 3.0,
            offset: int = 0, **kwargs) -> object:
    """Exponential Distribution."""
    length = int(length) if length is not None else 50
    lam = float(lam) if lam is not None else 3.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_expdist(_ptr(src), n, _ptr(dst), length, lam))
    return _wrap(dst, idx, f"EXPDIST_{length}", "numerics", offset)

def binomdist(close: object, length: int = 50, trials: int = 20,
              threshold: int = 10, offset: int = 0, **kwargs) -> object:
    """Binomial Distribution."""
    length = int(length) if length is not None else 50
    trials = int(trials) if trials is not None else 20
    threshold = int(threshold) if threshold is not None else 10
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_binomdist(_ptr(src), n, _ptr(dst), length, trials, threshold))
    return _wrap(dst, idx, f"BINOMDIST_{length}", "numerics", offset)

def cwt(close: object, scale: float = 10.0, omega: float = 6.0,
        offset: int = 0, **kwargs) -> object:
    """Continuous Wavelet Transform."""
    scale = float(scale) if scale is not None else 10.0
    omega = float(omega) if omega is not None else 6.0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_cwt(_ptr(src), n, _ptr(dst), scale, omega))
    return _wrap(dst, idx, "CWT", "numerics", offset)

def dwt(close: object, length: int = 4, levels: int = 0,
        offset: int = 0, **kwargs) -> object:
    """Discrete Wavelet Transform.

    Parameters
    ----------
    length : int
        Number of decomposition levels (1-8). Default 4.
    levels : int
        Output component: 0 = approximation, 1..length = detail level.
    """
    length = int(length) if length is not None else 4
    levels = int(levels) if levels is not None else 0
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src); dst = _out(n)
    _check(_lib.qtl_dwt(_ptr(src), n, _ptr(dst), length, levels))
    return _wrap(dst, idx, f"DWT_{length}", "numerics", offset)
