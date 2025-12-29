# QuanTAlib Indicator Test Protocol

> **Comprehensive Testing Requirements for All Indicators**

This document defines the **mandatory** and **recommended** tests that every indicator in QuanTAlib must implement. Adherence to this protocol ensures correctness, consistency, robustness, and maintainability across the entire library.

## File Structure

Every indicator requires the following test files:

| File                         | Purpose                                     | Mandatory |
|------------------------------|---------------------------------------------|-----------|
| `[Name].Tests.cs`            | Unit tests for core functionality           | ✅ Yes    |
| `[Name].Validation.Tests.cs` | Cross-validation against external libraries | ✅ Yes    |
| `[Name].Quantower.Tests.cs`  | Quantower adapter integration tests         | ✅ Yes    |

## 1. Unit Tests (`[Name].Tests.cs`)

Unit tests verify the internal logic, state management, API contracts, and edge case handling of the indicator.

### 1.1 Constructor & Parameter Validation

Every indicator must validate its constructor parameters.

#### Required Tests

| Test Name                           | Description                                                     | Priority    |
|-------------------------------------|-----------------------------------------------------------------|-------------|
| `Constructor_ValidatesInput`        | Verify invalid primary parameters throw `ArgumentException`     | 🔴 Critical |
| `Constructor_ValidatesOptionalArgs` | Verify invalid optional parameters throw appropriate exceptions | 🟡 Required |
| `Constructor_ValidBoundaryValues`   | Verify minimum valid values are accepted                        | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void Constructor_ValidatesInput()
{
    // Period-based indicators
    Assert.Throws<ArgumentException>(() => new Sma(0));
    Assert.Throws<ArgumentException>(() => new Sma(-1));
    
    // Valid construction
    var sma = new Sma(10);
    Assert.NotNull(sma);
}

[Fact]
public void Constructor_ValidatesOptionalArgs()
{
    // For EMA with alpha parameter
    Assert.Throws<ArgumentException>(() => new Ema(0.0));   // alpha must be > 0
    Assert.Throws<ArgumentException>(() => new Ema(-0.1));  // alpha must be positive
    Assert.Throws<ArgumentException>(() => new Ema(1.1));   // alpha must be <= 1
    
    var ema = new Ema(0.5);
    Assert.NotNull(ema);
}

[Fact]
public void Constructor_ValidatesRelatedParameters()
{
    // For KAMA with fast/slow periods
    Assert.Throws<ArgumentException>(() => new Kama(10, fastPeriod: 10, slowPeriod: 5)); // fast >= slow
    Assert.Throws<ArgumentException>(() => new Kama(10, fastPeriod: 0));
    Assert.Throws<ArgumentException>(() => new Kama(10, slowPeriod: 0));
}
```

### 1.2 Basic Functionality

#### Required Tests

| Test Name                    | Description                                               | Priority     |
|------------------------------|-----------------------------------------------------------|--------------|
| `Calc_ReturnsValue`          | Verify `Update` returns valid `TValue` and updates `Last` | 🔴 Critical  |
| `FirstValue_ReturnsExpected` | Verify first output value is correct (often equals input) | 🟡 Required  |
| `Properties_Accessible`      | Verify `Last`, `IsHot`, `Name` are accessible             | 🟡 Required  |
| `CalculatesCorrectValue`     | Verify calculation against known mathematical result      | 🔴 Critical  |

#### Implementation Pattern

```csharp
[Fact]
public void Calc_ReturnsValue()
{
    var sma = new Sma(10);
    
    Assert.Equal(0, sma.Last.Value);  // Initial value
    
    TValue result = sma.Update(new TValue(DateTime.UtcNow, 100));
    
    Assert.True(result.Value > 0);
    Assert.Equal(result.Value, sma.Last.Value);
}

[Fact]
public void FirstValue_ReturnsItself()
{
    var sma = new Sma(10);
    TValue result = sma.Update(new TValue(DateTime.UtcNow, 100));
    Assert.Equal(100.0, result.Value, 1e-10);
}

[Fact]
public void Properties_Accessible()
{
    var sma = new Sma(10);
    
    Assert.Equal(0, sma.Last.Value);
    Assert.False(sma.IsHot);
    Assert.Contains("Sma", sma.Name);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    Assert.NotEqual(0, sma.Last.Value);
}

[Fact]
public void CalculatesCorrectAverage()
{
    var sma = new Sma(5);
    
    sma.Update(new TValue(DateTime.UtcNow, 10));
    sma.Update(new TValue(DateTime.UtcNow, 20));
    sma.Update(new TValue(DateTime.UtcNow, 30));
    sma.Update(new TValue(DateTime.UtcNow, 40));
    sma.Update(new TValue(DateTime.UtcNow, 50));
    
    // SMA(5) of 10,20,30,40,50 = 150/5 = 30
    Assert.Equal(30.0, sma.Last.Value, 1e-10);
}
```

### 1.3 State Management & Bar Correction

Bar correction is critical for real-time trading applications where the current bar updates continuously.

#### Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `Calc_IsNew_AcceptsParameter` | Verify `isNew: true` advances state | 🔴 Critical |
| `Calc_IsNew_False_UpdatesValue` | Verify `isNew: false` updates without advancing | 🔴 Critical |
| `IterativeCorrections_RestoreToOriginalState` | Verify state restoration after corrections | 🔴 Critical |
| `Reset_ClearsState` | Verify `Reset()` restores to initial state | 🔴 Critical |
| `Reset_ClearsLastValidValue` | Verify NaN tracking is also reset | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void Calc_IsNew_AcceptsParameter()
{
    var sma = new Sma(10);
    
    sma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
    double value1 = sma.Last.Value;
    
    sma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
    double value2 = sma.Last.Value;
    
    Assert.NotEqual(value1, value2);
}

[Fact]
public void Calc_IsNew_False_UpdatesValue()
{
    var sma = new Sma(10);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    sma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
    double beforeUpdate = sma.Last.Value;
    
    sma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
    double afterUpdate = sma.Last.Value;
    
    Assert.NotEqual(beforeUpdate, afterUpdate);
}

[Fact]
public void IterativeCorrections_RestoreToOriginalState()
{
    var sma = new Sma(5);
    var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
    
    // Feed 10 new values
    TValue tenthInput = default;
    for (int i = 0; i < 10; i++)
    {
        var bar = gbm.Next(isNew: true);
        tenthInput = new TValue(bar.Time, bar.Close);
        sma.Update(tenthInput, isNew: true);
    }
    
    // Remember state after 10 values
    double stateAfterTen = sma.Last.Value;
    
    // Generate 9 corrections with isNew=false (different values)
    for (int i = 0; i < 9; i++)
    {
        var bar = gbm.Next(isNew: false);
        sma.Update(new TValue(bar.Time, bar.Close), isNew: false);
    }
    
    // Feed the remembered 10th input again with isNew=false
    TValue finalResult = sma.Update(tenthInput, isNew: false);
    
    // State should match the original state after 10 values
    Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
}

[Fact]
public void Reset_ClearsState()
{
    var sma = new Sma(10);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    sma.Update(new TValue(DateTime.UtcNow, 105));
    double valueBefore = sma.Last.Value;
    
    sma.Reset();
    
    Assert.Equal(0, sma.Last.Value);
    Assert.False(sma.IsHot);
    
    // After reset, should accept new values
    sma.Update(new TValue(DateTime.UtcNow, 50));
    Assert.NotEqual(0, sma.Last.Value);
    Assert.NotEqual(valueBefore, sma.Last.Value);
}

[Fact]
public void Reset_ClearsLastValidValue()
{
    var sma = new Sma(5);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    sma.Update(new TValue(DateTime.UtcNow, double.NaN));
    
    sma.Reset();
    
    // After reset, first valid value should establish new baseline
    var result = sma.Update(new TValue(DateTime.UtcNow, 50));
    Assert.Equal(50.0, result.Value, 1e-10);
}
```

### 1.4 Warmup & Convergence

#### Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `IsHot_BecomesTrueWhenBufferFull` | Verify warmup completion | 🔴 Critical |
| `IsHot_IsPeriodDependent` | Verify warmup scales with period | 🟡 Required |
| `WarmupPeriod_IsSetCorrectly` | Verify `WarmupPeriod` property | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void IsHot_BecomesTrueWhenBufferFull()
{
    var sma = new Sma(5);
    
    Assert.False(sma.IsHot);
    
    for (int i = 1; i <= 4; i++)
    {
        sma.Update(new TValue(DateTime.UtcNow, i * 10));
        Assert.False(sma.IsHot);
    }
    
    sma.Update(new TValue(DateTime.UtcNow, 50));
    Assert.True(sma.IsHot);
}

[Fact]
public void IsHot_IsPeriodDependent()
{
    // For exponential indicators like EMA
    int[] periods = [10, 20, 50, 100];
    int[] expectedSteps = new int[periods.Length];
    
    for (int i = 0; i < periods.Length; i++)
    {
        int period = periods[i];
        var ema = new Ema(period);
        
        int steps = 0;
        while (!ema.IsHot && steps < 500)
        {
            ema.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }
        expectedSteps[i] = steps;
    }
    
    // Verify warmup times increase with period
    Assert.True(expectedSteps[0] < expectedSteps[1]);
    Assert.True(expectedSteps[1] < expectedSteps[2]);
    Assert.True(expectedSteps[2] < expectedSteps[3]);
}

[Fact]
public void WarmupPeriod_IsSetCorrectly()
{
    var sma = new Sma(10);
    Assert.Equal(10, sma.WarmupPeriod);
}
```

### 1.5 Robustness (NaN/Infinity Handling)

All indicators must handle invalid inputs gracefully without crashing or propagating invalid values.

#### Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `NaN_Input_UsesLastValidValue` | Verify NaN substitution | 🔴 Critical |
| `Infinity_Input_UsesLastValidValue` | Verify Infinity handling | 🔴 Critical |
| `MultipleNaN_ContinuesWithLastValid` | Verify consecutive NaN handling | 🟡 Required |
| `BatchCalc_HandlesNaN` | Verify batch NaN handling | 🟡 Required |
| `AllNaN_ReturnsNaN` | Verify behavior with all-NaN input | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void NaN_Input_UsesLastValidValue()
{
    var sma = new Sma(5);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    sma.Update(new TValue(DateTime.UtcNow, 110));
    
    var resultAfterNaN = sma.Update(new TValue(DateTime.UtcNow, double.NaN));
    
    Assert.True(double.IsFinite(resultAfterNaN.Value));
    Assert.NotEqual(0, resultAfterNaN.Value);
}

[Fact]
public void Infinity_Input_UsesLastValidValue()
{
    var sma = new Sma(5);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    sma.Update(new TValue(DateTime.UtcNow, 110));
    
    var resultAfterPosInf = sma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
    Assert.True(double.IsFinite(resultAfterPosInf.Value));
    
    var resultAfterNegInf = sma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
    Assert.True(double.IsFinite(resultAfterNegInf.Value));
}

[Fact]
public void MultipleNaN_ContinuesWithLastValid()
{
    var sma = new Sma(5);
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    sma.Update(new TValue(DateTime.UtcNow, 110));
    sma.Update(new TValue(DateTime.UtcNow, 120));
    
    var r1 = sma.Update(new TValue(DateTime.UtcNow, double.NaN));
    var r2 = sma.Update(new TValue(DateTime.UtcNow, double.NaN));
    var r3 = sma.Update(new TValue(DateTime.UtcNow, double.NaN));
    
    Assert.True(double.IsFinite(r1.Value));
    Assert.True(double.IsFinite(r2.Value));
    Assert.True(double.IsFinite(r3.Value));
}

[Fact]
public void BatchCalc_HandlesNaN()
{
    var sma = new Sma(5);
    
    var series = new TSeries();
    series.Add(DateTime.UtcNow.Ticks, 100);
    series.Add(DateTime.UtcNow.Ticks + 1, 110);
    series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
    series.Add(DateTime.UtcNow.Ticks + 3, 120);
    series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
    series.Add(DateTime.UtcNow.Ticks + 5, 130);
    
    var results = sma.Update(series);
    
    foreach (var result in results)
    {
        Assert.True(double.IsFinite(result.Value), 
            $"Expected finite value but got {result.Value}");
    }
}
```

### 1.6 Consistency Tests

These tests ensure all API modes produce identical results.

#### Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `BatchCalc_MatchesIterativeCalc` | Verify TSeries batch matches streaming | 🔴 Critical |
| `AllModes_ProduceSameResult` | **Critical**: All 4 modes must match | 🔴 Critical |
| `StaticBatch_Works` | Verify static `Batch` method | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void BatchCalc_MatchesIterativeCalc()
{
    var smaIterative = new Sma(10);
    var smaBatch = new Sma(10);
    var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
    
    var series = new TSeries();
    for (int i = 0; i < 100; i++)
    {
        var bar = gbm.Next(isNew: true);
        series.Add(bar.Time, bar.Close);
    }
    
    // Calculate iteratively
    var iterativeResults = new TSeries();
    foreach (var item in series)
    {
        iterativeResults.Add(smaIterative.Update(item));
    }
    
    // Calculate batch
    var batchResults = smaBatch.Update(series);
    
    // Compare
    Assert.Equal(iterativeResults.Count, batchResults.Count);
    for (int i = 0; i < iterativeResults.Count; i++)
    {
        Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
    }
}

[Fact]
public void AllModes_ProduceSameResult()
{
    // Arrange
    int period = 10;
    var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    var series = bars.Close;
    
    // 1. Batch Mode (static method)
    var batchSeries = Sma.Batch(series, period);
    double expected = batchSeries.Last.Value;
    
    // 2. Span Mode (static method with spans)
    var tValues = series.Values.ToArray();
    var spanInput = new ReadOnlySpan<double>(tValues);
    var spanOutput = new double[tValues.Length];
    Sma.Batch(spanInput, spanOutput, period);
    double spanResult = spanOutput[^1];
    
    // 3. Streaming Mode (instance, one value at a time)
    var streamingInd = new Sma(period);
    for (int i = 0; i < series.Count; i++)
    {
        streamingInd.Update(series[i]);
    }
    double streamingResult = streamingInd.Last.Value;
    
    // 4. Eventing Mode (chained via ITValuePublisher)
    var pubSource = new TSeries();
    var eventingInd = new Sma(pubSource, period);
    for (int i = 0; i < series.Count; i++)
    {
        pubSource.Add(series[i]);
    }
    double eventingResult = eventingInd.Last.Value;
    
    // Assert all modes produce identical results
    Assert.Equal(expected, spanResult, precision: 9);
    Assert.Equal(expected, streamingResult, precision: 9);
    Assert.Equal(expected, eventingResult, precision: 9);
}

[Fact]
public void StaticBatch_Works()
{
    var series = new TSeries();
    series.Add(DateTime.UtcNow.Ticks, 10);
    series.Add(DateTime.UtcNow.Ticks + 1, 20);
    series.Add(DateTime.UtcNow.Ticks + 2, 30);
    series.Add(DateTime.UtcNow.Ticks + 3, 40);
    series.Add(DateTime.UtcNow.Ticks + 4, 50);
    
    var results = Sma.Batch(series, 3);
    
    Assert.Equal(5, results.Count);
    Assert.Equal(40.0, results.Last.Value, 1e-10);
}
```

### 1.7 Span API Tests (High Performance)

#### Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `SpanBatch_ValidatesInput` | Verify buffer length validation | 🔴 Critical |
| `SpanBatch_MatchesTSeriesBatch` | Verify Span matches TSeries output | 🔴 Critical |
| `SpanBatch_CalculatesCorrectly` | Verify correct calculation with spans | 🟡 Required |
| `SpanBatch_ZeroAllocation` | Verify no stack overflow on large data | 🟡 Required |
| `SpanBatch_HandlesNaN` | Verify NaN handling in span mode | 🟡 Required |
| `SpanBatch_Period1_ReturnsInput` | Verify edge case period=1 | 🟢 Recommended |

#### Implementation Pattern

```csharp
[Fact]
public void SpanBatch_ValidatesInput()
{
    double[] source = [1, 2, 3, 4, 5];
    double[] output = new double[5];
    double[] wrongSizeOutput = new double[3];
    
    // Period must be > 0
    Assert.Throws<ArgumentException>(() => 
        Sma.Batch(source.AsSpan(), output.AsSpan(), 0));
    Assert.Throws<ArgumentException>(() => 
        Sma.Batch(source.AsSpan(), output.AsSpan(), -1));
    
    // Output must be same length as source
    Assert.Throws<ArgumentException>(() => 
        Sma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
}

[Fact]
public void SpanBatch_MatchesTSeriesBatch()
{
    var series = new TSeries();
    double[] source = new double[100];
    double[] output = new double[100];
    
    var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
    for (int i = 0; i < 100; i++)
    {
        var bar = gbm.Next(isNew: true);
        source[i] = bar.Close;
        series.Add(bar.Time, bar.Close);
    }
    
    var tseriesResult = Sma.Batch(series, 10);
    Sma.Batch(source.AsSpan(), output.AsSpan(), 10);
    
    for (int i = 0; i < 100; i++)
    {
        Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
    }
}

[Fact]
public void SpanBatch_ZeroAllocation()
{
    double[] source = new double[10000];
    double[] output = new double[10000];
    
    var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
    for (int i = 0; i < source.Length; i++)
        source[i] = gbm.Next().Close;
    
    // Warm up
    Sma.Batch(source.AsSpan(), output.AsSpan(), 100);
    
    // Verify method completes without OOM or stack overflow
    Assert.True(double.IsFinite(output[^1]));
}

[Fact]
public void SpanBatch_HandlesNaN()
{
    double[] source = [100, 110, double.NaN, 120, 130];
    double[] output = new double[5];
    
    Sma.Batch(source.AsSpan(), output.AsSpan(), 3);
    
    foreach (var val in output)
    {
        Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
    }
}
```

### 1.8 Priming Tests

For indicators that support pre-loading state with historical data.

#### Required Tests (if indicator supports `Prime`)

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `Prime_SetsStateCorrectly` | Verify primed state matches streaming | 🟡 Required |
| `Prime_WithInsufficientHistory_IsNotHot` | Verify warmup with short history | 🟡 Required |
| `Prime_HandlesNaN_InHistory` | Verify NaN handling during prime | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void Prime_SetsStateCorrectly()
{
    var sma = new Sma(5);
    double[] history = [10, 20, 30, 40, 50]; // SMA(5) = 30
    
    sma.Prime(history);
    
    Assert.True(sma.IsHot);
    Assert.Equal(30.0, sma.Last.Value, 1e-10);
    
    // Verify it continues correctly
    sma.Update(new TValue(DateTime.UtcNow, 60)); // 20,30,40,50,60 -> 40
    Assert.Equal(40.0, sma.Last.Value, 1e-10);
}

[Fact]
public void Prime_WithInsufficientHistory_IsNotHot()
{
    var sma = new Sma(10);
    double[] history = [10, 20, 30, 40, 50];
    
    sma.Prime(history);
    
    Assert.False(sma.IsHot);
    Assert.Equal(30.0, sma.Last.Value, 1e-10); // It calculates what it can
}

[Fact]
public void Prime_HandlesNaN_InHistory()
{
    var sma = new Sma(3);
    double[] history = [10, 20, double.NaN, 40];
    
    sma.Prime(history);
    
    Assert.True(sma.IsHot);
    Assert.True(double.IsFinite(sma.Last.Value));
}
```

### 1.9 Calculate Method Tests

For the static `Calculate` method that returns both results and a primed indicator.

#### Required Tests (if indicator supports `Calculate`)

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `Calculate_ReturnsCorrectResultsAndHotIndicator` | Verify tuple return | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void Calculate_ReturnsCorrectResultsAndHotIndicator()
{
    var series = new TSeries();
    for (int i = 1; i <= 10; i++) 
        series.Add(DateTime.UtcNow, i * 10);
    
    var (results, indicator) = Sma.Calculate(series, 5);
    
    // Check results
    Assert.Equal(10, results.Count);
    Assert.Equal(30.0, results[4].Value); // SMA(10..50) = 30
    Assert.Equal(80.0, results.Last.Value); // SMA(60..100) = 80
    
    // Check indicator state
    Assert.True(indicator.IsHot);
    Assert.Equal(80.0, indicator.Last.Value);
    Assert.Equal(5, indicator.WarmupPeriod);
    
    // Verify indicator continues correctly
    indicator.Update(new TValue(DateTime.UtcNow, 110));
    Assert.Equal(90.0, indicator.Last.Value);
}
```

### 1.10 Chainability Tests

#### Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `Chainability_Works` | Verify event-based chaining | 🟡 Required |
| `Pub_EventFires` | Verify `Pub` event fires on update | 🟡 Required |

#### Implementation Pattern

```csharp
[Fact]
public void Chainability_Works()
{
    var source = new TSeries();
    var sma = new Sma(source, 10);
    
    source.Add(new TValue(DateTime.UtcNow, 100));
    Assert.Equal(100, sma.Last.Value);
}

[Fact]
public void Pub_EventFires()
{
    var sma = new Sma(10);
    bool eventFired = false;
    sma.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;
    
    sma.Update(new TValue(DateTime.UtcNow, 100));
    Assert.True(eventFired);
}
```

### 1.11 Indicator-Specific Tests

Some indicators require additional specialized tests.

#### Sliding Window Tests (SMA, WMA, etc.)

```csharp
[Fact]
public void SlidingWindow_Works()
{
    var sma = new Sma(3);
    
    sma.Update(new TValue(DateTime.UtcNow, 10));
    sma.Update(new TValue(DateTime.UtcNow, 20));
    sma.Update(new TValue(DateTime.UtcNow, 30));
    Assert.Equal(20.0, sma.Last.Value, 1e-10); // (10+20+30)/3
    
    sma.Update(new TValue(DateTime.UtcNow, 40));
    Assert.Equal(30.0, sma.Last.Value, 1e-10); // (20+30+40)/3
    
    sma.Update(new TValue(DateTime.UtcNow, 50));
    Assert.Equal(40.0, sma.Last.Value, 1e-10); // (30+40+50)/3
}
```

#### Flat Line Tests

```csharp
[Fact]
public void FlatLine_ReturnsSameValue()
{
    var sma = new Sma(10);
    for (int i = 0; i < 20; i++)
    {
        sma.Update(new TValue(DateTime.UtcNow, 100));
    }
    Assert.Equal(100, sma.Last.Value);
}
```

#### Multi-Output Indicator Tests (MAMA/FAMA, MACD, etc.)

```csharp
[Fact]
public void MultiOutput_AllOutputsAccessible()
{
    var mama = new Mama();
    mama.Update(new TValue(DateTime.UtcNow, 100));
    
    Assert.True(double.IsFinite(mama.Last.Value));  // MAMA
    Assert.True(double.IsFinite(mama.Fama.Value));  // FAMA
}

[Fact]
public void Calculate_Span_WithSecondaryOutput()
{
    var data = new double[100];
    var mamaOutput = new double[100];
    var famaOutput = new double[100];
    
    Mama.Calculate(data, mamaOutput, famaOutput: famaOutput);
    
    for (int i = 0; i < 100; i++)
    {
        Assert.True(double.IsFinite(mamaOutput[i]));
        Assert.True(double.IsFinite(famaOutput[i]));
    }
}
```

#### Division-by-Zero Tests (for indicators with denominators)

```csharp
[Fact]
public void HandlesDivisionByZero()
{
    var adl = new Adl();
    // High = Low = 10. Range = 0. MFM should be 0.
    var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
    var val = adl.Update(bar);
    Assert.Equal(0, val.Value);
}
```

### 1.12 Test Data Generation

Always use the `GBM` (Geometric Brownian Motion) helper for generating realistic test data.

#### Guidelines

```csharp
// ✅ CORRECT: Use GBM for random data
var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
var series = bars.Close;

// ❌ WRONG: Do not use System.Random directly
var random = new Random();  // AVOID
double[] data = new double[100];
for (int i = 0; i < 100; i++)
    data[i] = random.NextDouble() * 100;  // AVOID
```

## 2. Validation Tests (`[Name].Validation.Tests.cs`)

Validation tests compare the indicator's output against established external libraries to ensure mathematical accuracy.

### 2.1 Test Class Structure

```csharp
public sealed class SmaValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;
    
    public SmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }
    
    public void Dispose()
    {
        Dispose(true);
    }
    
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing) _testData?.Dispose();
    }
    
    // Tests go here...
}
```

### 2.2 Required Validation Tests

For each external library, validate all three API modes:

| External Library | Tests Required |
|-----------------|----------------|
| **Skender.Stock.Indicators** | `Validate_Skender_Batch`, `Validate_Skender_Streaming`, `Validate_Skender_Span` |
| **TA-Lib** | `Validate_Talib_Batch`, `Validate_Talib_Streaming`, `Validate_Talib_Span` |
| **Tulip** | `Validate_Tulip_Batch`, `Validate_Tulip_Streaming`, `Validate_Tulip_Span` |
| **OoplesFinance** | `Validate_Ooples_Batch` |

### 2.3 Validation Patterns

#### Skender Validation

```csharp
[Fact]
public void Validate_Skender_Batch()
{
    int[] periods = { 5, 10, 20, 50, 100 };
    
    foreach (var period in periods)
    {
        var sma = new Sma(period);
        var qResult = sma.Update(_testData.Data);
        
        var sResult = _testData.SkenderQuotes.GetSma(period).ToList();
        
        ValidationHelper.VerifyData(qResult, sResult, (s) => s.Sma);
    }
    _output.WriteLine("SMA Batch(TSeries) validated against Skender");
}

[Fact]
public void Validate_Skender_Streaming()
{
    int[] periods = { 5, 10, 20, 50, 100 };
    
    foreach (var period in periods)
    {
        var sma = new Sma(period);
        var qResults = new List<double>();
        foreach (var item in _testData.Data)
        {
            qResults.Add(sma.Update(item).Value);
        }
        
        var sResult = _testData.SkenderQuotes.GetSma(period).ToList();
        
        ValidationHelper.VerifyData(qResults, sResult, (s) => s.Sma);
    }
    _output.WriteLine("SMA Streaming validated against Skender");
}

[Fact]
public void Validate_Skender_Span()
{
    int[] periods = { 5, 10, 20, 50, 100 };
    double[] sourceData = _testData.RawData.ToArray();
    
    foreach (var period in periods)
    {
        double[] qOutput = new double[sourceData.Length];
        Sma.Batch(sourceData.AsSpan(), qOutput.AsSpan(), period);
        
        var sResult = _testData.SkenderQuotes.GetSma(period).ToList();
        
        ValidationHelper.VerifyData(qOutput, sResult, (s) => s.Sma);
    }
    _output.WriteLine("SMA Span validated against Skender");
}
```

#### TA-Lib Validation

```csharp
[Fact]
public void Validate_Talib_Batch()
{
    int[] periods = { 5, 10, 20, 50, 100 };
    double[] tData = _testData.RawData.ToArray();
    double[] output = new double[tData.Length];
    
    foreach (var period in periods)
    {
        var sma = new Sma(period);
        var qResult = sma.Update(_testData.Data);
        
        var retCode = TALib.Functions.Sma<double>(
            tData, 0..^0, output, out var outRange, period);
        Assert.Equal(Core.RetCode.Success, retCode);
        
        int lookback = TALib.Functions.SmaLookback(period);
        
        ValidationHelper.VerifyData(qResult, output, outRange, lookback);
    }
    _output.WriteLine("SMA Batch validated against TA-Lib");
}
```

#### Tulip Validation

```csharp
[Fact]
public void Validate_Tulip_Batch()
{
    int[] periods = { 5, 10, 20, 50, 100 };
    double[] tData = _testData.RawData.ToArray();
    
    foreach (var period in periods)
    {
        var sma = new Sma(period);
        var qResult = sma.Update(_testData.Data);
        
        var smaIndicator = Tulip.Indicators.sma;
        double[][] inputs = { tData };
        double[] options = { period };
        int lookback = period - 1;
        double[][] outputs = { new double[tData.Length - lookback] };
        
        smaIndicator.Run(inputs, options, outputs);
        var tResult = outputs[0];
        
        ValidationHelper.VerifyData(qResult, tResult, lookback);
    }
    _output.WriteLine("SMA Batch validated against Tulip");
}
```

#### OoplesFinance Validation

```csharp
[Fact]
public void Validate_Ooples_Batch()
{
    int[] periods = { 5, 10, 20, 50, 100 };
    
    var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
    {
        Date = q.Date,
        Close = (double)q.Close,
        High = (double)q.High,
        Low = (double)q.Low,
        Open = (double)q.Open,
        Volume = (double)q.Volume
    }).ToList();
    
    foreach (var period in periods)
    {
        var sma = new Sma(period);
        var qResult = sma.Update(_testData.Data);
        
        var stockData = new StockData(ooplesData);
        var sResult = Calculations.CalculateSimpleMovingAverage(stockData, period)
            .OutputValues.Values.First();
        
        ValidationHelper.VerifyData(qResult, sResult, 
            (s) => s, 100, ValidationHelper.OoplesTolerance);
    }
    _output.WriteLine("SMA Batch validated against Ooples");
}
```

### 2.4 Tolerance Constants

Use explicit tolerance constants from `ValidationHelper`:

```csharp
// Standard tolerances
ValidationHelper.SkenderTolerance  // 1e-9
ValidationHelper.TalibTolerance    // 1e-9
ValidationHelper.TulipTolerance    // 1e-9
ValidationHelper.OoplesTolerance   // 1e-6
```

## 3. Quantower Adapter Tests (`[Name].Quantower.Tests.cs`)

These tests verify the Quantower platform integration.

### 3.1 Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `Constructor_SetsDefaults` | Verify default property values | 🔴 Critical |
| `MinHistoryDepths_IsCorrect` | Verify history requirements | 🟡 Required |
| `ShortName_IncludesParameters` | Verify display name | 🟡 Required |
| `Initialize_CreatesInternalIndicator` | Verify initialization | 🔴 Critical |
| `ProcessUpdate_HistoricalBar_ComputesValue` | Verify historical processing | 🔴 Critical |
| `ProcessUpdate_NewBar_ComputesValue` | Verify new bar processing | 🔴 Critical |
| `ProcessUpdate_NewTick_ProcessesWithoutError` | Verify tick processing | 🟡 Required |
| `MultipleUpdates_ProducesCorrectSequence` | Verify sequence processing | 🟡 Required |
| `DifferentSourceTypes_Work` | Verify OHLC source types | 🟡 Required |
| `Length_CanBeChanged` | Verify parameter modification | 🟢 Recommended |

### 3.2 Implementation Pattern

```csharp
public class SmaIndicatorTests
{
    [Fact]
    public void SmaIndicator_Constructor_SetsDefaults()
    {
        var indicator = new SmaIndicator();
        
        Assert.Equal(14, indicator.Period);
        Assert.Equal(SourceType.Close, indicator.Source);
        Assert.True(indicator.ShowColdValues);
        Assert.Equal("SMA - Simple Moving Average", indicator.Name);
        Assert.False(indicator.SeparateWindow);
        Assert.True(indicator.OnBackGround);
    }
    
    [Fact]
    public void SmaIndicator_Initialize_CreatesInternalFilter()
    {
        var indicator = new SmaIndicator { Period = 14 };
        indicator.Initialize();
        Assert.Single(indicator.LinesSeries);
    }
    
    [Fact]
    public void SmaIndicator_ProcessUpdate_HistoricalBar_ComputesValue()
    {
        var indicator = new SmaIndicator { Period = 3 };
        indicator.Initialize();
        
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        
        var args = new UpdateArgs(UpdateReason.HistoricalBar);
        indicator.ProcessUpdate(args);
        
        Assert.Equal(1, indicator.LinesSeries[0].Count);
        Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)));
    }
    
    [Fact]
    public void SmaIndicator_ProcessUpdate_NewBar_ComputesValue()
    {
        var indicator = new SmaIndicator { Period = 3 };
        indicator.Initialize();
        
        var now = DateTime.UtcNow;
        indicator.HistoricalData.AddBar(now, 100, 105, 95, 102);
        indicator.HistoricalData.AddBar(now.AddMinutes(1), 102, 108, 100, 106);
        
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
        indicator.ProcessUpdate(new UpdateArgs(UpdateReason.NewBar));
        
        Assert.Equal(2, indicator.LinesSeries[0].Count);
    }
    
    [Fact]
    public void SmaIndicator_DifferentSourceTypes_Work()
    {
        var sources = new[]
        {
            SourceType.Open,
            SourceType.High,
            SourceType.Low,
            SourceType.Close,
            SourceType.HL2,
            SourceType.HLC3,
        };
        
        foreach (var source in sources)
        {
            var indicator = new SmaIndicator { Period = 3, Source = source };
            indicator.Initialize();
            
            var now = DateTime.UtcNow;
            indicator.HistoricalData.AddBar(now, 100, 110, 90, 105);
            indicator.ProcessUpdate(new UpdateArgs(UpdateReason.HistoricalBar));
            
            Assert.True(double.IsFinite(indicator.LinesSeries[0].GetValue(0)),
                $"Source {source} should produce finite value");
        }
    }
}
```

## 4. Volume/TBar Indicator Tests

For indicators that require OHLCV data (TBar input).

### 4.1 Additional Required Tests

| Test Name | Description | Priority |
|-----------|-------------|----------|
| `BasicCalculation_ReturnsExpectedValues` | Verify with known inputs | 🔴 Critical |
| `UpdateTBarSeries_ReturnsCorrectSeries` | Verify series processing | 🔴 Critical |
| `CalculateTBarSeries_ReturnsCorrectSeries` | Verify static method | 🟡 Required |
| `CalculateSpan_ReturnsCorrectValues` | Verify span with all inputs | 🟡 Required |
| `CalculateSpan_ThrowsOnMismatchedLengths` | Verify length validation | 🟡 Required |
| `TValueUpdate_DoesNotChangeValue` | Verify TValue ignored | 🟡 Required |

### 4.2 Implementation Pattern

```csharp
[Fact]
public void Adl_BasicCalculation_ReturnsExpectedValues()
{
    var adl = new Adl();
    var time = DateTime.UtcNow;
    
    // Bar 1: Close=10, High=12, Low=8. Range=4.
    // MFM = ((10-8) - (12-10)) / 4 = 0
    var bar1 = new TBar(time, 10, 12, 8, 10, 100);
    var val1 = adl.Update(bar1);
    Assert.Equal(0, val1.Value);
    
    // Bar 2: Close=12 (at high). MFM = 1.
    var bar2 = new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200);
    var val2 = adl.Update(bar2);
    Assert.Equal(200, val2.Value);
}

[Fact]
public void Adl_CalculateSpan_ReturnsCorrectValues()
{
    double[] high = { 12, 12, 12 };
    double[] low = { 8, 8, 8 };
    double[] close = { 10, 12, 8 };
    double[] volume = { 100, 200, 100 };
    double[] output = new double[3];
    
    Adl.Calculate(high, low, close, volume, output);
    
    Assert.Equal(0, output[0]);
    Assert.Equal(200, output[1]);
    Assert.Equal(100, output[2]);
}

[Fact]
public void Adl_CalculateSpan_ThrowsOnMismatchedLengths()
{
    double[] high = { 10, 11 };
    double[] low = { 9, 10 };
    double[] close = { 9.5, 10.5 };
    double[] volume = { 100 }; // Mismatched
    double[] output = new double[2];
    
    Assert.Throws<ArgumentException>(() =>
        Adl.Calculate(high, low, close, volume, output));
}
```

## 5. Test Checklist Summary

### Mandatory Tests (Every Indicator)

- [ ] `Constructor_ValidatesInput`
- [ ] `Calc_ReturnsValue`
- [ ] `Calc_IsNew_AcceptsParameter`
- [ ] `Calc_IsNew_False_UpdatesValue`
- [ ] `IterativeCorrections_RestoreToOriginalState`
- [ ] `Reset_ClearsState`
- [ ] `IsHot_BecomesTrueWhenBufferFull`
- [ ] `NaN_Input_UsesLastValidValue`
- [ ] `Infinity_Input_UsesLastValidValue`
- [ ] `BatchCalc_MatchesIterativeCalc`
- [ ] `AllModes_ProduceSameResult`
- [ ] `SpanBatch_ValidatesInput`
- [ ] `SpanBatch_MatchesTSeriesBatch`

### Validation Tests (At Least One)

- [ ] `Validate_Skender_Batch`
- [ ] `Validate_Skender_Streaming`
- [ ] `Validate_Skender_Span`
- [ ] `Validate_Talib_Batch` (if available)
- [ ] `Validate_Tulip_Batch` (if available)

### Quantower Tests

- [ ] `Constructor_SetsDefaults`
- [ ] `Initialize_CreatesInternalIndicator`
- [ ] `ProcessUpdate_HistoricalBar_ComputesValue`
- [ ] `ProcessUpdate_NewBar_ComputesValue`
- [ ] `DifferentSourceTypes_Work`

## 6. Test Naming Conventions

Follow this pattern for test method names:

```
[MethodUnderTest]_[Scenario]_[ExpectedBehavior]
```

Examples:
- `Constructor_InvalidPeriod_ThrowsArgumentException`
- `Update_NaNInput_UsesLastValidValue`
- `SpanBatch_MismatchedLengths_ThrowsArgumentException`
- `AllModes_SameInput_ProduceSameResult`

## 7. Assertions Best Practices

### Numeric Comparisons

```csharp
// For exact matches
Assert.Equal(expected, actual, 1e-10);

// For approximate matches (floating point)
Assert.Equal(expected, actual, precision: 9);

// For range checks
Assert.InRange(value, min, max);

// For finite checks
Assert.True(double.IsFinite(value));
```

### Exception Assertions

```csharp
// Verify exception type
Assert.Throws<ArgumentException>(() => new Sma(0));

// Verify exception parameter name (MA0015 compliance)
var ex = Assert.Throws<ArgumentException>(() => 
    Sma.Batch(source, output, 0));
Assert.Equal("period", ex.ParamName);
```

### Collection Assertions

```csharp
// Verify count
Assert.Equal(expected.Count, actual.Count);

// Verify empty
Assert.Empty(result);

// Verify single
Assert.Single(indicator.LinesSeries);
```
