# Plan: Rewrite All Oscillator .md Files to Gold Standard Template

## Context

The gold standard documentation template lives at `temp/doc_template.md`. It was derived from comparative analysis of 8 existing indicator docs across tiers (Jma.md, Rsi.md, Willr.md, Obv.md, Ssf.md, Adx.md, Imi.md, Mom.md) and is stored in qdrant.

The first application was `lib/oscillators/trix/Trix.md` (completed, verified against source with 22 verification points). This serves as the reference exemplar for oscillator-category docs.

## Inventory (19 indicators, 1 done)

| # | Indicator | Current Lines | File | Status |
|---|-----------|:------------:|------|--------|
| 1 | TRIX | 157 | `lib/oscillators/trix/Trix.md` | **Done** |
| 2 | WillR | 110 | `lib/oscillators/willr/Willr.md` | Pending |
| 3 | Stoch | 91 | `lib/oscillators/stoch/Stoch.md` | Pending |
| 4 | Stochf | 94 | `lib/oscillators/stochf/Stochf.md` | Pending |
| 5 | StochRSI | 157 | `lib/oscillators/stochrsi/Stochrsi.md` | Pending |
| 6 | SMI | 106 | `lib/oscillators/smi/Smi.md` | Pending |
| 7 | KDJ | 81 | `lib/oscillators/kdj/Kdj.md` | Pending |
| 8 | Fisher | 43 | `lib/oscillators/fisher/Fisher.md` | Pending |
| 9 | AC | 84 | `lib/oscillators/ac/Ac.md` | Pending |
| 10 | AO | 45 | `lib/oscillators/ao/Ao.md` | Pending |
| 11 | APO | 47 | `lib/oscillators/apo/Apo.md` | Pending |
| 12 | BBB | 71 | `lib/oscillators/bbb/Bbb.md` | Pending |
| 13 | BBS | 89 | `lib/oscillators/bbs/Bbs.md` | Pending |
| 14 | CFO | 102 | `lib/oscillators/cfo/Cfo.md` | Pending |
| 15 | DPO | 73 | `lib/oscillators/dpo/Dpo.md` | Pending |
| 16 | Inertia | 52 | `lib/oscillators/inertia/Inertia.md` | Pending |
| 17 | PGO | 82 | `lib/oscillators/pgo/Pgo.md` | Pending |
| 18 | TTM Wave | 106 | `lib/oscillators/ttm_wave/TtmWave.md` | Pending |
| 19 | Ultosc | 88 | `lib/oscillators/ultosc/Ultosc.md` | Pending |

## Execution Workflow (per indicator)

Each rewrite follows the same proven workflow used for TRIX:

### Step 1: Gather Implementation Details
- Use `understand` (dotnet-semantic-mcp) scoped to the indicator class to get full source, hierarchy, references
- Read the `.Validation.Tests.cs` file to extract validation library coverage and tolerances
- Check for `.pine` file in the indicator directory (source material reference)

### Step 2: Write the Document
Follow the gold standard template sections in order:
1. **Title + Quote** - `# ABBREV: Full Name` + witty one-liner
2. **Quick-ref card** - Category, Inputs, Parameters, Outputs, Output range, Warmup
3. **Key takeaways** - 5 bullets
4. **Historical Context** - 2-3 paragraphs
5. **What It Measures and Why It Matters** - 2-3 opinionated paragraphs
6. **Mathematical Foundation** - LaTeX formulas, parameter mapping, warmup period
7. **Architecture & Physics** - Pipeline description, state management, FMA usage, edge cases
8. **Interpretation and Signals** - Signal zones table, patterns, practical notes
9. **Related Indicators** - 2-4 with relative links
10. **Validation** - Simplified table (Status/Notes columns only)
11. **Performance Profile** - Key optimizations, operation count, SIMD analysis
12. **Common Pitfalls** - 5-7 numbered items
13. **FAQ** - Optional, for complex indicators
14. **References** - Academic and web sources

### Step 3: Verify Against Source
Cross-check all technical claims against the `.cs` source:
- Default period, parameter constraints
- State struct fields and initialization
- Alpha/decay formulas
- Warmup period formula and IsHot condition
- FMA patterns used
- NaN/Infinity handling
- Bar correction logic
- Validation library coverage matches test file

### Step 4: Store in qdrant
Record completion with tags for future reference.

## Batching Strategy

Process in groups of related indicators for cross-referencing efficiency:

### Batch 1: Stochastic Family (share architecture patterns)
- Stoch, Stochf, StochRSI, KDJ, SMI

### Batch 2: Bill Williams / Momentum Oscillators
- AC, AO, APO, CFO, DPO

### Batch 3: Bollinger/Statistical Oscillators
- BBB, BBS, PGO, Inertia

### Batch 4: Remaining
- Fisher, WillR, TtmWave, Ultosc

## Template Rules Reminder

- **Voice**: GRINGE blend. Skeptical-architect tone.
- **Anti-slop forbidden words**: delve, leverage, pivotal, tapestry, landscape, furthermore, seamless, ecosystem, transformative, foster
- **No em-dashes**
- **Quality Metrics section**: OMIT (oscillators are not trends/filters)
- **Validation table**: Simplified 2-column format (Status, Notes)
- **LaTeX**: Only in Mathematical Foundation section
- **Code blocks**: Only in Architecture & Physics section for pipeline diagrams
- **Exemplar**: `lib/oscillators/trix/Trix.md` (completed reference)

## Dependencies

- Gold standard template: `temp/doc_template.md`
- Reference exemplar: `lib/oscillators/trix/Trix.md`
- C# analysis: requires `__unlock_csharp_analysis__` per session
- All 19 indicators have validation tests (confirmed)
- All 19 indicators have existing .md files to be replaced
