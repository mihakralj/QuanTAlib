"""test_category_labels.py — Verify consistent category labels across all modules."""
from __future__ import annotations

import importlib
import inspect
import re
import pytest

CATEGORY_MODULES = {
    "quantalib.channels": "channels",
    "quantalib.core": "core",
    "quantalib.cycles": "cycles",
    "quantalib.dynamics": "dynamics",
    "quantalib.errors": "errors",
    "quantalib.filters": "filters",
    "quantalib.momentum": "momentum",
    "quantalib.numerics": "numerics",
    "quantalib.oscillators": "oscillators",
    "quantalib.reversals": "reversals",
    "quantalib.statistics": "statistics",
    "quantalib.trends_fir": "trends_fir",
    "quantalib.trends_iir": "trends_iir",
    "quantalib.volatility": "volatility",
    "quantalib.volume": "volume",
}

# Regex to match _wrap(..., "CATEGORY", ...) or _wrap_multi(..., "CATEGORY", ...)
LABEL_PATTERN = re.compile(r'_wrap(?:_multi)?\(.*?,\s*"([^"]+)",\s*(?:offset|[\w]+)\)')


@pytest.mark.parametrize("modname,expected", CATEGORY_MODULES.items(), ids=lambda x: x.split(".")[-1] if "." in x else x)
def test_category_labels_lowercase(modname: str, expected: str) -> None:
    """Category labels passed to _wrap/_wrap_multi must be lowercase."""
    mod = importlib.import_module(modname)
    source_file = inspect.getfile(mod)
    with open(source_file, "r", encoding="utf-8") as f:
        source = f.read()

    # Find all category labels in source
    labels = LABEL_PATTERN.findall(source)
    bad = [lbl for lbl in labels if lbl != expected]
    assert bad == [], (
        f"{modname}: expected category label '{expected}', found non-matching: {set(bad)}"
    )
