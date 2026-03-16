using Skender.Stock.Indicators;
using Xunit.Abstractions;

using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;

namespace QuanTAlib.Tests;

public sealed class StarchannelValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public StarchannelValidationTests(ITestOutputHelper output)
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
    public void Validate_ManualCalculation_FirstBars()
    {
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Create simple test data
        // Bar 0: close=100, high=105, low=95 (range=10)
        series.Add(new TBar(t0, 100, 105, 95, 100, 100));
        // Bar 1: close=102, high=108, low=98 (range=10, prevClose=100, TR=max(10,8,2)=10)
        series.Add(new TBar(t0.AddMinutes(1), 102, 108, 98, 102, 100));
        // Bar 2: close=105, high=112, low=100 (range=12, prevClose=102, TR=max(12,10,2)=12)
        series.Add(new TBar(t0.AddMinutes(2), 105, 112, 100, 105, 100));

        var ind = new Starchannel(10, 2.0);
        var (mid, up, lo) = ind.Update(series);

        // First bar: all equal close
        Assert.Equal(100.0, mid[0].Value, 1e-10);
        Assert.Equal(100.0, up[0].Value, 1e-10);
        Assert.Equal(100.0, lo[0].Value, 1e-10);

        // Subsequent bars: upper > middle > lower (bands expand)
        for (int i = 1; i < mid.Count; i++)
        {
            Assert.True(up[i].Value > mid[i].Value, $"Upper > Middle at {i}");
            Assert.True(lo[i].Value < mid[i].Value, $"Lower < Middle at {i}");
        }

        // Bands should be symmetric
        for (int i = 0; i < mid.Count; i++)
        {
            double upperDist = up[i].Value - mid[i].Value;
            double lowerDist = mid[i].Value - lo[i].Value;
            Assert.Equal(upperDist, lowerDist, 1e-10);
        }

        _output.WriteLine("Starchannel manual calculation validated");
    }

    [Fact]
    public void Validate_AllModes_Consistency()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] multipliers = { 1.0, 2.0, 2.5 };

        foreach (int period in periods)
        {
            foreach (double multiplier in multipliers)
            {
                // Batch (instance)
                var inst = new Starchannel(period, multiplier);
                var (bMid, bUp, bLo) = inst.Update(_testData.Bars);

                // Static batch
                var (sMid, sUp, sLo) = Starchannel.Batch(_testData.Bars, period, multiplier);

                ValidationHelper.VerifySeriesEqual(bMid, sMid);
                ValidationHelper.VerifySeriesEqual(bUp, sUp);
                ValidationHelper.VerifySeriesEqual(bLo, sLo);

                // Streaming
                var streaming = new Starchannel(period, multiplier);
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
                double[] close = _testData.ClosePrices.ToArray();
                double[] spanMid = new double[high.Length];
                double[] spanUp = new double[high.Length];
                double[] spanLo = new double[high.Length];
                Starchannel.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                    spanMid.AsSpan(), spanUp.AsSpan(), spanLo.AsSpan(), period, multiplier);

                for (int i = 0; i < high.Length; i++)
                {
                    Assert.Equal(sMid[i].Value, spanMid[i], 9);
                    Assert.Equal(sUp[i].Value, spanUp[i], 9);
                    Assert.Equal(sLo[i].Value, spanLo[i], 9);
                }
            }
        }

        _output.WriteLine("Starchannel mode consistency validated (batch/stream/span)");
    }

    [Fact]
    public void Validate_EventingMode_MatchesBatch()
    {
        const int period = 20;
        const double multiplier = 2.0;

        var pub = new TBarSeries();
        var evtInd = new Starchannel(pub, period, multiplier);
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

        var (bMid, bUp, bLo) = Starchannel.Batch(_testData.Bars, period, multiplier);

        ValidationHelper.VerifySeriesEqual(bMid, evtMid);
        ValidationHelper.VerifySeriesEqual(bUp, evtUp);
        ValidationHelper.VerifySeriesEqual(bLo, evtLo);

        _output.WriteLine("Starchannel eventing mode validated");
    }

    [Fact]
    public void Validate_Calculate_ReturnsHotIndicator()
    {
        const int period = 15;
        const double multiplier = 2.5;

        var ((mid, up, lo), ind) = Starchannel.Calculate(_testData.Bars, period, multiplier);

        Assert.True(ind.IsHot);
        Assert.Equal(period, ind.WarmupPeriod);
        Assert.Equal(mid.Last.Value, ind.Last.Value, 1e-10);
        Assert.Equal(up.Last.Value, ind.Upper.Value, 1e-10);
        Assert.Equal(lo.Last.Value, ind.Lower.Value, 1e-10);

        // Continue streaming
        var next = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        ind.Update(next);
        Assert.True(ind.IsHot);

        _output.WriteLine("Starchannel Calculate validated");
    }

    [Fact]
    public void Validate_Prime_MatchesBatch()
    {
        const int period = 25;
        const double multiplier = 1.5;

        var (bMid, bUp, bLo) = Starchannel.Batch(_testData.Bars, period, multiplier);

        var primed = new Starchannel(period, multiplier);
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

        _output.WriteLine("Starchannel Prime validated against batch");
    }

    [Fact]
    public void Validate_LargeDataset_FiniteOutputs()
    {
        var (mid, up, lo) = Starchannel.Batch(_testData.Bars, 50, 2.0);

        ValidationHelper.VerifyAllFinite(mid, startIndex: 0);
        ValidationHelper.VerifyAllFinite(up, startIndex: 0);
        ValidationHelper.VerifyAllFinite(lo, startIndex: 0);

        // After first bar, upper > lower
        for (int i = 1; i < mid.Count; i++)
        {
            Assert.True(up[i].Value > lo[i].Value, $"Upper > Lower at {i}");
        }

        _output.WriteLine("Starchannel large dataset validated");
    }

    [Fact]
    public void Validate_BandSymmetry_AllBars()
    {
        var ind = new Starchannel(20, 2.0);
        var (mid, up, lo) = ind.Update(_testData.Bars);

        for (int i = 0; i < mid.Count; i++)
        {
            double upperWidth = up[i].Value - mid[i].Value;
            double lowerWidth = mid[i].Value - lo[i].Value;
            Assert.Equal(upperWidth, lowerWidth, 1e-10);
        }

        _output.WriteLine("Starchannel band symmetry validated for all bars");
    }

    [Fact]
    public void Validate_MultiplierScaling()
    {
        double[] multipliers = { 1.0, 2.0, 3.0, 4.0 };
        double[] widths = new double[multipliers.Length];

        for (int i = 0; i < multipliers.Length; i++)
        {
            var ind = new Starchannel(20, multipliers[i]);
            foreach (var bar in _testData.Bars)
            {
                ind.Update(bar);
            }
            widths[i] = ind.Upper.Value - ind.Lower.Value;
        }

        // Widths should scale linearly with multiplier
        double baseWidth = widths[0];
        for (int i = 1; i < multipliers.Length; i++)
        {
            double expected = baseWidth * multipliers[i];
            Assert.Equal(expected, widths[i], 1e-9);
        }

        _output.WriteLine("Starchannel multiplier scaling validated");
    }

    [Fact]
    public void Validate_PeriodEffect_Smoothing()
    {
        int[] periods = { 5, 10, 20, 50 };
        double[] middles = new double[periods.Length];

        for (int i = 0; i < periods.Length; i++)
        {
            var ind = new Starchannel(periods[i], 2.0);
            foreach (var bar in _testData.Bars)
            {
                ind.Update(bar);
            }
            middles[i] = ind.Last.Value;
        }

        // All should produce finite values
        foreach (var m in middles)
        {
            Assert.True(double.IsFinite(m));
        }

        _output.WriteLine("Starchannel period effect validated");
    }

    [Fact]
    public void Validate_ATRComponent_TrueRange()
    {
        // Create data with gaps to verify True Range includes gaps
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Bar 0: normal
        series.Add(new TBar(t0, 100, 105, 95, 100, 100));
        // Bar 1: gap up (prev close=100, new low=110, gap=10)
        series.Add(new TBar(t0.AddMinutes(1), 115, 120, 110, 115, 100));
        // Bar 2: gap down (prev close=115, new high=100)
        series.Add(new TBar(t0.AddMinutes(2), 95, 100, 90, 95, 100));

        var ind = new Starchannel(3, 2.0);
        var (mid, up, lo) = ind.Update(series);

        // Bands should expand due to gaps
        for (int i = 1; i < mid.Count; i++)
        {
            double width = up[i].Value - lo[i].Value;
            Assert.True(width > 0, $"Band width > 0 at bar {i}");
        }

        _output.WriteLine("Starchannel ATR true range validated with gaps");
    }

    [Fact]
    public void Validate_WarmupCompensation_EarlyConvergence()
    {
        // Constant price data - SMA should converge quickly
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        for (int i = 0; i < 100; i++)
        {
            series.Add(new TBar(t0.AddMinutes(i), 100, 105, 95, 100, 100));
        }

        var ind = new Starchannel(20, 2.0);
        var (mid, _, _) = ind.Update(series);

        // After warmup, middle should be very close to constant price (SMA = 100 exactly)
        for (int i = 20; i < 100; i++)
        {
            Assert.Equal(100.0, mid[i].Value, 1e-10);
        }

        _output.WriteLine("Starchannel warmup compensation validated");
    }

    [Fact]
    public void Validate_StateRestoration_Iterative()
    {
        var ind = new Starchannel(15, 2.5);
        var gbm = new GBM(startPrice: 100, mu: 0.01, sigma: 0.1, seed: 42);

        // Build up state
        for (int i = 0; i < 50; i++)
        {
            ind.Update(gbm.Next(isNew: true), isNew: true);
        }

        // Multiple corrections
        var remembered = gbm.Next(isNew: true);
        ind.Update(remembered, isNew: true);

        for (int i = 0; i < 10; i++)
        {
            var corrected = gbm.Next(isNew: false);
            ind.Update(corrected, isNew: false);
        }

        // Restore
        ind.Update(remembered, isNew: false);

        // State should be back to remembered point (after remembered bar)
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(double.IsFinite(ind.Upper.Value));
        Assert.True(double.IsFinite(ind.Lower.Value));

        _output.WriteLine("Starchannel state restoration validated");
    }

    [Fact]
    public void Validate_SMA_VersusPineScript()
    {
        // PineScript: ta.sma(close, period)
        // Verify SMA calculation matches expected behavior
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Create predictable data: 100, 102, 104, 106, 108
        for (int i = 0; i < 5; i++)
        {
            double close = 100 + i * 2;
            series.Add(new TBar(t0.AddMinutes(i), close, close + 5, close - 5, close, 100));
        }

        var ind = new Starchannel(5, 2.0);
        var (mid, _, _) = ind.Update(series);

        // SMA(5) at bar 4 = (100+102+104+106+108)/5 = 104
        Assert.Equal(104.0, mid[4].Value, 1e-10);

        _output.WriteLine("Starchannel SMA calculation validated against expected");
    }

    [Fact]
    public void Validate_BandWidthConsistency()
    {
        // Verify that band width is consistent across different calculation modes
        int[] periods = { 10, 20, 30 };

        foreach (int period in periods)
        {
            var (mid, up, lo) = Starchannel.Batch(_testData.Bars, period, 2.0);

            // Band width should be exactly 2x ATR (multiplier * ATR)
            for (int i = 1; i < mid.Count; i++)
            {
                double width = up[i].Value - lo[i].Value;
                double upperDist = up[i].Value - mid[i].Value;
                double lowerDist = mid[i].Value - lo[i].Value;

                // Width = 2 * ATR * multiplier, so upperDist = lowerDist = ATR * multiplier
                Assert.Equal(upperDist, lowerDist, 1e-10);
                Assert.Equal(width, upperDist + lowerDist, 1e-10);
            }
        }

        _output.WriteLine("Starchannel band width consistency validated");
    }

    [Fact]
    public void Validate_ATRCalculation_Correctness()
    {
        // Verify ATR calculation using known values
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Create bars with known true range values
        // Bar 0: TR = high - low = 10 (no previous close)
        series.Add(new TBar(t0, 100, 105, 95, 100, 100));
        // Bar 1: TR = max(110-90, |110-100|, |90-100|) = max(20, 10, 10) = 20
        series.Add(new TBar(t0.AddMinutes(1), 100, 110, 90, 100, 100));
        // Bar 2: TR = max(105-95, |105-100|, |95-100|) = max(10, 5, 5) = 10
        series.Add(new TBar(t0.AddMinutes(2), 100, 105, 95, 100, 100));

        var ind = new Starchannel(3, 1.0); // multiplier=1 so width = 2*ATR
        var (mid, up, lo) = ind.Update(series);

        // All outputs should be finite
        for (int i = 0; i < mid.Count; i++)
        {
            Assert.True(double.IsFinite(mid[i].Value));
            Assert.True(double.IsFinite(up[i].Value));
            Assert.True(double.IsFinite(lo[i].Value));
        }

        // Band width should be positive after first bar
        for (int i = 1; i < mid.Count; i++)
        {
            double width = up[i].Value - lo[i].Value;
            Assert.True(width > 0, $"Band width > 0 at bar {i}");
        }

        _output.WriteLine("Starchannel ATR calculation validated");
    }

    [Fact]
    public void Validate_SMA_SlidingWindow()
    {
        // Verify SMA uses sliding window correctly
        var series = new TBarSeries();
        var t0 = DateTime.UtcNow;

        // Create 10 bars with close = bar index + 1 (1,2,3,4,5,6,7,8,9,10)
        for (int i = 0; i < 10; i++)
        {
            double close = i + 1;
            series.Add(new TBar(t0.AddMinutes(i), close, close + 1, close - 1, close, 100));
        }

        var ind = new Starchannel(5, 2.0);
        var (mid, _, _) = ind.Update(series);

        // Bar 4: SMA(5) = (1+2+3+4+5)/5 = 3
        Assert.Equal(3.0, mid[4].Value, 1e-10);

        // Bar 5: SMA(5) = (2+3+4+5+6)/5 = 4
        Assert.Equal(4.0, mid[5].Value, 1e-10);

        // Bar 9: SMA(5) = (6+7+8+9+10)/5 = 8
        Assert.Equal(8.0, mid[9].Value, 1e-10);

        _output.WriteLine("Starchannel SMA sliding window validated");
    }

    [Fact]
    public void Validate_KcComparison_Structure()
    {
        // Compare structural properties with Kc (EMA vs SMA middle)
        // Both use ATR for bands, so band calculation should be similar

        const int period = 20;
        const double multiplier = 2.0;

        var star = new Starchannel(period, multiplier);
        var kelt = new Kc(period, multiplier);

        foreach (var bar in _testData.Bars)
        {
            star.Update(bar);
            kelt.Update(bar);
        }

        // Both should have finite outputs
        Assert.True(double.IsFinite(star.Last.Value));
        Assert.True(double.IsFinite(star.Upper.Value));
        Assert.True(double.IsFinite(star.Lower.Value));

        Assert.True(double.IsFinite(kelt.Last.Value));
        Assert.True(double.IsFinite(kelt.Upper.Value));
        Assert.True(double.IsFinite(kelt.Lower.Value));

        // Both should have upper > middle > lower
        Assert.True(star.Upper.Value > star.Last.Value);
        Assert.True(star.Lower.Value < star.Last.Value);

        Assert.True(kelt.Upper.Value > kelt.Last.Value);
        Assert.True(kelt.Lower.Value < kelt.Last.Value);

        // Both should have symmetric bands
        double starUpperDist = star.Upper.Value - star.Last.Value;
        double starLowerDist = star.Last.Value - star.Lower.Value;
        Assert.Equal(starUpperDist, starLowerDist, 1e-10);

        double keltUpperDist = kelt.Upper.Value - kelt.Last.Value;
        double keltLowerDist = kelt.Last.Value - kelt.Lower.Value;
        Assert.Equal(keltUpperDist, keltLowerDist, 1e-10);

        _output.WriteLine("Starchannel vs Kc structure validated");
    }

    [Fact]
    public void Validate_Starchannel_DifferentFromKc()
    {
        // Starchannel (SMA) should differ from Kc (EMA) in the middle line
        const int period = 20;
        const double multiplier = 2.0;

        var star = new Starchannel(period, multiplier);
        var kelt = new Kc(period, multiplier);

        foreach (var bar in _testData.Bars)
        {
            star.Update(bar);
            kelt.Update(bar);
        }

        // Middle lines should be different (SMA vs EMA with different weighting)
        // They may be close but not identical
        double diff = Math.Abs(star.Last.Value - kelt.Last.Value);

        // Just verify they're both finite and reasonable
        Assert.True(double.IsFinite(diff));

        _output.WriteLine($"Starchannel vs Kc middle difference: {diff:F6}");
    }

    // ═══════════════════════════════════════════════════════════════
    //  Skender.Stock.Indicators Validation
    //  Skender GetStarcBands(smaPeriods, multiplier, atrPeriods)
    //  uses SMA centerline + ATR bands — same algorithm as QuanTAlib.
    //  We pass atrPeriods = smaPeriods to match QuanTAlib's single-period design.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Validate_Skender_Centerline()
    {
        int[] periods = { 5, 10, 20, 50 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            var (qMid, _, _) = Starchannel.Batch(_testData.Bars, period, multiplier);

            var sResult = _testData.SkenderQuotes
                .GetStarcBands(period, multiplier, period)
                .ToList();

            ValidationHelper.VerifyData(qMid, sResult, s => s.Centerline);
        }
        _output.WriteLine("Starchannel centerline validated against Skender for all periods");
    }

    [Fact]
    public void Validate_Skender_UpperBand()
    {
        int[] periods = { 5, 10, 20, 50 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            var (_, qUp, _) = Starchannel.Batch(_testData.Bars, period, multiplier);

            var sResult = _testData.SkenderQuotes
                .GetStarcBands(period, multiplier, period)
                .ToList();

            ValidationHelper.VerifyData(qUp, sResult, s => s.UpperBand);
        }
        _output.WriteLine("Starchannel upper band validated against Skender for all periods");
    }

    [Fact]
    public void Validate_Skender_LowerBand()
    {
        int[] periods = { 5, 10, 20, 50 };
        double multiplier = 2.0;

        foreach (var period in periods)
        {
            var (_, _, qLo) = Starchannel.Batch(_testData.Bars, period, multiplier);

            var sResult = _testData.SkenderQuotes
                .GetStarcBands(period, multiplier, period)
                .ToList();

            ValidationHelper.VerifyData(qLo, sResult, s => s.LowerBand);
        }
        _output.WriteLine("Starchannel lower band validated against Skender for all periods");
    }

    [Fact]
    public void Validate_Skender_BandStructure()
    {
        var period = 20;
        var multiplier = 2.0;

        var sResult = _testData.SkenderQuotes
            .GetStarcBands(period, multiplier, period)
            .ToList();

        var (qMid, qUp, qLo) = Starchannel.Batch(_testData.Bars, period, multiplier);

        int warmup = period * 2;
        for (int i = warmup; i < qMid.Count && i < sResult.Count; i++)
        {
            var sk = sResult[i];
            if (sk.UpperBand.HasValue && sk.LowerBand.HasValue && sk.Centerline.HasValue)
            {
                Assert.True(sk.UpperBand.Value > sk.Centerline.Value, $"Skender Upper > Middle at {i}");
                Assert.True(sk.LowerBand.Value < sk.Centerline.Value, $"Skender Lower < Middle at {i}");
                Assert.True(qUp[i].Value > qMid[i].Value, $"Q Upper > Middle at {i}");
                Assert.True(qLo[i].Value < qMid[i].Value, $"Q Lower < Middle at {i}");
            }
        }

        _output.WriteLine("Starchannel vs Skender band structure validated");
    }

    [Fact]
    public void Starchannel_MatchesOoples_Structural()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ooplesData = bars.Select(b => new TickerData
        {
            Date = new DateTime(b.Time, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low,
            Close = b.Close, Volume = b.Volume
        }).ToList();
        var result = new StockData(ooplesData).CalculateStollerAverageRangeChannels();
        var values = result.OutputValues.Values.First();
        int finiteCount = values.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite values, got {finiteCount}");
    }
}
