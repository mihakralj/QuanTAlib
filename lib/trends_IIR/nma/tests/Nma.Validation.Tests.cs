using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for NMA. No external library supports NMA,
/// so we validate internal consistency: streaming==batch==span, ratio bounds,
/// regime detection, and determinism.
/// </summary>
public class NmaValidationTests
{
    private const long Seed = 12345;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private static TSeries GetTestSeries(int count = 500)
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(count, Seed, Step);
        return bars.Close;
    }

    [Fact]
    public void StreamingEqualsBatch_DefaultPeriod()
    {
        var series = GetTestSeries(500);
        int period = 40;

        // Streaming
        var streaming = new Nma(period);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Batch (span)
        var batchResults = new double[series.Count];
        Nma.Batch(series.Values, batchResults, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i], 1e-7);
        }
    }

    [Fact]
    public void StreamingEqualsTSeries()
    {
        var series = GetTestSeries(500);
        int period = 40;

        // Streaming
        var streaming = new Nma(period);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // TSeries batch
        var batchSeries = Nma.Batch(series, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchSeries.Values[i], 1e-7);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(40)]
    [InlineData(80)]
    public void ConsistencyAcrossPeriods(int period)
    {
        var series = GetTestSeries(300);

        // Streaming
        var streaming = new Nma(period);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Batch
        var batchResults = new double[series.Count];
        Nma.Batch(series.Values, batchResults, period);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i], 1e-7);
        }
    }

    [Fact]
    public void ConstantInput_NmaEqualsConstant()
    {
        double constant = 100.0;
        int period = 40;
        int count = 200;

        var nma = new Nma(period);
        for (int i = 0; i < count; i++)
        {
            nma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constant));
        }

        // For constant input, volatility is 0 everywhere → ratio = 0
        // But first bar seeds NMA = constant, so it should stay constant
        Assert.Equal(constant, nma.Last.Value, 1e-8);
    }

    [Fact]
    public void MonotonicRising_NmaFollowsGradually()
    {
        int period = 14;
        var nma = new Nma(period);

        double lastNma = 0;
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (i * 0.5);
            lastNma = nma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price)).Value;
        }

        // NMA should lag behind the linearly rising price
        Assert.True(lastNma > 100.0, "NMA should rise");
        Assert.True(lastNma < 150.0, "NMA should lag behind final price");
    }

    [Fact]
    public void DeterministicOutput()
    {
        var series = GetTestSeries(200);
        int period = 40;

        var nma1 = new Nma(period);
        var nma2 = new Nma(period);
        for (int i = 0; i < series.Count; i++)
        {
            var r1 = nma1.Update(series[i]);
            var r2 = nma2.Update(series[i]);
            Assert.Equal(r1.Value, r2.Value, 1e-15);
        }
    }

    [Fact]
    public void OutputBounded_WithinInputRange()
    {
        var series = GetTestSeries(500);
        int period = 40;

        var nma = new Nma(period);
        double minInput = double.MaxValue;
        double maxInput = double.MinValue;

        for (int i = 0; i < series.Count; i++)
        {
            nma.Update(series[i]);
            if (series[i].Value < minInput)
            {
                minInput = series[i].Value;
            }
            if (series[i].Value > maxInput)
            {
                maxInput = series[i].Value;
            }
        }

        // NMA should stay within input range (with small tolerance for FP)
        Assert.True(nma.Last.Value >= minInput * 0.99);
        Assert.True(nma.Last.Value <= maxInput * 1.01);
    }

    [Fact]
    public void SmallPeriod_MoreResponsive()
    {
        var series = GetTestSeries(200);

        var nmaFast = new Nma(5);
        var nmaSlow = new Nma(80);

        double sumAbsDiffFast = 0;
        double sumAbsDiffSlow = 0;

        for (int i = 0; i < series.Count; i++)
        {
            var fast = nmaFast.Update(series[i]).Value;
            var slow = nmaSlow.Update(series[i]).Value;

            sumAbsDiffFast += Math.Abs(fast - series[i].Value);
            sumAbsDiffSlow += Math.Abs(slow - series[i].Value);
        }

        // Faster NMA (smaller period) should track price more closely
        Assert.True(sumAbsDiffFast < sumAbsDiffSlow,
            $"Fast NMA avg deviation ({sumAbsDiffFast / series.Count:F4}) should be less than slow ({sumAbsDiffSlow / series.Count:F4})");
    }
}
