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
}

ALIASES = {
    "medprice": "midprice",
    "typprice": "hlc3",
    "avgprice": "ohlc4",
    "midbody": "mid_body",
    "mom": "momentum",
    "bbands": "bbands",
    "stddev": "stdev",
    "zscore": "zscore",
    "tr": "true_range",
    "ema_alpha": None,
    "dema_alpha": None,
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


def normalize_pta_output(v: Any) -> np.ndarray:
    if isinstance(v, pd.Series):
        return v.to_numpy()
    if isinstance(v, pd.DataFrame):
        # default: first numeric column
        return v.iloc[:, 0].to_numpy()
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
    if q_name in ALIASES:
        return ALIASES[q_name]
    if hasattr(ta, q_name):
        return q_name
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
    return normalize_pta_output(out)


def call_pta(q_name: str) -> np.ndarray:
    if q_name in SPECIAL_PTA:
        return SPECIAL_PTA[q_name]()

    pta_name = choose_pta_name(q_name)
    if not pta_name:
        raise RuntimeError("no pandas-ta mapping")
    pta_fn = getattr(ta, pta_name)

    sig = inspect.signature(pta_fn)
    params = [
        n
        for n, p in sig.parameters.items()
        if p.kind not in (inspect.Parameter.VAR_POSITIONAL, inspect.Parameter.VAR_KEYWORD)
    ]
    kwargs: dict[str, Any] = {}

    if "length" in params:
        kwargs["length"] = 14
    if "fast" in params:
        kwargs["fast"] = 12
    if "slow" in params:
        kwargs["slow"] = 26
    if "signal" in params:
        kwargs["signal"] = 9
    if "offset" in params:
        kwargs["offset"] = 0

    args: list[Any] = []
    for p in params:
        if p in kwargs:
            continue
        if p == "close":
            args.append(S_CLOSE)
        elif p == "open":
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
    return normalize_pta_output(out)


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

    for name in names:
        fn = funcs[name]
        try:
            qv = call_q(name, fn)
            pv = call_pta(name)
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
        f"- Failing (⚠️): **{fail}**",
        "",
        "| Indicator | Status | Notes |",
        "|---|---:|---|",
    ]
    lines.extend([f"| `{n}` | {s} | {note} |" for n, s, note in rows])

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text("\n".join(lines), encoding="utf-8")

    print(f"Wrote {REPORT_PATH}")
    print(f"TOTAL={len(rows)} OK={ok} FAIL={fail}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())