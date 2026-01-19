# QuanTAlib Agent Playbook (v2026-01-11)

> This file is the onboarding packet for any autonomous agent touching QuanTAlib. Read it end-to-end before issuing a single command.

## 0. Scope & Signals
- Targets the entire repo (library, docs, Quantower adapters, tooling).
- Applies to GitHub Actions, MCP agents, and local shells.
- No Cursor or Copilot rule files exist right now; follow this document plus `.clinerules/AGENTS.md` for deeper philosophy.
- Root namespace: `QuanTAlib`. Target frameworks: net8.0 + net10.0 preview features.

## 1. Toolchain Baseline
- SDK: `.globalconfig` pins none; install .NET 8/10 SDKs.
- Solution: `QuanTAlib.sln` aggregates `lib` + `quantower` projects.
- Implicit usings disabled for library projects (`DisableImplicitNamespaceImports=true`), so import explicitly.
- Nullable + analyzers enforced in `Directory.Build.props`; warnings become errors on Release.
- Unsafe code, SIMD, intrinsics, stackalloc, `[SkipLocalsInit]`, `[AggressiveInlining]` allowed and encouraged.

## 2. Build & Restore Commands (run from repo root)
1. Restore everything:
   ```powershell
   dotnet restore QuanTAlib.sln
   ```
2. Build library quickly (Debug, net10.0):
   ```powershell
   dotnet build lib/quantalib.csproj --configuration Debug --framework net10.0 --no-restore
   ```
3. Build full solution (Release):
   ```powershell
   dotnet build QuanTAlib.sln --configuration Release --no-restore
   ```
4. Build Quantower adapters bundle (Release, example subset):
   ```powershell
   dotnet build quantower/Momentum.csproj --configuration Release --no-restore
   dotnet build quantower/Trends.csproj --configuration Release --no-restore
   dotnet build quantower/Volume.csproj --configuration Release --no-restore
   ```
5. Generate SARIF + coverage + NDepend badges (long runner, Windows PowerShell 7+):
   ```powershell
   pwsh ndepend/ndepend.ps1
   ```

## 3. Test Commands
- **All QuanTAlib unit + validation tests (Debug):**
  ```powershell
  dotnet test lib/QuanTAlib.Tests.csproj --configuration Debug --no-build
  ```
- **Quantower adapter tests (Debug):**
  ```powershell
  dotnet test quantower/Quantower.Tests.csproj --configuration Debug --no-build
  ```
- **Full coverage run with Coverlet + runsettings (matches CI):**
  ```powershell
  dotnet test lib/QuanTAlib.Tests.csproj --configuration Debug --no-build \
    --collect:"XPlat Code Coverage" --settings coverlet.runsettings \
    --results-directory ./TestResults
  dotnet test quantower/Quantower.Tests.csproj --configuration Debug --no-build \
    --collect:"XPlat Code Coverage" --settings coverlet.runsettings \
    --results-directory ./TestResults
  ```
- **Single test / filtered suite:** Replace the predicate with any substring of `FullyQualifiedName`.
  ```powershell
  dotnet test lib/QuanTAlib.Tests.csproj --no-build --configuration Debug \
    --filter "FullyQualifiedName~EmaValidation"
  ```
- **Quick span-path smoke (example)**
  ```powershell
  dotnet test lib/QuanTAlib.Tests.csproj --configuration Release --no-build --filter "Category=Span"
  ```
- **CI parity (Ubuntu)** uses `dotnet test --no-build --configuration Debug --collect:"XPlat Code Coverage;Format=opencover,cobertura,lcov" --results-directory ./TestResults --logger "trx;LogFileName=test_results.trx"`. Replicate locally when debugging pipeline-only regressions.

## 4. Lint / Quality Gates
- `dotnet build` (any configuration) enforces Roslyn, Sonar, Meziantou, Roslynator, SourceLink analyzers; fix warnings locally.
- `pwsh ndepend/ndepend.ps1` cleans, rebuilds, runs coverage, executes NDepend analysis, and emits badges + `.sarif/quantalib.sarif`.
- Qodana & SonarCloud pipelines read from SARIF plus coverage; keep `.sarif` directory clean.
- `ndepend/ndepend.ps1` expects env var `NDEPEND_LICENSE`; script still runs but warns if missing.
- `qodana.yaml` is present; locally you may execute `docker run -v ${PWD}:/data/project jetbrains/qodana-dotnet ...` (optional, not required for every change).

## 5. Repository Structure Highlights
- `lib/` – core indicators, tests, validation, docs per indicator folder.
- `quantower/` – platform adapters, per-category csproj plus shared tests.
- `docs/` – architecture, indicator catalog, integration notes, validation matrices.
- `perf/` – BenchmarkDotNet harnesses; results must go to `temp/benchmarks` during dev, never committed.
- `temp/` – gitignored scratch (scripts, datasets, logs). Create subdirs like `temp/scripts/run_bench_YYYYMMDD_hhmmss.ps1`.
- `.clinerules/AGENTS.md` – canonical philosophy file. Treat this AGENTS.md as quickstart + commands; consult `.clinerules` for deep rules.

## 6. Coding Style & Formatting (superset of .editorconfig)
1. **Whitespace & files**
   - LF endings, UTF-8, trim trailing whitespace, final newline required.
   - Indent with 4 spaces; no tabs.
2. **Var usage** (`.editorconfig` enforced)
   - Prefer explicit types for primitive math values (int, double, string) to document formulas.
   - Allow `var` only when RHS makes the type obvious (e.g., `new RingBuffer(capacity)` or Linq-free aggregator returns).
3. **Imports**
   - No implicit namespaces: add explicit `using` statements (file-scoped) for every dependency.
   - Sort system namespaces first, then QuanTAlib/local.
4. **Types & naming**
   - `public` members: PascalCase; private fields: `_camelCase`; constants: `SCREAMING_CASE` only for static readonly calibration.
   - Accept well-known abbreviations (Sma, Ema, Rsi, Atr, Dmx, Jma, Vidya) per library convention.
   - Use `record struct` for aggregated state, `readonly struct` for data carriers (`TValue`, `TBar`).
5. **Attributes & perf toggles**
   - `[SkipLocalsInit]` atop performance-critical classes/methods.
   - `[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]` for hot path helpers (`GetFiniteValue`, `Update`, `Calculate`).
6. **Memory & SIMD canon (DCT from .clinerules)**
   - Zero heap allocations inside `Update` and span-based `Calculate`; prefer `stackalloc` <=256 bytes, `ArrayPool` when bigger.
   - Maintain Structure-of-Arrays (SoA) layout: `List<long> _t`, `List<double> _v`, exposures via `CollectionsMarshal.AsSpan`.
   - Use `System.Runtime.Intrinsics` (AVX2/AVX-512) or `System.Numerics.Vector<T>` for batch loops; pair with scalar fallback for recursive cases.
   - Apply `Math.FusedMultiplyAdd` for every `a*b + c` in smoothing / IIR patterns.
7. **State & streaming rules**
   - Always implement `_state` / `_p_state` record structs for rollback when `isNew=false` (bar correction).
   - Maintain `Last`, `IsHot`, `WarmupPeriod`, and `Name` consistently.
   - Validate every constructor argument and throw `ArgumentException(nameof(param))` (MA0001-friendly).
8. **Error handling**
   - Never swallow exceptions. Guard invalid parameters early; prefer `ArgumentOutOfRangeException` for bounds.
   - Replace non-finite input with last valid value; never propagate NaN/Infinity to downstream spans or events.
9. **Events & reactive**
   - Subscribe directly (`source.Pub += Handle;`) without extra null guards if parameter non-nullable.
   - Custom delegates (`TValuePublishedHandler`) are acceptable; MA0046 suppressed repo-wide.
10. **Date/time & culture**
    - Always use `DateTime.UtcNow`. No `DateTime.Now`, `DateTimeOffset.Now`, or culture-specific string formatting in hot paths.
11. **Docs**
    - Markdown lint strict (MD022/30/31/32). Use skeptical, data-driven voice as described in `.clinerules/AGENTS.md` Section 4.
    - Every new indicator requires doc + validation entries in all indexes.

## 7. Testing Doctrine
- Test files live alongside sources (`[Name].Tests.cs`, `[Name].Validation.Tests.cs`, `[Name].Quantower.Tests.cs`).
- Data generation uses GBM helpers in `lib/feeds/gbm`; do NOT instantiate `System.Random` directly inside tests.
- Validation tests must compare against TA-Lib, Skender, Tulip, or Ooples with tight tolerances (see `ValidationHelper`).
- Always assert streaming vs batch vs span parity (last 100 bars).
- Include `isNew=false` correction tests, NaN/Infinity handling, `Reset`, `IsHot` transitions.

## 8. Workflow Expectations
1. Query qdrant memory before designing new algorithm; store decisions/benchmarks after validation.
2. Use `temp/` for generated artifacts, never commit.
3. When performance changes are made, capture BenchmarkDotNet tables (before/after) and summarize in PR/commit descriptions.
4. Do not push commits unless explicitly asked; run `git status`/`git diff` before staging.
5. Pull requests must mention which external validation suites ran (TA-Lib, Skender, etc.).

## 9. Common Pitfalls (avoid immediately)
- LINQ, `new` allocations, boxing, or string concatenation inside hot loops.
- Forgetting `docs/validation.md` rows when adding indicators.
- Failing to update Quantower adapters/tests when changing indicator APIs.
- Leaving Coverlet residue outside `TestResults/`.
- Using `DateTime.Now` or culture-specific formatting.
- Omitting `nameof(...)` in exceptions, breaking analyzer expectations.

## 10. Ready Checklist Before PR
- [ ] `dotnet build QuanTAlib.sln --configuration Release --no-restore` passes.
- [ ] `dotnet test lib/QuanTAlib.Tests.csproj --configuration Debug --no-build` passes.
- [ ] `dotnet test quantower/Quantower.Tests.csproj --configuration Debug --no-build` passes.
- [ ] Validation suite compares against at least one external library per indicator change.
- [ ] Docs + indexes updated, markdownlint clean.
- [ ] Benchmarks (if perf-sensitive change) captured under `temp/benchmarks` and summarized.
- [ ] `.sarif` regenerated if analyzer rules change.
- [ ] qdrant updated with new decisions/benchmarks.

Stay fast, stay precise, keep the garbage collector asleep.
