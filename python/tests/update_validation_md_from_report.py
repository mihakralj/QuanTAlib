from __future__ import annotations

import re
from pathlib import Path

REPORT = Path("python/tests/reports/pandas_ta_all_exported_report.md")
DOC = Path("docs/validation.md")

# docs stem (from markdown link filename) -> sweep key (python wrapper function name)
DOC_TO_SWEEP_ALIAS: dict[str, str] = {
    # core/price-transform naming differences
    "midprice": "medprice",
    "linreg": "lsma",
    "stdev": "stddev",
    "typicalprice": "typprice",
    "averageprice": "avgprice",
    "midbody": "midbody",
    # common TA abbreviations / canonical wrappers
    "true_range": "tr",
    "z_score": "zscore",
    "standarddeviation": "stddev",
    # explicit doc stems commonly used in this repo
    "wclprice": "typprice",
}

def _norm(s: str) -> str:
    return "".join(ch for ch in s.lower() if ch.isalnum())


def _resolve_status_key(stem: str, status_map: dict[str, str]) -> str | None:
    if stem in status_map:
        return stem

    nstem = _norm(stem)

    # 1) explicit alias by raw stem
    alias = DOC_TO_SWEEP_ALIAS.get(stem)
    if alias and alias in status_map:
        return alias

    # 2) explicit alias by normalized stem
    alias = DOC_TO_SWEEP_ALIAS.get(nstem)
    if alias and alias in status_map:
        return alias

    # 3) normalized exact match against sweep keys
    by_norm = {_norm(k): k for k in status_map.keys()}
    if nstem in by_norm:
        return by_norm[nstem]

    return None


def main() -> int:
    report_lines = REPORT.read_text(encoding="utf-8").splitlines()

    status_map: dict[str, str] = {}
    row_rx = re.compile(r"^\| `([^`]+)` \| (✔️|⚠️) \|")
    for line in report_lines:
        m = row_rx.match(line)
        if not m:
            continue
        status_map[m.group(1).lower()] = m.group(2)

    lines = DOC.read_text(encoding="utf-8").splitlines()
    out: list[str] = []
    updated = 0
    unresolved: list[str] = []

    link_rx = re.compile(r"\]\(([^)]+)\)")
    for line in lines:
        if not line.strip().startswith("|"):
            out.append(line)
            continue

        cols = [c.strip() for c in line.strip().split("|")[1:-1]]
        if len(cols) < 2:
            out.append(line)
            continue

        m = link_rx.search(cols[1])
        if not m:
            out.append(line)
            continue

        stem = Path(m.group(1)).stem.lower()
        if cols[-1] == "❔":
            resolved = _resolve_status_key(stem, status_map)
            if resolved is not None:
                cols[-1] = status_map[resolved]
                line = "| " + " | ".join(cols) + " |"
                updated += 1
            else:
                unresolved.append(stem)

        out.append(line)

    DOC.write_text("\n".join(out) + "\n", encoding="utf-8")
    unresolved_unique = sorted(set(unresolved))
    print(f"UPDATED={updated}")
    print(f"UNRESOLVED={len(unresolved_unique)}")
    if unresolved_unique:
        print("UNRESOLVED_SAMPLE=" + ",".join(unresolved_unique[:25]))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())