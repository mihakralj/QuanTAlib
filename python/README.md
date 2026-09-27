# quantalib

[![PyPI](https://img.shields.io/pypi/v/quantalib?style=flat-square)](https://pypi.org/project/quantalib/)
[![Python](https://img.shields.io/pypi/pyversions/quantalib?style=flat-square)](https://pypi.org/project/quantalib/)
[![License](https://img.shields.io/pypi/l/quantalib?style=flat-square)](https://github.com/mihakralj/quantalib/blob/main/LICENSE)

447 technical analysis indicators compiled to native code via .NET NativeAOT, called from Python through `ctypes`. Same SIMD-accelerated engine as the [QuanTAlib](https://github.com/mihakralj/quantalib) .NET package. Zero Python math reimplementation.

```bash
pip install quantalib
```

## Quick Start

```python
import numpy as np
import quantalib as qtl

close = np.random.default_rng(42).normal(100, 2, size=500)

sma = qtl.sma(close, period=20)
rsi = qtl.rsi(close, period=14)
upper, mid, lower = qtl.bbands(close, period=20, std=2.0)
```

Works with **pandas**, **polars**, and **pyarrow** — same-type-in, same-type-out:

```python
# pandas — preserves index
import pandas as pd
s = pd.Series(close, name="close")
rsi = qtl.rsi(s, period=14)        # → pd.Series

# polars — zero-copy-friendly
import polars as pl
s = pl.Series("close", close)
rsi = qtl.rsi(s, period=14)        # → pl.Series
bb  = qtl.bbands(s, period=20)     # → pl.DataFrame (upper, mid, lower)

# pyarrow — for Arrow-native pipelines
import pyarrow as pa
a = pa.array(close, type=pa.float64())
rsi = qtl.rsi(a, period=14)        # → pa.Array
```

> **pandas-ta users:** `length=` is accepted everywhere as an alias for `period=`.

Install optional backends:

```bash
pip install quantalib[pandas]       # pandas / pd.Series support
pip install quantalib[polars]       # polars / pl.Series support
pip install quantalib[pyarrow]      # pyarrow / pa.Array support
pip install quantalib[all]          # all three
```

## Performance (500,000 bars)

| Indicator | quantalib | wickra | pandas-ta |
| --------- | --------: | -----: | --------: |
| SMA | 1,298 μs | 12,548 μs | 63,721 μs |
| EMA | 1,067 μs | 12,179 μs | 4,280 μs |
| WMA | 1,139 μs | 12,547 μs | 83,903 μs |
| HMA | 2,647 μs | 14,768 μs | 152,733 μs |

quantalib calls the same SIMD-optimized NativeAOT core as the .NET package through a single `ctypes` call (5-15 μs overhead). wickra (Rust/PyO3) returns stdlib `array.array`; pandas-ta stays in pure Python/numpy. For arrays above a few hundred bars, NativeAOT wins by an order of magnitude or more.

## Categories

| Category | Module | Examples |
| -------- | ------ | -------- |
| Channels | `channels` | bbands, kc, dc, aberr |
| Core | `core` | ha, midpoint, avgprice, typprice |
| Cycles | `cycles` | ht_dcperiod, ht_sine, cg, dsp |
| Dynamics | `dynamics` | adx, aroon, ichimoku, supertrend |
| Errors | `errors` | mse, rmse, mae, mape, huber |
| Filters | `filters` | kalman, sgf, hp, butter2, wavelet |
| Momentum | `momentum` | rsi, macd, roc, mom, tsi |
| Numerics | `numerics` | fft, normalize, sigmoid, slope |
| Oscillators | `oscillators` | stoch, cci, fisher, qqe, willr |
| Reversals | `reversals` | sar, pivot, fractals, swings |
| Statistics | `statistics` | zscore, correl, entropy, linreg |
| Trends FIR | `trends_fir` | sma, wma, hma, alma, trima |
| Trends IIR | `trends_iir` | ema, dema, tema, kama, jma |
| Volatility | `volatility` | atr, bbw, stddev, hv, tr |
| Volume | `volume` | obv, vwma, mfi, cmf, adl |

## Requirements

- Python 3.10+
- NumPy >= 1.24

Pre-built wheels — the NativeAOT core is compiled per platform in CI and ships inside the wheel, so there is no source build and no C++ toolchain:

| Platform | Wheel tag |
| --- | --- |
| Windows x64 | `win_amd64` |
| Linux x64 | `manylinux_2_17_x86_64` |
| Linux arm64 (AWS Graviton) | `manylinux_2_17_aarch64` |
| macOS arm64 (Apple Silicon) | `macosx_11_0_arm64` |
| macOS x64 (Intel) | `macosx_10_13_x86_64` |

### Optional dependencies

| Extra | Minimum version | Enables |
| ----- | --------------- | ------- |
| `pandas` | ≥ 1.5 | `pd.Series` / `pd.DataFrame` round-trip |
| `polars` | ≥ 0.20 | `pl.Series` / `pl.DataFrame` round-trip |
| `pyarrow` | ≥ 14.0 | `pa.Array` / `pa.ChunkedArray` round-trip |

## License

[Apache License 2.0](https://github.com/mihakralj/quantalib/blob/main/LICENSE)
