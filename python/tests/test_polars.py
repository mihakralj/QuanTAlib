"""Round-trip tests for Polars Series / DataFrame input → output.

Requires: ``pip install quantalib[polars]``
"""
from __future__ import annotations

import numpy as np
import pytest

pl = pytest.importorskip("polars", minversion="0.20")

import quantalib as qtl  # noqa: E402


# ---------------------------------------------------------------------------
#  Fixtures
# ---------------------------------------------------------------------------
@pytest.fixture()
def close_series() -> pl.Series:
    """100-bar random close prices as a Polars Series."""
    rng = np.random.default_rng(42)
    return pl.Series(name="close", values=rng.random(100) * 100 + 50)


@pytest.fixture()
def ohlcv_df() -> pl.DataFrame:
    """100-bar OHLCV DataFrame."""
    rng = np.random.default_rng(42)
    c = rng.random(100) * 100 + 50
    return pl.DataFrame({
        "open": c + rng.uniform(-2, 2, 100),
        "high": c + rng.uniform(0, 5, 100),
        "low": c - rng.uniform(0, 5, 100),
        "close": c,
        "volume": rng.uniform(1e4, 1e6, 100),
    })


# ---------------------------------------------------------------------------
#  Single-output: Polars Series in → Polars Series out
# ---------------------------------------------------------------------------
class TestSingleOutput:
    def test_sma_returns_polars_series(self, close_series: pl.Series) -> None:
        result = qtl.sma(close_series, length=14)
        assert isinstance(result, pl.Series)
        assert len(result) == len(close_series)

    def test_ema_returns_polars_series(self, close_series: pl.Series) -> None:
        result = qtl.ema(close_series, length=14)
        assert isinstance(result, pl.Series)
        assert len(result) == len(close_series)

    def test_rsi_returns_polars_series(self, close_series: pl.Series) -> None:
        result = qtl.rsi(close_series, length=14)
        assert isinstance(result, pl.Series)
        assert len(result) == len(close_series)

    def test_series_name_follows_convention(self, close_series: pl.Series) -> None:
        result = qtl.sma(close_series, length=20)
        assert isinstance(result, pl.Series)
        assert result.name == "SMA_20"

    def test_stddev_returns_polars_series(self, close_series: pl.Series) -> None:
        result = qtl.stddev(close_series, length=14)
        assert isinstance(result, pl.Series)
        assert len(result) == len(close_series)

    def test_mom_returns_polars_series(self, close_series: pl.Series) -> None:
        result = qtl.mom(close_series, length=10)
        assert isinstance(result, pl.Series)
        assert len(result) == len(close_series)


# ---------------------------------------------------------------------------
#  DataFrame input: first column extracted
# ---------------------------------------------------------------------------
class TestDataFrameInput:
    def test_dataframe_first_col_used(self, ohlcv_df: pl.DataFrame) -> None:
        close_col = ohlcv_df.select("close")
        result = qtl.sma(close_col, length=14)
        assert isinstance(result, pl.Series)
        assert len(result) == len(ohlcv_df)


# ---------------------------------------------------------------------------
#  Multi-output: Polars Series in → Polars DataFrame out
# ---------------------------------------------------------------------------
class TestMultiOutput:
    def test_bbands_returns_polars_dataframe(self, close_series: pl.Series) -> None:
        result = qtl.bbands(close_series, length=20, std=2.0)
        assert isinstance(result, pl.DataFrame)
        assert result.shape[0] == len(close_series)
        assert result.shape[1] == 3  # upper, mid, lower


# ---------------------------------------------------------------------------
#  Numerical equivalence: Polars vs numpy should produce identical values
# ---------------------------------------------------------------------------
class TestNumericalEquivalence:
    def test_sma_values_match_numpy(self, close_series: pl.Series) -> None:
        np_arr = close_series.to_numpy()
        result_pl = qtl.sma(close_series, length=14)
        result_np = qtl.sma(np_arr, length=14)
        np.testing.assert_allclose(
            result_pl.to_numpy(), result_np, rtol=1e-12
        )

    def test_rsi_values_match_numpy(self, close_series: pl.Series) -> None:
        np_arr = close_series.to_numpy()
        result_pl = qtl.rsi(close_series, length=14)
        result_np = qtl.rsi(np_arr, length=14)
        np.testing.assert_allclose(
            result_pl.to_numpy(), result_np, rtol=1e-12, equal_nan=True
        )

    def test_ema_values_match_numpy(self, close_series: pl.Series) -> None:
        np_arr = close_series.to_numpy()
        result_pl = qtl.ema(close_series, length=14)
        result_np = qtl.ema(np_arr, length=14)
        np.testing.assert_allclose(
            result_pl.to_numpy(), result_np, rtol=1e-12, equal_nan=True
        )


# ---------------------------------------------------------------------------
#  Edge cases
# ---------------------------------------------------------------------------
class TestEdgeCases:
    def test_none_input_raises(self) -> None:
        with pytest.raises(ValueError, match="must not be None"):
            qtl.sma(None, length=14)

    def test_empty_series_raises(self) -> None:
        empty = pl.Series(name="empty", values=[], dtype=pl.Float64)
        with pytest.raises(ValueError, match="must not be empty"):
            qtl.sma(empty, length=14)

    def test_int_series_coerced_to_float(self) -> None:
        int_series = pl.Series(name="ints", values=list(range(1, 101)))
        result = qtl.sma(int_series, length=5)
        assert isinstance(result, pl.Series)
        assert len(result) == 100
