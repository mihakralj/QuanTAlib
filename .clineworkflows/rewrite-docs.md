# Documentation Rewrite Workflow

## Objective

Batch-process all markdown documentation files to apply the Grizzled Architect persona with consistent style guidelines.

## Style Guidelines Summary

### Voice & Tone

- **Slavic Cadence**: Direct, article-omitting where natural. "Architecture is trade-off. You want speed? Give me memory."
- **Evidence Hierarchy**: Why → How → Proof → So What
- **Bryson Warmth**: Gentle, inclusive humor that invites the reader in
- **Grizzled Verbs**: Code doesn't "run"; it grinds, chokes, strains, or sprints
- **No Pronouns**: Avoid I, we, my, me. Use impersonal constructions.
- **No Personification**: The library does not "want" or "decide" things.

### Anti-Slop Rules

**Forbidden words:**
- Delve, leverage, pivotal, tapestry, landscape, furthermore
- "is all about", "unlock the power", transformative, foster, seamless, ecosystem

**Structural rules:**
- No em-dashes (use colons or periods)
- No lists of exactly 3 or 5 items (use 4, 6, or 7)
- No perfectly balanced pros/cons
- No "On one hand... on the other" tropes

### Markdown Compliance

- MD022: Blank lines around headers
- MD031: Blank lines around fenced code blocks
- MD032: Blank lines around lists
- Use code blocks with language specifiers
- Tables for data-heavy content

### Content Requirements

- All claims backed by specific, measurable numbers
- Include test environment specs for benchmarks
- Code examples for implementation concepts
- Reference sources at end

## File Categories

### Priority 1: Core Documentation (docs/*.md)

| File | Status | Notes |
| :--- | :----: | :---- |
| architecture.md | ✅ | Rewritten with tables, trade-offs |
| api.md | ✅ | Clear mode explanations, code examples |
| benchmarks.md | ✅ | Added Grizzled voice, GC humor |
| errors.md | ✅ | Light touchups, preserved George Box quote |
| glossary.md | ✅ | Added personality to term definitions |
| indicators.md | ✅ | Catalog with Grizzled intro prose |
| integration.md | ✅ | Platform guides with gotchas sections |
| ma-qualities.md | ✅ | Four qualities with Woody Guthrie quote, comparative table |
| ndepend.md | ✅ | Bill Gates quote, quality gates table, interpretation guide |
| trendcomparison.md | ✅ | George Box quote, pattern analysis, 25-indicator scorecard |
| usage.md | ✅ | Kent Beck quote, mode comparison, gotchas per mode |
| validation.md | ✅ | Russian proverb, validation philosophy, symbol legend |

### Priority 2: Indicator Documentation (lib/**/*.md)

Each indicator doc should follow this template:

```markdown
# ABBREV: Full Name

> "Memorable quote that captures essence or challenges assumptions"

[Opening paragraph: What it is + key differentiator. Measurable claims.]

## Historical Context

[Origin story: Who created it, when, why. 2-4 paragraphs.]

## Architecture & Physics

[System overview with numbered subsections per component.]

### 1. Component Name

[Math notation, conditional logic, design rationale.]

## Mathematical Foundation

[Detailed derivations with LaTeX. Parameter mappings.]

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| ... | ... | ... | ... |

### Benchmark Results

[Test environment, comparative performance table.]

### Quality Metrics

| Metric | Score | Notes |
| :----- | ----: | :---- |
| Accuracy | N/10 | ... |
| Timeliness | N/10 | ... |
| Overshoot | N/10 | ... |
| Smoothness | N/10 | ... |

## Validation

| Library | Batch | Streaming | Span | Notes |
| :------ | :---: | :-------: | :--: | :---- |
| TA-Lib | ✅/❌ | ✅/❌ | ✅/❌ | ... |
| Skender | ✅/❌ | ✅/❌ | ✅/❌ | ... |
| Tulip | ✅/❌ | ✅/❌ | ✅/❌ | ... |
| Ooples | ✅/❌ | — | — | ... |

## Common Pitfalls

1. **Pitfall Name**: Description with quantified impact.
2. ...

## Usage Examples

[Code examples for streaming, batch, span, eventing.]

## Implementation Notes

[State structure, optimization techniques, memory summary.]

## References

- Author. (Year). "Title." *Source*.
```

### Priority 3: Index Files

- `_sidebar.md`: Navigation structure
- `lib/_index.md`: Category overview
- `lib/[category]/_index.md`: Category-specific index

### Priority 4: DocFx Mirror (docfx/indicators/*.md)

Many duplicate lib/ content. Update in sync.

## Execution Commands

### Process Single File

```bash
# Read, analyze, rewrite pattern
cline read lib/trends_IIR/[indicator]/[Indicator].md
# Apply style guidelines
# Write updated content
```

### Validate Markdown

```bash
npx markdownlint-cli2 "docs/**/*.md" "lib/**/*.md"
```

### Track Progress

Update this workflow file after each batch:

```markdown
| Category | Total | Done | Remaining |
| :------- | ----: | ---: | --------: |
| docs/ | 12 | 1 | 11 |
| lib/trends_IIR/ | 24 | 1 | 23 |
| lib/trends_FIR/ | 17 | 0 | 17 |
| lib/momentum/ | 6 | 0 | 6 |
| ... | ... | ... | ... |
```

## Quality Checklist (Per File)

- [ ] No forbidden words
- [ ] No em-dashes
- [ ] No I/we/my pronouns
- [ ] No personification
- [ ] Tables have 4+ or 6+ items (not exactly 3 or 5)
- [ ] Blank lines around headers, code blocks, lists
- [ ] All claims have measurable evidence
- [ ] Code examples include language specifier
- [ ] At least one Bryson-warmth moment per major section

## Examples of Good Rewrites

### Before (Anti-slop violation)

> "The EMA is a powerful tool that helps traders leverage market momentum to unlock profitable opportunities."

### After (Grizzled Architect)

> "The EMA applies exponentially decaying weights to older prices. Faster reaction without the drop-off effect that makes SMA users twitch nervously around window boundaries."

### Before (Personification)

> "QuanTAlib wants to give you the best possible accuracy."

### After (Impersonal)

> "QuanTAlib validates against original research papers. Accuracy is verified, not assumed."

### Before (Missing evidence)

> "The indicator is very fast."

### After (Measurable)

> "The indicator processes 500,000 bars in 318 μs (0.64 ns/bar) with zero heap allocations."