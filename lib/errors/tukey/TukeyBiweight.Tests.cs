namespace QuanTAlib.Tests;

public class TukeyBiweightTests
{
    private const double Precision = 1e-10;
    private const int DefaultPeriod = 10;
    private const double DefaultC = 4.685;

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new TukeyBiweight(0));
        Assert.Throws<ArgumentException>(() => new TukeyBiweight(-1));
        Assert.Throws<ArgumentException>(() => new TukeyBiweight(10, 0.0));
        Assert.Throws<ArgumentException>(() => new TukeyBiweight(10, -1.0));
    }

    [Fact]
    public void Constructor_ValidPeriod_Succeeds()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        Assert.NotNull(tukey);
        Assert.Equal(DefaultPeriod, tukey.WarmupPeriod);
        Assert.Equal(DefaultC, tukey.C);
    }

    [Fact]
    public void Constructor_CustomC_Succeeds()
    {
        var tukey = new TukeyBiweight(DefaultPeriod, 6.0);
        Assert.Equal(6.0, tukey.C);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        Assert.Contains("TukeyBiweight", tukey.Name, StringComparison.Ordinal);
        Assert.False(tukey.IsHot);
        Assert.Equal(0, tukey.Last.Value);
    }

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var tukey = new TukeyBiweight(5);
        for (int i = 0; i < 4; i++)
        {
            tukey.Update(100 + i, 100);
            Assert.False(tukey.IsHot);
        }
        tukey.Update(104, 100);
        Assert.True(tukey.IsHot);
    }

    [Fact]
    public void Calculate_PerfectPredictions_ReturnsZero()
    {
        var tukey = new TukeyBiweight(5);
        for (int i = 0; i < 5; i++)
        {
            tukey.Update(100, 100);
        }
        Assert.Equal(0.0, tukey.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_SmallError_ReturnsLessThanMaxLoss()
    {
        var tukey = new TukeyBiweight(1, 4.685);

        // Small error within threshold
        tukey.Update(100, 99);  // error = 1 < 4.685

        double cSquaredOver6 = (4.685 * 4.685) / 6.0;
        Assert.True(tukey.Last.Value < cSquaredOver6);
        Assert.True(tukey.Last.Value > 0);
    }

    [Fact]
    public void Calculate_LargeError_ReturnsMaxLoss()
    {
        var tukey = new TukeyBiweight(1, 4.685);

        // Large error beyond threshold
        tukey.Update(100, 90);  // error = 10 > 4.685

        double cSquaredOver6 = (4.685 * 4.685) / 6.0;
        Assert.Equal(cSquaredOver6, tukey.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_ErrorAtThreshold_ApproachesMaxLoss()
    {
        var tukey = new TukeyBiweight(1, 4.685);

        // Error at threshold
        tukey.Update(100, 100 - 4.685);

        double cSquaredOver6 = (4.685 * 4.685) / 6.0;
        // At boundary, (1 - (1 - 1)³) = 1, so loss = c²/6
        Assert.Equal(cSquaredOver6, tukey.Last.Value, 1e-6);
    }

    [Fact]
    public void Calculate_SymmetricErrors()
    {
        // Loss should be same for positive and negative errors of same magnitude
        var tukey1 = new TukeyBiweight(1);
        var tukey2 = new TukeyBiweight(1);

        tukey1.Update(100, 97);  // error = 3
        tukey2.Update(100, 103); // error = -3

        Assert.Equal(tukey1.Last.Value, tukey2.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_OutliersClipped()
    {
        // Verify that outliers beyond c give same loss regardless of magnitude
        var tukey = new TukeyBiweight(3, 4.685);
        double cSquaredOver6 = (4.685 * 4.685) / 6.0;

        tukey.Update(100, 90);   // error = 10 (outlier)
        tukey.Update(100, 50);   // error = 50 (bigger outlier)
        tukey.Update(100, 0);    // error = 100 (huge outlier)

        // All outliers should give same max loss
        Assert.Equal(cSquaredOver6, tukey.Last.Value, Precision);
    }

    [Fact]
    public void Calculate_IsNew_False_UpdatesValue()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        tukey.Update(100, 99);
        tukey.Update(100, 98, isNew: true);
        double beforeUpdate = tukey.Last.Value;

        tukey.Update(100, 90, isNew: false);
        double afterUpdate = tukey.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var tukey = new TukeyBiweight(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthActual = default;
        TValue tenthPredicted = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthActual = new TValue(bar.Time, bar.Close);
            tenthPredicted = new TValue(bar.Time, bar.Close * 0.98);
            tukey.Update(tenthActual, tenthPredicted, isNew: true);
        }

        double stateAfterTen = tukey.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            tukey.Update(new TValue(bar.Time, bar.Close), new TValue(bar.Time, bar.Close * 0.95), isNew: false);
        }

        TValue finalResult = tukey.Update(tenthActual, tenthPredicted, isNew: false);
        Assert.Equal(stateAfterTen, finalResult.Value, Precision);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        tukey.Update(100, 95);
        tukey.Update(105, 100);

        tukey.Reset();

        Assert.Equal(0, tukey.Last.Value);
        Assert.False(tukey.IsHot);
    }

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        tukey.Update(100, 95);
        tukey.Update(110, 105);

        var result = tukey.Update(double.NaN, 108);
        Assert.True(double.IsFinite(result.Value));

        result = tukey.Update(115, double.NaN);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        tukey.Update(100, 95);
        tukey.Update(110, 105);

        var result = tukey.Update(double.PositiveInfinity, 108);
        Assert.True(double.IsFinite(result.Value));

        result = tukey.Update(115, double.NegativeInfinity);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var tukeyIterative = new TukeyBiweight(DefaultPeriod);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var actualSeries = new TSeries();
        var predictedSeries = new TSeries();

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            actualSeries.Add(bar.Time, bar.Close);
            predictedSeries.Add(bar.Time, bar.Close * (1 + (i % 2 == 0 ? 0.02 : -0.02)));
        }

        var iterativeResults = new List<double>();
        foreach (var (actual, predicted) in actualSeries.Zip(predictedSeries))
        {
            iterativeResults.Add(tukeyIterative.Update(actual, predicted).Value);
        }

        var batchResults = TukeyBiweight.Calculate(actualSeries, predictedSeries, DefaultPeriod);

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
            TukeyBiweight.Batch(actual.AsSpan(), predicted.AsSpan(), wrongSizeOutput.AsSpan(), DefaultPeriod));

        Assert.Throws<ArgumentException>(() =>
            TukeyBiweight.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 0));

        Assert.Throws<ArgumentException>(() =>
            TukeyBiweight.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), DefaultPeriod, 0.0));
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

        var tseriesResult = TukeyBiweight.Calculate(actualSeries, predictedSeries, DefaultPeriod);
        TukeyBiweight.Batch(actualArr.AsSpan(), predictedArr.AsSpan(), output.AsSpan(), DefaultPeriod);

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

        TukeyBiweight.Batch(actual.AsSpan(), predicted.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Update_ThrowsOnSingleInput()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => tukey.Update(new TValue(DateTime.UtcNow, 100)));
    }

    [Fact]
    public void Prime_ThrowsNotSupported()
    {
        var tukey = new TukeyBiweight(DefaultPeriod);
        Assert.Throws<NotSupportedException>(() => tukey.Prime([1, 2, 3]));
    }

    [Fact]
    public void Calculate_MismatchedSeriesLengths_Throws()
    {
        var actual = new TSeries();
        var predicted = new TSeries();

        actual.Add(DateTime.UtcNow.Ticks, 100);
        actual.Add(DateTime.UtcNow.Ticks + 1, 110);

        predicted.Add(DateTime.UtcNow.Ticks, 98);

        Assert.Throws<ArgumentException>(() => TukeyBiweight.Calculate(actual, predicted, DefaultPeriod));
    }

    [Fact]
    public void Resync_PreventsFloatingPointDrift()
    {
        var tukey = new TukeyBiweight(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 1100; i++)
        {
            var bar = gbm.Next(isNew: true);
            tukey.Update(bar.Close, bar.Close * 0.98);
        }

        Assert.True(double.IsFinite(tukey.Last.Value));
        Assert.True(tukey.Last.Value >= 0);
    }

    [Fact]
    public void Calculate_Bounded()
    {
        // Tukey loss is bounded between 0 and c²/6
        var tukey = new TukeyBiweight(5, 4.685);
        double maxLoss = (4.685 * 4.685) / 6.0;
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.5, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            tukey.Update(bar.Close, bar.Close * (1 + (i % 3 - 1) * 0.2));
            Assert.True(tukey.Last.Value >= 0, $"Loss should be non-negative, got {tukey.Last.Value}");
            Assert.True(tukey.Last.Value <= maxLoss, $"Loss should be <= {maxLoss}, got {tukey.Last.Value}");
        }
    }

    [Fact]
    public void Calculate_RobustToOutliers()
    {
        // Tukey should be highly robust - outliers have limited influence
        var tukey = new TukeyBiweight(5, 4.685);
        double cSquaredOver6 = (4.685 * 4.685) / 6.0;

        // 4 small errors + 1 extreme outlier
        tukey.Update(100, 99);    // small error
        tukey.Update(100, 99);    // small error
        tukey.Update(100, 99);    // small error
        tukey.Update(100, 99);    // small error
        tukey.Update(100, -1000); // extreme outlier

        // Result should be bounded by max loss even with extreme outlier
        Assert.True(tukey.Last.Value <= cSquaredOver6);
    }

    [Fact]
    public void Calculate_DifferentC_AffectsThreshold()
    {
        var tukeySmallC = new TukeyBiweight(1, 2.0);
        var tukeyLargeC = new TukeyBiweight(1, 6.0);

        // Error = 3: within c=6 but outside c=2
        tukeySmallC.Update(100, 97);
        tukeyLargeC.Update(100, 97);

        double smallCMax = (2.0 * 2.0) / 6.0;
        double largeCMax = (6.0 * 6.0) / 6.0;

        // Small c should give max loss (error beyond threshold)
        Assert.Equal(smallCMax, tukeySmallC.Last.Value, Precision);

        // Large c should give less than max loss (error within threshold)
        Assert.True(tukeyLargeC.Last.Value < largeCMax);
    }
}
