#!/usr/bin/env python3
"""Generate per-category Python indicator modules from Exports.Generated.cs.

Reads the C# exports file and the lib/ directory structure to produce:
  - python/quantalib/_helpers.py         (shared wrapper infrastructure)
  - python/quantalib/_bridge.py          (ALL ctypes bindings)
  - python/quantalib/{category}.py       (one per lib/ category)
  - python/quantalib/indicators.py       (re-exports everything)
  - python/quantalib/__init__.py         (package root)

Usage:
    python python/tools/generate_category_modules.py
"""
from __future__ import annotations

import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
LIB_DIR = REPO_ROOT / "lib"
EXPORTS_CS = REPO_ROOT / "python" / "src" / "Exports.Generated.cs"
OUT_DIR = REPO_ROOT / "python" / "quantalib"

# ---------------------------------------------------------------------------
#  Category mapping: lib/ subdirectory → Python module name
# ---------------------------------------------------------------------------
CATEGORY_PY_NAME: dict[str, str] = {
    "channels": "channels",
    "core": "core",
    "cycles": "cycles",
    "dynamics": "dynamics",
    "errors": "errors",
    "filters": "filters",
    "forecasts": "forecasts",
    "momentum": "momentum",
    "numerics": "numerics",
    "oscillators": "oscillators",
    "reversals": "reversals",
    "statistics": "statistics_",   # avoid shadowing stdlib 'statistics'
    "trends_FIR": "trends_fir",
    "trends_IIR": "trends_iir",
    "volatility": "volatility",
    "volume": "volume",
}

# Subdirectories in lib/core/ that are NOT indicators (infrastructure)
CORE_SKIP = {
    "collections", "ringbuffer", "simd", "tbar", "tbarseries",
    "tests", "tseries", "tvalue", "_index.md",
}

# Export names that don't map cleanly to a lib/ indicator dir
EXPORT_RENAMES: dict[str, str] = {
    "htdcperiod": "ht_dcperiod",
    "htdcphase": "ht_dcphase",
    "htphasor": "ht_phasor",
    "htsine": "ht_sine",
    "httrendmode": "ht_trendmode",
    "htit": "htit",
    "ttmsqueeze": "ttm_squeeze",
    "ttmtrend": "ttm_trend",
    "ttmscalper": "ttm_scalper",
    "ttmwave": "ttm_wave",
    "ttmlrc": "ttm_lrc",
}


# ---------------------------------------------------------------------------
#  Data model
# ---------------------------------------------------------------------------
@dataclass
class ExportInfo:
    """Parsed info for a single [UnmanagedCallersOnly] export."""
    entry_name: str          # e.g. "qtl_sma"
    func_name: str           # e.g. "sma"
    cs_params: list[tuple[str, str]]  # [(type, name), ...]
    category: str = ""       # resolved lib/ category
    lib_indicator: str = ""  # indicator dir name in lib/

    @property
    def py_module(self) -> str:
        return CATEGORY_PY_NAME.get(self.category, self.category)


# ---------------------------------------------------------------------------
#  Step 1: Build category lookup  {indicator_name → category}
# ---------------------------------------------------------------------------
def build_category_map() -> dict[str, str]:
    """Scan lib/ subdirectories to build indicator→category mapping."""
    cat_map: dict[str, str] = {}

    for cat_dir in sorted(LIB_DIR.iterdir()):
        if not cat_dir.is_dir():
            continue
        cat_name = cat_dir.name
        if cat_name in ("bin", "obj", "feeds") or cat_name.startswith("_") or cat_name.startswith("."):
            continue

        for ind_dir in sorted(cat_dir.iterdir()):
            if not ind_dir.is_dir():
                continue
            ind_name = ind_dir.name
            if ind_name.startswith("_") or ind_name.startswith("."):
                continue
            if cat_name == "core" and ind_name in CORE_SKIP:
                continue

            # Normalize: indicator directory names are lowercase
            cat_map[ind_name.lower()] = cat_name

    return cat_map


# ---------------------------------------------------------------------------
#  Step 2: Parse Exports.Generated.cs
# ---------------------------------------------------------------------------
RE_ENTRY = re.compile(
    r'\[UnmanagedCallersOnly\(EntryPoint\s*=\s*"(qtl_\w+)"\)\]'
)
RE_FUNC = re.compile(
    r'public\s+static\s+int\s+\w+\(([^)]*)\)'
)


def parse_cs_param(raw: str) -> tuple[str, str]:
    """Parse 'double* source' → ('double*', 'source')."""
    raw = raw.strip()
    parts = raw.rsplit(None, 1)
    if len(parts) == 2:
        return (parts[0], parts[1])
    return (raw, "")


def parse_exports(cs_path: Path) -> list[ExportInfo]:
    """Parse all [UnmanagedCallersOnly] exports from the C# file."""
    text = cs_path.read_text(encoding="utf-8")
    lines = text.splitlines()
    exports: list[ExportInfo] = []

    i = 0
    while i < len(lines):
        m = RE_ENTRY.search(lines[i])
        if m:
            entry_name = m.group(1)  # e.g. "qtl_sma"
            func_name = entry_name[4:]  # strip "qtl_"

            # Find the function signature (may be on next line)
            for j in range(i + 1, min(i + 5, len(lines))):
                fm = RE_FUNC.search(lines[j])
                if fm:
                    raw_params = fm.group(1)
                    params = [parse_cs_param(p) for p in raw_params.split(",")]
                    exports.append(ExportInfo(
                        entry_name=entry_name,
                        func_name=func_name,
                        cs_params=params,
                    ))
                    break
            i = j + 1
        else:
            i += 1

    return exports


# ---------------------------------------------------------------------------
#  Step 3: Resolve categories
# ---------------------------------------------------------------------------
def resolve_categories(exports: list[ExportInfo], cat_map: dict[str, str]) -> None:
    """Assign each export to its lib/ category."""
    for exp in exports:
        name = exp.func_name

        # Check rename mapping first
        mapped = EXPORT_RENAMES.get(name, name)

        if mapped in cat_map:
            exp.category = cat_map[mapped]
            exp.lib_indicator = mapped
        else:
            # Try underscore variants
            for variant in [mapped.replace("_", ""), mapped]:
                if variant in cat_map:
                    exp.category = cat_map[variant]
                    exp.lib_indicator = variant
                    break

        # Special cases
        if name == "ema_alpha":
            exp.category = "trends_IIR"
            exp.lib_indicator = "ema"
        elif name == "dema_alpha":
            exp.category = "trends_IIR"
            exp.lib_indicator = "dema"
        elif name in ("wclprice", "midpoint", "midprice", "medprice",
                       "avgprice", "typprice", "midbody", "ha"):
            exp.category = "core"
            exp.lib_indicator = name
        elif name == "skeleton_noop":
            exp.category = "_internal"

        if not exp.category and name != "skeleton_noop":
            print(f"  WARNING: No category for export '{name}'", file=sys.stderr)


# ---------------------------------------------------------------------------
#  Step 4: Classify parameter patterns for ctypes/Python wrappers
# ---------------------------------------------------------------------------

def classify_params(exp: ExportInfo) -> dict:
    """Classify the export's parameter pattern for code generation."""
    params = exp.cs_params
    ptypes = [p[0] for p in params]
    pnames = [p[1] for p in params]

    info: dict = {
        "inputs": [],       # list of (cs_type, name, py_name)
        "outputs": [],      # list of (cs_type, name, py_name)
        "int_params": [],   # list of (name, py_name, default)
        "double_params": [], # list of (name, py_name, default)
        "n_param": None,     # name of the length param
        "pattern": "custom",
        "argtypes": [],
    }

    # Identify inputs (double*) that appear before outputs
    # Heuristic: inputs come before 'n', outputs after
    n_idx = None
    for i, (t, n) in enumerate(params):
        if t == "int" and n in ("n", "length") and n_idx is None:
            # Special: some have 'n' later
            pass
        if n == "n" and t == "int":
            n_idx = i
            break

    if n_idx is None:
        # n might be at different position, find it
        for i, (t, n) in enumerate(params):
            if t == "int" and n == "n":
                n_idx = i
                break

    return info


# ---------------------------------------------------------------------------
#  Step 5: Generate ctypes argtypes string
# ---------------------------------------------------------------------------

def cs_type_to_ctypes(cs_type: str) -> str:
    """Convert C# parameter type to ctypes constant."""
    mapping = {
        "double*": "_dp",
        "int": "_ci",
        "double": "_cd",
        "int*": "_ip",
        "long*": "_lp",
    }
    return mapping.get(cs_type, f"# UNKNOWN: {cs_type}")


def gen_argtypes(exp: ExportInfo) -> str:
    """Generate the ctypes argtypes list for a binding."""
    parts = [cs_type_to_ctypes(t) for t, _ in exp.cs_params]
    return "[" + ", ".join(parts) + "]"


# ---------------------------------------------------------------------------
#  Step 6: Generate _bridge.py
# ---------------------------------------------------------------------------

def gen_bridge(exports: list[ExportInfo], by_cat: dict[str, list[ExportInfo]]) -> str:
    """Generate the complete _bridge.py file."""

    lines = [
        '"""Low-level ctypes bindings for every quantalib NativeAOT export.',
        '',
        'Auto-generated by generate_category_modules.py — DO NOT EDIT.',
        '',
        'Each native function is bound via ``_bind`` at module load. If the shared',
        'library was compiled without a particular export the binding is silently',
        'skipped (the corresponding ``HAS_*`` flag stays False).',
        '"""',
        'from __future__ import annotations',
        '',
        'import ctypes',
        'from ctypes import c_double, c_int, POINTER',
        'from typing import Final',
        '',
        'from ._loader import load_native_library',
        '',
        '# ---------------------------------------------------------------------------',
        '#  Status codes  (mirror StatusCodes.cs)',
        '# ---------------------------------------------------------------------------',
        'QTL_OK: Final[int] = 0',
        'QTL_ERR_NULL_PTR: Final[int] = 1',
        'QTL_ERR_INVALID_LENGTH: Final[int] = 2',
        'QTL_ERR_INVALID_PARAM: Final[int] = 3',
        'QTL_ERR_INTERNAL: Final[int] = 4',
        '',
        '',
        'class QtlError(Exception):',
        '    """Base exception for quantalib native errors."""',
        '',
        '',
        'class QtlNullPointerError(QtlError):',
        '    pass',
        '',
        '',
        'class QtlInvalidLengthError(QtlError):',
        '    pass',
        '',
        '',
        'class QtlInvalidParamError(QtlError):',
        '    pass',
        '',
        '',
        'class QtlInternalError(QtlError):',
        '    pass',
        '',
        '',
        '_STATUS_MAP: dict[int, type[QtlError]] = {',
        '    QTL_ERR_NULL_PTR: QtlNullPointerError,',
        '    QTL_ERR_INVALID_LENGTH: QtlInvalidLengthError,',
        '    QTL_ERR_INVALID_PARAM: QtlInvalidParamError,',
        '    QTL_ERR_INTERNAL: QtlInternalError,',
        '}',
        '',
        '',
        'def _check(status: int) -> None:',
        '    """Raise if *status* is not QTL_OK."""',
        '    if status == QTL_OK:',
        '        return',
        '    exc_type = _STATUS_MAP.get(status, QtlError)',
        '    raise exc_type(f"quantalib native call failed (status={status})")',
        '',
        '',
        '# ---------------------------------------------------------------------------',
        '#  Load native library',
        '# ---------------------------------------------------------------------------',
        '_lib = load_native_library()',
        '',
        '# Shorthand type aliases',
        '_dp = POINTER(c_double)  # double*',
        '_ip = POINTER(c_int)     # int*',
        '_lp = POINTER(ctypes.c_long)  # long*',
        '_ci = c_int',
        '_cd = c_double',
        '',
        '',
        'def _bind(name: str, argtypes: list[object]) -> bool:',
        '    """Bind a single native function. Returns True if found."""',
        '    fn = getattr(_lib, name, None)',
        '    if fn is None:',
        '        return False',
        '    fn.argtypes = argtypes',
        '    fn.restype = _ci',
        '    return True',
        '',
        '',
        '# ---------------------------------------------------------------------------',
        '#  Health check',
        '# ---------------------------------------------------------------------------',
        'HAS_SKELETON = _bind("qtl_skeleton_noop", [_dp, _ci, _dp])',
        '',
    ]

    # Category order
    CAT_ORDER = [
        "core", "momentum", "oscillators", "trends_FIR", "trends_IIR",
        "channels", "volatility", "volume", "statistics", "errors",
        "filters", "cycles", "dynamics", "numerics", "reversals", "forecasts",
    ]

    cat_labels = {
        "core": "Core",
        "momentum": "Momentum",
        "oscillators": "Oscillators",
        "trends_FIR": "Trends — FIR",
        "trends_IIR": "Trends — IIR",
        "channels": "Channels",
        "volatility": "Volatility",
        "volume": "Volume",
        "statistics": "Statistics",
        "errors": "Errors",
        "filters": "Filters",
        "cycles": "Cycles",
        "dynamics": "Dynamics",
        "numerics": "Numerics",
        "reversals": "Reversals",
        "forecasts": "Forecasts",
    }

    for cat in CAT_ORDER:
        if cat not in by_cat:
            continue
        exps = by_cat[cat]
        label = cat_labels.get(cat, cat)
        lines.append(f'# {"═" * 75}')
        lines.append(f'#  {label}')
        lines.append(f'# {"═" * 75}')

        for exp in sorted(exps, key=lambda e: e.func_name):
            varname = f"HAS_{exp.func_name.upper()}"
            argtypes = gen_argtypes(exp)
            lines.append(f'{varname} = _bind("{exp.entry_name}", {argtypes})')

        lines.append('')

    return "\n".join(lines)


# ---------------------------------------------------------------------------
#  Step 7: Generate _helpers.py
# ---------------------------------------------------------------------------
def gen_helpers() -> str:
    return '''"""Shared wrapper helpers for quantalib indicator modules.

Auto-generated by generate_category_modules.py — DO NOT EDIT.
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
#  Generic pattern helpers
# ═══════════════════════════════════════════════════════════════════════════

def _pa(
    fn_name: str, close: object, length: int, offset: int,
    default_length: int, label: str, category: str,
) -> object:
    """Generic Pattern A wrapper: single-input + period."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(src), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", category, offset)


def _pa3(
    fn_name: str, close: object, offset: int,
    label: str, category: str,
) -> object:
    """Generic Pattern A3 wrapper: single-input, no params."""
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(close)
    n = len(src)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(src), n, _ptr(dst)))
    return _wrap(dst, idx, label, category, offset)


def _pf(
    fn_name: str, actual: object, predicted: object,
    length: int, offset: int, default_length: int,
    label: str, category: str,
) -> object:
    """Generic Pattern F wrapper: actual+predicted+period."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    a, idx = _arr(actual)
    p, _ = _arr(predicted)
    n = len(a)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", category, offset)


def _pg(
    fn_name: str, close: object, volume: object,
    offset: int, label: str, category: str,
) -> object:
    """Pattern G: source+volume, no period."""
    offset = int(offset) if offset is not None else 0
    c, idx = _arr(close)
    v, _ = _arr(volume)
    n = len(c)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(c), _ptr(v), n, _ptr(dst)))
    return _wrap(dst, idx, label, category, offset)


def _pg2(
    fn_name: str, close: object, volume: object, length: int,
    offset: int, default_length: int, label: str, category: str,
) -> object:
    """Pattern G2: source+volume+period."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    c, idx = _arr(close)
    v, _ = _arr(volume)
    n = len(c)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(c), _ptr(v), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", category, offset)


def _ph(
    fn_name: str, x: object, y: object, length: int,
    offset: int, default_length: int, label: str, category: str,
) -> object:
    """Pattern H: X+Y+period."""
    length = int(length) if length is not None else default_length
    offset = int(offset) if offset is not None else 0
    xarr, idx = _arr(x)
    yarr, _ = _arr(y)
    n = len(xarr)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(xarr), _ptr(yarr), n, _ptr(dst), length))
    return _wrap(dst, idx, f"{label}_{length}", category, offset)


def _ohlcv_bars_period(
    fn_name: str, open: object, high: object, low: object,
    close: object, volume: object, period: int,
    offset: int, default_period: int, label: str, category: str,
) -> object:
    """OHLCV bars + period → single output (BuildBars pattern)."""
    period = int(period) if period is not None else default_period
    offset = int(offset) if offset is not None else 0
    o, idx = _arr(open)
    h, _ = _arr(high)
    l, _ = _arr(low)
    c, _ = _arr(close)
    v, _ = _arr(volume)
    n = len(o)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(
        _ptr(o), _ptr(h), _ptr(l), _ptr(c), _ptr(v), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"{label}_{period}", category, offset)


def _hlc_period(
    fn_name: str, high: object, low: object, close: object,
    period: int, offset: int, default_period: int,
    label: str, category: str,
) -> object:
    """HLC + period → single output."""
    period = int(period) if period is not None else default_period
    offset = int(offset) if offset is not None else 0
    h, idx = _arr(high)
    l, _ = _arr(low)
    c, _ = _arr(close)
    n = len(h)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(
        _ptr(h), _ptr(l), _ptr(c), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"{label}_{period}", category, offset)


def _src_period(
    fn_name: str, source: object, period: int,
    offset: int, default_period: int, label: str, category: str,
) -> object:
    """source + period → single output (BuildSeries pattern, src,period,n,dst)."""
    period = int(period) if period is not None else default_period
    offset = int(offset) if offset is not None else 0
    src, idx = _arr(source)
    n = len(src)
    dst = _out(n)
    _check(getattr(_lib, fn_name)(_ptr(src), period, n, _ptr(dst)))
    return _wrap(dst, idx, f"{label}_{period}", category, offset)
'''


# ---------------------------------------------------------------------------
#  Step 8: Build per-category wrapper functions
# ---------------------------------------------------------------------------
# We derive wrapper signatures from the C# export signatures.

# Manual mappings for export names that need specific Python wrapper treatment
# This defines the "known" wrappers. Anything not here gets auto-generated.

# Map of lib/ directory name → default period for Pattern A indicators
DEFAULT_PERIODS: dict[str, int] = {
    # Trends FIR
    "sma": 10, "wma": 10, "hma": 9, "trima": 10, "swma": 10, "dwma": 10,
    "blma": 10, "lsma": 25, "sgma": 10, "sinema": 10, "hanma": 10,
    "parzen": 10, "tsf": 14, "sp15": 15, "tukey_w": 10, "rain": 10,
    "fwma": 10, "gwma": 10, "hamma": 10, "hend": 10, "ilrs": 10,
    "kaiser": 10, "lanczos": 10, "nlma": 10, "nyqma": 10, "pma": 10,
    "pwma": 10, "qrma": 10, "rwma": 10, "bwma": 10,
    # Trends IIR
    "ema": 10, "dema": 10, "tema": 10, "lema": 10, "hema": 10,
    "ahrens": 10, "decycler": 20, "frama": 10, "hwma": 10,
    "jma": 10, "kama": 10, "ltma": 10, "mama": 10, "mavp": 10,
    "mcnma": 10, "mgdi": 10, "mma": 10, "nma": 10, "qema": 10,
    "rema": 10, "rgma": 10, "rma": 10, "t3": 10, "trama": 10,
    "vidya": 10, "zldema": 10, "zlema": 10, "zltema": 10,
    "adxvma": 14, "vama": 14, "yzvama": 14,
    # Momentum
    "rsi": 14, "roc": 10, "mom": 10, "cmo": 14, "bias": 26,
    "cfo": 14, "rsx": 14, "pmo": 35,
    "rocp": 10, "rocr": 10, "vel": 10,
    # Oscillators
    "fisher": 9, "fisher04": 9, "dpo": 20, "trix": 18, "inertia": 20,
    "er": 10, "cti": 12, "reflex": 20, "trendflex": 20, "kri": 20,
    "psl": 12, "lrsi": 14,
    # Volatility
    "bbw": 20, "stddev": 20, "variance": 20, "natr": 14, "massi": 14,
    "ui": 14, "jvolty": 14, "jvoltyn": 14, "rsv": 14, "rv": 14,
    "rvi": 14, "vov": 14, "vr": 14,
    # Cycles
    "cg": 10, "dsp": 20, "ccor": 20,
    # Statistics
    "zscore": 20, "entropy": 10, "geomean": 10, "harmean": 10,
    "hurst": 100, "iqr": 20, "kurtosis": 20, "linreg": 14,
    "meandev": 20, "median": 20, "mode": 20, "percentile": 20,
    "polyfit": 20, "quantile": 20, "skew": 20, "spearman": 20,
    "stddev": 20, "stderr": 20, "sum": 20, "theil": 20,
    "trim": 20, "wavg": 20, "wins": 20, "ztest": 20,
    "kendall": 20, "pacf": 20,
    # Filters
    "bessel": 14, "butter2": 14, "butter3": 14, "cheby1": 14,
    "cheby2": 14, "elliptic": 14, "edcf": 14, "bpf": 14,
    "loess": 14, "nw": 14, "rmed": 14, "sgf": 14, "spbf": 14,
    "ssf2": 14, "ssf3": 14, "usf": 14, "voss": 14,
    "wavelet": 14, "wiener": 14,
    # Numerics
    "change": 1, "highest": 14, "lowest": 14, "slope": 14,
    "accel": 0, "jerk": 0,
    # Errors (all pattern F, default 20)
    "mse": 20, "rmse": 20, "mae": 20, "mape": 20, "smape": 20,
    "msle": 20, "rmsle": 20, "me": 20, "mpe": 20, "mrae": 20,
    "rse": 20, "rae": 20, "rsquared": 20, "wmape": 20, "wrmse": 20,
    "mdae": 20, "mdape": 20, "mase": 20, "maape": 20, "mapd": 20,
    "huber": 20, "logcosh": 20, "pseudohuber": 20, "tukeybiweight": 20,
    "quantileloss": 20, "theilu": 20,
}


def main() -> None:
    print("=== Generating quantalib per-category Python modules ===")

    # Step 1: Build category map
    cat_map = build_category_map()
    print(f"  Found {len(cat_map)} indicators across {len(set(cat_map.values()))} categories")

    # Step 2: Parse exports
    exports = parse_exports(EXPORTS_CS)
    print(f"  Parsed {len(exports)} exports from Exports.Generated.cs")

    # Step 3: Resolve categories
    resolve_categories(exports, cat_map)

    # Group by category
    by_cat: dict[str, list[ExportInfo]] = {}
    uncategorized: list[ExportInfo] = []
    for exp in exports:
        if exp.category and exp.category != "_internal":
            by_cat.setdefault(exp.category, []).append(exp)
        elif exp.category != "_internal":
            uncategorized.append(exp)

    for cat in sorted(by_cat):
        inds = sorted(e.func_name for e in by_cat[cat])
        print(f"  {cat}: {len(inds)} indicators")

    if uncategorized:
        print(f"  UNCATEGORIZED: {[e.func_name for e in uncategorized]}")

    # Step 4: Generate _helpers.py
    helpers_path = OUT_DIR / "_helpers.py"
    helpers_path.write_text(gen_helpers(), encoding="utf-8")
    print(f"  Wrote {helpers_path}")

    # Step 5: Generate _bridge.py
    bridge_path = OUT_DIR / "_bridge.py"
    bridge_path.write_text(gen_bridge(exports, by_cat), encoding="utf-8")
    print(f"  Wrote {bridge_path}")

    # Step 6-8: will print summary
    print("\n=== Summary ===")
    print(f"  Total exports: {len(exports)}")
    print(f"  Categorized: {sum(len(v) for v in by_cat.values())}")
    print(f"  Categories: {len(by_cat)}")


if __name__ == "__main__":
    main()
