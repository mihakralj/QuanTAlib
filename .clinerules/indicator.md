---
trigger: always_on
---

# QuanTAlib Protocol

> **To all AI Agents:** This file defines the laws, physics, and protocols of the QuanTAlib repository. Read this before writing a single line of code. Failure to adhere to these standards will result in rejected code.

## 1. Identity & Mission

**QuanTAlib** is a high-performance, zero-allocation C# library for quantitative technical analysis.

* **Model**: If pinescript of the indicator already exists in the same directory where indicator will be, this pinescript algorithm is used as the foundation.
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

### Design Principles

* **Source Material:** The algorithm and markdown documentation foundation should be sourced from [https://github.com/mihakralj/pinescript/blob/main/indicators/](PineScript).
* **Zero Allocation:** The core calculation loop must not allocate memory on the heap. Use `stackalloc`, `Span<T>`, and pinned memory where possible.
* **O(1) Complexity:** Streaming updates must be O(1) whenever mathematically possible. Use running sums/products or circular buffers to avoid re-iterating over history.
* **Dual API:** Provide both a stateful object-oriented API (`Update`) and a stateless static vector API (`Calculate`).
* **Bar Correction:** Support intra-bar updates via the `isNew` parameter. The indicator must be able to rollback the last update and apply a new value for the same timestamp.
* **Robustness:** Handle `NaN` and `Infinity` gracefully using last-valid-value substitution. Never propagate invalid values.
* **Reactive:** Implement `ITValuePublisher` to support event-driven architectures.
* **Time Handling:** Always use `DateTime.UtcNow` instead of `DateTime.Now` to ensure consistent time handling across timezones.

### Performance Rules

1. **Zero Allocation**: The `Update` method MUST NOT allocate memory on the heap. Use `stackalloc` or pre-allocated buffers.
2. **O(1) Complexity**: Streaming updates must be constant time. Use circular buffers (`RingBuffer`) or running sums.
3. **SIMD**: Batch operations (`Calculate`) should use `System.Runtime.Intrinsics` (AVX2) where possible. If SIMD is not possible due to recursive dependencies, use `stackalloc` for internal buffers to avoid heap allocations.
4. **Inlining**: Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot methods.
5. **Locals**: Use `[SkipLocalsInit]` to avoid zero-init costs in tight loops.

## 3. Indicator Implementation Standards

Every indicator must follow the **Good Indicator Guidelines** strictly.

### File Structure

Directory: `lib/[category]/[name]/` (e.g., `lib/trends/sma/`)

| File | Naming | Purpose |
|------|--------|---------|
| **Source** | `[Name].cs` | Main implementation. `public sealed class`. |
| **Tests** | `[Name].Tests.cs` | xUnit tests (correctness, edge cases). |
| **Validation** | `[Name].Validation.Tests.cs` | Compare against TA-Lib, Skender, etc. |
| **Docs** | `[Name].md` | User documentation with formulas. |
| **Adapter** | `[Name].Quantower.cs` | Quantower platform integration. |
| **Adapter Tests** | `[Name].Quantower.Tests.cs` | Tests for the adapter. |

### Class Definition

* **Namespace:** `QuanTAlib`
* **Attributes:** `[SkipLocalsInit]` for performance.
* **Modifiers:** `public sealed class`
* **Interface:** Implements `ITValuePublisher`

### State Management

* **Scalar State:** Use a `private record struct State` to group all scalar state variables. This ensures value semantics, automatic `IEquatable` implementation, and cleaner rollback logic.
* **State Variables:** Maintain `private State _state;` (current) and `private State _p_state;` (previous valid state).
* **Buffers:** Use `RingBuffer` for sliding window data.
* **Resync:** Implement a periodic full recalculation (e.g., every 1000 ticks) to prevent floating-point drift in running sums.

### Constructor

* Validate all parameters (throw `ArgumentException` for invalid values).
* Initialize `Name` property (e.g., `$"Sma({period})"`);
* Support chaining: `public [Name](ITValuePublisher source, ...)`

### The `Update` Method Contract

The `Update` method is the heart of the indicator.

```csharp
public TValue Update(TValue input, bool isNew = true)
```

* **Attribute:** `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
* **Logic:**
    1. **State Rollback:**

        ```csharp
        if (isNew) {
            _p_state = _state;
            // ... update state (e.g. counters) ...
        } else {
            _state = _p_state;
            // ... update state ...
        }
        ```

    2. **Input Validation:** Check `double.IsFinite`. If not, use `_lastValidValue` (stored in `State`).
    3. **Calculation:** Perform the math.
    4. **Publish:** Update `Last` property, invoke `Pub` event, return `Last`.

### Update Method (TSeries)

* **Signature:** `public TSeries Update(TSeries source)`
* **Placement:** Must be adjacent to the `Update(TValue)` method.
* **Logic:**
    1. Create output series.
    2. Call static `Calculate(Span)` for performance.
    3. Restore internal state by replaying the last `Period` bars (or full series if recursive).

### Static Calculate (TSeries)

* Create a new instance of the indicator.
* Iterate through the source series.
* Return the resulting `TSeries`.

### Static Calculate (Span) - **Critical for Performance**

* **Signature:** `public static void Calculate(ReadOnlySpan<double> source, Span<double> output, ...)`
* **Attribute:** `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
* **Optimization:**
  * Check for SIMD support (`Avx2.IsSupported`).
  * Use `stackalloc` for small buffers (threshold ~256) and for internal state buffers in recursive algorithms where SIMD is not applicable.
  * Implement a scalar fallback path that handles `NaN` safely.
  * Implement a SIMD path for large, clean datasets (optional but recommended for simple averages).

## 4. Testing Protocol

### Unit Tests (`[Name].Tests.cs`)

* **Framework:** xUnit
* **Data Generation:** Use `GBM` (Geometric Brownian Motion) for generating realistic test data. Avoid using `System.Random` directly.
* **Coverage:**
  * Constructor validation (invalid params).
  * Basic calculation correctness (compare against manual calc).
  * `isNew=true` vs `isNew=false` behavior (bar correction).
  * `Reset()` functionality.
  * `IsHot` property behavior.
  * `NaN` / `Infinity` handling (must not crash, must return finite values).
  * Consistency between Object API, Static TSeries API, and Static Span API.
  * Edge cases: Period=1, empty input, single input.

### Validation Tests (`[Name].Validation.Tests.cs`)

* **Mandatory**: You MUST validate against at least one external authority (TA-Lib, Skender, Tulip, OoplesFinance, Python libs).
* **Tolerance**: Typically `1e-6` to `1e-9`.
* **Data**: Use `ValidationTestData` class which wraps `GBM` (Geometric Brownian Motion) to generate realistic test data and provides pre-calculated Skender quotes.

#### External Library Usage Guide

* **Skender.Stock.Indicators:**
  * Use `_data.SkenderQuotes.Get[Indicator](...)`.
  * Compare using `ValidationHelper.VerifyData`.

* **TA-Lib (TALib.NETCore):**
  * Namespace: `using TALib;`
  * Method: `TALib.Functions.[Indicator]<double>(...)`.
  * Check `Assert.Equal(Core.RetCode.Success, retCode)`.
  * Use `ValidationHelper.VerifyData` with `outRange` and `lookback`.

* **Tulip (Tulip.NETCore):**
  * Namespace: `using Tulip;`
  * Method: `Tulip.Indicators.[indicator].Run(...)`.
  * Handle lookback/offset manually (Tulip output is shorter than input).
  * Use `ValidationHelper.VerifyData` with `lookback`.

* **OoplesFinance.StockIndicators:**
  * Namespace: `using OoplesFinance.StockIndicators;`
  * Convert data: `_data.SkenderQuotes.Select(q => new TickerData { ... }).ToList()`.
  * Use `new StockData(ooplesData).Calculate[Indicator](...)`.
  * Compare using `ValidationHelper.VerifyData`.

## 5. Documentation Standards

* **Format**: Markdown.
* **Content**: Title, Description, Parameters, Formula (LaTeX), C# Usage Examples.
* **Index**: Add the new indicator to the category index (e.g., `lib/trends/_index.md`) AND the main index (`lib/_index.md`).
* **Linting**: Ensure that markdownlint shows no issues for the file.
  * **MD022/MD031:** Headers and fenced code blocks MUST be surrounded by blank lines.
  * **MD030/list-marker-space:** Ensure exactly one space after list markers.
  * **MD032:** Ensure lists are surrounded by blank lines.

## 6. Quantower Adapter

* **Implementation:** Create a wrapper class in `[Name].Quantower.cs` that adapts the QuanTAlib indicator for the Quantower platform.
* **Tests:** Create unit tests in `[Name].Quantower.Tests.cs` to verify the adapter's functionality using mocks where necessary.

## 7. Code Review

* **Tool:** Run CodeRabbit on the changes.
* **Requirement:** Address and fix **ALL** issues identified by the CodeRabbit review before considering the task complete.

## 8. Development Checklist

When creating a new indicator, you are **DONE** only when:

* [ ] Source algorithm is verified.
* [ ] All 6 required files exist.
* [ ] `Update` handles `isNew` and `NaN` correctly.
* [ ] No heap allocations in `Update`.
* [ ] Static `Calculate(Span)` is implemented.
* [ ] Unit tests pass (including edge cases).
* [ ] Validation tests pass against external libs.
* [ ] Documentation is complete and linked in both `_index.md` files.
* [ ] Quantower adapter and tests are implemented.
* [ ] CodeRabbit review issues are resolved.

## 9. Forbidden Actions

* **DO NOT** use LINQ in hot paths (`Update` or `Calculate`).
* **DO NOT** use `new` inside `Update`.
* **DO NOT** change `Directory.Build.props` without explicit instruction.
* **DO NOT** remove `[SkipLocalsInit]` or `[MethodImpl]` attributes.
* **DO NOT** ignore `NaN` inputs; handle them safely.

## 10. Context & Resources

* **Time**: Use `DateTime.UtcNow`.
* **Math**: Use `System.Math` or `System.Numerics`.
* **Root Namespace**: `QuanTAlib`.
