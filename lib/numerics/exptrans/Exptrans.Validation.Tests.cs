using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// EXPTRANS validation tests - validates against Math.Exp (standard library)
/// </summary>
public class ExptransValidationTests
{
    private const double Tolerance = 1e-14;

    [Fact]
    public void Exptrans_Batch_MatchesMathExp()
    {
        int count = 100;
        // Use log-transformed prices to keep exp in reasonable range
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 50000);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var logSource = Logtrans.Batch(bars.Close);

        var result = Exptrans.Batch(logSource);

        for (int i = 0; i < logSource.Count; i++)
        {
            double expected = Math.Exp(logSource[i].Value);
            Assert.Equal(expected, result[i].Value, Tolerance);
        }
    }

    [Fact]
    public void Exptrans_Streaming_MatchesMathExp()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 50001);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var logSource = Logtrans.Batch(bars.Close);

        var indicator = new Exptrans();

        for (int i = 0; i < logSource.Count; i++)
        {
            indicator.Update(logSource[i]);
            double expected = Math.Exp(logSource[i].Value);
            Assert.Equal(expected, indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Exptrans_Span_MatchesMathExp()
    {
        int count = 100;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 50002);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var logSource = Logtrans.Batch(bars.Close);

        var values = logSource.Values.ToArray();
        var output = new double[count];
        Exptrans.Batch(values, output);

        for (int i = 0; i < count; i++)
        {
            double expected = Math.Exp(values[i]);
            Assert.Equal(expected, output[i], Tolerance);
        }
    }

    [Fact]
    public void Exptrans_KnownIdentities()
    {
        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        // exp(0) = 1
        indicator.Update(new TValue(time, 0.0));
        Assert.Equal(1.0, indicator.Last.Value, Tolerance);

        // exp(1) = e
        indicator.Update(new TValue(time.AddMinutes(1), 1.0));
        Assert.Equal(Math.E, indicator.Last.Value, Tolerance);

        // exp(n) = e^n
        for (int n = 2; n <= 5; n++)
        {
            indicator.Update(new TValue(time.AddMinutes(n), n));
            Assert.Equal(Math.Exp(n), indicator.Last.Value, Tolerance);
        }
    }

    [Fact]
    public void Exptrans_InverseOfLog()
    {
        // exp(ln(x)) = x for all x > 0
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 50003);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var source = bars.Close;

        var logResult = Logtrans.Batch(source);
        var expResult = Exptrans.Batch(logResult);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(source[i].Value, expResult[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Exptrans_ProductRule()
    {
        // exp(a + b) = exp(a) * exp(b)
        double a = 1.5;
        double b = 2.3;

        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double expA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double expB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a + b));
        double expAB = indicator.Last.Value;

        Assert.Equal(expA * expB, expAB, Tolerance);
    }

    [Fact]
    public void Exptrans_QuotientRule()
    {
        // exp(a - b) = exp(a) / exp(b)
        double a = 3.0;
        double b = 1.5;

        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double expA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, b));
        double expB = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, a - b));
        double expAMinusB = indicator.Last.Value;

        Assert.Equal(expA / expB, expAMinusB, Tolerance);
    }

    [Fact]
    public void Exptrans_PowerRule()
    {
        // exp(n * a) = exp(a)^n
        double a = 1.2;
        int n = 3;

        var indicator = new Exptrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, a));
        double expA = indicator.Last.Value;

        indicator.Reset();
        indicator.Update(new TValue(time, n * a));
        double expNA = indicator.Last.Value;

        Assert.Equal(Math.Pow(expA, n), expNA, 1e-12);
    }
}
