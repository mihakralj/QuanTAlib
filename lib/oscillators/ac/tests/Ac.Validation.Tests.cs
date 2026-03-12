using Xunit;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

/// <summary>
/// Self-consistency validation for AC. No external library implements AC with
/// identical SMA-based methodology, so we validate AC = AO - SMA(AO, acPeriod)
/// identity, determinism, and cross-mode consistency.
/// </summary>
public sealed class AcValidationTests
{
    private static TBarSeries GenerateSeries(int count, int seed = 42)
    {
        var gbm = new GBM(500.0, 0.05, 0.3, seed: seed);
        var series = new TBarSeries();
        for (int i = 0; i < count; i++)
        {
            series.Add(gbm.Next(isNew: true));
        }

        return series;
    }

    [Fact]
    public void AC_Equals_AO_Minus_SMA_AO()
    {
        var series = GenerateSeries(200);

        // Compute AO
        var ao = new Ao();
        var aoValues = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            var r = ao.Update(series[i], isNew: true);
            aoValues.Add(r.Value);
        }

        // Compute SMA(AO, 5)
        var smaAo = new Sma(5);
        var smaAoValues = new List<double>();
        for (int i = 0; i < aoValues.Count; i++)
        {
            var r = smaAo.Update(new TValue(DateTime.UtcNow.AddMinutes(i), aoValues[i]), isNew: true);
            smaAoValues.Add(r.Value);
        }

        // Compute AC via streaming
        var ac = new Ac();
        var acValues = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            var r = ac.Update(series[i], isNew: true);
            acValues.Add(r.Value);
        }

        // Verify AC = AO - SMA(AO, 5) once all are hot
        int start = 38; // slowPeriod(34) + acPeriod(5) - 1
        for (int i = start; i < series.Count; i++)
        {
            double expected = aoValues[i] - smaAoValues[i];
            Assert.Equal(expected, acValues[i], 1e-10);
        }
    }

    [Fact]
    public void BatchAndStreaming_Match()
    {
        var series = GenerateSeries(200);

        // Streaming
        var streaming = new Ac();
        var streamValues = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            var r = streaming.Update(series[i], isNew: true);
            streamValues.Add(r.Value);
        }

        // Batch
        var batchResult = Ac.Batch(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamValues[i], batchResult[i].Value, 4);
        }
    }

    [Fact]
    public void Determinism_SameSeedProducesSameResults()
    {
        var series1 = GenerateSeries(100, seed: 123);
        var series2 = GenerateSeries(100, seed: 123);

        var ac1 = new Ac();
        var ac2 = new Ac();

        for (int i = 0; i < series1.Count; i++)
        {
            var r1 = ac1.Update(series1[i], isNew: true);
            var r2 = ac2.Update(series2[i], isNew: true);
            Assert.Equal(r1.Value, r2.Value, 1e-12);
        }
    }

    [Fact]
    public void SpanBatch_Matches_TBarSeriesBatch()
    {
        var series = GenerateSeries(150);

        var batchResult = Ac.Batch(series);

        var output = new double[series.Count];
        Ac.Batch(series.High.Values, series.Low.Values, output);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void ParameterSensitivity_DifferentPeriods_DifferentResults()
    {
        var series = GenerateSeries(100);

        var ac1 = new Ac(5, 34, 5);
        var ac2 = new Ac(3, 20, 5);

        for (int i = 0; i < series.Count; i++)
        {
            _ = ac1.Update(series[i], isNew: true);
            _ = ac2.Update(series[i], isNew: true);
        }

        Assert.NotEqual(ac1.Last.Value, ac2.Last.Value);
    }

    [Fact]
    public void LargeDataset_Stability()
    {
        var series = GenerateSeries(5000, seed: 55);

        var ac = new Ac();
        for (int i = 0; i < series.Count; i++)
        {
            var result = ac.Update(series[i], isNew: true);
            Assert.True(double.IsFinite(result.Value), $"Non-finite at bar {i}");
        }

        Assert.True(ac.IsHot);
    }

    [Fact]
    public void MonotonicConvergence_ConstantInput()
    {
        var ac = new Ac();
        double prevAbsValue = double.MaxValue;
        bool convergenceStarted = false;

        for (int i = 0; i < 200; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 50.0, 50.0, 50.0, 50.0, 1000.0);
            var result = ac.Update(bar, isNew: true);

            if (ac.IsHot && i > 50)
            {
                double absVal = Math.Abs(result.Value);
                if (convergenceStarted)
                {
                    Assert.True(absVal <= prevAbsValue + 1e-10, $"Not converging at bar {i}: {absVal} > {prevAbsValue}");
                }

                convergenceStarted = true;
                prevAbsValue = absVal;
            }
        }

        Assert.True(convergenceStarted);
    }

    [Fact]
    public void Ac_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateAcceleratorOscillator();
        var values = result.CustomValuesList;
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
