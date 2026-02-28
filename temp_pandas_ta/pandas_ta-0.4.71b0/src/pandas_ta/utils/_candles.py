# -*- coding: utf-8 -*-
from pandas import Series
from pandas_ta.utils._core import non_zero_range

__all__ = ["candle_color", "high_low_range", "real_body"]



def candle_color(open_: Series, close: Series) -> Series:
    """Candle Change

    If ```close >= open_```, returns  ```1```. Otherwise ```-1```.

    Parameters:
        open_ (Series): ```open``` Series
        close (Series): ```close``` Series

    Returns:
        (Series): 1 column
    """
    color = close.copy().astype(int)
    color[close >= open_] = 1
    color[close < open_] = -1
    return color


def high_low_range(high: Series, low: Series) -> Series:
    """High Low Range

    Returns a non-zero ```high - low```.

    Parameters:
        high (Series): ```high``` Series
        low (Series): ```low``` Series

    Returns:
        (.Series): 1 column
    """
    return non_zero_range(high, low)


def real_body(open_: Series, close: Series) -> Series:
    """Body Range

    Returns a non-zero ```close - open_```.

    Parameters:
        open_ (Series): ```open``` Series
        close (Series): ```close``` Series

    Returns:
        (Series): 1 column
    """
    return non_zero_range(close, open_)
