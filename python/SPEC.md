# quantalib Python NativeAOT Wrapper — Specification v2

## 1) Objective

Deliver a Python package named **`quantalib`** that exposes the entire QuanTAlib
indicator library through a stable, versioned C ABI compiled via .NET NativeAOT.
Python ergonomics follow pandas-ta conventions where practical.

### Primary goals

- Near-zero-copy interop via `ctypes` + contiguous NumPy buffers (zero-copy when inputs are already C-contiguous `float64`; otherwise one normalization copy is allowed)
- Stable C ABI surface (`qtl_*`) with explicit major-version evolution policy for future non-Python consumers
- Full library coverage target: all supported `Batch` methods exported using a generated manifest and validated rollout gates
- pandas-ta-compatible function signatures for common indicators

### Non-goals (initial release)

- Rewriting indicator math in Python
- pybind11 / Cython extension modules
- GPU acceleration
- Streaming / stateful Python classes per indicator
- Alternate numeric dtypes (`float32`, decimal)
- Full pandas-ta edge-case parity on day one

---

## 2) Scope

### In scope

- `python/` directory at repository root
- NativeAOT shared library (`quantalib.dll` / `quantalib.so` / `quantalib.dylib`)
- Single `Exports.cs` mapping all `Batch` methods to `[UnmanagedCallersOnly]` exports
- Python package `quantalib` with loader, bridge, and indicator wrappers
- Compatibility matrix documenting pandas-ta parity levels
- CI matrix for `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`

### Out of scope

- Windows ARM64 packaging
- Async / streaming Python APIs
- TBarSeries / BatchInputs overloads (use flat span-based overloads only)

---

## 3) Design requirements

1. **ABI correctness** — pointer, length, and parameter contracts are explicit and validated on both sides of the boundary.
2. **Performance** — call QuanTAlib `Batch` methods directly; no managed allocations in the export shim beyond span construction.
3. **ABI stability** — export names are prefixed (`qtl_*`) and versioned; existing symbols never change within a major version.
4. **Deterministic behavior** — warmup slots filled with `NaN`; invalid inputs produce defined status codes, never exceptions.
5. **pandas-ta compatibility** — Python names and signatures mirror pandas-ta where feasible, with documented deltas.
6. **Thread safety** — exports are designed to be stateless and reentrant; concurrent calls are supported when each call uses distinct buffers. This must be validated by stress tests in CI.

---

## 4) Repository layout

```text
python/
  SPEC.md                          # this file
  python.csproj                    # NativeAOT shared lib project
  Directory.Build.props            # local build overrides
  publish.ps1                      # multi-RID publish + stage script
  pyproject.toml                   # Python package metadata
  README.md                        # usage docs + indicator list

  src/
    StatusCodes.cs                 # QTL_OK / QTL_ERR_* constants
    ArrayBridge.cs                 # unsafe ptr→span helpers + validation
    Exports.cs                     # ALL [UnmanagedCallersOnly] entry points

  quantalib/
    __init__.py                    # re-export indicators; __version__
    _loader.py                     # platform+arch dispatch; ctypes.CDLL
    _bridge.py                     # ctypes argtypes/restype declarations
    indicators.py                  # pandas-ta-compatible wrappers
    _compat.py                     # pandas-ta name aliases + helpers
    py.typed                       # PEP 561 marker

    native/
      win_amd64/.gitkeep
      linux_x86_64/.gitkeep
      macosx_arm64/.gitkeep
      macosx_x86_64/.gitkeep

  tests/
    test_smoke.py                  # import + load + call one indicator
    test_shapes.py                 # output length == input length
    test_status_codes.py           # null ptr, bad length, bad params
    test_golden.py                 # golden-value vs managed QuanTAlib
    test_compat.py                 # pandas-ta parity subset
```

**Design note:** `Exports.cs` and `indicators.py` stay as single flat files.
Internal splitting is allowed later without changing any public API or ABI.

---

## 5) Native ABI contract

### 5.1 Calling convention

- All exports use `[UnmanagedCallersOnly(EntryPoint = "qtl_<name>")]`.
- All exports return `int` status code.
- All pointer parameters use C ABI-compatible primitive types only.
- No managed exceptions ever cross the ABI boundary. Every export wraps its body
  in a `try/catch` that returns `QTL_ERR_INTERNAL` on unhandled exceptions.

### 5.2 Status codes

| Code | Name | Meaning |
|------|------|---------|
| `0` | `QTL_OK` | Success |
| `1` | `QTL_ERR_NULL_PTR` | Required pointer is null |
| `2` | `QTL_ERR_INVALID_LENGTH` | `n <= 0` or output length mismatch |
| `3` | `QTL_ERR_INVALID_PARAM` | Parameter out of valid range |
| `4` | `QTL_ERR_INTERNAL` | Unhandled exception in managed code |

Failure guarantees:
- For validation failures (`QTL_ERR_NULL_PTR`, `QTL_ERR_INVALID_LENGTH`, `QTL_ERR_INVALID_PARAM`), outputs are untouched by contract.
- For `QTL_ERR_INTERNAL`, output buffers are unspecified and must be treated as invalid by the caller.

### 5.3 ABI signature patterns

The ~290 `Batch` methods in QuanTAlib follow 9 distinct signature patterns.
Each pattern maps to a specific C ABI template:

#### Pattern A: Single-input, single-output + scalar params

**C# source:** `Sma.Batch(ReadOnlySpan<double> source, Span<double> output, int period)`

**C ABI:**

```c
int qtl_sma(double* src, int n, double* out, int period);
```

**Indicators (~130):** SMA, EMA, DEMA, TEMA, HMA, WMA, RSI, ROC, MOM, CMO,
TSI, APO, PPO, DPO, TRIX, Fisher, Inertia, Zscore, StdDev, Variance, etc.

#### Pattern B: Multi-input HLCV, single-output

**C# source:** `Mfi.Batch(ReadOnlySpan<double> high, ..low, ..close, ..volume, Span<double> output, int period)`

**C ABI:**

```c
int qtl_mfi(double* high, double* low, double* close, double* volume,
               int n, double* out, int period);
```

**Indicators (~25):** MFI, ADL, CMF, VWAP, VWAD, ADOsc, KVO, III, WAD, VA, EOM.

#### Pattern C: Multi-input OHLC (no volume), single-output

**C# source:** `Bop.Batch(ReadOnlySpan<double> open, ..high, ..low, ..close, Span<double> destination)`

**C ABI:**

```c
int qtl_bop(double* open, double* high, double* low, double* close,
               int n, double* out);
```

**Indicators (~20):** BOP, ASI, BRAR, Avgprice, Typprice, Wclprice, Midbody,
HA, ReverseEMA variants requiring OHLC, RVGI, DEM, etc.

#### Pattern D: Multi-input HL, single-output

**C# source:** `Ao.Batch(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> destination, int fast, int slow)`

**C ABI:**

```c
int qtl_ao(double* high, double* low, int n, double* out,
              int fast_period, int slow_period);
```

**Indicators (~10):** AO, AC, Medprice, Frama (HL overload), WillR, etc.

#### Pattern E: Multi-input HLC (no volume), single-output

**C# source:** `Tr.Batch(ReadOnlySpan<double> high, ..low, ..close, Span<double> output)`

**C ABI:**

```c
int qtl_tr(double* high, double* low, double* close, int n, double* out);
```

**Indicators (~15):** TR, ATR, Etherm, GHLA, PGO, RWMA, etc.

#### Pattern F: Dual-input (actual + predicted), single-output

**C# source:** `Mse.Batch(ReadOnlySpan<double> actual, ..predicted, Span<double> output, int period)`

**C ABI:**

```c
int qtl_mse(double* actual, double* predicted, int n, double* out, int period);
```

**Indicators (~25):** MSE, RMSE, MAE, MAPE, SMAPE, RMSLE, Rsquared, Huber,
PseudoHuber, TheilU, WMAPE, LogCosh, QuantileLoss, TukeyBiweight, etc.

#### Pattern G: Dual-input (source + volume), single-output

**C# source:** `Obv.Batch(ReadOnlySpan<double> close, ReadOnlySpan<double> volume, Span<double> output)`

**C ABI:**

```c
int qtl_obv(double* close, double* volume, int n, double* out);
```

**Indicators (~12):** OBV, PVT, PVR, VF, EFI, NVI, PVI, VWMA, EVWMA, TVI, PVD.

#### Pattern H: Dual-input statistics (seriesX + seriesY)

**C# source:** `Correlation.Batch(ReadOnlySpan<double> seriesX, ..seriesY, Span<double> output, int period)`

**C ABI:**

```c
int qtl_correlation(double* x, double* y, int n, double* out, int period);
```

**Indicators (~6):** Correlation, Covariance, Spearman, Kendall, Granger,
Cointegration.

#### Pattern I: Multi-output indicators

**C# source (example):** `Pvo.Batch(ReadOnlySpan<double> volume, Span<double> output, Span<double> signal, Span<double> histogram, ...)`

**C ABI:** Each output gets its own pointer parameter:

```c
int qtl_pvo(double* volume, int n,
               double* out_pvo, double* out_signal, double* out_hist,
               int fast, int slow, int signal_period);
```

**Multi-output indicators:**

| Indicator | Outputs | Names |
|-----------|---------|-------|
| PVO | 3 | pvo, signal, histogram |
| PMA | 2 | pma, trigger |
| MAMA | 2 | mama, fama |
| HtPhasor | 2 | inPhase, quadrature |
| HtSine | 2 | sine, leadSine |
| AOBV | 2 | fast, slow |
| AMAT | 2 | trend, strength |
| Stoch | 2 | K, D |
| Stochf | 2 | K, D |
| KDJ | 3 | K, D, J |
| SMI | 2+ | smi, signal |
| BBands | 3 | upper, middle, lower |
| KChannel | 3 | upper, middle, lower |
| DCChannel | 3 | upper, middle, lower |
| AccBands | 3 | upper, middle, lower |
| AtrBands | 3 | upper, middle, lower |
| All channel indicators | 3 | upper, middle, lower |
| Vortex | 2 | viPlus, viMinus |
| Aroon | 1* | combined |

### 5.4 Special parameter mappings

Several `Batch` methods use types that cannot cross the C ABI directly.
Each requires a specific mapping:

| C# type | C ABI type | Strategy |
|---------|-----------|----------|
| `enum StcSmoothing` | `int` | 0 = SMA, 1 = EMA |
| `enum WindowType` (Afirma) | `int` | Map enum ordinals |
| `bool` (isPopulation, annualize, etc.) | `int` | 0 = false, 1 = true |
| `int[]? lengths` (Cfb) | `int* lengths, int lengths_count` | Null-safe; null → use defaults |
| `double[] kernel` (Conv) | `double* kernel, int kernel_len` | Caller allocates |
| `ReadOnlySpan<long>` (Solar, Lunar) | `long* timestamps` | Direct pointer cast |
| `TBarSeries` overloads | **Skip** | Use flat span-based overload instead |
| `BatchInputs`/`BatchOutput` struct overloads | **Skip** | Use flat span-based overload instead |

### 5.5 Dual overloads (period vs alpha)

Many IIR filters expose both `Batch(src, out, int period)` and
`Batch(src, out, double alpha)`. Export strategy:

- Primary export: `qtl_ema` — takes `int period`
- Alpha export: `qtl_ema_alpha` — takes `double alpha`

Both are exported; Python wrapper defaults to the period variant.

### 5.6 Validation in each export

Every export function validates before calling the inner `Batch`:

1. Null checks for all required pointer parameters → `QTL_ERR_NULL_PTR`
2. `n > 0` → `QTL_ERR_INVALID_LENGTH`
3. Range checks on scalar parameters (e.g., `period > 0`) → `QTL_ERR_INVALID_PARAM`
4. Semantic checks (e.g., `fastPeriod < slowPeriod` where required) → `QTL_ERR_INVALID_PARAM`
5. `try/catch` around `Batch` call → `QTL_ERR_INTERNAL` on any managed exception

### 5.7 Memory ownership rules

- Input pointers are **read-only** by contract (cast to `ReadOnlySpan<double>`).
- Caller allocates **all** output buffers with length `n`.
- Native layer writes exactly `n` values per output buffer on success.
- No ownership transfer across boundary. No heap allocation in the shim.

### 5.8 ABI versioning policy

- All exports use the flat `qtl_` prefix for v1.
- Existing published symbols are immutable within ABI major v1.
- New optional parameters require new symbol names (e.g. `qtl_ema_alpha`).
- Deprecation policy: old symbols remain exported for at least one minor release after replacement and are marked deprecated in Python wrappers.
- First breaking ABI revision introduces `qtl2_` prefix and parallel support window.

### 5.9 Export manifest (authoritative source)

To reduce drift across `Exports.cs`, `_bridge.py`, and `indicators.py`, maintain one machine-readable manifest (e.g., JSON/YAML) containing:

- indicator name and export symbol
- input pattern (A–I or special)
- parameter schema (name, type, defaults, constraints)
- output schema (count, names, order)

Generated artifacts from this manifest are preferred over hand-maintained parallel lists.

### 5.10 Concrete export example

```csharp
// StatusCodes.cs
file static class StatusCodes
{
    public const int QTL_OK = 0;
    public const int QTL_ERR_NULL_PTR = 1;
    public const int QTL_ERR_INVALID_LENGTH = 2;
    public const int QTL_ERR_INVALID_PARAM = 3;
    public const int QTL_ERR_INTERNAL = 4;
}

// Exports.cs (excerpt)
[UnmanagedCallersOnly(EntryPoint = "qtl_sma")]
public static unsafe int Sma(double* src, int n, double* output, int period)
{
    if (src == null || output == null) return StatusCodes.QTL_ERR_NULL_PTR;
    if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
    if (period <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
    try
    {
        var srcSpan = new ReadOnlySpan<double>(src, n);
        var outSpan = new Span<double>(output, n);
        QuanTAlib.Sma.Batch(srcSpan, outSpan, period);
        return StatusCodes.QTL_OK;
    }
    catch { return StatusCodes.QTL_ERR_INTERNAL; }
}

[UnmanagedCallersOnly(EntryPoint = "qtl_bbands")]
public static unsafe int Bbands(
    double* src, int n,
    double* outUpper, double* outMiddle, double* outLower,
    int period, double multiplier)
{
    if (src == null || outUpper == null || outMiddle == null || outLower == null)
        return StatusCodes.QTL_ERR_NULL_PTR;
    if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
    if (period <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
    try
    {
        var srcSpan = new ReadOnlySpan<double>(src, n);
        var upper = new Span<double>(outUpper, n);
        var middle = new Span<double>(outMiddle, n);
        var lower = new Span<double>(outLower, n);
        QuanTAlib.Bbands.Batch(srcSpan, upper, middle, lower, period, multiplier);
        return StatusCodes.QTL_OK;
    }
    catch { return StatusCodes.QTL_ERR_INTERNAL; }
}
```

---

## 6) Python contract

### 6.1 Package identity

| Property | Value |
|----------|-------|
| PyPI name | `quantalib` |
| Import name | `quantalib` |
| AssemblyName | `quantalib` |

### 6.2 Loader (`_loader.py`)

- Detect OS + architecture at import time.
- Load bundled shared library from `quantalib/native/<platform>/`.
- Platform directory mapping:

| Platform | Directory |
|----------|-----------|
| Windows x64 | `native/win_amd64/quantalib.dll` |
| Linux x64 | `native/linux_x86_64/quantalib.so` |
| macOS ARM64 | `native/macosx_arm64/quantalib.dylib` |
| macOS x64 | `native/macosx_x86_64/quantalib.dylib` |

- On failure: raise `OSError` with actionable message listing expected path
  and detected platform.
- Use explicit package-path loading; never rely on system library search order.

### 6.3 Bridge (`_bridge.py`)

- Declare `argtypes` and `restype` for every native function at module level.
- Validate function presence at import time (graceful `AttributeError` → skip).
- Input normalization: `np.ascontiguousarray(arr, dtype=np.float64)`.
- Status code checking: helper raises Python exception mapped from return code:

```python
class QtlError(Exception):
    """Base exception for quantalib native errors."""

class QtlNullPointerError(QtlError): ...    # status 1
class QtlInvalidLengthError(QtlError): ...  # status 2
class QtlInvalidParamError(QtlError): ...   # status 3
class QtlInternalError(QtlError): ...       # status 4
```

### 6.4 Indicator wrappers (`indicators.py`)

- Expose pandas-ta-compatible function signatures where practical.
- Return types:

| Input type | Output (single) | Output (multi) |
|------------|-----------------|----------------|
| `pd.Series` | `pd.Series` | `pd.DataFrame` |
| `pd.DataFrame` | `pd.Series` | `pd.DataFrame` |
| `np.ndarray` | `np.ndarray` | tuple of `np.ndarray` |

- Series names follow pandas-ta conventions: `SMA_14`, `BBU_20_2.0`, etc.
- DataFrame columns for multi-output: `BBU_20_2.0`, `BBM_20_2.0`, `BBL_20_2.0`.

### 6.5 Concrete Python wrapper example

```python
# indicators.py (excerpt)
import numpy as np
import pandas as pd
from quantalib._bridge import _lib, _check

def sma(close, length=None, offset=None, **kwargs):
    """Simple Moving Average."""
    length = int(length) if length else 10
    offset = int(offset) if offset else 0

    # Extract numpy array
    index = None
    if isinstance(close, pd.Series):
        index = close.index
        close = close.to_numpy(dtype=np.float64, copy=False)
    close = np.ascontiguousarray(close, dtype=np.float64)

    n = len(close)
    out = np.empty(n, dtype=np.float64)
    _check(_lib.qtl_sma(
        close.ctypes.data_as(c_double_p), n,
        out.ctypes.data_as(c_double_p), length
    ))

    if offset:
        out = np.roll(out, offset)
        out[:offset] = np.nan

    if index is not None:
        result = pd.Series(out, index=index, name=f"SMA_{length}")
        result.category = "trend"
        return result
    return out
```

### 6.6 NaN / warmup behavior

- Warmup slots are filled with `NaN` by the C# `Batch` methods.
- This matches pandas-ta's convention for most indicators.
- Known deltas from pandas-ta warmup lengths are documented in the
  compatibility table in `README.md`.

### 6.7 pandas fallback policy

When pandas is not installed:

- Functions accept and return `np.ndarray` only.
- `pd.Series` / `pd.DataFrame` input raises `ImportError` with message
  `"pandas required for Series/DataFrame input"`.
- `numpy` is a hard dependency; `pandas` is an optional extra.

---

## 7) Build and packaging

### 7.1 .NET project (`python.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <PublishAot>true</PublishAot>
    <AssemblyName>quantalib</AssemblyName>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <InvariantGlobalization>true</InvariantGlobalization>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="src\**\*.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\lib\quantalib.csproj" />
  </ItemGroup>
</Project>
```

### 7.2 Local build props (`Directory.Build.props`)

```xml
<Project>
  <PropertyGroup>
    <GitVersionSkip>true</GitVersionSkip>
    <IlcOptimizationPreference>Speed</IlcOptimizationPreference>
    <RunAnalyzers>false</RunAnalyzers>
    <ErrorReport>none</ErrorReport>
  </PropertyGroup>
</Project>
```

### 7.3 Publish script (`publish.ps1`)

Requirements:

- Publish for RIDs: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`
- Clean target directory before each publish
- Verify expected artifact exists per RID (`.dll`, `.so`, `.dylib`)
- Copy artifact into `quantalib/native/<platform>/`
- Exit non-zero on any missing artifact

### 7.4 Python packaging (`pyproject.toml`)

```toml
[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[project]
name = "quantalib"
version = "0.1.0"
description = "High-performance technical analysis via QuanTAlib NativeAOT"
requires-python = ">=3.10"
dependencies = ["numpy>=1.24"]

[project.optional-dependencies]
pandas = ["pandas>=1.5"]
dev = ["pytest", "pandas>=1.5"]

[tool.hatch.build.targets.wheel]
packages = ["quantalib"]
```

### 7.5 NativeAOT considerations

- **Trimming:** NativeAOT trims unreachable code. All `Batch` methods are
  reachable via explicit calls in `Exports.cs`, so no `rd.xml` needed.
- **Reflection:** QuanTAlib indicators do not use reflection in `Batch` paths.
- **SIMD:** NativeAOT preserves hardware intrinsics. `Avx2.IsSupported` works
  at runtime in the compiled native binary.
- **Startup:** NativeAOT has near-zero startup overhead. No JIT warmup.

---

## 8) Full indicator catalog by category

### 8.1 Core (7 indicators)

| Indicator | C# Class | Pattern | Params |
|-----------|----------|---------|--------|
| avgprice | `Avgprice` | C (OHLC) | — |
| ha | `Ha` | C (OHLC) | — |
| medprice | `Medprice` | D (HL) | — |
| midbody | `Midbody` | C (OC) | — |
| midpoint | `Midpoint` | A | period |
| midprice | `Midprice` | D (HL) | period |
| typprice | `Typprice` | C (OHLC) | — |
| wclprice | `Wclprice` | E (HLC) | — |

### 8.2 Momentum (18 indicators)

| Indicator | C# Class | Pattern | Params |
|-----------|----------|---------|--------|
| asi | `Asi` | C (OHLC) | period, limitMove |
| bias | `Bias` | A | period |
| bop | `Bop` | C (OHLC) | — |
| cfb | `Cfb` | A + int[]* | lengths |
| cmo | `Cmo` | A | period |
| macd | `Macd` | A | fastPeriod, slowPeriod |
| mom | `Mom` | A | period |
| pmo | `Pmo` | A | timePeriods, smoothPeriods, signalPeriods |
| ppo | `Ppo` | A | fastPeriod, slowPeriod |
| prs | `Prs` | H (dual) | period |
| roc | `Roc` | A | period |
| rocp | `Rocp` | A | period |
| rocr | `Rocr` | A | period |
| rsi | `Rsi` | A | period |
| rsx | `Rsx` | A | period |
| sam | `Sam` | A | alpha, cutoff |
| tsi | `Tsi` | A | longPeriod, shortPeriod |
| vel | `Vel` | A | period |

### 8.3 Oscillators (~35 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| ac | `Ac` | D (HL) | fastPeriod, slowPeriod, acPeriod |
| ao | `Ao` | D (HL) | fastPeriod, slowPeriod |
| apo | `Apo` | A | fastPeriod, slowPeriod |
| bbb | `Bbb` | A | period, multiplier |
| bbi | `Bbi` | A | periods... |
| bbs | `Bbs` | E (HLC) | multi-output |
| brar | `Brar` | C (OHLC) | period |
| cfo | `Cfo` | A | period |
| coppock | `Coppock` | A | roc1, roc2, wma |
| crsi | `Crsi` | A | rsiPeriod, streakPeriod, rankPeriod |
| cti | `Cti` | A | period |
| deco | `Deco` | A | shortPeriod, longPeriod |
| dem | `Dem` | E (HLC) | period |
| dpo | `Dpo` | A | period |
| dosc | `Dosc` | A | rsiPeriod, ema1Period, ema2Period, sigPeriod |
| dymoi | `Dymoi` | A | basePeriod, shortPeriod, longPeriod... |
| er | `Er` | A | period |
| fisher | `Fisher` | A | period, alpha |
| fisher04 | `Fisher04` | A | period |
| gator | `Gator` | A | jawPeriod... |
| imi | `Imi` | C (OHLC) | period |
| inertia | `Inertia` | A | period |
| kdj | `Kdj` | I (HLC→3) | period, kSmooth, dSmooth |
| kri | `Kri` | A | period |
| kst | `Kst` | A | multiple roc/ma periods |
| marketfi | `Marketfi` | D (HL) + vol | — |
| mstoch | `Mstoch` | A | period |
| pgo | `Pgo` | E (HLC) | period |
| psl | `Psl` | A | period |
| qqe | `Qqe` | A | rsiPeriod, smoothFactor, qqeFactor |
| reflex | `Reflex` | A | period |
| reverseema | `ReverseEma` | A | period |
| rvgi | `Rvgi` | C (OHLC) | period |
| smi | `Smi` | I (HLC→2+) | period, smoothK, smoothD |
| squeeze | `Squeeze` | I (HLC→multi) | bbPeriod, kcPeriod... |
| stc | `Stc` | A + enum | kPeriod, dPeriod, fastLen, slowLen, smoothing(int) |
| stoch | `Stoch` | I (HLC→2) | kPeriod, kSmooth, dSmooth |
| stochf | `Stochf` | I (HLC→2) | kPeriod, dPeriod |
| stochrsi | `Stochrsi` | A | rsiLen, stochLen, kSmooth, dSmooth |
| td_seq | `Td_seq` | C (OHLC) | — |
| trendflex | `Trendflex` | A | period |
| trix | `Trix` | A | period |
| ttm_wave | `TtmWave` | I (HLC→multi) | — |
| ultosc | `Ultosc` | E (HLC) | period1, period2, period3 |
| willr | `Willr` | E (HLC) | period |

### 8.4 Trends — FIR (~30 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| alma | `Alma` | A | period, offset, sigma |
| blma | `Blma` | A | period |
| bwma | `Bwma` | A | period, order |
| conv | `Conv` | A + double[]* | kernel |
| crma | `Crma` | A | period |
| dwma | `Dwma` | A | period |
| fwma | `Fwma` | A | period |
| gwma | `Gwma` | A | period, sigma |
| hamma | `Hamma` | A | period |
| hanma | `Hanma` | A | period |
| hend | `Hend` | A | period |
| hma | `Hma` | A | period |
| ilrs | `Ilrs` | A | period |
| kaiser | `Kaiser` | A | period, beta |
| lanczos | `Lanczos` | A | period |
| lsma | `Lsma` | A | period, offset |
| nlma | `Nlma` | A | period |
| nyqma | `Nyqma` | A | period, nyquistPeriod |
| parzen | `Parzen` | A | period |
| pma | `Pma` | A / I (2) | period |
| pwma | `Pwma` | A | period |
| qrma | `Qrma` | A | period |
| rain | `Rain` | A | period |
| rwma | `Rwma` | D (CHL) | period |
| sgma | `Sgma` | A | period, degree |
| sinema | `Sinema` | A | period |
| sma | `Sma` | A | period |
| sp15 | `Sp15` | A | — |
| swma | `Swma` | A | period |
| trima | `Trima` | A | period |
| tsf | `Tsf` | A | period |
| tukey_w | `Tukey_w` | A | period, alpha |
| wma | `Wma` | A | period |

### 8.5 Trends — IIR (~30 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| ahrens | `Ahrens` | A | period |
| coral | `Coral` | A | period, cd |
| decycler | `Decycler` | A | period |
| dema | `Dema` | A (+ alpha) | period |
| dsma | `Dsma` | A | period |
| ema | `Ema` | A (+ alpha) | period |
| frama | `Frama` | A / D (HL) | period |
| gdema | `Gdema` | A | period, vfactor |
| hema | `Hema` | A | period |
| holt | `Holt` | A | period, gamma |
| htit | `Htit` | A | — |
| hwma | `Hwma` | A | period |
| jma | `Jma` | A | period, phase, power |
| kama | `Kama` | A | period, fastPeriod, slowPeriod |
| lema | `Lema` | A (+ alpha) | period |
| ltma | `Ltma` | A | period |
| mama | `Mama` | I (2) | fastLimit, slowLimit |
| mavp | `Mavp` | H (src+periods) | minPeriod, maxPeriod |
| mcnma | `Mcnma` | A (+ alpha) | period |
| mgdi | `Mgdi` | A | period, k |
| mma | `Mma` | A | period |
| nma | `Nma` | A | period |
| qema | `Qema` | A | period |
| rema | `Rema` | A | period, lambda |
| rgma | `Rgma` | A | period, passes |
| rma | `Rma` | A | period |
| t3 | `T3` | A | period, vfactor |
| tema | `Tema` | A (+ alpha) | period |
| trama | `Trama` | A | period |
| vama | `Vama` | A | period |
| vidya | `Vidya` | A | period |
| yzvama | `Yzvama` | A | period |
| zldema | `Zldema` | A (+ alpha) | period |
| zlema | `Zlema` | A (+ alpha) | period |
| zltema | `Zltema` | A (+ alpha) | period |

### 8.6 Channels (~20 indicators)

All channel indicators output 3 spans: upper, middle, lower (Pattern I).

| Indicator | C# Class | Input pattern | Key params |
|-----------|----------|---------------|------------|
| abber | `Abber` | A | period |
| accbands | `AccBands` | B (HLC) | period |
| apchannel | `Apchannel` | D (HL) | period |
| apz | `Apz` | B (HLC) | period |
| atrbands | `AtrBands` | B (HLC) | period, multiplier |
| bbands | `Bbands` | A | period, multiplier |
| dchannel | `Dchannel` | D (HL) | period |
| decaychannel | `Decaychannel` | B (HLC) | period |
| fcb | `Fcb` | D (HL) | period |
| jbands | `Jbands` | A | period, phase, power |
| kchannel | `Kchannel` | B (HLC) | period, multiplier |
| maenv | `Maenv` | A | period, pct |
| mmchannel | `Mmchannel` | D (HL) | period |
| pchannel | `Pchannel` | D (HL) | period |
| regchannel | `Regchannel` | A | period |
| sdchannel | `Sdchannel` | A | period |
| starchannel | `Starchannel` | B (HLC) | period |
| stbands | `Stbands` | B (HLC) | period |
| ttm_lrc | `TtmLrc` | A | period |
| ubands | `Ubands` | A | period |
| uchannel | `Uchannel` | D (HL) | period |
| vwapbands | `Vwapbands` | A + vol | period |
| vwapsd | `Vwapsd` | A + vol | period |

### 8.7 Volatility (~20 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| bbw | `Bbw` | A | period, multiplier |
| bbwn | `Bbwn` | A | period, multiplier, lookback |
| bbwp | `Bbwp` | A | period, multiplier, lookback |
| ccv | `Ccv` | A | period, method |
| cv | `Cv` | A | period, alpha, beta |
| cvi | `Cvi` | A | rocLength, smoothLength |
| etherm | `Etherm` | E (HLC) | period |
| ewma | `Ewma` | A | period, annualize, annualPeriods |
| gkv | `Gkv` | C (OHLC) | period, annualPeriods |
| hlv | `Hlv` | D (HL) | period, annualPeriods |
| hv | `Hv` | A | period, annualPeriods |
| jvolty | `Jvolty` | A | period, phase, power |
| jvoltyn | `Jvoltyn` | A | period, phase, power |
| massi | `Massi` | A | emaLength, sumLength |
| rsv | `Rsv` | C (OHLC) | period, annualPeriods |
| rv | `Rv` | A | period, annualPeriods |
| rvi | `Rvi` | A | period |
| tr | `Tr` | E (HLC) | — |
| ui | `Ui` | A | period |
| vov | `Vov` | A | period, vovPeriod |
| vr | `Vr` | E (HLC) | period |
| yzv | `Yzv` | C (OHLC) | period, annualPeriods |

### 8.8 Volume (~30 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| adl | `Adl` | B (HLCV) | — |
| adosc | `Adosc` | B (HLCV) | fastPeriod, slowPeriod |
| aobv | `Aobv` | I (CV→2) | — |
| cmf | `Cmf` | B (HLCV) | period |
| efi | `Efi` | G (CV) | period |
| eom | `Eom` | B (HLV) | period, volumeScale |
| evwma | `Evwma` | G (SV) | period |
| iii | `Iii` | B (HLCV) | period |
| kvo | `Kvo` | B (HLCV) | fastPeriod, slowPeriod |
| mfi | `Mfi` | B (HLCV) | period |
| nvi | `Nvi` | G (CV) | startValue |
| obv | `Obv` | G (CV) | — |
| pvd | `Pvd` | G (CV) | pricePeriod, volumePeriod, smoothingPeriod |
| pvi | `Pvi` | G (CV) | startValue |
| pvo | `Pvo` | I (V→3) | fastPeriod, slowPeriod, signalPeriod |
| pvr | `Pvr` | G (PV) | — |
| pvt | `Pvt` | G (CV) | — |
| tvi | `Tvi` | G (PV) | minTick |
| twap | `Twap` | A | period |
| va | `Va` | B (HLCV) | — |
| vf | `Vf` | G (CV) | period |
| vo | `Vo` | A (volume) | shortPeriod, longPeriod |
| vroc | `Vroc` | A (volume) | period, usePercent |
| vwad | `Vwad` | B (HLCV) | period |
| vwap | `Vwap` | B (HLCV) | period |
| vwma | `Vwma` | G (SV) | period |
| wad | `Wad` | B (HLCV) | — |

### 8.9 Statistics (~30 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| acf | `Acf` | A | period, lag |
| cma | `Cma` | A | — |
| correlation | `Correlation` | H | period |
| covariance | `Covariance` | H | period, isPopulation |
| entropy | `Entropy` | A | period |
| geomean | `Geomean` | A | period |
| granger | `Granger` | H | period, maxlag |
| harmean | `Harmean` | A | period |
| hurst | `Hurst` | A | period |
| iqr | `Iqr` | A | period |
| jb | `Jb` | A | period |
| kendall | `Kendall` | H | period |
| kurtosis | `Kurtosis` | A | period, isPopulation |
| linreg | `LinReg` | A | period, offset |
| meandev | `MeanDev` | A | period |
| median | `Median` | A | period |
| mode | `Mode` | A | period |
| pacf | `Pacf` | A | period, lag |
| percentile | `Percentile` | A | period, percent |
| polyfit | `Polyfit` | A | period, degree |
| quantile | `Quantile` | A | period, quantileLevel |
| skew | `Skew` | A | period, isPopulation |
| spearman | `Spearman` | H | period |
| stddev | `StdDev` | A | period, isPopulation |
| stderr | `Stderr` | A | period |
| sum | `Sum` | A | period |
| theil | `Theil` | A | period |
| trim | `Trim` | A | period, trimPct |
| variance | `Variance` | A | period, isPopulation |
| wavg | `Wavg` | A | period |
| wins | `Wins` | A | period, winPct |
| zscore | `Zscore` | A | period |
| ztest | `Ztest` | A | period, mu0 |
| cointegration | `Cointegration` | H | — |

### 8.10 Errors (~25 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| huber | `Huber` | F | period, delta |
| logcosh | `LogCosh` | F | period |
| mae | `Mae` | F | period |
| maape | `Maape` | F | period |
| mape | `Mape` | F | period |
| mapd | `Mapd` | F | period |
| mase | `Mase` | F | period |
| mdae | `Mdae` | F | period |
| mdape | `Mdape` | F | period |
| me | `Me` | F | period |
| mpe | `Mpe` | F | period |
| mrae | `Mrae` | F | period |
| mse | `Mse` | F | period |
| msle | `Msle` | F | period |
| pseudohuber | `PseudoHuber` | F | period, delta |
| quantileloss | `QuantileLoss` | F | period, quantile |
| rae | `Rae` | F | period |
| rmse | `Rmse` | F | period |
| rmsle | `Rmsle` | F | period |
| rse | `Rse` | F | period |
| rsquared | `Rsquared` | F | period |
| smape | `Smape` | F | period |
| theilu | `TheilU` | F | period |
| tukeybiweight | `TukeyBiweight` | F | period, c |
| wmape | `Wmape` | F | period |
| wrmse | `Wrmse` | F | period (+ weighted variant) |

### 8.11 Filters (~30 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| agc | `Agc` | A | decay |
| alaguerre | `ALaguerre` | A | length, medianLength |
| baxterking | `BaxterKing` | A | pLow, pHigh, k |
| bessel | `Bessel` | A | length |
| bilateral | `Bilateral` | A | period, sigmaSRatio, sigmaRMult |
| bpf | `Bpf` | A | lowerPeriod, upperPeriod |
| butter2 | `Butter2` | A | period |
| butter3 | `Butter3` | A | period |
| cfitz | `Cfitz` | A | pLow, pHigh |
| cheby1 | `Cheby1` | A | period, ripple |
| cheby2 | `Cheby2` | A | period, attenuation |
| edcf | `Edcf` | A | length |
| elliptic | `Elliptic` | A | period |
| gauss | `Gauss` | A | sigma |
| hann | `Hann` | A | length |
| hp | `Hp` | A | lambda |
| hpf | `Hpf` | A | length |
| kalman | `Kalman` | A | q, r (or period, gain) |
| laguerre | `Laguerre` | A | gamma |
| lms | `Lms` | A | order, mu |
| loess | `Loess` | A | period |
| modf | `Modf` | A | period, beta, feedback, fbWeight |
| notch | `Notch` | A | period, q |
| nw | `Nw` | A | period, bandwidth |
| oneeuro | `OneEuro` | A | minCutoff, beta, dCutoff |
| rls | `Rls` | A | order, lambda |
| rmed | `Rmed` | A | period |
| roofing | `Roofing` | A | hpLength, ssLength |
| sgf | `Sgf` | A | period, polyOrder |
| spbf | `Spbf` | A | shortPeriod, longPeriod, rmsPeriod |
| ssf2 | `Ssf2` | A | period |
| ssf3 | `Ssf3` | A | period |
| usf | `Usf` | A | period |
| voss | `Voss` | A | period, predict, bandwidth |
| wavelet | `Wavelet` | A | levels, threshMult |
| wiener | `Wiener` | A | period, smoothPeriod |

### 8.12 Cycles (~14 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| ccor | `Ccor` | A | period, threshold |
| ccyc | `Ccyc` | A | alpha |
| cg | `Cg` | A | period |
| dsp | `Dsp` | A | period |
| eacp | `Eacp` | A | minPeriod, maxPeriod, avgLength, enhance |
| ebsw | `Ebsw` | A | hpLength, ssfLength |
| homod | `Homod` | A | minPeriod, maxPeriod |
| ht_dcperiod | `HtDcperiod` | A | — |
| ht_dcphase | `HtDcphase` | A | — |
| ht_phasor | `HtPhasor` | I (→2) | — |
| ht_sine | `HtSine` | I (→2) | — |
| lunar | `Lunar` | Special (long*) | — |
| solar | `Solar` | Special (long*) | — |
| ssfdsp | `Ssfdsp` | A | period |

### 8.13 Dynamics (~10 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| amat | `Amat` | I (→2) | fastPeriod, slowPeriod |
| aroon | `Aroon` | D (HL) | period |
| ghla | `Ghla` | E (HLC) | period |
| ht_trendmode | `HtTrendmode` | A | — |
| pfe | `Pfe` | A | period, smoothPeriod |
| ravi | `Ravi` | A | shortPeriod, longPeriod |
| vhf | `Vhf` | A | period |
| vortex | `Vortex` | I (HLC→2) | period |
| qstick | `Qstick` | C (OC) | period |

### 8.14 Reversals (~10 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| chandelier | `Chandelier` | C (OHLC→2) | period, multiplier |
| ckstop | `Ckstop` | C (OHLC→2) | period, multiplier |
| fractals | `Fractals` | D (HL→2) | period |
| pivot | `Pivot` | D (HL→multi) | — |
| pivotcam | `Pivotcam` | E (HLC→multi) | — |
| pivotdem | `Pivotdem` | C (OHLC→multi) | — |
| pivotext | `Pivotext` | E (HLC→multi) | — |
| pivotfib | `Pivotfib` | E (HLC→multi) | — |
| pivotwood | `Pivotwood` | E (HLC→multi) | — |
| psar | `Psar` | C (OHLC) | accelStart, accelMax |
| swings | `Swings` | D (HL→multi) | period |
| ttm_scalper | `TtmScalper` | D (HL→multi) | period |

### 8.15 Numerics (~15 indicators)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| accel | `Accel` | A | — |
| betadist | `Betadist` | A | alpha, beta params |
| binomdist | `Binomdist` | A | n, p |
| change | `Change` | A | period |
| dwt | `Dwt` | A | levels |
| expdist | `Expdist` | A | lambda |
| exptrans | `Exptrans` | A | — |
| fdist | `Fdist` | A | df1, df2 |
| fft | `Fft` | A | — |
| gammadist | `Gammadist` | A | alpha, beta |
| highest | `Highest` | A | period |
| ifft | `Ifft` | A | — |
| jerk | `Jerk` | A | — |
| lineartrans | `Lineartrans` | A | slope, intercept |
| lognormdist | `Lognormdist` | A | mu, sigma |
| logtrans | `Logtrans` | A | — |
| lowest | `Lowest` | A | period |
| normdist | `Normdist` | A | mu, sigma |
| normalize | `Normalize` | A | period |
| poissondist | `Poissondist` | A | lambda |
| relu | `Relu` | A | — |
| sigmoid | `Sigmoid` | A | k, x0 |
| slope | `Slope` | A | — |
| sqrttrans | `Sqrttrans` | A | — |
| tdist | `Tdist` | A | df |
| weibulldist | `Weibulldist` | A | k, lambda |

### 8.16 Forecasts (1 indicator)

| Indicator | C# Class | Pattern | Key params |
|-----------|----------|---------|------------|
| afirma | `Afirma` | A + enum | period, window(int), leastSquares(bool→int) |

---

## 9) Compatibility strategy with pandas-ta

### 9.1 Compatibility levels

| Level | Description |
|-------|-------------|
| **L1** | Signature-compatible: same parameter names and defaults |
| **L2** | Return-compatible: same return container shape and type |
| **L3** | Behavior-compatible: matching warmup NaN count, naming, core semantics |

### 9.2 pandas-ta overlap indicators

These indicators exist in both pandas-ta and QuanTAlib. All target L2 minimum,
L3 where QuanTAlib's algorithm matches:

**Trend:** SMA, EMA, DEMA, TEMA, HMA, WMA, KAMA, FWMA, T3, TRIMA, ALMA, LSMA,
SINEMA, VIDYA, ZLEMA, FRAMA, HWMA

**Momentum:** RSI, ROC, MOM, CMO, MACD, PPO, TSI, RSX

**Oscillators:** STOCH, STOCHF, STOCHRSI, WILLR, AO, APO, TRIX, FISHER, DPO,
ULTOSC, KDJ, QQE, SQUEEZE, STC, RVGI

**Volatility:** ATR, TR, BBANDS, BBW, MASSI, UI, EWMA (realized vol)

**Volume:** OBV, ADL, ADOSC, CMF, MFI, NVI, PVI, PVT, VWAP, VWMA, EFI, KVO

**Statistics:** ZSCORE, VARIANCE, STDDEV, SKEW, KURTOSIS, MEDIAN, ENTROPY,
LINREG

### 9.3 Known deltas

- pandas-ta uses `talib=True` to delegate to TA-Lib C library; quantalib uses
  its own C# implementations.
- Warmup NaN counts may differ for recursive indicators (EMA, RSI) due to
  different convergence thresholds.
- `offset` parameter: quantalib supports it via `np.roll` in the Python layer.
- `fillna`/`fill_method` kwargs: supported in Python layer, not in native.

---

## 10) Testing protocol

### 10.1 Native ABI tests (C# xUnit in lib project)

- Status-code contract tests for all exports
- Null pointer, zero length, negative period → correct error codes
- Known golden values vs managed `Batch` calls

### 10.2 Python tests (pytest)

| Suite | Description |
|-------|-------------|
| `test_smoke.py` | Import, load native lib, call SMA, check output is ndarray |
| `test_shapes.py` | `len(output) == len(input)` for all single-output indicators |
| `test_status_codes.py` | Null ptr, bad length, bad params raise correct exceptions |
| `test_golden.py` | Compare outputs vs known golden values from managed QuanTAlib |
| `test_compat.py` | pandas-ta parity for overlap indicators (Series/DataFrame) |

### 10.3 Performance tests

Track per-indicator:

- Python wrapper overhead (µs per call for 10k bars)
- Throughput (rows/sec)
- Compare vs pandas-ta for overlap indicators

Acceptance (initial baseline):

- Median wrapper overhead does not exceed pandas-ta by more than 20% on overlap indicators where algorithms are directly comparable.
- No unbounded memory growth across repeated runs.
- Performance report is produced per release candidate and checked into CI artifacts.

---

## 11) CI and release

### 11.1 Build matrix

| RID | Runner | Artifact |
|-----|--------|----------|
| `win-x64` | `windows-latest` | `quantalib.dll` |
| `linux-x64` | `ubuntu-latest` | `quantalib.so` |
| `osx-arm64` | `macos-14` | `quantalib.dylib` |
| `osx-x64` | `macos-13` | `quantalib.dylib` |

### 11.2 Pipeline stages

1. `dotnet publish -r <rid> -c Release` per RID
2. Stage artifacts into `quantalib/native/<platform>/`
3. `python -m pytest tests/` per platform
4. Build wheel with `python -m build`
5. Upload to PyPI (manual trigger for releases)

### 11.3 Release checklist

- [ ] All RID artifacts present and loadable
- [ ] All pytest suites pass on all platforms
- [ ] Performance benchmark logged (no regressions)
- [ ] `CHANGELOG.md` updated
- [ ] Version bumped in `pyproject.toml`

---

## 12) Risk register

| # | Risk | Impact | Mitigation |
|---|------|--------|------------|
| 1 | ABI breakage on update | High | Versioned symbols (`qtl_*`) + ABI contract tests |
| 2 | NativeAOT trims used code | High | Explicit calls in `Exports.cs` guarantee reachability |
| 3 | Platform loader failures | Medium | Explicit package-path loading + startup diagnostics |
| 4 | Behavior drift vs pandas-ta | Medium | Compatibility suite + documented deltas |
| 5 | Enum/array params over ABI | Medium | int/ptr mapping with documented contracts |
| 6 | Large export surface (~290) | Medium | Code generation considered for `Exports.cs` if manual is too error-prone |
| 7 | Cross-compilation failures | Medium | CI matrix catches per-platform issues early |
| 8 | Thread safety | Low | `Batch` methods are stateless; no shared mutable state |

---

## 13) Review checklist

- [ ] Naming accepted: PyPI/import/binary all `quantalib`
- [ ] Versioned ABI prefix accepted (`qtl_*`)
- [ ] Flat structure accepted (`Exports.cs`, `indicators.py`)
- [ ] Status-code model accepted (0–4)
- [ ] 9 ABI signature patterns accepted
- [ ] Special parameter mappings accepted (enum→int, bool→int, array→ptr+len)
- [ ] Full indicator catalog reviewed
- [ ] Compatibility levels accepted (L1/L2/L3)
- [ ] RID matrix accepted (4 platforms)
- [ ] NaN/warmup policy accepted
- [ ] Thread safety model accepted
- [ ] CI pipeline accepted

---

## 14) Implementation order

1. **Skeleton:** `python.csproj`, `Directory.Build.props`, package layout,
   `_loader.py`, `_bridge.py` scaffolding, `pyproject.toml`
2. **ABI layer:** `StatusCodes.cs`, `ArrayBridge.cs`, `Exports.cs` (all ~290)
3. **Python layer:** `indicators.py` (all wrappers), `_compat.py` (aliases)
4. **Tests:** `test_smoke.py`, `test_shapes.py`, `test_status_codes.py`,
   `test_golden.py`, `test_compat.py`
5. **Publish:** `publish.ps1`, CI workflow, wheel packaging
6. **Docs:** `README.md` with full indicator list and compatibility table

---

## 15) Open decisions

1. **Error mapping granularity:** `ValueError` for bad params vs separate
   exception classes per status code (current spec uses separate classes).
2. **Alpha overloads:** Export as `qtl_ema_alpha` or defer to v2?
3. **Code generation:** Generate `Exports.cs` + `_bridge.py` + `indicators.py`
   from a manifest file, or write manually?
4. **Linux wheel tag:** `manylinux2014_x86_64` vs `manylinux_2_17_x86_64`?
5. **Version sync:** Should `quantalib` Python version track QuanTAlib NuGet
   version, or version independently?

## 16) Review findings and prioritized refinements

### High priority

1. **Finalize ABI evolution policy**
   - Keep `qtl_` immutable for v1 and reserve `qtl2_` for first break.
2. **Adopt manifest-driven generation**
   - Eliminate manual drift risk across native exports, ctypes bridge, and wrappers.
3. **Clarify output validity on internal failures**
   - Treat outputs as invalid on `QTL_ERR_INTERNAL`.

### Medium priority

4. **Lock pandas fallback behavior**
   - NumPy-only mode should be deterministic and explicitly tested.
5. **Define parity subset for L3**
   - Freeze exact Wave 1 L3 indicator list before coding starts.
6. **Set measurable perf gates**
   - Keep CI benchmarks with comparable overlap indicators and stable datasets.

### Low priority

7. **Catalog automation**
   - Generate indicator catalog tables from manifest to avoid stale docs.
8. **Release metadata policy**
   - Define whether Python package version mirrors or decouples from NuGet version.
