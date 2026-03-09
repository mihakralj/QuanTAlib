using Xunit;

namespace QuanTAlib.Tests;

public class DecayTests
{
    private readonly TSeries _gbm;
    private const int TestPeriod = 5;
    private const int DataPoints = 100;

    public DecayTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var decay = new Decay(TestPeriod);
        Assert.Equal($"Decay({TestPeriod})", decay.Name);
        Assert.Equal(1, decay.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Decay(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Decay(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var decay = new Decay(source, TestPeriod);
        Assert.NotNull(decay);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstBar_ReturnsInputValue()
    {
        var decay = new Decay(TestPeriod);
        var tv = decay.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, tv.Value);
    }

    [Fact]
    public void Update_DecayingValues_OutputDecaysLinearly()
    {
        var decay = new Decay(5); // scale = 0.2
        var time = DateTime.UtcNow;

        // First bar at 1.0
        decay.Update(new TValue(time, 1.0), true);

        // Next bars at 0.0 — output should decay by 0.2 per bar
        var tv1 = decay.Update(new TValue(time.AddSeconds(1), 0.0), true);
        Assert.Equal(0.8, tv1.Value, 10); // 1.0 - 0.2

        var tv2 = decay.Update(new TValue(time.AddSeconds(2), 0.0), true);
        Assert.Equal(0.6, tv2.Value, 10); // 0.8 - 0.2

        var tv3 = decay.Update(new TValue(time.AddSeconds(3), 0.0), true);
        Assert.Equal(0.4, tv3.Value, 10); // 0.6 - 0.2
    }

    [Fact]
    public void Update_RisingInput_FollowsInput()
    {
        var decay = new Decay(5);
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 100.0), true);
        var tv = decay.Update(new TValue(time.AddSeconds(1), 105.0), true);
        Assert.Equal(105.0, tv.Value, 10); // input > decayed, so follows input
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var decay = new Decay(TestPeriod);
        decay.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, decay.Last.Value, 10);
    }

    [Fact]
    public void IsHot_ReturnsFalseBeforeFirstBar()
    {
        var decay = new Decay(TestPeriod);
        Assert.False(decay.IsHot);
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterFirstBar()
    {
        var decay = new Decay(TestPeriod);
        decay.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(decay.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var decay = new Decay(TestPeriod);
        Assert.Equal($"Decay({TestPeriod})", decay.Name);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var decay = new Decay(TestPeriod);
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 100.0), true);
        decay.Update(new TValue(time.AddSeconds(1), 105.0), true);
        decay.Update(new TValue(time.AddSeconds(2), 110.0), true);

        Assert.NotEqual(default, decay.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var decay = new Decay(5);
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 1.0), true);
        var first = decay.Update(new TValue(time.AddSeconds(1), 0.0), true);

        // Correct same bar with different value
        var corrected = decay.Update(new TValue(time.AddSeconds(1), 0.5), false);

        // first: max(0.0, 1.0-0.2)=0.8
        Assert.Equal(0.8, first.Value, 10);
        // corrected: max(0.5, 1.0-0.2)=0.8
        Assert.Equal(0.8, corrected.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var decay = new Decay(5);
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 1.0), true);
        var baseline = decay.Update(new TValue(time.AddSeconds(1), 0.5), true);

        // Make several corrections
        decay.Update(new TValue(time.AddSeconds(1), 0.9), false);
        decay.Update(new TValue(time.AddSeconds(1), 0.1), false);
        var restored = decay.Update(new TValue(time.AddSeconds(1), 0.5), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var decay = new Decay(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            decay.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        decay.Reset();

        Assert.Equal(default, decay.Last);
        Assert.False(decay.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var decay = new Decay(5);
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 1.0), true);
        var afterNaN = decay.Update(new TValue(time.AddSeconds(1), double.NaN), true);

        Assert.True(double.IsFinite(afterNaN.Value));
        // NaN uses last valid (1.0), so max(1.0, 1.0-0.2)=1.0
        Assert.Equal(1.0, afterNaN.Value, 10);
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var decay = new Decay(5);
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 1.0), true);
        var afterInf = decay.Update(new TValue(time.AddSeconds(1), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var decay = new Decay(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var value = i % 3 == 0 ? double.NaN : 100.0 + i;
            var tv = decay.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region Consistency Tests (All 4 modes must match)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Decay.Batch(_gbm, TestPeriod);

        // Mode 2: Streaming
        var streamingDecay = new Decay(TestPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingDecay.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Decay.Batch(_gbm.Values, spanOutput, TestPeriod);

        // Mode 4: Event-driven
        var eventDecay = new Decay(TestPeriod);
        var eventResult = new TSeries(DataPoints);
        eventDecay.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventDecay.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
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
            Decay.Batch(empty, output, TestPeriod);
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
            Decay.Batch(source, output, TestPeriod);
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
            Decay.Batch(source, output, 0);
        });
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Decay.Batch(_gbm, TestPeriod);

        Span<double> spanOutput = stackalloc double[DataPoints];
        Decay.Batch(_gbm.Values, spanOutput, TestPeriod);

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

        Decay.Batch(source, output, TestPeriod);

        Assert.Equal(largeSize, output.Length);
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var decay = new Decay(TestPeriod);
        bool eventFired = false;

        decay.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        decay.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var decay = new Decay(source, 2);
        var results = new List<double>();

        decay.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(10, results.Count);
    }

    #endregion

    #region Decay-Specific Tests

    [Fact]
    public void Decay_Period1_DecaysByOneEachBar()
    {
        var decay = new Decay(1); // scale = 1.0
        var time = DateTime.UtcNow;

        decay.Update(new TValue(time, 5.0), true);
        var tv = decay.Update(new TValue(time.AddSeconds(1), 0.0), true);
        // max(0.0, 5.0-1.0) = 4.0
        Assert.Equal(4.0, tv.Value, 10);
    }

    [Fact]
    public void Decay_ConstantInput_OutputEqualsInput()
    {
        var decay = new Decay(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var tv = decay.Update(new TValue(time.AddSeconds(i), 100.0), true);
            Assert.Equal(100.0, tv.Value, 10);
        }
    }

    [Fact]
    public void Decay_OutputNeverBelowInput()
    {
        var decay = new Decay(10);
        var time = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            double input = rng.NextDouble() * 200;
            var tv = decay.Update(new TValue(time.AddSeconds(i), input), true);
            Assert.True(tv.Value >= input || Math.Abs(tv.Value - input) < 1e-10,
                $"Output {tv.Value} should be >= input {input}");
        }
    }

    #endregion
}
