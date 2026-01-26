using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// SQRTTRANS validation tests - validates against Math.Sqrt (standard library)
/// </summary>
public class SqrttransValidationTests
{
    private const double Tolerance = 1e-14;

    [Fact]
    public void Sqrttrans_Batch_MatchesMathSqrt()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var result = Sqrttrans.Calculate(source);

        for (int i = 0; i < source.Count; i++)
        {
            double expected = Math.Sqrt(source[i].Value);
            Assert.Equal(expected, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Sqrttrans_Streaming_MatchesMathSqrt()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var indicator = new Sqrttrans();

        for (int i = 0; i < source.Count; i++)
        {
            indicator.Update(source[i]);
            double expected = Math.Sqrt(source[i].Value);
            Assert.Equal(expected, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Sqrttrans_Span_MatchesMathSqrt()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var values = source.Values.ToArray();
        var output = new double[count];
        Sqrttrans.Calculate(values, output);

        for (int i = 0; i < count; i++)
        {
            double expected = Math.Sqrt(values[i]);
            Assert.Equal(expected, output[i], Tolerance);
        }
    }

    [Fact]
    public void Sqrttrans_KnownPerfectSquares()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        // sqrt(0) = 0
        indicator.Update(new TValue(time, 0.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);

        // sqrt(1) = 1
        indicator.Update(new TValue(time.AddMinutes(1), 1.0));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);

        // sqrt(4) = 2
        indicator.Update(new TValue(time.AddMinutes(2), 4.0));
        Assert.Equal(2.0, indicator.Last.Value, Tolerance);

        // sqrt(9) = 3
        indicator.Update(new TValue(time.AddMinutes(3), 9.0));
        Assert.Equal(3.0, indicator.Last.Value, Tolerance);

        // sqrt(16) = 4
        indicator.Update(new TValue(time.AddMinutes(4), 16.0));
        Assert.Equal(4.0, indicator.Last.Value, Tolerance);

        // sqrt(25) = 5
        indicator.Update(new TValue(time.AddMinutes(5), 25.0));
        Assert.Equal(5.0, indicator.Last.Value, Tolerance);

        // sqrt(100) = 10
        indicator.Update(new TValue(time.AddMinutes(6), 100.0));
        Assert.Equal(10.0, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Sqrttrans_KnownIrrationalResults()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        // sqrt(2) ≈ 1.41421356...
        indicator.Update(new TValue(time, 2.0));
        Assert.Equal(Math.Sqrt(2.0), indicator.Last.Value, Tolerance);

        // sqrt(3) ≈ 1.73205080...
        indicator.Update(new TValue(time.AddMinutes(1), 3.0));
        Assert.Equal(Math.Sqrt(3.0), indicator.Last.Value, Tolerance);

        // sqrt(5) ≈ 2.23606797...
        indicator.Update(new TValue(time.AddMinutes(2), 5.0));
        Assert.Equal(Math.Sqrt(5.0), indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Sqrttrans_InverseOfSquare()
    {
        // sqrt(x^2) = |x| for all x
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60003);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        // Square the values first
        var squared = new TSeries();
        foreach (var tv in source)
        {
            squared.Add(new TValue(tv.Time, tv.Value * tv.Value));
        }

        var sqrtResult = Sqrttrans.Calculate(squared);

        for (int i = 0; i < source.Count; i++)
        {
            // sqrt(x^2) = |x|
            Assert.Equal(Math.Abs(source[i].Value), sqrtResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Sqrttrans_ProductRule()
    {
        // sqrt(a * b) = sqrt(a) * sqrt(b) for a,b >= 0
        double a = 16.0;
        double b = 25.0;

        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double sqrtA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double sqrtB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a * b));
        double sqrtAB = indicator.Last.Value;

        Assert.Equal(sqrtA * sqrtB, sqrtAB, Tolerance);
    }

    [Fact]
    public void Sqrttrans_QuotientRule()
    {
        // sqrt(a / b) = sqrt(a) / sqrt(b) for a >= 0, b > 0
        double a = 100.0;
        double b = 25.0;

        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double sqrtA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double sqrtB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a / b));
        double sqrtAOverB = indicator.Last.Value;

        Assert.Equal(sqrtA / sqrtB, sqrtAOverB, Tolerance);
    }

    [Fact]
    public void Sqrttrans_PowerRelationship()
    {
        // sqrt(x) = x^0.5
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 60004);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var indicator = new Sqrttrans();

        for (int i = 0; i < source.Count; i++)
        {
            indicator.Update(source[i]);
            double expected = Math.Pow(source[i].Value, 0.5);
            Assert.Equal(expected, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Sqrttrans_SmallValues()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        // Test small positive values
        double[] smallValues = { 1e-10, 1e-8, 1e-6, 1e-4, 1e-2 };
        for (int i = 0; i < smallValues.Length; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), smallValues[i]));
            Assert.Equal(Math.Sqrt(smallValues[i]), indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Sqrttrans_LargeValues()
    {
        var indicator = new Sqrttrans();
        var time = DateTime.UtcNow;

        // Test large values (within double range that won't overflow)
        double[] largeValues = { 1e10, 1e20, 1e30, 1e50, 1e100 };
        for (int i = 0; i < largeValues.Length; i++)
        {
            indicator.Update(new TValue(time.AddMinutes(i), largeValues[i]));
            Assert.Equal(Math.Sqrt(largeValues[i]), indicator.Last.Value, Tolerance);
        }
    }
}
