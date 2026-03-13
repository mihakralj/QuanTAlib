"""Unit tests for pure-Python EMA implementation.

Per PYTHON_FALLBACK_SPEC §5: co-located <indicator>_test.py

Run::

    pytest lib/trends_IIR/ema/ema_test.py -v
"""

import math
import sys
from pathlib import Path

import numpy as np
import pytest

# Import from co-located ema.py
sys.path.insert(0, str(Path(__file__).parent))
from ema import ema  # noqa: E402


# ── basic correctness ──

def test_known_values():
    """Hand-calculated EMA(3) with bias compensation.

    alpha=0.5, decay=0.5:
    Bar 0: acc = 0.5*10 = 5,   E = 0.5,   result = 5/0.5 = 10.0
    Bar 1: acc = 5*0.5 + 0.5*20 = 12.5,  E = 0.25,  result = 12.5/0.75 ≈ 16.667
    Bar 2: acc = 12.5*0.5 + 0.5*30 = 21.25,  E = 0.125,  result = 21.25/0.875 ≈ 24.286
    """
    data = np.array([10.0, 20.0, 30.0, 40.0, 50.0])
    result = ema(data, period=3)
    assert result[0] == pytest.approx(10.0, rel=1e-12)
    assert result[1] == pytest.approx(16.666666666666668, rel=1e-10)
    assert result[2] == pytest.approx(24.285714285714285, rel=1e-10)


def test_period_1():
    """Period=1 → alpha=1.0 → output equals input (no smoothing)."""
    data = np.array([10.0, 20.0, 30.0, 40.0, 50.0])
    result = ema(data, period=1)
    np.testing.assert_array_equal(result, data)


def test_constant_input():
    """Constant 100.0 through any EMA → always 100.0."""
    data = np.full(100, 100.0)
    result = ema(data, period=20)
    np.testing.assert_allclose(result, 100.0, rtol=1e-12)


# ── edge cases ──

def test_empty_array():
    """Empty input → raises ValueError."""
    with pytest.raises(ValueError, match="must not be empty"):
        ema(np.array([]), period=5)


def test_invalid_period():
    """Period ≤ 0 → raises ValueError."""
    with pytest.raises(ValueError, match="period must be > 0"):
        ema(np.array([1.0, 2.0, 3.0]), period=0)
    with pytest.raises(ValueError, match="period must be > 0"):
        ema(np.array([1.0, 2.0, 3.0]), period=-1)


def test_single_element():
    """Single-element input returns that element."""
    result = ema([42.0], period=10)
    assert len(result) == 1
    assert result[0] == pytest.approx(42.0, rel=1e-12)


# ── NaN handling ──

def test_nan_in_input():
    """NaN replaced with last valid value → output stays finite."""
    data = np.array([10.0, 20.0, np.nan, 40.0, 50.0])
    result = ema(data, period=3)
    assert all(math.isfinite(v) for v in result)


def test_all_nan_returns_nan():
    """All-NaN input → all-NaN output."""
    data = np.full(5, np.nan)
    result = ema(data, period=3)
    assert all(math.isnan(v) for v in result)


def test_inf_handled():
    """Inf replaced with last valid value."""
    data = np.array([10.0, 20.0, np.inf, 40.0, 50.0])
    result = ema(data, period=3)
    assert all(math.isfinite(v) for v in result)


# ── output shape & warmup ──

def test_output_length_matches_input():
    """len(output) == len(input)."""
    data = np.random.default_rng(42).normal(100, 5, size=200)
    result = ema(data, period=14)
    assert len(result) == 200


def test_first_bar_always_valid():
    """Bias compensation means EMA produces valid output from bar 0."""
    data = np.random.default_rng(42).normal(100, 5, size=50)
    result = ema(data, period=50)
    assert math.isfinite(result[0])


# ── numerical precision ──

def test_large_values():
    """No overflow with 1e300 values."""
    data = np.full(100, 1e300)
    result = ema(data, period=10)
    np.testing.assert_allclose(result, 1e300, rtol=1e-10)


def test_tiny_values():
    """No underflow with 1e-300 values."""
    data = np.full(100, 1e-300)
    result = ema(data, period=10)
    np.testing.assert_allclose(result, 1e-300, rtol=1e-10)


def test_10k_series_all_finite():
    """Long series stays finite (no drift)."""
    data = np.random.default_rng(42).normal(100, 10, size=10_000)
    result = ema(data, period=20)
    assert np.all(np.isfinite(result))


def test_alpha_from_period_equivalence():
    """ema(period=N) == ema(alpha=2/(N+1))."""
    data = np.random.default_rng(42).normal(100, 5, size=200)
    r1 = ema(data, period=14)
    r2 = ema(data, alpha=2.0 / 15.0)
    np.testing.assert_allclose(r1, r2, rtol=1e-14)
