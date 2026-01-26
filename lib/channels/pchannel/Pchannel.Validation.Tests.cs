using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class PchannelValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public PchannelValidationTests(ITestOutputHelper output)
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

        var ind = new Pchannel(3);
        var (mid, up, lo) = ind.Update(series);

        Assert.Equal(16.0, up.Last.Value, 1e-10);
        Assert.Equal(8.0, lo.Last.Value, 1e-10);
        Assert.Equal(12.0, mid.Last.Value, 1e-10);
        Assert.True(ind.IsHot);

        _output.WriteLine("Pchannel manual period-3 calculation validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            var inst = new Pchannel(period);
            var (bMid, bUp, bLo) = inst.Update(_testData.Bars);

            var (sMid, sUp, sLo) = Pchannel.Batch(_testData.Bars, period);

            ValidationHelper.VerifySeriesEqual(bMid, sMid);
            ValidationHelper.VerifySeriesEqual(bUp, sUp);
            ValidationHelper.VerifySeriesEqual(bLo, sLo);

            var streaming = new Pchannel(period);
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

            double[] high = _testData.HighPrices.ToArray();
            double[] low = _testData.LowPrices.ToArray();
            double[] spanMid = new double[high.Length];
            double[] spanUp = new double[high.Length];
            double[] spanLo = new double[high.Length];
            Pchannel.Batch(high.AsSpan(), low.AsSpan(),
                spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan(), period);

            for (int i = 0; i < high.Length; i++)
            {
                Assert.Equal(sMid[i].Value, spanMid[i], 9);
                Assert.Equal(sUp[i].Value, spanUp[i], 9);
                Assert.Equal(sLo[i].Value, spanLo[i], 9);
            }
        }

        _output.WriteLine("Pchannel mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        var pub = new TBarSeries();
        var evtInd = new Pchannel(pub, period);
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

        var (bMid, bUp, bLo) = Pchannel.Batch(_testData.Bars, period);

        ValidationHelper.VerifySeriesEqual(bMid, evtMid);
        ValidationHelper.VerifySeriesEqual(bUp, evtUp);
        ValidationHelper.VerifySeriesEqual(bLo, evtLo);

        _output.WriteLine("Pchannel eventing mode validated");
    }

    [Fact]
    public void Validate_AgainstDchannel_ExactMatch()
    {
        // Pchannel should produce identical results to Dchannel
        int[] periods = { 10, 20, 50 };

        foreach (int period in periods)
        {
            var (dcMid, dcUp, dcLo) = Dchannel.Batch(_testData.Bars, period);
            var (pcMid, pcUp, pcLo) = Pchannel.Batch(_testData.Bars, period);

            ValidationHelper.VerifySeriesEqual(dcMid, pcMid);
            ValidationHelper.VerifySeriesEqual(dcUp, pcUp);
            ValidationHelper.VerifySeriesEqual(dcLo, pcLo);
        }

        _output.WriteLine("Pchannel matches Dchannel exactly");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        var ((mid, up, lo), ind) = Pchannel.Calculate(_testData.Bars, period);

        Assert.True(ind.IsHot);
        Assert.Equal(period, ind.WarmupPeriod);
        Assert.Equal(mid.Last.Value, ind.Last.Value, 1e-10);
        Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
        Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

        var next = new TBar(DateTime.UtcNow, 0, 150, 50, 100, 1000);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("Pchannel Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;

        var (bMid, bUp, bLo) = Pchannel.Batch(_testData.Bars, period);

        var primed = new Pchannel(period);
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

        _output.WriteLine("Pchannel Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, up, lo) = Pchannel.Batch(_testData.Bars, 50);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        for (int i = 50; i < mid.Count; i++)
        {
            Assert.True(up[i].Value >= lo[i].Value, $"Upper >= Lower at {i}");
        }

        _output.WriteLine("Pchannel large dataset validated");
    }

    [Fact]
    public void Validate_StateRestoration_Iterative()
    {
        var ind = new Pchannel(15);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            ind.Update(gbm.Next(isNew: true), isNew: true);
        }

        var remembered = gbm.Next(isNew: true);
        ind.Update(remembered, isNew: true);

        var savedMid = ind.Last.Value;
        var savedUp = ind.Upper.Value;
        var savedLo = ind.Lower.Value;

        for (int i = 0; i < 10; i++)
        {
            var corrected = gbm.Next(isNew: false);
            ind.Update(corrected, isNew: false);
        }

        ind.Update(remembered, isNew: false);

        Assert.Equal(savedMid, ind.Last.Value, 1e-10);
        Assert.Equal(savedUp, ind.Upper.Value, 1e-10);
        Assert.Equal(savedLo, ind.Lower.Value, 1e-10);

        _output.WriteLine("Pchannel state restoration validated");
    }
}
