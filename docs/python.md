# Python Guide: quantalib for Data Scientists

> "You chose Python because life is short. QuanTAlib talks raw machine code directly to the CPU because nanoseconds are shorter."

## What This Is (and What It Is Not)

`quantalib` is not a Python library. It is a pre-compiled native binary wearing a Python trench coat. The math runs as raw machine code — the kind that talks directly to CPU vector registers and crunches 8 numbers simultaneously per clock tick. Think NumPy speed, except the entire indicator algorithm is fused into one native call with zero Python loops. Half a million bars of SMA in 328 microseconds. That is faster than your monitor can refresh a single frame.

The `pip install` delivers a ready-to-run native binary for your platform. No compilation step, no build tools, no waiting. Just import and go.

This means:

- **Batch only.** You pass in an array, you get back an array. No streaming, no bar-by-bar updates. (The .NET version does streaming at 0.4 μs per update. Python's function-call overhead would eat that alive.)
- **Same numbers.** The results are identical to the C# core library, bit for bit. Cross-validated against TA-Lib, Tulip, Skender, and half a dozen other implementations nobody remembers.
- **393 indicators.** Not 12. Not "the popular ones." All of them. From SMA to Yang-Zhang Volatility Adaptive Moving Average.

## Installation

```bash
pip install quantalib
```

Pre-built wheels ship for all 6 combinations of Windows, Linux, and macOS on both x64 and ARM64. If your platform is missing, you are doing something creative.

### Optional backends

```bash
pip install quantalib[pandas]     # pd.Series round-trip
pip install quantalib[polars]     # pl.Series round-trip
pip install quantalib[pyarrow]    # pa.Array round-trip
pip install quantalib[all]        # all three, because indecision is valid
```

Without any optional backend, plain NumPy arrays work. Always have. Always will.

## Finding Your Indicator

Every indicator lives in one of 15 category modules. You do not need to know which module contains what: the top-level `quantalib` namespace re-exports everything.

```python
import quantalib as qtl

# These are identical:
result = qtl.sma(prices, period=20)
result = qtl.trends_fir.sma(prices, period=20)
```

If you know the indicator name, call it. If you do not, here is the map:

| Category | Module | What It Measures | Examples |
| :--- | :--- | :--- | :--- |
| **Core** | `core` | Price transforms, building blocks | `avgprice`, `medprice`, `typprice`, `ha` |
| **Trends (FIR)** | `trends_fir` | Finite impulse response averages | `sma`, `wma`, `hma`, `alma`, `trima` |
| **Trends (IIR)** | `trends_iir` | Infinite impulse response averages | `ema`, `dema`, `tema`, `kama`, `jma` |
| **Filters** | `filters` | Signal processing, noise reduction | `kalman`, `sgf`, `butter2`, `gauss` |
| **Oscillators** | `oscillators` | Bounded/centered oscillators | `stoch`, `rsi`, `cci`, `fisher`, `willr` |
| **Dynamics** | `dynamics` | Trend strength and direction | `adx`, `aroon`, `supertrend`, `ichimoku` |
| **Momentum** | `momentum` | Speed of price changes | `roc`, `mom`, `macd`, `tsi`, `vel` |
| **Volatility** | `volatility` | Price variability | `atr`, `bbw`, `stddev`, `hv`, `tr` |
| **Volume** | `volume` | Trading activity | `obv`, `vwma`, `mfi`, `cmf`, `adl` |
| **Statistics** | `statistics` | Statistical measures | `zscore`, `correl`, `entropy` |
| **Channels** | `channels` | Price boundaries | `bbands`, `kc`, `dc` |
| **Cycles** | `cycles` | Cycle analysis | `ht_dcperiod`, `ht_sine`, `cg`, `dsp` |
| **Reversals** | `reversals` | Pattern detection | `sar`, `pivot`, `fractals`, `swings` |
| **Errors** | `errors` | Error metrics, loss functions | `rmse`, `mae`, `mape`, `smape` |
| **Numerics** | `numerics` | Mathematical transforms | `fft`, `normalize`, `sigmoid`, `slope` |

**Full indicator catalog with descriptions: [393 indicators](../lib/_index.md)**

## Calling Convention

Simple indicators follow this pattern:

```python
result = qtl.indicator_name(source_data, period=N, offset=0)
```

Many indicators have their own parameters — multiple periods, multipliers, smoothing factors, phase controls. Use your IDE's autocomplete or `help()` to see what each function accepts. A few representative examples:

```python
# Simple: one period
sma = qtl.sma(close, period=20)

# Multiple periods
macd_line, signal, hist = qtl.macd(close, fastPeriod=12, slowPeriod=26)

# Period + multiplier
upper, mid, lower = qtl.bbands(close, bbPeriod=20, bbMult=2.0)

# Complex: many named parameters
gator_upper, gator_lower = qtl.gator(close, jawPeriod=13, jawShift=8,
    teethPeriod=8, teethShift=5, lipsPeriod=5, lipsShift=3)

# Filter with float controls
filtered = qtl.kalman(close, q=0.01, r=0.1)
```

### Common parameters

- **`source_data`**: NumPy array, pandas Series, polars Series, or PyArrow Array. The library detects the type and returns the same type.
- **`period`**: The primary lookback window (where applicable). This is the canonical name. If you are coming from `pandas-ta`, `length=` works too — it is silently aliased.
- **`offset`**: Shift the output by N bars. Default 0. Positive values shift right.
- **`**kwargs`**: Every function accepts `**kwargs` for forward compatibility and aliasing.

### Input patterns

Most indicators take a single source series. Some need OHLCV data:

```python
# Single source (Pattern A): most indicators
sma = qtl.sma(close, period=20)

# High-Low-Close (Pattern E): ATR, channels
atr = qtl.atr(high, low, close, period=14)

# OHLCV (Pattern B): volume indicators, dynamics
adx = qtl.adx(open, high, low, close, volume, period=14)

# Dual source (Pattern F): error metrics
rmse = qtl.rmse(actual, predicted, period=20)

# Source + Volume (Pattern G): volume-weighted indicators
vwma = qtl.vwma(close, volume, period=20)
```

### Return types

| Input type | Single output | Multi output |
| :--- | :--- | :--- |
| `np.ndarray` | `np.ndarray` | `tuple[np.ndarray, ...]` |
| `pd.Series` | `pd.Series` (preserves index) | `pd.DataFrame` |
| `pl.Series` | `pl.Series` | `pl.DataFrame` |
| `pa.Array` | `pa.Array` | `dict[str, pa.Array]` |

Multi-output indicators (Bollinger Bands, Stochastic, MACD, Ichimoku) return multiple arrays. Unpack them:

```python
upper, mid, lower = qtl.bbands(close, period=20, std=2.0)
k, d = qtl.stoch(high, low, close, kLength=14, dPeriod=3)
```

## Working with DataFrames

### pandas

```python
import pandas as pd
import quantalib as qtl

df = pd.read_csv("ohlcv.csv", parse_dates=["date"], index_col="date")

df["sma_20"] = qtl.sma(df["close"], period=20)
df["rsi_14"] = qtl.rsi(df["close"], period=14)
df["atr_14"] = qtl.atr(df["high"], df["low"], df["close"], period=14)

# Multi-output unpacks into separate columns
df["bb_upper"], df["bb_mid"], df["bb_lower"] = qtl.bbands(
    df["close"], period=20, std=2.0
)
```

The pandas index survives the round-trip. The output Series inherits the input's index, gets a name like `"SMA_20"`, and stores the category in `.attrs["category"]`.

### polars

```python
import polars as pl
import quantalib as qtl

df = pl.read_csv("ohlcv.csv")

df = df.with_columns(
    qtl.sma(df["close"], period=20).alias("sma_20"),
    qtl.rsi(df["close"], period=14).alias("rsi_14"),
)
```

### pyarrow

```python
import pyarrow as pa
import pyarrow.parquet as pq
import quantalib as qtl

table = pq.read_table("ohlcv.parquet")
close = table.column("close").combine_chunks()

rsi = qtl.rsi(close, period=14)   # pa.Array
```

## Warmup Behavior

QuanTAlib produces output from bar 1. There are no NaN gaps at the beginning. A 20-period SMA with only 5 bars returns the average of those 5 bars — not the 20-period average (that would require prescience), but a mathematically defensible estimate that improves as data accumulates.

```python
sma = qtl.sma(prices, period=20)
# sma[0] has a value — the best estimate given 1 bar
# sma[19] is the first fully-converged 20-period SMA
# All 20 values are finite numbers, not NaN
```

Early values are usable approximations, not garbage. This is a deliberate design decision. Other libraries leave NaN gaps. QuanTAlib fills them.

If your input data itself contains NaN (e.g., missing bars), those propagate through as the last valid value — QuanTAlib substitutes rather than spreading the disease.

## pandas-ta Migration

Coming from `pandas-ta`? Two things changed:

1. **Parameter name**: `period` is canonical. `length` still works as an alias.
2. **Function names**: Most are identical. For ambiguous cases, use the compatibility layer:

```python
from quantalib._compat import get_compat

# Resolve a pandas-ta name to a quantalib function
fn = get_compat("bbands")
if fn:
    result = fn(close, period=20)
```

## Performance Reality Check

The native engine bypasses Python entirely for the math. No interpreter loop, no garbage collector pauses, no GIL contention. Your data goes in as a memory pointer, the CPU grinds through it at hardware speed, and the result comes back as a NumPy array. The Python overhead (marshaling the pointer, wrapping the result) adds about 10 μs — roughly the time it takes to blink, divided by 10,000.

To put the numbers in perspective:

| Indicator | quantalib (500K bars) | pandas-ta (500K bars) | How much faster |
| :--- | ---: | ---: | :--- |
| SMA | 328 μs (⅓ of a millisecond) | ~50 ms | **150×** faster |
| EMA | 421 μs | ~45 ms | **107×** faster |
| RSI | 517 μs | ~80 ms | **155×** faster |

What does 150× mean in practice? If your pandas-ta backtest over 2,000 symbols takes **8 hours**, quantalib finishes the same work in **3 minutes**. That is the difference between "run it overnight and hope" and "run it while the coffee brews."

## Error Handling

The C ABI returns status codes. The Python bridge converts them to exceptions:

```python
from quantalib._bridge import QtlInvalidLengthError, QtlInvalidParamError

try:
    qtl.sma(prices, period=0)     # period must be > 0
except QtlInvalidLengthError:
    print("Period must be positive")

try:
    qtl.sma(np.array([]))         # empty array
except QtlInvalidLengthError:
    print("Array too short")
```

## Platform Support

| Platform | Architecture | Status |
| :--- | :--- | :--- |
| Windows | x64 | ✅ Pre-built wheel |
| Windows | ARM64 | ✅ Pre-built wheel |
| Linux | x64 | ✅ Pre-built wheel |
| Linux | ARM64 (aarch64) | ✅ Pre-built wheel |
| macOS | x64 | ✅ Pre-built wheel |
| macOS | ARM64 (Apple Silicon) | ✅ Pre-built wheel |

The native shared library (`quantalib.dll` / `libquantalib.so` / `libquantalib.dylib`) is bundled inside the wheel. No separate installation, no system dependencies, no `cmake` rituals.

## Going Deeper

- **[Full indicator catalog](../lib/_index.md)**: Every indicator with mathematical descriptions
- **[Architecture](architecture.md)**: How the SIMD engine works
- **[Benchmarks](benchmarks.md)**: Performance numbers with methodology
- **[Validation](validation.md)**: Cross-library verification matrices
- **[API Reference](api.md)**: The .NET API (for when Python is not enough)
