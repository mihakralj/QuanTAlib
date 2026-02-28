from __future__ import annotations

import numpy as np
import pandas as pd
import pandas_ta as ta
import pytest

from quantalib import indicators as q


SEED = 42
N = 10_000
VERIFY_COUNT = 100


def _generate_gbm(
    n: int,
    seed: int = SEED,
    start_price: float = 100.0,
    mu: float = 0.05,
    sigma: float = 0.2,
    dt: float = 1 / 252,
) -> np.ndarray:
    rng = np.random.default_rng(seed)
    z = rng.standard_normal(n - 1)
    drift = (mu - 0.5 * sigma**2) * dt
    diffusion = sigma * np.sqrt(dt) * z
    log_returns = drift + diffusion

    prices = np.empty(n, dtype=np.float64)
    prices[0] = start_price
    np.cumsum(log_returns, out=prices[1:])
    prices[1:] += np.log(start_price)
    np.exp(prices[1:], out=prices[1:])
    prices[0] = start_price
    return prices


CLOSE = _generate_gbm(N)
SERIES = pd.Series(CLOSE, name="close")


def _verify_last_n(
    qtl_arr: np.ndarray,
    pta_arr: np.ndarray,
    *,
    verify_count: int = VERIFY_COUNT,
    tolerance: float = 1e-6,
    label: str,
) -> None:
    assert len(qtl_arr) == len(pta_arr), f"{label}: length mismatch"

    start = max(0, len(qtl_arr) - verify_count)
    q_tail = qtl_arr[start:]
    p_tail = pta_arr[start:]

    finite = np.isfinite(q_tail) & np.isfinite(p_tail)
    assert int(np.sum(finite)) > 0, f"{label}: no finite overlap in tail"

    diff = np.abs(q_tail[finite] - p_tail[finite])
    max_diff = float(np.max(diff))
    assert max_diff <= tolerance, f"{label}: max_diff={max_diff:.3e} > tol={tolerance:.1e}"


@pytest.mark.parametrize(
    "name,qtl,pta,tol",
    [
        ("rsi_14", q.rsi(CLOSE, length=14), ta.rsi(SERIES, length=14).to_numpy(), 1e-6),
        ("mom_10", q.mom(CLOSE, length=10), ta.mom(SERIES, length=10).to_numpy(), 1e-9),
        ("cmo_14", q.cmo(CLOSE, length=14), ta.cmo(SERIES, length=14, talib=False).to_numpy(), 1e-6),
        ("apo_12_26", q.apo(CLOSE, fast=12, slow=26), ta.apo(SERIES, fast=12, slow=26, mamode="ema", talib=False).to_numpy(), 1e-6),
        ("bias_26", q.bias(CLOSE, length=26), ta.bias(SERIES, length=26).to_numpy(), 1e-6),
        ("cfo_14", q.cfo(CLOSE, length=14), (100.0 * (SERIES - ta.linreg(SERIES, length=14, tsf=False, talib=False)) / SERIES).to_numpy(), 1e-6),
        ("dpo_20", q.dpo(CLOSE, length=20), ta.dpo(SERIES, length=20, centered=False).to_numpy(), 1e-6),
        ("trix_18", q.trix(CLOSE, length=18), ta.trix(SERIES, length=18).iloc[:, 0].to_numpy(), 1e-6),
        ("er_10", q.er(CLOSE, length=10), ta.er(SERIES, length=10).to_numpy(), 1e-6),
        ("cti_12", q.cti(CLOSE, length=12), ta.cti(SERIES, length=12).to_numpy(), 1e-6),
    ],
)
def test_pandas_ta_parity_batch_01(name: str, qtl: np.ndarray, pta: np.ndarray, tol: float) -> None:
    _verify_last_n(qtl, pta, tolerance=tol, label=name)