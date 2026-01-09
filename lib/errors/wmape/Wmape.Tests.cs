namespace QuanTAlib.Tests;

public class WmapeTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Wmape(0));
        Assert.Throws<ArgumentException>(() => new Wmape(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var wmape = new Wmape(DefaultPeriod);
        Assert.NotNull(wmape);
        Assert.Equal(DefaultPeriod, wmape.WarmupPeriod);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var wmape = new Wmape(DefaultPeriod);
        Assert.Contains("Wmape", wmape.Name, StringComparison.Ordinal);
        Assert.False(wmape.IsHot);
        Assert.Equal(0, wmape.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var wmape = new Wmape(5);
        for (int i = 0; i < 4; i++)
        {
            wmape.Update(100 + i, 100);
            Assert.False(wmape.IsHot);
        }
        wmape.Update(104, 100);
        Assert.True(wmape.IsHot);
    }

    [Fact]
    public void Calculate_ReturnsCorrectValue()
    {
        // WMAPE = (Σ|actual - predicted| / Σ|actual|) * 100
        var wmape = new Wmape(3);

        // Actuals: 100, 200, 300 -> Sum = 600
        // Errors: |100-90|=10, |200-180|=20, |300-270|=30 -> Sum = 60
        // WMAPE = (60 / 600) * 100 = 10%
        wmape.Update(100, 90);
        wmape.Update(200, 180);
        wmape.Update(300, 270);

        Assert.Equal(10.0, wmape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_WeightsLargerValuesMore()
    {
        // WMAPE should weight larger actual values more heavily
        var wmape = new Wmape(2);

        // First scenario: small actual, large error %
        // Actual: 10, Error: 5 (50% individual error)
        // Actual: 100, Error: 5 (5% individual error)
        // Sum actuals = 110, Sum errors = 10
        // WMAPE = (10/110) * 100 = 9.09%
        wmape.Update(10, 5);   // |10-5| = 5
        wmape.Update(100, 95); // |100-95| = 5

        const double expected = (10.0 / 110.0) * 100.0;
        Assert.Equal(expected, wmape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var wmape = new Wmape(5);
        for (int i = 0; i < 5; i++)
        {
            wmape.Update(100 * (i + 1), 100 * (i + 1));
        }
        Assert.Equal(0.0, wmape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var wmape = new Wmape(DefaultPeriod);
        wmape.Update(100, 95);
        wmape.Update(200, 190, isNew: true);
        double beforeUpdate = wmape.Last.Value;

        wmape.Update(200, 180, isNew: false);
        double afterUpdate = wmape.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var wmape = new Wmape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            wmape.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = wmape.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            wmape.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = wmape.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var wmape = new Wmape(DefaultPeriod);
        wmape.Update(100, 95);
        wmape.Update(105, 100);

        wmape.Reset();

        Assert.Equal(0, wmape.Last.Value);
        Assert.False(wmape.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var wmape = new Wmape(DefaultPeriod);
        wmape.Update(100, 95);
        wmape.Update(110, 105);

        var result = wmape.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = wmape.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var wmape = new Wmape(DefaultPeriod);
        wmape.Update(100, 95);
        wmape.Update(110, 105);

        var result = wmape.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = wmape.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var wmapeIterative = new Wmape(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02)));
        }

        var batchResults = Wmape.Calculate(actualSeries, predictedSeries, DefaultPeriod);

        var iterativeResults = new List<double>();
        for (int i = 0; i < actualSeries.Count; i++)
        {
            iterativeResults.Add(wmapeIterative.Update(actualSeries[i], predictedSeries[i]).Value);
        }

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
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
            Wmape.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            Wmape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Wmape.Calculate(actualSeries, predictedSeries, DefaultPeriod);
        Wmape.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

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

        Wmape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var wmape = new Wmape(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => wmape.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var wmape = new Wmape(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => wmape.Prime([1, 2, 3]));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => Wmape.Calculate(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        // Test that resync keeps values accurate over many updates
        var wmape = new Wmape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        // Run more than ResyncInterval (1000) updates
        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next(isNew: true);
            wmape.Update(bar.Close, bar.Close * 0.98);
        }

        Assert.True(double.IsFinite(wmape.Last.Value));
        Assert.True(wmape.Last.Value > 0);
        Assert.True(wmape.Last.Value < 100); // Should be around 2%
    }

    [Fact]
    public void Calculate_ZeroActuals_ReturnsZero()
    {
        // When sum of actuals is near zero, should return 0 (epsilon protection)
        var wmape = new Wmape(3);

        wmape.Update(0.0, 10);
        wmape.Update(0.0, 20);
        wmape.Update(0.0, 30);

        Assert.Equal(0.0, wmape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SlidingWindow_Works()
    {
        var wmape = new Wmape(2);

        // Window 1: actuals 100, 200 (sum=300), errors 10, 20 (sum=30)
        // WMAPE = (30/300) * 100 = 10%
        wmape.Update(100, 90);
        wmape.Update(200, 180);
        Assert.Equal(10.0, wmape.Last.Value, Precision);

        // Window 2: actuals 200, 300 (sum=500), errors 20, 30 (sum=50)
        // WMAPE = (50/500) * 100 = 10%
        wmape.Update(300, 270);
        Assert.Equal(10.0, wmape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IntermittentDemand_Stable()
    {
        // WMAPE should be stable with intermittent (zero) values
        var wmape = new Wmape(5);

        wmape.Update(100, 95);  // 5% error
        wmape.Update(0, 0);     // 0 error, 0 actual
        wmape.Update(200, 190); // 10 error
        wmape.Update(0, 0);     // 0 error, 0 actual
        wmape.Update(300, 285); // 15 error

        // Sum errors = 5 + 0 + 10 + 0 + 15 = 30
        // Sum actuals = 100 + 0 + 200 + 0 + 300 = 600
        // WMAPE = (30/600) * 100 = 5%
        Assert.Equal(5.0, wmape.Last.Value, Precision);
    }
}
