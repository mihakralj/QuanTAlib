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

        var result = Logtrans.Batch(source);

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
        Logtrans.Batch(values, output);

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
    public void Logtrans_ZeroInput_UsesLastValid()
    {
        // Zero input uses last valid value (robustness pattern)
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // First update with valid value
        indicator.Update(new TValue(time, Math.E));
        double lastValid = indicator.Last.Value;  // ln(e) = 1.0

        // Zero input - should use last valid
        indicator.Update(new TValue(time.AddMinutes(1), 0.0));

        Assert.Equal(lastValid, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Logtrans_NegativeInput_UsesLastValid()
    {
        // Negative input uses last valid value (robustness pattern)
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // First update with valid value
        indicator.Update(new TValue(time, 2.0));
        double lastValid = indicator.Last.Value;  // ln(2)

        // Negative input - should use last valid
        indicator.Update(new TValue(time.AddMinutes(1), -1.0));

        Assert.Equal(lastValid, indicator.Last.Value, Tolerance);
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

    [Fact]
    public void Logtrans_VerySmallPositive_ApproachesNegativeInfinity()
    {
        // ln(ε) → -∞ as ε → 0+
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, double.Epsilon));
        double result = indicator.Last.Value;

        Assert.True(double.IsFinite(result));
        Assert.True(result < -700); // ln(double.Epsilon) ≈ -744
    }

    [Fact]
    public void Logtrans_VeryLargeValue_Handles()
    {
        // ln(large) should be finite
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 1e300));
        double result = indicator.Last.Value;

        Assert.True(double.IsFinite(result));
        Assert.Equal(Math.Log(1e300), result, Tolerance);
    }

    [Fact]
    public void Logtrans_Span_ZeroInput_UsesLastValid()
    {
        // Span API: zero input uses last valid value (robustness pattern)
        var values = new double[] { 2.0, 0.0, 3.0 };
        var output = new double[3];

        Logtrans.Batch(values, output);

        Assert.Equal(Math.Log(2.0), output[0], Tolerance);  // ln(2)
        Assert.Equal(Math.Log(2.0), output[1], Tolerance);  // zero -> uses last valid (ln(2))
        Assert.Equal(Math.Log(3.0), output[2], Tolerance);  // ln(3)
    }

    [Fact]
    public void Logtrans_Span_NegativeInput_UsesLastValid()
    {
        // Span API: negative input uses last valid value (robustness pattern)
        var values = new double[] { 2.0, -5.0, 3.0 };
        var output = new double[3];

        Logtrans.Batch(values, output);

        Assert.Equal(Math.Log(2.0), output[0], Tolerance);  // ln(2)
        Assert.Equal(Math.Log(2.0), output[1], Tolerance);  // negative -> uses last valid (ln(2))
        Assert.Equal(Math.Log(3.0), output[2], Tolerance);  // ln(3)
    }

    [Fact]
    public void Logtrans_NaNInput_UsesLastValid()
    {
        // NaN input uses last valid value (robustness pattern)
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // First update with valid value
        indicator.Update(new TValue(time, Math.E));
        double lastValid = indicator.Last.Value;  // ln(e) = 1.0

        // NaN input - should use last valid
        indicator.Update(new TValue(time.AddMinutes(1), double.NaN));

        Assert.Equal(lastValid, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Logtrans_PositiveInfinityInput_UsesLastValid()
    {
        // Positive infinity input uses last valid value (robustness pattern)
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // First update with valid value
        indicator.Update(new TValue(time, 10.0));
        double lastValid = indicator.Last.Value;  // ln(10)

        // Positive infinity input - should use last valid
        indicator.Update(new TValue(time.AddMinutes(1), double.PositiveInfinity));

        Assert.Equal(lastValid, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Logtrans_NegativeInfinityInput_UsesLastValid()
    {
        // Negative infinity input uses last valid value (robustness pattern)
        var indicator = new Logtrans();
        var time = DateTime.UtcNow;

        // First update with valid value
        indicator.Update(new TValue(time, 5.0));
        double lastValid = indicator.Last.Value;  // ln(5)

        // Negative infinity input - should use last valid
        indicator.Update(new TValue(time.AddMinutes(1), double.NegativeInfinity));

        Assert.Equal(lastValid, indicator.Last.Value, Tolerance);
    }
}
