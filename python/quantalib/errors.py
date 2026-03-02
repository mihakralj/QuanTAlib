"""quantalib errors indicators.

Auto-generated — DO NOT EDIT.
"""
from __future__ import annotations

from ._helpers import _arr, _ptr, _out, _wrap, _wrap_multi, _check, _lib


__all__ = [
    "huber",
    "logcosh",
    "maape",
    "mapd",
    "mase",
    "mdae",
    "mdape",
    "me",
    "mpe",
    "mrae",
    "msle",
    "pseudohuber",
    "quantileloss",
    "rae",
    "rmsle",
    "rse",
    "rsquared",
    "smape",
    "theilu",
    "tukeybiweight",
    "wmape",
    "wrmse",
    "mse",
    "rmse",
    "mae",
    "mape",
]


def huber(actual: object, predicted: object, period: int = 14, delta: float = 1.35, offset: int = 0, **kwargs) -> object:
    """Huber Loss."""
    period = int(kwargs.get("length", period))
    delta = float(delta)
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_huber(_ptr(a), _ptr(p), _ptr(output), n, period, delta))
    return _wrap(output, idx, f"HUBER_{period}", "errors", offset)


def logcosh(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Log-Cosh Loss."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_logcosh(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"LOGCOSH_{period}", "errors", offset)


def maape(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Arctangent Absolute Percentage Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_maape(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MAAPE_{period}", "errors", offset)


def mapd(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Absolute Percentage Deviation."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_mapd(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MAPD_{period}", "errors", offset)


def mase(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Absolute Scaled Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_mase(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MASE_{period}", "errors", offset)


def mdae(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Median Absolute Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_mdae(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MDAE_{period}", "errors", offset)


def mdape(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Median Absolute Percentage Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_mdape(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MDAPE_{period}", "errors", offset)


def me(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_me(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"ME_{period}", "errors", offset)


def mpe(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Percentage Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_mpe(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MPE_{period}", "errors", offset)


def mrae(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Relative Absolute Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_mrae(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MRAE_{period}", "errors", offset)


def msle(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Mean Squared Logarithmic Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_msle(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"MSLE_{period}", "errors", offset)


def pseudohuber(actual: object, predicted: object, period: int = 14, delta: float = 1.35, offset: int = 0, **kwargs) -> object:
    """Pseudo-Huber Loss."""
    period = int(kwargs.get("length", period))
    delta = float(delta)
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_pseudohuber(_ptr(a), _ptr(p), _ptr(output), n, period, delta))
    return _wrap(output, idx, f"PSEUDOHUBER_{period}", "errors", offset)


def quantileloss(actual: object, predicted: object, period: int = 14, quantile: float = 0.5, offset: int = 0, **kwargs) -> object:
    """Quantile Loss (Pinball Loss)."""
    period = int(kwargs.get("length", period))
    quantile = float(quantile)
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_quantileloss(_ptr(a), _ptr(p), _ptr(output), n, period, quantile))
    return _wrap(output, idx, f"QUANTILELOSS_{period}", "errors", offset)


def rae(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Absolute Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_rae(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"RAE_{period}", "errors", offset)


def rmsle(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Root Mean Squared Logarithmic Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_rmsle(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"RMSLE_{period}", "errors", offset)


def rse(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Relative Squared Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_rse(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"RSE_{period}", "errors", offset)


def rsquared(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """R-Squared (Coefficient of Determination)."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_rsquared(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"RSQUARED_{period}", "errors", offset)


def smape(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Symmetric Mean Absolute Percentage Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_smape(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"SMAPE_{period}", "errors", offset)


def theilu(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Theil U Statistic (Error)."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_theilu(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"THEILU_{period}", "errors", offset)


def tukeybiweight(actual: object, predicted: object, period: int = 14, c: float = 4.685, offset: int = 0, **kwargs) -> object:
    """Tukey Biweight Loss."""
    period = int(kwargs.get("length", period))
    c = float(c)
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_tukeybiweight(_ptr(a), _ptr(p), _ptr(output), n, period, c))
    return _wrap(output, idx, f"TUKEYBIWEIGHT_{period}", "errors", offset)


def wmape(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Weighted Mean Absolute Percentage Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_wmape(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"WMAPE_{period}", "errors", offset)


def wrmse(actual: object, predicted: object, period: int = 14, offset: int = 0, **kwargs) -> object:
    """Weighted Root Mean Squared Error."""
    period = int(kwargs.get("length", period))
    offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a)
    output = _out(n)
    _check(_lib.qtl_wrmse(_ptr(a), _ptr(p), _ptr(output), n, period))
    return _wrap(output, idx, f"WRMSE_{period}", "errors", offset)

def mse(actual: object, predicted: object, length: int = 20,
        offset: int = 0, **kwargs) -> object:
    """Mean Squared Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_mse(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MSE_{length}", "errors", offset)


def rmse(actual: object, predicted: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Root Mean Squared Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_rmse(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"RMSE_{length}", "errors", offset)


def mae(actual: object, predicted: object, length: int = 20,
        offset: int = 0, **kwargs) -> object:
    """Mean Absolute Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_mae(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MAE_{length}", "errors", offset)


def mape(actual: object, predicted: object, length: int = 20,
         offset: int = 0, **kwargs) -> object:
    """Mean Absolute Percentage Error."""
    length = int(length); offset = int(offset)
    a, idx = _arr(actual); p, _ = _arr(predicted)
    n = len(a); dst = _out(n)
    _check(_lib.qtl_mape(_ptr(a), _ptr(p), n, _ptr(dst), length))
    return _wrap(dst, idx, f"MAPE_{length}", "errors", offset)
