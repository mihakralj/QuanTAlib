# pandas-ta parity report — Batch 01 (10 indicators)

Date: 2026-02-28  
Test file: `python/tests/test_pandas_ta_parity_batch_01.py`  
Command: `python -m pytest python/tests/test_pandas_ta_parity_batch_01.py -q`

## Summary

- Total tests: **10**
- Passed: **6**
- Failed: **4**
- Duration: **0.47s**

## Indicators in Batch 01

1. `rsi_14` ✅
2. `mom_10` ✅
3. `cmo_14` ❌
4. `apo_12_26` ❌
5. `bias_26` ✅
6. `cfo_14` ❌
7. `dpo_20` ✅
8. `trix_18` ❌
9. `er_10` ✅
10. `cti_12` ✅

## Failure details

### 1) `cmo_14`
- Error: numeric mismatch in tail window
- Max diff: `4.017e+01`
- Tolerance: `1e-6`

### 2) `apo_12_26`
- Error: numeric mismatch in tail window
- Max diff: `1.633e+00`
- Tolerance: `1e-6`

### 3) `cfo_14`
- Error: numeric mismatch in tail window
- Max diff: `8.166e-01`
- Tolerance: `1e-6`

### 4) `trix_18`
- Error: shape mismatch during comparison
- `quantalib`: shape `(10000,)`
- `pandas-ta`: shape `(10000, 2)` (DataFrame with TRIX + signal)
- Exception: broadcast error in finite-mask step

## Notes

- Batch-01 tests were created and executed as requested.
- Current failures are due to:
  - Known algorithmic differences (`cmo`, `apo`, `cfo`) and/or parameter semantics mismatch.
  - Output-shape mismatch for `trix` (single-series vs multi-column DataFrame).