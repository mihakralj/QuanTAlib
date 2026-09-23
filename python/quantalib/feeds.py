"""quantalib.feeds — synthetic market data generators.

Vectorized NumPy port of the C# `GBM` feed (lib/feeds/gbm/gbm.cs). No native
FFI call is involved: the entire bar sequence is generated with array
operations (cumsum / exp / rng draws), so there is no per-bar Python-loop
overhead — the same trade that makes the .NET Batch paths fast.
"""
from __future__ import annotations

import numpy as np
from numpy.typing import NDArray

__all__ = ["gbm"]

# 252 trading days * 6.5 trading hours/day * 60 minutes/hour
_MINUTES_PER_YEAR = 252.0 * 6.5 * 60.0


def gbm(
    n: int,
    start_price: float = 100.0,
    mu: float = 0.05,
    sigma: float = 0.2,
    timeframe_minutes: float = 1.0,
    seed: int | None = None,
) -> dict[str, NDArray[np.float64]]:
    """Generate `n` synthetic OHLCV bars via Geometric Brownian Motion.

    Mirrors the drift/volatility scaling and OHLC construction of the C#
    `GBM` feed, but computes the whole series in one vectorized pass instead
    of bar-by-bar `Next()` calls.

    Args:
        n: Number of bars to generate. Must be positive.
        start_price: Initial price (must be positive and finite).
        mu: Annual drift/return rate.
        sigma: Annual volatility (must be non-negative and finite).
        timeframe_minutes: Bar duration in minutes (must be positive).
        seed: Optional random seed for reproducibility.

    Returns:
        dict with keys "open", "high", "low", "close", "volume", each a
        float64 NumPy array of length `n`.
    """
    if n <= 0:
        raise ValueError("n must be positive")
    if start_price <= 0 or not np.isfinite(start_price):
        raise ValueError("start_price must be positive and finite")
    if not np.isfinite(mu):
        raise ValueError("mu must be finite")
    if sigma < 0 or not np.isfinite(sigma):
        raise ValueError("sigma must be non-negative and finite")
    if timeframe_minutes <= 0:
        raise ValueError("timeframe_minutes must be positive")

    rng = np.random.default_rng(seed)

    dt = timeframe_minutes / _MINUTES_PER_YEAR
    drift = (mu - 0.5 * sigma * sigma) * dt
    vol = sigma * np.sqrt(dt)

    log_returns = drift + vol * rng.standard_normal(n)
    close = start_price * np.exp(np.cumsum(log_returns))

    # Fall back to the previous price on any non-finite/non-positive draw,
    # matching the C# guard in GBM.Next(). Rare, so a short scalar pass is fine.
    bad = ~np.isfinite(close) | (close <= 0)
    if bad.any():
        last = start_price
        for i in range(n):
            if bad[i]:
                close[i] = last
            last = close[i]

    open_ = np.empty(n, dtype=np.float64)
    open_[0] = start_price
    open_[1:] = close[:-1]

    hi_base = np.maximum(open_, close)
    lo_base = np.minimum(open_, close)

    high = np.maximum(hi_base * (1.0 + rng.random(n) * 0.01), hi_base)
    low = np.maximum(np.minimum(lo_base * (1.0 - rng.random(n) * 0.01), lo_base), np.finfo(np.float64).tiny)

    volume = 1000.0 + rng.random(n) * 1000.0

    return {
        "open": open_,
        "high": high,
        "low": low,
        "close": close,
        "volume": volume,
    }
