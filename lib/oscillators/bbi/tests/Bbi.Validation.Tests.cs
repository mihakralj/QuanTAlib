using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation: batch TSeries == streaming == span == eventing.
/// No external library implements BBI, so cross-library comparison is N/A.
/// </summary>
public sealed class BbiValidationTests
{
    private const double Tolerance = 1e-10;

    private static TSeries BuildGbmSeries(int count, int seed = 1)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        return bars.Close;
    }

    // ── Batch == Streaming ───────────────────────────────────────────────────

    [Fact]
    public void Batch_EqualsStreaming_DefaultPeriods()
    {
        TSeries source = BuildGbmSeries(500, seed: 1);

        // Streaming
        var bbi = new Bbi();
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = bbi.Update(source[i]).Value;
        }

        // Batch TSeries
        TSeries batch = Bbi.Batch(source);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batch.Values[i], Tolerance);
        }
    }

    [Fact]
    public void Batch_EqualsStreaming_CustomPeriods()
    {
        TSeries source = BuildGbmSeries(300, seed: 2);
        int p1 = 5, p2 = 10, p3 = 20, p4 = 40;

        var bbi = new Bbi(p1, p2, p3, p4);
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = bbi.Update(source[i]).Value;
        }

        TSeries batch = Bbi.Batch(source, p1, p2, p3, p4);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], batch.Values[i], Tolerance);
        }
    }

    // ── Batch(Span) == Batch(TSeries) ────────────────────────────────────────

    [Fact]
    public void BatchSpan_EqualsBatchTSeries_DefaultPeriods()
    {
        TSeries source = BuildGbmSeries(400, seed: 3);

        TSeries batchTs = Bbi.Batch(source);
        var spanOut = new double[source.Count];
        Bbi.Batch(source.Values, spanOut);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOut[i], Tolerance);
        }
    }

    [Fact]
    public void BatchSpan_EqualsBatchTSeries_CustomPeriods()
    {
        TSeries source = BuildGbmSeries(200, seed: 4);
        int p1 = 4, p2 = 8, p3 = 16, p4 = 32;

        TSeries batchTs = Bbi.Batch(source, p1, p2, p3, p4);
        var spanOut = new double[source.Count];
        Bbi.Batch(source.Values, spanOut, p1, p2, p3, p4);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batchTs.Values[i], spanOut[i], Tolerance);
        }
    }

    // ── Eventing == Streaming ────────────────────────────────────────────────

    [Fact]
    public void Eventing_EqualsStreaming_DefaultPeriods()
    {
        TSeries source = BuildGbmSeries(300, seed: 5);

        // Streaming
        var bbi = new Bbi();
        var streamVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamVals[i] = bbi.Update(source[i]).Value;
        }

        // Eventing
        var eventSource = new TSeries();
        var eventBbi = new Bbi(eventSource);
        var eventVals = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            eventSource.Add(source[i]);
            eventVals[i] = eventBbi.Last.Value;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamVals[i], eventVals[i], Tolerance);
        }
    }

    // ── Mathematical properties ──────────────────────────────────────────────

    [Fact]
    public void ConstantInput_BbiEqualsConstant()
    {
        // Constant price → all SMAs == price → BBI == price
        const double price = 75.0;
        var bbi = new Bbi();
        for (int i = 0; i < 100; i++)
        {
            bbi.Update(new TValue(DateTime.UtcNow, price));
        }
        Assert.Equal(price, bbi.Last.Value, Tolerance);
    }

    [Fact]
    public void ConstantInput_BatchBbiEqualsConstant()
    {
        const double price = 125.0;
        int n = 100;
        var source = new double[n];
        var output = new double[n];
        for (int i = 0; i < n; i++) { source[i] = price; }
        Bbi.Batch(source.AsSpan(), output.AsSpan());

        // After full warmup (bar 24+), every output should equal price
        for (int i = 24; i < n; i++)
        {
            Assert.Equal(price, output[i], Tolerance);
        }
    }

    [Fact]
    public void AllPeriodsOne_BbiEqualsInput()
    {
        // With all periods=1, each SMA is just the current value → BBI == current value
        var bbi = new Bbi(p1: 1, p2: 1, p3: 1, p4: 1);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 6);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        for (int i = 0; i < source.Count; i++)
        {
            var result = bbi.Update(source[i]);
            Assert.Equal(source.Values[i], result.Value, Tolerance);
        }
    }

    // ── UpdateTSeries primes streaming state correctly ───────────────────────

    [Fact]
    public void UpdateTSeries_ContinuedStreaming_Consistent()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 7);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Instance-based batch (internally resets + re-streams)
        var batchBbi = new Bbi();
        batchBbi.Update(source);

        // Pure streaming
        var streamBbi = new Bbi();
        for (int i = 0; i < source.Count; i++)
        {
            streamBbi.Update(source[i]);
        }

        // Both should have identical Last values after processing same data
        Assert.Equal(streamBbi.Last.Value, batchBbi.Last.Value, Tolerance);
    }

    // ── Calculate bridge ─────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ResultsMatchBatch()
    {
        TSeries source = BuildGbmSeries(200, seed: 8);

        var (results, indicator) = Bbi.Calculate(source);
        TSeries batch = Bbi.Batch(source);

        Assert.Equal(batch.Count, results.Count);
        for (int i = 0; i < batch.Count; i++)
        {
            Assert.Equal(batch.Values[i], results.Values[i], Tolerance);
        }
        Assert.True(indicator.IsHot);
    }
}
