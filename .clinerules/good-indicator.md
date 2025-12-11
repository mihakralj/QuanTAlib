# Good Indicator Guidelines

This document defines the strict standards for creating high-quality technical indicators in the QuanTAlib library. All new indicators MUST adhere to these rules to ensure consistency, performance, and reliability.

## 1. Architecture & Design Principles

* **Source Material:** The algorithm and markdown documentation foundation should be sourced from [https://github.com/mihakralj/pinescript/blob/main/indicators/](https://github.com/mihakralj/pinescript/blob/main/indicators/).
* **Zero Allocation:** The core calculation loop must not allocate memory on the heap. Use `stackalloc`, `Span<T>`, and pinned memory where possible.
* **O(1) Complexity:** Streaming updates must be O(1) whenever mathematically possible. Use running sums/products or circular buffers to avoid re-iterating over history.
* **Dual API:** Provide both a stateful object-oriented API (`Update`) and a stateless static vector API (`Calculate`).
* **Bar Correction:** Support intra-bar updates via the `isNew` parameter. The indicator must be able to rollback the last update and apply a new value for the same timestamp.
* **Robustness:** Handle `NaN` and `Infinity` gracefully using last-valid-value substitution. Never propagate invalid values.
* **Reactive:** Implement `ITValuePublisher` to support event-driven architectures.
* **Time Handling:** Always use `DateTime.UtcNow` instead of `DateTime.Now` to ensure consistent time handling across timezones.

## 2. File Structure

Each indicator resides in its own directory such as `lib/trends/`, `lib/indicators/`, or `lib/oscillators/`.

**Directory:** `lib/[category]/[name]/`

| File | Purpose | Naming Convention |
|------|---------|-------------------|
| **Source** | Main implementation | `[Name].cs` (e.g., `Sma.cs`) |
| **Tests** | Unit tests | `[Name].Tests.cs` |
| **Validation** | Cross-library validation | `[Name].Validation.Tests.cs` |
| **Docs** | User documentation | `[Name].md` |
| **Quantower** | Quantower adapter | `[Name].Quantower.cs` |
| **Quantower Tests** | Quantower adapter tests | `[Name].Quantower.Tests.cs` |

## 3. Implementation Rules (`[Name].cs`)

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

### Update Method

* **Signature:** `public TValue Update(TValue input, bool isNew = true)`
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
* Use `stackalloc` for small buffers (threshold ~256).
* Implement a scalar fallback path that handles `NaN` safely.
* Implement a SIMD path for large, clean datasets (optional but recommended for simple averages).

## 4. Testing Standards

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

* **Purpose:** Verify accuracy against **ALL** available external libraries (Skender, TA-Lib, Tulip, Python libraries, etc.) where the indicator is implemented. You must actively search for existing implementations to validate against.
* **Data:** Use `GBM` (Geometric Brownian Motion) to generate realistic test data.
* **Scenarios:**

* Batch processing.
* Streaming processing.
* Span/Vector processing.

* **Tolerance:** Typically `1e-6` or `1e-9` depending on the algorithm.

## 5. Documentation Standards (`[Name].md`)

Follow the standard template and ensure strict adherence to Markdownlint rules, specifically:

* **MD030:** Ensure exactly one space after list markers (e.g., `* Item`, not `*Item` or `*  Item`).
* **MD032:** Ensure lists are surrounded by blank lines (one blank line before the first item and one after the last item).

Template structure:

1. **Title & Overview:** What is it? What does it do?
2. **Core Concepts:** Key features (e.g., equal weighting, noise reduction).
3. **Parameters:** Table of constructor parameters.
4. **Formula:** LaTeX formatted math ($$...$$).
5. **C# Implementation:** Code examples for:
    * Standard usage.
    * Span API (high performance).
    * Bar correction (`isNew`).
    * Eventing.
6. **Interpretation:** How to use it in trading.
7. **References:** Books or papers.

## 6. Quantower Adapter

* **Implementation:** Create a wrapper class in `[Name].Quantower.cs` that adapts the QuanTAlib indicator for the Quantower platform.
* **Tests:** Create unit tests in `[Name].Quantower.Tests.cs` to verify the adapter's functionality using mocks where necessary.

## 7. Code Review

* **Tool:** Run CodeRabbit on the changes.
* **Requirement:** Address and fix **ALL** issues identified by the CodeRabbit review before considering the task complete.

## 8. Performance Guidelines

* **Inlining:** Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on all hot path methods (`Update`, `Calculate`).
* **Locals Init:** Use `[SkipLocalsInit]` on the class to skip zero-initialization of locals.
* **Loops:** Prefer `for` loops over `foreach` for arrays/spans.
* **Math:** Use `System.Math` or `System.Numerics`. Avoid LINQ in hot paths.
* **Memory:** **NEVER** use `new` inside the `Update` method. Pre-allocate everything in the constructor.

## 9. Checklist for New Indicators

* [ ] **Source Material:** Sourced algorithm and docs from `mihakralj/pinescript` or `mihakralj/quantalib`?
* [ ] **File Structure:** Created all 6 required files?
* [ ] **Constructor:** Validates inputs? Sets `Name`?
* [ ] **Update:** Handles `isNew` correctly? Handles `NaN`? O(1)?
* [ ] **Static API:** Implemented `Calculate(Span)`?
* [ ] **Tests:** Unit tests pass? `NaN` tests included?
* [ ] **Validation:** Matches **ALL** available external libraries?
* [ ] **Docs:** Markdown file created with formula and examples? Linted (MD030, MD032)?
* [ ] **Quantower:** Adapter created in `[Name].Quantower.cs`?
* [ ] **Quantower Tests:** Adapter tests created in `[Name].Quantower.Tests.cs`?
* [ ] **Code Review:** Ran CodeRabbit and fixed all issues?
* [ ] **Index:** Added to category `_index.md` with link and description?
* [ ] **Performance:** No allocations in `Update`? `[SkipLocalsInit]` used?
