"""Exponential Moving Average — bias-compensated, NaN-safe.

Algorithm (mirrors Ema.cs):

    alpha  = 2 / (period + 1)
    decay  = 1 - alpha

    For each bar:
        ema   = ema * decay + alpha * value
        E    *= decay
        result = ema / (1 - E)      while E > epsilon
        result = ema                 after warmup
"""

import math

import numpy as np

__all__ = ["ema"]

EPSILON = 1e-10  # bias-compensator cutoff


def ema(source, period: int = 10, *, alpha: float | None = None) -> np.ndarray:
    """Bias-compensated EMA.

    Use *period* (default) or explicit *alpha* (keyword-only).
    If *alpha* is given, *period* is ignored.
    """
    if alpha is not None:
        if not (0.0 < alpha <= 1.0):
            raise ValueError(f"alpha must be in (0, 1], got {alpha}")
    else:
        if period <= 0:
            raise ValueError(f"period must be > 0, got {period}")
        alpha = 2.0 / (period + 1)

    src = np.asarray(source, dtype=np.float64)
    if src.ndim == 0 or src.size == 0:
        raise ValueError("source must not be empty")
    src = src.ravel()

    out = np.empty(len(src), dtype=np.float64)
    decay = 1.0 - alpha
    ema_val = 0.0
    e = 1.0
    last_valid = 0.0
    has_valid = False

    for i, v in enumerate(src):
        if math.isfinite(v):
            last_valid = v
            has_valid = True
        elif has_valid:
            v = last_valid
        else:
            out[i] = math.nan
            continue

        ema_val = ema_val * decay + alpha * v

        if e > EPSILON:
            e *= decay
            out[i] = ema_val / (1.0 - e)
        else:
            out[i] = ema_val

    return out
