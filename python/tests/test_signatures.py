"""test_signatures.py — Verify function signatures have no reserved keywords or duplicates."""
from __future__ import annotations

import importlib
import inspect
import keyword
import pytest

# All category modules with public indicator functions
CATEGORY_MODULES = [
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
]


def _get_public_functions():
    """Yield (module_name, func_name, func) for all public functions."""
    for modname in CATEGORY_MODULES:
        mod = importlib.import_module(modname)
        all_names = getattr(mod, "__all__", [])
        for name in all_names:
            fn = getattr(mod, name, None)
            if fn is not None and callable(fn):
                yield modname, name, fn


@pytest.fixture(scope="module")
def all_functions():
    return list(_get_public_functions())


class TestNoReservedKeywords:
    """No function parameter should use a Python reserved keyword."""

    def test_no_reserved_keyword_params(self, all_functions) -> None:
        violations = []
        for modname, fname, fn in all_functions:
            sig = inspect.signature(fn)
            for pname in sig.parameters:
                if keyword.iskeyword(pname):
                    violations.append(f"{modname}.{fname}(... {pname} ...)")
        assert violations == [], (
            f"Reserved keyword used as parameter name:\n"
            + "\n".join(f"  - {v}" for v in violations)
        )


class TestNoDuplicateParams:
    """No function should have duplicate parameter names (caught at parse time,
    but this validates post-fix)."""

    def test_no_duplicate_params(self, all_functions) -> None:
        violations = []
        for modname, fname, fn in all_functions:
            sig = inspect.signature(fn)
            params = list(sig.parameters.keys())
            if len(params) != len(set(params)):
                seen = set()
                dupes = [p for p in params if p in seen or seen.add(p)]  # type: ignore[func-returns-value]
                violations.append(f"{modname}.{fname}: duplicates={dupes}")
        assert violations == [], (
            f"Duplicate parameter names found:\n"
            + "\n".join(f"  - {v}" for v in violations)
        )


class TestNoBuiltinShadowing:
    """Public function names should not shadow critical Python builtins."""

    CRITICAL_BUILTINS = {"super", "type", "id", "input", "print", "open", "list", "dict", "set", "map", "filter"}

    def test_no_builtin_function_names(self, all_functions) -> None:
        violations = []
        for modname, fname, fn in all_functions:
            if fname in self.CRITICAL_BUILTINS:
                violations.append(f"{modname}.{fname}")
        assert violations == [], (
            f"Function names shadow Python builtins:\n"
            + "\n".join(f"  - {v}" for v in violations)
        )


class TestVolumeIndicatorsHaveVolumeParam:
    """Volume indicators that use _ptr(volume) must have volume in their signature."""

    VOLUME_REQUIRED = [
        "adl", "adosc", "iii", "kvo", "va", "vwad", "vwap", "wad",
        "obv", "pvt", "pvr", "vf", "nvi", "pvi", "tvi", "pvd",
        "vwma", "evwma", "efi", "aobv", "mfi", "cmf", "eom", "pvo",
    ]

    def test_volume_funcs_have_volume_param(self) -> None:
        import quantalib.volume as vol
        violations = []
        for fname in self.VOLUME_REQUIRED:
            fn = getattr(vol, fname, None)
            if fn is None:
                continue
            sig = inspect.signature(fn)
            if "volume" not in sig.parameters:
                violations.append(fname)
        assert violations == [], (
            f"Volume functions missing 'volume' parameter: {violations}"
        )
