#!/usr/bin/env python3
"""Complete the Python bridge by adding all Exports.cs (manual) bindings and wrappers.

This script:
1. Rewrites _bridge.py with ALL bindings (Generated + Manual)
2. Appends missing wrappers to each category .py file
3. Rewrites indicators.py as thin re-export
4. Updates __init__.py
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "quantalib")


def write(path: str, content: str) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)
    print(f"  wrote {os.path.relpath(path, ROOT)}")


def append(path: str, content: str) -> None:
    with open(path, "a", encoding="utf-8", newline="\n") as f:
        f.write(content)
    print(f"  appended to {os.path.relpath(path, ROOT)}")


def read(path: str) -> str:
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


# ═══════════════════════════════════════════════════════════════════════════
#  Step 1: Read existing _bridge.py and add missing manual bindings
# ═══════════════════════════════════════════════════════════════════════════
print("Step 1: Adding missing bindings to _bridge.py ...")

bridge_path = os.path.join(PKG, "_bridge.py")
bridge = read(bridge_path)

# Check which bindings already exist
MANUAL_BINDINGS = {
    # ── Core ──
    "qtl_avgprice": ["{dp}", "{dp}", "{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_medprice": ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_typprice": ["{dp}", "{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_midbody":  ["{dp}", "{dp}", "{ci}", "{dp}"],
    # ── Momentum ──
    "qtl_rsi":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_roc":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_mom":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_cmo":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_tsi":  ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_apo":  ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_bias": ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_cfo":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_cfb":  ["{dp}", "{ci}", "{dp}", "{ip}", "{ci}"],
    "qtl_asi":  ["{dp}", "{dp}", "{dp}", "{dp}", "{ci}", "{dp}", "{cd}"],
    # ── Oscillators ──
    "qtl_fisher":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_fisher04":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_dpo":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_trix":      ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_inertia":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_rsx":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_er":        ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_cti":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_reflex":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_trendflex": ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_kri":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_psl":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_deco":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_dosc":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}", "{ci}"],
    "qtl_dymoi":     ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}", "{ci}", "{ci}"],
    "qtl_crsi":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}"],
    "qtl_bbb":       ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_bbi":       ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}", "{ci}"],
    "qtl_dem":       ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_brar":      ["{dp}", "{dp}", "{dp}", "{dp}", "{ci}", "{dp}", "{dp}", "{ci}"],
    # ── Trends FIR ──
    "qtl_sma":     ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_wma":     ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_hma":     ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_trima":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_swma":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_dwma":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_blma":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_alma":    ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}", "{cd}"],
    "qtl_lsma":    ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{cd}"],
    "qtl_sgma":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_sinema":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_hanma":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_parzen":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_tsf":     ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_conv":    ["{dp}", "{ci}", "{dp}", "{dp}", "{ci}"],
    "qtl_bwma":    ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_crma":    ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_sp15":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_tukey_w": ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_rain":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_afirma":  ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}"],
    # ── Trends IIR ──
    "qtl_ema":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_ema_alpha": ["{dp}", "{ci}", "{dp}", "{cd}"],
    "qtl_dema":      ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_dema_alpha":["{dp}", "{ci}", "{dp}", "{cd}"],
    "qtl_tema":      ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_lema":      ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_hema":      ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_ahrens":    ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_decycler":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_dsma":      ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_gdema":     ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_coral":     ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_agc":       ["{dp}", "{ci}", "{dp}", "{cd}"],
    "qtl_ccyc":      ["{dp}", "{ci}", "{dp}", "{cd}"],
    # ── Channels ──
    "qtl_bbands":    ["{dp}", "{ci}", "{dp}", "{dp}", "{dp}", "{ci}", "{cd}"],
    "qtl_aberr":     ["{dp}", "{ci}", "{dp}", "{dp}", "{dp}", "{ci}", "{cd}"],
    "qtl_atrbands":  ["{dp}", "{dp}", "{dp}", "{ci}", "{dp}", "{dp}", "{dp}", "{ci}", "{cd}"],
    "qtl_apchannel": ["{dp}", "{dp}", "{ci}", "{dp}", "{dp}", "{cd}"],
    # ── Volatility ──
    "qtl_tr":       ["{dp}", "{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_bbw":      ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_bbwn":     ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}", "{ci}"],
    "qtl_bbwp":     ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}", "{ci}"],
    "qtl_stddev":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_variance": ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_etherm":   ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_ccv":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_cv":       ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}", "{cd}"],
    "qtl_cvi":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_ewma":     ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}"],
    # ── Volume ──
    "qtl_obv":   ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_pvt":   ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_pvr":   ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_vf":    ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_nvi":   ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_pvi":   ["{dp}", "{dp}", "{ci}", "{dp}"],
    "qtl_tvi":   ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_pvd":   ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_vwma":  ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_evwma": ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_efi":   ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_aobv":  ["{dp}", "{dp}", "{ci}", "{dp}", "{dp}"],
    "qtl_mfi":   ["{dp}", "{dp}", "{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_cmf":   ["{dp}", "{dp}", "{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_eom":   ["{dp}", "{dp}", "{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_pvo":   ["{dp}", "{ci}", "{dp}", "{dp}", "{dp}", "{ci}", "{ci}", "{ci}"],
    # ── Statistics ──
    "qtl_zscore":        ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_cma":           ["{dp}", "{ci}", "{dp}"],
    "qtl_entropy":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_correlation":   ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_covariance":    ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_cointegration": ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    # ── Errors ──
    "qtl_mse":  ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_rmse": ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_mae":  ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_mape": ["{dp}", "{dp}", "{ci}", "{dp}", "{ci}"],
    # ── Filters ──
    "qtl_bessel":     ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_butter2":    ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_butter3":    ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_cheby1":     ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_cheby2":     ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_elliptic":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_edcf":       ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_bpf":        ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_alaguerre":  ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_bilateral":  ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}", "{cd}"],
    "qtl_baxterking": ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}"],
    "qtl_cfitz":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    # ── Cycles ──
    "qtl_cg":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_dsp":  ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_ccor": ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_ebsw": ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
    "qtl_eacp": ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}", "{ci}"],
    # ── Numerics ──
    "qtl_change":   ["{dp}", "{ci}", "{dp}", "{ci}"],
    "qtl_exptrans": ["{dp}", "{ci}", "{dp}"],
    "qtl_betadist": ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}", "{cd}"],
    "qtl_expdist":  ["{dp}", "{ci}", "{dp}", "{ci}", "{cd}"],
    "qtl_binomdist":["{dp}", "{ci}", "{dp}", "{ci}", "{ci}", "{ci}"],
    "qtl_cwt":      ["{dp}", "{ci}", "{dp}", "{cd}", "{cd}"],
    "qtl_dwt":      ["{dp}", "{ci}", "{dp}", "{ci}", "{ci}"],
}

# Build binding lines
TYPE_MAP = {"{dp}": "_dp", "{ci}": "_ci", "{cd}": "_cd", "{ip}": "_ip"}

missing_bindings = {}  # category -> list of lines
categories_order = [
    ("Core", ["qtl_avgprice","qtl_medprice","qtl_typprice","qtl_midbody"]),
    ("Momentum", ["qtl_rsi","qtl_roc","qtl_mom","qtl_cmo","qtl_tsi","qtl_apo","qtl_bias","qtl_cfo","qtl_cfb","qtl_asi"]),
    ("Oscillators", ["qtl_fisher","qtl_fisher04","qtl_dpo","qtl_trix","qtl_inertia","qtl_rsx","qtl_er","qtl_cti","qtl_reflex","qtl_trendflex","qtl_kri","qtl_psl","qtl_deco","qtl_dosc","qtl_dymoi","qtl_crsi","qtl_bbb","qtl_bbi","qtl_dem","qtl_brar"]),
    ("Trends — FIR", ["qtl_sma","qtl_wma","qtl_hma","qtl_trima","qtl_swma","qtl_dwma","qtl_blma","qtl_alma","qtl_lsma","qtl_sgma","qtl_sinema","qtl_hanma","qtl_parzen","qtl_tsf","qtl_conv","qtl_bwma","qtl_crma","qtl_sp15","qtl_tukey_w","qtl_rain","qtl_afirma"]),
    ("Trends — IIR", ["qtl_ema","qtl_ema_alpha","qtl_dema","qtl_dema_alpha","qtl_tema","qtl_lema","qtl_hema","qtl_ahrens","qtl_decycler","qtl_dsma","qtl_gdema","qtl_coral","qtl_agc","qtl_ccyc"]),
    ("Channels", ["qtl_bbands","qtl_aberr","qtl_atrbands","qtl_apchannel"]),
    ("Volatility", ["qtl_tr","qtl_bbw","qtl_bbwn","qtl_bbwp","qtl_stddev","qtl_variance","qtl_etherm","qtl_ccv","qtl_cv","qtl_cvi","qtl_ewma"]),
    ("Volume", ["qtl_obv","qtl_pvt","qtl_pvr","qtl_vf","qtl_nvi","qtl_pvi","qtl_tvi","qtl_pvd","qtl_vwma","qtl_evwma","qtl_efi","qtl_aobv","qtl_mfi","qtl_cmf","qtl_eom","qtl_pvo"]),
    ("Statistics", ["qtl_zscore","qtl_cma","qtl_entropy","qtl_correlation","qtl_covariance","qtl_cointegration"]),
    ("Errors", ["qtl_mse","qtl_rmse","qtl_mae","qtl_mape"]),
    ("Filters", ["qtl_bessel","qtl_butter2","qtl_butter3","qtl_cheby1","qtl_cheby2","qtl_elliptic","qtl_edcf","qtl_bpf","qtl_alaguerre","qtl_bilateral","qtl_baxterking","qtl_cfitz"]),
    ("Cycles", ["qtl_cg","qtl_dsp","qtl_ccor","qtl_ebsw","qtl_eacp"]),
    ("Numerics", ["qtl_change","qtl_exptrans","qtl_betadist","qtl_expdist","qtl_binomdist","qtl_cwt","qtl_dwt"]),
]

added_count = 0
new_lines = []
for cat, names in categories_order:
    cat_lines = []
    for name in names:
        if f'"{name}"' in bridge:
            continue  # already bound
        args = MANUAL_BINDINGS[name]
        arg_str = ", ".join(TYPE_MAP[a] for a in args)
        var = "HAS_" + name.replace("qtl_", "").upper()
        cat_lines.append(f'{var} = _bind("{name}", [{arg_str}])')
        added_count += 1
    if cat_lines:
        new_lines.append(f"\n# ── {cat}  (Exports.cs — manual) ──")
        new_lines.extend(cat_lines)

if new_lines:
    # Append to end of _bridge.py
    with open(bridge_path, "a", encoding="utf-8", newline="\n") as f:
        f.write("\n")
        f.write("\n".join(new_lines))
        f.write("\n")
    print(f"  Added {added_count} bindings to _bridge.py")
else:
    print("  All bindings already present in _bridge.py")


# ═══════════════════════════════════════════════════════════════════════════
#  Step 2: Add missing wrappers to category .py files
# ═══════════════════════════════════════════════════════════════════════════
print("\nStep 2: Adding missing wrappers to category files ...")

# For each category file, check what's in __all__ and add missing funcs


# ── core.py ──
core_additions = '''

def avgprice(open: object, high: object, low: object, close: object,
             offset: int = 0, **kwargs) -> object:
    """Average Price = (O+H+L+C)/4."""
    offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_avgprice(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "AVGPRICE", "core", offset)


def medprice(high: object, low: object, offset: int = 0, **kwargs) -> object:
    """Median Price = (H+L)/2."""
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_medprice(_ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "MEDPRICE", "core", int(offset))


def typprice(open: object, high: object, low: object,
             offset: int = 0, **kwargs) -> object:
    """Typical Price = (O+H+L)/3 (QuanTAlib variant)."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_typprice(_ptr(o), _ptr(h), _ptr(l), n, _ptr(dst)))
    return _wrap(dst, idx, "TYPPRICE", "core", int(offset))


def midbody(open: object, close: object, offset: int = 0, **kwargs) -> object:
    """Mid Body = (O+C)/2."""
    o, idx = _arr(open); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_midbody(_ptr(o), _ptr(c), n, _ptr(dst)))
    return _wrap(dst, idx, "MIDBODY", "core", int(offset))
'''


# ── momentum.py ──
momentum_additions = '''
import ctypes


def rsi(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Strength Index."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rsi(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"RSI_{length}", "momentum", offset)


def roc(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Rate of Change."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_roc(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ROC_{length}", "momentum", offset)


def mom(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Momentum."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_mom(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MOM_{length}", "momentum", offset)


def cmo(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Chande Momentum Oscillator."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cmo(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CMO_{length}", "momentum", offset)


def tsi(close: object, long_period: int = 25, short_period: int = 13,
        offset: int = 0, **kwargs) -> object:
    """True Strength Index."""
    long_period = int(long_period); short_period = int(short_period); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tsi(_ptr(src), n, _ptr(dst), long_period, short_period))
    return _wrap(dst, idx, f"TSI_{long_period}_{short_period}", "momentum", offset)


def apo(close: object, fast: int = 12, slow: int = 26,
        offset: int = 0, **kwargs) -> object:
    """Absolute Price Oscillator."""
    fast = int(fast); slow = int(slow); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_apo(_ptr(src), n, _ptr(dst), fast, slow))
    return _wrap(dst, idx, f"APO_{fast}_{slow}", "momentum", offset)


def bias(close: object, length: int = 26, offset: int = 0, **kwargs) -> object:
    """Bias."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bias(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"BIAS_{length}", "momentum", offset)


def cfo(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Chande Forecast Oscillator."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cfo(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CFO_{length}", "momentum", offset)


def cfb(close: object, lengths: list | None = None,
        offset: int = 0, **kwargs) -> object:
    """Composite Fractal Behavior."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    if lengths:
        arr_t = (ctypes.c_int * len(lengths))(*lengths)
        _check(_lib.qtl_cfb(_ptr(src), n, _ptr(dst), arr_t, len(lengths)))
    else:
        _check(_lib.qtl_cfb(_ptr(src), n, _ptr(dst), None, 0))
    return _wrap(dst, idx, "CFB", "momentum", offset)


def asi(open: object, high: object, low: object, close: object,
        limit: float = 3.0, offset: int = 0, **kwargs) -> object:
    """Accumulative Swing Index."""
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); dst = _out(n)
    _check(_lib.qtl_asi(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(dst), float(limit)))
    return _wrap(dst, idx, "ASI", "momentum", int(offset))
'''


# ── oscillators.py ──
oscillators_additions = '''

def fisher(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Fisher Transform."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_fisher(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"FISHER_{length}", "oscillators", offset)


def fisher04(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Fisher Transform (0.4 variant)."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_fisher04(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"FISHER04_{length}", "oscillators", offset)


def dpo(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Detrended Price Oscillator."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dpo(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DPO_{length}", "oscillators", offset)


def trix(close: object, length: int = 18, offset: int = 0, **kwargs) -> object:
    """Triple EMA Rate of Change."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trix(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TRIX_{length}", "oscillators", offset)


def inertia(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Inertia."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_inertia(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"INERTIA_{length}", "oscillators", offset)


def rsx(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Strength Xtra."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rsx(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"RSX_{length}", "oscillators", offset)


def er(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Efficiency Ratio."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_er(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ER_{length}", "oscillators", offset)


def cti(close: object, length: int = 12, offset: int = 0, **kwargs) -> object:
    """Correlation Trend Indicator."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cti(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CTI_{length}", "oscillators", offset)


def reflex(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Reflex."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_reflex(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"REFLEX_{length}", "oscillators", offset)


def trendflex(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Trendflex."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trendflex(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TRENDFLEX_{length}", "oscillators", offset)


def kri(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Kairi Relative Index."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_kri(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"KRI_{length}", "oscillators", offset)


def psl(close: object, length: int = 12, offset: int = 0, **kwargs) -> object:
    """Psychological Line."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_psl(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"PSL_{length}", "oscillators", offset)


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


def dymoi(close: object, base_period: int = 14, short_period: int = 5,
          long_period: int = 10, min_period: int = 3, max_period: int = 30,
          offset: int = 0, **kwargs) -> object:
    """Dynamic Momentum Index."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dymoi(_ptr(src), n, _ptr(dst),
                          int(base_period), int(short_period), int(long_period),
                          int(min_period), int(max_period)))
    return _wrap(dst, idx, "DYMOI", "oscillators", offset)


def crsi(close: object, rsi_period: int = 3, streak_period: int = 2,
         rank_period: int = 100, offset: int = 0, **kwargs) -> object:
    """Connors RSI."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_crsi(_ptr(src), n, _ptr(dst),
                         int(rsi_period), int(streak_period), int(rank_period)))
    return _wrap(dst, idx, f"CRSI_{rsi_period}", "oscillators", offset)


def bbb(close: object, length: int = 20, mult: float = 2.0,
        offset: int = 0, **kwargs) -> object:
    """Bollinger Band Bounce."""
    length = int(length); mult = float(mult); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbb(_ptr(src), n, _ptr(dst), length, mult))
    return _wrap(dst, idx, f"BBB_{length}", "oscillators", offset)


def bbi(close: object, p1: int = 3, p2: int = 6, p3: int = 12, p4: int = 24,
        offset: int = 0, **kwargs) -> object:
    """Bull Bear Index."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bbi(_ptr(src), n, _ptr(dst), int(p1), int(p2), int(p3), int(p4)))
    return _wrap(dst, idx, "BBI", "oscillators", offset)


def dem(high: object, low: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """DeMarker."""
    length = int(length)
    h, idx = _arr(high); l, _ = _arr(low)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_dem(_ptr(h), _ptr(l), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DEM_{length}", "oscillators", int(offset))


def brar(open: object, high: object, low: object, close: object,
         length: int = 26, offset: int = 0, **kwargs) -> object:
    """Bull-Bear Ratio (BRAR)."""
    length = int(length); offset = int(offset)
    o, idx = _arr(open); h, _ = _arr(high); l, _ = _arr(low); c, _ = _arr(close)
    n = len(o); br = _out(n); ar = _out(n)
    _check(_lib.qtl_brar(_ptr(o), _ptr(h), _ptr(l), _ptr(c), n, _ptr(br), _ptr(ar), length))
    return _wrap_multi({f"BR_{length}": br, f"AR_{length}": ar}, idx, "oscillators", offset)
'''


# ── trends_fir.py ──
trends_fir_additions = '''
import numpy as np

_F64 = np.float64


def sma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Simple Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SMA_{length}", "trends_fir", offset)


def wma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_wma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"WMA_{length}", "trends_fir", offset)


def hma(close: object, length: int = 9, offset: int = 0, **kwargs) -> object:
    """Hull Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"HMA_{length}", "trends_fir", offset)


def trima(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Triangular Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_trima(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TRIMA_{length}", "trends_fir", offset)


def swma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Symmetric Weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_swma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SWMA_{length}", "trends_fir", offset)


def dwma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Double Weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dwma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DWMA_{length}", "trends_fir", offset)


def blma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Blackman Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_blma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"BLMA_{length}", "trends_fir", offset)


def alma(close: object, length: int = 10, alma_offset: float = 0.85,
         sigma: float = 6.0, offset: int = 0, **kwargs) -> object:
    """Arnaud Legoux Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_alma(_ptr(src), n, _ptr(dst), length, float(alma_offset), float(sigma)))
    return _wrap(dst, idx, f"ALMA_{length}", "trends_fir", offset)


def lsma(close: object, length: int = 25, offset: int = 0, **kwargs) -> object:
    """Least Squares Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lsma(_ptr(src), n, _ptr(dst), length, 0, 1.0))
    return _wrap(dst, idx, f"LSMA_{length}", "trends_fir", offset)


def sgma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Savitzky-Golay Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sgma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SGMA_{length}", "trends_fir", offset)


def sinema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Sine-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sinema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SINEMA_{length}", "trends_fir", offset)


def hanma(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Hann-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hanma(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"HANMA_{length}", "trends_fir", offset)


def parzen(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Parzen-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_parzen(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"PARZEN_{length}", "trends_fir", offset)


def tsf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Time Series Forecast."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tsf(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TSF_{length}", "trends_fir", offset)


def conv(close: object, kernel: list | None = None,
         offset: int = 0, **kwargs) -> object:
    """Convolution with custom kernel."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    if kernel is None:
        kernel = [1.0]
    k = np.ascontiguousarray(kernel, dtype=_F64)
    _check(_lib.qtl_conv(_ptr(src), n, _ptr(dst), _ptr(k), len(k)))
    return _wrap(dst, idx, "CONV", "trends_fir", offset)


def bwma(close: object, length: int = 10, order: int = 0,
         offset: int = 0, **kwargs) -> object:
    """Butterworth-weighted Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bwma(_ptr(src), n, _ptr(dst), length, int(order)))
    return _wrap(dst, idx, f"BWMA_{length}", "trends_fir", offset)


def crma(close: object, length: int = 10, volume_factor: float = 1.0,
         offset: int = 0, **kwargs) -> object:
    """Cosine-Ramp Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_crma(_ptr(src), n, _ptr(dst), length, float(volume_factor)))
    return _wrap(dst, idx, f"CRMA_{length}", "trends_fir", offset)


def sp15(close: object, length: int = 15, offset: int = 0, **kwargs) -> object:
    """SP-15 Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_sp15(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"SP15_{length}", "trends_fir", offset)


def tukey_w(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Tukey-windowed Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tukey_w(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TUKEY_{length}", "trends_fir", offset)


def rain(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """RAIN Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_rain(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"RAIN_{length}", "trends_fir", offset)


def afirma(close: object, length: int = 10, window_type: int = 0,
           use_simd: bool = False, offset: int = 0, **kwargs) -> object:
    """Adaptive FIR Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_afirma(_ptr(src), n, _ptr(dst), length, int(window_type), int(use_simd)))
    return _wrap(dst, idx, f"AFIRMA_{length}", "trends_fir", offset)
'''


# ── trends_iir.py ──
trends_iir_additions = '''

def ema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Exponential Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EMA_{length}", "trends_iir", offset)


def ema_alpha(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """EMA with explicit alpha."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"EMA_a{alpha:.4f}", "trends_iir", offset)


def dema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Double Exponential Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DEMA_{length}", "trends_iir", offset)


def dema_alpha(close: object, alpha: float = 0.1, offset: int = 0, **kwargs) -> object:
    """DEMA with explicit alpha."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dema_alpha(_ptr(src), n, _ptr(dst), float(alpha)))
    return _wrap(dst, idx, f"DEMA_a{alpha:.4f}", "trends_iir", offset)


def tema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Triple Exponential Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_tema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TEMA_{length}", "trends_iir", offset)


def lema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Laguerre-based EMA."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_lema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"LEMA_{length}", "trends_iir", offset)


def hema(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Henderson EMA."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_hema(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"HEMA_{length}", "trends_iir", offset)


def ahrens(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Ahrens Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ahrens(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"AHRENS_{length}", "trends_iir", offset)


def decycler(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Simple Decycler."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_decycler(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DECYCLER_{length}", "trends_iir", offset)


def dsma(close: object, length: int = 10, factor: float = 0.5,
         offset: int = 0, **kwargs) -> object:
    """Deviation-Scaled Moving Average."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dsma(_ptr(src), n, _ptr(dst), length, float(factor)))
    return _wrap(dst, idx, f"DSMA_{length}", "trends_iir", offset)


def gdema(close: object, length: int = 10, vfactor: float = 1.0,
          offset: int = 0, **kwargs) -> object:
    """Generalized DEMA."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_gdema(_ptr(src), n, _ptr(dst), length, float(vfactor)))
    return _wrap(dst, idx, f"GDEMA_{length}", "trends_iir", offset)


def coral(close: object, length: int = 10, friction: float = 0.4,
          offset: int = 0, **kwargs) -> object:
    """CORAL Trend."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_coral(_ptr(src), n, _ptr(dst), length, float(friction)))
    return _wrap(dst, idx, f"CORAL_{length}", "trends_iir", offset)


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
'''


# ── channels.py ──
channels_additions = '''

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


def aberr(close: object, length: int = 20, mult: float = 2.0,
          offset: int = 0, **kwargs) -> object:
    """Aberration Bands -> (upper, mid, lower) or DataFrame."""
    length = int(length); mult = float(mult); offset = int(offset)
    src, idx = _arr(close); n = len(src)
    upper = _out(n); mid = _out(n); lower = _out(n)
    _check(_lib.qtl_aberr(_ptr(src), n, _ptr(mid), _ptr(upper), _ptr(lower), length, mult))
    return _wrap_multi(
        {f"ABERRU_{length}_{mult}": upper, f"ABERRM_{length}_{mult}": mid, f"ABERRL_{length}_{mult}": lower},
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
'''


# ── volatility.py ──
volatility_additions = '''

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
'''


# ── volume.py ──
volume_additions = '''

def obv(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """On-Balance Volume."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_obv(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "OBV", "volume", offset)


def pvt(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Price Volume Trend."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvt(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVT", "volume", offset)


def pvr(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Price Volume Rank."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvr(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVR", "volume", offset)


def vf(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Volume Flow."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_vf(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "VF", "volume", offset)


def nvi(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Negative Volume Index."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_nvi(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "NVI", "volume", offset)


def pvi(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Positive Volume Index."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvi(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, "PVI", "volume", offset)


def tvi(close: object, volume: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """Trade Volume Index."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_tvi(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"TVI_{length}", "volume", offset)


def pvd(close: object, volume: object, length: int = 14,
        offset: int = 0, **kwargs) -> object:
    """Price Volume Divergence."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_pvd(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"PVD_{length}", "volume", offset)


def vwma(close: object, volume: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Volume Weighted Moving Average."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_vwma(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"VWMA_{length}", "volume", offset)


def evwma(close: object, volume: object, length: int = 20,
          offset: int = 0, **kwargs) -> object:
    """Elastic Volume Weighted Moving Average."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_evwma(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EVWMA_{length}", "volume", offset)


def efi(close: object, volume: object, length: int = 13,
        offset: int = 0, **kwargs) -> object:
    """Elder Force Index."""
    length = int(length); offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); dst = _out(n)
    _check(_lib.qtl_efi(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EFI_{length}", "volume", offset)


def aobv(close: object, volume: object, offset: int = 0, **kwargs) -> object:
    """Archer OBV -> (fast, slow) or DataFrame."""
    offset = int(offset)
    c, idx = _arr(close); v, _ = _arr(volume)
    n = len(c); obv_out = _out(n); sig = _out(n)
    _check(_lib.qtl_aobv(_ptr(c), _ptr(v), n, _ptr(obv_out), _ptr(sig)))
    return _wrap_multi({"AOBV": obv_out, "AOBV_SIG": sig}, idx, "volume", offset)


def mfi(high: object, low: object, close: object, volume: object,
        length: int = 14, offset: int = 0, **kwargs) -> object:
    """Money Flow Index."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_mfi(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MFI_{length}", "volume", offset)


def cmf(high: object, low: object, close: object, volume: object,
        length: int = 20, offset: int = 0, **kwargs) -> object:
    """Chaikin Money Flow."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); c, _ = _arr(close); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_cmf(_ptr(h), _ptr(l), _ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CMF_{length}", "volume", offset)


def eom(high: object, low: object, volume: object,
        length: int = 14, offset: int = 0, **kwargs) -> object:
    """Ease of Movement."""
    length = int(length); offset = int(offset)
    h, idx = _arr(high); l, _ = _arr(low); v, _ = _arr(volume)
    n = len(h); dst = _out(n)
    _check(_lib.qtl_eom(_ptr(h), _ptr(l), _ptr(v), n, _ptr(dst), length, 1e9))
    return _wrap(dst, idx, f"EOM_{length}", "volume", offset)


def pvo(volume: object, fast: int = 12, slow: int = 26, signal: int = 9,
        offset: int = 0, **kwargs) -> object:
    """Percentage Volume Oscillator -> (pvo, signal, histogram) or DataFrame."""
    fast = int(fast); slow = int(slow); signal = int(signal); offset = int(offset)
    v, idx = _arr(volume); n = len(v)
    pvo_out = _out(n); sig = _out(n); hist = _out(n)
    _check(_lib.qtl_pvo(_ptr(v), n, _ptr(pvo_out), _ptr(sig), _ptr(hist), fast, slow, signal))
    return _wrap_multi(
        {f"PVO_{fast}_{slow}_{signal}": pvo_out, f"PVOs_{fast}_{slow}_{signal}": sig, f"PVOh_{fast}_{slow}_{signal}": hist},
        idx, "volume", offset)
'''


# ── statistics.py ──
statistics_additions = '''

def zscore(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Z-Score."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_zscore(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ZSCORE_{length}", "statistics", offset)


def cma(close: object, offset: int = 0, **kwargs) -> object:
    """Cumulative Moving Average."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cma(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "CMA", "statistics", offset)


def entropy(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Shannon Entropy."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_entropy(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ENTROPY_{length}", "statistics", offset)


def correlation(x: object, y: object, length: int = 20,
                offset: int = 0, **kwargs) -> object:
    """Pearson Correlation."""
    length = int(length); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_correlation(_ptr(xarr), _ptr(yarr), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CORR_{length}", "statistics", offset)


def covariance(x: object, y: object, length: int = 20,
               is_sample: bool = True, offset: int = 0, **kwargs) -> object:
    """Covariance."""
    length = int(length); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_covariance(_ptr(xarr), _ptr(yarr), n, _ptr(dst), length, int(is_sample)))
    return _wrap(dst, idx, f"COV_{length}", "statistics", offset)


def cointegration(x: object, y: object, length: int = 20,
                  offset: int = 0, **kwargs) -> object:
    """Cointegration."""
    length = int(length); offset = int(offset)
    xarr, idx = _arr(x); yarr, _ = _arr(y)
    n = len(xarr); dst = _out(n)
    _check(_lib.qtl_cointegration(_ptr(xarr), _ptr(yarr), n, _ptr(dst), length))
    return _wrap(dst, idx, f"COINT_{length}", "statistics", offset)
'''


# ── errors.py ──
errors_additions = '''

def mse(actual: object, predicted: object, length: int = 20,
        offset: int = 0, **kwargs) -> object:
    """Mean Squared Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_mse(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MSE_{length}", "errors", offset)


def rmse(actual: object, predicted: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Root Mean Squared Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_rmse(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"RMSE_{length}", "errors", offset)


def mae(actual: object, predicted: object, length: int = 20,
        offset: int = 0, **kwargs) -> object:
    """Mean Absolute Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_mae(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MAE_{length}", "errors", offset)


def mape(actual: object, predicted: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Mean Absolute Percentage Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_mape(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MAPE_{length}", "errors", offset)
'''


# ── filters.py ──
filters_additions = '''

def bessel(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Bessel Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bessel(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"BESSEL_{length}", "filters", offset)


def butter2(close: object, length: int = 14, gain: float = 1.0,
            offset: int = 0, **kwargs) -> object:
    """2nd-order Butterworth."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_butter2(_ptr(src), n, _ptr(dst), length, float(gain)))
    return _wrap(dst, idx, f"BUTTER2_{length}", "filters", offset)


def butter3(close: object, length: int = 14, gain: float = 1.0,
            offset: int = 0, **kwargs) -> object:
    """3rd-order Butterworth."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_butter3(_ptr(src), n, _ptr(dst), length, float(gain)))
    return _wrap(dst, idx, f"BUTTER3_{length}", "filters", offset)


def cheby1(close: object, length: int = 14, ripple: float = 0.5,
           offset: int = 0, **kwargs) -> object:
    """Chebyshev Type I."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cheby1(_ptr(src), n, _ptr(dst), length, float(ripple)))
    return _wrap(dst, idx, f"CHEBY1_{length}", "filters", offset)


def cheby2(close: object, length: int = 14, ripple: float = 0.5,
           offset: int = 0, **kwargs) -> object:
    """Chebyshev Type II."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cheby2(_ptr(src), n, _ptr(dst), length, float(ripple)))
    return _wrap(dst, idx, f"CHEBY2_{length}", "filters", offset)


def elliptic(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Elliptic (Cauer) Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_elliptic(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"ELLIPTIC_{length}", "filters", offset)


def edcf(close: object, length: int = 14, offset: int = 0, **kwargs) -> object:
    """Ehlers Distance Coefficient Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_edcf(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"EDCF_{length}", "filters", offset)


def bpf(close: object, length: int = 14, bandwidth: int = 5,
        offset: int = 0, **kwargs) -> object:
    """Bandpass Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bpf(_ptr(src), n, _ptr(dst), length, int(bandwidth)))
    return _wrap(dst, idx, f"BPF_{length}", "filters", offset)


def alaguerre(close: object, length: int = 20, order: int = 5,
              offset: int = 0, **kwargs) -> object:
    """Adaptive Laguerre Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_alaguerre(_ptr(src), n, _ptr(dst), length, int(order)))
    return _wrap(dst, idx, f"ALAGUERRE_{length}", "filters", offset)


def bilateral(close: object, length: int = 14, sigma_s: float = 0.5,
              sigma_r: float = 1.0, offset: int = 0, **kwargs) -> object:
    """Bilateral Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_bilateral(_ptr(src), n, _ptr(dst), length, float(sigma_s), float(sigma_r)))
    return _wrap(dst, idx, f"BILATERAL_{length}", "filters", offset)


def baxterking(close: object, length: int = 12, min_period: int = 6,
               max_period: int = 32, offset: int = 0, **kwargs) -> object:
    """Baxter-King Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_baxterking(_ptr(src), n, _ptr(dst), length, int(min_period), int(max_period)))
    return _wrap(dst, idx, f"BAXTERKING_{length}", "filters", offset)


def cfitz(close: object, length: int = 6, bw_period: int = 32,
          offset: int = 0, **kwargs) -> object:
    """Christiano-Fitzgerald Filter."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cfitz(_ptr(src), n, _ptr(dst), length, int(bw_period)))
    return _wrap(dst, idx, f"CFITZ_{length}", "filters", offset)
'''


# ── cycles.py ──
cycles_additions = '''

def cg(close: object, length: int = 10, offset: int = 0, **kwargs) -> object:
    """Center of Gravity."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cg(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CG_{length}", "cycles", offset)


def dsp(close: object, length: int = 20, offset: int = 0, **kwargs) -> object:
    """Dominant Cycle Period (DSP)."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dsp(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"DSP_{length}", "cycles", offset)


def ccor(close: object, length: int = 20, alpha: float = 0.07,
         offset: int = 0, **kwargs) -> object:
    """Circular Correlation."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ccor(_ptr(src), n, _ptr(dst), length, float(alpha)))
    return _wrap(dst, idx, f"CCOR_{length}", "cycles", offset)


def ebsw(close: object, hp_length: int = 40, ssf_length: int = 10,
         offset: int = 0, **kwargs) -> object:
    """Even Better Sinewave."""
    hp_length = int(hp_length); ssf_length = int(ssf_length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_ebsw(_ptr(src), n, _ptr(dst), hp_length, ssf_length))
    return _wrap(dst, idx, f"EBSW_{hp_length}", "cycles", offset)


def eacp(close: object, min_period: int = 8, max_period: int = 48,
         avg_length: int = 3, enhance: int = 1,
         offset: int = 0, **kwargs) -> object:
    """Ehlers Autocorrelation Periodogram."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_eacp(_ptr(src), n, _ptr(dst), int(min_period), int(max_period), int(avg_length), int(enhance)))
    return _wrap(dst, idx, f"EACP_{min_period}_{max_period}", "cycles", offset)
'''


# ── numerics.py ──
numerics_additions = '''

def change(close: object, length: int = 1, offset: int = 0, **kwargs) -> object:
    """Price Change."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_change(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"CHANGE_{length}", "numerics", offset)


def exptrans(close: object, offset: int = 0, **kwargs) -> object:
    """Exponential Transform."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_exptrans(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, "EXPTRANS", "numerics", offset)


def betadist(close: object, length: int = 50, alpha: float = 2.0,
             beta: float = 2.0, offset: int = 0, **kwargs) -> object:
    """Beta Distribution."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_betadist(_ptr(src), n, _ptr(dst), length, float(alpha), float(beta)))
    return _wrap(dst, idx, f"BETADIST_{length}", "numerics", offset)


def expdist(close: object, length: int = 50, lam: float = 3.0,
            offset: int = 0, **kwargs) -> object:
    """Exponential Distribution."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_expdist(_ptr(src), n, _ptr(dst), length, float(lam)))
    return _wrap(dst, idx, f"EXPDIST_{length}", "numerics", offset)


def binomdist(close: object, length: int = 50, trials: int = 20,
              threshold: int = 10, offset: int = 0, **kwargs) -> object:
    """Binomial Distribution."""
    length = int(length); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_binomdist(_ptr(src), n, _ptr(dst), length, int(trials), int(threshold)))
    return _wrap(dst, idx, f"BINOMDIST_{length}", "numerics", offset)


def cwt(close: object, scale: float = 10.0, omega: float = 6.0,
        offset: int = 0, **kwargs) -> object:
    """Continuous Wavelet Transform."""
    offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_cwt(_ptr(src), n, _ptr(dst), float(scale), float(omega)))
    return _wrap(dst, idx, "CWT", "numerics", offset)


def dwt(close: object, length: int = 4, levels: int = 0,
        offset: int = 0, **kwargs) -> object:
    """Discrete Wavelet Transform."""
    length = int(length); levels = int(levels); offset = int(offset)
    src, idx = _arr(close); n = len(src); dst = _out(n)
    _check(_lib.qtl_dwt(_ptr(src), n, _ptr(dst), length, levels))
    return _wrap(dst, idx, f"DWT_{length}", "numerics", offset)
'''


# Now apply all additions
additions = {
    "core.py": (core_additions, ["avgprice", "medprice", "typprice", "midbody"]),
    "momentum.py": (momentum_additions, ["rsi", "roc", "mom", "cmo", "tsi", "apo", "bias", "cfo", "cfb", "asi"]),
    "oscillators.py": (oscillators_additions, ["fisher", "fisher04", "dpo", "trix", "inertia", "rsx", "er", "cti", "reflex", "trendflex", "kri", "psl", "deco", "dosc", "dymoi", "crsi", "bbb", "bbi", "dem", "brar"]),
    "trends_fir.py": (trends_fir_additions, ["sma", "wma", "hma", "trima", "swma", "dwma", "blma", "alma", "lsma", "sgma", "sinema", "hanma", "parzen", "tsf", "conv", "bwma", "crma", "sp15", "tukey_w", "rain", "afirma"]),
    "trends_iir.py": (trends_iir_additions, ["ema", "ema_alpha", "dema", "dema_alpha", "tema", "lema", "hema", "ahrens", "decycler", "dsma", "gdema", "coral", "agc", "ccyc"]),
    "channels.py": (channels_additions, ["bbands", "aberr", "atrbands", "apchannel"]),
    "volatility.py": (volatility_additions, ["tr", "bbw", "bbwn", "bbwp", "stddev", "variance", "etherm", "ccv", "cv", "cvi", "ewma"]),
    "volume.py": (volume_additions, ["obv", "pvt", "pvr", "vf", "nvi", "pvi", "tvi", "pvd", "vwma", "evwma", "efi", "aobv", "mfi", "cmf", "eom", "pvo"]),
    "statistics.py": (statistics_additions, ["zscore", "cma", "entropy", "correlation", "covariance", "cointegration"]),
    "errors.py": (errors_additions, ["mse", "rmse", "mae", "mape"]),
    "filters.py": (filters_additions, ["bessel", "butter2", "butter3", "cheby1", "cheby2", "elliptic", "edcf", "bpf", "alaguerre", "bilateral", "baxterking", "cfitz"]),
    "cycles.py": (cycles_additions, ["cg", "dsp", "ccor", "ebsw", "eacp"]),
    "numerics.py": (numerics_additions, ["change", "exptrans", "betadist", "expdist", "binomdist", "cwt", "dwt"]),
}

total_added = 0
for filename, (code, funcnames) in additions.items():
    filepath = os.path.join(PKG, filename)
    content = read(filepath)
    
    # Check which functions are already defined
    missing = [f for f in funcnames if f"\ndef {f}(" not in content]
    if not missing:
        print(f"  {filename}: all {len(funcnames)} functions already present")
        continue
    
    # Update __all__ to include new functions
    # Find __all__ closing bracket
    import re
    all_match = re.search(r'__all__\s*=\s*\[([^\]]*)\]', content, re.DOTALL)
    if all_match:
        existing_all = all_match.group(1)
        existing_names = [s.strip().strip('"').strip("'") for s in existing_all.split(",") if s.strip().strip('"').strip("'")]
        new_names = [f for f in funcnames if f not in existing_names]
        if new_names:
            all_entries = existing_names + new_names
            new_all = "__all__ = [\n" + "".join(f'    "{n}",\n' for n in all_entries) + "]"
            content = content[:all_match.start()] + new_all + content[all_match.end():]
    
    # Append the wrapper code
    content += "\n" + code.strip() + "\n"
    
    write(filepath, content)
    total_added += len(missing)
    print(f"  {filename}: added {len(missing)} functions: {', '.join(missing)}")

print(f"\n  Total wrappers added: {total_added}")


# ═══════════════════════════════════════════════════════════════════════════
#  Step 3: Rewrite indicators.py as thin re-export
# ═══════════════════════════════════════════════════════════════════════════
print("\nStep 3: Rewriting indicators.py as re-export module ...")

CATEGORY_MODULES = [
    "channels", "core", "cycles", "dynamics", "errors", "filters",
    "momentum", "numerics", "oscillators", "reversals", "statistics",
    "trends_fir", "trends_iir", "volatility", "volume",
]

indicators_content = '''"""High-level indicator wrappers for quantalib.

This module re-exports all indicator functions from per-category submodules.
Each function accepts numpy arrays (or pandas Series / DataFrame) and
returns the same type.

Category submodules:
    quantalib.channels     — Bollinger Bands, Keltner, Donchian, etc.
    quantalib.core         — Price transforms (avgprice, medprice, etc.)
    quantalib.cycles       — Hilbert, Sinewave, CG, DSP, etc.
    quantalib.dynamics     — ADX, Ichimoku, Supertrend, etc.
    quantalib.errors       — MSE, RMSE, MAE, MAPE, Huber, etc.
    quantalib.filters      — Butterworth, Chebyshev, Kalman, etc.
    quantalib.momentum     — RSI, MACD, ROC, MOM, etc.
    quantalib.numerics     — FFT, sigmoid, slope, distributions, etc.
    quantalib.oscillators  — Stochastic, Fisher, Williams %R, etc.
    quantalib.reversals    — Pivot points, PSAR, fractals, etc.
    quantalib.statistics   — Z-score, correlation, linreg, etc.
    quantalib.trends_fir   — SMA, WMA, HMA, ALMA, etc.
    quantalib.trends_iir   — EMA, DEMA, TEMA, JMA, KAMA, etc.
    quantalib.volatility   — ATR, TR, Bollinger Width, etc.
    quantalib.volume       — OBV, VWAP, MFI, CMF, etc.
"""
from __future__ import annotations

'''

for mod in CATEGORY_MODULES:
    indicators_content += f"from .{mod} import *  # noqa: F401, F403\n"

write(os.path.join(PKG, "indicators.py"), indicators_content)


# ═══════════════════════════════════════════════════════════════════════════
#  Step 4: Update __init__.py
# ═══════════════════════════════════════════════════════════════════════════
print("\nStep 4: Updating __init__.py ...")

init_content = '''"""quantalib — Python wrapper for QuanTAlib NativeAOT exports.

Usage::

    import quantalib as qtl

    result = qtl.sma(close_array, length=14)
    result = qtl.bbands(close_array, length=20, std=2.0)
"""
from __future__ import annotations

from pathlib import Path

from ._loader import load_native_library
from . import indicators
from .indicators import *  # noqa: F401, F403 — re-export all indicator functions

# Re-export per-category submodules for direct access
from . import (  # noqa: F401
    channels,
    core,
    cycles,
    dynamics,
    errors,
    filters,
    momentum,
    numerics,
    oscillators,
    reversals,
    statistics,
    trends_fir,
    trends_iir,
    volatility,
    volume,
)

from ._compat import ALIASES, get_compat
from ._bridge import (
    QtlError,
    QtlNullPointerError,
    QtlInvalidLengthError,
    QtlInvalidParamError,
    QtlInternalError,
)

__all__ = [
    "load_native_library",
    "indicators",
    "channels",
    "core",
    "cycles",
    "dynamics",
    "errors",
    "filters",
    "momentum",
    "numerics",
    "oscillators",
    "reversals",
    "statistics",
    "trends_fir",
    "trends_iir",
    "volatility",
    "volume",
    "ALIASES",
    "get_compat",
    "QtlError",
    "QtlNullPointerError",
    "QtlInvalidLengthError",
    "QtlInvalidParamError",
    "QtlInternalError",
]


def _resolve_version() -> str:
    version_file = Path(__file__).resolve().parents[2] / "lib" / "VERSION"
    if version_file.exists():
        version = version_file.read_text(encoding="utf-8").strip()
        if version:
            return version
    return "0.0.0"


__version__ = _resolve_version()
'''

write(os.path.join(PKG, "__init__.py"), init_content)


# ═══════════════════════════════════════════════════════════════════════════
#  Step 5: Count & verify
# ═══════════════════════════════════════════════════════════════════════════
print("\nStep 5: Verification counts ...")

import re as re2

total_funcs = 0
for mod in CATEGORY_MODULES:
    filepath = os.path.join(PKG, f"{mod}.py")
    content = read(filepath)
    funcs = re2.findall(r'^def (\w+)\(', content, re2.MULTILINE)
    # Exclude private helpers
    public = [f for f in funcs if not f.startswith('_')]
    total_funcs += len(public)
    print(f"  {mod:15s}: {len(public):3d} functions")

print(f"  {'TOTAL':15s}: {total_funcs:3d} functions")
print("\nDone!")
