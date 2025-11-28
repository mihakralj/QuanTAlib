# QuanTAlib AI Coding Agent Instructions

## Project Overview
QuanTAlib is a high-performance C# library for quantitative technical analysis, targeting .NET 8.0 with real-time streaming data processing. The library provides 50+ technical indicators optimized for sub-millisecond calculations using circular buffers, SIMD operations, and event-driven architecture.

## Critical Architecture Patterns

### Core Data Flow
All indicators inherit from `AbstractBase` (in `lib/core/abstractBase.cs`) which implements `ITValue`:
```csharp
// Standard indicator lifecycle:
Input → Calc() → ManageState(isNew) → Calculation() → Process() → Pub event
```

**Key insight**: The `isNew` parameter distinguishes between new bars and updates to the last bar. Indicators must support both modes - this is tested extensively in `Tests/test_updates_*.cs`.

### Circular Buffer Pattern
`CircularBuffer` (in `lib/core/circularbuffer.cs`) is the foundation for memory-efficient fixed-capacity storage:
- Never grows beyond initial capacity
- O(1) add/access operations
- SIMD-optimized aggregations (Sum, Min, Max, Average)
- **Critical**: Always use `Add(item, isNew)` - the `isNew` flag controls whether to append or update

### State Management in Indicators
Every indicator must implement:
```csharp
protected override void ManageState(bool isNew)
{
    if (isNew) {
        _index++;
        _p_prevValue = _prevValue;  // Backup state
    } else {
        _prevValue = _p_prevValue;  // Restore state
    }
}
```
This allows bar updates without corrupting historical calculations.

## Development Workflow

### MCP-Orchestrated Process
**Research Gate**: Before implementing non-trivial indicators, use Context7 to retrieve authoritative formulas/references. Embed citation tags in PR descriptions.

**Decomposition**: Use Sequential-Thinking for complex multi-stage work (SIMD refactors, multi-timeframe logic).

**Task Tracking**: Taskmaster holds the canonical task graph. Feature branches follow pattern: `feature/{taskId}-{slug}`.

**Quality Gates**:
1. Formula citation required for non-trivial indicators (Context7 tag)
2. Benchmark data required for performance-related changes
3. Taskmaster task IDs must be referenced in PRs
4. Update `memory-bank/progress.md` after merge when threshold met

### Build & Test Commands
```powershell
# Build solution
dotnet build QuanTAlib.sln

# Run all tests
dotnet test --no-build

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

# Build using tasks.json
# Use Run Task: "build" or "test"
```

### Adding a New Indicator
1. **Research**: Get formula/specification (Context7 if needed)
2. **Location**: Place in appropriate `lib/` subdirectory (averages, oscillators, momentum, volatility, volume, statistics)
3. **Template structure**:
```csharp
using System.Runtime.CompilerServices;
namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MyIndicator : AbstractBase
{
    private CircularBuffer _buffer;
    private double _prevValue, _p_prevValue;  // State + backup
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MyIndicator(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _buffer = new(period);
        WarmupPeriod = period;  // Set when indicator stabilizes
        Name = $"MyIndicator({period})";
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew) {
            _index++;
            _p_prevValue = _prevValue;
        } else {
            _prevValue = _p_prevValue;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);
        // Implement calculation logic
        return result;
    }
}
```

4. **Testing**: Create update test in `Tests/test_updates_*.cs`:
```csharp
[Fact]
public void MyIndicator_Update()
{
    var indicator = new MyIndicator(period: 14);
    TestTValueUpdate(indicator, indicator.Calc);
}
```

### Quantower Integration
For platform indicators in `quantower/`, create wrapper classes inheriting from Quantower's `Indicator`:
- Use private `lib/` indicator instances
- Map `OnUpdate()` to indicator's `Calc()` method
- Extract output fields (e.g., `ma`, `jmaUp`, `jmaLo`) from indicator state

## Code Style Requirements

### Performance First
- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for hot paths
- Use `[MethodImpl(MethodImplOptions.AggressiveOptimization)]` for calculation methods
- Apply `[SkipLocalsInit]` to indicator classes
- Prefer SIMD operations in `CircularBuffer` for aggregations
- Minimize allocations in `Calculation()` methods

### C# Conventions
- **No inline comments** within methods - code should be self-documenting
- Use XML doc comments for public APIs only
- PascalCase for public members, _camelCase for private fields
- Compact code - minimal whitespace between logical blocks
- Latest C# features: `ArgumentOutOfRangeException.ThrowIfLessThan`, pattern matching, etc.

### Project Settings
- `LangVersion: preview` - use cutting-edge C# features
- `AllowUnsafeBlocks: true` - SIMD and unsafe operations permitted
- `Nullable: enable` - strict nullability checking
- Target: `net8.0`

## Key Files & Directories

### Core Library Structure
```
lib/
├── core/           # AbstractBase, CircularBuffer, TSeries, TBar, TValue
├── averages/       # Moving averages (SMA, EMA, DEMA, TEMA, JMA, etc.)
├── oscillators/    # RSI, Stochastic, Williams %R, CCI, Fisher
├── momentum/       # MACD, ADX, ROC, Vortex
├── volatility/     # ATR, Bollinger Bands, volatility measures
├── volume/         # Volume-based indicators
└── statistics/     # Statistical measures, correlations
```

### Critical Reference Files
- `lib/core/abstractBase.cs` - Base class for all indicators
- `lib/core/circularbuffer.cs` - Memory-efficient storage with SIMD
- `Directory.Build.props` - Solution-wide MSBuild properties
- `memory-bank/systemPatterns.md` - Architecture patterns
- `memory-bank/activeContext.md` - Current work focus and MCP policies
- `memory-bank/progress.md` - Completed features and roadmap

### Testing Reference
- `Tests/test_updates_*.cs` - Update behavior validation (IsNew handling)
- `Tests/test_quantower.cs` - Quantower integration validation
- `Tests/test_talib.cs`, `test_Trady.cs` - Cross-validation against reference libraries

## Common Patterns

### Multi-Stage Smoothing
Many indicators (DEMA, TEMA, MACD) use cascaded smoothing:
```csharp
private readonly Ema _ema1;
private readonly Ema _ema2;

_ema1.Calc(Input.Value, Input.IsNew);
_ema2.Calc(_ema1.Value, Input.IsNew);
```

### Bar-Based vs Value-Based
- **Value-based**: Accept `TValue`, process single values (most indicators)
- **Bar-based**: Accept `TBar` (OHLCV), process bar data (ATR, Stochastic, volume indicators)

Override appropriate `Calc()` method:
```csharp
public override TValue Calc(TBar barInput) { /* ... */ }
```

### WarmupPeriod Calculation
Set `WarmupPeriod` to indicate when the indicator reaches 95% accuracy:
```csharp
WarmupPeriod = (int)Math.Ceiling(Math.Log(0.05) / Math.Log(1 - alpha));
```

## Validation Strategy
1. **Update tests**: Verify `isNew=false` behavior converges to `isNew=true` with same final value
2. **Reference comparison**: Validate against TALib, Trady, or Skender implementations
3. **Edge cases**: Test with insufficient data (< period), NaN/Infinity, extreme values
4. **Performance**: Benchmark calculation time - target < 0.5ms per update

## Documentation Requirements
- XML docs on public classes/methods describing purpose, formula, and sources
- Mathematical formulas in doc comments with source citations
- No internal comments - let code structure communicate intent
- Update `memory-bank/progress.md` after significant feature completion

## GitVersion & Releases
- Semantic versioning via GitVersion.yml
- Version properties auto-injected: `$(GitVersion_MajorMinorPatch)`
- Commit messages influence version bumps (conventional commits)
- Build creates NuGet package with embedded version metadata
