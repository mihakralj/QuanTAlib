# NDepend Report

> "Measuring programming progress by lines of code is like measuring aircraft building progress by weight."  Bill Gates (and yet, here are the metrics anyway)

Static analysis tools either tell uncomfortable truths or produce comfortable lies. NDepend belongs to the first category. The report below dissects QuanTAlib's architecture with the cold precision of a pathologist examining code for signs of technical debt.

<iframe src="../ndepend/NDependOut/NDependReport.html" 
        style="width: 100%; height: calc(100vh - 100px); border: 1px solid #ccc; border-radius: 4px;"
        title="NDepend Report"
        frameborder="0"
        allowfullscreen>
</iframe>

## What NDepend Measures

NDepend examines codebases the way a structural engineer examines bridges: looking for load-bearing weaknesses before they become collapse points.

| Metric Category | What It Reveals | Why It Matters |
| :-------------- | :-------------- | :------------- |
| Cyclomatic Complexity | Control flow branch count per method | Methods above 15 become untestable |
| Afferent Coupling | How many types depend on this type | High values create ripple effects |
| Efferent Coupling | How many types this type depends on | High values indicate poor encapsulation |
| Lines of Code | Raw size metric | Correlates loosely with defect density |
| Technical Debt | Estimated remediation hours | The cost of shortcuts taken |
| Coverage Delta | Change in test coverage between builds | Regression early warning system |

## QuanTAlib Quality Gates

The NDepend analysis enforces several quality gates. Violations block the build:

| Gate | Threshold | Rationale |
| :--- | :-------- | :-------- |
| Method Complexity | d 20 | Beyond this, testing becomes guesswork |
| Type Coupling | d 30 | Beyond this, changes cascade unpredictably |
| Test Coverage | e 80% | Below this, refactoring becomes gambling |
| Technical Debt Ratio | d 5% | Beyond this, velocity degrades measurably |
| Critical Issues | 0 | Any critical issue blocks release |
| Dependency Cycles | 0 | Cycles create build order nightmares |

## Interpreting the Dependency Matrix

The dependency matrix shows which namespaces depend on which. Blue cells indicate dependencies. The diagonal should be empty (no self-dependencies). Off-diagonal clusters indicate potential architectural boundaries.

**Healthy patterns:**

- Clear layering: lower layers have no upward dependencies
- Minimal cross-cutting: utilities used everywhere but depending on nothing
- Isolated complexity: high-coupling types contained in specific namespaces

**Warning signs:**

- Bidirectional dependencies between namespaces
- Utility namespaces depending on domain namespaces
- Large clusters of mutual dependencies (the "big ball of mud")

## Direct Access

If the embedded report fails to load (iframe security restrictions vary by browser):

**Local development:** Open `ndepend/NDependOut/NDependReport.html` directly in a browser.

**CI artifacts:** The report generates during each NDepend analysis run and uploads to the build artifacts.

## Regenerating the Report

```powershell
# From repository root
pwsh ndepend/ndepend.ps1
```

This script:

1. Builds the solution in Release configuration
2. Runs NDepend analysis against the compiled assemblies
3. Generates the HTML report at `ndepend/NDependOut/NDependReport.html`
4. Updates quality gate badges in `ndepend/badges/`

**Prerequisites:** NDepend license (set `NDEPEND_LICENSE` environment variable). Without a license, the script runs but produces warnings instead of full analysis.

## Historical Trends

The report includes trend charts showing metric evolution over time. These charts answer questions like:

- Is technical debt accumulating or being paid down?
- Is complexity increasing faster than test coverage?
- Are dependency cycles appearing in new code?

Trend inflection points often correlate with specific commits. The CQLinq query engine allows drilling into which changes caused metric shifts.

## References

- NDepend Documentation: [ndepend.com/docs](https://www.ndepend.com/docs/)
- CQLinq Query Language: [ndepend.com/docs/cqlinq-syntax](https://www.ndepend.com/docs/cqlinq-syntax)