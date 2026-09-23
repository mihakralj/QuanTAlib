"""High-level indicator wrappers for quantalib.

This module re-exports all indicator functions from per-category submodules.
Each function accepts numpy arrays (or pandas Series / DataFrame) and
returns the same type.

Category submodules:
    quantalib.channels     — Bollinger Bands, Keltner, Donchian, etc.
    quantalib.core         — Price transforms (avgprice, medprice, etc.)
    quantalib.cycles       — Hilbert, Sinewave, CG, DSP, etc.
    quantalib.dynamics     — ADX, Ichimoku, Supertrend, etc.
    quantalib.errors       — MSE, RMSE, MAE, MAPE, Huber, etc.
    quantalib.filters      — Butterworth, Chebyshev, Kalman, etc.
    quantalib.momentum     — RSI, MACD, ROC, MOM, etc.
    quantalib.numerics     — FFT, sigmoid, slope, distributions, etc.
    quantalib.oscillators  — Stochastic, Fisher, Williams %R, etc.
    quantalib.reversals    — Pivot points, SAR, fractals, etc.
    quantalib.statistics   — Z-score, correl, linreg, etc.
    quantalib.trends_fir   — SMA, WMA, HMA, ALMA, etc.
    quantalib.trends_iir   — EMA, DEMA, TEMA, JMA, KAMA, etc.
    quantalib.volatility   — ATR, TR, Bollinger Width, etc.
    quantalib.volume       — OBV, VWAP, MFI, CMF, etc.
"""
from __future__ import annotations

from . import (
    channels,
    core,
    cycles,
    dynamics,
    errors,
    filters,
    momentum,
    numerics,
    oscillators,
    reversals,
    statistics,
    trends_fir,
    trends_iir,
    volatility,
    volume,
)
from .channels import *  # noqa: F401, F403
from .core import *  # noqa: F401, F403
from .cycles import *  # noqa: F401, F403
from .dynamics import *  # noqa: F401, F403
from .errors import *  # noqa: F401, F403
from .filters import *  # noqa: F401, F403
from .momentum import *  # noqa: F401, F403
from .numerics import *  # noqa: F401, F403
from .oscillators import *  # noqa: F401, F403
from .reversals import *  # noqa: F401, F403
from .statistics import *  # noqa: F401, F403
from .trends_fir import *  # noqa: F401, F403
from .trends_iir import *  # noqa: F401, F403
from .volatility import *  # noqa: F401, F403
from .volume import *  # noqa: F401, F403

# Explicit re-export list (PEP 484 §Stub files / py.typed packages require this
# for wildcard-imported names to be considered public by type checkers).
__all__ = [
    *channels.__all__,
    *core.__all__,
    *cycles.__all__,
    *dynamics.__all__,
    *errors.__all__,
    *filters.__all__,
    *momentum.__all__,
    *numerics.__all__,
    *oscillators.__all__,
    *reversals.__all__,
    *statistics.__all__,
    *trends_fir.__all__,
    *trends_iir.__all__,
    *volatility.__all__,
    *volume.__all__,
]
