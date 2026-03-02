# quantalib — Python NativeAOT Wrapper

High-performance Python wrapper for [QuanTAlib](https://github.com/mihakralj/quantalib), a .NET NativeAOT technical analysis library.

## Features

- **~391 indicators** across 15 categories: channels, core, cycles, dynamics, errors, filters, momentum, numerics, oscillators, reversals, statistics, trends (FIR & IIR), volatility, volume
- **Zero-copy FFI** — ctypes bridge to pre-compiled NativeAOT shared library
- **NumPy native** — all inputs/outputs are `float64` arrays
- **Optional pandas support** — pass `pd.Series` in, get `pd.Series` out with preserved index
- **pandas-ta compatible** — `quantalib._compat` provides alias mapping for drop-in migration

## Installation

```bash
pip install quantalib
```

> **Note:** The NativeAOT shared library (`quantalib_native.dll` / `.so` / `.dylib`) must be present in `quantalib/native/<platform>/`. Pre-built binaries are included in wheel distributions.

## Quick Start

```python
import numpy as np
import quantalib as qtl

close = np.random.randn(200).cumsum() + 100

# Simple Moving Average
sma = qtl.sma(close, length=20)

# Bollinger Bands (multi-output → tuple or DataFrame)
upper, mid, lower = qtl.bbands(close, length=20, std=2.0)

# With pandas
import pandas as pd
s = pd.Series(close, name="close")
rsi = qtl.rsi(s, length=14)  # returns pd.Series with preserved index
```

## Categories

| Category | Module | Examples |
|----------|--------|----------|
| Channels | `channels` | bbands, kchannel, dchannel, aberr |
| Core | `core` | ha, midpoint, avgprice, typprice |
| Cycles | `cycles` | ht_dcperiod, ht_sine, cg, dsp |
| Dynamics | `dynamics` | adx, aroon, ichimoku, supertrend |
| Errors | `errors` | mse, rmse, mae, mape, huber |
| Filters | `filters` | kalman, sgf, hp, butter2, wavelet |
| Momentum | `momentum` | rsi, macd, roc, mom, tsi |
| Numerics | `numerics` | fft, normalize, sigmoid, slope |
| Oscillators | `oscillators` | stoch, cci, fisher, qqe, willr |
| Reversals | `reversals` | psar, pivot, fractals, swings |
| Statistics | `statistics` | zscore, correlation, entropy, linreg |
| Trends FIR | `trends_fir` | sma, wma, hma, alma, trima |
| Trends IIR | `trends_iir` | ema, dema, tema, kama, jma |
| Volatility | `volatility` | atr, bbw, stddev, hv, tr |
| Volume | `volume` | obv, vwma, mfi, cmf, adl |

## Local Development

```bash
cd python/
python -m venv .venv && .venv/Scripts/activate  # or source .venv/bin/activate
pip install -e ".[dev]"
pytest
```

### Building the native library

```bash
dotnet publish python.csproj -c Release
```

## License

[MIT](../LICENSE)
