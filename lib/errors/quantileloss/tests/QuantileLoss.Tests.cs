namespace QuanTAlib.Tests;

public class QuantileLossTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new QuantileLoss(0));
        Assert.Throws<ArgumentException>(() => new QuantileLoss(-1));
        Assert.Throws<ArgumentException>(() => new QuantileLoss(10, 0.0));
        Assert.Throws<ArgumentException>(() => new QuantileLoss(10, 1.0));
        Assert.Throws<ArgumentException>(() => new QuantileLoss(10, -0.1));
        Assert.Throws<ArgumentException>(() => new QuantileLoss(10, 1.1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        Assert.NotNull(quantileLoss);
        Assert.Equal(DefaultPeriod, quantileLoss.WarmupPeriod);
        Assert.Equal(0.5, quantileLoss.Quantile);
    }

    [Fact]
    public void Constructor_CustomQuantile_Succeeds()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod, 0.9);
        Assert.Equal(0.9, quantileLoss.Quantile);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        Assert.Contains("QuantileLoss", quantileLoss.Name, StringComparison.Ordinal);
        Assert.False(quantileLoss.IsHot);
        Assert.Equal(0, quantileLoss.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var quantileLoss = new QuantileLoss(5);
        for (int i = 0; i < 4; i++)
        {
            quantileLoss.Update(100 + i, 100);
            Assert.False(quantileLoss.IsHot);
        }
        quantileLoss.Update(104, 100);
        Assert.True(quantileLoss.IsHot);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var quantileLoss = new QuantileLoss(5);
        for (int i = 0; i < 5; i++)
        {
            quantileLoss.Update(100, 100);
        }
        Assert.Equal(0.0, quantileLoss.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_Quantile05_EquivalentToMAE()
    {
        // With q=0.5, quantile loss = 0.5 * |error| = MAE/2
        var quantileLoss = new QuantileLoss(2, 0.5);

        // Error 1: 100 - 90 = 10 (actual > predicted)
        // Error 2: 100 - 110 = -10 (actual < predicted)
        quantileLoss.Update(100, 90);  // 0.5 * 10 = 5
        quantileLoss.Update(100, 110); // (0.5-1) * (-10) = 0.5 * 10 = 5

        // Mean = (5 + 5) / 2 = 5
        Assert.Equal(5.0, quantileLoss.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_HighQuantile_PenalizesUnderPrediction()
    {
        // q=0.9 penalizes under-prediction (actual > predicted) more heavily
        var quantileLoss = new QuantileLoss(1, 0.9);

        // Under-prediction: actual > predicted
        quantileLoss.Update(100, 90);  // 0.9 * 10 = 9

        Assert.Equal(9.0, quantileLoss.Last.Value, Precision);

        // Over-prediction: actual < predicted
        quantileLoss.Reset();
        quantileLoss.Update(100, 110); // (0.9-1) * (-10) = 0.1 * 10 = 1

        Assert.Equal(1.0, quantileLoss.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_LowQuantile_PenalizesOverPrediction()
    {
        // q=0.1 penalizes over-prediction (actual < predicted) more heavily
        var quantileLoss = new QuantileLoss(1, 0.1);

        // Under-prediction: actual > predicted
        quantileLoss.Update(100, 90);  // 0.1 * 10 = 1

        Assert.Equal(1.0, quantileLoss.Last.Value, Precision);

        // Over-prediction: actual < predicted
        quantileLoss.Reset();
        quantileLoss.Update(100, 110); // (0.1-1) * (-10) = 0.9 * 10 = 9

        Assert.Equal(9.0, quantileLoss.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_AsymmetricPenalty()
    {
        // Verify asymmetric penalty with same magnitude errors
        var qlHigh = new QuantileLoss(2, 0.9);
        var qlLow = new QuantileLoss(2, 0.1);

        // Both get one under-prediction and one over-prediction of same magnitude
        qlHigh.Update(100, 90);  // under: 0.9 * 10 = 9
        qlHigh.Update(100, 110); // over: 0.1 * 10 = 1
        // Mean = (9 + 1) / 2 = 5

        qlLow.Update(100, 90);   // under: 0.1 * 10 = 1
        qlLow.Update(100, 110);  // over: 0.9 * 10 = 9
        // Mean = (1 + 9) / 2 = 5

        // Both should give same result with symmetric errors
        Assert.Equal(qlHigh.Last.Value, qlLow.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        quantileLoss.Update(100, 95);
        quantileLoss.Update(100, 90, isNew: true);
        double beforeUpdate = quantileLoss.Last.Value;

        quantileLoss.Update(100, 80, isNew: false);
        double afterUpdate = quantileLoss.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var quantileLoss = new QuantileLoss(5, 0.75);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            quantileLoss.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = quantileLoss.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            quantileLoss.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = quantileLoss.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        quantileLoss.Update(100, 95);
        quantileLoss.Update(105, 100);

        quantileLoss.Reset();

        Assert.Equal(0, quantileLoss.Last.Value);
        Assert.False(quantileLoss.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        quantileLoss.Update(100, 95);
        quantileLoss.Update(110, 105);

        var result = quantileLoss.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = quantileLoss.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        quantileLoss.Update(100, 95);
        quantileLoss.Update(110, 105);

        var result = quantileLoss.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = quantileLoss.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var quantileLossIterative = new QuantileLoss(DefaultPeriod, 0.75);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02)));
        }

        var iterativeResults = actualSeries.Zip(predictedSeries, (actual, predicted) => quantileLossIterative.Update(actual.Value, predicted.Value).Value).ToList();

        var batchResults = QuantileLoss.Batch(actualSeries, predictedSeries, DefaultPeriod, 0.75);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        int count = iterativeResults.Count;
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
            QuantileLoss.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            QuantileLoss.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));

        Assert.Throws<ArgumentException>(() =>
            QuantileLoss.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), DefaultPeriod, 0.0));

        Assert.Throws<ArgumentException>(() =>
            QuantileLoss.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), DefaultPeriod, 1.0));
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

        var tseriesResult = QuantileLoss.Batch(actualSeries, predictedSeries, DefaultPeriod, 0.75);
        QuantileLoss.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod, 0.75);

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

        QuantileLoss.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => quantileLoss.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var quantileLoss = new QuantileLoss(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => quantileLoss.Prime([1, 2, 3]));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => QuantileLoss.Batch(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        var quantileLoss = new QuantileLoss(5, 0.75);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next(isNew: true);
            quantileLoss.Update(bar.Close, bar.Close * 0.98);
        }

        Assert.True(double.IsFinite(quantileLoss.Last.Value));
    }

    [Fact]
    public void Calculate_SlidingWindow_Works()
    {
        var quantileLoss = new QuantileLoss(2, 0.5);

        // Error 1: 10 (under), Error 2: -10 (over)
        quantileLoss.Update(100, 90);   // 0.5 * 10 = 5
        quantileLoss.Update(100, 110);  // 0.5 * 10 = 5
        Assert.Equal(5.0, quantileLoss.Last.Value, Precision);

        // Slide: Error 2: -10, Error 3: 20
        quantileLoss.Update(100, 80);   // 0.5 * 20 = 10
        // Mean = (5 + 10) / 2 = 7.5
        Assert.Equal(7.5, quantileLoss.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_AlwaysNonNegative()
    {
        // Quantile loss should always be non-negative
        var quantileLoss = new QuantileLoss(5, 0.5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.3, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            quantileLoss.Update(bar.Close, bar.Close * (1 + (((i % 3) - 1) * 0.1)));
            Assert.True(quantileLoss.Last.Value >= 0, $"QuantileLoss should be non-negative, got {quantileLoss.Last.Value}");
        }
    }
}
