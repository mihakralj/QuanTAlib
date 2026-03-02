"""test_module_imports.py — Verify all category modules import without SyntaxError."""
from __future__ import annotations

import importlib
import pytest

# Every module that must be importable (no native lib required at import time
# because all native calls are deferred to function invocation).
MODULES = [
    "quantalib._helpers",
    "quantalib._compat",
    "quantalib._loader",
    "quantalib._bridge",
    "quantalib.channels",
    "quantalib.core",
    "quantalib.cycles",
    "quantalib.dynamics",
    "quantalib.errors",
    "quantalib.filters",
    "quantalib.momentum",
    "quantalib.numerics",
    "quantalib.oscillators",
    "quantalib.reversals",
    "quantalib.statistics",
    "quantalib.trends_fir",
    "quantalib.trends_iir",
    "quantalib.volatility",
    "quantalib.volume",
    "quantalib.indicators",
]


@pytest.mark.parametrize("modname", MODULES, ids=lambda m: m.split(".")[-1])
def test_module_imports(modname: str) -> None:
    """Each module must import without SyntaxError or ImportError.

    This catches reserved-keyword parameter names (lambda), duplicate
    parameter names, and broken imports.
    """
    mod = importlib.import_module(modname)
    assert mod is not None
