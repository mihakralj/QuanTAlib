using Xunit;

namespace QuanTAlib.Tests;

public class MdaeTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Mdae(0));
        Assert.Throws<ArgumentException>(() => new Mdae(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var mdae = new Mdae(DefaultPeriod);
        Assert.NotNull(mdae);
        Assert.Equal(DefaultPeriod, mdae.WarmupPeriod);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var mdae = new Mdae(DefaultPeriod);
        Assert.True(mdae.Name.Contains("Mdae", StringComparison.Ordinal));
        Assert.False(mdae.IsHot);
        Assert.Equal(0, mdae.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var mdae = new Mdae(5);
        for (int i = 0; i < 4; i++)
        {
            mdae.Update(100 + i, 100);
            Assert.False(mdae.IsHot);
        }
        mdae.Update(104, 100);
        Assert.True(mdae.IsHot);
    }

    [Fact]
    public void Calculate_ReturnsCorrectMedian()
    {
        // MdAE = Median of |actual - predicted|
        var mdae = new Mdae(5);

        // Errors: |10-8|=2, |12-10|=2, |15-14|=1, |20-18|=2, |25-20|=5
        // Sorted errors: 1, 2, 2, 2, 5
        // Median = 2 (middle value)
        mdae.Update(10, 8);
        mdae.Update(12, 10);
        mdae.Update(15, 14);
        mdae.Update(20, 18);
        mdae.Update(25, 20);

        Assert.Equal(2.0, mdae.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_EvenCount_AveragesTwoMiddle()
    {
        // Test median with even count
        var mdae = new Mdae(4);

        // Errors: 1, 2, 3, 4 -> sorted: 1, 2, 3, 4
        // Median = (2 + 3) / 2 = 2.5
        mdae.Update(10, 9);  // error = 1
        mdae.Update(20, 18); // error = 2
        mdae.Update(30, 27); // error = 3
        mdae.Update(40, 36); // error = 4

        Assert.Equal(2.5, mdae.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var mdae = new Mdae(5);
        for (int i = 0; i < 5; i++)
        {
            mdae.Update(100, 100);
        }
        Assert.Equal(0.0, mdae.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var mdae = new Mdae(DefaultPeriod);
        mdae.Update(100, 95);
        mdae.Update(110, 108, isNew: true);
        double beforeUpdate = mdae.Last.Value;

        mdae.Update(110, 105, isNew: false);
        double afterUpdate = mdae.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var mdae = new Mdae(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            mdae.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = mdae.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            mdae.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = mdae.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mdae = new Mdae(DefaultPeriod);
        mdae.Update(100, 95);
        mdae.Update(105, 100);

        mdae.Reset();

        Assert.Equal(0, mdae.Last.Value);
        Assert.False(mdae.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var mdae = new Mdae(DefaultPeriod);
        mdae.Update(100, 95);
        mdae.Update(110, 105);

        var result = mdae.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = mdae.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var mdae = new Mdae(DefaultPeriod);
        mdae.Update(100, 95);
        mdae.Update(110, 105);

        var result = mdae.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = mdae.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var mdaeIterative = new Mdae(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02)));
        }

        var iterativeResults = new List<double>(actualSeries.Count);
        foreach (var (actual, predicted) in actualSeries.Zip(predictedSeries))
        {
            iterativeResults.Add(mdaeIterative.Update(actual, predicted).Value);
        }

        var batchResults = Mdae.Calculate(actualSeries, predictedSeries, DefaultPeriod);

        Assert.Equal(100, iterativeResults.Count);
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
            Mdae.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            Mdae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));
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

        var tseriesResult = Mdae.Calculate(actualSeries, predictedSeries, DefaultPeriod);
        Mdae.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

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

        Mdae.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var mdae = new Mdae(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => mdae.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var mdae = new Mdae(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => mdae.Prime(new double[] { 1, 2, 3 }));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => Mdae.Calculate(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Calculate_RobustToOutliers()
    {
        // Median should be robust to extreme outliers
        var mdae = new Mdae(5);

        // Errors: 1, 1, 1, 1, 1000
        // Sorted: 1, 1, 1, 1, 1000
        // Median = 1 (not affected by the outlier 1000)
        mdae.Update(10, 9);   // error = 1
        mdae.Update(20, 19);  // error = 1
        mdae.Update(30, 29);  // error = 1
        mdae.Update(40, 39);  // error = 1
        mdae.Update(50, -950); // error = 1000

        Assert.Equal(1.0, mdae.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SlidingWindow_Works()
    {
        var mdae = new Mdae(3);

        // Fill window: errors 1, 2, 3 -> sorted 1,2,3 -> median = 2
        mdae.Update(10, 9);  // 1
        mdae.Update(20, 18); // 2
        mdae.Update(30, 27); // 3
        Assert.Equal(2.0, mdae.Last.Value, Precision);

        // Slide: errors 2, 3, 4 -> sorted 2,3,4 -> median = 3
        mdae.Update(40, 36); // 4
        Assert.Equal(3.0, mdae.Last.Value, Precision);

        // Slide: errors 3, 4, 5 -> sorted 3,4,5 -> median = 4
        mdae.Update(50, 45); // 5
        Assert.Equal(4.0, mdae.Last.Value, Precision);
    }
}
