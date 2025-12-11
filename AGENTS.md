# AGENTS.md - QuanTAlib Protocol

> **To all AI Agents:** This file defines the laws, physics, and protocols of the QuanTAlib repository. Read this before writing a single line of code. Failure to adhere to these standards will result in rejected code.

## 1. Identity & Mission

**QuanTAlib** is a high-performance, zero-allocation C# library for quantitative technical analysis.

* **Target**: Quantower and custom C# trading engines.
* **Core Philosophy**: Speed, Correctness, and Memory Efficiency.
* **Key Constraint**: Hot paths must be allocation-free (GC pressure is the enemy).

## 2. Architecture & "Physics"

### Memory Model: Structure of Arrays (SoA)

We do not store objects in lists. We store primitive arrays.

* **TSeries**: Internally uses `List<long> _t` (timestamps) and `List<double> _v` (values).
* **Access**: Expose data via `ReadOnlySpan<double>` for SIMD operations.

### Core Types

* `TValue`: Struct (16 bytes). `DateTime Time`, `double Value`.
* `TBar`: Struct (48 bytes). `DateTime Time`, `double Open, High, Low, Close, Volume`.
* `TSeries`: The primary data structure for time series.
* `ITValuePublisher`: The interface for reactive data flow.

### Performance Rules

1. **Zero Allocation**: The `Update` method MUST NOT allocate memory on the heap. Use `stackalloc` or pre-allocated buffers.
2. **O(1) Complexity**: Streaming updates must be constant time. Use circular buffers (`RingBuffer`) or running sums.
3. **SIMD**: Batch operations (`Calculate`) should use `System.Runtime.Intrinsics` (AVX2) where possible.
4. **Inlining**: Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot methods.
5. **Locals**: Use `[SkipLocalsInit]` to avoid zero-init costs in tight loops.

## 3. Indicator Implementation Standards

Every indicator must follow the **Good Indicator Guidelines** strictly.

### File Structure

Directory: `lib/[category]/[name]/` (e.g., `lib/trends/sma/`)

| File | Naming | Purpose |
|------|--------|---------|
| **Source** | `[Name].cs` | Main logic. `public sealed class`. |
| **Tests** | `[Name].Tests.cs` | xUnit tests (correctness, edge cases). |
| **Validation** | `[Name].Validation.Tests.cs` | Compare against TA-Lib, Skender, etc. |
| **Docs** | `[Name].md` | User documentation with formulas. |
| **Adapter** | `[Name].Quantower.cs` | Quantower platform integration. |
| **Adapter Tests** | `[Name].Quantower.Tests.cs` | Tests for the adapter. |

### The `Update` Method Contract

The `Update` method is the heart of the indicator.

```csharp
public TValue Update(TValue input, bool isNew = true)
```

* **`isNew = true`**: A new bar has arrived. Save current state to history (or `_p_` variables), then calculate.
* **`isNew = false`**: The current bar is updating (tick data). Restore state from history (or `_p_` variables), then recalculate.
* **NaN Handling**: If input is `NaN` or `Infinity`, use the last valid value. Never propagate `NaN`.

### State Management

* Use `RingBuffer` for sliding windows.
* Maintain `_state` and `_p_state` (previous state) variables to support `isNew=false` rollbacks.
* **Resync**: Periodically recalculate running sums to prevent floating-point drift.

### Dual API Requirement

1. **Stateful (Streaming)**: `Update(TValue)` for live data.
2. **Stateless (Vector)**: `static void Calculate(ReadOnlySpan<double> src, Span<double> dst)` for batch history.

## 4. Testing Protocol

### Unit Tests (`[Name].Tests.cs`)

* Use `GBM` (Geometric Brownian Motion) for data generation.
* Test `isNew=true` vs `isNew=false` consistency.
* Test `Reset()` and `IsHot` (warmup).
* Test edge cases: `NaN` inputs, empty series, period=1.

### Validation Tests (`[Name].Validation.Tests.cs`)

* **Mandatory**: You MUST validate against at least one external authority (TA-Lib, Skender, Tulip, Python libs).
* **Tolerance**: Typically `1e-6` to `1e-9`.

## 5. Documentation Standards

* **Format**: Markdown.
* **Content**: Title, Description, Parameters, Formula (LaTeX), C# Usage Examples.
* **Index**: Add the new indicator to the category index (e.g., `lib/trends/_index.md`).

## 6. Development Checklist

When creating a new indicator, you are **DONE** only when:

* [ ] Source algorithm is verified.
* [ ] All 6 required files exist.
* [ ] `Update` handles `isNew` and `NaN` correctly.
* [ ] No heap allocations in `Update`.
* [ ] Static `Calculate(Span)` is implemented.
* [ ] Unit tests pass (including edge cases).
* [ ] Validation tests pass against external libs.
* [ ] Documentation is complete and linked in `_index.md`.
* [ ] CodeRabbit review issues are resolved.

## 7. Forbidden Actions

* **DO NOT** use LINQ in hot paths (`Update` or `Calculate`).
* **DO NOT** use `new` inside `Update`.
* **DO NOT** change `Directory.Build.props` without explicit instruction.
* **DO NOT** remove `[SkipLocalsInit]` or `[MethodImpl]` attributes.
* **DO NOT** ignore `NaN` inputs; handle them safely.

## 8. Context & Resources

* **Time**: Use `DateTime.UtcNow`.
* **Math**: Use `System.Math` or `System.Numerics`.
* **Root Namespace**: `QuanTAlib`.
