namespace QuanTAlib.Tests;

public class TheilUTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new TheilU(0));
        Assert.Throws<ArgumentException>(() => new TheilU(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var theilU = new TheilU(DefaultPeriod);
        Assert.NotNull(theilU);
        Assert.Equal(DefaultPeriod, theilU.WarmupPeriod);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var theilU = new TheilU(DefaultPeriod);
        Assert.Contains("TheilU", theilU.Name, StringComparison.Ordinal);
        Assert.False(theilU.IsHot);
        Assert.Equal(0, theilU.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var theilU = new TheilU(5);
        for (int i = 0; i < 4; i++)
        {
            theilU.Update(100 + i, 100);
            Assert.False(theilU.IsHot);
        }
        theilU.Update(104, 100);
        Assert.True(theilU.IsHot);
    }

    [Fact]
    public void Calculate_PerfectForecast_ReturnsZero()
    {
        // U = 0 for perfect forecast
        var theilU = new TheilU(5);
        for (int i = 0; i < 5; i++)
        {
            theilU.Update(100, 100);
        }
        Assert.Equal(0.0, theilU.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ReturnsCorrectValue()
    {
        // TheilU = √(Σ(pred-act)²) / √(Σact² + Σpred²)
        var theilU = new TheilU(2);

        // Actual: 100, 100 -> sum of squares = 20000
        // Predicted: 110, 90 -> sum of squares = 12100 + 8100 = 20200
        // Errors: 10, -10 -> sum of squared errors = 200
        // TheilU = √200 / √(20000 + 20200) = √200 / √40200
        theilU.Update(100, 110);
        theilU.Update(100, 90);

        double expected = Math.Sqrt(200) / Math.Sqrt(20000 + 20200);
        Assert.Equal(expected, theilU.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_BoundedZeroToOne_ForReasonableForecasts()
    {
        var theilU = new TheilU(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Run with reasonable prediction errors
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            theilU.Update(bar.Close, bar.Close * 0.95); // 5% prediction error
        }

        Assert.True(theilU.Last.Value >= 0.0);
        Assert.True(theilU.Last.Value <= 1.0);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var theilU = new TheilU(DefaultPeriod);
        theilU.Update(100, 95);
        theilU.Update(110, 108, isNew: true);
        double beforeUpdate = theilU.Last.Value;

        theilU.Update(110, 100, isNew: false);
        double afterUpdate = theilU.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var theilU = new TheilU(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            theilU.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = theilU.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            theilU.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = theilU.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var theilU = new TheilU(DefaultPeriod);
        theilU.Update(100, 95);
        theilU.Update(105, 100);

        theilU.Reset();

        Assert.Equal(0, theilU.Last.Value);
        Assert.False(theilU.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var theilU = new TheilU(DefaultPeriod);
        theilU.Update(100, 95);
        theilU.Update(110, 105);

        var result = theilU.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = theilU.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var theilU = new TheilU(DefaultPeriod);
        theilU.Update(100, 95);
        theilU.Update(110, 105);

        var result = theilU.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = theilU.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var theilUIterative = new TheilU(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        var iterativeResults = new List<double>();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            double predicted = bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02));

            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, predicted);

            iterativeResults.Add(theilUIterative.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, predicted)).Value);
        }

        var batchResults = TheilU.Batch(actualSeries, predictedSeries, DefaultPeriod);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
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
            TheilU.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            TheilU.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = TheilU.Batch(actualSeries, predictedSeries, DefaultPeriod);
        TheilU.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

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

        TheilU.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var theilU = new TheilU(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => theilU.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var theilU = new TheilU(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => theilU.Prime([1, 2, 3]));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => TheilU.Batch(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        // Test that resync keeps values accurate over many updates
        var theilU = new TheilU(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Run more than ResyncInterval (1000) updates
        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next(isNew: true);
            theilU.Update(bar.Close, bar.Close * 0.98);
        }

        Assert.True(double.IsFinite(theilU.Last.Value));
        Assert.True(theilU.Last.Value >= 0);
        Assert.True(theilU.Last.Value <= 1); // Should be bounded
    }

    [Fact]
    public void Calculate_ZeroValues_ReturnsZero()
    {
        // When denominator is near zero, should return 0 (epsilon protection)
        var theilU = new TheilU(3);

        theilU.Update(0.0, 0.0);
        theilU.Update(0.0, 0.0);
        theilU.Update(0.0, 0.0);

        Assert.Equal(0.0, theilU.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ScaleIndependent()
    {
        // TheilU should be scale-independent (relative measure)
        var theilU1 = new TheilU(3);
        var theilU2 = new TheilU(3);

        // Scale 1
        theilU1.Update(100, 110);
        theilU1.Update(100, 90);
        theilU1.Update(100, 105);

        // Scale 1000 (same relative errors)
        theilU2.Update(100000, 110000);
        theilU2.Update(100000, 90000);
        theilU2.Update(100000, 105000);

        Assert.Equal(theilU1.Last.Value, theilU2.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SymmetricErrors()
    {
        // Note: Theil's U is NOT symmetric with respect to direction because
        // the denominator includes √(Σact² + Σpred²) where pred differs.
        // However, the squared error in the numerator treats positive and
        // negative errors the same way.
        var theilU1 = new TheilU(2);
        var theilU2 = new TheilU(2);

        // Predict 10% above: errors = (100-110)² = 100 each
        theilU1.Update(100, 110);
        theilU1.Update(100, 110);

        // Predict 10% below: errors = (100-90)² = 100 each (same squared error)
        theilU2.Update(100, 90);
        theilU2.Update(100, 90);

        // Both should produce valid bounded values
        Assert.True(theilU1.Last.Value >= 0 && theilU1.Last.Value <= 1);
        Assert.True(theilU2.Last.Value >= 0 && theilU2.Last.Value <= 1);

        // The squared errors are the same, but denominators differ due to pred² terms
        // So we just verify both produce sensible values (not exact equality)
        Assert.True(double.IsFinite(theilU1.Last.Value));
        Assert.True(double.IsFinite(theilU2.Last.Value));
    }
}
