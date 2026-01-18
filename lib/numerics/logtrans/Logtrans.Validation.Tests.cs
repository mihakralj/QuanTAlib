using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// LOGTRANS validation tests - validates against Math.Log (standard library)
/// No external TA libraries implement LOG directly, so we validate against .NET Math.
/// </summary>
public class LogtransValidationTests
{
    private const double Tolerance = 1e-14;  // Very tight - should match exactly

    [Fact]
    public void Logtrans_Batch_MatchesMathLog()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 30000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var result = Logtrans.Calculate(source);

        for (int i = 0; i < source.Count; i++)
        {
            double expected = Math.Log(source[i].Value);
            Assert.Equal(expected, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Logtrans_Streaming_MatchesMathLog()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 30001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var indicator = new Logtrans();

        for (int i = 0; i < source.Count; i++)
        {
            indicator.Update(source[i]);
            double expected = Math.Log(source[i].Value);
            Assert.Equal(expected, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Logtrans_Span_MatchesMathLog()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 30002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var values = source.Values.ToArray();
        var output = new double[count];
        Logtrans.Calculate(values, output);

        for (int i = 0; i < count; i++)
        {
            double expected = Math.Log(values[i]);
            Assert.Equal(expected, output[i], Tolerance);
        }
    }

    [Fact]
    public void Logtrans_KnownIdentities()
    {
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // ln(1) = 0
        indicator.Update(new TValue(time, 1.0));
        Assert.Equal(0.0, indicator.Last.Value, Tolerance);

        // ln(e) = 1
        indicator.Update(new TValue(time.AddMinutes(1), Math.E));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);

        // ln(e^n) = n
        for (int n = 2; n <= 5; n++)
        {
            indicator.Update(new TValue(time.AddMinutes(n), Math.Pow(Math.E, n)));
            Assert.Equal(n, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Logtrans_ProductRule()
    {
        // ln(a*b) = ln(a) + ln(b)
        double a = 2.5;
        double b = 3.7;

        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double lnA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double lnB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a * b));
        double lnAB = indicator.Last.Value;

        Assert.Equal(lnA + lnB, lnAB, Tolerance);
    }

    [Fact]
    public void Logtrans_QuotientRule()
    {
        // ln(a/b) = ln(a) - ln(b)
        double a = 10.0;
        double b = 2.5;

        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double lnA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double lnB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a / b));
        double lnADivB = indicator.Last.Value;

        Assert.Equal(lnA - lnB, lnADivB, Tolerance);
    }

    [Fact]
    public void Logtrans_PowerRule()
    {
        // ln(a^n) = n * ln(a)
        double a = 3.0;
        int n = 4;

        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double lnA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, Math.Pow(a, n)));
        double lnAPowN = indicator.Last.Value;

        Assert.Equal(n * lnA, lnAPowN, Tolerance);
    }
}