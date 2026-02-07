using Xunit;

namespace QuanTAlib.Tests;

public class RocrTests
{
    private readonly TSeries _gbm;
    private const int TestPeriod = 9;
    private const int DataPoints = 100;

    public RocrTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var rocr = new Rocr(TestPeriod);
        Assert.Equal($"Rocr({TestPeriod})", rocr.Name);
        Assert.Equal(TestPeriod + 1, rocr.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rocr(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rocr(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var rocr = new Rocr(source, TestPeriod);
        Assert.NotNull(rocr);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsCorrectValue()
    {
        var rocr = new Rocr(TestPeriod);
        var tv = rocr.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1.0, tv.Value);
    }

    [Fact]
    public void Update_FirstValues_ReturnsOne()
    {
        var rocr = new Rocr(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            var tv = rocr.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.Equal(1.0, tv.Value);
        }
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsRatio()
    {
        var rocr = new Rocr(2); // period=2
        var values = new double[] { 100, 102, 105, 103, 110 };

        for (int i = 0; i < values.Length; i++)
        {
            var tv = rocr.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i]), true);

            if (i < 2)
            {
                Assert.Equal(1.0, tv.Value); // warmup period
            }
            else
            {
                // ratio: current / past
                double expected = values[i] / values[i - 2];
                Assert.Equal(expected, tv.Value, 10);
            }
        }
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var rocr = new Rocr(TestPeriod);
        rocr.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1.0, rocr.Last.Value);
    }

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var rocr = new Rocr(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            rocr.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(rocr.IsHot);
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var rocr = new Rocr(TestPeriod);
        for (int i = 0; i <= TestPeriod; i++)
        {
            rocr.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(rocr.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var rocr = new Rocr(TestPeriod);
        Assert.Equal($"Rocr({TestPeriod})", rocr.Name);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var rocr = new Rocr(TestPeriod);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 105.0), true);
        rocr.Update(new TValue(time.AddSeconds(2), 110.0), true);

        // state should advance after each true
        Assert.NotEqual(default, rocr.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        // Warmup
        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var first = rocr.Update(new TValue(time.AddSeconds(2), 105.0), true);

        // Update same bar with different value
        var corrected = rocr.Update(new TValue(time.AddSeconds(2), 108.0), false);

        Assert.NotEqual(first.Value, corrected.Value);
        // first: 105 / 100 = 1.05
        // corrected: 108 / 100 = 1.08
        Assert.Equal(1.05, first.Value, 10);
        Assert.Equal(1.08, corrected.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        // Initial values
        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var baseline = rocr.Update(new TValue(time.AddSeconds(2), 105.0), true);

        // Make several corrections
        rocr.Update(new TValue(time.AddSeconds(2), 108.0), false);
        rocr.Update(new TValue(time.AddSeconds(2), 110.0), false);
        var restored = rocr.Update(new TValue(time.AddSeconds(2), 105.0), false);

        // Should match original value
        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var rocr = new Rocr(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i <= TestPeriod; i++)
        {
            rocr.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        rocr.Reset();

        Assert.Equal(default, rocr.Last);
        Assert.False(rocr.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 102.0), true);
        _ = rocr.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterNaN = rocr.Update(new TValue(time.AddSeconds(3), double.NaN), true);

        // NaN should use last valid (105), so ratio is 105 / 102 = 1.0294...
        Assert.True(double.IsFinite(afterNaN.Value));
        Assert.Equal(105.0 / 102.0, afterNaN.Value, 10);
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 102.0), true);
        rocr.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterInf = rocr.Update(new TValue(time.AddSeconds(3), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var rocr = new Rocr(TestPeriod);
        var time = DateTime.UtcNow;

        // Insert several NaN values
        for (int i = 0; i < 20; i++)
        {
            var value = i % 3 == 0 ? double.NaN : 100.0 + i;
            var tv = rocr.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    [Fact]
    public void Update_WithZeroPastValue_ReturnsOne()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 0.0), true);         // Value of 0
        rocr.Update(new TValue(time.AddSeconds(1), 50.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(2), 100.0), true);

        // Division by zero: 100 / 0 should return 1.0 as safe default
        Assert.Equal(1.0, result.Value);
    }

    #endregion

    #region Consistency Tests (All 4 modes must match)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Rocr.Calculate(_gbm, TestPeriod);

        // Mode 2: Streaming
        var streamingRocr = new Rocr(TestPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingRocr.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Rocr.Calculate(_gbm.Values, spanOutput, TestPeriod);

        // Mode 4: Event-driven
        var eventRocr = new Rocr(TestPeriod);
        var eventResult = new TSeries(DataPoints);
        eventRocr.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventRocr.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
        }

        // Compare last 100 values (or all if fewer)
        int compareCount = Math.Min(100, DataPoints);
        for (int i = DataPoints - compareCount; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResult[i].Value, 10);
            Assert.Equal(batchResult[i].Value, spanOutput[i], 10);
            Assert.Equal(batchResult[i].Value, eventResult[i].Value, 10);
        }
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Calculate_Span_ValidatesEmptySource()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> empty = [];
            Span<double> output = stackalloc double[1];
            Rocr.Calculate(empty, output, TestPeriod);
        });
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesOutputLength()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[3]; // too short
            Rocr.Calculate(source, output, TestPeriod);
        });
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesPeriod()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[5];
            Rocr.Calculate(source, output, 0);
        });
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Rocr.Calculate(_gbm, TestPeriod);

        Span<double> spanOutput = stackalloc double[DataPoints];
        Rocr.Calculate(_gbm.Values, spanOutput, TestPeriod);

        for (int i = 0; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 10);
        }
    }

    [Fact]
    public void Calculate_Span_HandlesZeroDivision()
    {
        double[] source = [0, 100, 102, 103, 104];
        Span<double> output = stackalloc double[5];

        // Should not throw
        Rocr.Calculate(source, output, 2);

        // First element after warmup divides by 0
        Assert.Equal(1.0, output[2]); // 102 / 0 = 1.0 (safe default)
    }

    [Fact]
    public void Calculate_Span_LargeData_NoStackOverflow()
    {
        int largeSize = 10000;
        double[] source = new double[largeSize];
        double[] output = new double[largeSize];

        for (int i = 0; i < largeSize; i++)
        {
            source[i] = 100.0 + i * 0.1;
        }

        // Should not throw
        Rocr.Calculate(source, output, TestPeriod);

        Assert.Equal(largeSize, output.Length);
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var rocr = new Rocr(TestPeriod);
        bool eventFired = false;

        rocr.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        rocr.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var rocr = new Rocr(source, 2);
        var results = new List<double>();

        rocr.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(10, results.Count);
    }

    #endregion

    #region Mathematical Properties Tests

    [Fact]
    public void Update_PriceDoubled_ReturnsTwo()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 50.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 60.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(2), 100.0), true);

        // 100 / 50 = 2.0
        Assert.Equal(2.0, result.Value, 10);
    }

    [Fact]
    public void Update_PriceHalved_ReturnsPointFive()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 80.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(2), 50.0), true);

        // 50 / 100 = 0.5
        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void Update_NoChange_ReturnsOne()
    {
        var rocr = new Rocr(2);
        var time = DateTime.UtcNow;

        rocr.Update(new TValue(time, 100.0), true);
        rocr.Update(new TValue(time.AddSeconds(1), 105.0), true);
        var result = rocr.Update(new TValue(time.AddSeconds(2), 100.0), true);

        // 100 / 100 = 1.0
        Assert.Equal(1.0, result.Value, 10);
    }

    #endregion
}
