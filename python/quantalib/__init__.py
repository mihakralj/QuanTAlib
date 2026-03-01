"""quantalib — Python wrapper for QuanTAlib NativeAOT exports.

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
