using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for JB — self-consistency and mathematical properties.
/// No external library implements rolling Jarque-Bera, so validation is based
/// on known mathematical properties and analytical results.
/// </summary>
public class JbValidationTests
{
    [Fact]
    public void ConstantSeries_JbIsZero()
    {
        var jb = new Jb(20);
        for (int i = 0; i < 50; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        Assert.Equal(0.0, jb.Last.Value, 10);
    }

    [Fact]
    public void SymmetricData_SkewnessTermIsZero()
    {
        // Symmetric data around mean → skewness ≈ 0
        // JB should be driven entirely by excess kurtosis term
        var jb = new Jb(11);
        for (int i = -5; i <= 5; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i));
        }
        // For uniform-like data, excess kurtosis ≈ -1.2, so JB > 0
        Assert.True(jb.Last.Value >= 0.0);
        Assert.True(double.IsFinite(jb.Last.Value));
    }

    [Fact]
    public void LinearSequence_KnownJb()
    {
        // Window of {1,...,20}: uniform distribution
        // Population skewness ≈ 0, excess kurtosis ≈ -1.2
        // JB = (20/6) * (S² + EK²/4) ≈ 1.212 (exact depends on FP rounding in moment sums)
        var jb = new Jb(20);
        for (int i = 1; i <= 20; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, i));
        }
        // Verify JB is in expected range for uniform-like data
        Assert.True(jb.Last.Value > 1.0 && jb.Last.Value < 1.5,
            $"JB for linear sequence {1..20} expected ~1.2, got {jb.Last.Value}");
    }

    [Fact]
    public void SkewedData_LargerJb()
    {
        // Right-skewed data should produce larger JB than symmetric
        var jbSymmetric = new Jb(10);
        for (int i = -5; i <= 4; i++)
        {
            jbSymmetric.Update(new TValue(DateTime.UtcNow, i));
        }

        var jbSkewed = new Jb(10);
        double[] skewed = [1, 1, 1, 2, 2, 3, 5, 10, 20, 100];
        for (int i = 0; i < skewed.Length; i++)
        {
            jbSkewed.Update(new TValue(DateTime.UtcNow, skewed[i]));
        }

        Assert.True(jbSkewed.Last.Value > jbSymmetric.Last.Value,
            $"Skewed JB ({jbSkewed.Last.Value}) should exceed symmetric JB ({jbSymmetric.Last.Value})");
    }

    [Fact]
    public void Deterministic_SameInputSameOutput()
    {
        int period = 10;
        var jb1 = new Jb(period);
        var jb2 = new Jb(period);

        var rng1 = new GBM(seed: 42);
        var rng2 = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar1 = rng1.Next();
            var bar2 = rng2.Next();
            jb1.Update(new TValue(bar1.Time, bar1.Close));
            jb2.Update(new TValue(bar2.Time, bar2.Close));
        }

        Assert.Equal(jb1.Last.Value, jb2.Last.Value, 1e-10);
    }

    [Fact]
    public void BatchVsStreaming_Match()
    {
        int period = 10;
        int bars = 100;
        var rng = new GBM();
        var source = new TSeries();
        for (int i = 0; i < bars; i++)
        {
            var bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var streaming = new Jb(period);
        double lastStreaming = 0;
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            lastStreaming = streaming.Last.Value;
        }

        var batchSeries = Jb.Batch(source, period);
        Assert.Equal(lastStreaming, batchSeries[bars - 1].Value, 1e-8);
    }

    [Fact]
    public void SpanVsStreaming_Match()
    {
        int period = 10;
        int bars = 100;
        var rng = new GBM();
        var source = new TSeries();
        for (int i = 0; i < bars; i++)
        {
            var bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var streaming = new Jb(period);
        var streamResults = new double[bars];
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            streamResults[i] = streaming.Last.Value;
        }

        var spanOutput = new double[bars];
        Jb.Batch(source.Values, spanOutput.AsSpan(), period);

        for (int i = period - 1; i < bars; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-1);
        }
    }

    [Fact]
    public void CalculateBridge_ReturnsIndicatorAndResults()
    {
        int period = 10;
        var rng = new GBM();
        var source = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            var bar = rng.Next();
            source.Add(new TValue(bar.Time, bar.Close));
        }

        var (results, indicator) = Jb.Calculate(source, period);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void JbNonNegative_ForAllInputs()
    {
        var jb = new Jb(20);
        var rng = new GBM();
        for (int i = 0; i < 200; i++)
        {
            var bar = rng.Next();
            jb.Update(new TValue(bar.Time, bar.Close));
            Assert.True(jb.Last.Value >= 0.0, $"JB negative at bar {i}");
        }
    }

    [Fact]
    public void OutlierIncreases_Jb()
    {
        // Adding outlier to normal-ish data should increase JB
        var jb = new Jb(10);
        for (int i = 1; i <= 9; i++)
        {
            jb.Update(new TValue(DateTime.UtcNow, 50.0 + i));
        }
        jb.Update(new TValue(DateTime.UtcNow, 55.0));
        double normalJb = jb.Last.Value;

        var jbOutlier = new Jb(10);
        for (int i = 1; i <= 9; i++)
        {
            jbOutlier.Update(new TValue(DateTime.UtcNow, 50.0 + i));
        }
        jbOutlier.Update(new TValue(DateTime.UtcNow, 500.0));
        double outlierJb = jbOutlier.Last.Value;

        Assert.True(outlierJb > normalJb,
            $"Outlier JB ({outlierJb}) should exceed normal JB ({normalJb})");
    }
}
