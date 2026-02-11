using Xunit;

namespace QuanTAlib.Tests;

public class PmoTests
{
    private readonly TSeries _gbm;
    private const int TestTimePeriods = 10;
    private const int TestSmoothPeriods = 5;
    private const int TestSignalPeriods = 3;
    private const int DataPoints = 100;

    public PmoTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriods_SetsProperties()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        Assert.Equal($"Pmo({TestTimePeriods},{TestSmoothPeriods},{TestSignalPeriods})", pmo.Name);
        Assert.Equal(TestTimePeriods + TestSmoothPeriods, pmo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultParams_UsesStandardValues()
    {
        var pmo = new Pmo();
        Assert.Equal("Pmo(35,20,10)", pmo.Name);
        Assert.Equal(55, pmo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroRocPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pmo(0, 5, 3));
        Assert.Equal("timePeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithZeroSmooth1Period_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pmo(10, 0, 3));
        Assert.Equal("smoothPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithZeroSmooth2Period_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pmo(10, 5, 0));
        Assert.Equal("signalPeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pmo(-1, 5, 3));
        Assert.Equal("timePeriods", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var pmo = new Pmo(source, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        Assert.NotNull(pmo);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstValue_ReturnsFinite()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        var tv = pmo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(tv.Value));
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToZero()
    {
        var pmo = new Pmo(5, 3, 3);
        for (int i = 0; i < 50; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }
        // Constant price → ROC% = 0 → PMO → 0
        Assert.True(Math.Abs(pmo.Last.Value) < 1e-6,
            $"PMO with constant input should converge to 0, got {pmo.Last.Value}");
    }

    [Fact]
    public void Update_RisingPrices_ReturnsPositive()
    {
        var pmo = new Pmo(5, 3, 3);
        for (int i = 0; i < 30; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 2.0), true);
        }
        Assert.True(pmo.Last.Value > 0,
            $"PMO should be positive with rising prices, got {pmo.Last.Value}");
    }

    [Fact]
    public void Update_FallingPrices_ReturnsNegative()
    {
        var pmo = new Pmo(5, 3, 3);
        for (int i = 0; i < 30; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200.0 - i * 2.0), true);
        }
        Assert.True(pmo.Last.Value < 0,
            $"PMO should be negative with falling prices, got {pmo.Last.Value}");
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        pmo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(pmo.Last.Value));
    }

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        var warmup = TestTimePeriods + TestSmoothPeriods;
        for (int i = 0; i < warmup; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(pmo.IsHot, $"Should not be hot at bar {i}");
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        var warmup = TestTimePeriods + TestSmoothPeriods;
        for (int i = 0; i <= warmup; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(pmo.IsHot);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            pmo.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
        }
        Assert.NotEqual(default, pmo.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_RollsBackState()
    {
        var pmo = new Pmo(5, 3, 3);
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 20; i++)
        {
            pmo.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.5), true);
        }

        var baseline = pmo.Update(new TValue(time.AddSeconds(20), 120.0), true);
        var corrected = pmo.Update(new TValue(time.AddSeconds(20), 115.0), false);

        Assert.NotEqual(baseline.Value, corrected.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var pmo = new Pmo(5, 3, 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            pmo.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.5), true);
        }

        var baseline = pmo.Update(new TValue(time.AddSeconds(20), 120.0), true);

        // Several corrections
        pmo.Update(new TValue(time.AddSeconds(20), 130.0), false);
        pmo.Update(new TValue(time.AddSeconds(20), 110.0), false);
        var restored = pmo.Update(new TValue(time.AddSeconds(20), 120.0), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);

        for (int i = 0; i < 30; i++)
        {
            pmo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        pmo.Reset();

        Assert.Equal(default, pmo.Last);
        Assert.False(pmo.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var pmo = new Pmo(5, 3, 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            pmo.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
        }
        var afterNaN = pmo.Update(new TValue(time.AddSeconds(15), double.NaN), true);

        Assert.True(double.IsFinite(afterNaN.Value));
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var pmo = new Pmo(5, 3, 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            pmo.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
        }
        var afterInf = pmo.Update(new TValue(time.AddSeconds(15), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var pmo = new Pmo(5, 3, 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            var value = i % 5 == 0 ? double.NaN : 100.0 + i;
            var tv = pmo.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void BatchTSeries_And_Streaming_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Pmo.Batch(_gbm, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);

        // Mode 2: Streaming
        var streamingPmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingPmo.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Compare last 50 values (post-warmup region)
        int start = Math.Max(0, DataPoints - 50);
        for (int i = start; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanBatch_And_Streaming_ProduceSameResults()
    {
        // Mode 1: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Pmo.Batch(_gbm.Values, spanOutput, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);

        // Mode 2: Streaming
        var streamingPmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        for (int i = 0; i < _gbm.Count; i++)
        {
            streamingPmo.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
        }

        // Compare last value
        Assert.Equal(spanOutput[DataPoints - 1], streamingPmo.Last.Value, 6);
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
            Pmo.Batch(empty, output, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
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
            Pmo.Batch(source, output, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
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
            Pmo.Batch(source, output, 0, TestSmoothPeriods, TestSignalPeriods);
        });
        Assert.Equal("timePeriods", ex.ParamName);
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

        Pmo.Batch(source, output, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);

        Assert.Equal(largeSize, output.Length);
        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var pmo = new Pmo(TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);
        bool eventFired = false;

        pmo.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        pmo.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var pmo = new Pmo(source, 3, 2, 2);
        var results = new List<double>();

        pmo.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(20, results.Count);
    }

    #endregion

    #region Calculate Method Tests

    [Fact]
    public void Calculate_ReturnsTupleWithResultsAndIndicator()
    {
        var (results, indicator) = Pmo.Calculate(_gbm, TestTimePeriods, TestSmoothPeriods, TestSignalPeriods);

        Assert.Equal(DataPoints, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var pmo = new Pmo(5, 3, 3);
        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
                              110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120];

        pmo.Prime(primeData);

        Assert.NotEqual(default, pmo.Last);
        Assert.True(pmo.IsHot);
    }

    [Fact]
    public void Prime_SameAsSequentialUpdates()
    {
        var pmo1 = new Pmo(5, 3, 3);
        var pmo2 = new Pmo(5, 3, 3);
        double[] data = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
                         110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120];

        pmo1.Prime(data);

        foreach (var value in data)
        {
            pmo2.Update(new TValue(DateTime.MinValue, value));
        }

        Assert.Equal(pmo1.Last.Value, pmo2.Last.Value, 10);
    }

    #endregion
}
