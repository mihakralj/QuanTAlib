namespace QuanTAlib.Tests;

public class ParzenTests
{
    private const int DefaultPeriod = 14;
    private const double Epsilon = 1e-10;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    private readonly TSeries _data = MakeSeries();

    // ── A) Constructor validation ──────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    public void Constructor_InvalidPeriod_Throws(int period)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Parzen(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(14)]
    [InlineData(100)]
    public void Constructor_ValidPeriod_Succeeds(int period)
    {
        var parzen = new Parzen(period);
        Assert.Contains(period.ToString(System.Globalization.CultureInfo.InvariantCulture), parzen.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DefaultName()
    {
        var parzen = new Parzen(14);
        Assert.Equal("Parzen(14)", parzen.Name);
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<NullReferenceException>(() => new Parzen(null!, DefaultPeriod));
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var parzen = new Parzen(DefaultPeriod);
        var result = parzen.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var parzen = new Parzen(DefaultPeriod);
        parzen.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(parzen.Last.Value));
    }

    [Fact]
    public void Name_IsCorrect()
    {
        var parzen = new Parzen(20);
        Assert.Equal("Parzen(20)", parzen.Name);
    }

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var parzen = new Parzen(DefaultPeriod);
        foreach (var tv in _data)
        {
            var result = parzen.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var parzen = new Parzen(5);
        var now = DateTime.UtcNow;
        parzen.Update(new TValue(now, 10.0), isNew: true);
        parzen.Update(new TValue(now.AddMinutes(1), 20.0), isNew: true);
        Assert.True(double.IsFinite(parzen.Last.Value));
    }

    [Fact]
    public void IsNew_False_DoesNotAdvanceBuffer()
    {
        // The Parzen window has zero weight at the boundary (|u|=1 → 2*(1-1)³=0),
        // so the newest bar can have zero weight. Test that isNew=false does not
        // advance the buffer by verifying state is preserved after correction.
        var parzen = new Parzen(7);
        var src = MakeSeries(20);

        for (int i = 0; i < src.Count; i++)
        {
            parzen.Update(src[i], isNew: true);
        }

        double original = parzen.Last.Value;

        // Multiple corrections should not change the final result when
        // we restore the original value
        parzen.Update(new TValue(src[src.Count - 1].Time, 500.0), isNew: false);
        parzen.Update(new TValue(src[src.Count - 1].Time, src[src.Count - 1].Value), isNew: false);

        Assert.Equal(original, parzen.Last.Value, Epsilon);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var parzen = new Parzen(14);
        var src = MakeSeries(30);

        for (int i = 0; i < src.Count; i++)
        {
            parzen.Update(src[i], isNew: true);
        }

        double original = parzen.Last.Value;

        for (int c = 0; c < 5; c++)
        {
            parzen.Update(new TValue(src[src.Count - 1].Time, 200.0 + c), isNew: false);
        }

        // Restore original value
        parzen.Update(new TValue(src[src.Count - 1].Time, src[src.Count - 1].Value), isNew: false);
        Assert.Equal(original, parzen.Last.Value, Epsilon);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var parzen = new Parzen(DefaultPeriod);
        foreach (var tv in _data)
        {
            parzen.Update(tv);
        }

        parzen.Reset();
        Assert.False(parzen.IsHot);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var parzen = new Parzen(5);
        for (int i = 0; i < 4; i++)
        {
            parzen.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(parzen.IsHot);
        }
        parzen.Update(new TValue(DateTime.UtcNow, 105.0));
        Assert.True(parzen.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var parzen = new Parzen(10);
        Assert.Equal(10, parzen.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var parzen = new Parzen(5);
        for (int i = 0; i < 5; i++)
        {
            parzen.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        parzen.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(parzen.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var parzen = new Parzen(5);
        for (int i = 0; i < 5; i++)
        {
            parzen.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        parzen.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(parzen.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var parzen = new Parzen(5);
        var src = MakeSeries(50);
        var result = parzen.Update(src);
        Assert.Equal(src.Count, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result[i].Value));
        }
    }

    // ── F) Consistency (4-API match) ───────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        var src = MakeSeries(100);

        // Streaming
        var streaming = new Parzen(period);
        var streamResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streamResults[i] = streaming.Update(src[i]).Value;
        }

        // Batch (TSeries)
        var batchResults = Parzen.Batch(src, period);

        // Span
        var spanOutput = new double[src.Count];
        Parzen.Batch(src.Values, spanOutput, period);

        // Event-based
        var publisher = new TSeries();
        var eventParzen = new Parzen(publisher, period);
        var eventResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            publisher.Add(src[i], isNew: true);
            eventResults[i] = eventParzen.Last.Value;
        }

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-6);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-6);
            Assert.Equal(streamResults[i], eventResults[i], 1e-6);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var src = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Parzen.Batch(src, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Parzen.Batch(src, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOp()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Parzen.Batch(src, output, 5);
        Assert.True(true);
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var parzen = new Parzen(5);
        int count = 0;
        parzen.Pub += (object? _, in TValueEventArgs e) => count++;
        parzen.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void EventBased_Chaining()
    {
        var source = new TSeries();
        using var parzen = new Parzen(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(double.IsFinite(parzen.Last.Value));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var parzen = new Parzen(source, 5);
        parzen.Dispose();

        source.Add(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.Equal(default, parzen.Last);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var parzen = new Parzen(5);
        parzen.Dispose();
        parzen.Dispose();
        Assert.True(true);
    }

    // ── I) Parzen-specific: piecewise cubic properties ─────────────────

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        var parzen = new Parzen(7);
        for (int i = 0; i < 20; i++)
        {
            parzen.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, parzen.Last.Value, 1e-10);
    }

    [Fact]
    public void Weights_AreSymmetric()
    {
        // Parzen window is symmetric around center
        int period = 9;
        var parzen1 = new Parzen(period);
        var parzen2 = new Parzen(period);

        // Feed ascending then descending series — symmetric weights means
        // feeding [1,2,3,4,5] and [5,4,3,2,1] should give same result for center-weighted
        var ascending = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var descending = new double[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        double resultAsc = 0, resultDesc = 0;
        for (int i = 0; i < period; i++)
        {
            resultAsc = parzen1.Update(new TValue(DateTime.UtcNow, ascending[i])).Value;
            resultDesc = parzen2.Update(new TValue(DateTime.UtcNow, descending[i])).Value;
        }

        // Both should give 5.0 (the mean) because symmetric weights on symmetric data
        Assert.Equal(resultAsc, resultDesc, 1e-10);
    }

    [Fact]
    public void LargerPeriod_SmoothsMore()
    {
        var src = MakeSeries(200);

        var smallPeriod = new Parzen(5);
        var largePeriod = new Parzen(20);

        double sumDiffSmall = 0;
        double sumDiffLarge = 0;
        int countSmall = 0;
        int countLarge = 0;

        for (int i = 0; i < src.Count; i++)
        {
            double raw = src[i].Value;
            smallPeriod.Update(src[i]);
            largePeriod.Update(src[i]);

            if (smallPeriod.IsHot)
            {
                sumDiffSmall += Math.Abs(raw - smallPeriod.Last.Value);
                countSmall++;
            }
            if (largePeriod.IsHot)
            {
                sumDiffLarge += Math.Abs(raw - largePeriod.Last.Value);
                countLarge++;
            }
        }

        double avgDiffSmall = sumDiffSmall / countSmall;
        double avgDiffLarge = sumDiffLarge / countLarge;

        // Larger period should smooth more (larger avg deviation from raw)
        Assert.True(avgDiffLarge > avgDiffSmall);
    }

    [Fact]
    public void AllWeights_NonNegative()
    {
        // Parzen window guarantees all non-negative weights (convex combination)
        int period = 14;
        var src = new double[period];
        var output = new double[period];
        for (int i = 0; i < period; i++)
        {
            src[i] = 100.0;
        }
        src[period - 1] = 200.0; // spike at newest

        Parzen.Batch(src, output, period);

        // Since all weights are non-negative, convex combination means output <= max(input)
        // and output >= min(input)
        Assert.True(output[period - 1] >= 100.0);
        Assert.True(output[period - 1] <= 200.0);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var (results, indicator) = Parzen.Calculate(_data, 14);
        Assert.Equal(_data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var parzen = new Parzen(5);
        var src = MakeSeries(20);
        parzen.Prime(src.Values);
        Assert.True(parzen.IsHot);
    }
}
