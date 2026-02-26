using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation: batch == streaming, span == TSeries batch.
/// </summary>
public sealed class CrsiValidationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Streaming_MatchesBatch_DefaultParams()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 1001);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Crsi(3, 2, 100);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        // Batch TSeries
        TSeries batchTs = Crsi.Batch(source, 3, 2, 100);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batchTs.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Span_MatchesBatch_DefaultParams()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 1002);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Batch TSeries
        TSeries batchTs = Crsi.Batch(source, 3, 2, 100);

        // Batch Span
        var spanOut = new double[source.Count];
        Crsi.Batch(source.Values, spanOut, 3, 2, 100);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOut[i], Tolerance);
        }
    }

    [Fact]
    public void Eventing_MatchesStreaming()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 1003);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var streaming = new Crsi(3, 2, 50);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = streaming.Update(source[i]).Value;
        }

        // Event-based
        var eventTs = new TSeries();
        var eventCrsi = new Crsi(eventTs, 3, 2, 50);
        var eventVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventTs.Add(source[i]);
            eventVals[i] = eventCrsi.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], eventVals[i], Tolerance);
        }
    }

    [Fact]
    public void Output_AlwaysInRange0To100()
    {
        var gbm = new GBM(startPrice: 50.0, mu: 0.05, sigma: 0.5, seed: 1004);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var crsi = new Crsi(3, 2, 100);
        for (int i = 0; i < source.Count; i++)
        {
            double v = crsi.Update(source[i]).Value;
            Assert.True(v >= 0.0 && v <= 100.0, $"CRSI={v} at i={i}");
        }
    }

    [Fact]
    public void Reset_ThenReplay_MatchesFreshRun()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 1005);
        var bars = gbm.Fetch(150, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var crsi1 = new Crsi(3, 2, 30);
        for (int i = 0; i < source.Count; i++)
        {
            crsi1.Update(source[i]);
        }

        double finalVal1 = crsi1.Last.Value;

        // Reset and replay
        crsi1.Reset();
        for (int i = 0; i < source.Count; i++)
        {
            crsi1.Update(source[i]);
        }

        Assert.Equal(finalVal1, crsi1.Last.Value, Tolerance);
    }

    [Fact]
    public void DifferentPeriods_ProduceDistinctResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.01, sigma: 0.2, seed: 1006);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        TSeries r1 = Crsi.Batch(source, 3, 2, 50);
        TSeries r2 = Crsi.Batch(source, 5, 3, 50);

        // With different RSI/streak parameters and same data, results should differ
        bool anyDiff = false;
        for (int i = 0; i < source.Count; i++)
        {
            if (Math.Abs(r1.Values[i] - r2.Values[i]) > 1e-6)
            {
                anyDiff = true;
                break;
            }
        }

        Assert.True(anyDiff, "Different periods should produce different results");
    }
}
