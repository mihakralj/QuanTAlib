namespace QuanTAlib.Tests;

public class MaapeTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Maape(0));
        Assert.Throws<ArgumentException>(() => new Maape(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var maape = new Maape(DefaultPeriod);
        Assert.NotNull(maape);
        Assert.Equal(DefaultPeriod, maape.WarmupPeriod);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var maape = new Maape(DefaultPeriod);
        Assert.True(maape.Name.Contains("Maape", StringComparison.Ordinal));
        Assert.False(maape.IsHot);
        Assert.Equal(0, maape.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var maape = new Maape(5);
        for (int i = 0; i < 4; i++)
        {
            maape.Update(100 + i, 100);
            Assert.False(maape.IsHot);
        }
        maape.Update(104, 100);
        Assert.True(maape.IsHot);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var maape = new Maape(5);
        for (int i = 0; i < 5; i++)
        {
            maape.Update(100, 100);
        }
        Assert.Equal(0.0, maape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ReturnsCorrectValue()
    {
        // MAAPE = (1/n) * Σ arctan(|error| / |actual|)
        var maape = new Maape(2);

        // Two errors with known atan values
        // Error 1: |100-90|/100 = 0.1 -> atan(0.1)
        // Error 2: |100-80|/100 = 0.2 -> atan(0.2)
        maape.Update(100, 90);
        maape.Update(100, 80);

        double expected = (Math.Atan(0.1) + Math.Atan(0.2)) / 2.0;
        Assert.Equal(expected, maape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_BoundedBetweenZeroAndPiOverTwo()
    {
        // MAAPE should always be between 0 and π/2
        var maape = new Maape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.5, seed: 42);

        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            // Use extreme prediction errors
            maape.Update(bar.Close, bar.Close * (i % 2 == 0 ? 2.0 : 0.5));
        }

        Assert.True(maape.Last.Value >= 0.0);
        Assert.True(maape.Last.Value <= Math.PI / 2.0);
    }

    [Fact]
    public void Calculate_ZeroActual_ApproachesPiOverTwo()
    {
        // When actual is zero, arctan approaches π/2
        var maape = new Maape(3);

        maape.Update(0.0, 10);
        maape.Update(0.0, 20);
        maape.Update(0.0, 30);

        // All three values should be π/2, so mean is π/2
        Assert.Equal(Math.PI / 2.0, maape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var maape = new Maape(DefaultPeriod);
        maape.Update(100, 95);
        maape.Update(100, 90, isNew: true);
        double beforeUpdate = maape.Last.Value;

        maape.Update(100, 80, isNew: false);
        double afterUpdate = maape.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var maape = new Maape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            maape.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = maape.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            maape.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = maape.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var maape = new Maape(DefaultPeriod);
        maape.Update(100, 95);
        maape.Update(105, 100);

        maape.Reset();

        Assert.Equal(0, maape.Last.Value);
        Assert.False(maape.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var maape = new Maape(DefaultPeriod);
        maape.Update(100, 95);
        maape.Update(110, 105);

        var result = maape.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = maape.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var maape = new Maape(DefaultPeriod);
        maape.Update(100, 95);
        maape.Update(110, 105);

        var result = maape.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = maape.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        const int count = 100;
        var maapeIterative = new Maape(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();
        double[] actualArr = new double[count];
        double[] predictedArr = new double[count];

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            actualArr[i] = bar.Close;
            double pred = bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02));
            predictedSeries.Add(bar.Time, pred);
            predictedArr[i] = pred;
        }

        var streamingResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            streamingResults[i] = maapeIterative.Update(actualArr[i], predictedArr[i]).Value;
        }

        var batchResults = Maape.Calculate(actualSeries, predictedSeries, DefaultPeriod);

        Assert.Equal(count, batchResults.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, Precision);
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
            Maape.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            Maape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Maape.Calculate(actualSeries, predictedSeries, DefaultPeriod);
        Maape.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

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

        Maape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var maape = new Maape(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => maape.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var maape = new Maape(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => maape.Prime([1, 2, 3]));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => Maape.Calculate(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        var maape = new Maape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next(isNew: true);
            maape.Update(bar.Close, bar.Close * 0.98);
        }

        Assert.True(double.IsFinite(maape.Last.Value));
        Assert.True(maape.Last.Value >= 0);
        Assert.True(maape.Last.Value <= Math.PI / 2.0);
    }

    [Fact]
    public void Calculate_SymmetricErrors()
    {
        // Over and under predictions should be treated similarly
        var maape1 = new Maape(2);
        var maape2 = new Maape(2);

        // Predict 10% above
        maape1.Update(100, 110);
        maape1.Update(100, 110);

        // Predict 10% below
        maape2.Update(100, 90);
        maape2.Update(100, 90);

        Assert.Equal(maape1.Last.Value, maape2.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ScaleIndependent()
    {
        // MAAPE should be scale-independent
        var maape1 = new Maape(3);
        var maape2 = new Maape(3);

        // Scale 1
        maape1.Update(100, 110);
        maape1.Update(100, 90);
        maape1.Update(100, 105);

        // Scale 1000 (same relative errors)
        maape2.Update(100000, 110000);
        maape2.Update(100000, 90000);
        maape2.Update(100000, 105000);

        Assert.Equal(maape1.Last.Value, maape2.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SlidingWindow_Works()
    {
        var maape = new Maape(2);

        // Error 1: atan(0.1), Error 2: atan(0.2)
        maape.Update(100, 90);   // 10% error
        maape.Update(100, 80);   // 20% error
        double expected1 = (Math.Atan(0.1) + Math.Atan(0.2)) / 2.0;
        Assert.Equal(expected1, maape.Last.Value, Precision);

        // Slide: Error 2: atan(0.2), Error 3: atan(0.3)
        maape.Update(100, 70);   // 30% error
        double expected2 = (Math.Atan(0.2) + Math.Atan(0.3)) / 2.0;
        Assert.Equal(expected2, maape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_RobustToOutliers()
    {
        // MAAPE should be robust due to arctan bounding
        var maape = new Maape(5);

        // 4 normal errors + 1 extreme error
        maape.Update(100, 95);   // 5%
        maape.Update(100, 95);   // 5%
        maape.Update(100, 95);   // 5%
        maape.Update(100, 95);   // 5%
        maape.Update(100, -900); // 1000% (extreme, but bounded by atan)

        // Result should still be reasonable (bounded)
        Assert.True(maape.Last.Value >= 0.0);
        Assert.True(maape.Last.Value <= Math.PI / 2.0);
    }
}
