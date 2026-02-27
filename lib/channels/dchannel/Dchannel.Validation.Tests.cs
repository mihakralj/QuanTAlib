using Skender.Stock.Indicators;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class DchannelValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public DchannelValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose() => Dispose(true);

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    [Fact]
    public void Validate_ManualCalculation_Period3()
    {
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;
        series.Add(new TBar(t0, 0, 12, 8, 10, 100));
        series.Add(new TBar(t0.AddMinutes(1), 0, 14, 10, 12, 100));
        series.Add(new TBar(t0.AddMinutes(2), 0, 16, 12, 14, 100));

        var ind = new Dchannel(3);
        var (mid, up, lo) = ind.Update(series);

        Assert.Equal(16.0, up.Last.Value, 1e-10);
        Assert.Equal(8.0, lo.Last.Value, 1e-10);
        Assert.Equal(12.0, mid.Last.Value, 1e-10);
        Assert.True(ind.IsHot);

        _output.WriteLine("Dchannel manual period-3 calculation validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            // Batch (instance)
            var inst = new Dchannel(period);
            var (bMid, bUp, bLo) = inst.Update(_testData.Bars);

            // Static batch
            var (sMid, sUp, sLo) = Dchannel.Batch(_testData.Bars, period);

            ValidationHelper.VerifySeriesEqual(bMid, sMid);
            ValidationHelper.VerifySeriesEqual(bUp, sUp);
            ValidationHelper.VerifySeriesEqual(bLo, sLo);

            // Streaming
            var streaming = new Dchannel(period);
            var sMidStream = new TSeries();
            var sUpStream = new TSeries();
            var sLoStream = new TSeries();
            foreach (var bar in _testData.Bars)
            {
                streaming.Update(bar);
                sMidStream.Add(streaming.Last);
                sUpStream.Add(streaming.Upper);
                sLoStream.Add(streaming.Lower);
            }

            ValidationHelper.VerifySeriesEqual(sMid, sMidStream);
            ValidationHelper.VerifySeriesEqual(sUp, sUpStream);
            ValidationHelper.VerifySeriesEqual(sLo, sLoStream);

            // Span
            double[] high = _testData.HighPrices.ToArray();
            double[] low = _testData.LowPrices.ToArray();
            double[] spanMid = new double[high.Length];
            double[] spanUp = new double[high.Length];
            double[] spanLo = new double[high.Length];
            Dchannel.Batch(high.AsSpan(), low.AsSpan(),
                spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan(), period);

            for (int i = 0; i < high.Length; i++)
            {
                Assert.Equal(sMid[i].Value, spanMid[i], 9);
                Assert.Equal(sUp[i].Value, spanUp[i], 9);
                Assert.Equal(sLo[i].Value, spanLo[i], 9);
            }
        }

        _output.WriteLine("Dchannel mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        var pub = new TBarSeries();
        var evtInd = new Dchannel(pub, period);
        var evtMid = new TSeries();
        var evtUp = new TSeries();
        var evtLo = new TSeries();

        foreach (var bar in _testData.Bars)
        {
            pub.Add(bar);
            evtMid.Add(evtInd.Last);
            evtUp.Add(evtInd.Upper);
            evtLo.Add(evtInd.Lower);
        }

        var (bMid, bUp, bLo) = Dchannel.Batch(_testData.Bars, period);

        ValidationHelper.VerifySeriesEqual(bMid, evtMid);
        ValidationHelper.VerifySeriesEqual(bUp, evtUp);
        ValidationHelper.VerifySeriesEqual(bLo, evtLo);

        _output.WriteLine("Dchannel eventing mode validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        var ((mid, up, lo), ind) = Dchannel.Calculate(_testData.Bars, period);

        Assert.True(ind.IsHot);
        Assert.Equal(period, ind.WarmupPeriod);
        Assert.Equal(mid.Last.Value, ind.Last.Value, 1e-10);
        Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
        Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

        // Continue streaming
        var next = new TBar(DateTime.UtcNow, 0, 150, 50, 100, 1000);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("Dchannel Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;

        var (bMid, bUp, bLo) = Dchannel.Batch(_testData.Bars, period);

        var primed = new Dchannel(period);
        var subset = new TBarSeries();
        for (int i = 0; i < 200; i++)
        {
            subset.Add(_testData.Bars[i]);
        }

        primed.Prime(subset);

        for (int i = 200; i < _testData.Bars.Count; i++)
        {
            primed.Update(_testData.Bars[i]);
        }

        Assert.Equal(bMid.Last.Value, primed.Last.Value, 1e-9);
        Assert.Equal(bUp.Last.Value, primed.Upper.Value, 1e-9);
        Assert.Equal(bLo.Last.Value, primed.Lower.Value, 1e-9);

        _output.WriteLine("Dchannel Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, up, lo) = Dchannel.Batch(_testData.Bars, 50);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        for (int i = 50; i < mid.Count; i++)
        {
            Assert.True(up[i].Value >= lo[i].Value, $"Upper >= Lower at {i}");
        }

        _output.WriteLine("Dchannel large dataset validated");
    }

    [Fact]
    public void Validate_Skender_Batch_UpperBand()
    {
        // Convention difference: Skender Donchian uses prior N bars [i-N, i-1] (excludes current bar)
        // QuanTAlib Dchannel uses inclusive N bars [i-N+1, i] (includes current bar).
        // Therefore: QuanTAlib[i] should match Skender[i+1] for converged values.
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var (_, qUp, _) = Dchannel.Batch(_testData.Bars, period);
            var sResult = _testData.SkenderQuotes.GetDonchian(period).ToList();

            int count = Math.Min(qUp.Count, sResult.Count);
            int start = Math.Max(period + 1, count - 100);
            for (int i = start; i < count - 1; i++)
            {
                double qValue = qUp[i].Value;
                double? sValue = (double?)sResult[i + 1].UpperBand;
                if (!sValue.HasValue)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(qValue - sValue.Value) <= ValidationHelper.SkenderTolerance,
                    $"Period={period}, Mismatch at q[{i}] vs s[{i + 1}]: QuanTAlib={qValue:G17}, Skender={sValue.Value:G17}");
            }
        }
        _output.WriteLine("Dchannel upper band validated against Skender GetDonchian (offset +1)");
    }

    [Fact]
    public void Validate_Skender_Batch_LowerBand()
    {
        // Same offset convention: QuanTAlib[i] == Skender[i+1]
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var (_, _, qLo) = Dchannel.Batch(_testData.Bars, period);
            var sResult = _testData.SkenderQuotes.GetDonchian(period).ToList();

            int count = Math.Min(qLo.Count, sResult.Count);
            int start = Math.Max(period + 1, count - 100);
            for (int i = start; i < count - 1; i++)
            {
                double qValue = qLo[i].Value;
                double? sValue = (double?)sResult[i + 1].LowerBand;
                if (!sValue.HasValue)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(qValue - sValue.Value) <= ValidationHelper.SkenderTolerance,
                    $"Period={period}, Mismatch at q[{i}] vs s[{i + 1}]: QuanTAlib={qValue:G17}, Skender={sValue.Value:G17}");
            }
        }
        _output.WriteLine("Dchannel lower band validated against Skender GetDonchian (offset +1)");
    }

    [Fact]
    public void Validate_Skender_Batch_Centerline()
    {
        // Same offset convention: QuanTAlib[i] == Skender[i+1]
        int[] periods = { 5, 10, 20, 50, 100 };

        foreach (var period in periods)
        {
            var (qMid, _, _) = Dchannel.Batch(_testData.Bars, period);
            var sResult = _testData.SkenderQuotes.GetDonchian(period).ToList();

            int count = Math.Min(qMid.Count, sResult.Count);
            int start = Math.Max(period + 1, count - 100);
            for (int i = start; i < count - 1; i++)
            {
                double qValue = qMid[i].Value;
                double? sValue = (double?)sResult[i + 1].Centerline;
                if (!sValue.HasValue)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(qValue - sValue.Value) <= ValidationHelper.SkenderTolerance,
                    $"Period={period}, Mismatch at q[{i}] vs s[{i + 1}]: QuanTAlib={qValue:G17}, Skender={sValue.Value:G17}");
            }
        }
        _output.WriteLine("Dchannel centerline validated against Skender GetDonchian (offset +1)");
    }

    [Fact]
    public void Validate_Skender_Streaming_UpperBand()
    {
        // Same offset convention: QuanTAlib[i] == Skender[i+1]
        int[] periods = { 10, 20, 50 };

        foreach (var period in periods)
        {
            var dchannel = new Dchannel(period);
            var qUpResults = new TSeries();
            foreach (var bar in _testData.Bars)
            {
                dchannel.Update(bar);
                qUpResults.Add(dchannel.Upper);
            }

            var sResult = _testData.SkenderQuotes.GetDonchian(period).ToList();

            int count = Math.Min(qUpResults.Count, sResult.Count);
            int start = Math.Max(period + 1, count - 100);
            for (int i = start; i < count - 1; i++)
            {
                double qValue = qUpResults[i].Value;
                double? sValue = (double?)sResult[i + 1].UpperBand;
                if (!sValue.HasValue)
                {
                    continue;
                }

                Assert.True(
                    Math.Abs(qValue - sValue.Value) <= ValidationHelper.SkenderTolerance,
                    $"Period={period}, Mismatch at q[{i}] vs s[{i + 1}]: QuanTAlib={qValue:G17}, Skender={sValue.Value:G17}");
            }
        }
        _output.WriteLine("Dchannel streaming upper band validated against Skender GetDonchian (offset +1)");
    }

    [Fact]
    public void Dchannel_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateDonchianChannels();
        var values = result.OutputValues.Values.First();
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
