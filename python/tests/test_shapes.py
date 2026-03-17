"""test_shapes.py — Verify len(output) == len(input) for all single-output indicators.

Requires the native library to be published first:
    pwsh python/publish.ps1
"""
from __future__ import annotations

import numpy as np
import pytest

# All Pattern A indicators (single-input + period → single-output)
# These accept fn(CLOSE, length=N) calling convention.
PATTERN_A = [
    "rsi", "roc", "mom", "cmo", "bias", "cfo",
    "fisher", "fisher04", "dpo", "trix", "inertia", "rsx", "er", "cti",
    "reflex", "trendflex", "kri", "psl",
    "sma", "wma", "hma", "trima", "swma", "dwma", "blma", "alma",
    "lsma", "sgma", "sinema", "hanma", "parzen", "tsf",
    "sp15", "tukey_w", "rain",
    "ema", "dema", "tema", "lema", "hema", "ahrens", "decycler",
    "bbw", "stddev", "variance",
    "zscore", "entropy",
    "bessel", "butter2", "butter3", "cheby1", "cheby2", "elliptic",
    "edcf", "bpf",
    "cg", "dsp", "ccor",
    "change",
]

# No-param indicators (single-input, no period)
NO_PARAM = ["cma", "exptrans"]

# Multi-param indicators that need custom calls
MULTI_PARAM = [
    # (name, kwargs_dict)
    ("tsi", {"long_period": 25, "short_period": 13}),
    ("apo", {"fast": 12, "slow": 26}),
    ("deco", {"short_period": 30, "long_period": 60}),
    ("dosc", {"rsi_period": 14, "ema1_period": 5, "ema2_period": 3, "signal_period": 9}),
    ("dymi", {"base_period": 14, "short_period": 5, "long_period": 10, "min_period": 3, "max_period": 30}),
    ("crsi", {"rsi_period": 3, "streak_period": 2, "rank_period": 100}),
    ("bbb", {"length": 20, "mult": 2.0}),
    ("bbi", {"p1": 3, "p2": 6, "p3": 12, "p4": 24}),
    ("bwma", {"length": 14, "order": 0}),
    ("crma", {"length": 14, "volume_factor": 1.0}),
    ("dsma", {"length": 14, "factor": 0.5}),
    ("gdema", {"length": 14, "vfactor": 1.0}),
    ("coral", {"length": 14, "friction": 0.4}),
    ("bbwn", {"length": 20, "mult": 2.0, "lookback": 252}),
    ("bbwp", {"length": 20, "mult": 2.0, "lookback": 252}),
    ("ccv", {"short_period": 20, "long_period": 1}),
    ("cv", {"length": 20, "min_vol": 0.2, "max_vol": 0.7}),
    ("cvi", {"ema_period": 10, "roc_period": 10}),
    ("ewma", {"length": 20, "is_pop": 1, "ann_factor": 252}),
    ("alaguerre", {"length": 20, "order": 5}),
    ("bilateral", {"length": 14, "sigma_s": 0.5, "sigma_r": 1.0}),
    ("baxterking", {"length": 12, "min_period": 6, "max_period": 32}),
    ("cfitz", {"length": 6, "bw_period": 32}),
    ("ebsw", {"hp_length": 40, "ssf_length": 10}),
    ("acp", {"min_period": 8, "max_period": 48, "avg_length": 3, "enhance": 1}),
    ("betadist", {"length": 50, "alpha": 2.0, "beta": 2.0}),
    ("expdist", {"length": 50, "lam": 3.0}),
    ("binomdist", {"length": 50, "trials": 20, "threshold": 10}),
    ("cwt", {"scale": 10.0, "omega": 6.0}),
    ("dwt", {"length": 4, "levels": 0}),
]

N = 200
RNG = np.random.default_rng(42)
CLOSE = RNG.standard_normal(N).cumsum() + 100.0


@pytest.fixture(scope="module")
def qtl():
    """Import quantalib; skip if native lib not available."""
    try:
        import quantalib as _qtl
        return _qtl
    except (OSError, ImportError) as e:
        pytest.skip(f"quantalib native lib not available: {e}")


@pytest.mark.parametrize("name", PATTERN_A)
def test_pattern_a_shape(qtl, name: str) -> None:
    fn = getattr(qtl.indicators, name, None)
    if fn is None:
        pytest.skip(f"{name} not available")
    result = fn(CLOSE, length=14)
    assert isinstance(result, np.ndarray), f"{name} did not return ndarray"
    assert len(result) == N, f"{name}: expected {N}, got {len(result)}"


@pytest.mark.parametrize("name", NO_PARAM)
def test_no_param_shape(qtl, name: str) -> None:
    fn = getattr(qtl.indicators, name, None)
    if fn is None:
        pytest.skip(f"{name} not available")
    result = fn(CLOSE)
    assert isinstance(result, np.ndarray)
    assert len(result) == N


@pytest.mark.parametrize("name,kwargs", MULTI_PARAM, ids=[m[0] for m in MULTI_PARAM])
def test_multi_param_shape(qtl, name: str, kwargs: dict) -> None:
    fn = getattr(qtl.indicators, name, None)
    if fn is None:
        pytest.skip(f"{name} not available")
    result = fn(CLOSE, **kwargs)
    assert isinstance(result, np.ndarray), f"{name} did not return ndarray"
    assert len(result) == N, f"{name}: expected {N}, got {len(result)}"


def test_medprice_shape(qtl) -> None:
    h = CLOSE + RNG.uniform(0, 2, N)
    l = CLOSE - RNG.uniform(0, 2, N)
    result = qtl.indicators.medprice(h, l)
    assert len(result) == N


def test_tr_shape(qtl) -> None:
    h = CLOSE + RNG.uniform(0, 2, N)
    l = CLOSE - RNG.uniform(0, 2, N)
    result = qtl.indicators.tr(h, l, CLOSE)
    assert len(result) == N


def test_bbands_shape(qtl) -> None:
    result = qtl.indicators.bbands(CLOSE, length=20, std=2.0)
    # Returns tuple of 3 arrays when no pandas
    assert len(result) == 3
    for arr in result:
        assert len(arr) == N


def test_obv_shape(qtl) -> None:
    vol = RNG.uniform(1e6, 1e7, N)
    result = qtl.indicators.obv(CLOSE, vol)
    assert len(result) == N


def test_mfi_shape(qtl) -> None:
    h = CLOSE + RNG.uniform(0, 2, N)
    l = CLOSE - RNG.uniform(0, 2, N)
    vol = RNG.uniform(1e6, 1e7, N)
    result = qtl.indicators.mfi(h, l, CLOSE, vol, length=14)
    assert len(result) == N


def test_correlation_shape(qtl) -> None:
    y = RNG.standard_normal(N).cumsum() + 50.0
    result = qtl.indicators.correlation(CLOSE, y, length=20)
    assert len(result) == N


def test_mse_shape(qtl) -> None:
    predicted = CLOSE + RNG.standard_normal(N) * 0.5
    result = qtl.indicators.mse(CLOSE, predicted, length=20)
    assert len(result) == N


def test_pvo_shape(qtl) -> None:
    vol = RNG.uniform(1e6, 1e7, N)
    result = qtl.indicators.pvo(vol, fast=12, slow=26, signal=9)
    assert len(result) == 3  # tuple of 3
    for arr in result:
        assert len(arr) == N
