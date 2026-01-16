# QuanTAlib Master Protocol (AI Agents)
WARN: repo laws/physics. Read relevant sections bf code. Noncompliance→reject.

DCT{
1:Hot paths allocation-free (no heap alloc); GC pressure enemy;
2:Streaming updates O(1) when math allows;
3:Dual API: stateful Update + stateless static Calculate;
4:Bar correction via isNew rollback (same timestamp rewrite);
5:Robustness: handle NaN/Infinity via last-valid-value substitution; never propagate invalids;
6:SoA: store primitives in concrete List<T> fields + expose spans via CollectionsMarshal.AsSpan;
7:SIMD in Calculate where possible; scalar fallback else;
8:Docs: technical correctness + measurable evidence + skeptical-architect tone; markdownlint strict.
}

QUICKSTART: DO:{Create Indicator}#p1; DO:{Create Adapters for Quantower + other platforms}#p1; DO:{Write comprehensive Tests + validations}#p1; DO:{Write Stylistically + Structurally correct Docs}#p2; DO:{Performance Tuning}#p1.

CRIT_PATTERNS:
- State: use `private record struct State`
- Stack-only: prefer `readonly ref struct` when something should absolutely never leave the stack
- Imports: prefer `using static` for math-heavy/pure helper classes to reduce ceremony and keep expressions readable
- Types: prefer nullable annotations for optional refs; prefer `record`/`record struct` for value-like models/state
- Code clarity: prioritize self-documenting names/structure; use comments for “why”, not “what”
- Encapsulation: prefer `file` types for internal-only helpers
- Closures: prefer `static` local functions to avoid accidental captures
- Discards: use explicit discard (`_ = expr;`) when intentionally ignoring a return value
- Lifetimes: use `scoped ref` parameters for internal APIs to constrain lifetimes
- Events: `source.Pub += Handle;`
- Args: `ArgumentException` + `nameof(param)`
- FMA: `Math.FusedMultiplyAdd(a, b, c)` for `a*b+c`
- Storage: `List<T>` fields for SoA (suppress MA0016 narrowly around those fields)
- Time: always `DateTime.UtcNow` (never `DateTime.Now`)
- Tests: use `GBM` for data; never `System.Random`

1) IDENTITY & MISSION
- QuanTAlib: high-perf, ^1 C# lib for quantitative technical analysis.
- Model: IF PineScript exists in same indicator dir → use as foundation.
- Target: Quantower + custom C# trading engines.
- Philosophy: Speed + Correctness + Memory Efficiency.

2) ARCHITECTURE & PHYSICS
2.1 Memory model (^6)
- No objects-in-lists; primitives-in-arrays.
- TSeries internal: `List<long> _t` (timestamps), `List<double> _v` (values).
- Access: expose `ReadOnlySpan<double>` for SIMD.
- Analyzer note: MA0016 suggests abstractions; suppress only around core List<T> fields by design.

2.2 Core types
- `TValue` struct (16 bytes): `DateTime Time`, `double Value`.
- `TBar` struct (48 bytes): `DateTime Time`, `double Open, High, Low, Close, Volume`.
- `TSeries` primary time-series DS; `ITValuePublisher` reactive flow.

2.3 Design principles
- Source material: PineScript at [U1].
- ^1: use `Span<T>`, `stackalloc`, pinned mem where needed.
- ^2: running sums/products or RingBuffer; avoid history re-iter.
- ^3: Update + Calculate.
- ^4: isNew rollback required.
- ^5: NaN/Infinity safe.
- Reactive: implement `ITValuePublisher`.
- Time handling: `DateTime.UtcNow`.

2.4 Performance rules (hard constraints)
- Update MUST satisfy ^1 and ^2.
- SIMD: Calculate should use `System.Runtime.Intrinsics` (AVX2) or `System.Numerics.Vector<T>`; use `Vector.ConditionalSelect` for branchless edge handling (eg div0). If recursion blocks SIMD → prefer `stackalloc` buffers.
- Hot methods: `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- Tight loops: `[SkipLocalsInit]`.

2.5 FMA patterns (use in hot paths)
- EMA smoothing: `x + alpha * (y - x)` → `Math.FusedMultiplyAdd(x, decay, alpha * y)` where `decay = 1 - alpha`
- Weighted sum: `a*w1 + b*w2` → `Math.FusedMultiplyAdd(a, w1, b * w2)`
- Linear combo: `3.0*a - b` → `Math.FusedMultiplyAdd(3.0, a, -b)`
- Cross product: `(a*b) + (c*d)` → `Math.FusedMultiplyAdd(a, b, c * d)`
- IIR: `coef*input + feedback*state` → `Math.FusedMultiplyAdd(coef, input, feedback * state)`
Use FMA for EMA-style smoothing, IIR (Butterworth/Chebyshev/SSF), HTIT/MAMA-style homodyne, any `a*b+c`.
Avoid FMA for simple ops, when intermediate rounding required, or in SIMD paths (use `Fma.MultiplyAdd` / `Avx512F.FusedMultiplyAdd` / `AdvSimd.Arm64.FusedMultiplyAdd`).
Precompute decay constants:
`private readonly double _alpha; private readonly double _decay; // = 1 - _alpha`
Hot path: `result = Math.FusedMultiplyAdd(prevState, _decay, _alpha * newInput);`

3) IMPLEMENTATION STANDARDS (every indicator)
3.1 File layout
DIR: `lib/[category]/[name]/` (eg `lib/trends/sma/`)
REQ files:
- `[Name].cs` (main impl, `public sealed class`)
- `[Name].Tests.cs` (xUnit)
- `[Name].Validation.Tests.cs` (vs TA-Lib/Skender/Tulip/Ooples)
- `[Name].md` (docs + formulas)
- `[Name].Quantower.cs` (adapter)
- `[Name].Quantower.Tests.cs` (adapter tests)

3.2 Class definition
- Namespace `QuanTAlib`; `[SkipLocalsInit]`; `public sealed class`; implements `ITValuePublisher`.

3.3 State mgmt
- Scalar state: `private record struct State` grouping all scalar vars.
- Maintain `_state` (current) + `_p_state` (prev valid).
- Buffers: `RingBuffer` for sliding windows.
- Resync: periodic full recalculation (eg every 1000 ticks) to limit floating drift in running sums.

3.4 Constructor rules
- Validate params: throw `ArgumentException` + `nameof()`.
- Set Name: eg `$\"Sma({period})\"`.
- Support chaining ctor: `public [Name](ITValuePublisher source, ...)`.
- Event subscribe: `source.Pub += Handle;` (no defensive null checks when source is non-nullable).

3.5 Update(TValue) contract
SIG: `public TValue Update(TValue input, bool isNew = true)` w/ `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
FLOW:
- IF isNew→ `_p_state=_state` then advance counters; ELSE rollback `_state=_p_state`.
- Validate input: if `!double.IsFinite` → substitute last-valid (stored in State).
- Compute (apply FMA where relevant).
- Publish: update `Last`, invoke `Pub`, return `Last`.

3.6 Update(TSeries)
SIG: `public TSeries Update(TSeries source)` adjacent to Update(TValue)
- Create output series
- Call static `Calculate(ReadOnlySpan<double>, Span<double>, ...)`
- Restore internal state by replaying last Period bars (or full series if recursive).

3.7 Static Calculate(TSeries)
- Create indicator instance
- Iterate source series
- Return output TSeries

3.8 Static Calculate(Span) (perf-critical)
SIG: `public static void Calculate(ReadOnlySpan<double> source, Span<double> output, ...)`
RULES:
- Validate args w/ `ArgumentException` incl `nameof(output)` etc (MA0015-friendly).
- SIMD path optional-but-recommended for simple/non-recursive; check `Avx2.IsSupported`.
- Use `stackalloc` for small buffers (threshold ~256) + internal state buffers when SIMD not applicable.
- Scalar fallback must handle NaN safely.

4) DOCUMENTATION STANDARDS (^8)
Mission: persuade skeptical architects via correctness + architecture evidence + benevolent curmudgeon wit; ruthless w/ math, kind to humans.
Audience: practitioners who value implementation + trade-offs.
Voice: GRINGE blend (Bryson warmth; Roach curiosity; Sedaris self-own; O'Rourke cynicism).
Argumentation: steel-man opponents, rebut w/ data; Evidence chain: Why→How→Proof→So what; Feel-Felt-Found ok.
Language: direct cadence; precise nums; avoid "we"; deliberate sentence-length variation; "code as evidence".
Humor: allowed for complexity/context; NOT in perf/security/math correctness claims.
Anti-slop:
- Forbidden words SET{Delve|leverage|pivotal|tapestry|landscape|furthermore|\"is all about\"|\"unlock the power\"|transformative|foster|seamless|ecosystem}
- Avoid em-dashes; avoid formulaic perfectly balanced pro/con tropes.
Markdown: strict CommonMark + markdownlint; watch MD022/MD031, MD030, MD032; include perf env specs (AVX2, Turbo status, sample sizes).

4.1 DOC_TMPL (required sections)
Reference: `lib/trends_IIR/jma/Jma.md` as canonical exemplar.

```markdown
# [ABBREV]: [Full Name]

> "[Memorable quote that captures the indicator's essence or challenges common assumptions]"

[Opening paragraph: What it is + key differentiator. State what makes THIS implementation unique vs common approximations. Include measurable claims (e.g., "within floating-point tolerance", "3-4% divergence during 3-sigma events").]

## Historical Context

[Origin story: Who created it, when, why. Address the knowledge gap: what was publicly known vs actual implementation details. Acknowledge prior approximations and explain how/why this implementation differs. 2-4 paragraphs.]

## Architecture & Physics

[System overview: Describe the indicator as interconnected components. Use numbered subsections for each major component.]

### 1. [Component Name]

[Describe component purpose + behavior. Include conditional logic with mathematical notation:]

$$
X_t = \begin{cases}
P_t & \text{if condition A} \\
f(X_{t-1}, P_t) & \text{otherwise}
\end{cases}
$$

[Explain WHY this design choice matters. Note alternative naming conventions if applicable.]

### 2. [Component Name]

[Continue pattern for each component. Include epsilon guards, buffer sizes, smoothing mechanisms.]

### N. [Final Component / Core Filter]

[For IIR/FIR filters, include transfer function in z-domain if applicable:]

$$
H(z) = \frac{...}{...}
$$

[Explain state-space form and coupled recursions.]

## Mathematical Foundation

[Detailed derivations for each calculation step. Use subsections for logical groupings.]

### [Calculation Name] (e.g., Dynamic Exponent Calculation)

$$
r_t = \frac{|\Delta_t|}{\hat{V}_t}
$$

$$
d_t = \text{clamp}(r_t^{P_{exp}}, 1, \text{logParam})
$$

where:
- $P_{exp} = ...$
- $\text{logParam} = ...$

[Continue for each derived quantity: coefficients, decay rates, recursions.]

### [Recursion Name] (e.g., IIR Recursion)

[State equations in sequence:]

$$
C_{0,t} = (1 - \alpha_t) \cdot P_t + \alpha_t \cdot C_{0,t-1}
$$

[Include parameter mappings (e.g., phase [-100,100] → [0.5,2.5]).]

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

[Itemize computational cost per bar:]

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | N | 1 | N |
| MUL | N | 3 | 3N |
| DIV | N | 15 | 15N |
| CMP/ABS | N | 1 | N |
| SQRT | N | 15 | 15N |
| EXP/POW | N | 50-80 | ... |
| SORT (if applicable) | 1 | ~O(n log n) | ... |
| **Total** | **sum** | — | **~X cycles** |

[Identify dominant cost contributor with percentage.]

### Batch Mode (512 values, SIMD/FMA)

[Explain SIMD applicability. For recursive indicators, acknowledge limitations:]

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| [Vectorizable op] | N | N/8 | 8× |
| FMA operations | N | N/3 | 3× |

**Per-bar savings with SIMD/FMA:**

| Optimization | Cycles Saved | New Total |
| :--- | :---: | :---: |
| [Optimization 1] | ~X | Y |
| **Total SIMD/FMA savings** | **~X cycles** | **~Y cycles** |

**Batch efficiency (512 bars):**

| Mode | Cycles/bar | Total (512 bars) | Overhead |
| :--- | :---: | :---: | :---: |
| Scalar streaming | X | 512X | — |
| SIMD/FMA streaming | Y | 512Y | — |
| **Improvement** | **Z%** | **N saved** | — |

[Explain why improvement is modest/significant based on algorithm characteristics.]

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | N/10 | [Brief justification] |
| **Timeliness** | N/10 | [Brief justification] |
| **Overshoot** | N/10 | [Brief justification] |
| **Smoothness** | N/10 | [Brief justification] |
| **[Custom metric if applicable]** | N/10 | [Brief justification] |

## Validation

[State validation context: proprietary, open-source availability, reference sources.]

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅/N/A | [Implementation status or match notes] |
| **Skender** | ✅/N/A | [Implementation status or match notes] |
| **Tulip** | ✅/N/A | [Implementation status or match notes] |
| **Ooples** | ✅/N/A | [Implementation status or match notes] |
| **[Other reference]** | ✅ | [Match notes] |

## Common Pitfalls

1. **[Pitfall Category]**: [Specific issue + quantified impact. Include formulas for warmup periods, memory footprints, etc.]

2. **[Pitfall Category]**: [Parameter confusion, default behaviors, migration gotchas.]

3. **[Pitfall Category]**: [Computational cost awareness with concrete numbers.]

4. **[Pitfall Category]**: [Memory footprint with per-instance and scaled estimates.]

5. **[Pitfall Category]**: [Edge case limitations.]

6. **[Pitfall Category]**: [API usage (isNew, Reset, etc.).]

## References

- [Author]. ([Year]). "[Title]." *[Source]*.
- [Author]. ([Year]). "[Title]." *[Source]*.
```

4.2 Section requirements checklist
- [ ] Title: `# ABBREV: Full Name` + memorable quote
- [ ] Intro: 1 paragraph, key differentiator, measurable claims
- [ ] Historical Context: origin, knowledge gap, prior art, this impl's difference
- [ ] Architecture & Physics: numbered subsections per component, conditional math, z-domain transfer functions
- [ ] Mathematical Foundation: all derivations with LaTeX, parameter mappings
- [ ] Performance Profile: operation count table, SIMD analysis, quality metrics (1-10 scale)
- [ ] Validation: library comparison table with status + notes
- [ ] Common Pitfalls: 5-7 numbered items with quantified impacts
- [ ] References: academic/forum sources

4.3 Doc linking reqs when adding indicator
Update: `lib/[category]/_index.md`, `lib/_index.md`, `docs/_sidebar.md`, `docs/integration.md`, `docs/indicators.md`, `docs/validation.md`.

5) TESTING PROTOCOL
REQ test files: `[Name].Tests.cs`, `[Name].Validation.Tests.cs`, `[Name].Quantower.Tests.cs`.
5.2 Unit tests (xUnit)
- Data: MUST use `GBM` helper. Never `System.Random`.
- Required coverage buckets:
  A) ctor validation (throws `ArgumentException` w/ ParamName)
  B) basic calc (Update returns TValue; Last/IsHot/Name accessible; known-value check)
  C) state + bar correction (critical): isNew true advances; isNew false rewrites; iterative corrections restore; Reset clears state + last-valid tracking
  D) warmup/convergence: IsHot flips when buffer full; WarmupPeriod period-dependent
  E) robustness (critical): NaN + Infinity use last-valid; batch NaN safe
  F) consistency (critical): BatchCalc == streaming == span == eventing (All 4 modes match)
  G) span API tests: validates lengths w/ ParamName; matches TSeries; handles NaN; avoid stack overflow large data
  H) chainability: `Pub` fires; event-based chaining works
5.3 Validation tests
- Compare vs Skender.Stock.Indicators, TA-Lib (TALib.NETCore), Tulip (Tulip.NETCore), OoplesFinance.
- For each external lib: validate Batch + Streaming + Span where supported.
- Tolerances: `ValidationHelper.SkenderTolerance`=1e-9; `TalibTolerance`=1e-9; `TulipTolerance`=1e-9; `OoplesTolerance`=1e-6.
5.4 Quantower adapter tests (req):
Constructor defaults; MinHistoryDepths; Initialize creates internal indicator; ProcessUpdate historical/new; different OHLC source types.
5.5 Checklists
- Mandatory unit tests include ctor validation, isNew behavior, iterative correction restore, Reset, IsHot warmup, NaN/Infinity handling, mode consistency, span validation.
- At least one full validation suite (Skender batch/stream/span) + Quantower minimal set.

6) QUANTOWER ADAPTER
6.1 File locations
- **Preferred:** Place `[Name].Quantower.cs` + `[Name].Quantower.Tests.cs` in `lib/[category]/[name]/` alongside the indicator.
- **Alternative:** Place in `quantower/[Category]/` subdirectory (legacy pattern).
- Both locations are auto-included in `quantower/Quantower.Tests.csproj`.

6.2 Test project inclusion (automatic)
The `quantower/Quantower.Tests.csproj` includes:
- `<Compile Include="..\lib\**\*.Quantower.cs" />` - all adapters from lib
- `<Compile Include="..\lib\**\*.Quantower.Tests.cs" />` - all adapter tests from lib
- `<Compile Include="[Category]\*.cs" />` - adapters from quantower folder per category
- `<Compile Include="**\*.Tests.cs" />` - all tests from quantower folder

6.3 Implementation requirements
- Inherit from Quantower's `Indicator` base class.
- Use `IndicatorExtensions` helpers for OHLC source mapping.
- Implement `MinHistoryDepths` for warmup period.
- Handle both historical and streaming updates in `ProcessUpdate`.
- Use mocks from `quantower/Mocks/` for unit tests.

6.4 Adding new adapter checklist
1. Create `[Name].Quantower.cs` (in lib or quantower folder)
2. Create `[Name].Quantower.Tests.cs` (same folder as adapter)
3. Verify tests compile: `dotnet build quantower/Quantower.Tests.csproj`
4. Run tests: `dotnet test quantower/Quantower.Tests.csproj --filter "[Name]"`
5. Add to category-specific csproj if adapter in quantower folder (eg `quantower/Trends.csproj`)

7) WORKFLOW & TOOLS
Tools:
- seq-think-mcp: decomposition/planning
- tavily-mcp: fresh API/lib info, .NET updates, perf patterns
- ref-tools-mcp: .NET docs, API specs, SIMD intrinsics
- wolfram-mcp: math validation
- git-mcp: codebase search + conventions
- qdrant-mcp: persist decisions/benchmarks/patterns (no secrets)
Priority: ref-tools → tavily for .NET specifics; seq-think for complex; qdrant for context.
Dev cycle (compact):
1 Recall(qdrant)→2 Analyze(profile)→3 Investigate(git+debug)→4 Research(ref-tools+tavily)→5 Plan(seq-think, STS)→6 Implement(C# 13, SIMD/Span/stackalloc, minimal comments, no regions, no XML in impl)→7 Debug(BenchmarkDotNet, codegen verify)→8 Test(edge cases)→9 Validate(correct+perf, store bench)→10 Memorize(qdrant JSON record).
Git policy:
- No auto-commit; explicit user command only.
- Pre-commit: tests pass; verify no hotpath alloc; verify SIMD codegen.
Commit msg:
`<type>: <imperative verb> <what> [scope]` + why + perf delta + refs + benchmarks.
Types SET{feat|perf|fix|refactor|test|docs}.
Comms: concise tech; bullets for lists; code blocks for examples; perf nums include baseline+optimized+%.

8) CONTEXT MGMT (qdrant)
Store: arch decisions, benchmarks, proven patterns, deprecated approaches.
Record format: `{decision, benchmark, pattern, src, date, tags}`.
Query strategy: Before/During/After work.
Event flow (commit d7dbd70):
For `ITValuePublisher` indicators: subscribe in ctor w/ `source.Pub += Handle;` (don’t store source solely for subscription). Use struct-based event args (`TBarEventArgs`, `TValueEventArgs`). If MA0046 flags non-EventArgs signature, suppress locally w/ targeted pragma + perf rationale.

9) REFERENCE (pitfalls + prohibitions + done criteria)
Common pitfalls: LINQ in hot paths; `new` inside Update; ignore NaN; inconsistent 4 API modes; missing ParamName in `ArgumentException`; missing Quantower adapter/tests; forgetting `docs/validation.md`; using `System.Random` in tests.
Forbidden actions:
- DO NOT LINQ in Update/Calculate
- DO NOT `new` inside Update
- DO NOT change `Directory.Build.props` w/o explicit instruction
- DO NOT remove `[SkipLocalsInit]` / `[MethodImpl]`
- DO NOT ignore NaN/Infinity
- DO NOT skip `[Name].Quantower.cs` + `[Name].Quantower.Tests.cs`
- DO NOT skip updating `docs/validation.md`
Done checklist (condensed):
Source verified (PineScript or equiv); C# 13 optimized; O(1) where possible; SIMD where possible; FMA in hot paths where applicable; all 6 files exist; Update handles isNew + NaN; Update alloc-free; Calculate(Span) implemented + ParamName validation; unit+validation+adapter tests pass; docs complete + markdownlint; all required indices updated; CodeRabbit issues resolved; benchmarks run + stored in qdrant.