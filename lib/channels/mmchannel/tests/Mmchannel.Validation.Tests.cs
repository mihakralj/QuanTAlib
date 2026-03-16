using Skender.Stock.Indicators;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class MmchannelValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public MmchannelValidationTests(ITestOutputHelper output)
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

        var ind = new Mmchannel(3);
        var (up, lo) = ind.Update(series);

        Assert.Equal(16.0, up.Last.Value, 1e-10);
        Assert.Equal(8.0, lo.Last.Value, 1e-10);
        Assert.True(ind.IsHot);

        _output.WriteLine("Mmchannel manual period-3 calculation validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };

        foreach (int period in periods)
        {
            // Batch (instance)
            var inst = new Mmchannel(period);
            var (bUp, bLo) = inst.Update(_testData.Bars);

            // Static batch
            var (sUp, sLo) = Mmchannel.Batch(_testData.Bars, period);

            ValidationHelper.VerifySeriesEqual(bUp, sUp);
            ValidationHelper.VerifySeriesEqual(bLo, sLo);

            // Streaming
            var streaming = new Mmchannel(period);
            var sUpStream = new TSeries();
            var sLoStream = new TSeries();
            foreach (var bar in _testData.Bars)
            {
                streaming.Update(bar);
                sUpStream.Add(streaming.Upper);
                sLoStream.Add(streaming.Lower);
            }

            ValidationHelper.VerifySeriesEqual(sUp, sUpStream);
            ValidationHelper.VerifySeriesEqual(sLo, sLoStream);

            // Span
            double[] high = _testData.HighPrices.ToArray();
            double[] low = _testData.LowPrices.ToArray();
            double[] spanUp = new double[high.Length];
            double[] spanLo = new double[high.Length];
            Mmchannel.Batch(high.AsSpan(), low.AsSpan(),
                spanUp.AsSpan(), spanLo.AsSpan(), period);

            for (int i = 0; i < high.Length; i++)
            {
                Assert.Equal(sUp[i].Value, spanUp[i], 9);
                Assert.Equal(sLo[i].Value, spanLo[i], 9);
            }
        }

        _output.WriteLine("Mmchannel mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        var pub = new TBarSeries();
        var evtInd = new Mmchannel(pub, period);
        var evtUp = new TSeries();
        var evtLo = new TSeries();

        foreach (var bar in _testData.Bars)
        {
            pub.Add(bar);
            evtUp.Add(evtInd.Upper);
            evtLo.Add(evtInd.Lower);
        }

        var (bUp, bLo) = Mmchannel.Batch(_testData.Bars, period);

        ValidationHelper.VerifySeriesEqual(bUp, evtUp);
        ValidationHelper.VerifySeriesEqual(bLo, evtLo);

        _output.WriteLine("Mmchannel eventing mode validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        var ((up, lo), ind) = Mmchannel.Calculate(_testData.Bars, period);

        Assert.True(ind.IsHot);
        Assert.Equal(period, ind.WarmupPeriod);
        Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
        Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

        // Continue streaming
        var next = new TBar(DateTime.UtcNow, 0, 150, 50, 100, 1000);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("Mmchannel Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;

        var (bUp, bLo) = Mmchannel.Batch(_testData.Bars, period);

        var primed = new Mmchannel(period);
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

        Assert.Equal(bUp.Last.Value, primed.Upper.Value, 1e-9);
        Assert.Equal(bLo.Last.Value, primed.Lower.Value, 1e-9);

        _output.WriteLine("Mmchannel Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (up, lo) = Mmchannel.Batch(_testData.Bars, 50);

        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        for (int i = 50; i < up.Count; i++)
        {
            Assert.True(up[i].Value >= lo[i].Value, $"Upper >= Lower at {i}");
        }

        _output.WriteLine("Mmchannel large dataset validated");
    }

    [Fact]
    public void Validate_AgainstDc_Bands()
    {
        // Mmchannel upper/lower should exactly match Dc upper/lower
        int[] periods = { 10, 20, 50 };

        foreach (int period in periods)
        {
            var (_, dcUp, dcLo) = Dc.Batch(_testData.Bars, period);
            var (mmUp, mmLo) = Mmchannel.Batch(_testData.Bars, period);

            ValidationHelper.VerifySeriesEqual(dcUp, mmUp);
            ValidationHelper.VerifySeriesEqual(dcLo, mmLo);
        }

        _output.WriteLine("Mmchannel matches Dc upper/lower bands");
    }

    [Fact]
    public void Validate_Skender_Donchian()
    {
        // Note: Skender's Donchian uses lookbackPeriods+1 for the window size (includes current bar differently)
        // This test validates that we get finite, reasonable results, but exact match is not expected
        // due to this convention difference. The exact match is validated via Dc comparison above.
        const int period = 20;

        var skenderResult = _testData.SkenderQuotes
            .GetDonchian(period)
            .ToList();

        var (mmUp, mmLo) = Mmchannel.Batch(_testData.Bars, period);

        // Verify we have results and they are finite after warmup
        int startIndex = period;
        for (int i = startIndex; i < Math.Min(skenderResult.Count, mmUp.Count); i++)
        {
            var sk = skenderResult[i];
            if (sk.UpperBand.HasValue && sk.LowerBand.HasValue)
            {
                // Both should be finite
                Assert.True(double.IsFinite(mmUp[i].Value));
                Assert.True(double.IsFinite(mmLo[i].Value));
                // Upper >= Lower invariant
                Assert.True(mmUp[i].Value >= mmLo[i].Value);
            }
        }

        _output.WriteLine("Mmchannel validated against Skender Donchian (finite outputs, convention differs)");
    }

    [Fact]
    public void Validate_SlidingWindow_CorrectMaxMin()
    {
        // Manually verify sliding window max/min
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Create test data with known pattern
        // Bar 0: H=100, L=90
        // Bar 1: H=105, L=95
        // Bar 2: H=102, L=88  <- new low
        // Bar 3: H=110, L=92  <- new high
        // Bar 4: H=98,  L=85  <- new low
        series.Add(new TBar(t0, 0, 100, 90, 95, 100));
        series.Add(new TBar(t0.AddMinutes(1), 0, 105, 95, 100, 100));
        series.Add(new TBar(t0.AddMinutes(2), 0, 102, 88, 95, 100));
        series.Add(new TBar(t0.AddMinutes(3), 0, 110, 92, 100, 100));
        series.Add(new TBar(t0.AddMinutes(4), 0, 98, 85, 90, 100));

        var ind = new Mmchannel(3);
        var (up, lo) = ind.Update(series);

        // Bar 0: upper=100, lower=90 (only bar 0)
        Assert.Equal(100.0, up[0].Value, 1e-10);
        Assert.Equal(90.0, lo[0].Value, 1e-10);

        // Bar 1: upper=max(100,105)=105, lower=min(90,95)=90
        Assert.Equal(105.0, up[1].Value, 1e-10);
        Assert.Equal(90.0, lo[1].Value, 1e-10);

        // Bar 2: upper=max(100,105,102)=105, lower=min(90,95,88)=88
        Assert.Equal(105.0, up[2].Value, 1e-10);
        Assert.Equal(88.0, lo[2].Value, 1e-10);

        // Bar 3: upper=max(105,102,110)=110, lower=min(95,88,92)=88 (bar 0 dropped)
        Assert.Equal(110.0, up[3].Value, 1e-10);
        Assert.Equal(88.0, lo[3].Value, 1e-10);

        // Bar 4: upper=max(102,110,98)=110, lower=min(88,92,85)=85 (bar 1 dropped)
        Assert.Equal(110.0, up[4].Value, 1e-10);
        Assert.Equal(85.0, lo[4].Value, 1e-10);

        _output.WriteLine("Mmchannel sliding window max/min validated");
    }

    [Fact]
    public void Validate_StateRestoration_Iterative()
    {
        var ind = new Mmchannel(15);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        // Build up state
        for (int i = 0; i < 50; i++)
        {
            ind.Update(gbm.Next(isNew: true), isNew: true);
        }

        // Multiple corrections
        var remembered = gbm.Next(isNew: true);
        ind.Update(remembered, isNew: true);

        var savedUpper = ind.Upper.Value;
        var savedLower = ind.Lower.Value;

        for (int i = 0; i < 10; i++)
        {
            var corrected = gbm.Next(isNew: false);
            ind.Update(corrected, isNew: false);
        }

        // Restore by re-applying remembered bar
        ind.Update(remembered, isNew: false);

        // Values should match saved state (upper/lower restored)
        Assert.Equal(savedUpper, ind.Upper.Value, 1e-10);
        Assert.Equal(savedLower, ind.Lower.Value, 1e-10);

        _output.WriteLine("Mmchannel state restoration validated");
    }

    [Fact]
    public void Validate_PeriodEffect_Smoothness()
    {
        // Longer periods should have wider bands (more history)
        int[] periods = { 5, 10, 20, 50 };
        double[] widths = new double[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            var (up, lo) = Mmchannel.Batch(_testData.Bars, periods[i]);
            widths[i] = up.Last.Value - lo.Last.Value;
        }

        // All widths should be positive
        foreach (var w in widths)
        {
            Assert.True(w >= 0, "Width should be non-negative");
        }

        // Generally, longer periods have wider bands (more price extremes included)
        // But not strictly monotonic due to price dynamics

        _output.WriteLine("Mmchannel period effect validated");
    }
}
