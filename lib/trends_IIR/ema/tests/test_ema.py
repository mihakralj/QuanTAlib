"""Tests for the pure-Python EMA implementation.

Validates that ``ema.py`` produces results matching the C# ``Ema.Batch``
algorithm, including bias compensation, NaN handling, and edge cases.

Run with::

    python -m pytest lib/trends_IIR/ema/tests/test_ema.py -v
"""

import math
import sys
from pathlib import Path

import numpy as np
import pytest

# ── ensure the ema module is importable ──────────────────────────────────
# The ema.py lives one directory up from tests/
_EMA_DIR = Path(__file__).resolve().parent.parent
if str(_EMA_DIR) not in sys.path:
    sys.path.insert(0, str(_EMA_DIR))

from ema import ema  # noqa: E402


# ═════════════════════════════════════════════════════════════════════════
#  Fixtures
# ═════════════════════════════════════════════════════════════════════════

@pytest.fixture
def constant_series() -> np.ndarray:
    """100 bars of constant value 50.0."""
    return np.full(100, 50.0)


@pytest.fixture
def ramp_series() -> np.ndarray:
    """50 bars ramping 1..50."""
    return np.arange(1.0, 51.0)


@pytest.fixture
def short_series() -> np.ndarray:
    """10 bars: [10, 20, 30, 40, 50, 60, 70, 80, 90, 100]."""
    return np.arange(10.0, 110.0, 10.0)


# ═════════════════════════════════════════════════════════════════════════
#  Input validation
# ═════════════════════════════════════════════════════════════════════════

class TestInputValidation:
    """Guard clauses match C# ArgumentOutOfRangeException behavior."""

    def test_period_zero_raises(self) -> None:
        with pytest.raises(ValueError, match="period must be > 0"):
            ema([1.0, 2.0], period=0)

    def test_period_negative_raises(self) -> None:
        with pytest.raises(ValueError, match="period must be > 0"):
            ema([1.0, 2.0], period=-5)

    def test_alpha_zero_raises(self) -> None:
        with pytest.raises(ValueError, match="alpha must be in"):
            ema([1.0, 2.0], alpha=0.0)

    def test_alpha_negative_raises(self) -> None:
        with pytest.raises(ValueError, match="alpha must be in"):
            ema([1.0, 2.0], alpha=-0.1)

    def test_alpha_above_one_raises(self) -> None:
        with pytest.raises(ValueError, match="alpha must be in"):
            ema([1.0, 2.0], alpha=1.01)

    def test_alpha_exactly_one_ok(self) -> None:
        result = ema([5.0, 10.0, 15.0], alpha=1.0)
        # alpha=1 means output == input (no smoothing)
        np.testing.assert_array_equal(result, [5.0, 10.0, 15.0])

    def test_empty_source_raises(self) -> None:
        with pytest.raises(ValueError, match="must not be empty"):
            ema([], period=10)

    def test_scalar_source_raises(self) -> None:
        with pytest.raises(ValueError, match="must not be empty"):
            ema(np.float64(5.0), period=10)


# ═════════════════════════════════════════════════════════════════════════
#  Output shape
# ═════════════════════════════════════════════════════════════════════════

class TestOutputShape:
    """Output array must match input length exactly."""

    def test_length_equals_input(self, ramp_series: np.ndarray) -> None:
        result = ema(ramp_series, period=10)
        assert len(result) == len(ramp_series)

    def test_single_element(self) -> None:
        result = ema([42.0], period=5)
        assert len(result) == 1

    def test_dtype_float64(self, ramp_series: np.ndarray) -> None:
        result = ema(ramp_series, period=10)
        assert result.dtype == np.float64

    def test_accepts_list_input(self) -> None:
        result = ema([1.0, 2.0, 3.0], period=2)
        assert len(result) == 3

    def test_accepts_tuple_input(self) -> None:
        result = ema((1.0, 2.0, 3.0), period=2)
        assert len(result) == 3


# ═════════════════════════════════════════════════════════════════════════
#  Bias compensation correctness
# ═════════════════════════════════════════════════════════════════════════

class TestBiasCompensation:
    """Verify the warmup compensator E = (1-α)^t produces valid early values."""

    def test_first_bar_equals_input(self) -> None:
        """EMA(bar_0) must equal the input itself (compensated to 1×input)."""
        result = ema([100.0, 200.0, 300.0], period=10)
        # After compensation: ema_acc = alpha * 100, E = decay
        # result[0] = (alpha * 100) / (1 - decay) = (alpha * 100) / alpha = 100
        assert result[0] == pytest.approx(100.0, rel=1e-12)

    def test_constant_series_converges_to_constant(
        self, constant_series: np.ndarray
    ) -> None:
        """On constant input, every bar should equal the constant."""
        result = ema(constant_series, period=10)
        np.testing.assert_allclose(result, 50.0, rtol=1e-12)

    def test_compensator_eliminates_zero_bias(self) -> None:
        """Without compensation, starting from ema=0 would bias downward.
        With compensation, bar 1 on a constant 100 series must be 100."""
        result = ema(np.full(5, 100.0), period=20)
        # Every value should be exactly 100.0 (constant input)
        np.testing.assert_allclose(result, 100.0, rtol=1e-12)

    def test_period_1_passthrough(self) -> None:
        """Period=1 → alpha=1.0 → output equals input."""
        data = np.array([10.0, 20.0, 30.0, 40.0, 50.0])
        result = ema(data, period=1)
        np.testing.assert_array_equal(result, data)


# ═════════════════════════════════════════════════════════════════════════
#  EMA mathematical properties
# ═════════════════════════════════════════════════════════════════════════

class TestMathProperties:
    """Verify EMA satisfies known mathematical properties."""

    def test_monotone_input_monotone_output(self, ramp_series: np.ndarray) -> None:
        """Strictly increasing input → strictly increasing EMA."""
        result = ema(ramp_series, period=10)
        diffs = np.diff(result)
        assert np.all(diffs > 0), "EMA of monotone-increasing input must increase"

    def test_ema_lags_behind_ramp(self, ramp_series: np.ndarray) -> None:
        """EMA of increasing ramp must be ≤ the source (lag property)."""
        result = ema(ramp_series, period=10)
        # After first bar, EMA should lag behind
        assert np.all(result[1:] <= ramp_series[1:] + 1e-10)

    def test_ema_between_min_and_max(self, ramp_series: np.ndarray) -> None:
        """EMA output must lie within [min(source), max(source)]."""
        result = ema(ramp_series, period=10)
        assert np.all(result >= ramp_series.min() - 1e-10)
        assert np.all(result <= ramp_series.max() + 1e-10)

    def test_alpha_from_period(self) -> None:
        """ema(period=N) must equal ema(alpha=2/(N+1))."""
        data = np.random.default_rng(42).normal(100, 5, size=200)
        r1 = ema(data, period=14)
        r2 = ema(data, alpha=2.0 / 15.0)
        np.testing.assert_allclose(r1, r2, rtol=1e-14)

    def test_larger_period_smoother(self) -> None:
        """Larger period → less variance in the output."""
        data = np.random.default_rng(99).normal(100, 10, size=500)
        r5 = ema(data, period=5)
        r50 = ema(data, period=50)
        # Skip warmup region; use last 300 bars
        assert np.std(r50[-300:]) < np.std(r5[-300:])


# ═════════════════════════════════════════════════════════════════════════
#  NaN / Inf handling
# ═════════════════════════════════════════════════════════════════════════

class TestNanHandling:
    """NaN/Inf inputs are replaced with last valid value (C# GetValidValue)."""

    def test_nan_in_middle(self) -> None:
        data = np.array([10.0, 20.0, np.nan, 40.0, 50.0])
        result = ema(data, period=3)
        assert all(math.isfinite(v) for v in result)

    def test_inf_in_middle(self) -> None:
        data = np.array([10.0, 20.0, np.inf, 40.0, 50.0])
        result = ema(data, period=3)
        assert all(math.isfinite(v) for v in result)

    def test_neg_inf_in_middle(self) -> None:
        data = np.array([10.0, 20.0, -np.inf, 40.0, 50.0])
        result = ema(data, period=3)
        assert all(math.isfinite(v) for v in result)

    def test_nan_at_start_skipped(self) -> None:
        """Leading NaNs use last_valid = 0 until a finite value arrives."""
        data = np.array([np.nan, np.nan, 100.0, 200.0, 300.0])
        result = ema(data, period=3)
        # First two bars use last_valid=0 initially, then seed
        # After 100.0 arrives, behavior normalizes
        assert math.isfinite(result[2])
        assert math.isfinite(result[4])

    def test_all_nan_returns_nan(self) -> None:
        """If every value is NaN, output must be all NaN."""
        data = np.full(5, np.nan)
        result = ema(data, period=3)
        assert all(math.isnan(v) for v in result)

    def test_all_inf_returns_nan(self) -> None:
        """If every value is Inf, no valid seed → all NaN."""
        data = np.full(5, np.inf)
        result = ema(data, period=3)
        assert all(math.isnan(v) for v in result)


# ═════════════════════════════════════════════════════════════════════════
#  Golden values (hand-calculated reference)
# ═════════════════════════════════════════════════════════════════════════

class TestGoldenValues:
    """Verify against hand-calculated EMA with bias compensation.

    For period=3, alpha=0.5, decay=0.5:
    Bar 0: ema_acc = 0.5*10 = 5,   E = 0.5,   result = 5/(1-0.5) = 10.0
    Bar 1: ema_acc = 5*0.5 + 0.5*20 = 12.5,  E = 0.25,  result = 12.5/0.75 ≈ 16.6667
    Bar 2: ema_acc = 12.5*0.5 + 0.5*30 = 21.25,  E = 0.125,  result = 21.25/0.875 ≈ 24.2857
    """

    def test_period_3_first_three_bars(self) -> None:
        data = np.array([10.0, 20.0, 30.0, 40.0, 50.0])
        result = ema(data, period=3)

        assert result[0] == pytest.approx(10.0, rel=1e-12)
        assert result[1] == pytest.approx(16.666666666666668, rel=1e-10)
        assert result[2] == pytest.approx(24.285714285714285, rel=1e-10)

    def test_period_5_constant_100(self) -> None:
        """Constant 100 through EMA(5) → all 100.0."""
        data = np.full(20, 100.0)
        result = ema(data, period=5)
        np.testing.assert_allclose(result, 100.0, rtol=1e-12)

    def test_ema_alpha_direct(self) -> None:
        """ema(alpha=0.5) on [10,20,30] → same as period=3."""
        data = np.array([10.0, 20.0, 30.0])
        r1 = ema(data, period=3)
        r2 = ema(data, alpha=0.5)
        np.testing.assert_allclose(r1, r2, rtol=1e-14)


# ═════════════════════════════════════════════════════════════════════════
#  Stability and performance
# ═════════════════════════════════════════════════════════════════════════

class TestStability:
    """Long series shouldn't drift or produce non-finite values."""

    def test_10k_bars_all_finite(self) -> None:
        rng = np.random.default_rng(42)
        data = rng.normal(100, 10, size=10_000)
        result = ema(data, period=20)
        assert np.all(np.isfinite(result))

    def test_large_values_no_overflow(self) -> None:
        data = np.full(100, 1e300)
        result = ema(data, period=10)
        np.testing.assert_allclose(result, 1e300, rtol=1e-10)

    def test_tiny_values_no_underflow(self) -> None:
        data = np.full(100, 1e-300)
        result = ema(data, period=10)
        np.testing.assert_allclose(result, 1e-300, rtol=1e-10)

    def test_alternating_sign(self) -> None:
        """Alternating +100 / -100 should converge toward 0 for large period."""
        data = np.array([100.0, -100.0] * 500)
        result = ema(data, period=100)
        # Last few values should be near zero
        assert abs(result[-1]) < 20.0


# ═════════════════════════════════════════════════════════════════════════
#  Cross-validation with C# native (when available)
# ═════════════════════════════════════════════════════════════════════════

class TestCrossValidation:
    """Compare pure Python EMA against NativeAOT EMA (skipped if unavailable)."""

    @pytest.fixture
    def native_ema(self):
        """Try to import the native EMA wrapper."""
        try:
            from quantalib.trends_iir import ema as native_ema_fn
            return native_ema_fn
        except (ImportError, OSError):
            pytest.skip("quantalib native lib not available")

    def test_matches_native_random_data(self, native_ema) -> None:
        rng = np.random.default_rng(12345)
        data = rng.normal(100, 10, size=500)
        py_result = ema(data, period=14)
        native_result = native_ema(data, period=14)

        # Convert native result to numpy if needed
        native_arr = np.asarray(native_result, dtype=np.float64)
        np.testing.assert_allclose(py_result, native_arr, rtol=1e-10,
                                   err_msg="Python EMA diverges from native")

    def test_matches_native_with_nans(self, native_ema) -> None:
        data = np.array([10.0, np.nan, 30.0, 40.0, np.nan, 60.0, 70.0])
        py_result = ema(data, period=3)
        native_result = native_ema(data, period=3)
        native_arr = np.asarray(native_result, dtype=np.float64)
        np.testing.assert_allclose(py_result, native_arr, rtol=1e-10,
                                   err_msg="NaN handling diverges from native")
