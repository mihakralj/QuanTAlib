"""pandas-ta compatibility aliases.

Maps pandas-ta function names to quantalib equivalents where signatures
overlap.  Import ``from quantalib._compat import ALIASES`` then look up
the target function in ``quantalib.indicators``.

Usage::

    from quantalib._compat import get_compat
    fn = get_compat("midprice")  # returns indicators.medprice
"""
from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Callable

# pandas-ta name → quantalib indicators function name
ALIASES: dict[str, str] = {
    # Core
    "midprice": "medprice",
    "typical_price": "typprice",
    "average_price": "avgprice",
    "mid_body": "midbody",
    # Momentum
    "momentum": "mom",
    # Trends
    "simple_moving_average": "sma",
    "weighted_moving_average": "wma",
    "hull_moving_average": "hma",
    "triangular_moving_average": "trima",
    "exponential_moving_average": "ema",
    "double_exponential_moving_average": "dema",
    "triple_exponential_moving_average": "tema",
    "least_squares_moving_average": "lsma",
    "time_series_forecast": "tsf",
    "linreg": "lsma",
    "sinwma": "sinema",
    # Core (pandas-ta price transforms)
    "hl2": "medprice",
    "hlc3": "typprice",
    "ohlc4": "avgprice",
    # Volatility
    "true_range": "tr",
    "standard_deviation": "stddev",
    "stdev": "stddev",
    # Volume
    "on_balance_volume": "obv",
    "price_volume_trend": "pvt",
    "volume_weighted_moving_average": "vwma",
    "money_flow_index": "mfi",
    "chaikin_money_flow": "cmf",
    "ease_of_movement": "eom",
    # Channels
    "bollinger_bands": "bbands",
    "aberration": "aberr",
    # Statistics
    "z_score": "zscore",
    # Filters
    "butterworth": "butter2",
}


def get_compat(name: str) -> Callable[..., object] | None:
    """Resolve a pandas-ta alias to the quantalib function, or None."""
    from . import indicators

    target = ALIASES.get(name)
    if target is None:
        return None
    return getattr(indicators, target, None)
