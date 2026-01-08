workflows:
  - name: "Indicator Review"
    description: "Comprehensive review of an indicator implementation against QuanTAlib standards"
    steps:
      - step: "1. File Structure Verification"
        tasks:
          - "Verify all 6 required files exist: [Name].cs, [Name].md, [Name].Tests.cs, [Name].Validation.Tests.cs, [Name].Quantower.cs, [Name].Quantower.Tests.cs"
          - "Check for [Name].Pine and alert if it is missing"
          - "List all files in the indicator directory"

      - step: "2. AGENTS.md Compliance Review"
        tasks:
          - "Architecture & Memory Model:"
            - "Verify Structure of Arrays (SoA) pattern usage"
            - "Check for RingBuffer or appropriate data structure"
            - "Confirm zero allocation in hot paths (Update method)"
            - "Verify O(1) complexity for streaming updates (or document why not possible)"
          - "State Management:"
            - "Check for 'private record struct State' pattern"
            - "Verify _state and _p_state for rollback capability"
            - "Confirm state restoration in isNew=false path"
          - "Performance Attributes:"
            - "Class has [SkipLocalsInit] attribute"
            - "Update method has [MethodImpl(MethodImplOptions.AggressiveInlining)]"
            - "Batch methods use [MethodImpl(MethodImplOptions.AggressiveOptimization)] where appropriate"
          - "SIMD Optimization:"
            - "Check for AVX2/AVX-512/NEON implementations in Batch methods"
            - "Verify ContainsNonFinite() check before SIMD path"
            - "Confirm scalar fallback path exists"
          - "FMA Usage (if applicable):"
            - "Check for Math.FusedMultiplyAdd() in EMA/exponential smoothing patterns"
            - "Verify decay constants are pre-computed"
          - "API Design:"
            - "Update(TValue, bool isNew = true) method exists"
            - "Update(TSeries) batch method exists"
            - "Static Batch(TSeries) method exists"
            - "Static Batch(ReadOnlySpan, Span) method exists"
            - "Optional: Calculate(TSeries) returns (TSeries, Indicator) tuple"
            - "Optional: Prime(ReadOnlySpan) method for state initialization"
          - "Robustness:"
            - "NaN/Infinity handling via GetValidValue or last-valid-value pattern"
            - "Constructor validates all parameters (throws ArgumentException)"
            - "Reset() method clears all state"
          - "Reactive Pattern:"
            - "Implements ITValuePublisher interface"
            - "Has Pub event for chaining"
            - "Constructor accepts ITValuePublisher source parameter"
            - "Uses direct subscription pattern (source.Pub += handler)"

      - step: "3. testprotocol.md Compliance Review"
        tasks:
          - "Unit Tests ([Name].Tests.cs):"
            - "Constructor validation tests (invalid parameters throw ArgumentException with paramName)"
            - "Basic functionality tests (Calc_ReturnsValue, FirstValue_ReturnsExpected)"
            - "State management tests (IsNew true/false, IterativeCorrections_RestoreToOriginalState)"
            - "Reset tests (Reset_ClearsState, Reset_ClearsLastValidValue)"
            - "Warmup tests (IsHot_BecomesTrueWhenBufferFull, WarmupPeriod_IsSetCorrectly)"
            - "NaN/Infinity handling tests (all return finite values)"
            - "Consistency test: AllModes_ProduceSameResult (Batch, Span, Streaming, Eventing)"
            - "Span API tests (validates input, matches TSeries, handles NaN)"
            - "Edge cases: Period=1, empty input, flat line"
            - "Prime tests (if Prime method exists)"
            - "Calculate tests (if Calculate method exists)"
            - "Chainability tests"
          - "Validation Tests ([Name].Validation.Tests.cs):"
            - "Skender validation: Batch, Streaming, Span (3 tests minimum)"
            - "TA-Lib validation: Batch, Streaming, Span (3 tests minimum)"
            - "Tulip validation: Batch, Streaming, Span (3 tests minimum)"
            - "Ooples validation: Batch (1 test minimum)"
            - "Total: 10 validation tests against 4 external libraries"
            - "Uses ValidationHelper.VerifyData with correct tolerance constants"
            - "Uses ValidationTestData class for test data generation"
          - "Quantower Tests ([Name].Quantower.Tests.cs):"
            - "Constructor_SetsDefaults"
            - "MinHistoryDepths validation"
            - "ShortName_IncludesParameters"
            - "Initialize_CreatesInternalIndicator"
            - "ProcessUpdate tests (HistoricalBar, NewBar, NewTick)"
            - "DifferentSourceTypes_Work"
            - "Parameter modification tests"
          - "Test Data Generation:"
            - "All tests use GBM (Geometric Brownian Motion) for realistic data"
            - "No direct use of System.Random in tests"

      - step: "4. Performance Optimization Verification"
        tasks:
          - "Hot Path Analysis:"
            - "Update method: zero allocations, no 'new', no LINQ"
            - "Batch scalar path: uses stackalloc or ArrayPool<T>"
            - "SIMD paths use proper intrinsics (System.Runtime.Intrinsics)"
          - "Complexity Verification:"
            - "Document actual complexity (O(1), O(n), O(n²))"
            - "If not O(1), explain why (e.g., recursive algorithm)"
          - "Memory Efficiency:"
            - "RingBuffer for sliding windows"
            - "No unnecessary List<T> or List<double> in hot paths"
            - "Proper capacity pre-allocation where needed"
          - "Resync Mechanism (if applicable):"
            - "Running sums have periodic recalculation to prevent drift"
            - "ResyncInterval constant defined (typically 1000)"

      - step: "5. API Consistency Check"
        tasks:
          - "Run or verify AllModes_ProduceSameResult test exists and passes"
          - "Confirm all 4 API modes produce identical results:"
            - "1. Batch: Indicator.Batch(TSeries, params)"
            - "2. Span: Indicator.Batch(ReadOnlySpan, Span, params)"
            - "3. Streaming: new Indicator(params).Update(TValue)"
            - "4. Eventing: new Indicator(source, params)"
          - "Precision: Results match to 9 decimal places minimum"

      - step: "6. Documentation Quality Review ([Name].md)"
        tasks:
          - "Structure Verification:"
            - "Title: '## [Name]: [Full Name]'"
            - "Opening quote (witty, insightful, or cynical)"
            - "Introduction paragraph"
            - "## Historical Context section"
            - "## Architecture & Physics section"
            - "## Mathematical Foundation section (with LaTeX formulas)"
            - "## Performance Profile section (table with 7 metrics)"
            - "## Validation section (table with library status)"
            - "### Common Pitfalls section"
          - "Style Compliance (techdocs.md):"
            - "No first-person plural ('we') - uses 'QuanTAlib' as subject"
            - "Gringe voice: blend of Bryson/Roach/Sedaris/O'Rourke"
            - "Technical precision with personality"
            - "Evidence-based claims with specific numbers"
            - "No forbidden words: delve, leverage, tapestry, ecosystem, seamless, etc."
            - "No em-dashes (use colons or periods)"
          - "Markdown Linting:"
            - "MD022/MD031: Headers and code blocks surrounded by blank lines"
            - "MD030: Exactly one space after list markers"
            - "MD032: Lists surrounded by blank lines"
          - "Performance Table Metrics:"
            - "Throughput (1-10, with ns/bar if available)"
            - "Allocations (0 or specific count)"
            - "Complexity (Big O notation)"
            - "Accuracy (1-10)"
            - "Timeliness (1-10)"
            - "Overshoot (0-10)"
            - "Smoothness (1-10)"
          - "Validation Table:"
            - "TA-Lib status (✅/⚠️/❌)"
            - "Skender status"
            - "Tulip status"
            - "Ooples status"

      - step: "7. Cross-Reference Verification"
        tasks:
          - "Check indicator is cataloged in:"
            - "lib/_index.md (main library index)"
            - "lib/[category]/_index.md (category index, e.g., lib/trends/_index.md)"
            - "docs/_sidebar.md (documentation navigation)"
            - "docs/indicators.md (full indicators list)"
            - "docs/validation.md (validation status table)"
            - "docs/integration.md (if integration examples needed)"
          - "Verify links are not broken"
          - "Add indicators if missing in catalogs"
          - "Confirm alphabetical ordering within lists"

      - step: "8. Source Algorithm Verification"
        tasks:
          - "If .pine file exists, compare against .cs implementation and alert if algo is inconsistent"
          - "Check algorithm against published sources (search Ref MCP or Tavily MCP)"
          - "Verify mathematical formulas match documentation"
          - "Check for known indicator variants (e.g., Wilder's RSI vs. Cutler's RSI)"
          - "Document any deviations from standard implementation"

      - step: "9. Edge Cases & Error Handling"
        tasks:
          - "Division by zero scenarios tested"
          - "Empty input handling"
          - "Single value input"
          - "All NaN input"
          - "Period = 1 edge case"
          - "Very large periods (>10000)"
          - "Negative inputs (if applicable)"
          - "Zero inputs (if applicable)"

      - step: "10. Generate Review Report"
        tasks:
          - "Create summary with:"
            - "Overall grade (A+/A/B/C/D/F)"
            - "What could improve the quality and performance?"
            - "AGENTS.md compliance score (percentage)"
            - "testprotocol.md compliance score (percentage)"
            - "File count and test statistics"
            - "Performance highlights"
            - "Validation summary (X/Y libraries validated)"
          - "List findings by severity:"
            - "🔴 Critical: Blocks production use"
            - "🟡 Warning: Should be addressed soon"
            - "🟢 Recommendation: Nice-to-have improvements"
          - "Provide specific action items with file:line references"
          - "Highlight exemplary patterns for other indicators to follow"
          - "Final verdict: Production Ready / Needs Work / Blocked"

      - step: "11. Optional: Benchmark Verification"
        tasks:
          - "Check if benchmark exists in perf/Benchmark.cs"
          - "Verify allocation count = 0 for hot paths"
          - "Review throughput numbers (ops/sec or ns/op)"
          - "Compare against baseline (if available)"

    notes:
      - "Use qdrant MCP to check for stored QuanTAlib patterns and decisions"
      - "Use ref-tools MCP for .NET and SIMD intrinsics documentation"
      - "Use tavily MCP for published indicator sources and mathematical definitions"
      - "Use wolfram MCP for mathematical formula verification if needed"
      - "Focus on AGENTS.md and testprotocol.md compliance first - these are the foundation"
      - "No need to check Quantower adapter correctness in detail - trust the tests"
      - "Document any deviations from standards with rationale"
      - "Prioritize correctness > performance > style"

```yaml
# Usage Example:
```
User: "Review indicator SMA"

Cline: 
1. Lists all files in lib/trends/sma/
2. Reads each file systematically
3. Checks compliance against AGENTS.md (architecture, performance, API design)
4. Checks compliance against testprotocol.md (test coverage)
5. Verifies API consistency (AllModes test)
6. Reviews documentation quality
7. Checks cross-references in docs
8. Generates comprehensive review report with:
   - Overall grade
   - Compliance scores
   - Specific findings
   - Action items
   - Final verdict

Example output:
"SMA Indicator: Grade A+ (100% AGENTS.md, 98% testprotocol.md)
✅ Production Ready - Gold Standard Implementation
- 67 tests passing
- 4 libraries validated
- O(1) complexity with SIMD optimization
- Zero allocations in hot paths
- Exemplary documentation"
```

# Quick Reference: Key Compliance Points

## AGENTS.md Critical Items
- [ ] `[SkipLocalsInit]` on class
- [ ] `record struct State` pattern
- [ ] Zero allocations in Update
- [ ] O(1) streaming (or documented reason)
- [ ] SIMD in Batch methods
- [ ] NaN/Infinity handling
- [ ] All 4 API modes (Update, Batch TSeries, Batch Span, Calculate)
- [ ] ITValuePublisher implementation

## testprotocol.md Critical Items
- [ ] Constructor validation tests
- [ ] AllModes_ProduceSameResult test
- [ ] IterativeCorrections_RestoreToOriginalState test
- [ ] Validation against ≥3 external libraries
- [ ] NaN/Infinity handling tests
- [ ] Span API validation tests
- [ ] Quantower adapter tests
- [ ] Uses GBM for test data (not System.Random)

## Documentation Critical Items
- [ ] Performance profile table (7 metrics)
- [ ] Validation status table (4 libraries)
- [ ] LaTeX formulas
- [ ] No first-person plural
- [ ] Markdown lint clean
- [ ] Listed in all required index files
