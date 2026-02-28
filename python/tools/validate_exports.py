#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


EXPORT_BATCH_PATTERN = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\.Batch\(")
PUBLIC_CLASS_PATTERN = re.compile(
    r"\bpublic\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)"
)
PUBLIC_STATIC_BATCH_PATTERN = re.compile(r"\bpublic\s+static\b[\s\S]{0,1200}?\bBatch\s*\(")


def collect_exported_indicators(exports_dir: Path) -> set[str]:
    exported: set[str] = set()
    for p in exports_dir.glob("Exports*.cs"):
        text = p.read_text(encoding="utf-8", errors="ignore")
        exported.update(EXPORT_BATCH_PATTERN.findall(text))
    return exported


def collect_lib_indicators(lib_dir: Path) -> set[str]:
    indicators: set[str] = set()

    for cs_file in lib_dir.rglob("*.cs"):
        parts = {p.lower() for p in cs_file.parts}
        if "bin" in parts or "obj" in parts:
            continue
        if cs_file.name.endswith(".Tests.cs"):
            continue

        text = cs_file.read_text(encoding="utf-8", errors="ignore")
        if "Batch(" not in text or "public static" not in text:
            continue

        class_names = PUBLIC_CLASS_PATTERN.findall(text)
        if not class_names:
            continue

        if not PUBLIC_STATIC_BATCH_PATTERN.search(text):
            continue

        indicators.update(class_names)

    return indicators


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate that python/src/Exports.cs covers all indicator classes in lib/ with public static Batch methods."
    )
    parser.add_argument("--repo-root", type=Path, default=None)
    parser.add_argument("--max-print", type=int, default=200)
    args = parser.parse_args()

    repo_root = args.repo_root or Path(__file__).resolve().parents[2]
    exports_dir = repo_root / "python" / "src"
    lib_dir = repo_root / "lib"

    if not exports_dir.exists():
        print(f"ERROR: missing dir: {exports_dir}")
        return 2
    if not lib_dir.exists():
        print(f"ERROR: missing dir: {lib_dir}")
        return 2

    exported = collect_exported_indicators(exports_dir)
    lib_indicators = collect_lib_indicators(lib_dir)

    missing = sorted(lib_indicators - exported)
    extra = sorted(exported - lib_indicators)

    print(f"EXPORTED_COUNT={len(exported)}")
    print(f"LIB_INDICATOR_COUNT={len(lib_indicators)}")
    print(f"MISSING_COUNT={len(missing)}")
    print(f"EXTRA_COUNT={len(extra)}")

    if missing:
        print("\nMISSING_EXPORTS:")
        for name in missing[: args.max_print]:
            print(name)
        if len(missing) > args.max_print:
            print(f"... ({len(missing) - args.max_print} more)")

    if extra:
        print("\nEXTRA_EXPORT_REFERENCES:")
        for name in extra[: args.max_print]:
            print(name)
        if len(extra) > args.max_print:
            print(f"... ({len(extra) - args.max_print} more)")

    if missing:
        print(
            "\nFAILED: Exports.cs is missing indicators found in /lib. "
            "Add exports or intentionally exclude in validator policy."
        )
        return 1

    print("\nOK: Exports.cs covers all detected /lib indicators.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())