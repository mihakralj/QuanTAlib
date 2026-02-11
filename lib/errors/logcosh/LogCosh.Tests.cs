namespace QuanTAlib.Tests;

public class LogCoshTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new LogCosh(0));
        Assert.Throws<ArgumentException>(() => new LogCosh(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        Assert.NotNull(logCosh);
        Assert.Equal(DefaultPeriod, logCosh.WarmupPeriod);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        Assert.Contains("LogCosh", logCosh.Name, StringComparison.Ordinal);
        Assert.False(logCosh.IsHot);
        Assert.Equal(0, logCosh.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var logCosh = new LogCosh(5);
        for (int i = 0; i < 4; i++)
        {
            logCosh.Update(100 + i, 100);
            Assert.False(logCosh.IsHot);
        }
        logCosh.Update(104, 100);
        Assert.True(logCosh.IsHot);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        // log(cosh(0)) = log(1) = 0
        var logCosh = new LogCosh(5);
        for (int i = 0; i < 5; i++)
        {
            logCosh.Update(100, 100);
        }
        Assert.Equal(0.0, logCosh.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ReturnsCorrectValue()
    {
        // LogCosh = (1/n) * Σ log(cosh(error))
        var logCosh = new LogCosh(2);

        // Error 1: 100 - 98 = 2
        // Error 2: 100 - 96 = 4
        logCosh.Update(100, 98);
        logCosh.Update(100, 96);

        double expected = (Math.Log(Math.Cosh(2)) + Math.Log(Math.Cosh(4))) / 2.0;
        Assert.Equal(expected, logCosh.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SymmetricErrors()
    {
        // log(cosh(x)) = log(cosh(-x)) because cosh is even
        var logCosh1 = new LogCosh(2);
        var logCosh2 = new LogCosh(2);

        // Positive errors
        logCosh1.Update(100, 95);  // error = 5
        logCosh1.Update(100, 90);  // error = 10

        // Negative errors (same magnitude)
        logCosh2.Update(100, 105); // error = -5
        logCosh2.Update(100, 110); // error = -10

        Assert.Equal(logCosh1.Last.Value, logCosh2.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SmallErrors_ApproximatesL2()
    {
        // For small errors, log(cosh(x)) ≈ x²/2
        var logCosh = new LogCosh(1);

        const double smallError = 0.1;
        logCosh.Update(100, 100 - smallError);

        double l2Approx = (smallError * smallError) / 2.0;
        double actual = logCosh.Last.Value;

        // Should be close to L2/2 approximation
        Assert.True(Math.Abs(actual - l2Approx) < 0.001);
    }

    [Fact]
    public void Calculate_LargeErrors_ApproximatesL1()
    {
        // For large errors, log(cosh(x)) ≈ |x| - log(2)
        var logCosh = new LogCosh(1);

        double largeError = 50.0;
        logCosh.Update(100, 100 - largeError);

        double l1Approx = largeError - Math.Log(2);
        double actual = logCosh.Last.Value;

        // Should be close to L1 approximation
        Assert.True(Math.Abs(actual - l1Approx) < 0.001);
    }

    [Fact]
    public void Calculate_NumericalStability_VeryLargeErrors()
    {
        // Should handle very large errors without overflow
        var logCosh = new LogCosh(3);

        logCosh.Update(1000, 0);    // error = 1000
        logCosh.Update(10000, 0);   // error = 10000
        logCosh.Update(100000, 0);  // error = 100000

        Assert.True(double.IsFinite(logCosh.Last.Value));
        Assert.True(logCosh.Last.Value > 0);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        logCosh.Update(100, 95);
        logCosh.Update(100, 90, isNew: true);
        double beforeUpdate = logCosh.Last.Value;

        logCosh.Update(100, 80, isNew: false);
        double afterUpdate = logCosh.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var logCosh = new LogCosh(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            logCosh.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = logCosh.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            logCosh.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = logCosh.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        logCosh.Update(100, 95);
        logCosh.Update(105, 100);

        logCosh.Reset();

        Assert.Equal(0, logCosh.Last.Value);
        Assert.False(logCosh.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        logCosh.Update(100, 95);
        logCosh.Update(110, 105);

        var result = logCosh.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = logCosh.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        logCosh.Update(100, 95);
        logCosh.Update(110, 105);

        var result = logCosh.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = logCosh.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        const int count = 100;
        var logCoshIterative = new LogCosh(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02)));
        }

        var iterativeResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            iterativeResults[i] = logCoshIterative.Update(actualSeries[i], predictedSeries[i]).Value;
        }

        var batchResults = LogCosh.Batch(actualSeries, predictedSeries, DefaultPeriod);

        Assert.Equal(count, batchResults.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(iterativeResults[i], batchResults[i].Value, Precision);
        }
    }

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] actual = [1, 2, 3, 4, 5];
        double[] predicted = [1.1, 2.1, 3.1, 4.1, 5.1];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() =>
            LogCosh.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            LogCosh.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();
        double[] actualArr = new double[100];
        double[] predictedArr = new double[100];
        double[] output = new double[100];

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            actualArr[i] = bar.Close;
            double pred = bar.Close * 0.98;
            predictedSeries.Add(bar.Time, pred);
            predictedArr[i] = pred;
        }

        var tseriesResult = LogCosh.Batch(actualSeries, predictedSeries, DefaultPeriod);
        LogCosh.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], Precision);
        }
    }

    [Fact]
    public void SpanBatch_HandlesNaN()
    {
        double[] actual = [100, 110, double.NaN, 120, 130];
        double[] predicted = [98, 108, 112, 118, double.NaN];
        double[] output = new double[5];

        LogCosh.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => logCosh.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var logCosh = new LogCosh(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => logCosh.Prime([1, 2, 3]));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => LogCosh.Batch(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        var logCosh = new LogCosh(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next(isNew: true);
            logCosh.Update(bar.Close, bar.Close * 0.98);
        }

        Assert.True(double.IsFinite(logCosh.Last.Value));
        Assert.True(logCosh.Last.Value >= 0);
    }

    [Fact]
    public void Calculate_SlidingWindow_Works()
    {
        var logCosh = new LogCosh(2);

        // Error 1: 5, Error 2: 10
        logCosh.Update(100, 95);
        logCosh.Update(100, 90);
        double expected1 = (Math.Log(Math.Cosh(5)) + Math.Log(Math.Cosh(10))) / 2.0;
        Assert.Equal(expected1, logCosh.Last.Value, Precision);

        // Slide: Error 2: 10, Error 3: 15
        logCosh.Update(100, 85);
        double expected2 = (Math.Log(Math.Cosh(10)) + Math.Log(Math.Cosh(15))) / 2.0;
        Assert.Equal(expected2, logCosh.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_LessSensitiveToOutliers_ThanMse()
    {
        // Compare sensitivity to outliers vs MSE behavior
        var logCosh = new LogCosh(5);

        // 4 small errors + 1 very large error
        logCosh.Update(100, 99);   // error = 1
        logCosh.Update(100, 99);   // error = 1
        logCosh.Update(100, 99);   // error = 1
        logCosh.Update(100, 99);   // error = 1
        logCosh.Update(100, 0);    // error = 100 (outlier)

        // LogCosh of outlier is approximately 100 - log(2) ≈ 99.3
        // LogCosh of small errors is approximately 0.5
        // Mean should be much less than 100^2 / 5 = 2000 (what MSE would give)
        Assert.True(logCosh.Last.Value < 100);
        Assert.True(double.IsFinite(logCosh.Last.Value));
    }

    [Fact]
    public void Calculate_AlwaysNonNegative()
    {
        // log(cosh(x)) >= 0 for all x because cosh(x) >= 1
        var logCosh = new LogCosh(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            logCosh.Update(bar.Close, bar.Close * (1 + (i % 3 - 1) * 0.1));
            Assert.True(logCosh.Last.Value >= 0, $"LogCosh should be non-negative, got {logCosh.Last.Value}");
        }
    }
}
