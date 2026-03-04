"""Round-trip tests for PyArrow Array / ChunkedArray input → output.

Requires: ``pip install quantalib[pyarrow]``
"""
from __future__ import annotations

import numpy as np
import pytest

pa = pytest.importorskip("pyarrow", minversion="14.0")

import quantalib as qtl  # noqa: E402


# ---------------------------------------------------------------------------
#  Fixtures
# ---------------------------------------------------------------------------
@pytest.fixture()
def close_array() -> pa.Array:
    """100-bar random close prices as a PyArrow float64 Array."""
    rng = np.random.default_rng(42)
    return pa.array(rng.random(100) * 100 + 50, type=pa.float64())


@pytest.fixture()
def close_chunked() -> pa.ChunkedArray:
    """100-bar random close prices as a PyArrow ChunkedArray (2 chunks)."""
    rng = np.random.default_rng(42)
    data = rng.random(100) * 100 + 50
    chunk1 = pa.array(data[:50], type=pa.float64())
    chunk2 = pa.array(data[50:], type=pa.float64())
    return pa.chunked_array([chunk1, chunk2])


# ---------------------------------------------------------------------------
#  Single-output: pa.Array in → pa.Array out
# ---------------------------------------------------------------------------
class TestSingleOutput:
    def test_sma_returns_arrow_array(self, close_array: pa.Array) -> None:
        result = qtl.sma(close_array, length=14)
        assert isinstance(result, pa.Array)
        assert len(result) == len(close_array)
        assert result.type == pa.float64()

    def test_ema_returns_arrow_array(self, close_array: pa.Array) -> None:
        result = qtl.ema(close_array, length=14)
        assert isinstance(result, pa.Array)
        assert len(result) == len(close_array)

    def test_rsi_returns_arrow_array(self, close_array: pa.Array) -> None:
        result = qtl.rsi(close_array, length=14)
        assert isinstance(result, pa.Array)
        assert len(result) == len(close_array)

    def test_stddev_returns_arrow_array(self, close_array: pa.Array) -> None:
        result = qtl.stddev(close_array, length=14)
        assert isinstance(result, pa.Array)
        assert len(result) == len(close_array)

    def test_mom_returns_arrow_array(self, close_array: pa.Array) -> None:
        result = qtl.mom(close_array, length=10)
        assert isinstance(result, pa.Array)
        assert len(result) == len(close_array)


# ---------------------------------------------------------------------------
#  ChunkedArray input
# ---------------------------------------------------------------------------
class TestChunkedArray:
    def test_chunked_array_accepted(self, close_chunked: pa.ChunkedArray) -> None:
        result = qtl.sma(close_chunked, length=14)
        assert isinstance(result, pa.Array)
        assert len(result) == len(close_chunked)

    def test_chunked_values_match_flat(self, close_chunked: pa.ChunkedArray) -> None:
        flat = close_chunked.combine_chunks()
        result_chunked = qtl.sma(close_chunked, length=14)
        result_flat = qtl.sma(flat, length=14)
        np.testing.assert_allclose(
            result_chunked.to_numpy(zero_copy_only=False),
            result_flat.to_numpy(zero_copy_only=False),
            rtol=1e-12,
        )


# ---------------------------------------------------------------------------
#  Multi-output: pa.Array in → dict[str, pa.Array] out
# ---------------------------------------------------------------------------
class TestMultiOutput:
    def test_bbands_returns_dict_of_arrays(self, close_array: pa.Array) -> None:
        result = qtl.bbands(close_array, length=20, std=2.0)
        assert isinstance(result, dict)
        assert all(isinstance(v, pa.Array) for v in result.values())
        assert len(result) == 3  # upper, mid, lower
        for v in result.values():
            assert len(v) == len(close_array)
            assert v.type == pa.float64()


# ---------------------------------------------------------------------------
#  Numerical equivalence: Arrow vs numpy should produce identical values
# ---------------------------------------------------------------------------
class TestNumericalEquivalence:
    def test_sma_values_match_numpy(self, close_array: pa.Array) -> None:
        np_arr = close_array.to_numpy(zero_copy_only=False)
        result_pa = qtl.sma(close_array, length=14)
        result_np = qtl.sma(np_arr, length=14)
        np.testing.assert_allclose(
            result_pa.to_numpy(zero_copy_only=False),
            result_np,
            rtol=1e-12,
        )

    def test_rsi_values_match_numpy(self, close_array: pa.Array) -> None:
        np_arr = close_array.to_numpy(zero_copy_only=False)
        result_pa = qtl.rsi(close_array, length=14)
        result_np = qtl.rsi(np_arr, length=14)
        np.testing.assert_allclose(
            result_pa.to_numpy(zero_copy_only=False),
            result_np,
            rtol=1e-12,
            equal_nan=True,
        )

    def test_ema_values_match_numpy(self, close_array: pa.Array) -> None:
        np_arr = close_array.to_numpy(zero_copy_only=False)
        result_pa = qtl.ema(close_array, length=14)
        result_np = qtl.ema(np_arr, length=14)
        np.testing.assert_allclose(
            result_pa.to_numpy(zero_copy_only=False),
            result_np,
            rtol=1e-12,
            equal_nan=True,
        )


# ---------------------------------------------------------------------------
#  Type coercion
# ---------------------------------------------------------------------------
class TestTypeCoercion:
    def test_int32_array_coerced(self) -> None:
        arr = pa.array(list(range(1, 101)), type=pa.int32())
        result = qtl.sma(arr, length=5)
        assert isinstance(result, pa.Array)
        assert result.type == pa.float64()
        assert len(result) == 100

    def test_float32_array_coerced(self) -> None:
        rng = np.random.default_rng(42)
        arr = pa.array(rng.random(100).astype(np.float32), type=pa.float32())
        result = qtl.sma(arr, length=5)
        assert isinstance(result, pa.Array)
        assert result.type == pa.float64()
        assert len(result) == 100


# ---------------------------------------------------------------------------
#  Edge cases
# ---------------------------------------------------------------------------
class TestEdgeCases:
    def test_empty_array_raises(self) -> None:
        empty = pa.array([], type=pa.float64())
        with pytest.raises(ValueError, match="must not be empty"):
            qtl.sma(empty, length=14)
