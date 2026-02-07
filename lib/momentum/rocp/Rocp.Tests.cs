using Xunit;

namespace QuanTAlib.Tests;

public class RocpTests
{
    private readonly TSeries _gbm;
    private const int TestPeriod = 9;
    private const int DataPoints = 100;

    public RocpTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var rocp = new Rocp(TestPeriod);
        Assert.Equal($"Rocp({TestPeriod})", rocp.Name);
        Assert.Equal(TestPeriod + 1, rocp.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rocp(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rocp(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var rocp = new Rocp(source, TestPeriod);
        Assert.NotNull(rocp);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsCorrectValue()
    {
        var rocp = new Rocp(TestPeriod);
        var tv = rocp.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, tv.Value);
    }

    [Fact]
    public void Update_FirstValues_ReturnsZero()
    {
        var rocp = new Rocp(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            var tv = rocp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.Equal(0.0, tv.Value);
        }
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsPercentage()
    {
        var rocp = new Rocp(2); // period=2
        var values = new double[] { 100, 102, 105, 103, 110 };

        for (int i = 0; i < values.Length; i++)
        {
            var tv = rocp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), values[i]), true);

            if (i < 2)
            {
                Assert.Equal(0.0, tv.Value); // warmup period
            }
            else
            {
                // percentage: 100 * (current - past) / past
                double expected = 100.0 * (values[i] - values[i - 2]) / values[i - 2];
                Assert.Equal(expected, tv.Value, 10);
            }
        }
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var rocp = new Rocp(TestPeriod);
        rocp.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, rocp.Last.Value);
    }

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var rocp = new Rocp(TestPeriod);
        for (int i = 0; i < TestPeriod; i++)
        {
            rocp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(rocp.IsHot);
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var rocp = new Rocp(TestPeriod);
        for (int i = 0; i <= TestPeriod; i++)
        {
            rocp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(rocp.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var rocp = new Rocp(TestPeriod);
        Assert.Equal($"Rocp({TestPeriod})", rocp.Name);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var rocp = new Rocp(TestPeriod);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        rocp.Update(new TValue(time.AddSeconds(1), 105.0), true);
        rocp.Update(new TValue(time.AddSeconds(2), 110.0), true);

        Assert.NotEqual(default, rocp.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var rocp = new Rocp(2);
        var time = DateTime.UtcNow;

        // Warmup
        rocp.Update(new TValue(time, 100.0), true);
        rocp.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var first = rocp.Update(new TValue(time.AddSeconds(2), 105.0), true);

        // Update same bar with different value
        var corrected = rocp.Update(new TValue(time.AddSeconds(2), 110.0), false);

        Assert.NotEqual(first.Value, corrected.Value);
        // first: 100 * (105-100)/100 = 5%
        // corrected: 100 * (110-100)/100 = 10%
        Assert.Equal(5.0, first.Value, 10);
        Assert.Equal(10.0, corrected.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var rocp = new Rocp(2);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        rocp.Update(new TValue(time.AddSeconds(1), 102.0), true);
        var baseline = rocp.Update(new TValue(time.AddSeconds(2), 105.0), true);

        rocp.Update(new TValue(time.AddSeconds(2), 110.0), false);
        rocp.Update(new TValue(time.AddSeconds(2), 115.0), false);
        var restored = rocp.Update(new TValue(time.AddSeconds(2), 105.0), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var rocp = new Rocp(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i <= TestPeriod; i++)
        {
            rocp.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        rocp.Reset();

        Assert.Equal(default, rocp.Last);
        Assert.False(rocp.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var rocp = new Rocp(2);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        rocp.Update(new TValue(time.AddSeconds(1), 102.0), true);
        _ = rocp.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterNaN = rocp.Update(new TValue(time.AddSeconds(3), double.NaN), true);

        // NaN uses last valid (105), so: 100 * (105-102)/102 ≈ 2.94%
        Assert.True(double.IsFinite(afterNaN.Value));
        Assert.Equal(100.0 * (105.0 - 102.0) / 102.0, afterNaN.Value, 10);
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var rocp = new Rocp(2);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        rocp.Update(new TValue(time.AddSeconds(1), 102.0), true);
        rocp.Update(new TValue(time.AddSeconds(2), 105.0), true);
        var afterInf = rocp.Update(new TValue(time.AddSeconds(3), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var rocp = new Rocp(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var value = i % 3 == 0 ? double.NaN : 100.0 + i;
            var tv = rocp.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    [Fact]
    public void Update_WithZeroPastValue_ReturnsZero()
    {
        var rocp = new Rocp(2);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 0.0), true);
        rocp.Update(new TValue(time.AddSeconds(1), 50.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(2), 100.0), true);

        // Division by zero: returns 0.0 as safe default
        Assert.Equal(0.0, result.Value);
    }

    #endregion

    #region Consistency Tests (All 4 modes must match)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Rocp.Calculate(_gbm, TestPeriod);

        // Mode 2: Streaming
        var streamingRocp = new Rocp(TestPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingRocp.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Rocp.Calculate(_gbm.Values, spanOutput, TestPeriod);

        // Mode 4: Event-driven
        var eventRocp = new Rocp(TestPeriod);
        var eventResult = new TSeries(DataPoints);
        eventRocp.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventRocp.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
        }

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
            Rocp.Calculate(empty, output, TestPeriod);
        });
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesOutputLength()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[3];
            Rocp.Calculate(source, output, TestPeriod);
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
            Rocp.Calculate(source, output, 0);
        });
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Rocp.Calculate(_gbm, TestPeriod);

        Span<double> spanOutput = stackalloc double[DataPoints];
        Rocp.Calculate(_gbm.Values, spanOutput, TestPeriod);

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
            source[i] = 100.0 + i * 0.1;
        }

        Rocp.Calculate(source, output, TestPeriod);
        Assert.Equal(largeSize, output.Length);
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var rocp = new Rocp(TestPeriod);
        bool eventFired = false;

        rocp.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        rocp.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var rocp = new Rocp(source, 2);
        var results = new List<double>();

        rocp.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(10, results.Count);
    }

    #endregion

    #region Mathematical Properties Tests

    [Fact]
    public void Update_TenPercentIncrease_ReturnsTen()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 110.0), true);

        // 100 * (110 - 100) / 100 = 10%
        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Update_TenPercentDecrease_ReturnsNegativeTen()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 90.0), true);

        // 100 * (90 - 100) / 100 = -10%
        Assert.Equal(-10.0, result.Value, 10);
    }

    [Fact]
    public void Update_PriceDoubled_Returns100()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 50.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 100.0), true);

        // 100 * (100 - 50) / 50 = 100%
        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_PriceHalved_ReturnsNegative50()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 50.0), true);

        // 100 * (50 - 100) / 100 = -50%
        Assert.Equal(-50.0, result.Value, 10);
    }

    [Fact]
    public void Update_NoChange_ReturnsZero()
    {
        var rocp = new Rocp(1);
        var time = DateTime.UtcNow;

        rocp.Update(new TValue(time, 100.0), true);
        var result = rocp.Update(new TValue(time.AddSeconds(1), 100.0), true);

        Assert.Equal(0.0, result.Value, 10);
    }

    #endregion
}
