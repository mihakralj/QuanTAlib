#!/usr/bin/env python3
"""Transform new-style functions from `length` → `period` (primary) with `length` as kwargs alias.

Target: all category module .py files in python/quantalib/ that have functions
using bare `length` as a parameter name.

Rules:
  1.  `def foo(close, length: int = X, ...)` → `def foo(close, period: int = X, ...)`
  2.  `length = int(length)` (standalone assignment) → `period = int(kwargs.get("length", period))`
  3.  Any remaining standalone `length` in the function body → `period`
  4.  Compound names like hpLength, ssLength, minLength, etc. are NOT touched.
  5.  `lengths` (plural) is NOT touched.
  6.  `default_length` in _helpers.py pattern helpers is NOT touched (separate ticket).

Also fixes _helpers.py pattern helpers (_pa, _pf, _pg2, _ph) the same way.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

QUANTALIB = Path(__file__).resolve().parent.parent / "quantalib"

# Files to process (category modules + _helpers.py)
TARGET_FILES = [
    "channels.py",
    "core.py",
    "cycles.py",
    "dynamics.py",
    "errors.py",
    "filters.py",
    "momentum.py",
    "numerics.py",
    "oscillators.py",
    "reversals.py",
    "statistics.py",
    "trends_fir.py",
    "trends_iir.py",
    "volatility.py",
    "volume.py",
    "_helpers.py",
]

# Pattern: standalone `length` as a word — NOT preceded or followed by
# alphanumeric or underscore (i.e., not part of hpLength, minLength, etc.)
# Also NOT `lengths` (plural).
STANDALONE_LENGTH = re.compile(r'(?<![a-zA-Z0-9_])length(?![a-zA-Z0-9_])')

# Pattern for the signature line: `length: int = <default>`
SIG_PATTERN = re.compile(r'(?<![a-zA-Z0-9_])length(\s*:\s*int\s*=\s*\d+)')

# Pattern for the assignment line: `length = int(length)` possibly with `;`
ASSIGN_PATTERN = re.compile(
    r'^(\s*)length\s*=\s*int\(length\)\s*;?\s*'
)

# Pattern for assignment in _helpers.py: `length = int(length) if length is not None else default_length`
HELPERS_ASSIGN = re.compile(
    r'^(\s*)length\s*=\s*int\(length\)\s+if\s+length\s+is\s+not\s+None\s+else\s+default_length'
)


def has_standalone_length_param(line: str) -> bool:
    """Check if a def line has standalone `length` as a parameter."""
    if not line.lstrip().startswith("def "):
        return False
    # Must have `length` as standalone word (not part of compound)
    return bool(STANDALONE_LENGTH.search(line))


def transform_file(filepath: Path) -> tuple[int, int]:
    """Transform a single file. Returns (functions_changed, lines_changed)."""
    text = filepath.read_text(encoding="utf-8")
    lines = text.split("\n")
    new_lines: list[str] = []
    in_target_func = False
    func_indent = ""
    funcs_changed = 0
    lines_changed = 0
    assignment_done = False

    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.lstrip()

        # Detect start of a function with `length` parameter
        if stripped.startswith("def ") and has_standalone_length_param(line):
            in_target_func = True
            func_indent = line[: len(line) - len(stripped)]
            assignment_done = False
            funcs_changed += 1

            # Handle multi-line def (continuation lines)
            full_def = line
            while i < len(lines) - 1 and line.rstrip().endswith(","):
                # Replace standalone length in this line
                new_line = STANDALONE_LENGTH.sub("period", line)
                if new_line != line:
                    lines_changed += 1
                new_lines.append(new_line)
                i += 1
                line = lines[i]

            # Last line of def (or single-line def)
            new_line = STANDALONE_LENGTH.sub("period", line)
            if new_line != line:
                lines_changed += 1
            new_lines.append(new_line)
            i += 1
            continue

        # Inside a target function?
        if in_target_func:
            # Detect end of function: non-empty line at or less than func indent,
            # or a new def/class
            if stripped and not line.startswith(func_indent + " ") and not line.startswith(func_indent + "\t"):
                if not stripped.startswith('"""') and not stripped.startswith("'"):
                    # Could be the docstring continuation; check differently
                    if stripped.startswith("def ") or stripped.startswith("class ") or (len(line) - len(stripped) <= len(func_indent) and stripped and not stripped.startswith('#')):
                        in_target_func = False

            if in_target_func:
                # Check for _helpers.py style assignment:
                # `length = int(length) if length is not None else default_length`
                m_helpers = HELPERS_ASSIGN.match(line)
                if m_helpers and not assignment_done:
                    indent = m_helpers.group(1)
                    new_line = f"{indent}period = int(kwargs.get(\"length\", period)) if period is not None else default_length"
                    new_lines.append(new_line)
                    lines_changed += 1
                    assignment_done = True
                    i += 1
                    continue

                # Check for standard assignment: `length = int(length);`
                m_assign = ASSIGN_PATTERN.match(line)
                if m_assign and not assignment_done:
                    indent = m_assign.group(1)
                    # Preserve anything after the semicolon on the same line
                    rest_of_line = ASSIGN_PATTERN.sub("", line)
                    # Check if there's more after (e.g., "; offset = int(offset)")
                    remaining = line[m_assign.end():]
                    new_assignment = f'{indent}period = int(kwargs.get("length", period))'
                    if remaining.strip():
                        new_assignment += "; " + remaining.strip()
                    new_lines.append(new_assignment)
                    lines_changed += 1
                    assignment_done = True
                    i += 1
                    continue

                # Replace any remaining standalone `length` references
                new_line = STANDALONE_LENGTH.sub("period", line)
                if new_line != line:
                    lines_changed += 1
                new_lines.append(new_line)
                i += 1
                continue

        new_lines.append(line)
        i += 1

    new_text = "\n".join(new_lines)
    if new_text != text:
        filepath.write_text(new_text, encoding="utf-8")

    return funcs_changed, lines_changed


def main() -> None:
    total_funcs = 0
    total_lines = 0

    for fname in TARGET_FILES:
        fpath = QUANTALIB / fname
        if not fpath.exists():
            print(f"  SKIP {fname} (not found)")
            continue

        funcs, lines = transform_file(fpath)
        if funcs > 0:
            print(f"  {fname}: {funcs} functions, {lines} lines changed")
            total_funcs += funcs
            total_lines += lines
        else:
            print(f"  {fname}: no changes")

    print(f"\nTotal: {total_funcs} functions, {total_lines} lines changed")


if __name__ == "__main__":
    main()
