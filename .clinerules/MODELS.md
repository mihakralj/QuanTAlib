# Recommended Test Pattern for Indicators

This document outlines the standard set of unit tests that every indicator in QuanTAlib should implement to ensure correctness, consistency, and robustness.

## 1. Standard Unit Tests (`[Name].Tests.cs`)

These tests verify the internal logic, state management, and API contract of the indicator.

### Constructor & Validation

- **`Constructor_ValidatesInput`**: Verify that invalid parameters (e.g., `period <= 0`) throw `ArgumentException`.
- **`Constructor_ValidatesOptionalArgs`**: If applicable, verify other parameters (e.g., `alpha`, `sigma`).

### Random Data Generation

- **Use `GBM` Helper**: Always use the `GBM` (Geometric Brownian Motion) helper class for generating random test data.
- **Avoid `System.Random`**: Do not use `System.Random` directly in tests to ensure consistency and realism.
- **Example**: `var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);`

### Basic Functionality

- **`Calc_ReturnsValue`**: Verify `Update` returns a valid `TValue` and updates the `Last` property.
- **`FirstValue_ReturnsExpected`**: Verify the first output value (often the input itself for averages).
- **`Properties_Accessible`**: Verify `Last`, `IsHot`, `Name`, etc., are accessible and initialized correctly.

### State Management & Bar Correction

- **`Calc_IsNew_AcceptsParameter`**: Verify that `isNew: true` advances the state.
- **`Calc_IsNew_False_UpdatesValue`**: Verify that `isNew: false` updates the current value without advancing state (intra-bar update).
- **`IterativeCorrections_RestoreToOriginalState`**: Critical test.
    1. Feed $N$ values.
    2. Remember state.
    3. Feed $M$ updates with `isNew: false`.
    4. Feed the original $N$-th value again with `isNew: false`.
    5. Verify state matches the remembered state.
- **`Reset_ClearsState`**: Verify `Reset()` clears all internal state and the indicator behaves like a new instance.

### Warmup & Convergence

- **`IsHot_BecomesTrueWhenBufferFull`**: Verify `IsHot` becomes true after the expected number of periods.
- **`IsHot_IsPeriodDependent`**: If applicable, verify warmup time scales with period.

### Robustness (NaN/Infinity)

- **`NaN_Input_UsesLastValidValue`**: Verify that `NaN` input does not crash and typically carries forward the last valid value.
- **`Infinity_Input_UsesLastValidValue`**: Verify handling of `PositiveInfinity` and `NegativeInfinity`.
- **`MultipleNaN_ContinuesWithLastValid`**: Verify behavior with consecutive invalid inputs.
- **`BatchCalc_HandlesNaN`**: Verify batch processing handles `NaN` correctly.

### Consistency

- **`BatchCalc_MatchesIterativeCalc`**: Verify that `Update(TSeries)` produces the same results as a loop of `Update(TValue)`.
- **`AllModes_ProduceSameResult`**: **Crucial**. Verify that all 4 usage modes produce identical results:
    1. **Batch**: `Indicator.Calculate(TSeries)`
    2. **Span**: `Indicator.Calculate(ReadOnlySpan, Span)`
    3. **Streaming**: `new Indicator().Update(TValue)`
    4. **Eventing**: `new Indicator(source).Update()`

### Span API (High Performance)

- **`SpanCalc_ValidatesInput`**: Verify input/output buffer length checks.
- **`SpanCalc_MatchesTSeriesCalc`**: Verify Span API output matches TSeries API output.
- **`SpanCalc_ZeroAllocation`**: Verify the method runs without obvious errors on large datasets (allocation verified via benchmarks, but this ensures no OOM or stack overflow).
- **`SpanCalc_HandlesNaN`**: Verify Span API handles invalid inputs safely.

## 2. Validation Tests (`[Name].Validation.Tests.cs`)

These tests compare the indicator's output against established external libraries to ensure mathematical accuracy.

- **Compare against Skender.Stock.Indicators**: Primary validation target.
- **Compare against TA-Lib**: Secondary validation target.
- **Compare against Python (pandas-ta/talib)**: If C# libs are unavailable.
- **Tolerance**: Typically `1e-6` to `1e-9`.

When asserting parameter validation behavior (e.g., invalid periods or mismatched buffer lengths), align with the MA0015-compliant pattern defined in `AGENTS.md` (commit d7dbd70): `ArgumentException` (or derived) overloads must include the offending `paramName` (for example `nameof(output)` or `nameof(sourceY)`), and tests should verify both the exception type and the parameter name where relevant.

## 3. Example Test Template

```csharp
[Fact]
public void AllModes_ProduceSameResult()
{
    // Arrange
    int period = 10;
    var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    var series = bars.Close;
    
    // 1. Batch Mode
    var batchSeries = MyIndicator.Calculate(series, period);
    double expected = batchSeries.Last.Value;

    // 2. Span Mode
    var tValues = series.Values.ToArray();
    var spanInput = new ReadOnlySpan<double>(tValues);
    var spanOutput = new double[tValues.Length];
    MyIndicator.Calculate(spanInput, spanOutput, period);
    double spanResult = spanOutput[^1];

    // 3. Streaming Mode
    var streamingInd = new MyIndicator(period);
    for (int i = 0; i < series.Count; i++)
    {
        streamingInd.Update(series[i]);
    }
    double streamingResult = streamingInd.Last.Value;

    // 4. Eventing Mode
    var pubSource = new TSeries();
    var eventingInd = new MyIndicator(pubSource, period);
    for (int i = 0; i < series.Count; i++)
    {
        pubSource.Add(series[i]);
    }
    double eventingResult = eventingInd.Last.Value;

    // Assert
    Assert.Equal(expected, spanResult, precision: 9);
    Assert.Equal(expected, streamingResult, precision: 9);
    Assert.Equal(expected, eventingResult, precision: 9);
}
