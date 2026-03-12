using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for IQR — self-consistency and mathematical properties.
/// No external library implements rolling IQR with linear interpolation,
/// so validation is based on known mathematical properties.
/// </summary>
public class IqrValidationTests
{
    [Fact]
    public void ConstantSeries_IqrIsZero()
    {
        var iqr = new Iqr(20);
        for (int i = 0; i < 50; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        Assert.Equal(0.0, iqr.Last.Value, 10);
    }

    [Fact]
    public void LinearSequence_KnownIqr()
    {
        // Window of {1,2,3,...,20} → sorted [1..20]
        // Q1: rank = 0.25*19 = 4.75 → 5 + 0.75*(6-5) = 5.75
        // Q3: rank = 0.75*19 = 14.25 → 15 + 0.25*(16-15) = 15.25
        // IQR = 15.25 - 5.75 = 9.5
        var iqr = new Iqr(20);
        for (int i = 1; i <= 20; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(9.5, iqr.Last.Value, 10);
    }

    [Fact]
    public void SymmetricDistribution_IqrSymmetric()
    {
        // Values: {-5,-4,-3,-2,-1,0,1,2,3,4,5} → sorted [-5..5], n=11
        // Q1: rank = 0.25*10 = 2.5 → -3 + 0.5*(-2-(-3)) = -2.5
        // Q3: rank = 0.75*10 = 7.5 → 3 + 0.5*(4-3) = 2.5 (wait, index 7=2, index 8=3)
        // Actually: sorted = [-5,-4,-3,-2,-1,0,1,2,3,4,5]
        // index:      0   1   2   3   4  5  6  7  8  9  10
        // Q1: rank=2.5 → sorted[2] + 0.5*(sorted[3]-sorted[2]) = -3 + 0.5*1 = -2.5
        // Q3: rank=7.5 → sorted[7] + 0.5*(sorted[8]-sorted[7]) = 2 + 0.5*1 = 2.5
        // IQR = 2.5 - (-2.5) = 5.0
        var iqr = new Iqr(11);
        for (int i = -5; i <= 5; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(5.0, iqr.Last.Value, 10);
    }

    [Fact]
    public void Deterministic_SameInputSameOutput()
    {
        int period = 10;
        var iqr1 = new Iqr(period);
        var iqr2 = new Iqr(period);

        var rng1 = new GBM(seed: 42);
        var rng2 = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar1 = rng1.Next();
            var bar2 = rng2.Next();
            iqr1.Update(new TValue(bar1.Time, bar1.Close));
            iqr2.Update(new TValue(bar2.Time, bar2.Close));
        }

        Assert.Equal(iqr1.Last.Value, iqr2.Last.Value, 1e-10);
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

        // Streaming
        var streaming = new Iqr(period);
        double lastStreaming = 0;
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            lastStreaming = streaming.Last.Value;
        }

        // Batch
        var batchSeries = Iqr.Batch(source, period);
        Assert.Equal(lastStreaming, batchSeries[bars - 1].Value, 1e-10);
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

        // Streaming
        var streaming = new Iqr(period);
        var streamResults = new double[bars];
        for (int i = 0; i < bars; i++)
        {
            streaming.Update(source[i]);
            streamResults[i] = streaming.Last.Value;
        }

        // Span
        var spanOutput = new double[bars];
        Iqr.Batch(source.Values, spanOutput.AsSpan(), period);

        for (int i = period - 1; i < bars; i++)
        {
            Assert.Equal(streamResults[i], spanOutput[i], 1e-10);
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

        var (results, indicator) = Iqr.Calculate(source, period);
        Assert.Equal(50, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void IqrNonNegative_ForAllInputs()
    {
        var iqr = new Iqr(20);
        var rng = new GBM();
        for (int i = 0; i < 200; i++)
        {
            var bar = rng.Next();
            iqr.Update(new TValue(bar.Time, bar.Close));
            Assert.True(iqr.Last.Value >= 0.0, $"IQR negative at bar {i}");
        }
    }

    [Fact]
    public void OutlierResistance_IqrLessThanRange()
    {
        // IQR should always be <= full range for any window
        var iqr = new Iqr(10);
        var values = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 100 };
        double min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i < values.Length; i++)
        {
            iqr.Update(new TValue(DateTime.UtcNow, values[i]));
            if (values[i] < min) { min = values[i]; }
            if (values[i] > max) { max = values[i]; }
        }
        double range = max - min;
        Assert.True(iqr.Last.Value <= range, $"IQR ({iqr.Last.Value}) > range ({range})");
    }
}
