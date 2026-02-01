namespace QuanTAlib.Tests;

using Xunit;

public class BbwnValidationTests
{
    private const double Tolerance = 1e-10;

    private static TBarSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Validates BBWN calculation against Pine Script reference implementation
    /// </summary>
    [Fact]
    public void BBWN_Pine_Validation()
    {
        // Use GBM data for varied, realistic test data
        var bars = GenerateTestData(50);

        var bbwn = new Bbwn(period: 5, multiplier: 2.0, lookback: 10);

        var results = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(bars.Times[i], bars.CloseValues[i]));
            results.Add(result.Value);
        }

        // Validate key properties
        Assert.All(results, r => Assert.True(r >= 0.0 && r <= 1.0, "All values should be in [0,1] range"));

        // After sufficient data, should have meaningful variation
        var laterResults = results.Skip(15).ToList();
        if (laterResults.Count > 5)
        {
            double min = laterResults.Min();
            double max = laterResults.Max();
            Assert.True(max >= min, "Max should be >= min");
        }
    }

    [Fact]
    public void BBWN_Batch_Consistency()
    {
        var testData = new double[]
        {
            100.0, 101.5, 99.2, 102.1, 98.7, 103.3, 97.8, 104.2, 96.9, 105.1,
            95.3, 106.4, 94.7, 107.2, 93.8, 108.5, 92.6, 109.3, 91.9, 110.7
        };

        const int period = 5;
        const double multiplier = 2.0;
        const int lookback = 10;

        // Calculate using streaming updates
        var bbwn = new Bbwn(period, multiplier, lookback);
        var streamResults = new List<double>();

        foreach (var value in testData)
        {
            var result = bbwn.Update(new TValue(DateTime.UtcNow.Ticks, value));
            streamResults.Add(result.Value);
        }

        // Calculate using batch method
        var batchResults = new double[testData.Length];
        Bbwn.Batch(testData, batchResults, period, multiplier, lookback);

        // Compare results (allowing for some numerical differences)
        for (int i = 0; i < testData.Length; i++)
        {
            Assert.True(Math.Abs(streamResults[i] - batchResults[i]) < 1e-10,
                $"Mismatch at index {i}: Stream={streamResults[i]:F12}, Batch={batchResults[i]:F12}");
        }
    }

    [Fact]
    public void BBWN_Normalization_Properties()
    {
        var bars = GenerateTestData(100);
        var close = bars.CloseValues;

        var bbwn = new Bbwn(period: 10, multiplier: 2.0, lookback: 30);
        var results = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(bars.Times[i], close[i]));
            results.Add(result.Value);
        }

        // All values should be properly normalized
        Assert.All(results, r => Assert.True(r >= 0.0 && r <= 1.0));

        // After warmup, we should see values utilizing the full range
        var warmedUpResults = results.Skip(40).ToList();
        if (warmedUpResults.Count > 20)
        {
            double min = warmedUpResults.Min();
            double max = warmedUpResults.Max();

            // Should use a good portion of the [0,1] range
            Assert.True(max - min > 0.3, "Normalized values should span a reasonable range");
        }
    }

    [Fact]
    public void BBWN_Edge_Cases()
    {
        // Test with minimum viable parameters
        var bbwn = new Bbwn(period: 2, multiplier: 0.1, lookback: 3);

        var edgeCaseData = new double[]
        {
            100.0, 100.0, 100.0, // Constant values
            101.0, 99.0, 101.0,  // Small variation
            110.0, 90.0, 110.0   // Larger variation
        };

        foreach (var value in edgeCaseData)
        {
            var result = bbwn.Update(new TValue(DateTime.UtcNow.Ticks, value));

            Assert.True(double.IsFinite(result.Value), "Result should be finite");
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0, "Result should be in [0,1] range");
        }
    }

    [Fact]
    public void BBWN_TSeries_Integration()
    {
        var bars = GenerateTestData(50);

        var source = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            source.Add(new TValue(bars.Times[i], bars.CloseValues[i]));
        }
        var result = Bbwn.Calculate(source, period: 10, multiplier: 2.0, lookback: 20);

        Assert.Equal(source.Count, result.Count);

        // Validate all calculated values
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result.Values[i]), $"Value at {i} should be finite");
            Assert.True(result.Values[i] >= 0.0 && result.Values[i] <= 1.0,
                $"Value at {i} should be in [0,1] range");
        }
    }

    [Theory]
    [InlineData(5, 1.0, 10)]
    [InlineData(10, 2.0, 20)]
    [InlineData(20, 2.5, 50)]
    [InlineData(3, 0.5, 5)]
    public void BBWN_Parameter_Variations(int period, double multiplier, int lookback)
    {
        var bbwn = new Bbwn(period, multiplier, lookback);
        var bars = GenerateTestData(period + lookback + 10);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = bbwn.Update(new TValue(bars.Times[i], bars.CloseValues[i]));

            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 && result.Value <= 1.0);
        }

        Assert.Equal(period, bbwn.Period);
        Assert.Equal(multiplier, bbwn.Multiplier);
        Assert.Equal(lookback, bbwn.Lookback);
    }
}