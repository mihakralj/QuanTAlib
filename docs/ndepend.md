# NDepend Report

> **Architecture & Code Quality Analysis**

The NDepend report provides comprehensive analysis of the QuanTAlib codebase, including metrics, dependencies, quality gates, and architectural insights.

<iframe src="../ndepend/NDependOut/NDependReport.html" 
        style="width: 100%; height: calc(100vh - 100px); border: 1px solid #ccc; border-radius: 4px;"
        title="NDepend Report"
        frameborder="0"
        allowfullscreen>
</iframe>

---

## About NDepend Analysis

NDepend provides:

- **Code Metrics**: Lines of code, cyclomatic complexity, coupling metrics
- **Quality Gates**: Automated code quality validation
- **Dependency Analysis**: Visual dependency graphs and matrices
- **Technical Debt**: Estimation of remediation effort
- **Trend Charts**: Historical code quality tracking
- **CQLinq Queries**: Custom code analysis queries

## Direct Access

If the embedded report doesn't load properly, you can access it directly:

**Local Development**: `file:///c:/github/quantalib/ndepend/NDependOut/NDependReport.html`

**After Build**: The report is regenerated with each NDepend analysis run.

## Running NDepend Analysis

```powershell
# From the project root
dotnet build
# NDepend analysis runs automatically as part of the build
```

The report will be updated at `ndepend/NDependOut/NDependReport.html`.
