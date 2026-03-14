#!/usr/bin/env python3
"""QuanTAlib Python benchmark -- mirrors perf/Benchmark.cs indicators.

Compares:
  - quantalib  (NativeAOT via ctypes FFI)
  - pandas-ta  (pure Python / numpy)
  - pandas     (rolling baseline where applicable)

Indicators benchmarked (matching C# Benchmark.cs):
  SMA, EMA, WMA, HMA, ADOSC, CORRELATION, SKEW

Usage:
    cd python && python tests/benchmark.py
    cd python && python tests/benchmark.py --bars 100000
    cd python && python tests/benchmark.py --bars 500000 --period 220 --iterations 5
"""
from __future__ import annotations

import argparse
import gc
import os
import sys
import time
from dataclasses import dataclass
from pathlib import Path

# Ensure parent directory is on sys.path so `import quantalib` works
# when running as `python tests/benchmark.py` from python/ directory.
_HERE = Path(__file__).resolve().parent
_PKG_ROOT = _HERE.parent
if str(_PKG_ROOT) not in sys.path:
    sys.path.insert(0, str(_PKG_ROOT))

import numpy as np

# ---------------------------------------------------------------------------
#  Optional imports -- benchmark degrades gracefully
# ---------------------------------------------------------------------------
try:
    import pandas as pd
except ImportError:
    pd = None  # type: ignore[assignment]

try:
    import pandas_ta as ta  # type: ignore[import-untyped]
except ImportError:
    ta = None  # type: ignore[assignment]

try:
    import quantalib as qtl
except (ImportError, OSError) as _err:
    qtl = None  # type: ignore[assignment]
    print(f"[bench] quantalib not available: {_err}", file=sys.stderr)


# ===========================================================================
#  Data generation -- Geometric Brownian Motion (matches C# GBM)
# ===========================================================================

def generate_gbm(
    n: int,
    start_price: float = 100.0,
    mu: float = 0.05,
    sigma: float = 0.2,
    seed: int = 42,
) -> dict[str, np.ndarray]:
    """Generate synthetic OHLCV bars via GBM, same params as C# Benchmark."""
    rng = np.random.default_rng(seed)
    dt = 1.0 / (252 * 390)  # ~1 minute bars, 252 days x 390 min/day

    # Log-normal random walk for close prices
    log_returns = (mu - 0.5 * sigma**2) * dt + sigma * np.sqrt(dt) * rng.standard_normal(n)
    close = start_price * np.exp(np.cumsum(log_returns))

    # Synthetic OHLV from close
    spread = sigma * np.sqrt(dt) * close
    high = close + np.abs(rng.standard_normal(n)) * spread
    low = close - np.abs(rng.standard_normal(n)) * spread
    opn = close + rng.standard_normal(n) * spread * 0.5
    volume = np.abs(rng.standard_normal(n) * 1_000_000 + 5_000_000)

    return {
        "open": opn.astype(np.float64),
        "high": high.astype(np.float64),
        "low": low.astype(np.float64),
        "close": close.astype(np.float64),
        "volume": volume.astype(np.float64),
    }


# ===========================================================================
#  Timing helper
# ===========================================================================

@dataclass
class BenchResult:
    name: str
    library: str
    mean_us: float = 0.0       # microseconds
    std_us: float = 0.0
    alloc_note: str = ""


def _bench(fn, iterations: int, warmup: int = 2) -> tuple[float, float]:
    """Run *fn* and return (mean_us, std_us)."""
    for _ in range(warmup):
        fn()

    gc.disable()
    times: list[float] = []
    for _ in range(iterations):
        t0 = time.perf_counter_ns()
        fn()
        t1 = time.perf_counter_ns()
        times.append((t1 - t0) / 1_000.0)  # ns -> us
    gc.enable()

    arr = np.array(times)
    return float(np.mean(arr)), float(np.std(arr))


# ===========================================================================
#  Benchmark definitions
# ===========================================================================

def run_benchmarks(
    bars: int, period: int, iterations: int
) -> list[BenchResult]:
    results: list[BenchResult] = []
    data = generate_gbm(bars)
    close_np = data["close"]
    open_np = data["open"]
    high_np = data["high"]
    low_np = data["low"]
    vol_np = data["volume"]

    # Build pandas objects if pandas available
    close_pd = pd.Series(close_np, name="close") if pd is not None else None
    open_pd = pd.Series(open_np, name="open") if pd is not None else None

    # Build DataFrame for pandas-ta
    df_ta = None
    if pd is not None and ta is not None:
        df_ta = pd.DataFrame({
            "open": open_np,
            "high": high_np,
            "low": low_np,
            "close": close_np,
            "volume": vol_np,
        })

    print(f"\n{'=' * 76}")
    print(f"  QuanTAlib Python Benchmark")
    print(f"  Bars: {bars:,}  |  Period: {period}  |  Iterations: {iterations}")
    print(f"  Python {sys.version.split()[0]}  |  NumPy {np.__version__}", end="")
    if pd is not None:
        print(f"  |  pandas {pd.__version__}", end="")
    if ta is not None:
        print(f"  |  pandas-ta {ta.version}", end="")
    if qtl is not None:
        print(f"  |  quantalib {getattr(qtl, '__version__', '?')}", end="")
    print(f"\n{'=' * 76}\n")

    # -- SMA --
    _category("SMA", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=lambda: qtl.sma(close_np, period=period) if qtl else None,
              pta_fn=lambda: ta.sma(close_pd, length=period) if (ta and close_pd is not None) else None,
              pd_fn=lambda: close_pd.rolling(period).mean() if close_pd is not None else None,
              pd_label="pandas rolling")

    # -- EMA --
    _category("EMA", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=lambda: qtl.ema(close_np, period=period) if qtl else None,
              pta_fn=lambda: ta.ema(close_pd, length=period) if (ta and close_pd is not None) else None,
              pd_fn=lambda: close_pd.ewm(span=period, adjust=False).mean() if close_pd is not None else None,
              pd_label="pandas ewm")

    # -- WMA --
    def _pd_wma():
        if close_pd is None:
            return None
        weights = np.arange(1, period + 1, dtype=np.float64)
        return close_pd.rolling(period).apply(
            lambda x: np.dot(x, weights) / weights.sum(), raw=True
        )

    _category("WMA", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=lambda: qtl.wma(close_np, period=period) if qtl else None,
              pta_fn=lambda: ta.wma(close_pd, length=period) if (ta and close_pd is not None) else None,
              pd_fn=_pd_wma,
              pd_label="pandas rolling+apply")

    # -- HMA --
    _category("HMA", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=lambda: qtl.hma(close_np, period=period) if qtl else None,
              pta_fn=lambda: ta.hma(close_pd, length=period) if (ta and close_pd is not None) else None,
              pd_fn=None,
              pd_label=None)

    # -- ADOSC --
    def _qtl_adosc():
        if qtl is None:
            return None
        return qtl.adosc(high_np, low_np, close_np, vol_np,
                         fastPeriod=3, slowPeriod=10)

    def _pta_adosc():
        if ta is None or df_ta is None:
            return None
        return ta.adosc(df_ta["high"], df_ta["low"], df_ta["close"],
                        df_ta["volume"], fast=3, slow=10)

    _category("ADOSC", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=_qtl_adosc,
              pta_fn=_pta_adosc,
              pd_fn=None,
              pd_label=None)

    # -- CORRELATION --
    def _qtl_corr():
        if qtl is None:
            return None
        return qtl.correlation(close_np, open_np, period=period)

    def _pd_corr():
        if close_pd is None or open_pd is None:
            return None
        return close_pd.rolling(period).corr(open_pd)

    _category("CORRELATION", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=_qtl_corr,
              pta_fn=None,  # pandas-ta has no rolling correlation
              pd_fn=_pd_corr,
              pd_label="pandas rolling.corr")

    # -- SKEW --
    def _pd_skew():
        if close_pd is None:
            return None
        return close_pd.rolling(period).skew()

    _category("SKEW", results, bars, period, iterations,
              close_np, close_pd, df_ta,
              qtl_fn=lambda: qtl.skew(close_np, period=period) if qtl else None,
              pta_fn=lambda: ta.skew(close_pd, length=period) if (ta and close_pd is not None) else None,
              pd_fn=_pd_skew,
              pd_label="pandas rolling.skew")

    return results


def _category(
    name: str,
    results: list[BenchResult],
    bars: int,
    period: int,
    iterations: int,
    close_np,
    close_pd,
    df_ta,
    qtl_fn,
    pta_fn,
    pd_fn,
    pd_label,
):
    """Benchmark a single indicator category across all available libraries."""
    sep = "-" * (60 - len(name))
    print(f"  -- {name} {sep}")

    # quantalib (NativeAOT)
    if qtl is not None and qtl_fn is not None:
        try:
            mean, std = _bench(qtl_fn, iterations)
            r = BenchResult(name, "quantalib (NativeAOT)", mean, std, "0 B (ctypes)")
            results.append(r)
            print(f"    quantalib         : {mean:>12,.1f} us  +/- {std:>8,.1f} us")
        except Exception as e:
            print(f"    quantalib         : FAILED -- {e}")
    else:
        print(f"    quantalib         : not available")

    # pandas-ta
    if ta is not None and pta_fn is not None:
        try:
            mean, std = _bench(pta_fn, iterations)
            r = BenchResult(name, "pandas-ta", mean, std)
            results.append(r)
            print(f"    pandas-ta         : {mean:>12,.1f} us  +/- {std:>8,.1f} us")
        except Exception as e:
            print(f"    pandas-ta         : FAILED -- {e}")
    elif pta_fn is None:
        print(f"    pandas-ta         : N/A (no equivalent)")
    else:
        print(f"    pandas-ta         : not installed")

    # pandas baseline
    if pd is not None and pd_fn is not None:
        try:
            mean, std = _bench(pd_fn, iterations)
            r = BenchResult(name, pd_label or "pandas", mean, std)
            results.append(r)
            lbl = (pd_label or "pandas")
            print(f"    {lbl:<18s}: {mean:>12,.1f} us  +/- {std:>8,.1f} us")
        except Exception as e:
            lbl = (pd_label or "pandas")
            print(f"    {lbl:<18s}: FAILED -- {e}")

    print()


# ===========================================================================
#  Markdown report
# ===========================================================================

def print_markdown(results: list[BenchResult], bars: int, period: int):
    """Print results as Markdown table, grouped by indicator."""
    print(f"\n## Python Benchmark Results")
    print(f"\n**{bars:,} bars, period={period}**\n")

    # Group by indicator name
    from collections import OrderedDict
    groups: dict[str, list[BenchResult]] = OrderedDict()
    for r in results:
        groups.setdefault(r.name, []).append(r)

    print("| Indicator | Library | Mean (us) | StdDev (us) | vs. fastest |")
    print("|-----------|---------|----------:|------------:|------------:|")

    for indicator, group in groups.items():
        fastest = min(g.mean_us for g in group)
        for r in group:
            ratio = r.mean_us / fastest if fastest > 0 else 0
            ratio_str = "**1.00x**" if ratio < 1.01 else f"{ratio:.2f}x"
            print(f"| {r.name:<11s} | {r.library:<25s} | {r.mean_us:>10,.1f} | {r.std_us:>10,.1f} | {ratio_str:>11s} |")

    print()

    # Summary comparison table: quantalib vs pandas-ta
    qtl_map: dict[str, float] = {}
    pta_map: dict[str, float] = {}
    pd_map: dict[str, float] = {}
    for r in results:
        if "quantalib" in r.library:
            qtl_map[r.name] = r.mean_us
        elif "pandas-ta" in r.library:
            pta_map[r.name] = r.mean_us
        elif "pandas" in r.library:
            pd_map[r.name] = r.mean_us

    if qtl_map and pta_map:
        print("### quantalib vs pandas-ta Speedup\n")
        print("| Indicator | quantalib (us) | pandas-ta (us) | Speedup |")
        print("|-----------|---------------:|---------------:|--------:|")
        for name in qtl_map:
            if name in pta_map:
                q = qtl_map[name]
                p = pta_map[name]
                speedup = p / q if q > 0 else float("inf")
                print(f"| {name:<11s} | {q:>13,.1f} | {p:>13,.1f} | {speedup:>6.1f}x |")
        print()

    if qtl_map and pd_map:
        print("### quantalib vs pandas Speedup\n")
        print("| Indicator | quantalib (us) | pandas (us) | Speedup |")
        print("|-----------|---------------:|------------:|--------:|")
        for name in qtl_map:
            if name in pd_map:
                q = qtl_map[name]
                p = pd_map[name]
                speedup = p / q if q > 0 else float("inf")
                print(f"| {name:<11s} | {q:>13,.1f} | {p:>11,.1f} | {speedup:>6.1f}x |")
        print()


# ===========================================================================
#  Entry point
# ===========================================================================

def main():
    parser = argparse.ArgumentParser(description="QuanTAlib Python Benchmark")
    parser.add_argument("--bars", type=int, default=500_000,
                        help="Number of bars (default: 500000)")
    parser.add_argument("--period", type=int, default=220,
                        help="Indicator period (default: 220)")
    parser.add_argument("--iterations", type=int, default=10,
                        help="Timing iterations (default: 10)")
    parser.add_argument("--markdown", action="store_true", default=True,
                        help="Print Markdown report (default: True)")
    args = parser.parse_args()

    results = run_benchmarks(args.bars, args.period, args.iterations)

    if args.markdown and results:
        print_markdown(results, args.bars, args.period)


if __name__ == "__main__":
    main()
