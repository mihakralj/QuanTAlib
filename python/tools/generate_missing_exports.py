#!/usr/bin/env python3
from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

RE_EXPORT_CALL = re.compile(r"\b([A-Za-z_][A-Za-z0-9_]*)\.Batch\(")
RE_CLASS = re.compile(r"\bpublic\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)")
RE_BATCH_HEAD = re.compile(r"\bpublic\s+static\s+([A-Za-z0-9_<>,\.\?\[\]\(\)\s]+?)\s+Batch\s*\(")


@dataclass
class Param:
    type_name: str
    name: str
    has_default: bool


@dataclass
class Overload:
    return_type: str
    params: list[Param]


def split_top_level(s: str) -> list[str]:
    out: list[str] = []
    cur: list[str] = []
    depth_angle = 0
    depth_paren = 0
    for ch in s:
        if ch == "<":
            depth_angle += 1
        elif ch == ">":
            depth_angle = max(0, depth_angle - 1)
        elif ch == "(":
            depth_paren += 1
        elif ch == ")":
            depth_paren = max(0, depth_paren - 1)

        if ch == "," and depth_angle == 0 and depth_paren == 0:
            part = "".join(cur).strip()
            if part:
                out.append(part)
            cur = []
        else:
            cur.append(ch)
    part = "".join(cur).strip()
    if part:
        out.append(part)
    return out


def parse_params(params_text: str) -> list[Param]:
    params: list[Param] = []
    if not params_text.strip():
        return params
    for raw in split_top_level(params_text):
        has_default = "=" in raw
        left = raw.split("=", 1)[0].strip()
        tokens = left.split()
        if len(tokens) < 2:
            continue
        name = tokens[-1].strip()
        type_name = " ".join(tokens[:-1]).replace("in ", "").replace("ref ", "").strip()
        params.append(Param(type_name=type_name, name=name, has_default=has_default))
    return params


def extract_batch_overloads(text: str) -> list[Overload]:
    overloads: list[Overload] = []
    for m in RE_BATCH_HEAD.finditer(text):
        ret = " ".join(m.group(1).split())
        i = m.end()
        depth = 1
        j = i
        while j < len(text) and depth > 0:
            if text[j] == "(":
                depth += 1
            elif text[j] == ")":
                depth -= 1
            j += 1
        if depth != 0:
            continue
        params_text = text[i : j - 1]
        overloads.append(Overload(return_type=ret, params=parse_params(params_text)))
    return overloads


def load_exported_indicators(repo_root: Path) -> set[str]:
    # Baseline only: compare against hand-authored Exports.cs.
    # Generated file is overwritten each run and must not affect diff input.
    exported: set[str] = set()
    p = repo_root / "python" / "src" / "Exports.cs"
    if p.exists():
        txt = p.read_text(encoding="utf-8", errors="ignore")
        exported.update(RE_EXPORT_CALL.findall(txt))
    return exported


def load_lib_indicators(repo_root: Path) -> dict[str, Path]:
    lib = repo_root / "lib"
    indicators: dict[str, Path] = {}
    for p in lib.rglob("*.cs"):
        parts = {x.lower() for x in p.parts}
        if "bin" in parts or "obj" in parts:
            continue
        if p.name.endswith(".Tests.cs"):
            continue
        txt = p.read_text(encoding="utf-8", errors="ignore")
        if "public static" not in txt or "Batch(" not in txt:
            continue
        m = RE_CLASS.search(txt)
        if not m:
            continue
        indicators[m.group(1)] = p
    return indicators


def is_supported_scalar(type_name: str) -> bool:
    if type_name in {"int", "double", "bool"}:
        return True
    if "BatchOutputs" in type_name:
        return False
    if type_name in {"TSeries", "TBarSeries", "Span<double>", "ReadOnlySpan<double>", "Span<long>", "ReadOnlySpan<long>"}:
        return False
    # enum-like names
    return bool(re.fullmatch(r"[A-Za-z_][A-Za-z0-9_\.]*\??", type_name))


def is_tuple_return_of_tseries(ret: str) -> bool:
    if not (ret.startswith("(") and ret.endswith(")")):
        return False
    parts = split_top_level(ret[1:-1])
    if not parts:
        return False
    for p in parts:
        t = p.strip().split()[0]
        if t != "TSeries":
            return False
    return True


def tuple_fields(ret: str) -> list[str]:
    parts = split_top_level(ret[1:-1])
    fields: list[str] = []
    for p in parts:
        toks = p.strip().split()
        if len(toks) >= 2:
            fields.append(toks[1])
    return fields


def choose_overload(overloads: list[Overload]) -> Overload | None:
    def supported(o: Overload) -> bool:
        for p in o.params:
            t = p.type_name
            if t in {"ReadOnlySpan<double>", "Span<double>", "TSeries", "TBarSeries"}:
                continue
            if not is_supported_scalar(t):
                return False
        return True

    span_candidates: list[Overload] = []
    series_candidates: list[Overload] = []
    tuple_candidates: list[Overload] = []

    for o in overloads:
        if not supported(o):
            continue
        has_in_span = any(p.type_name == "ReadOnlySpan<double>" for p in o.params)
        has_out_span = any(p.type_name == "Span<double>" for p in o.params)
        has_series_obj = any(p.type_name in {"TSeries", "TBarSeries"} for p in o.params)

        if o.return_type == "void" and has_in_span and has_out_span:
            span_candidates.append(o)
        elif o.return_type == "TSeries" and has_series_obj:
            series_candidates.append(o)
        elif is_tuple_return_of_tseries(o.return_type) and has_series_obj:
            tuple_candidates.append(o)

    # priority: span overloads (supports multi-output), then single TSeries, then tuple TSeries
    if span_candidates:
        span_candidates.sort(key=lambda o: (sum(1 for p in o.params if p.type_name == "Span<double>"), -len(o.params)), reverse=True)
        return span_candidates[0]
    if series_candidates:
        series_candidates.sort(key=lambda o: -len(o.params))
        return series_candidates[0]
    if tuple_candidates:
        tuple_candidates.sort(key=lambda o: -len(o.params))
        return tuple_candidates[0]
    return None


def map_scalar_to_abi(type_name: str) -> str:
    if type_name == "double":
        return "double"
    # int, bool, enums, nullable enums -> int ABI
    return "int"


def map_scalar_call(type_name: str, arg_name: str) -> str:
    if type_name == "double":
        return arg_name
    if type_name == "int":
        return arg_name
    if type_name == "bool":
        return f"{arg_name} != 0"
    # enum or nullable enum
    return f"({type_name.rstrip('?')}){arg_name}"


def build_wrapper(class_name: str, ov: Overload) -> str:
    entry = f"qtl_{class_name.lower()}"
    method = f"Qtl{class_name}"

    sig_parts: list[str] = []
    null_checks: list[str] = []
    setup_lines: list[str] = []
    call_args: list[str] = []

    has_n = False

    # span-based overload
    if ov.return_type == "void" and any(p.type_name == "ReadOnlySpan<double>" for p in ov.params):
        for p in ov.params:
            t = p.type_name
            if t == "ReadOnlySpan<double>":
                sig_parts.append(f"double* {p.name}")
                null_checks.append(f"{p.name} == null")
                call_args.append(f"Src({p.name}, n)")
                has_n = True
            elif t == "Span<double>":
                sig_parts.append(f"double* {p.name}")
                null_checks.append(f"{p.name} == null")
                call_args.append(f"Dst({p.name}, n)")
                has_n = True
            else:
                abi_t = map_scalar_to_abi(t)
                sig_parts.append(f"{abi_t} {p.name}")
                call_args.append(map_scalar_call(t, p.name))
        if has_n:
            sig_parts.insert(sum(1 for s in sig_parts if s.startswith('double* ')), "int n")

        null_expr = " || ".join(null_checks) if null_checks else "false"
        lines = [
            f'    [UnmanagedCallersOnly(EntryPoint = "{entry}")]',
            f"    public static int {method}({', '.join(sig_parts)})",
            "    {",
            f"        if ({null_expr}) return StatusCodes.QTL_ERR_NULL_PTR;",
            "        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;",
            "        try",
            "        {",
            f"            {class_name}.Batch({', '.join(call_args)});",
            "            return StatusCodes.QTL_OK;",
            "        }",
            "        catch { return StatusCodes.QTL_ERR_INTERNAL; }",
            "    }",
        ]
        return "\n".join(lines)

    # object-based overloads
    for p in ov.params:
        t = p.type_name
        if t == "TSeries":
            sig_parts.append(f"double* {p.name}")
            null_checks.append(f"{p.name} == null")
            has_n = True
            setup_lines.append(f"            var {p.name}Series = BuildSeries({p.name}, n);")
            call_args.append(f"{p.name}Series")
        elif t == "TBarSeries":
            for fld in ("Open", "High", "Low", "Close", "Volume"):
                nm = f"{p.name}{fld}"
                sig_parts.append(f"double* {nm}")
                null_checks.append(f"{nm} == null")
            has_n = True
            setup_lines.append(
                f"            var {p.name}Bars = BuildBars({p.name}Open, {p.name}High, {p.name}Low, {p.name}Close, {p.name}Volume, n);"
            )
            call_args.append(f"{p.name}Bars")
        else:
            abi_t = map_scalar_to_abi(t)
            sig_parts.append(f"{abi_t} {p.name}")
            call_args.append(map_scalar_call(t, p.name))

    if has_n:
        sig_parts.append("int n")

    if ov.return_type == "TSeries":
        sig_parts.append("double* dst")
        null_checks.append("dst == null")
    elif is_tuple_return_of_tseries(ov.return_type):
        for f in tuple_fields(ov.return_type):
            dn = f"dst{f}"
            sig_parts.append(f"double* {dn}")
            null_checks.append(f"{dn} == null")

    null_expr = " || ".join(null_checks) if null_checks else "false"
    lines = [
        f'    [UnmanagedCallersOnly(EntryPoint = "{entry}")]',
        f"    public static int {method}({', '.join(sig_parts)})",
        "    {",
        f"        if ({null_expr}) return StatusCodes.QTL_ERR_NULL_PTR;",
    ]
    if has_n:
        lines.append("        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;")
    lines.extend(["        try", "        {"])
    lines.extend(setup_lines)

    call = f"{class_name}.Batch({', '.join(call_args)})"
    if ov.return_type == "TSeries":
        lines.extend(
            [
                f"            var result = {call};",
                "            var values = result.Values;",
                "            if (values.Length > n) return StatusCodes.QTL_ERR_INVALID_LENGTH;",
                "            var outSpan = Dst(dst, n);",
                "            outSpan.Fill(double.NaN);",
                "            values.CopyTo(outSpan);",
                "            return StatusCodes.QTL_OK;",
            ]
        )
    elif is_tuple_return_of_tseries(ov.return_type):
        fields = tuple_fields(ov.return_type)
        lines.append(f"            var result = {call};")
        for f in fields:
            dn = f"dst{f}"
            lines.extend(
                [
                    f"            var values{f} = result.{f}.Values;",
                    f"            if (values{f}.Length > n) return StatusCodes.QTL_ERR_INVALID_LENGTH;",
                    f"            var outSpan{f} = Dst({dn}, n);",
                    f"            outSpan{f}.Fill(double.NaN);",
                    f"            values{f}.CopyTo(outSpan{f});",
                ]
            )
        lines.append("            return StatusCodes.QTL_OK;")
    else:
        lines.append(f"            {call};")
        lines.append("            return StatusCodes.QTL_OK;")

    lines.extend(["        }", "        catch { return StatusCodes.QTL_ERR_INTERNAL; }", "    }"])
    return "\n".join(lines)


def generate(repo_root: Path) -> str:
    exported = load_exported_indicators(repo_root)
    lib_indicators = load_lib_indicators(repo_root)
    missing = sorted(set(lib_indicators.keys()) - exported)

    wrappers: list[str] = []
    skipped: list[str] = []

    for cls in missing:
        p = lib_indicators[cls]
        text = p.read_text(encoding="utf-8", errors="ignore")
        ovs = extract_batch_overloads(text)
        ov = choose_overload(ovs)
        if ov is None:
            skipped.append(cls)
            continue
        wrappers.append(build_wrapper(cls, ov))

    header = """// <auto-generated />
#nullable enable
using System;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace QuanTAlib.Python;

public static unsafe partial class Exports
{
    private static TSeries BuildSeries(double* src, int n)
    {
        var t = new long[n];
        var v = new double[n];
        new ReadOnlySpan<double>(src, n).CopyTo(v);
        for (int i = 0; i < n; i++) t[i] = i;
        return new TSeries(t, v);
    }

    private static TBarSeries BuildBars(double* open, double* high, double* low, double* close, double* volume, int n)
    {
        var t = new long[n];
        var o = new double[n];
        var h = new double[n];
        var l = new double[n];
        var c = new double[n];
        var v = new double[n];
        new ReadOnlySpan<double>(open, n).CopyTo(o);
        new ReadOnlySpan<double>(high, n).CopyTo(h);
        new ReadOnlySpan<double>(low, n).CopyTo(l);
        new ReadOnlySpan<double>(close, n).CopyTo(c);
        new ReadOnlySpan<double>(volume, n).CopyTo(v);
        for (int i = 0; i < n; i++) t[i] = i;
        var bars = new TBarSeries(n);
        bars.AddRange(t, o, h, l, c, v);
        return bars;
    }

"""
    skipped_block = ""
    if skipped:
        skipped_block = "\n// Skipped (no supported overload found):\n" + "\n".join(f"// - {s}" for s in skipped) + "\n"

    return header + "\n\n".join(wrappers) + skipped_block + "\n}\n"


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    out_file = repo_root / "python" / "src" / "Exports.Generated.cs"
    out_file.write_text(generate(repo_root), encoding="utf-8")
    print(f"Wrote {out_file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())