using Xunit;

namespace QuanTAlib.Tests;

public class PpoTests
{
    private readonly TSeries _gbm;
    private const int TestFastPeriod = 5;
    private const int TestSlowPeriod = 10;
    private const int TestSignalPeriod = 3;
    private const int DataPoints = 100;

    public PpoTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriods_SetsProperties()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        Assert.Equal($"Ppo({TestFastPeriod},{TestSlowPeriod},{TestSignalPeriod})", ppo.Name);
        Assert.Equal(TestSlowPeriod + TestSignalPeriod, ppo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultParams_UsesStandardValues()
    {
        var ppo = new Ppo();
        Assert.Equal("Ppo(12,26,9)", ppo.Name);
        Assert.Equal(35, ppo.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroFastPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ppo(0, 10, 3));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithZeroSlowPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ppo(5, 0, 3));
        Assert.Equal("slowPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithZeroSignalPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ppo(5, 10, 0));
        Assert.Equal("signalPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_FastNotLessThanSlow_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ppo(10, 10, 3));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_FastGreaterThanSlow_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ppo(15, 10, 3));
        Assert.Equal("fastPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var ppo = new Ppo(source, TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        Assert.NotNull(ppo);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstValue_ReturnsFinite()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var tv = ppo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(tv.Value));
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToZero()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < 80; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }
        // Constant price → FastEMA = SlowEMA → PPO = 0
        Assert.True(Math.Abs(ppo.Last.Value) < 1e-6,
            $"PPO with constant input should converge to 0, got {ppo.Last.Value}");
    }

    [Fact]
    public void Signal_IsAccessible()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < 20; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }
        Assert.True(double.IsFinite(ppo.Signal.Value));
    }

    [Fact]
    public void Histogram_IsAccessible()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < 20; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }
        Assert.True(double.IsFinite(ppo.Histogram.Value));
    }

    [Fact]
    public void Histogram_EqualsPpoMinusSignal()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < 30; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.5), true);
        }
        Assert.Equal(ppo.Last.Value - ppo.Signal.Value, ppo.Histogram.Value, 10);
    }

    [Fact]
    public void Update_RisingPrices_ReturnsPositive()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < 40; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 2.0), true);
        }
        Assert.True(ppo.Last.Value > 0,
            $"PPO should be positive with rising prices, got {ppo.Last.Value}");
    }

    [Fact]
    public void Update_FallingPrices_ReturnsNegative()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < 40; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 300.0 - i * 2.0), true);
        }
        Assert.True(ppo.Last.Value < 0,
            $"PPO should be negative with falling prices, got {ppo.Last.Value}");
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        ppo.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(ppo.Last.Value));
    }

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        // It needs at least slow period bars before fast & slow EMAs are both hot
        for (int i = 0; i < TestSlowPeriod; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(ppo.IsHot, $"Should not be hot at bar {i}");
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        for (int i = 0; i < TestSlowPeriod + TestSignalPeriod + 5; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(ppo.IsHot);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ppo.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
        }
        Assert.NotEqual(default, ppo.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_RollsBackState()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 25; i++)
        {
            ppo.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.5), true);
        }

        var baseline = ppo.Update(new TValue(time.AddSeconds(25), 120.0), true);
        var corrected = ppo.Update(new TValue(time.AddSeconds(25), 115.0), false);

        Assert.NotEqual(baseline.Value, corrected.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 25; i++)
        {
            ppo.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.5), true);
        }

        var baseline = ppo.Update(new TValue(time.AddSeconds(25), 120.0), true);

        ppo.Update(new TValue(time.AddSeconds(25), 130.0), false);
        ppo.Update(new TValue(time.AddSeconds(25), 110.0), false);
        var restored = ppo.Update(new TValue(time.AddSeconds(25), 120.0), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);

        for (int i = 0; i < 30; i++)
        {
            ppo.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        ppo.Reset();

        Assert.Equal(default, ppo.Last);
        Assert.Equal(default, ppo.Signal);
        Assert.Equal(default, ppo.Histogram);
        Assert.False(ppo.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ppo.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
        }
        var afterNaN = ppo.Update(new TValue(time.AddSeconds(20), double.NaN), true);

        Assert.True(double.IsFinite(afterNaN.Value));
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            ppo.Update(new TValue(time.AddSeconds(i), 100.0 + i), true);
        }
        var afterInf = ppo.Update(new TValue(time.AddSeconds(20), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 30; i++)
        {
            var value = i % 5 == 0 ? double.NaN : 100.0 + i;
            var tv = ppo.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void BatchTSeries_And_Streaming_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Ppo.Batch(_gbm, TestFastPeriod, TestSlowPeriod, TestSignalPeriod);

        // Mode 2: Streaming
        var streamingPpo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingPpo.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Compare last 50 values (post-warmup)
        int start = Math.Max(0, DataPoints - 50);
        for (int i = start; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanBatch_ProducesFiniteResults()
    {
        Span<double> spanOutput = stackalloc double[DataPoints];
        Ppo.Batch(_gbm.Values, spanOutput, TestFastPeriod, TestSlowPeriod);

        // Last value should be finite
        Assert.True(double.IsFinite(spanOutput[DataPoints - 1]));
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Calculate_Span_ValidatesMismatchedLengths()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[3]; // different length
            Ppo.Batch(source, output, TestFastPeriod, TestSlowPeriod);
        });
        Assert.Equal("destination", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesPeriod()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[5];
            Ppo.Batch(source, output, 0, TestSlowPeriod);
        });
        Assert.Contains("period", ex.Message, StringComparison.OrdinalIgnoreCase);
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

        Ppo.Batch(source, output, TestFastPeriod, TestSlowPeriod);

        Assert.Equal(largeSize, output.Length);
        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        bool eventFired = false;

        ppo.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        ppo.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var ppo = new Ppo(source, 2, 5, 3);
        var results = new List<double>();

        ppo.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

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
        var (results, indicator) = Ppo.Calculate(_gbm, TestFastPeriod, TestSlowPeriod, TestSignalPeriod);

        Assert.Equal(DataPoints, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var ppo = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        double[] primeData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
                              110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120];

        ppo.Prime(primeData);

        Assert.NotEqual(default, ppo.Last);
        Assert.True(ppo.IsHot);
    }

    [Fact]
    public void Prime_SameAsSequentialUpdates()
    {
        var ppo1 = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        var ppo2 = new Ppo(TestFastPeriod, TestSlowPeriod, TestSignalPeriod);
        double[] data = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109,
                         110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120];

        ppo1.Prime(data);

        foreach (var value in data)
        {
            ppo2.Update(new TValue(DateTime.MinValue, value));
        }

        Assert.Equal(ppo1.Last.Value, ppo2.Last.Value, 10);
    }

    #endregion
}
