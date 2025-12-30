using Xunit;

namespace QuanTAlib.Tests;

public class MdapeTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mdape(0));
        Assert.Throws<ArgumentException>(() => new Mdape(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var mdape = new Mdape(DefaultPeriod);
        Assert.NotNull(mdape);
        Assert.Equal(DefaultPeriod, mdape.WarmupPeriod);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mdape = new Mdape(DefaultPeriod);
        Assert.Contains("Mdape", mdape.Name, StringComparison.Ordinal);
        Assert.False(mdape.IsHot);
        Assert.Equal(0, mdape.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var mdape = new Mdape(5);
        for (int i = 0; i < 4; i++)
        {
            mdape.Update(100 + i, 100);
            Assert.False(mdape.IsHot);
        }
        mdape.Update(104, 100);
        Assert.True(mdape.IsHot);
    }

    [Fact]
    public void Calculate_ReturnsCorrectMedian()
    {
        // MdAPE = Median of (|actual - predicted| / |actual|) * 100
        var mdape = new Mdape(5);

        // Errors: |100-90|/100=10%, |100-95|/100=5%, |100-80|/100=20%, |100-85|/100=15%, |100-92|/100=8%
        // Sorted: 5, 8, 10, 15, 20
        // Median = 10%
        mdape.Update(100, 90);  // 10%
        mdape.Update(100, 95);  // 5%
        mdape.Update(100, 80);  // 20%
        mdape.Update(100, 85);  // 15%
        mdape.Update(100, 92);  // 8%

        Assert.Equal(10.0, mdape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_EvenCount_AveragesTwoMiddle()
    {
        // Test median with even count
        var mdape = new Mdape(4);

        // Errors: 5%, 10%, 15%, 20%
        // Sorted: 5, 10, 15, 20
        // Median = (10 + 15) / 2 = 12.5%
        mdape.Update(100, 95);  // 5%
        mdape.Update(100, 90);  // 10%
        mdape.Update(100, 85);  // 15%
        mdape.Update(100, 80);  // 20%

        Assert.Equal(12.5, mdape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var mdape = new Mdape(5);
        for (int i = 0; i < 5; i++)
        {
            mdape.Update(100, 100);
        }
        Assert.Equal(0.0, mdape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var mdape = new Mdape(DefaultPeriod);
        mdape.Update(100, 95);
        mdape.Update(100, 90, isNew: true);
        double beforeUpdate = mdape.Last.Value;

        mdape.Update(100, 85, isNew: false);
        double afterUpdate = mdape.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mdape = new Mdape(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            mdape.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = mdape.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            mdape.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = mdape.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mdape = new Mdape(DefaultPeriod);
        mdape.Update(100, 95);
        mdape.Update(105, 100);

        mdape.Reset();

        Assert.Equal(0, mdape.Last.Value);
        Assert.False(mdape.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mdape = new Mdape(DefaultPeriod);
        mdape.Update(100, 95);
        mdape.Update(110, 105);

        var result = mdape.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = mdape.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mdape = new Mdape(DefaultPeriod);
        mdape.Update(100, 95);
        mdape.Update(110, 105);

        var result = mdape.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = mdape.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var mdapeIterative = new Mdape(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        const int count = 100;

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02)));
        }

        var iterativeResults = new TSeries();
        for (int i = 0; i < count; i++)
        {
            iterativeResults.Add(mdapeIterative.Update(actualSeries[i], predictedSeries[i]));
        }

        var batchResults = Mdape.Calculate(actualSeries, predictedSeries, DefaultPeriod);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, Precision);
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
            Mdape.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            Mdape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Mdape.Calculate(actualSeries, predictedSeries, DefaultPeriod);
        Mdape.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

        for (int i = 0; i < tseriesResult.Count; i++)
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

        Mdape.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var mdape = new Mdape(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => mdape.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var mdape = new Mdape(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => mdape.Prime(new double[] { 1, 2, 3 }));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => Mdape.Calculate(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Calculate_RobustToOutliers()
    {
        // Median should be robust to extreme outliers
        var mdape = new Mdape(5);

        // Errors: 5%, 5%, 5%, 5%, 500%
        // Sorted: 5, 5, 5, 5, 500
        // Median = 5% (not affected by the outlier 500%)
        mdape.Update(100, 95);   // 5%
        mdape.Update(100, 95);   // 5%
        mdape.Update(100, 95);   // 5%
        mdape.Update(100, 95);   // 5%
        mdape.Update(100, -400); // 500%

        Assert.Equal(5.0, mdape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ZeroActual_ReturnsZeroError()
    {
        // When actual is zero or near-zero, should return 0 error (epsilon protection)
        var mdape = new Mdape(3);

        mdape.Update(0.0, 10);
        mdape.Update(0.0, 20);
        mdape.Update(0.0, 30);

        // With epsilon protection, all errors are 0
        Assert.Equal(0.0, mdape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SlidingWindow_Works()
    {
        var mdape = new Mdape(3);

        // Fill window: errors 5%, 10%, 15% -> sorted 5,10,15 -> median = 10%
        mdape.Update(100, 95);  // 5%
        mdape.Update(100, 90);  // 10%
        mdape.Update(100, 85);  // 15%
        Assert.Equal(10.0, mdape.Last.Value, Precision);

        // Slide: errors 10%, 15%, 20% -> sorted 10,15,20 -> median = 15%
        mdape.Update(100, 80);  // 20%
        Assert.Equal(15.0, mdape.Last.Value, Precision);

        // Slide: errors 15%, 20%, 25% -> sorted 15,20,25 -> median = 20%
        mdape.Update(100, 75);  // 25%
        Assert.Equal(20.0, mdape.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ScaleIndependent()
    {
        // MdAPE should give same result regardless of scale
        var mdape1 = new Mdape(3);
        var mdape2 = new Mdape(3);

        // Scale 1: 100 -> 90 (10% error)
        mdape1.Update(100, 90);
        mdape1.Update(100, 95);
        mdape1.Update(100, 85);

        // Scale 1000: 1000 -> 900 (10% error)
        mdape2.Update(1000, 900);
        mdape2.Update(1000, 950);
        mdape2.Update(1000, 850);

        Assert.Equal(mdape1.Last.Value, mdape2.Last.Value, Precision);
    }
}
