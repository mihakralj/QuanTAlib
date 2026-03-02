"""quantalib — Python wrapper for QuanTAlib NativeAOT exports.

Usage::

    import quantalib as qtl

    result = qtl.sma(close_array, length=14)
    result = qtl.bbands(close_array, length=20, std=2.0)

    print(qtl.version)       # e.g. "0.8.0"
    print(qtl.__version__)   # same
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
    "version",
    "__version__",
]


def _resolve_version() -> str:
    """Resolve version from lib/VERSION (dev) or package metadata (installed)."""
    # 1. Try repo-local VERSION file (works in dev / editable install)
    pkg_dir = Path(__file__).resolve().parent          # python/quantalib/
    candidates = [
        pkg_dir.parents[1] / "lib" / "VERSION",       # repo root / lib / VERSION
        pkg_dir.parent / "lib" / "VERSION",            # python / lib / VERSION (fallback)
        pkg_dir / "VERSION",                           # baked into wheel
    ]
    for vf in candidates:
        if vf.is_file():
            ver = vf.read_text(encoding="utf-8").strip()
            if ver:
                return ver

    # 2. Fall back to importlib.metadata (pip-installed wheel)
    try:
        from importlib.metadata import version as _pkg_version
        return _pkg_version("quantalib")
    except Exception:
        pass

    return "0.0.0"


__version__: str = _resolve_version()
version: str = __version__
