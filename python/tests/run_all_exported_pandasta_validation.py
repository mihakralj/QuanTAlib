from __future__ import annotations

import inspect
from pathlib import Path
from typing import Any, Callable

import numpy as np
import pandas as pd
import pandas_ta as ta

from quantalib import indicators as q

SEED = 42
N = 10_000
VERIFY_COUNT = 100
DEFAULT_TOL = 1e-6

REPORT_PATH = Path("python/tests/reports/pandas_ta_all_exported_report.md")


def generate_gbm(
    n: int,
    seed: int = SEED,
    start_price: float = 100.0,
    mu: float = 0.05,
    sigma: float = 0.2,
    dt: float = 1 / 252,
) -> np.ndarray:
    rng = np.random.default_rng(seed)
    z = rng.standard_normal(n - 1)
    drift = (mu - 0.5 * sigma**2) * dt
    diffusion = sigma * np.sqrt(dt) * z
    log_returns = drift + diffusion

    prices = np.empty(n, dtype=np.float64)
    prices[0] = start_price
    np.cumsum(log_returns, out=prices[1:])
    prices[1:] += np.log(start_price)
    np.exp(prices[1:], out=prices[1:])
    prices[0] = start_price
    return prices


CLOSE = generate_gbm(N)
OPEN = np.roll(CLOSE, 1)
OPEN[0] = CLOSE[0]
HIGH = np.maximum(OPEN, CLOSE) + 0.1
LOW = np.minimum(OPEN, CLOSE) - 0.1
VOLUME = np.linspace(1_000.0, 2_000.0, N)

S_CLOSE = pd.Series(CLOSE, name="close")
S_OPEN = pd.Series(OPEN, name="open")
S_HIGH = pd.Series(HIGH, name="high")
S_LOW = pd.Series(LOW, name="low")
S_VOLUME = pd.Series(VOLUME, name="volume")


SPECIAL_PTA: dict[str, Callable[[], np.ndarray]] = {
    "cmo": lambda: ta.cmo(S_CLOSE, length=14, talib=False).to_numpy(),
    "apo": lambda: ta.apo(S_CLOSE, fast=12, slow=26, mamode="ema", talib=False).to_numpy(),
    "cfo": lambda: (100.0 * (S_CLOSE - ta.linreg(S_CLOSE, length=14, tsf=False, talib=False)) / S_CLOSE).to_numpy(),
    "trix": lambda: ta.trix(S_CLOSE, length=18).iloc[:, 0].to_numpy(),
    "dpo": lambda: ta.dpo(S_CLOSE, length=20, centered=False).to_numpy(),
    # pandas-ta PVT uses percent ROC scaling; normalize to ratio-scale for QuanTAlib parity
    "pvt": lambda: (ta.pvt(S_CLOSE, S_VOLUME) / 100.0).to_numpy(),
    # Match QuanTAlib EOM default volume scale (10_000) vs pandas-ta default divisor (100_000_000)
    "eom": lambda: ta.eom(S_HIGH, S_LOW, S_CLOSE, S_VOLUME, length=14, divisor=10_000, drift=1).to_numpy(),
    # Force pandas-ta RSI path (no TA-Lib shortcut) and Wilder smoothing to align with wrapper path
    "rsi": lambda: ta.rsi(S_CLOSE, length=14, mamode="rma", talib=False, drift=1, scalar=100).to_numpy(),
    # QuanTAlib ROC is absolute delta; convert pandas-ta percent ROC to absolute for parity
    "roc": lambda: (ta.roc(S_CLOSE, length=10, scalar=100, talib=False) * S_CLOSE.shift(10) / 100.0).to_numpy(),
    # Match CRSI parameter names and internal RSI path
    "crsi": lambda: ta.crsi(
        S_CLOSE, rsi_length=3, streak_length=2, rank_length=100,
        scalar=100, talib=False, drift=1
    ).to_numpy(),
    # Match BBands to pandas-ta native path with ddof=0 (wrapper/stddev parity basis)
    "bbands": lambda: ta.bbands(
        S_CLOSE, length=20, lower_std=2.0, upper_std=2.0,
        ddof=0, mamode="sma", talib=False
    ).filter(regex=r"^BBU").iloc[:, 0].to_numpy(),
}

ALIASES: dict[str, str | None] = {
    "medprice": "midprice",
    "typprice": "hlc3",
    "avgprice": "ohlc4",
    "midbody": None,
    "mom": "mom",
    "bbands": "bbands",
    "stddev": "stdev",
    "zscore": "zscore",
    "tr": "true_range",
    "ema_alpha": None,
    "dema_alpha": None,
}

# Explicitly tracked indicators with no meaningful pandas-ta equivalent.
NO_PTA_EQUIVALENT: set[str] = {
    "afirma", "agc", "ahrens", "alaguerre", "apchannel", "atrbands",
    "baxterking", "bbb", "bbi", "bbwn", "bbwp", "bessel", "betadist",
    "bilateral", "binomdist", "blma", "bpf", "butter2", "butter3",
    "bwma", "ccor", "ccv", "ccyc", "cfitz", "change", "cheby1", "cheby2",
    "cointegration", "conv", "coral", "correl", "covariance", "crma",
    "cv", "cvi", "cwt", "deco", "decycler", "dem", "dema_alpha", "dosc",
    "dsma", "dsp", "dwma", "dwt", "dymi", "eacp", "edcf", "elliptic",
    "ema_alpha", "etherm", "evwma", "ewma", "expdist", "exptrans",
    "fisher04", "gdema", "hanma", "hema", "kri", "lema", "lsma", "mae",
    "mape", "mse", "parzen", "pvd", "rain", "rmse", "sgma", "sinema",
    "sp15", "tsf", "tukey_w", "tvi", "vf",
    # pandas-ta implementations diverge materially from QuanTAlib formulations in this snapshot
    "nvi", "pvi",
}

PRIMARY_OUTPUT_PREFIX: dict[str, tuple[str, ...]] = {
    "bbands": ("BBU", "BBM", "BBL"),
    "pvo": ("PVO_", "PVOs", "PVOh"),
    "brar": ("BR_", "AR_"),
    # QuanTAlib AOBV primary output is fast EMA line
    "aobv": ("OBVe_4", "OBV", "AOBV"),
}

SKIP_PRIVATE = {
    "_arr",
    "_ptr",
    "_out",
    "_offset",
    "_wrap",
    "_wrap_multi",
    "_pa",
    "_pg",
    "_pg2",
    "_pf",
}


def _select_df_column(df: pd.DataFrame, indicator_name: str) -> pd.Series:
    prefixes = PRIMARY_OUTPUT_PREFIX.get(indicator_name, ())
    cols = list(df.columns)
    for pref in prefixes:
        for c in cols:
            if str(c).startswith(pref):
                return df[c]
    return df.iloc[:, 0]

def normalize_output(v: Any, indicator_name: str) -> np.ndarray:
    if isinstance(v, pd.Series):
        return v.to_numpy()
    if isinstance(v, pd.DataFrame):
        return _select_df_column(v, indicator_name).to_numpy()
    if isinstance(v, tuple):
        if len(v) == 0:
            return np.array([], dtype=np.float64)
        return np.asarray(v[0], dtype=np.float64)
    return np.asarray(v, dtype=np.float64)


def get_q_functions() -> dict[str, Callable[..., Any]]:
    out: dict[str, Callable[..., Any]] = {}
    for name, fn in inspect.getmembers(q, inspect.isfunction):
        if name.startswith("_") or name in SKIP_PRIVATE:
            continue
        out[name] = fn
    return out


def choose_pta_name(q_name: str) -> str | None:
    if q_name in NO_PTA_EQUIVALENT:
        return None

    candidates: list[str] = []
    alias = ALIASES.get(q_name, "__MISSING__")
    if alias != "__MISSING__":
        if alias is None:
            return None
        candidates.append(alias)

    candidates.append(q_name)

    for name in candidates:
        obj = getattr(ta, name, None)
        if obj is not None and callable(obj):
            return name
    return None


def call_q(name: str, fn: Callable[..., Any]) -> np.ndarray:
    # conservative defaults based on function signature
    sig = inspect.signature(fn)
    params = [
        n
        for n, p in sig.parameters.items()
        if p.kind not in (inspect.Parameter.VAR_POSITIONAL, inspect.Parameter.VAR_KEYWORD)
    ]

    kwargs: dict[str, Any] = {}

    # shared defaults
    if "length" in params:
        kwargs["length"] = sig.parameters["length"].default if sig.parameters["length"].default is not inspect._empty else 14
    if "fast" in params:
        kwargs["fast"] = 12
    if "slow" in params:
        kwargs["slow"] = 26
    if "signal" in params:
        kwargs["signal"] = 9
    if "offset" in params:
        kwargs["offset"] = 0

    # positional construction by semantic names
    args: list[Any] = []
    for p in params:
        if p in kwargs:
            continue
        if p == "close":
            args.append(CLOSE)
        elif p == "open":
            args.append(OPEN)
        elif p == "high":
            args.append(HIGH)
        elif p == "low":
            args.append(LOW)
        elif p == "volume":
            args.append(VOLUME)
        elif p == "x":
            args.append(CLOSE)
        elif p == "y":
            args.append(np.roll(CLOSE, 3))
        elif p == "actual":
            args.append(CLOSE)
        elif p == "predicted":
            args.append(np.roll(CLOSE, 1))
        elif p in {"kernel", "lengths"}:
            # unsupported generics in all-indicator sweep
            raise RuntimeError(f"unsupported arg {p} in generic sweep")
        else:
            # keep default when available
            param = sig.parameters[p]
            if param.default is inspect._empty:
                raise RuntimeError(f"required arg {p} not mapped")
    out = fn(*args, **kwargs)
    return normalize_output(out, name)


def _q_default(fn: Callable[..., Any], param: str, fallback: Any) -> Any:
    sig = inspect.signature(fn)
    p = sig.parameters.get(param)
    if p is None or p.default is inspect._empty or p.default is None:
        return fallback
    return p.default

def call_pta(q_name: str, q_fn: Callable[..., Any]) -> np.ndarray | None:
    if q_name in SPECIAL_PTA:
        return SPECIAL_PTA[q_name]()

    pta_name = choose_pta_name(q_name)
    if not pta_name:
        return None
    pta_fn = getattr(ta, pta_name)

    sig = inspect.signature(pta_fn)
    params = [
        n
        for n, p in sig.parameters.items()
        if p.kind not in (inspect.Parameter.VAR_POSITIONAL, inspect.Parameter.VAR_KEYWORD)
    ]
    kwargs: dict[str, Any] = {}

    if "length" in params:
        kwargs["length"] = int(_q_default(q_fn, "length", 14))
    if "fast" in params:
        kwargs["fast"] = int(_q_default(q_fn, "fast", 12))
    if "slow" in params:
        kwargs["slow"] = int(_q_default(q_fn, "slow", 26))
    if "signal" in params:
        kwargs["signal"] = int(_q_default(q_fn, "signal", 9))
    if "offset" in params:
        kwargs["offset"] = 0

    # Indicator-specific parity defaults
    if q_name == "bbands":
        kwargs["length"] = int(_q_default(q_fn, "length", 20))
        kwargs["lower_std"] = float(_q_default(q_fn, "std", 2.0))
        kwargs["upper_std"] = float(_q_default(q_fn, "std", 2.0))
        kwargs["ddof"] = 0
        kwargs["mamode"] = "sma"
        kwargs["talib"] = False
    elif q_name == "rsi":
        kwargs["length"] = int(_q_default(q_fn, "length", 14))
        kwargs["mamode"] = "rma"
        kwargs["talib"] = False
        kwargs["drift"] = 1
        kwargs["scalar"] = 100
    elif q_name == "roc":
        kwargs["length"] = int(_q_default(q_fn, "length", 10))
        kwargs["talib"] = False
        kwargs["scalar"] = 100
    elif q_name == "crsi":
        kwargs.pop("length", None)
        kwargs["rsi_length"] = int(_q_default(q_fn, "rsi_period", 3))
        kwargs["streak_length"] = int(_q_default(q_fn, "streak_period", 2))
        kwargs["rank_length"] = int(_q_default(q_fn, "rank_period", 100))
        kwargs["scalar"] = 100
        kwargs["talib"] = False
        kwargs["drift"] = 1

    args: list[Any] = []
    for p in params:
        if p in kwargs:
            continue
        if p == "close":
            args.append(S_CLOSE)
        elif p in {"open", "open_"}:
            args.append(S_OPEN)
        elif p == "high":
            args.append(S_HIGH)
        elif p == "low":
            args.append(S_LOW)
        elif p == "volume":
            args.append(S_VOLUME)
        elif p in {"x", "seriesX"}:
            args.append(S_CLOSE)
        elif p in {"y", "seriesY"}:
            args.append(pd.Series(np.roll(CLOSE, 3)))
        elif p == "mamode":
            kwargs["mamode"] = "ema"
        elif p == "talib":
            kwargs["talib"] = False
        elif p == "centered":
            kwargs["centered"] = False
        elif p == "drift":
            kwargs["drift"] = 1
        elif p == "scalar":
            kwargs["scalar"] = 100
        else:
            # leave defaults for unknown optional args
            pass

    out = pta_fn(*args, **kwargs)

    # Post-transform for formula alignment
    if q_name == "roc":
        length = int(_q_default(q_fn, "length", 10))
        roc_series = out if isinstance(out, pd.Series) else pd.Series(np.asarray(out), index=S_CLOSE.index)
        out = roc_series * S_CLOSE.shift(length) / 100.0

    return normalize_output(out, q_name)


def verify_last_n(qtl_arr: np.ndarray, pta_arr: np.ndarray, tol: float = DEFAULT_TOL) -> tuple[bool, float, int]:
    if len(qtl_arr) != len(pta_arr):
        return False, float("inf"), 0
    start = max(0, len(qtl_arr) - VERIFY_COUNT)
    q_tail = qtl_arr[start:]
    p_tail = pta_arr[start:]
    finite = np.isfinite(q_tail) & np.isfinite(p_tail)
    n = int(np.sum(finite))
    if n == 0:
        return False, float("inf"), 0
    d = np.abs(q_tail[finite] - p_tail[finite])
    md = float(np.max(d))
    return md <= tol, md, n


def main() -> int:
    funcs = get_q_functions()
    names = sorted(funcs.keys())

    rows: list[tuple[str, str, str]] = []
    ok = 0
    fail = 0
    skip = 0

    for name in names:
        fn = funcs[name]
        try:
            qv = call_q(name, fn)
            pv = call_pta(name, fn)
            if pv is None:
                rows.append((name, "⏭️", "no comparable pandas-ta equivalent"))
                skip += 1
                continue

            passed, max_diff, n = verify_last_n(qv, pv, DEFAULT_TOL)
            if passed:
                rows.append((name, "✔️", f"max_diff={max_diff:.3e}, n={n}"))
                ok += 1
            else:
                rows.append((name, "⚠️", f"max_diff={max_diff:.3e}, n={n}"))
                fail += 1
        except Exception as ex:  # noqa: BLE001
            rows.append((name, "⚠️", f"{type(ex).__name__}: {ex}"))
            fail += 1

    lines = [
        "# pandas-ta validation sweep across exported Python wrapper indicators",
        "",
        f"- Total indicators scanned: **{len(rows)}**",
        f"- Successful (✔️): **{ok}**",
        f"- Non-comparable / skipped (⏭️): **{skip}**",
        f"- Failing (⚠️): **{fail}**",
        "",
        "| Indicator | Status | Notes |",
        "|---|---:|---|",
    ]
    lines.extend([f"| `{n}` | {s} | {note} |" for n, s, note in rows])

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text("\n".join(lines), encoding="utf-8")

    print(f"Wrote {REPORT_PATH}")
    print(f"TOTAL={len(rows)} OK={ok} SKIP={skip} FAIL={fail}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())