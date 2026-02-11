using Xunit;

namespace QuanTAlib.Tests;

public class StandardizeTests
{
    private readonly GBM _gbm = new(100, 0.05, 0.2, seed: 42);

    [Fact]
    public void Standardize_Constructor_ValidPeriod_SetsProperties()
    {
        var standardize = new Standardize(20);

        Assert.Equal("Standardize(20)", standardize.Name);
        Assert.Equal(20, standardize.WarmupPeriod);
        Assert.False(standardize.IsHot);
    }

    [Fact]
    public void Standardize_Constructor_InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Standardize(1));
        Assert.Throws<ArgumentException>(() => new Standardize(0));
        Assert.Throws<ArgumentException>(() => new Standardize(-1));
    }

    [Fact]
    public void Standardize_Constructor_Period2_IsMinimumValid()
    {
        var standardize = new Standardize(2);
        Assert.Equal("Standardize(2)", standardize.Name);
        Assert.Equal(2, standardize.WarmupPeriod);
    }

    [Fact]
    public void Standardize_Update_BasicCalculation()
    {
        var standardize = new Standardize(5);

        // Feed values: 10, 20, 30, 40, 50
        // Mean = 30, Sample StdDev = sqrt(((10-30)^2 + (20-30)^2 + ... + (50-30)^2) / 4)
        // = sqrt((400 + 100 + 0 + 100 + 400) / 4) = sqrt(250) ≈ 15.811
        // Z-score of 50: (50 - 30) / 15.811 ≈ 1.265
        standardize.Update(new TValue(DateTime.UtcNow, 10));
        standardize.Update(new TValue(DateTime.UtcNow, 20));
        standardize.Update(new TValue(DateTime.UtcNow, 30));
        standardize.Update(new TValue(DateTime.UtcNow, 40));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 50));

        double expectedStdDev = Math.Sqrt(250.0);  // 15.811...
        double expectedZ = (50 - 30) / expectedStdDev;  // ≈ 1.265

        Assert.Equal(expectedZ, result.Value, 1e-6);
    }

    [Fact]
    public void Standardize_Update_MeanValueReturnsZero()
    {
        var standardize = new Standardize(5);

        // Values with known pattern
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 50));

        // Mean = (0 + 100 + 50 + 50 + 50) / 5 = 50
        // Value 50 = mean, so z-score = 0
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Standardize_Update_NegativeZScore()
    {
        var standardize = new Standardize(5);

        // Feed ascending values, then test below mean
        standardize.Update(new TValue(DateTime.UtcNow, 10));
        standardize.Update(new TValue(DateTime.UtcNow, 20));
        standardize.Update(new TValue(DateTime.UtcNow, 30));
        standardize.Update(new TValue(DateTime.UtcNow, 40));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 10));

        // Mean of [10, 20, 30, 40, 10] = 22
        // Value 10 < mean, so z-score should be negative
        Assert.True(result.Value < 0, "Z-score should be negative for below-mean value");
    }

    [Fact]
    public void Standardize_Update_PositiveZScore()
    {
        var standardize = new Standardize(5);

        // Feed descending values, then test above mean
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 40));
        standardize.Update(new TValue(DateTime.UtcNow, 30));
        standardize.Update(new TValue(DateTime.UtcNow, 20));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 50));

        // Value 50 > mean, so z-score should be positive
        Assert.True(result.Value > 0, "Z-score should be positive for above-mean value");
    }

    [Fact]
    public void Standardize_Update_FlatRange_ReturnsZero()
    {
        var standardize = new Standardize(5);

        // All same values
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 100));

        // Flat data: stdev = 0, value = mean, so z-score = 0
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Standardize_Update_IsNew_False_RollsBack()
    {
        var standardize = new Standardize(5);

        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 50));

        var result1 = standardize.Update(new TValue(DateTime.UtcNow, 25), isNew: true);
        var result2 = standardize.Update(new TValue(DateTime.UtcNow, 75), isNew: false);

        // Different values should give different z-scores
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Standardize_Update_NaN_UsesLastValid()
    {
        var standardize = new Standardize(5);

        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        var valid = standardize.Update(new TValue(DateTime.UtcNow, 50));

        var nanResult = standardize.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.Equal(valid.Value, nanResult.Value, 1e-10);
    }

    [Fact]
    public void Standardize_Update_Infinity_UsesLastValid()
    {
        var standardize = new Standardize(5);

        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        var valid = standardize.Update(new TValue(DateTime.UtcNow, 50));

        var infResult = standardize.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.Equal(valid.Value, infResult.Value, 1e-10);
    }

    [Fact]
    public void Standardize_IsHot_BecomesTrue_AfterWarmup()
    {
        var standardize = new Standardize(5);

        for (int i = 0; i < 4; i++)
        {
            standardize.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(standardize.IsHot);
        }

        standardize.Update(new TValue(DateTime.UtcNow, 40));
        Assert.True(standardize.IsHot);
    }

    [Fact]
    public void Standardize_Reset_ClearsState()
    {
        var standardize = new Standardize(5);

        for (int i = 0; i < 10; i++)
        {
            standardize.Update(new TValue(DateTime.UtcNow, i * 10));
        }

        Assert.True(standardize.IsHot);

        standardize.Reset();

        Assert.False(standardize.IsHot);
    }

    [Fact]
    public void Standardize_OutputIsFinite()
    {
        var standardize = new Standardize(20);
        var series = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in series)
        {
            var result = standardize.Update(new TValue(bar.Time, bar.Close));
            Assert.True(double.IsFinite(result.Value),
                $"Standardize output {result.Value} should be finite");
        }
    }

    [Fact]
    public void Standardize_OutputTypicallyInReasonableRange()
    {
        var standardize = new Standardize(20);
        var series = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        int extremeCount = 0;
        foreach (var bar in series)
        {
            var result = standardize.Update(new TValue(bar.Time, bar.Close));
            // Most z-scores should be within ±4 for normal data
            if (Math.Abs(result.Value) > 4)
            {
                extremeCount++;
            }
        }

        // Allow up to 5% extreme values
        Assert.True(extremeCount < 25, $"Too many extreme z-scores: {extremeCount}");
    }

    [Fact]
    public void Standardize_Chaining_WorksCorrectly()
    {
        var source = new TSeries();
        var standardize = new Standardize(source, 10);

        for (int i = 0; i < 20; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), i * 5));
        }

        Assert.True(standardize.IsHot);
        // Last value in a linear sequence should have positive z-score
        Assert.True(standardize.Last.Value > 0);
    }

    [Fact]
    public void Standardize_StaticCalculate_TSeries_MatchesStreaming()
    {
        var series = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var tseries = new TSeries();
        foreach (var bar in series)
        {
            tseries.Add(new TValue(bar.Time, bar.Close), true);
        }

        // Static calculation
        var staticResult = Standardize.Batch(tseries, 14);

        // Streaming calculation
        var streamStandardize = new Standardize(14);
        var streamResult = new TSeries();
        foreach (var bar in series)
        {
            streamResult.Add(streamStandardize.Update(new TValue(bar.Time, bar.Close)), true);
        }

        // Compare last 50 values
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(staticResult[i].Value, streamResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Standardize_StaticCalculate_Span_MatchesStreaming()
    {
        var series = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] values = series.Select(b => b.Close).ToArray();
        double[] output = new double[values.Length];

        // Span calculation
        Standardize.Batch(values, output, 14);

        // Streaming calculation
        var standardize = new Standardize(14);
        for (int i = 0; i < values.Length; i++)
        {
            var result = standardize.Update(new TValue(DateTime.UtcNow, values[i]));
            Assert.Equal(output[i], result.Value, 1e-10);
        }
    }

    [Fact]
    public void Standardize_StaticCalculate_Span_ValidatesParameters()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Standardize.Batch([], output));
        Assert.Throws<ArgumentException>(() => Standardize.Batch(source, new double[3]));
        Assert.Throws<ArgumentException>(() => Standardize.Batch(source, output, 1));
    }

    [Fact]
    public void Standardize_RollingWindow_AdaptsToNewData()
    {
        var standardize = new Standardize(3);

        // Feed: 0, 50, 100 -> window complete
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 100));

        // Mean = 50, value = 100, should be positive z-score
        Assert.True(standardize.Last.Value > 0);

        // Now feed 0, window becomes [50, 100, 0]
        // Mean = 50, value = 0, should be negative z-score
        var result = standardize.Update(new TValue(DateTime.UtcNow, 0));
        Assert.True(result.Value < 0);
    }

    [Fact]
    public void Standardize_SampleStdDev_UsesN_Minus_1()
    {
        var standardize = new Standardize(3);

        // Values: 2, 4, 6
        // Mean = 4
        // Sum of squared deviations = (2-4)² + (4-4)² + (6-4)² = 4 + 0 + 4 = 8
        // Sample variance = 8 / (3-1) = 4
        // Sample StdDev = 2
        // Z-score of 6: (6 - 4) / 2 = 1

        standardize.Update(new TValue(DateTime.UtcNow, 2));
        standardize.Update(new TValue(DateTime.UtcNow, 4));
        var result = standardize.Update(new TValue(DateTime.UtcNow, 6));

        Assert.Equal(1.0, result.Value, 1e-10);
    }

    [Fact]
    public void Standardize_Symmetry_PositiveAndNegative()
    {
        var standardize = new Standardize(5);

        // Create symmetric distribution around 50
        standardize.Update(new TValue(DateTime.UtcNow, 30));
        standardize.Update(new TValue(DateTime.UtcNow, 40));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 60));
        standardize.Update(new TValue(DateTime.UtcNow, 70));
        // Mean = 50, StdDev = sqrt(200)

        // Now test symmetry
        standardize.Reset();
        standardize.Update(new TValue(DateTime.UtcNow, 30));
        standardize.Update(new TValue(DateTime.UtcNow, 40));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 60));
        var zPositive = standardize.Update(new TValue(DateTime.UtcNow, 70));  // Above mean

        standardize.Reset();
        standardize.Update(new TValue(DateTime.UtcNow, 70));
        standardize.Update(new TValue(DateTime.UtcNow, 60));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 40));
        var zNegative = standardize.Update(new TValue(DateTime.UtcNow, 30));  // Below mean

        // Symmetric: |z(70)| should equal |z(30)|
        Assert.Equal(Math.Abs(zPositive.Value), Math.Abs(zNegative.Value), 1e-10);
        Assert.True(zPositive.Value > 0, "Z-score for above-mean value should be positive");
        Assert.True(zNegative.Value < 0, "Z-score for below-mean value should be negative");
    }

    [Fact]
    public void Standardize_NegativeValues_WorksCorrectly()
    {
        var standardize = new Standardize(5);

        // Range from -100 to +100
        standardize.Update(new TValue(DateTime.UtcNow, -100));
        standardize.Update(new TValue(DateTime.UtcNow, -50));
        standardize.Update(new TValue(DateTime.UtcNow, 0));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 100));

        // Mean = 0, so z-score of 100 should be positive and equal to z-score of 0 
        // z = (100 - 0) / stdev
        Assert.True(standardize.Last.Value > 0);

        // Test zero: should have z-score of 0
        standardize.Reset();
        standardize.Update(new TValue(DateTime.UtcNow, -100));
        standardize.Update(new TValue(DateTime.UtcNow, -50));
        standardize.Update(new TValue(DateTime.UtcNow, 50));
        standardize.Update(new TValue(DateTime.UtcNow, 100));
        var zeroResult = standardize.Update(new TValue(DateTime.UtcNow, 0));
        Assert.Equal(0.0, zeroResult.Value, 1e-10);
    }

    [Fact]
    public void Standardize_Prime_WorksCorrectly()
    {
        var standardize = new Standardize(5);

        double[] primeData = [10, 20, 30, 40, 50];
        standardize.Prime(primeData);

        Assert.True(standardize.IsHot);
        // After prime, should have valid z-score
        Assert.True(double.IsFinite(standardize.Last.Value));
    }
}
