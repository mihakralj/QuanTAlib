# quantalib (Python NativeAOT wrapper)

Skeleton package and NativeAOT project scaffolding for the `quantalib` Python wrapper over QuanTAlib.

## Current status

This is a **skeleton-only** implementation containing:

- NativeAOT project files (`python.csproj`, `Directory.Build.props`)
- Python packaging metadata (`pyproject.toml`)
- Python package layout (`quantalib/`)
- Loader and bridge stubs (`_loader.py`, `_bridge.py`)
- Native artifact placeholders (`quantalib/native/...`)
- Minimal smoke test scaffold (`tests/test_smoke.py`)
- Native export scaffolding (`src/StatusCodes.cs`, `src/ArrayBridge.cs`, `src/Exports.cs`)

## Not included yet

- Full indicator export implementation
- Full ctypes signatures for all exports
- Indicator wrappers in `indicators.py`
- Complete test matrix and compatibility suite

## Local dev

From `python/`:

- Create venv and install deps
- Run tests: `pytest`
- Build wheel: `python -m build`