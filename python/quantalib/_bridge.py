"""Low-level ctypes bindings for every quantalib NativeAOT export.

Each native function is bound via ``_bind`` at module load. If the shared
library was compiled without a particular export the binding is silently
skipped (the corresponding ``HAS_*`` flag stays False).
"""
from __future__ import annotations

import ctypes
from ctypes import c_double, c_int, POINTER
from typing import Final

from ._loader import load_native_library

# ---------------------------------------------------------------------------
#  Status codes  (mirror StatusCodes.cs)
# ---------------------------------------------------------------------------
QTL_OK: Final[int] = 0
QTL_ERR_NULL_PTR: Final[int] = 1
QTL_ERR_INVALID_LENGTH: Final[int] = 2
QTL_ERR_INVALID_PARAM: Final[int] = 3
QTL_ERR_INTERNAL: Final[int] = 4


class QtlError(Exception):
    """Base exception for quantalib native errors."""


class QtlNullPointerError(QtlError):
    pass


class QtlInvalidLengthError(QtlError):
    pass


class QtlInvalidParamError(QtlError):
    pass


class QtlInternalError(QtlError):
    pass


_STATUS_MAP: dict[int, type[QtlError]] = {
    QTL_ERR_NULL_PTR: QtlNullPointerError,
    QTL_ERR_INVALID_LENGTH: QtlInvalidLengthError,
    QTL_ERR_INVALID_PARAM: QtlInvalidParamError,
    QTL_ERR_INTERNAL: QtlInternalError,
}


def _check(status: int) -> None:
    """Raise if *status* is not QTL_OK."""
    if status == QTL_OK:
        return
    exc_type = _STATUS_MAP.get(status, QtlError)
    raise exc_type(f"quantalib native call failed (status={status})")


# ---------------------------------------------------------------------------
#  Load native library
# ---------------------------------------------------------------------------
_lib = load_native_library()

# Shorthand type aliases
_dp = POINTER(c_double)  # double*
_ip = POINTER(c_int)     # int*
_ci = c_int
_cd = c_double

# ---------------------------------------------------------------------------
#  ABI signature pattern templates
#
#  Pattern A : (src*, n, dst*, period)                   → single-input + int
#  Pattern A2: (src*, n, dst*, alpha)                    → single-input + double
#  Pattern A3: (src*, n, dst*)                           → single-input no params
#  Pattern B : (h*, l*, c*, v*, n, dst*, period)         → HLCV + int
#  Pattern C : (o*, h*, l*, c*, n, dst*)                 → OHLC no extra
#  Pattern C2: (o*, h*, l*, c*, n, dst*, double)         → OHLC + double
#  Pattern D : (h*, l*, n, dst*)                         → HL
#  Pattern E : (h*, l*, c*, n, dst*)                     → HLC
#  Pattern F : (actual*, predicted*, n, dst*, period)    → dual-input + int
#  Pattern G : (src*, vol*, n, dst*)                     → source+volume
#  Pattern G2: (src*, vol*, n, dst*, period)             → source+volume+int
#  Pattern H : (x*, y*, n, dst*, period)                 → X+Y + int
#  Pattern I : multi-output (various)
# ---------------------------------------------------------------------------

# Common argtypes per pattern
_PA  = [_dp, _ci, _dp, _ci]                    # Pattern A
_PA2 = [_dp, _ci, _dp, _cd]                    # Pattern A (alpha)
_PA3 = [_dp, _ci, _dp]                         # Pattern A (no param)
_PB  = [_dp, _dp, _dp, _dp, _ci, _dp, _ci]    # Pattern B (HLCV)
_PC  = [_dp, _dp, _dp, _dp, _ci, _dp]          # Pattern C (OHLC)
_PC2 = [_dp, _dp, _dp, _dp, _ci, _dp, _cd]     # Pattern C (OHLC+double)
_PD  = [_dp, _dp, _ci, _dp]                    # Pattern D (HL)
_PE  = [_dp, _dp, _dp, _ci, _dp]               # Pattern E (HLC)
_PF  = [_dp, _dp, _ci, _dp, _ci]               # Pattern F
_PG  = [_dp, _dp, _ci, _dp]                    # Pattern G
_PG2 = [_dp, _dp, _ci, _dp, _ci]               # Pattern G2
_PH  = [_dp, _dp, _ci, _dp, _ci]               # Pattern H


def _bind(name: str, argtypes: list[object]) -> bool:
    """Bind a single native function. Returns True if found."""
    fn = getattr(_lib, name, None)
    if fn is None:
        return False
    fn.argtypes = argtypes
    fn.restype = _ci
    return True


# ---------------------------------------------------------------------------
#  Health check
# ---------------------------------------------------------------------------
HAS_SKELETON = _bind("qtl_skeleton_noop", [_dp, _ci, _dp])

# ═══════════════════════════════════════════════════════════════════════════
#  §8.1  Core
# ═══════════════════════════════════════════════════════════════════════════
HAS_AVGPRICE  = _bind("qtl_avgprice",  _PC)
HAS_MEDPRICE  = _bind("qtl_medprice",  _PD)
HAS_TYPPRICE  = _bind("qtl_typprice",  [_dp, _dp, _dp, _ci, _dp])  # OHL (no close!)
HAS_MIDBODY   = _bind("qtl_midbody",   [_dp, _dp, _ci, _dp])       # OC

# ═══════════════════════════════════════════════════════════════════════════
#  §8.2  Momentum
# ═══════════════════════════════════════════════════════════════════════════
HAS_RSI     = _bind("qtl_rsi",     _PA)
HAS_ROC     = _bind("qtl_roc",     _PA)
HAS_MOM     = _bind("qtl_mom",     _PA)
HAS_CMO     = _bind("qtl_cmo",     _PA)
HAS_TSI     = _bind("qtl_tsi",     [_dp, _ci, _dp, _ci, _ci])        # longP, shortP
HAS_APO     = _bind("qtl_apo",     [_dp, _ci, _dp, _ci, _ci])        # fast, slow
HAS_BIAS    = _bind("qtl_bias",    _PA)
HAS_CFO     = _bind("qtl_cfo",     _PA)
HAS_CFB     = _bind("qtl_cfb",     [_dp, _ci, _dp, _ip, _ci])  # special
HAS_ASI     = _bind("qtl_asi",     _PC2)  # OHLC + double limit

# ═══════════════════════════════════════════════════════════════════════════
#  §8.3  Oscillators
# ═══════════════════════════════════════════════════════════════════════════
HAS_FISHER    = _bind("qtl_fisher",    _PA)
HAS_FISHER04  = _bind("qtl_fisher04",  _PA)
HAS_DPO       = _bind("qtl_dpo",       _PA)
HAS_TRIX      = _bind("qtl_trix",      _PA)
HAS_INERTIA   = _bind("qtl_inertia",   _PA)
HAS_RSX       = _bind("qtl_rsx",       _PA)
HAS_ER        = _bind("qtl_er",        _PA)
HAS_CTI       = _bind("qtl_cti",       _PA)
HAS_REFLEX    = _bind("qtl_reflex",    _PA)
HAS_TRENDFLEX = _bind("qtl_trendflex", _PA)
HAS_KRI       = _bind("qtl_kri",       _PA)
HAS_PSL       = _bind("qtl_psl",       _PA)
HAS_DECO      = _bind("qtl_deco",      [_dp, _ci, _dp, _ci, _ci])    # shortP, longP
HAS_DOSC      = _bind("qtl_dosc",      [_dp, _ci, _dp, _ci, _ci, _ci, _ci])  # rsiP, ema1P, ema2P, sigP
HAS_DYMOI     = _bind("qtl_dymoi",     [_dp, _ci, _dp, _ci, _ci, _ci, _ci, _ci])  # p1..p5
HAS_CRSI      = _bind("qtl_crsi",      [_dp, _ci, _dp, _ci, _ci, _ci])  # rsiP, streakP, rankP
HAS_BBB       = _bind("qtl_bbb",       [_dp, _ci, _dp, _ci, _cd])    # period, mult
HAS_BBI       = _bind("qtl_bbi",       [_dp, _ci, _dp, _ci, _ci, _ci, _ci])  # p1..p4
HAS_DEM       = _bind("qtl_dem",       _PD)   # HL pattern
HAS_BRAR      = _bind("qtl_brar",      [_dp, _dp, _dp, _dp, _ci, _dp, _dp, _ci])  # OHLC + 2 outputs + period

# ═══════════════════════════════════════════════════════════════════════════
#  §8.4  Trends — FIR
# ═══════════════════════════════════════════════════════════════════════════
HAS_SMA     = _bind("qtl_sma",     _PA)
HAS_WMA     = _bind("qtl_wma",     _PA)
HAS_HMA     = _bind("qtl_hma",     _PA)
HAS_TRIMA   = _bind("qtl_trima",   _PA)
HAS_SWMA    = _bind("qtl_swma",    _PA)
HAS_DWMA    = _bind("qtl_dwma",    _PA)
HAS_BLMA    = _bind("qtl_blma",    _PA)
HAS_ALMA    = _bind("qtl_alma",    _PA)
HAS_LSMA    = _bind("qtl_lsma",   _PA)
HAS_SGMA    = _bind("qtl_sgma",    _PA)
HAS_SINEMA  = _bind("qtl_sinema",  _PA)
HAS_HANMA   = _bind("qtl_hanma",   _PA)
HAS_PARZEN  = _bind("qtl_parzen",  _PA)
HAS_TSF     = _bind("qtl_tsf",     _PA)
HAS_CONV    = _bind("qtl_conv",    [_dp, _ci, _dp, _dp, _ci])  # src,n,dst,kernel*,kernelLen
HAS_BWMA    = _bind("qtl_bwma",    [_dp, _ci, _dp, _ci, _ci])        # period, polyOrder
HAS_CRMA    = _bind("qtl_crma",    [_dp, _ci, _dp, _ci, _cd])        # period, volumeFactor
HAS_SP15    = _bind("qtl_sp15",    _PA)
HAS_TUKEY_W = _bind("qtl_tukey_w", _PA)
HAS_RAIN    = _bind("qtl_rain",    _PA)
HAS_AFIRMA  = _bind("qtl_afirma",  [_dp, _ci, _dp, _ci, _ci, _ci])  # src,n,dst,period,windowType,useSimd

# ═══════════════════════════════════════════════════════════════════════════
#  §8.5  Trends — IIR
# ═══════════════════════════════════════════════════════════════════════════
HAS_EMA         = _bind("qtl_ema",         _PA)
HAS_EMA_ALPHA   = _bind("qtl_ema_alpha",   _PA2)
HAS_DEMA        = _bind("qtl_dema",        _PA)
HAS_DEMA_ALPHA  = _bind("qtl_dema_alpha",  _PA2)
HAS_TEMA        = _bind("qtl_tema",        _PA)
HAS_LEMA        = _bind("qtl_lema",        _PA)
HAS_HEMA        = _bind("qtl_hema",        _PA)
HAS_AHRENS      = _bind("qtl_ahrens",      _PA)
HAS_DECYCLER    = _bind("qtl_decycler",    _PA)
HAS_DSMA        = _bind("qtl_dsma",        [_dp, _ci, _dp, _ci, _cd])  # period, factor
HAS_GDEMA       = _bind("qtl_gdema",       [_dp, _ci, _dp, _ci, _cd])  # period, factor
HAS_CORAL       = _bind("qtl_coral",       [_dp, _ci, _dp, _ci, _cd])  # period, friction
HAS_AGC         = _bind("qtl_agc",         _PA2)  # alpha
HAS_CCYC        = _bind("qtl_ccyc",        _PA2)  # alpha

# ═══════════════════════════════════════════════════════════════════════════
#  §8.6  Channels
# ═══════════════════════════════════════════════════════════════════════════
HAS_BBANDS    = _bind("qtl_bbands",    [_dp, _ci, _dp, _dp, _dp, _ci, _cd])  # src,n, upper,mid,lower, period,mult
HAS_ABBER     = _bind("qtl_abber",     [_dp, _dp, _dp, _dp, _ci, _ci, _cd])  # src,mid,upper,lower,n,period,mult
HAS_ATRBANDS  = _bind("qtl_atrbands",  [_dp, _dp, _dp, _ci, _dp, _dp, _dp, _ci, _cd])  # h,l,c,n, upper,mid,lower, period,mult
HAS_APCHANNEL = _bind("qtl_apchannel", [_dp, _dp, _ci, _dp, _dp, _ci])  # h,l,n, upper,lower, period

# ═══════════════════════════════════════════════════════════════════════════
#  §8.7  Volatility
# ═══════════════════════════════════════════════════════════════════════════
HAS_TR       = _bind("qtl_tr",       _PE)  # HLC
HAS_BBW      = _bind("qtl_bbw",      _PA)
HAS_BBWN     = _bind("qtl_bbwn",     [_dp, _ci, _dp, _ci, _cd, _ci])  # period, mult, lookback
HAS_BBWP     = _bind("qtl_bbwp",     [_dp, _ci, _dp, _ci, _cd, _ci])  # period, mult, lookback
HAS_STDDEV   = _bind("qtl_stddev",   _PA)
HAS_VARIANCE = _bind("qtl_variance", _PA)
HAS_ETHERM   = _bind("qtl_etherm",   _PD)  # HL
HAS_CCV      = _bind("qtl_ccv",      [_dp, _ci, _dp, _ci, _ci])      # shortP, longP
HAS_CV       = _bind("qtl_cv",       [_dp, _ci, _dp, _ci, _cd, _cd]) # period, minVol, maxVol
HAS_CVI      = _bind("qtl_cvi",      [_dp, _ci, _dp, _ci, _ci])      # emaPeriod, rocPeriod
HAS_EWMA     = _bind("qtl_ewma",     [_dp, _ci, _dp, _ci, _ci, _ci]) # period, isPop, annFactor

# ═══════════════════════════════════════════════════════════════════════════
#  §8.8  Volume
# ═══════════════════════════════════════════════════════════════════════════
HAS_OBV    = _bind("qtl_obv",    _PG)  # close,vol,n,dst
HAS_PVT    = _bind("qtl_pvt",    _PG)
HAS_PVR    = _bind("qtl_pvr",    _PG)
HAS_VF     = _bind("qtl_vf",     _PG)
HAS_NVI    = _bind("qtl_nvi",    _PG)
HAS_PVI    = _bind("qtl_pvi",    _PG)
HAS_TVI    = _bind("qtl_tvi",    _PG2)  # close,vol,n,dst,period
HAS_PVD    = _bind("qtl_pvd",    _PG2)
HAS_VWMA   = _bind("qtl_vwma",   _PG2)
HAS_EVWMA  = _bind("qtl_evwma",  _PG2)
HAS_EFI    = _bind("qtl_efi",    _PG2)
HAS_AOBV   = _bind("qtl_aobv",   [_dp, _dp, _ci, _dp, _dp])  # close,vol,n,obv,signal
HAS_MFI    = _bind("qtl_mfi",    _PB)   # HLCV + period
HAS_CMF    = _bind("qtl_cmf",    _PB)
HAS_EOM    = _bind("qtl_eom",    [_dp, _dp, _dp, _ci, _dp, _ci])  # h,l,v,n,dst,period
HAS_PVO    = _bind("qtl_pvo",    [_dp, _ci, _dp, _dp, _dp, _ci, _ci, _ci])  # vol,n, pvo,signal,hist, fast,slow,signal_p

# ═══════════════════════════════════════════════════════════════════════════
#  §8.9  Statistics
# ═══════════════════════════════════════════════════════════════════════════
HAS_ZSCORE        = _bind("qtl_zscore",        _PA)
HAS_CMA           = _bind("qtl_cma",           _PA3)  # no period
HAS_ENTROPY       = _bind("qtl_entropy",       _PA)
HAS_CORRELATION   = _bind("qtl_correlation",   _PH)
HAS_COVARIANCE    = _bind("qtl_covariance",    [_dp, _dp, _ci, _dp, _ci, _ci])  # x,y,n,dst,period,isSample
HAS_COINTEGRATION = _bind("qtl_cointegration", _PH)

# ═══════════════════════════════════════════════════════════════════════════
#  §8.10  Errors
# ═══════════════════════════════════════════════════════════════════════════
HAS_MSE  = _bind("qtl_mse",  _PF)
HAS_RMSE = _bind("qtl_rmse", _PF)
HAS_MAE  = _bind("qtl_mae",  _PF)
HAS_MAPE = _bind("qtl_mape", _PF)

# ═══════════════════════════════════════════════════════════════════════════
#  §8.11  Filters
# ═══════════════════════════════════════════════════════════════════════════
HAS_BESSEL     = _bind("qtl_bessel",     _PA)
HAS_BUTTER2    = _bind("qtl_butter2",    _PA)
HAS_BUTTER3    = _bind("qtl_butter3",    _PA)
HAS_CHEBY1     = _bind("qtl_cheby1",     _PA)
HAS_CHEBY2     = _bind("qtl_cheby2",     _PA)
HAS_ELLIPTIC   = _bind("qtl_elliptic",   _PA)
HAS_EDCF       = _bind("qtl_edcf",       _PA)
HAS_BPF        = _bind("qtl_bpf",        _PA)
HAS_ALAGUERRE  = _bind("qtl_alaguerre",  [_dp, _ci, _dp, _ci, _ci])      # period, order
HAS_BILATERAL  = _bind("qtl_bilateral",  [_dp, _ci, _dp, _ci, _cd, _cd]) # period, sigmaS, sigmaR
HAS_BAXTERKING = _bind("qtl_baxterking", [_dp, _ci, _dp, _ci, _ci, _ci]) # period, minP, maxP
HAS_CFITZ      = _bind("qtl_cfitz",      [_dp, _ci, _dp, _ci, _ci])      # period, bandwidthP

# ═══════════════════════════════════════════════════════════════════════════
#  §8.12  Cycles
# ═══════════════════════════════════════════════════════════════════════════
HAS_CG   = _bind("qtl_cg",   _PA)
HAS_DSP  = _bind("qtl_dsp",  _PA)
HAS_CCOR = _bind("qtl_ccor", _PA)
HAS_EBSW = _bind("qtl_ebsw", [_dp, _ci, _dp, _ci, _ci])              # period, hpPeriod
HAS_EACP = _bind("qtl_eacp", [_dp, _ci, _dp, _ci, _ci, _ci, _ci])    # period, minP, maxP, useMedian

# ═══════════════════════════════════════════════════════════════════════════
#  §8.14  Numerics
# ═══════════════════════════════════════════════════════════════════════════
HAS_CHANGE    = _bind("qtl_change",    _PA)
HAS_EXPTRANS  = _bind("qtl_exptrans",  _PA3)  # no period
HAS_BETADIST  = _bind("qtl_betadist",  [_dp, _ci, _dp, _ci, _cd, _cd])  # period, alpha, beta
HAS_EXPDIST   = _bind("qtl_expdist",   [_dp, _ci, _dp, _ci, _cd])       # period, lambda
HAS_BINOMDIST = _bind("qtl_binomdist", [_dp, _ci, _dp, _ci, _ci, _ci])  # period, trials, successes
HAS_CWT       = _bind("qtl_cwt",       [_dp, _ci, _dp, _cd, _cd])       # scale, omega
HAS_DWT       = _bind("qtl_dwt",       [_dp, _ci, _dp, _ci, _ci])       # period, levels
