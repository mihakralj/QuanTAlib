"""test_status_codes.py — Verify correct exceptions for bad inputs.

Tests that null pointers, invalid lengths, and invalid params raise
the expected quantalib exception types.
"""
from __future__ import annotations

import numpy as np
import pytest


@pytest.fixture(scope="module")
def qtl():
    try:
        import quantalib as _qtl
        return _qtl
    except (OSError, ImportError) as e:
        pytest.skip(f"quantalib native lib not available: {e}")


@pytest.fixture(scope="module")
def bridge():
    try:
        from quantalib import _bridge
        return _bridge
    except (OSError, ImportError) as e:
        pytest.skip(f"quantalib native lib not available: {e}")


class TestInvalidLength:
    """Period <= 0 should raise QtlInvalidParamError (ChkPeriod returns status 3)."""

    def test_sma_zero_length(self, qtl) -> None:
        close = np.ones(10, dtype=np.float64)
        # ChkPeriod checks period > 0; returns QTL_ERR_INVALID_PARAM (3) for <= 0
        with pytest.raises(qtl.QtlInvalidParamError):
            qtl.sma(close, length=0)

    def test_sma_negative_length(self, qtl) -> None:
        close = np.ones(10, dtype=np.float64)
        with pytest.raises(qtl.QtlInvalidParamError):
            qtl.sma(close, length=-5)


class TestInvalidParam:
    """Bad parameter values should raise QtlInvalidParamError."""

    def test_sma_period_exceeds_length(self, qtl) -> None:
        """SMA Batch processes whatever data is available;
        period > n is not an error — it just computes with partial data."""
        close = np.ones(5, dtype=np.float64)
        # This should NOT raise; SMA handles period > n gracefully
        result = qtl.sma(close, length=10)
        assert len(result) == 5


class TestNullPointer:
    """Null pointer should raise QtlNullPointerError via raw bridge call."""

    def test_null_src(self, bridge) -> None:
        import ctypes as ct
        null = ct.cast(None, bridge._dp)
        dst = np.empty(10, dtype=np.float64)
        status = bridge._lib.qtl_sma(null, 10, dst.ctypes.data_as(bridge._dp), 5)
        assert status == bridge.QTL_ERR_NULL_PTR

    def test_null_dst(self, bridge) -> None:
        import ctypes as ct
        src = np.ones(10, dtype=np.float64)
        null = ct.cast(None, bridge._dp)
        status = bridge._lib.qtl_sma(src.ctypes.data_as(bridge._dp), 10, null, 5)
        assert status == bridge.QTL_ERR_NULL_PTR


class TestCheckHelper:
    """Verify _check() maps status codes to exceptions."""

    def test_ok(self, bridge) -> None:
        bridge._check(0)  # Should not raise

    def test_null_ptr(self, bridge) -> None:
        with pytest.raises(bridge.QtlNullPointerError):
            bridge._check(1)

    def test_invalid_length(self, bridge) -> None:
        with pytest.raises(bridge.QtlInvalidLengthError):
            bridge._check(2)

    def test_invalid_param(self, bridge) -> None:
        with pytest.raises(bridge.QtlInvalidParamError):
            bridge._check(3)

    def test_internal(self, bridge) -> None:
        with pytest.raises(bridge.QtlInternalError):
            bridge._check(4)

    def test_unknown(self, bridge) -> None:
        with pytest.raises(bridge.QtlError):
            bridge._check(99)
