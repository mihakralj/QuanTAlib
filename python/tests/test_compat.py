"""test_compat.py — pandas-ta compatibility tests.

Verifies that:
1. ALIASES map resolves to real functions
2. pd.Series input → pd.Series output with correct name
3. pd.DataFrame input → works for single-column
"""
from __future__ import annotations

import numpy as np
import pytest

RNG = np.random.default_rng(99)
N = 50
CLOSE = RNG.standard_normal(N).cumsum() + 100.0


@pytest.fixture(scope="module")
def qtl():
    try:
        import quantalib as _qtl
        return _qtl
    except (OSError, ImportError) as e:
        pytest.skip(f"quantalib native lib not available: {e}")


@pytest.fixture(scope="module")
def pd():
    try:
        import pandas as _pd
        return _pd
    except ImportError:
        pytest.skip("pandas not installed")


class TestAliases:
    """Verify ALIASES map entries resolve to real functions."""

    def test_all_aliases_resolve(self, qtl) -> None:
        from quantalib._compat import ALIASES
        for alias, target in ALIASES.items():
            fn = getattr(qtl.indicators, target, None)
            assert fn is not None, f"Alias '{alias}' → '{target}' not found"

    def test_get_compat_returns_callable(self, qtl) -> None:
        from quantalib._compat import get_compat
        fn = get_compat("midprice")
        assert callable(fn)

    def test_get_compat_unknown_returns_none(self, qtl) -> None:
        from quantalib._compat import get_compat
        assert get_compat("nonexistent_indicator") is None


class TestPandasSeriesIO:
    """Verify pd.Series input → pd.Series output."""

    def test_sma_series_output(self, qtl, pd) -> None:
        idx = pd.date_range("2020-01-01", periods=N, freq="D")
        s = pd.Series(CLOSE, index=idx, name="Close")
        result = qtl.sma(s, length=10)
        assert isinstance(result, pd.Series)
        assert result.name == "SMA_10"
        assert len(result) == N
        assert (result.index == idx).all()

    def test_ema_series_category(self, qtl, pd) -> None:
        s = pd.Series(CLOSE)
        result = qtl.ema(s, length=14)
        assert isinstance(result, pd.Series)
        assert result.name == "EMA_14"
        assert hasattr(result, "category")
        assert result.category == "trend"

    def test_rsi_series(self, qtl, pd) -> None:
        s = pd.Series(CLOSE)
        result = qtl.rsi(s, length=14)
        assert isinstance(result, pd.Series)
        assert result.name == "RSI_14"


class TestPandasDataFrameIO:
    """Verify pd.DataFrame input uses first column."""

    def test_sma_dataframe_input(self, qtl, pd) -> None:
        df = pd.DataFrame({"Close": CLOSE, "Volume": np.ones(N)})
        result = qtl.sma(df, length=10)
        assert isinstance(result, pd.Series)
        assert len(result) == N


class TestMultiOutputPandas:
    """Verify multi-output returns DataFrame when given Series."""

    def test_bbands_dataframe_output(self, qtl, pd) -> None:
        s = pd.Series(CLOSE)
        result = qtl.bbands(s, length=20, std=2.0)
        assert isinstance(result, pd.DataFrame)
        assert result.shape == (N, 3)
        cols = list(result.columns)
        assert "BBU_20_2.0" in cols
        assert "BBM_20_2.0" in cols
        assert "BBL_20_2.0" in cols


class TestOffset:
    """Verify offset parameter works."""

    def test_sma_offset(self, qtl, pd) -> None:
        s = pd.Series(CLOSE)
        result = qtl.sma(s, length=10, offset=3)
        assert isinstance(result, pd.Series)
        # First 3 values should be NaN (from offset)
        assert np.isnan(result.iloc[0])
        assert np.isnan(result.iloc[1])
        assert np.isnan(result.iloc[2])
