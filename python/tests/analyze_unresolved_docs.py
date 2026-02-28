from __future__ import annotations

import inspect
import re
from pathlib import Path

from quantalib import indicators as q

DOC = Path("docs/validation.md")
BRIDGE = Path("python/quantalib/_bridge.py")
EXPORTS = [
    Path("python/src/Exports.cs"),
    Path("python/src/Exports.Generated.cs"),
]
REPORT = Path("python/tests/reports/pandas_ta_all_exported_report.md")

def main() -> int:
    doc_lines = DOC.read_text(encoding="utf-8").splitlines()
    bridge_text = BRIDGE.read_text(encoding="utf-8")
    exports_text = "\n".join(p.read_text(encoding="utf-8", errors="ignore") for p in EXPORTS)
    report_text = REPORT.read_text(encoding="utf-8")

    wrappers = {
        n.lower()
        for n, fn in inspect.getmembers(q, inspect.isfunction)
        if not n.startswith("_")
        and n
        not in {"_arr", "_ptr", "_out", "_offset", "_wrap", "_wrap_multi", "_pa", "_pg", "_pg2", "_pf"}
    }

    link_rx = re.compile(r"\]\(([^)]+)\)")
    unresolved = []
    for line in doc_lines:
        s = line.strip()
        if not s.startswith("|"):
            continue
        cols = [c.strip() for c in s.split("|")[1:-1]]
        if len(cols) < 2 or cols[-1] != "❔":
            continue
        m = link_rx.search(cols[1])
        if not m:
            continue
        stem = Path(m.group(1)).stem.lower()
        unresolved.append(stem)

    unresolved = sorted(set(unresolved))

    rows = []
    for stem in unresolved:
        qtl_name = f"qtl_{stem}"
        has_export = qtl_name in exports_text
        has_bind = qtl_name in bridge_text
        has_wrapper = stem in wrappers
        in_report = f"`{stem}`" in report_text
        rows.append((stem, has_export, has_bind, has_wrapper, in_report))

    no_wrapper = [r for r in rows if not r[3]]
    wrapper_no_report = [r for r in rows if r[3] and not r[4]]

    print(f"UNRESOLVED_TOTAL={len(rows)}")
    print(f"NO_WRAPPER={len(no_wrapper)}")
    print(f"WRAPPER_NOT_IN_REPORT={len(wrapper_no_report)}")
    print("SAMPLE_NO_WRAPPER=" + ",".join(r[0] for r in no_wrapper[:30]))
    print("SAMPLE_WRAPPER_NOT_IN_REPORT=" + ",".join(r[0] for r in wrapper_no_report[:30]))

    return 0

if __name__ == "__main__":
    raise SystemExit(main())