using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class GaussTests
{
    [Fact]
    public void Constructor_LimitsSigma()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Gauss(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Gauss(-1.0));
    }

    [Fact]
    public void Constructor_CalculatesKernelSizeCorrectly()
    {
        // Kernel size = 2 * ceil(3 * sigma) + 1

        // sigma = 1.0 -> 3 * 1 = 3 -> ceil(3) = 3 -> 2*3 + 1 = 7
        Assert.Equal(7, new Gauss(1.0).KernelSize);

        // sigma = 0.5 -> 3 * 0.5 = 1.5 -> ceil(1.5) = 2 -> 2*2 + 1 = 5
        Assert.Equal(5, new Gauss(0.5).KernelSize);

        // sigma = 2.0 -> 3 * 2 = 6 -> ceil(6) = 6 -> 2*6 + 1 = 13
        Assert.Equal(13, new Gauss(2.0).KernelSize);
    }

    [Fact]
    public void Update_CalculatesCorrectly()
    {
        // Simple case: constant series should remain constant
        var gauss = new Gauss(1.0);
        for (int i = 0; i < 20; i++)
        {
            var result = gauss.Update(new TValue(DateTime.MinValue, 100));
            Assert.Equal(100, result.Value, 8);
        }
    }

    [Fact]
    public void Calculate_MatchesUpdate()
    {
        var source = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.MinValue.AddSeconds(i), 100 + Math.Sin(i * 0.1) * 10));
        }

        var gauss = new Gauss(1.0);
        var seriesResult = gauss.Update(source);

        // Streaming update comparison
        var gaussStream = new Gauss(1.0);
        for (int i = 0; i < source.Count; i++)
        {
            var streamVal = gaussStream.Update(source[i]);
            Assert.Equal(seriesResult[i].Value, streamVal.Value, 1e-9);
        }

        // Static calculate comparison
        double[] output = new double[source.Count];
        Gauss.Calculate(source.Values, output, 1.0);
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(seriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void HandlesNaN()
    {
        var gauss = new Gauss(1.0);

        // Fill with numbers
        gauss.Update(new TValue(DateTime.MinValue, 10));
        gauss.Update(new TValue(DateTime.MinValue, 20));

        // Update with NaN
        var result = gauss.Update(new TValue(DateTime.MinValue, double.NaN));

        // Should return weighted average of non-NaN values (10, 20)
        // With partial history, it processes available valid numbers.
        Assert.False(double.IsNaN(result.Value));

        // Fill full buffer with NaNs
        for (int i=0; i<20; i++)
            gauss.Update(new TValue(DateTime.MinValue, double.NaN));

        // Should evaluate to NaN if all are NaN
        Assert.True(double.IsNaN(gauss.Last.Value));
    }

    [Fact]
    public void WarmupPeriod_IsKernelSize()
    {
        var gauss = new Gauss(1.0); // Kernel size 7
        Assert.Equal(7, gauss.WarmupPeriod);
        Assert.False(gauss.IsHot);

        for (int i = 0; i < 6; i++)
            gauss.Update(new TValue(DateTime.MinValue, i));

        Assert.False(gauss.IsHot);

        gauss.Update(new TValue(DateTime.MinValue, 6));
        Assert.True(gauss.IsHot);
    }
}