# QuanTAlib AI Coding Agent Instructions

## Project Overview
QuanTAlib is a high-performance C# library for quantitative technical analysis targeting .NET 8.0. Provides 50+ technical indicators optimized for sub-millisecond real-time streaming calculations using circular buffers, SIMD operations, and event-driven architecture. Used in production live trading environments.

## Critical Architecture Patterns

### Core Data Flow
All indicators inherit from `AbstractBase` (`lib/core/abstractBase.cs`) implementing `ITValue`:
```csharp
// Standard indicator lifecycle:
TValue/TBar Input → Calc() → ManageState(isNew) → Calculation() → Process() → Pub event
```

**Critical concept**: The `isNew` parameter differentiates:
- `isNew=true`: New bar/candle arrives → increment `_index`, backup all state variables
- `isNew=false`: Update to current bar → restore backed-up state, recalculate with new value

This dual-mode processing is **essential** for real-time trading where the current bar updates continuously before the next bar starts. Every indicator must handle both modes correctly - validated extensively in `Tests/test_updates_*.cs`.

### Circular Buffer Pattern
`CircularBuffer` (`lib/core/circularbuffer.cs`) provides memory-efficient fixed-capacity storage:
- Never grows beyond initial capacity (fixed memory footprint regardless of data volume)
- O(1) add/access operations with wraparound
- SIMD-optimized aggregations (Sum, Min, Max, Average) using `System.Numerics.Vector`
- **Critical**: Always use `Add(item, isNew)` - the `isNew` flag controls append vs update behavior

### State Management in Indicators
Every indicator **must** implement this pattern to support bar updates:
```csharp
protected override void ManageState(bool isNew)
{
    if (isNew) {
        _index++;
        _p_prevValue = _prevValue;      // Backup state
        _p_lastEma = _lastEma;           // Backup all stateful variables
    } else {
        _prevValue = _p_prevValue;       // Restore state
        _lastEma = _p_lastEma;           // Restore all stateful variables
    }
}
```
**Pattern**: Use `_p_` prefix for backup variables (e.g., `_p_lastEma`, `_p_isInit`, `_p_e`). When `isNew=false`, restore ALL stateful variables before recalculating. See `lib/trends/Ema.cs` for reference implementation.

## Development Workflow

### MCP-Orchestrated Process
**Research Gate**: Before implementing non-trivial indicators, use Context7 MCP to retrieve authoritative formulas/references. Embed citation tags in PR descriptions.

**Decomposition**: Use Sequential-Thinking MCP for complex multi-stage work (SIMD refactors, multi-timeframe logic, performance optimization epics).

**Task Tracking**: Taskmaster MCP holds the canonical task graph. Feature branches follow pattern: `feature/{taskId}-{slug}`. Tasks include: feature, performance, documentation with status transitions (not-started → in-progress → done).

**Quality Gates**:
1. Formula citation required for non-trivial indicators (Context7 tag in PR description)
2. Benchmark data required for performance-related changes
3. Taskmaster task IDs must be referenced in PR body with closing keywords
4. Update `memory-bank/progress.md` after merge when threshold met (≥5 feature tasks or perf epic completes)

### Build & Test Commands
```powershell
# Build solution (or use VS Code Task: "build")
dotnet build QuanTAlib.sln

# Run all tests (or use VS Code Task: "test")
dotnet test --no-build --verbosity:normal

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./lcov.info --no-build

# Clean build artifacts
dotnet clean QuanTAlib.sln
```

**VS Code Tasks**: Use Run Task menu for `build`, `test`, `test with coverage`, `clean` - configured in `.vscode/tasks.json`.

### Adding a New Indicator

1. **Research**: Get formula/specification. For non-trivial indicators, use Context7 to retrieve authoritative references.

2. **Location**: Place in appropriate `lib/` subdirectory:
   - `trends/` - Trend indicators (SMA, EMA, JMA, etc.)
   - `oscillators/` - RSI, Stochastic, CCI, etc.
   - `momentum/` - MACD, ADX, ROC, etc.
   - `volatility/` - ATR, Bollinger Bands, volatility measures
   - `volume/` - Volume-based indicators
   - `statistics/` - Statistical measures, correlations

3. **Template structure**:
```csharp
using System.Runtime.CompilerServices;
namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MyIndicator : AbstractBase
{
    private readonly int _period;
    private CircularBuffer _buffer;
    private double _prevValue, _p_prevValue;  // State + backup with _p_ prefix
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MyIndicator(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        _period = period;
        _buffer = new(period);
        WarmupPeriod = period;  // Set when indicator stabilizes (95% accuracy)
        Name = $"MyIndicator({period})";
        Init();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevValue = 0;
        _buffer = new(_period);
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
        double result = _buffer.Average();  // Example using SIMD-optimized operation
        _prevValue = result;
        
        IsHot = _index >= WarmupPeriod;  // Mark when indicator reaches accuracy threshold
        return result;
    }
}
```

4. **Testing**: Create update test in appropriate `Tests/test_updates_*.cs` file:
```csharp
[Fact]
public void MyIndicator_Update()
{
    var indicator = new MyIndicator(period: 14);
    double initialValue = indicator.Calc(new TValue(DateTime.Now, 100.0, IsNew: true));
    
    // Apply 100 random updates with isNew=false
    for (int i = 0; i < 100; i++)
    {
        indicator.Calc(new TValue(DateTime.Now, GetRandomDouble(), IsNew: false));
    }
    
    // Final value with same input should equal initial value
    double finalValue = indicator.Calc(new TValue(DateTime.Now, 100.0, IsNew: false));
    Assert.Equal(initialValue, finalValue, precision: 8);
}
```

5. **Validation**: Compare against reference implementations (TALib, Trady, Skender) in appropriate test file.

### Quantower Integration
For platform indicators in `quantower/`, create wrapper classes inheriting from Quantower's `Indicator`:
```csharp
public class MyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;
    
    private QuanTAlib.MyIndicator? ma;
    protected LineSeries? Series;
    
    protected override void OnInit()
    {
        ma = new QuanTAlib.MyIndicator(period: Period);
        base.OnInit();
    }
    
    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        TValue result = ma!.Calc(input);
        Series!.SetValue(result.Value);
    }
}
```
- Use private `lib/` indicator instances
- Map `OnUpdate()` to indicator's `Calc()` method  
- Extract output from indicator state/properties
- Apply `IndicatorExtensions` for styling and painting

## Code Style Requirements

### Performance First
- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for all public methods and hot paths
- Use `[MethodImpl(MethodImplOptions.AggressiveOptimization)]` for `Calculation()` methods
- Apply `[SkipLocalsInit]` to indicator classes to skip zero-initialization
- Prefer SIMD operations in `CircularBuffer` for aggregations (Sum, Min, Max, Average)
- Minimize allocations in `Calculation()` methods - reuse buffers and avoid LINQ
- Use `sealed` classes when possible for devirtualization

### C# Conventions
- **No inline comments** within methods - code should be self-documenting through clear naming
- Use XML doc comments for public classes/methods only - include purpose, formula description, and source citations
- PascalCase for public members, `_camelCase` for private fields
- `_p_` prefix for backup state variables used in `ManageState()`
- Compact code - minimal whitespace between logical blocks
- Latest C# features: `ArgumentOutOfRangeException.ThrowIfLessThan`, pattern matching, collection expressions, etc.
- No namespace imports in individual files - `Directory.Build.props` enables implicit usings

### Project Settings (Directory.Build.props)
- `LangVersion: preview` - use cutting-edge C# features
- `AllowUnsafeBlocks: true` - SIMD and unsafe operations permitted
- `Nullable: enable` - strict nullability checking enforced
- Target: `net8.0`
- `DisableImplicitNamespaceImports: true` - explicit namespace control
- Release optimizations: AOT, ReadyToRun, TieredCompilation, trimming enabled

## Key Files & Directories

### Core Library Structure
```
lib/
├── core/              # AbstractBase, CircularBuffer, TSeries, TBar, TValue, ITValue
├── trends/            # Trend indicators: SMA, EMA, DEMA, TEMA, JMA, KAMA, etc. (25+ indicators)
├── oscillators/       # RSI, Stochastic, Williams %R, CCI, Fisher, CTI, etc.
├── momentum/          # MACD, ADX, DMI, ROC, TRIX, Vortex, PMO, etc.
├── volatility/        # ATR, Bollinger Bands, Keltner Channels, volatility measures
├── volume/            # Volume-based indicators (OBV, MFI, etc.)
├── statistics/        # Statistical measures, correlations
└── errors/            # Error metrics: MAE, MSE, RMSE, MAPE, R-squared, etc.
```

### Critical Reference Files
- `lib/core/abstractBase.cs` - Base class for all indicators with lifecycle management
- `lib/core/circularbuffer.cs` - Memory-efficient storage with SIMD operations
- `lib/core/TValue.cs` - Immutable record struct for time-value pairs with IsNew/IsHot flags
- `lib/core/TBar.cs` - OHLCV bar data structure
- `Directory.Build.props` - Solution-wide MSBuild properties and optimizations
- `memory-bank/systemPatterns.md` - Architecture patterns and design decisions
- `memory-bank/activeContext.md` - Current work focus, MCP policies, and operational rules
- `memory-bank/progress.md` - Completed features, roadmap, and version history

### Testing Reference
- `Tests/test_updates_*.cs` - Update behavior validation (IsNew handling) - **CRITICAL TESTS**
- `Tests/test_quantower.cs` - Quantower integration validation
- `Tests/test_talib.cs` - Cross-validation against TA-Lib reference library
- `Tests/test_Trady.cs` - Cross-validation against Trady reference library
- `Tests/test_skender.stock.cs` - Cross-validation against Skender.Stock.Indicators

## Common Patterns

### Multi-Stage Smoothing
Many indicators (DEMA, TEMA, MACD) use cascaded smoothing with child indicator instances:
```csharp
private readonly Ema _ema1;
private readonly Ema _ema2;

public MyIndicator(int period)
{
    _ema1 = new Ema(period);
    _ema2 = new Ema(period);
}

protected override double Calculation()
{
    _ema1.Calc(Input.Value, Input.IsNew);
    _ema2.Calc(_ema1.Value, Input.IsNew);  // Feed output of first into second
    return _ema2.Value;
}
```

### Bar-Based vs Value-Based Indicators
- **Value-based**: Accept `TValue`, process single values (most indicators like SMA, EMA, RSI)
- **Bar-based**: Accept `TBar` (OHLCV), process bar data (ATR, Stochastic, volume indicators)

Override appropriate `Calc()` method:
```csharp
// For bar-based indicators
public override TValue Calc(TBar barInput)
{
    BarInput = barInput;
    return Process(barInput.Close, barInput.Time, barInput.IsNew);
}
```

### WarmupPeriod Calculation
Set `WarmupPeriod` to indicate when the indicator reaches 95% accuracy (used for IsHot flag):
```csharp
// For exponential smoothing with constant alpha/k
WarmupPeriod = (int)Math.Ceiling(Math.Log(0.05) / Math.Log(1 - k));

// For simple period-based indicators
WarmupPeriod = period;

// For multi-stage indicators
WarmupPeriod = stage1.WarmupPeriod + stage2.WarmupPeriod;
```

### Event-Driven Updates
Indicators support pub-sub pattern through `Pub` event:
```csharp
// Publishing side (automatic in AbstractBase.Process())
Pub?.Invoke(this, new ValueEventArgs(value));

// Subscribing side
var ema = new Ema(20);
ema.Pub += (sender, args) => Console.WriteLine($"New EMA value: {args.Tick.Value}");

// Or subscribe one indicator to another
var sma = new Sma(10);
var ema = new Ema(sma, period: 20);  // EMA automatically subscribes to SMA's Pub event
```

## Validation Strategy

1. **Update tests** (CRITICAL): Verify `isNew=false` behavior converges to `isNew=true` with same final value after 100 random updates. This validates state management correctness. See `Tests/test_updates_*.cs`.

2. **Reference comparison**: Validate against TALib, Trady, or Skender implementations. Expect high precision match (typically 8+ decimal places).

3. **Edge cases**: Test with:
   - Insufficient data (count < period)
   - NaN and Infinity inputs (should propagate last valid value)
   - Extreme values (very large/small numbers)
   - Zero and negative values where applicable

4. **Performance**: Benchmark calculation time - target < 0.5ms per update. Use `BenchmarkDotNet` for precise measurements.

## Documentation Requirements

- XML doc comments on public classes/methods describing:
  - Purpose and use case
  - Formula/algorithm description
  - Source citations (URLs to papers, documentation, books)
  - Parameter constraints and validation
- Mathematical formulas in doc comments with proper notation
- No internal code comments - let code structure communicate intent through clear naming
- Update `memory-bank/progress.md` after significant feature completion (threshold: ≥5 feature tasks merged)

## GitVersion & Releases

- Semantic versioning via `GitVersion.yml`
- Version properties auto-injected: `$(GitVersion_MajorMinorPatch)`, `$(GitVersion_AssemblySemVer)`
- Commit messages influence version bumps using conventional commits:
  - `+semver: major` or `+semver: breaking` → major bump
  - `+semver: minor` or `+semver: feature` → minor bump  
  - `+semver: patch` or `+semver: fix` → patch bump
  - `+semver: none` or `+semver: skip` → no bump
- `main` branch: ContinuousDeployment mode, patch increment
- `dev` branch: ContinuousDelivery mode, pre-release weight 30000
- Build creates NuGet package with embedded version metadata and source link
