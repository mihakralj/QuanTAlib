using Xunit;

namespace QuanTAlib.Tests;

public class SamTests
{
    private readonly TSeries _gbm;
    private const int DataPoints = 500;

    public SamTests()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        var bars = gbm.Fetch(DataPoints, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        _gbm = bars.Close;
    }

    #region A) Constructor Validation

    [Fact]
    public void Constructor_WithDefaults_SetsProperties()
    {
        var sam = new Sam();
        Assert.Equal("Sam(0.07,8)", sam.Name);
        Assert.Equal(100, sam.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithCustomParams_SetsProperties()
    {
        var sam = new Sam(alpha: 0.1, cutoff: 12);
        Assert.Equal("Sam(0.1,12)", sam.Name);
    }

    [Fact]
    public void Constructor_WithZeroAlpha_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sam(alpha: 0));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativeAlpha_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sam(alpha: -0.1));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithAlphaGreaterThanOne_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sam(alpha: 1.5));
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithAlphaOne_DoesNotThrow()
    {
        var sam = new Sam(alpha: 1.0);
        Assert.NotNull(sam);
    }

    [Fact]
    public void Constructor_WithCutoffLessThanTwo_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Sam(cutoff: 1));
        Assert.Equal("cutoff", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithCutoffTwo_DoesNotThrow()
    {
        var sam = new Sam(cutoff: 2);
        Assert.NotNull(sam);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries(DataPoints);
        var sam = new Sam(source);
        Assert.NotNull(sam);
    }

    #endregion

    #region B) Basic Calculation

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var sam = new Sam();
        var tv = sam.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(tv.Value));
    }

    [Fact]
    public void Update_FirstValue_ReturnsZero()
    {
        var sam = new Sam();
        var tv = sam.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, tv.Value);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var sam = new Sam();
        sam.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(sam.Last.Value));
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var sam = new Sam();
        Assert.Equal("Sam(0.07,8)", sam.Name);
    }

    [Fact]
    public void DominantCycle_IsAccessible()
    {
        var sam = new Sam();
        for (int i = 0; i < 200; i++)
        {
            sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1));
        }
        Assert.True(sam.DominantCycle > 0);
    }

    [Fact]
    public void Update_ConstantInput_ProducesZeroOutput()
    {
        var sam = new Sam();
        TValue result = default;
        for (int i = 0; i < 300; i++)
        {
            result = sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0), true);
        }
        // Constant input → zero momentum → smoothed zero output
        Assert.Equal(0.0, result.Value, 8);
    }

    #endregion

    #region C) State + Bar Correction (critical)

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        sam.Update(new TValue(time, 100.0), true);
        sam.Update(new TValue(time.AddSeconds(1), 105.0), true);
        sam.Update(new TValue(time.AddSeconds(2), 110.0), true);

        Assert.NotEqual(default, sam.Last);
    }

    [Fact]
    public void Update_WithIsNewFalse_UpdatesCurrentState()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        // Feed enough data to get past trivial warmup
        for (int i = 0; i < 120; i++)
        {
            sam.Update(new TValue(time.AddSeconds(i), 100.0 + Math.Sin(i * 0.3) * 10), true);
        }

        var first = sam.Update(new TValue(time.AddSeconds(120), 115.0), true);
        var corrected = sam.Update(new TValue(time.AddSeconds(120), 130.0), false);

        // Different input should produce different output
        Assert.NotEqual(first.Value, corrected.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresPreviousState()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 120; i++)
        {
            sam.Update(new TValue(time.AddSeconds(i), 100.0 + Math.Sin(i * 0.3) * 10), true);
        }

        var baseline = sam.Update(new TValue(time.AddSeconds(120), 105.0), true);

        // Apply multiple corrections
        sam.Update(new TValue(time.AddSeconds(120), 110.0), false);
        sam.Update(new TValue(time.AddSeconds(120), 120.0), false);
        var restored = sam.Update(new TValue(time.AddSeconds(120), 105.0), false);

        Assert.Equal(baseline.Value, restored.Value, 10);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValidTracking()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 120; i++)
        {
            sam.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }

        sam.Reset();

        Assert.Equal(default, sam.Last);
        Assert.False(sam.IsHot);
    }

    #endregion

    #region D) Warmup / Convergence

    [Fact]
    public void IsHot_ReturnsFalseDuringWarmup()
    {
        var sam = new Sam();
        for (int i = 0; i < 99; i++)
        {
            sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            Assert.False(sam.IsHot);
        }
    }

    [Fact]
    public void IsHot_ReturnsTrueAfterWarmup()
    {
        var sam = new Sam();
        for (int i = 0; i < 101; i++)
        {
            sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }
        Assert.True(sam.IsHot);
    }

    [Fact]
    public void WarmupPeriod_Is100()
    {
        var sam = new Sam();
        Assert.Equal(100, sam.WarmupPeriod);
    }

    #endregion

    #region E) Robustness (critical)

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 120; i++)
        {
            sam.Update(new TValue(time.AddSeconds(i), 100.0 + Math.Sin(i * 0.2) * 5), true);
        }

        var afterNaN = sam.Update(new TValue(time.AddSeconds(120), double.NaN), true);
        Assert.True(double.IsFinite(afterNaN.Value));
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 120; i++)
        {
            sam.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.1), true);
        }

        var afterInf = sam.Update(new TValue(time.AddSeconds(120), double.PositiveInfinity), true);
        Assert.True(double.IsFinite(afterInf.Value));
    }

    [Fact]
    public void Update_BatchNaN_HandlesSafely()
    {
        var sam = new Sam();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 200; i++)
        {
            var value = i % 5 == 0 ? double.NaN : 100.0 + i * 0.1;
            var tv = sam.Update(new TValue(time.AddSeconds(i), value), true);
            Assert.True(double.IsFinite(tv.Value));
        }
    }

    #endregion

    #region F) Consistency — All 4 modes must match (critical)

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        // Mode 1: Batch via TSeries
        var batchResult = Sam.Batch(_gbm);

        // Mode 2: Streaming
        var streamingSam = new Sam();
        var streamingResult = new TSeries(DataPoints);
        for (int i = 0; i < _gbm.Count; i++)
        {
            var tv = streamingSam.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
            streamingResult.Add(tv, true);
        }

        // Mode 3: Span-based
        double[] spanOutput = new double[DataPoints];
        Sam.Batch(_gbm.Values, spanOutput, 0.07, 8);

        // Mode 4: Event-driven
        var eventSam = new Sam();
        var eventResult = new TSeries(DataPoints);
        eventSam.Pub += (object? _, in TValueEventArgs e) => eventResult.Add(e.Value, e.IsNew);
        for (int i = 0; i < _gbm.Count; i++)
        {
            eventSam.Update(new TValue(_gbm[i].Time, _gbm[i].Value), true);
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

    #region G) Span API Tests

    [Fact]
    public void Calculate_Span_ValidatesOutputLength()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[3]; // too short
            Sam.Batch(source, output);
        });
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesAlpha()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[5];
            Sam.Batch(source, output, alpha: 0);
        });
        Assert.Equal("alpha", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesCutoff()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<double> source = stackalloc double[] { 1, 2, 3, 4, 5 };
            Span<double> output = stackalloc double[5];
            Sam.Batch(source, output, cutoff: 1);
        });
        Assert.Equal("cutoff", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_MatchesTSeries()
    {
        var batchResult = Sam.Batch(_gbm);

        double[] spanOutput = new double[DataPoints];
        Sam.Batch(_gbm.Values, spanOutput);

        for (int i = 0; i < DataPoints; i++)
        {
            Assert.Equal(batchResult[i].Value, spanOutput[i], 10);
        }
    }

    [Fact]
    public void Calculate_Span_HandlesNaN()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            source[i] = i % 7 == 0 ? double.NaN : 100.0 + i;
        }

        Sam.Batch(source, output);

        for (int i = 0; i < 100; i++)
        {
            Assert.True(double.IsFinite(output[i]));
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
            source[i] = 100.0 + Math.Sin(i * 0.1) * 20;
        }

        Sam.Batch(source, output);

        Assert.Equal(largeSize, output.Length);
        for (int i = 0; i < largeSize; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Calculate_Span_EmptyInput_DoesNotThrow()
    {
        ReadOnlySpan<double> source = [];
        Span<double> output = [];
        Sam.Batch(source, output);
        Assert.True(true); // Verify no exception thrown
    }

    #endregion

    #region H) Chainability

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var sam = new Sam();
        bool eventFired = false;

        sam.Pub += (object? _, in TValueEventArgs e) => eventFired = true;
        sam.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var source = new TSeries(10);
        var sam = new Sam(source);
        var results = new List<double>();

        sam.Pub += (object? _, in TValueEventArgs e) => results.Add(e.Value.Value);

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
        var (results, indicator) = Sam.Calculate(_gbm);

        Assert.Equal(DataPoints, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_InitializesState()
    {
        var sam = new Sam();
        double[] primeData = new double[150];
        for (int i = 0; i < 150; i++)
        {
            primeData[i] = 100.0 + Math.Sin(i * 0.2) * 10;
        }

        sam.Prime(primeData);

        Assert.NotEqual(default, sam.Last);
        Assert.True(sam.IsHot);
    }

    [Fact]
    public void Prime_SameAsSequentialUpdates()
    {
        var sam1 = new Sam();
        var sam2 = new Sam();
        double[] data = new double[150];
        for (int i = 0; i < 150; i++)
        {
            data[i] = 100.0 + Math.Sin(i * 0.2) * 10;
        }

        sam1.Prime(data);

        foreach (var value in data)
        {
            sam2.Update(new TValue(DateTime.MinValue, value));
        }

        Assert.Equal(sam1.Last.Value, sam2.Last.Value, 10);
    }

    #endregion

    #region SAM-Specific Behavior Tests

    [Fact]
    public void Sam_TrendingInput_ProducesNonZeroOutput()
    {
        var sam = new Sam();
        TValue result = default;

        for (int i = 0; i < 200; i++)
        {
            result = sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 2), true);
        }

        // Strong trend should produce non-zero smoothed momentum
        Assert.NotEqual(0.0, result.Value);
    }

    [Fact]
    public void Sam_SinusoidalInput_OscillatesAroundZero()
    {
        var sam = new Sam();
        int positiveCount = 0;
        int negativeCount = 0;

        for (int i = 0; i < 500; i++)
        {
            var result = sam.Update(
                new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.2) * 20), true);

            if (sam.IsHot)
            {
                if (result.Value > 0)
                {
                    positiveCount++;
                }
                else if (result.Value < 0)
                {
                    negativeCount++;
                }
            }
        }

        // For sinusoidal input, should oscillate both positive and negative
        Assert.True(positiveCount > 0, "Expected some positive values");
        Assert.True(negativeCount > 0, "Expected some negative values");
    }

    [Fact]
    public void Sam_DominantCycle_StabilizesAfterWarmup()
    {
        var sam = new Sam();

        // Feed sinusoidal data with known period ~20
        for (int i = 0; i < 300; i++)
        {
            sam.Update(new TValue(DateTime.UtcNow.AddSeconds(i),
                100.0 + Math.Sin(i * 2.0 * Math.PI / 20.0) * 10), true);
        }

        // After warmup, dominant cycle should have stabilized to a finite positive value
        Assert.True(sam.DominantCycle >= 6 && sam.DominantCycle <= 50,
            $"DominantCycle {sam.DominantCycle} should be within [6, 50]");
    }

    [Fact]
    public void Sam_AllOutputFinite_WithGBMData()
    {
        var sam = new Sam();

        for (int i = 0; i < _gbm.Count; i++)
        {
            var result = sam.Update(_gbm[i]);
            Assert.True(double.IsFinite(result.Value),
                $"Non-finite value at bar {i}: {result.Value}");
        }
    }

    #endregion
}
