using Xunit;

namespace QuanTAlib.Tests;

public class MomTests
{
    private readonly TSeries _gbm;
    private const int TestPeriod = 9;
    private const int DataPoints = 100;

    public MomTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var mom = new Mom(TestPeriod);
        Assert.Equal($"Mom({TestPeriod})", mom.Name);
        Assert.Equal(TestPeriod + 1, mom.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Mom(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Mom(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var mom = new Mom(source, TestPeriod);
        Assert.NotNull(mom);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsZeroDuringWarmup()
    {
        var mom = new Mom(TestPeriod);
        var tv = mom.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, tv.Value);
    }

    [Fact]
    public void Update_FirstValues_ReturnsZero()
    {
        var mom = new Mom(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            var tv = mom.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.Equal(0.0, tv.Value);
        }
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsAbsoluteChange()
    {
        var mom = new Mom(2); // period=2
        var values = new double[] { 100, 102, 105, 103, 110 };

        for (int i = 0; i < values.Length; i++)
        {
            var tv = mom.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i]), true);

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
    public void Update_ConstantInput_ReturnsZero()
    {
        var mom = new Mom(5);
        for (int i = 0; i < 20; i++)
        {
            var tv = mom.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
            Assert.Equal(0.0, tv.Value);
        }
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var mom = new Mom(TestPeriod);
        mom.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, mom.Last.Value);
    }

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var mom = new Mom(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            mom.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(mom.IsHot);
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var mom = new Mom(TestPeriod);
        for (int i = 0; i <= TestPeriod; i++)
        {
            mom.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(mom.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var mom = new Mom(TestPeriod);
        Assert.Equal($"Mom({TestPeriod})", mom.Name);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var mom = new Mom(TestPeriod);
        var time = DateTime.UtcNow;

        mom.Update(new TValue(time, 100.0), true);
        mom.Update(new TValue(time.AddSeconds(1), 105.0), true);
        mom.Update(new TValue(time.AddSeconds(2), 110.0), true);

        Assert.NotEqual(default, mom.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var mom = new Mom(2);
        var time = DateTime.UtcNow;

        mom.Update(new TValue(time, 100.0), true);
        mom.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var first = mom.Update(new TValue(time.AddSeconds(2), 105.0), true);

        var corrected = mom.Update(new TValue(time.AddSeconds(2), 108.0), false);

        Assert.NotEqual(first.Value, corrected.Value);
        // first: 105 - 100 = 5
        // corrected: 108 - 100 = 8
        Assert.Equal(5.0, first.Value, 10);
        Assert.Equal(8.0, corrected.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var mom = new Mom(2);
        var time = DateTime.UtcNow;

        mom.Update(new TValue(time, 100.0), true);
        mom.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var baseline = mom.Update(new TValue(time.AddSeconds(2), 105.0), true);

        mom.Update(new TValue(time.AddSeconds(2), 108.0), false);
        mom.Update(new TValue(time.AddSeconds(2), 110.0), false);
        var restored = mom.Update(new TValue(time.AddSeconds(2), 105.0), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var mom = new Mom(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i <= TestPeriod; i++)
        {
            mom.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        mom.Reset();

        Assert.Equal(default, mom.Last);
        Assert.False(mom.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var mom = new Mom(2);
        var time = DateTime.UtcNow;

        mom.Update(new TValue(time, 100.0), true);
        mom.Update(new TValue(time.AddSeconds(1), 102.0), true);
        _ = mom.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterNaN = mom.Update(new TValue(time.AddSeconds(3), double.NaN), true);

        // NaN should use last valid (105), so change is 105 - 102 = 3
        Assert.True(double.IsFinite(afterNaN.Value));
        Assert.Equal(3.0, afterNaN.Value, 10);
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var mom = new Mom(2);
        var time = DateTime.UtcNow;

        mom.Update(new TValue(time, 100.0), true);
        mom.Update(new TValue(time.AddSeconds(1), 102.0), true);
        mom.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterInf = mom.Update(new TValue(time.AddSeconds(3), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var mom = new Mom(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var value = i % 3 == 0 ? double.NaN : 100.0 + i;
            var tv = mom.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region Consistency Tests (All 4 modes must match)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Mom.Batch(_gbm, TestPeriod);

        // Mode 2: Streaming
        var streamingMom = new Mom(TestPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingMom.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Mom.Batch(_gbm.Values, spanOutput, TestPeriod);

        // Mode 4: Event-driven
        var eventMom = new Mom(TestPeriod);
        var eventResult = new TSeries(DataPoints);
        eventMom.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventMom.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
        }

        // Compare all values
        for (int i = 0; i < DataPoints; i++)
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
            Mom.Batch(empty, output, TestPeriod);
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
            Mom.Batch(source, output, TestPeriod);
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
            Mom.Batch(source, output, 0);
        });
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Mom.Batch(_gbm, TestPeriod);

        Span<double> spanOutput = stackalloc double[DataPoints];
        Mom.Batch(_gbm.Values, spanOutput, TestPeriod);

        for (int i = 0; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 10);
        }
    }

    [Fact]
    public void Calculate_Span_LargeData_NoStackOverflow()
    {
        int largeSize = 10000;
        double[] source = new double[largeSize];
        double[] output = new double[largeSize];

        for (int i = 0; i < largeSize; i++)
        {
            source[i] = 100.0 + (i * 0.1);
        }

        Mom.Batch(source, output, TestPeriod);

        Assert.Equal(largeSize, output.Length);
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var mom = new Mom(TestPeriod);
        bool eventFired = false;

        mom.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        mom.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var mom = new Mom(source, 2);
        var results = new List<double>();

        mom.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(10, results.Count);
    }

    #endregion

    #region Calculate Method Tests

    [Fact]
    public void Calculate_ReturnsTupleWithResultsAndIndicator()
    {
        var (results, indicator) = Mom.Calculate(_gbm, TestPeriod);

        Assert.Equal(DataPoints, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var mom = new Mom(TestPeriod);
        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        mom.Prime(primeData);

        Assert.NotEqual(default, mom.Last);
        Assert.True(mom.IsHot);
    }

    [Fact]
    public void Prime_SameAsSequentialUpdates()
    {
        var mom1 = new Mom(3);
        var mom2 = new Mom(3);
        double[] data = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110];

        mom1.Prime(data);

        foreach (var value in data)
        {
            mom2.Update(new TValue(DateTime.MinValue, value));
        }

        Assert.Equal(mom1.Last.Value, mom2.Last.Value, 10);
    }

    #endregion
}
