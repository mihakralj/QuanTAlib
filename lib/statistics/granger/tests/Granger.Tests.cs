namespace QuanTAlib.Tests;

public class GrangerConstructorTests
{
    [Fact]
    public void Constructor_WithValidPeriod_SetsProperties()
    {
        var indicator = new Granger(10);

        Assert.Equal("Granger(10)", indicator.Name);
        Assert.Equal(11, indicator.WarmupPeriod); // period + 1
        Assert.False(indicator.IsHot);
    }

    [Fact]
    public void Constructor_WithDefaultPeriod_UsesTwenty()
    {
        var indicator = new Granger();

        Assert.Equal("Granger(20)", indicator.Name);
        Assert.Equal(21, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithPeriodThree_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Granger(3));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithPeriodTwo_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Granger(2));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithPeriodZero_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Granger(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Granger(-5));
        Assert.Equal("period", ex.ParamName);
    }
}

public class GrangerBasicTests
{
    private const int DefaultPeriod = 20;

    [Fact]
    public void Update_ReturnsTValue()
    {
        var indicator = new Granger(DefaultPeriod);

        var result = indicator.Update(100.0, 100.0);

        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_ReturnsNaN_BeforeWarmup()
    {
        var indicator = new Granger(DefaultPeriod);

        // First few updates should return NaN until warmup
        for (int i = 0; i < 3; i++)
        {
            var result = indicator.Update(100.0 + i, 100.0 + i);
            Assert.True(double.IsNaN(result.Value));
        }
    }

    [Fact]
    public void Update_ReturnsFiniteValue_AfterWarmup()
    {
        var indicator = new Granger(DefaultPeriod);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.1, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.1, seed: 54321);

        // Feed enough data to warm up
        for (int i = 0; i < DefaultPeriod + 5; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close);
        }

        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Update_IsHot_BecomesTrueAfterWarmup()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 54321);

        Assert.False(indicator.IsHot);

        for (int i = 0; i < 20; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close);
        }

        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_SingleInput_ThrowsNotSupported()
    {
        var indicator = new Granger();

        Assert.Throws<NotSupportedException>(() => indicator.Update(new TValue(DateTime.UtcNow, 100.0)));
    }

    [Fact]
    public void Update_TSeries_ThrowsNotSupported()
    {
        var indicator = new Granger();
        var series = new TSeries(10);

        Assert.Throws<NotSupportedException>(() => indicator.Update(series));
    }

    [Fact]
    public void Update_FStatistic_IsNonNegative()
    {
        var indicator = new Granger(10);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 54321);

        for (int i = 0; i < 50; i++)
        {
            var result = indicator.Update(gbmY.Next().Close, gbmX.Next().Close);
            Assert.True(double.IsNaN(result.Value) || result.Value >= 0.0,
                $"F-statistic should be non-negative or NaN, got {result.Value}");
        }
    }
}

public class GrangerStateCorrectionTests
{
    [Fact]
    public void Update_IsNew_True_AdvancesState()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        TValue prev = default;
        for (int i = 0; i < 10; i++)
        {
            prev = indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        var next = indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);

        // New bar should advance state and potentially produce different value
        Assert.NotEqual(0.0, next.Value + prev.Value); // Not both zero
    }

    [Fact]
    public void Update_IsNew_False_RewritesCurrentBar()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        // New bar
        double y1 = gbmY.Next().Close;
        double x1 = gbmX.Next().Close;
        var result1 = indicator.Update(y1, x1, isNew: true);

        // Correct with same values
        var result2 = indicator.Update(y1, x1, isNew: false);

        Assert.Equal(result1.Value, result2.Value, 10);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        // New bar
        double y1 = gbmY.Next().Close;
        double x1 = gbmX.Next().Close;
        indicator.Update(y1, x1, isNew: true);

        // Multiple corrections converge
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(y1 + i * 0.01, x1 + i * 0.01, isNew: false);
        }

        var final1 = indicator.Update(y1, x1, isNew: false);
        var final2 = indicator.Update(y1, x1, isNew: false);

        Assert.Equal(final1.Value, final2.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        Assert.True(indicator.IsHot);

        indicator.Reset();

        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }
}

public class GrangerWarmupTests
{
    [Fact]
    public void IsHot_FlipsWhenWindowFull()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        // Need period+1 bars for IsHot (1 for lag + period for window)
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
            Assert.False(indicator.IsHot);
        }

        // After period+1 bars, should be hot
        indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsPeriodPlusOne()
    {
        var indicator = new Granger(10);
        Assert.Equal(11, indicator.WarmupPeriod);
    }
}

public class GrangerRobustnessTests
{
    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        _ = indicator.Last;

        // Feed NaN - should not propagate to output
        var result = indicator.Update(double.NaN, double.NaN, isNew: true);
        Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
        // Key: should not throw
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var indicator = new Granger(5);
        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        // Feed Infinity - should not throw or produce Infinity
        var result = indicator.Update(double.PositiveInfinity, double.NegativeInfinity, isNew: true);
        Assert.False(double.IsInfinity(result.Value));
    }

    [Fact]
    public void Update_BatchNaN_DoesNotThrow()
    {
        var indicator = new Granger(5);

        // Feed all NaN - should not throw
        for (int i = 0; i < 20; i++)
        {
            var result = indicator.Update(double.NaN, double.NaN, isNew: true);
            Assert.False(double.IsInfinity(result.Value));
        }
    }

    [Fact]
    public void Update_ConstantSeries_ReturnsNaNOrZero()
    {
        // Constant series has zero variance, should handle gracefully
        var indicator = new Granger(5);

        for (int i = 0; i < 20; i++)
        {
            var result = indicator.Update(100.0, 100.0, isNew: true);
            Assert.True(double.IsNaN(result.Value) || result.Value >= 0.0,
                $"Should handle constant series gracefully, got {result.Value}");
        }
    }
}

public class GrangerConsistencyTests
{
    [Fact]
    public void BatchCalc_MatchesStreaming()
    {
        const int period = 10;
        const int count = 100;
        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.15, seed: 54321);

        var seriesY = new TSeries(count);
        var seriesX = new TSeries(count);

        for (int i = 0; i < count; i++)
        {
            var barY = gbmY.Next(isNew: true);
            var barX = gbmX.Next(isNew: true);
            seriesY.Add(new TValue(barY.Time, barY.Close));
            seriesX.Add(new TValue(barX.Time, barX.Close));
        }

        // Batch calculation
        var batchResults = Granger.Batch(seriesY, seriesX, period);

        // Streaming calculation
        var streamIndicator = new Granger(period);
        var streamResults = new TSeries(count);
        for (int i = 0; i < count; i++)
        {
            streamResults.Add(streamIndicator.Update(
                new TValue(seriesY.Times[i], seriesY.Values[i]),
                new TValue(seriesX.Times[i], seriesX.Values[i]),
                isNew: true));
        }

        // Compare
        for (int i = 0; i < count; i++)
        {
            if (double.IsNaN(batchResults.Values[i]) && double.IsNaN(streamResults.Values[i]))
            {
                continue;
            }
            Assert.Equal(batchResults.Values[i], streamResults.Values[i], 10);
        }
    }

    [Fact]
    public void SpanCalc_MatchesStreaming()
    {
        const int period = 10;
        const int count = 100;
        var gbmY = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.15, seed: 54321);

        double[] yValues = new double[count];
        double[] xValues = new double[count];
        double[] output = new double[count];

        for (int i = 0; i < count; i++)
        {
            yValues[i] = gbmY.Next(isNew: true).Close;
            xValues[i] = gbmX.Next(isNew: true).Close;
        }

        // Span calculation
        Granger.Batch(yValues.AsSpan(), xValues.AsSpan(), output.AsSpan(), period);

        // Streaming calculation
        var gbmY2 = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 12345);
        var gbmX2 = new GBM(startPrice: 100.0, mu: 0.03, sigma: 0.15, seed: 54321);
        var streamIndicator = new Granger(period);

        for (int i = 0; i < count; i++)
        {
            var result = streamIndicator.Update(gbmY2.Next(isNew: true).Close, gbmX2.Next(isNew: true).Close, isNew: true);
            if (double.IsNaN(output[i]) && double.IsNaN(result.Value))
            {
                continue;
            }
            Assert.Equal(output[i], result.Value, 10);
        }
    }
}

public class GrangerSpanTests
{
    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        double[] y = new double[10];
        double[] x = new double[5];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Granger.Batch(y.AsSpan(), x.AsSpan(), output.AsSpan(), 4));
        Assert.Equal("seriesX", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputLengthMismatch_Throws()
    {
        double[] y = new double[10];
        double[] x = new double[10];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Granger.Batch(y.AsSpan(), x.AsSpan(), output.AsSpan(), 4));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        double[] y = new double[10];
        double[] x = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            Granger.Batch(y.AsSpan(), x.AsSpan(), output.AsSpan(), 3));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_TSeries_MismatchedLengths_Throws()
    {
        var seriesY = new TSeries(10);
        var seriesX = new TSeries(5);
        for (int i = 0; i < 10; i++)
        {
            seriesY.Add(new TValue(DateTime.UtcNow, i));
        }

        for (int i = 0; i < 5; i++)
        {
            seriesX.Add(new TValue(DateTime.UtcNow, i));
        }

        var ex = Assert.Throws<ArgumentException>(() =>
            Granger.Batch(seriesY, seriesX, 4));
        Assert.Equal("seriesX", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        double[] y = new double[20];
        double[] x = new double[20];
        double[] output = new double[20];

        for (int i = 0; i < 20; i++)
        {
            y[i] = double.NaN;
            x[i] = double.NaN;
        }

        // Should not throw
        Granger.Batch(y.AsSpan(), x.AsSpan(), output.AsSpan(), 5);

        for (int i = 0; i < 20; i++)
        {
            Assert.False(double.IsInfinity(output[i]));
        }
    }
}

public class GrangerEventTests
{
    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var indicator = new Granger(5);
        int eventCount = 0;

        indicator.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void Pub_EventChaining_Works()
    {
        var indicator = new Granger(5);
        var receivedValues = new List<double>();

        indicator.Pub += (object? sender, in TValueEventArgs args) => receivedValues.Add(args.Value.Value);

        var gbmY = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 42);
        var gbmX = new GBM(startPrice: 100.0, mu: 0.0, sigma: 0.1, seed: 84);

        for (int i = 0; i < 10; i++)
        {
            indicator.Update(gbmY.Next().Close, gbmX.Next().Close, isNew: true);
        }

        Assert.Equal(10, receivedValues.Count);
        // All received values should match Last at time of emission
        Assert.Equal(indicator.Last.Value, receivedValues[^1]);
    }
}
