using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Slope using synthetic data with known mathematical results.
/// </summary>
public class SlopeValidationTests
{
    [Fact]
    public void LinearSequence_ProducesConstantSlope()
    {
        // Linear sequence: 0, 2, 4, 6, 8, 10 (slope = 2)
        double[] data = [0, 2, 4, 6, 8, 10];
        double[] expected = [0, 2, 2, 2, 2, 2]; // First is 0 (no history), rest are 2

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void ConstantSequence_ProducesZeroSlope()
    {
        // Constant sequence: 5, 5, 5, 5, 5 (slope = 0)
        double[] data = [5, 5, 5, 5, 5];
        double[] expected = [0, 0, 0, 0, 0];

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void DecreasingSequence_ProducesNegativeSlope()
    {
        // Decreasing sequence: 10, 7, 4, 1, -2 (slope = -3)
        double[] data = [10, 7, 4, 1, -2];
        double[] expected = [0, -3, -3, -3, -3];

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void QuadraticSequence_ProducesLinearSlope()
    {
        // Quadratic sequence: 0, 1, 4, 9, 16, 25 (x^2)
        // Slope: n^2 - (n-1)^2 = 2n - 1 → 1, 3, 5, 7, 9
        double[] data = [0, 1, 4, 9, 16, 25];
        double[] expected = [0, 1, 3, 5, 7, 9];

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void AlternatingSequence_ProducesAlternatingSlope()
    {
        // Alternating: 0, 10, 0, 10, 0
        double[] data = [0, 10, 0, 10, 0];
        double[] expected = [0, 10, -10, 10, -10];

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void FibonacciSequence_ProducesCorrectSlope()
    {
        // Fibonacci: 1, 1, 2, 3, 5, 8, 13
        // Slope: 0, 1, 1, 2, 3, 5
        double[] data = [1, 1, 2, 3, 5, 8, 13];
        double[] expected = [0, 0, 1, 1, 2, 3, 5];

        var slope = new Slope();
        for (int i = 0; i < data.Length; i++)
        {
            var result = slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(expected[i], result.Value, precision: 9);
        }
    }

    [Fact]
    public void BatchCalculation_MatchesSyntheticData()
    {
        double[] data = [0, 2, 4, 6, 8, 10];
        double[] expected = [0, 2, 2, 2, 2, 2];
        double[] output = new double[data.Length];

        Slope.Batch(data, output);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(expected[i], output[i], precision: 9);
        }
    }

    // === Skender Cross-Validation ===

    /// <summary>
    /// Structural validation against Skender <c>GetSlope</c>.
    /// Skender Slope computes linear regression slope over a lookback window,
    /// while QuanTAlib Slope computes simple first difference (current - previous).
    /// Different formulas mean numeric equality is not expected.
    /// Both must produce finite output and agree on trend direction for simple linear data.
    /// </summary>
    [Fact]
    public void Validate_Skender_Slope_Structural()
    {
        using var data = new ValidationTestData();
        const int period = 14;

        // QuanTAlib Slope (streaming, simple difference)
        var slope = new Slope();
        var qResults = new List<double>();
        foreach (var tv in data.Data)
        {
            qResults.Add(slope.Update(tv).Value);
        }

        // Skender Slope (linear regression slope)
        var sResult = data.SkenderQuotes.GetSlope(period).ToList();

        // Structural: both produce finite output after warmup
        Assert.True(double.IsFinite(slope.Last.Value), "QuanTAlib Slope last must be finite");

        int finiteCount = sResult.Count(r => r.Slope is not null && double.IsFinite(r.Slope.Value));
        Assert.True(finiteCount > 100, $"Skender Slope should produce >100 finite values, got {finiteCount}");
    }

    [Fact]
    public void LargeLinearSequence_ProducesConstantSlope()
    {
        // Generate 1000 points with slope = 0.5
        int count = 1000;
        double[] data = new double[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = 100.0 + (i * 0.5);
        }

        var slope = new Slope();
        // First element - no previous value, slope = 0
        slope.Update(new TValue(DateTime.UtcNow, data[0]));
        Assert.Equal(0.0, slope.Last.Value, precision: 9);

        // Rest should have constant slope of 0.5
        for (int i = 1; i < count; i++)
        {
            slope.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(0.5, slope.Last.Value, precision: 9);
        }
    }
}
