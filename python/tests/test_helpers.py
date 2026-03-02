"""test_helpers.py — Unit tests for quantalib._helpers (no native lib needed)."""
from __future__ import annotations

import numpy as np
import pytest


# ---------------------------------------------------------------------------
#  Helpers
# ---------------------------------------------------------------------------
def _has_pandas() -> bool:
    try:
        import pandas  # noqa: F401
        return True
    except ImportError:
        return False


# ---------------------------------------------------------------------------
#  _arr
# ---------------------------------------------------------------------------
class TestArr:
    """Tests for _arr() input coercion and validation."""

    def test_list_to_float64(self) -> None:
        from quantalib._helpers import _arr
        arr, idx = _arr([1.0, 2.0, 3.0])
        assert arr.dtype == np.float64
        assert idx is None
        np.testing.assert_array_equal(arr, [1.0, 2.0, 3.0])

    def test_int_array_coerced(self) -> None:
        from quantalib._helpers import _arr
        arr, _ = _arr(np.array([1, 2, 3]))
        assert arr.dtype == np.float64

    def test_contiguous_no_copy(self) -> None:
        from quantalib._helpers import _arr
        src = np.array([1.0, 2.0, 3.0], dtype=np.float64)
        arr, _ = _arr(src)
        # Already contiguous float64 — should share memory
        assert np.shares_memory(arr, src)

    def test_non_contiguous_made_contiguous(self) -> None:
        from quantalib._helpers import _arr
        src = np.array([1.0, 2.0, 3.0, 4.0], dtype=np.float64)[::2]
        assert not src.flags["C_CONTIGUOUS"]
        arr, _ = _arr(src)
        assert arr.flags["C_CONTIGUOUS"]

    def test_none_raises(self) -> None:
        from quantalib._helpers import _arr
        with pytest.raises(ValueError, match="must not be None"):
            _arr(None)

    def test_empty_raises(self) -> None:
        from quantalib._helpers import _arr
        with pytest.raises(ValueError, match="must not be empty"):
            _arr(np.array([], dtype=np.float64))

    def test_scalar_raises(self) -> None:
        from quantalib._helpers import _arr
        with pytest.raises(ValueError, match="must not be empty"):
            _arr(np.float64(42.0))

    @pytest.mark.skipif(not _has_pandas(), reason="pandas not installed")
    def test_pandas_series_preserves_index(self) -> None:
        import pandas as pd
        from quantalib._helpers import _arr
        idx = pd.date_range("2020-01-01", periods=5)
        s = pd.Series([1.0, 2.0, 3.0, 4.0, 5.0], index=idx)
        arr, ridx = _arr(s)
        assert arr.dtype == np.float64
        assert ridx is idx

    @pytest.mark.skipif(not _has_pandas(), reason="pandas not installed")
    def test_pandas_dataframe_uses_first_col(self) -> None:
        import pandas as pd
        from quantalib._helpers import _arr
        df = pd.DataFrame({"a": [1.0, 2.0], "b": [3.0, 4.0]})
        arr, idx = _arr(df)
        np.testing.assert_array_equal(arr, [1.0, 2.0])


# ---------------------------------------------------------------------------
#  _offset
# ---------------------------------------------------------------------------
class TestOffset:
    """Tests for _offset() roll + NaN fill."""

    def test_zero_offset_noop(self) -> None:
        from quantalib._helpers import _offset
        arr = np.array([1.0, 2.0, 3.0])
        result = _offset(arr, 0)
        np.testing.assert_array_equal(result, arr)

    def test_positive_offset(self) -> None:
        from quantalib._helpers import _offset
        arr = np.array([1.0, 2.0, 3.0, 4.0])
        result = _offset(arr, 2)
        assert np.isnan(result[0])
        assert np.isnan(result[1])
        assert result[2] == 1.0
        assert result[3] == 2.0

    def test_negative_offset(self) -> None:
        from quantalib._helpers import _offset
        arr = np.array([1.0, 2.0, 3.0, 4.0])
        result = _offset(arr, -1)
        assert result[0] == 2.0
        assert result[1] == 3.0
        assert result[2] == 4.0
        assert np.isnan(result[3])


# ---------------------------------------------------------------------------
#  _wrap and _wrap_multi
# ---------------------------------------------------------------------------
class TestWrap:
    """Tests for _wrap() and _wrap_multi()."""

    def test_wrap_numpy_no_offset(self) -> None:
        from quantalib._helpers import _wrap
        arr = np.array([10.0, 20.0, 30.0])
        result = _wrap(arr, None, "TEST", "cat", 0)
        assert isinstance(result, np.ndarray)
        np.testing.assert_array_equal(result, arr)

    def test_wrap_numpy_with_offset(self) -> None:
        from quantalib._helpers import _wrap
        arr = np.array([10.0, 20.0, 30.0])
        result = _wrap(arr, None, "TEST", "cat", 1)
        assert np.isnan(result[0])
        assert result[1] == 10.0

    @pytest.mark.skipif(not _has_pandas(), reason="pandas not installed")
    def test_wrap_pandas_series_category_in_attrs(self) -> None:
        import pandas as pd
        from quantalib._helpers import _wrap
        idx = pd.RangeIndex(3)
        arr = np.array([10.0, 20.0, 30.0])
        result = _wrap(arr, idx, "SMA_10", "trends_fir", 0)
        assert isinstance(result, pd.Series)
        assert result.name == "SMA_10"
        assert result.attrs["category"] == "trends_fir"

    def test_wrap_multi_numpy(self) -> None:
        from quantalib._helpers import _wrap_multi
        arrays = {
            "upper": np.array([1.0, 2.0]),
            "lower": np.array([0.5, 1.0]),
        }
        result = _wrap_multi(arrays, None, "cat", 0)
        assert isinstance(result, tuple)
        assert len(result) == 2

    @pytest.mark.skipif(not _has_pandas(), reason="pandas not installed")
    def test_wrap_multi_pandas_attrs(self) -> None:
        import pandas as pd
        from quantalib._helpers import _wrap_multi
        idx = pd.RangeIndex(2)
        arrays = {
            "upper": np.array([1.0, 2.0]),
            "lower": np.array([0.5, 1.0]),
        }
        result = _wrap_multi(arrays, idx, "channels", 0)
        assert isinstance(result, pd.DataFrame)
        assert result.attrs["category"] == "channels"


# ---------------------------------------------------------------------------
#  _out
# ---------------------------------------------------------------------------
class TestOut:
    """Tests for _out() allocation."""

    def test_out_shape_and_dtype(self) -> None:
        from quantalib._helpers import _out
        arr = _out(100)
        assert arr.shape == (100,)
        assert arr.dtype == np.float64


# ---------------------------------------------------------------------------
#  _ptr
# ---------------------------------------------------------------------------
class TestPtr:
    """Tests for _ptr() ctypes pointer extraction."""

    def test_ptr_not_none(self) -> None:
        from quantalib._helpers import _ptr
        arr = np.array([1.0, 2.0, 3.0], dtype=np.float64)
        p = _ptr(arr)
        assert p is not None
