using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class SmiValidationTests
{
    private static TBarSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: seed);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // --- A) Streaming vs Batch agreement ---

    [Fact]
    public void Streaming_Matches_Batch_Blau()
    {
        var series = GenerateSeries(300);
        const int kPeriod = 10;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var smi = new Smi(kPeriod, kSmooth, dSmooth, blau: true);
        for (int i = 0; i < series.Count; i++)
        {
            smi.Update(series[i]);
        }

        var (batchK, batchD) = Smi.Batch(series, kPeriod, kSmooth, dSmooth, blau: true);

        Assert.Equal(smi.K.Value, batchK[^1].Value, 1e-6);
        Assert.Equal(smi.D.Value, batchD[^1].Value, 1e-6);
    }

    [Fact]
    public void Streaming_Matches_Batch_ChandeKroll()
    {
        var series = GenerateSeries(300);
        const int kPeriod = 10;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var smi = new Smi(kPeriod, kSmooth, dSmooth, blau: false);
        for (int i = 0; i < series.Count; i++)
        {
            smi.Update(series[i]);
        }

        var (batchK, batchD) = Smi.Batch(series, kPeriod, kSmooth, dSmooth, blau: false);

        Assert.Equal(smi.K.Value, batchK[^1].Value, 1e-6);
        Assert.Equal(smi.D.Value, batchD[^1].Value, 1e-6);
    }

    // --- B) SpanBatch vs TBarSeriesBatch ---

    [Fact]
    public void SpanBatch_Matches_TBarSeriesBatch()
    {
        var series = GenerateSeries(200);
        const int kPeriod = 10;
        const int kSmooth = 3;
        const int dSmooth = 3;

        var (batchK, batchD) = Smi.Batch(series, kPeriod, kSmooth, dSmooth);

        var spanK = new double[series.Count];
        var spanD = new double[series.Count];
        Smi.Batch(series.High.Values, series.Low.Values, series.Close.Values,
            spanK, spanD, kPeriod, kSmooth, dSmooth);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchK[i].Value, spanK[i], 1e-10);
            Assert.Equal(batchD[i].Value, spanD[i], 1e-10);
        }
    }

    // --- C) Directional correctness ---

    [Fact]
    public void ConstantPrice_KIsZero()
    {
        var bars = new TBarSeries();
        long t = DateTime.UtcNow.Ticks;

        for (int i = 0; i < 100; i++)
        {
            bars.Add(new TBar(t + i, 50.0, 50.0, 50.0, 50.0, 1000));
        }

        var (k, d) = Smi.Batch(bars, 10, 3, 3);
        Assert.Equal(0.0, k[^1].Value, 1e-6);
        Assert.Equal(0.0, d[^1].Value, 1e-6);
    }

    [Fact]
    public void PriceAboveMidpoint_PositiveK()
    {
        // Close consistently near high → positive SMI
        var bars = new TBarSeries();
        long t = DateTime.UtcNow.Ticks;
        for (int i = 0; i < 50; i++)
        {
            bars.Add(new TBar(t + i, 100, 110, 90, 109, 1000));
        }

        var (k, _) = Smi.Batch(bars, 10, 3, 3);
        Assert.True(k[^1].Value > 0.0, "Close near high should produce positive K");
    }

    [Fact]
    public void PriceBelowMidpoint_NegativeK()
    {
        // Close consistently near low → negative SMI
        var bars = new TBarSeries();
        long t = DateTime.UtcNow.Ticks;
        for (int i = 0; i < 50; i++)
        {
            bars.Add(new TBar(t + i, 100, 110, 90, 91, 1000));
        }

        var (k, _) = Smi.Batch(bars, 10, 3, 3);
        Assert.True(k[^1].Value < 0.0, "Close near low should produce negative K");
    }

    // --- D) Multi-period consistency ---

    [Fact]
    public void DifferentPeriods_AllProduceFiniteResults()
    {
        var series = GenerateSeries(200);

        int[] periods = [5, 10, 14, 20];
        foreach (int p in periods)
        {
            var (k, d) = Smi.Batch(series, kPeriod: p, kSmooth: 3, dSmooth: 3);
            Assert.Equal(200, k.Count);
            Assert.Equal(200, d.Count);
            Assert.True(double.IsFinite(k[^1].Value), $"K should be finite for kPeriod={p}");
            Assert.True(double.IsFinite(d[^1].Value), $"D should be finite for kPeriod={p}");
        }
    }

    // --- E) Determinism ---

    [Fact]
    public void MultipleRuns_ProduceIdenticalResults()
    {
        var series = GenerateSeries(100, seed: 55);

        var (k1, d1) = Smi.Batch(series, 10, 3, 3);
        var (k2, d2) = Smi.Batch(series, 10, 3, 3);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(k1[i].Value, k2[i].Value, 1e-15);
            Assert.Equal(d1[i].Value, d2[i].Value, 1e-15);
        }
    }

    [Fact]
    public void Smi_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateStochasticMomentumIndex();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}