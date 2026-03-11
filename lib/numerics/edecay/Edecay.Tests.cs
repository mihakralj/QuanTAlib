using Xunit;

namespace QuanTAlib.Tests;

public class EdecayTests
{
    private readonly TSeries _gbm;
    private const int TestPeriod = 5;
    private const int DataPoints = 100;

    public EdecayTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var edecay = new Edecay(TestPeriod);
        Assert.Equal($"Edecay({TestPeriod})", edecay.Name);
        Assert.Equal(1, edecay.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Edecay(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Edecay(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var edecay = new Edecay(source, TestPeriod);
        Assert.NotNull(edecay);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstBar_ReturnsInputValue()
    {
        var edecay = new Edecay(TestPeriod);
        var tv = edecay.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, tv.Value);
    }

    [Fact]
    public void Update_DecayingValues_OutputDecaysExponentially()
    {
        var edecay = new Edecay(5); // scale = 4/5 = 0.8
        var time = DateTime.UtcNow;

        // First bar at 1.0
        edecay.Update(new TValue(time, 1.0), true);

        // Next bars at 0.0 — output should decay by ×0.8 per bar
        var tv1 = edecay.Update(new TValue(time.AddSeconds(1), 0.0), true);
        Assert.Equal(0.8, tv1.Value, 10); // 1.0 * 0.8

        var tv2 = edecay.Update(new TValue(time.AddSeconds(2), 0.0), true);
        Assert.Equal(0.64, tv2.Value, 10); // 0.8 * 0.8

        var tv3 = edecay.Update(new TValue(time.AddSeconds(3), 0.0), true);
        Assert.Equal(0.512, tv3.Value, 10); // 0.64 * 0.8
    }

    [Fact]
    public void Update_RisingInput_FollowsInput()
    {
        var edecay = new Edecay(5);
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 100.0), true);
        var tv = edecay.Update(new TValue(time.AddSeconds(1), 105.0), true);
        Assert.Equal(105.0, tv.Value, 10); // input > decayed, so follows input
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var edecay = new Edecay(TestPeriod);
        edecay.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, edecay.Last.Value, 10);
    }

    [Fact]
    public void IsHot_ReturnsFalseBeforeFirstBar()
    {
        var edecay = new Edecay(TestPeriod);
        Assert.False(edecay.IsHot);
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterFirstBar()
    {
        var edecay = new Edecay(TestPeriod);
        edecay.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(edecay.IsHot);
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var edecay = new Edecay(TestPeriod);
        Assert.Equal($"Edecay({TestPeriod})", edecay.Name);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var edecay = new Edecay(TestPeriod);
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 100.0), true);
        edecay.Update(new TValue(time.AddSeconds(1), 105.0), true);
        edecay.Update(new TValue(time.AddSeconds(2), 110.0), true);

        Assert.NotEqual(default, edecay.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var edecay = new Edecay(5); // scale = 0.8
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 1.0), true);
        var first = edecay.Update(new TValue(time.AddSeconds(1), 0.0), true);

        // Correct same bar with different value
        var corrected = edecay.Update(new TValue(time.AddSeconds(1), 0.5), false);

        // first: max(0.0, 1.0*0.8)=0.8
        Assert.Equal(0.8, first.Value, 10);
        // corrected: max(0.5, 1.0*0.8)=0.8
        Assert.Equal(0.8, corrected.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var edecay = new Edecay(5);
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 1.0), true);
        var baseline = edecay.Update(new TValue(time.AddSeconds(1), 0.5), true);

        // Make several corrections
        edecay.Update(new TValue(time.AddSeconds(1), 0.9), false);
        edecay.Update(new TValue(time.AddSeconds(1), 0.1), false);
        var restored = edecay.Update(new TValue(time.AddSeconds(1), 0.5), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var edecay = new Edecay(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            edecay.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        edecay.Reset();

        Assert.Equal(default, edecay.Last);
        Assert.False(edecay.IsHot);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var edecay = new Edecay(5);
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 1.0), true);
        var afterNaN = edecay.Update(new TValue(time.AddSeconds(1), double.NaN), true);

        Assert.True(double.IsFinite(afterNaN.Value));
        // NaN uses last valid (1.0), so max(1.0, 1.0*0.8)=1.0
        Assert.Equal(1.0, afterNaN.Value, 10);
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var edecay = new Edecay(5);
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 1.0), true);
        var afterInf = edecay.Update(new TValue(time.AddSeconds(1), double.PositiveInfinity), true);

        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var edecay = new Edecay(TestPeriod);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var value = i % 3 == 0 ? double.NaN : 100.0 + i;
            var tv = edecay.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region Consistency Tests (All 4 modes must match)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Edecay.Batch(_gbm, TestPeriod);

        // Mode 2: Streaming
        var streamingEdecay = new Edecay(TestPeriod);
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingEdecay.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        Span<double> spanOutput = stackalloc double[DataPoints];
        Edecay.Batch(_gbm.Values, spanOutput, TestPeriod);

        // Mode 4: Event-driven
        var eventEdecay = new Edecay(TestPeriod);
        var eventResult = new TSeries(DataPoints);
        eventEdecay.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventEdecay.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
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
            Edecay.Batch(empty, output, TestPeriod);
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
            Edecay.Batch(source, output, TestPeriod);
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
            Edecay.Batch(source, output, 0);
        });
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Edecay.Batch(_gbm, TestPeriod);

        Span<double> spanOutput = stackalloc double[DataPoints];
        Edecay.Batch(_gbm.Values, spanOutput, TestPeriod);

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

        Edecay.Batch(source, output, TestPeriod);

        Assert.Equal(largeSize, output.Length);
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var edecay = new Edecay(TestPeriod);
        bool eventFired = false;

        edecay.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        edecay.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var edecay = new Edecay(source, 2);
        var results = new List<double>();

        edecay.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), true);
        }

        Assert.Equal(10, results.Count);
    }

    #endregion

    #region Edecay-Specific Tests

    [Fact]
    public void Edecay_Period1_DecaysToZero()
    {
        var edecay = new Edecay(1); // scale = 0/1 = 0.0
        var time = DateTime.UtcNow;

        edecay.Update(new TValue(time, 5.0), true);
        var tv = edecay.Update(new TValue(time.AddSeconds(1), 0.0), true);
        // max(0.0, 5.0*0.0) = 0.0
        Assert.Equal(0.0, tv.Value, 10);
    }

    [Fact]
    public void Edecay_ConstantInput_OutputEqualsInput()
    {
        var edecay = new Edecay(5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var tv = edecay.Update(new TValue(time.AddSeconds(i), 100.0), true);
            Assert.Equal(100.0, tv.Value, 10);
        }
    }

    [Fact]
    public void Edecay_OutputNeverBelowInput()
    {
        var edecay = new Edecay(10);
        var time = DateTime.UtcNow;
        var rng = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            double input = rng.NextDouble() * 200;
            var tv = edecay.Update(new TValue(time.AddSeconds(i), input), true);
            Assert.True(tv.Value >= input || Math.Abs(tv.Value - input) < 1e-10,
                $"Output {tv.Value} should be >= input {input}");
        }
    }

    [Fact]
    public void Edecay_DiffersFromLinearDecay()
    {
        var edecay = new Edecay(5); // scale = 0.8
        var decay = new Decay(5);   // scale = 0.2
        var time = DateTime.UtcNow;

        // Start both at 100
        edecay.Update(new TValue(time, 100.0), true);
        decay.Update(new TValue(time, 100.0), true);

        // Feed 0.0 and compare
        var e1 = edecay.Update(new TValue(time.AddSeconds(1), 0.0), true);
        var d1 = decay.Update(new TValue(time.AddSeconds(1), 0.0), true);

        // Edecay: max(0, 100*0.8) = 80
        // Decay: max(0, 100-0.2) = 99.8
        Assert.Equal(80.0, e1.Value, 10);
        Assert.Equal(99.8, d1.Value, 10);
        Assert.NotEqual(e1.Value, d1.Value);
    }

    #endregion
}
