using Xunit;

namespace QuanTAlib.Tests;

public class RocTests
{
    private readonly TSeries _gbm;
    private const int TestPeriod = 9;
    private const int DataPoints = 100;

    public RocTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var roc = new Roc(TestPeriod);
        Assert.Equal($"Roc({TestPeriod})", roc.Name);
        Assert.Equal(TestPeriod + 1, roc.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Roc(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Roc(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var roc = new Roc(source, TestPeriod);
        Assert.NotNull(roc);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsCorrectValue()
    {
        var roc = new Roc(TestPeriod);
        var tv = roc.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, tv.Value);
    }

    [Fact]
    public void Update_FirstValues_ReturnsZero()
    {
        var roc = new Roc(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            var tv = roc.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.Equal(0.0, tv.Value);
        }
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsAbsoluteChange()
    {
        var roc = new Roc(2); // period=2
        var values = new double[] { 100, 102, 105, 103, 110 };

        for (int i = 0; i < values.Length; i++)
        {
            var tv = roc.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i]), true);

            if (i < 2)
            {
                Assert.Equal(0.0, tv.Value); // warmup period
            }
            else
            {
                // absolute change: current - past
                double expected = values[i] - values[i - 2];
                Assert.Equal(expected, tv.Value, 10);
            }
        }
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var roc = new Roc(TestPeriod);
        roc.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, roc.Last.Value);
    }

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var roc = new Roc(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            roc.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(roc.IsHot);
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var roc = new Roc(TestPeriod);
        for (int i = 0; i <= TestPeriod; i++)
        {
            roc.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(roc.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var roc = new Roc(TestPeriod);
        Assert.Equal($"Roc({TestPeriod})", roc.Name);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var roc = new Roc(TestPeriod);
        var time = DateTime.UtcNow;

        roc.Update(new TValue(time, 100.0), true);
        roc.Update(new TValue(time.AddSeconds(1), 105.0), true);
        roc.Update(new TValue(time.AddSeconds(2), 110.0), true);

        // state should advance after each true
        Assert.NotEqual(default, roc.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var roc = new Roc(2);
        var time = DateTime.UtcNow;

        // Warmup
        roc.Update(new TValue(time, 100.0), true);
        roc.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var first = roc.Update(new TValue(time.AddSeconds(2), 105.0), true);

        // Update same bar with different value
        var corrected = roc.Update(new TValue(time.AddSeconds(2), 108.0), false);

        Assert.NotEqual(first.Value, corrected.Value);
        // first: 105 - 100 = 5
        // corrected: 108 - 100 = 8
        Assert.Equal(5.0, first.Value, 10);
        Assert.Equal(8.0, corrected.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var roc = new Roc(2);
        var time = DateTime.UtcNow;

        // Initial values
        roc.Update(new TValue(time, 100.0), true);
        roc.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var baseline = roc.Update(new TValue(time.AddSeconds(2), 105.0), true);

        // Make several corrections
        roc.Update(new TValue(time.AddSeconds(2), 108.0), false);
        roc.Update(new TValue(time.AddSeconds(2), 110.0), false);
        var restored = roc.Update(new TValue(time.AddSeconds(2), 105.0), false);

        // Should match original value
        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var roc = new Roc(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i <= TestPeriod; i++)
        {
            roc.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        roc.Reset();

        Assert.Equal(default, roc.Last);
        Assert.False(roc.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var roc = new Roc(2);
        var time = DateTime.UtcNow;

        roc.Update(new TValue(time, 100.0), true);
        roc.Update(new TValue(time.AddSeconds(1), 102.0), true);
        _ = roc.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterNaN = roc.Update(new TValue(time.AddSeconds(3), double.NaN), true);

        // NaN should use last valid (105), so change is 105 - 102 = 3
        Assert.True(double.IsFinite(afterNaN.Value));
        Assert.Equal(3.0, afterNaN.Value, 10);
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var roc = new Roc(2);
        var time = DateTime.UtcNow;

        roc.Update(new TValue(time, 100.0), true);
        roc.Update(new TValue(time.AddSeconds(1), 102.0), true);
        roc.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterInf = roc.Update(new TValue(time.AddSeconds(3), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var roc = new Roc(TestPeriod);
        var time = DateTime.UtcNow;

        // Insert several NaN values
        for (int i = 0; i < 20; i++)
        {
            var value = i % 3 == 0 ? double.NaN : 100.0 + i;
            var tv = roc.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region Consistency Tests (All 4 modes must match)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Roc.Calculate(_gbm, TestPeriod);

        // Mode 2: Streaming
        var streamingRoc = new Roc(TestPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingRoc.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Roc.Calculate(_gbm.Values, spanOutput, TestPeriod);

        // Mode 4: Event-driven
        var eventRoc = new Roc(TestPeriod);
        var eventResult = new TSeries(DataPoints);
        eventRoc.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventRoc.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
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
            Roc.Calculate(empty, output, TestPeriod);
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
            Roc.Calculate(source, output, TestPeriod);
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
            Roc.Calculate(source, output, 0);
        });
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Roc.Calculate(_gbm, TestPeriod);

        Span<double> spanOutput = stackalloc double[DataPoints];
        Roc.Calculate(_gbm.Values, spanOutput, TestPeriod);

        for (int i = 0; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 10);
        }
    }

    [Fact]
    public void Calculate_Span_HandlesNaN()
    {
        double[] source = [100, double.NaN, 102, 103, 104];
        Span<double> output = stackalloc double[5];

        // Should not throw
        Roc.Calculate(source, output, 2);

        // Output will contain NaN due to input NaN
        // This is expected for span-based (no state tracking)
        Assert.Equal(5, output.Length);
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
        Roc.Calculate(source, output, TestPeriod);

        Assert.Equal(largeSize, output.Length);
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var roc = new Roc(TestPeriod);
        bool eventFired = false;

        roc.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        roc.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var roc = new Roc(source, 2);
        var results = new List<double>();

        roc.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(10, results.Count);
    }

    #endregion
}
